const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const code = fs.readFileSync(path.resolve(__dirname, '../IdeioCreative/wwwroot/js', process.argv[2] || 'ad-audit.min.js'), 'utf8');
let passed = 0;

function setup({ response = { success: true, conversionEventId: 'free-ad-audit-lead-42' }, status = 200, pixel = 'ok', valid = true, href = 'https://ideiocreative.com/ucretsiz-reklam-denetimi?oppref=opaque%2Bclick', fetchError } = {}) {
    const events = { pixel: [], google: [], meta: [], fetch: [] };
    let listener;
    const element = () => ({ hidden: true, disabled: false, focus() { this.focused = true; } });
    const fields = { ...element(), hidden: false };
    const error = element();
    const success = element();
    const intro = { hidden: false };
    const submit = { disabled: false, innerHTML: 'Submit' };
    const form = {
        action: '/Contact/Index',
        elements: { namedItem: () => ({ value: '' }) },
        querySelector: () => submit,
        querySelectorAll: () => [],
        reportValidity: () => valid,
        addEventListener: (name, fn) => { listener = fn; },
        setAttribute() {}, removeAttribute() {}
    };
    class FormData {
        constructor() { this.map = new Map([['LeadSource', 'free_ad_audit']]); }
        get(key) { return this.map.get(key) ?? null; }
        getAll() { return []; }
        set(key, value) { this.map.set(key, value); }
        delete(key) { this.map.delete(key); }
    }
    const window = { location: { href }, setTimeout: () => 1, clearTimeout() {},
        gtag: (...args) => events.google.push(args), fbq: (...args) => events.meta.push(args) };
    if (pixel !== 'missing') window.oaiq = (...args) => { events.pixel.push(args); if (pixel === 'throws') throw new Error('pixel blocked'); };
    const document = {
        getElementById: id => ({ 'audit-form': form, 'audit-form-fields': fields, 'audit-form-error': error, 'audit-form-success': success }[id] ?? null),
        querySelector: selector => selector === '.audit-form-intro' ? intro : null
    };
    const fetch = async (url, request) => {
        events.fetch.push({ url, request });
        if (fetchError) throw fetchError;
        return { status, ok: status >= 200 && status < 300, json: async () => response };
    };
    vm.runInNewContext(code, { window, document, fetch, URL, FormData, AbortController, Date });
    return { events, window, fields, success, error, submit, send: () => listener({ preventDefault() {} }) };
}
async function check(name, options, verify) {
    const context = setup(options); await context.send(); verify(context); passed++; console.log('PASS: ' + name);
}
(async () => {
    const initial = setup();
    assert.equal(initial.events.pixel.length, 0); assert.equal(initial.events.fetch.length, 0);
    passed++; console.log('PASS: page load creates no lead');
    await check('confirmed save emits one deduplicated pixel event', {}, c => {
        assert.equal(c.events.pixel.length, 1);
        const [command, type, data, options] = c.events.pixel[0];
        assert.equal(command, 'measure'); assert.equal(type, 'lead_created');
        assert.equal(data.type, 'customer_action'); assert.equal(options.event_id, 'free-ad-audit-lead-42');
        assert.equal(c.success.hidden, false); assert.equal(c.fields.hidden, true);
        assert.equal(c.events.fetch[0].request.body.get('OpenAIOppref'), null);
        assert.equal(c.events.fetch[0].request.body.get('LeadSource'), 'free_ad_audit');
        assert.equal(c.events.google.length, 1); assert.equal(c.events.meta.length, 1);
    });
    for (const [name, options] of [
        ['validation rejection', { response: { success: false, message: 'Invalid' } }],
        ['duplicate rejection', { response: { success: false, message: 'Duplicate' } }],
        ['HTTP 400', { status: 400 }], ['rate limit', { status: 429 }], ['server error', { status: 500 }],
        ['network failure', { fetchError: new Error('offline') }],
        ['timeout', { fetchError: Object.assign(new Error('timeout'), { name: 'AbortError' }) }]
    ]) await check(name + ' emits no lead', options, c => {
        assert.equal(c.events.pixel.length, 0); assert.equal(c.events.google.length, 0); assert.equal(c.events.meta.length, 0);
        assert.equal(c.success.hidden, true); assert.equal(c.error.hidden, false); assert.equal(c.submit.disabled, false);
    });
    await check('browser validation blocks submission and tracking', { valid: false }, c => {
        assert.equal(c.events.fetch.length, 0); assert.equal(c.events.pixel.length, 0);
    });
    for (const pixel of ['missing', 'throws']) await check(pixel + ' pixel preserves success and other tracking', { pixel }, c => {
        assert.equal(c.success.hidden, false); assert.equal(c.error.hidden, true);
        assert.equal(c.events.google.length, 1); assert.equal(c.events.meta.length, 1);
    });
    await check('missing event ID does not send an undeduplicated lead', { response: { success: true } }, c => {
        assert.equal(c.events.pixel.length, 0); assert.equal(c.success.hidden, false);
    });
    await check('no click ID keeps form working', { href: 'https://ideiocreative.com/ucretsiz-reklam-denetimi' }, c => {
        assert.equal(c.events.fetch[0].request.body.get('OpenAIOppref'), null); assert.equal(c.events.pixel.length, 1);
    });
    const simultaneous = setup(); await Promise.all([simultaneous.send(), simultaneous.send()]);
    assert.equal(simultaneous.events.fetch.length, 1); assert.equal(simultaneous.events.pixel.length, 1);
    passed++; console.log('PASS: concurrent submit sends and measures once');
    console.log(`${passed} client checks passed. No real tracking requests were made.`);
})().catch(error => { console.error(error); process.exitCode = 1; });
