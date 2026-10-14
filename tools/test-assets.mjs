import fs from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const webRoot = path.join(root, 'IdeioCreative/wwwroot');
const bundle = fs.readFileSync(path.join(webRoot, 'assets/js/site.bundle.min.js'), 'utf8');
function bundledChunk(input) {
    const start = bundle.indexOf(`/* ${input} */`);
    assert.ok(start >= 0, `Missing bundled dependency: ${input}`);
    const next = bundle.indexOf('\n/* ', start + 3);
    return bundle.slice(start, next < 0 ? undefined : next);
}

function environment() {
    const groups = new Map();
    const ready = [];
    const documentEvents = new Map();
    const requests = [];
    const sliders = [];
    const timers = [];
    const document = { body: {}, documentElement: { scrollTop: 0, scrollHeight: 1000, clientHeight: 600 },
        getElementById: () => null, querySelector: () => null, querySelectorAll: () => [],
        addEventListener: (event, callback) => documentEvents.set(event, callback) };
    function group(key) {
        if (groups.has(key)) return groups.get(key);
        const state = { classes: new Set(), attributes: {}, events: {}, visible: false, value: 'fixture', resets: 0 };
        const element = { key, reset() { state.resets++; } };
        const result = { 0: element, state,
            ready(callback) { ready.push(callback); return this; },
            on(event, callback) { state.events[event] = callback; return this; },
            addClass(value) { state.classes.add(value); return this; },
            removeClass(value) { state.classes.delete(value); return this; },
            toggleClass(value) { state.classes.has(value) ? state.classes.delete(value) : state.classes.add(value); return this; },
            hasClass(value) { return state.classes.has(value); },
            attr(name, value) { if (value !== undefined) state.attributes[name] = value; return value === undefined ? state.attributes[name] : this; },
            val(value) { if (value !== undefined) state.value = value; return value === undefined ? state.value : this; },
            show() { state.visible = true; return this; }, hide() { state.visible = false; return this; },
            prop(name, value) { state.attributes[name] = value; return this; },
            html(value) { state.html = value; return this; }, text(value) { state.text = value; return this; },
            find(selector) { return group(key + ' ' + selector); },
            serialize() { return 'Name=Fixture&Email=fixture%40example.test'; },
            slideToggle() { state.visible = !state.visible; return this; },
            siblings() { return group(key + ' siblings'); }, scrollTop() { return 0; },
            clone() { return this; }, appendTo() { return this; }, animate() { return this; }, fadeOut() { return this; },
            counterUp() { return this; }, magnificPopup() { return this; }, niceSelect() { return this; },
            closest() { return this; }, next() { return this; }, not() { return this; }, slideUp() { return this; }, delay() { return this; }
        };
        groups.set(key, result);
        return result;
    }
    const context = { document, console, Date,
        setTimeout(callback) { timers.push(callback); return timers.length; },
        VanillaTilt: { init() {} }, AOS: { init() {} },
        Swiper: function(selector, options) { sliders.push({ selector, options }); } };
    context.window = context;
    const $ = target => group(target === document ? 'document' : target === context ? 'window' :
        target === document.body ? 'document.body' : typeof target === 'string' ? target : target.key);
    $.ajax = request => requests.push(request);
    context.$ = context.jQuery = $;
    vm.createContext(context);
    return { context, group, ready, documentEvents, requests, sliders, timers,
        run(code) { vm.runInContext(code, context); },
        start() { ready.forEach(callback => callback()); },
        trigger(selector, event, data = {}) { group(selector).state.events[event].call(group(selector)[0], data); } };
}

for (const minified of [false, true]) {
    const label = minified ? 'minified' : 'source';
    const app = environment();
    app.run(minified ? bundledChunk('assets/js/main.js') : fs.readFileSync(path.join(webRoot, 'assets/js/main.js'), 'utf8'));
    app.run(minified ? bundledChunk('js/quote-drawer.js') : fs.readFileSync(path.join(webRoot, 'js/quote-drawer.js'), 'utf8'));
    app.start();
    app.context.onload();
    app.context.onscroll();
    app.trigger('.navbar-toggle-btn', 'click');
    assert.equal(app.group('.navbar-toggle-item').state.visible, true);
    assert.equal(app.group('body').state.classes.has('overflow-hidden'), true);
    app.trigger('.navbar-toggle-btn', 'click');
    assert.equal(app.group('.navbar-toggle-item').state.visible, false);
    app.trigger('#quoteDrawerToggle', 'click');
    assert.equal(app.group('#quoteDrawer').state.attributes['aria-hidden'], 'false');
    app.trigger('document', 'keydown', { key: 'Escape' });
    assert.equal(app.group('#quoteDrawer').state.attributes['aria-hidden'], 'true');
    app.trigger('#slideQuoteForm', 'submit', { preventDefault() {} });
    assert.equal(app.requests.length, 1);
    assert.equal(app.requests[0].url, '/Contact/Index');
    assert.equal(app.requests[0].data.SecurityQuestionToken, 'fixture');
    app.requests[0].success({ success: true });
    assert.equal(app.group('#slideQuoteForm').state.resets, 1);
    app.requests[0].complete();
    assert.equal(app.group("#slideQuoteForm button[type='submit']").state.attributes.disabled, false);
    console.log(`PASS ${label}: mobile menu, quote panel, Escape, submission and missing scroll indicator`);

    const contact = environment();
    contact.run(fs.readFileSync(path.join(webRoot, `js/contact${minified ? '.min' : ''}.js`), 'utf8'));
    contact.start();
    contact.trigger('#iletisimFormu', 'submit', { preventDefault() {} });
    assert.equal(contact.requests[0].url, '/Contact/Index');
    contact.requests[0].success({ success: true });
    assert.equal(contact.group('#iletisimFormu').state.resets, 1);
    assert.equal(contact.group('.sent-message').state.visible, true);
    contact.requests[0].error({ status: 429 });
    assert.equal(contact.group('.sent-message').state.visible, false);
    assert.ok(contact.group('.error-message').state.html.includes('Çok fazla istek'));
    console.log(`PASS ${label}: contact form success and rate-limit response`);

    const home = environment();
    home.context.document.querySelector = () => ({});
    home.run(fs.readFileSync(path.join(webRoot, `js/home-page${minified ? '.min' : ''}.js`), 'utf8'));
    home.documentEvents.get('DOMContentLoaded')();
    assert.equal(home.sliders.length, 1);
    assert.equal(home.sliders[0].selector, '.references-swiper');
    assert.equal(home.sliders[0].options.navigation.nextEl, '.references-next');
    console.log(`PASS ${label}: reference slider initializes after DOM parsing`);
}

console.log('Asset UI checks passed; no real network or tracking requests were made.');
