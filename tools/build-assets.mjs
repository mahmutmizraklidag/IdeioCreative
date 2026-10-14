import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import postcss from 'postcss';
import { minify } from 'terser';
import { parse as parseJavaScript } from 'acorn';
import assert from 'node:assert/strict';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const webRoot = path.join(root, 'IdeioCreative/wwwroot');
const check = process.argv.includes('--check');
const cssBundles = {
    'assets/css/site.bundle.min.css': [
        'assets/css/bootstrap.min.css', 'assets/css/all.min.css', 'assets/css/magnific-popup.css',
        'assets/css/nice-select.css', 'assets/css/swiper.min.css', 'assets/css/aos.css',
        'assets/css/main.css', 'css/reference-logos.css', 'assets/vendor/bootstrap-icons/bootstrap-icons.css'
    ],
    'assets/css/ad-audit.bundle.min.css': ['assets/css/main.css', 'assets/css/all.min.css', 'css/ad-audit.css'],
    'css/home-page.min.css': ['css/home-hero.css', 'css/home-page.css'],
    'css/recovery.min.css': ['css/recovery.css'],
    'css/faq.min.css': ['css/faq.css'],
    'css/site.min.css': ['css/site.css']
};
const jsBundles = {
    'assets/js/site.bundle.min.js': [
        'assets/js/jquery.js', 'assets/js/bootstrap.bundle.min.js', 'assets/js/jquery.waypoints.js',
        'assets/js/magnific-popup.js', 'assets/js/jquery.nice-select.min.js', 'assets/js/swiper.js',
        'assets/js/jquery.counterup.min.js', 'assets/js/aos.js', 'assets/js/vanilla-tilt.min.js',
        'assets/js/main.js', 'js/quote-drawer.js'
    ],
    'js/contact.min.js': ['js/contact.js'],
    'js/home-page.min.js': ['js/home-page.js'],
    'js/service-page.min.js': ['js/service-page.js'],
    'js/ad-audit.min.js': ['js/ad-audit.js'],
    'js/admin-service-faqs.min.js': ['js/admin-service-faqs.js']
};
const results = [];

function parsePreservingLegacyCss(source, inputPath) {
    const markers = [];
    for (let attempt = 0; attempt < 20; attempt++) {
        try {
            return { root: postcss.parse(source, { from: inputPath }), markers };
        } catch (error) {
            if (error.reason !== 'Unexpected }') throw error;
            const lines = source.split('\n');
            const offset = lines.slice(0, error.line - 1).reduce((sum, line) => sum + line.length + 1, 0)
                + error.column - 1;
            if (source[offset] !== '}') throw error;
            const marker = `__ideio_existing_unmatched_close_${markers.length}__`;
            markers.push(marker);
            // Keep the existing token at its original position. Removing it could change CSS error recovery.
            source = source.slice(0, offset) + `/*${marker}*/` + source.slice(offset + 1);
        }
    }
    throw new Error(`Too many CSS syntax errors in ${inputPath}`);
}

function compactCss(root) {
    root.walk(node => {
        if (node.type === 'comment' && !/^!|@license|@preserve|copyright|license|__ideio_existing_unmatched_close_/i.test(node.text)) {
            node.remove();
            return;
        }
        node.raws.before = '';
        node.raws.after = '';
        // Preserve custom-property whitespace, selector text, declaration values and all rule ordering.
        if (node.type === 'decl' && !node.prop.startsWith('--')) node.raws.between = ':';
        if (node.type === 'rule' || node.type === 'atrule') node.raws.between = '';
        if (node.important) node.raws.important = '!important';
        if (node.type === 'comment') { node.raws.left = ''; node.raws.right = ''; }
    });
    root.raws.after = '';
    return root.toString();
}

function cssSignature(node) {
    if (node.type === 'comment') return null;
    const signature = { type: node.type };
    for (const key of ['selector', 'name', 'params', 'prop', 'value'])
        if (node[key] !== undefined) signature[key] = node[key];
    if (node.type === 'decl') signature.important = Boolean(node.important);
    if (node.nodes) signature.nodes = node.nodes.map(cssSignature).filter(Boolean);
    return signature;
}

function javascriptSignature(source) {
    const ast = parseJavaScript(source, { ecmaVersion: 'latest', sourceType: 'script' });
    return JSON.stringify(ast, (key, value) => {
        if (['start', 'end', 'loc', 'range', 'raw'].includes(key)) return undefined;
        if (value instanceof RegExp) return value.toString();
        // Quoted and unquoted ordinary object keys have the same JavaScript semantics.
        if (value && ['Property', 'MethodDefinition'].includes(value.type) && !value.computed) {
            const property = { ...value };
            if (property.key.type === 'Identifier') property.key = { type: 'CanonicalKey', name: property.key.name };
            else if (property.key.type === 'Literal') property.key = { type: 'CanonicalKey', name: String(property.key.value) };
            return property;
        }
        return value;
    });
}

async function emit(output, content, originalBytes) {
    const destination = path.join(webRoot, output);
    if (check) {
        const existing = await fs.readFile(destination, 'utf8').catch(() => '');
        if (existing !== content) throw new Error(`${output} is stale. Run npm run build:assets.`);
    } else {
        await fs.mkdir(path.dirname(destination), { recursive: true });
        await fs.writeFile(destination, content, 'utf8');
    }
    results.push({ output, originalBytes, minifiedBytes: Buffer.byteLength(content) });
}

function rebaseUrls(value, input, output) {
    return value.replace(/url\(\s*(?:"([^"]*)"|'([^']*)'|([^)]*))\s*\)/gi, (match, doubleQuoted, singleQuoted, unquoted) => {
        const url = (doubleQuoted ?? singleQuoted ?? unquoted).trim();
        if (/^(?:[a-z][a-z0-9+.-]*:|\/|#)/i.test(url)) return match;
        const suffixPosition = url.search(/[?#]/);
        const resource = suffixPosition < 0 ? url : url.slice(0, suffixPosition);
        const suffix = suffixPosition < 0 ? '' : url.slice(suffixPosition);
        const absolute = path.resolve(path.dirname(input), resource);
        const rebased = path.relative(path.dirname(output), absolute).split(path.sep).join('/');
        return `url("${rebased}${suffix}")`;
    });
}

for (const [output, inputs] of Object.entries(cssBundles)) {
    const merged = postcss.root();
    const imports = [];
    const markers = [];
    let originalBytes = 0;
    for (const input of inputs) {
        const inputPath = path.join(webRoot, input);
        const source = await fs.readFile(inputPath, 'utf8');
        originalBytes += Buffer.byteLength(source);
        const preserved = parsePreservingLegacyCss(source, inputPath);
        const parsed = preserved.root;
        // Marker text includes a per-input prefix to avoid collisions between bundled files.
        for (const marker of preserved.markers) {
            const unique = `${marker}_${markers.length}`;
            parsed.walkComments(comment => { if (comment.text === marker) comment.text = unique; });
            markers.push(unique);
        }
        parsed.walkDecls(declaration => {
            declaration.value = rebaseUrls(declaration.value, inputPath, path.join(webRoot, output));
        });
        parsed.walkAtRules('charset', declaration => declaration.remove());
        parsed.walkAtRules('import', declaration => {
            imports.push(declaration.clone());
            declaration.remove();
        });
        merged.append(parsed.nodes);
    }
    // CSS imports must precede all style rules, including when main.css is bundled after Bootstrap.
    for (const declaration of imports.reverse()) merged.prepend(declaration);
    const expectedSignature = cssSignature(merged);
    let compact = compactCss(merged);
    for (const marker of markers) compact = compact.replace(`/*${marker}*/`, '}');
    assert.deepEqual(cssSignature(parsePreservingLegacyCss(compact, output).root), expectedSignature,
        `CSS rule order or declaration values changed in ${output}`);
    await emit(output, compact + '\n', originalBytes);
}

for (const [output, inputs] of Object.entries(jsBundles)) {
    let combined = '';
    let originalBytes = 0;
    for (const input of inputs) {
        const source = await fs.readFile(path.join(webRoot, input), 'utf8');
        originalBytes += Buffer.byteLength(source);
        // Preserve names, properties and execution semantics; only remove formatting and ordinary comments.
        const minified = await minify(source, {
            compress: false, mangle: false,
            format: { comments: /^!|@license|@preserve|copyright|license/i }
        });
        if (!minified.code) throw new Error(`No generated JavaScript for ${input}`);
        assert.equal(javascriptSignature(minified.code), javascriptSignature(source),
            `JavaScript semantics changed while minifying ${input}`);
        // Keep each vendor's directive prologue and scope. UMD wrappers still receive the browser window.
        combined += inputs.length > 1
            ? `/* ${input} */\n(function(){\n${minified.code}\n}).call(window);\n`
            : `/* ${input} */\n${minified.code}\n;\n`;
    }
    await emit(output, combined, originalBytes);
}

for (const result of results) {
    console.log(`${check ? 'Checked' : 'Built'} ${result.output}: ${result.originalBytes} → ${result.minifiedBytes} bytes (${Math.round((1 - result.minifiedBytes / result.originalBytes) * 100)}% smaller)`);
}
