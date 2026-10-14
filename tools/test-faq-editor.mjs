import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const source = process.argv.includes('--source');
const scriptPath = path.join(root, `IdeioCreative/wwwroot/js/admin-service-faqs${source ? '' : '.min'}.js`);
const code = fs.readFileSync(scriptPath, 'utf8');
const variant = source ? 'source' : 'minified';

// A small DOM fixture exercises the shipped script without a browser or network.
class Element {
    constructor(tag, attributes = {}, children = []) {
        this.tagName = tag.toUpperCase();
        this.attributes = {};
        this.dataset = {};
        this.children = [];
        this.events = new Map();
        this.parent = null;
        this.value = '';
        this.checked = false;
        this.disabled = false;
        this.validityMessage = '';
        this.focusCount = 0;
        for (const [key, value] of Object.entries(attributes)) this.setAttribute(key, value);
        for (const child of children) this.appendChild(child);
    }
    setAttribute(key, value) {
        this.attributes[key] = String(value);
        if (key.startsWith('data-')) {
            this.dataset[key.slice(5).replace(/-([a-z])/g, (_, letter) => letter.toUpperCase())] = String(value);
        } else if (['type', 'name', 'id', 'value'].includes(key)) {
            this[key] = String(value);
        } else if (key === 'checked') {
            this.checked = true;
        }
    }
    hasAttribute(key) { return Object.hasOwn(this.attributes, key); }
    matches(selector) {
        const attribute = /^\[([^=\]]+)(?:="([^"]*)")?\]$/.exec(selector);
        return attribute
            ? this.hasAttribute(attribute[1]) && (attribute[2] === undefined || this.attributes[attribute[1]] === attribute[2])
            : this.tagName.toLowerCase() === selector;
    }
    querySelectorAll(selector) {
        const result = [];
        for (const child of this.children) {
            if (child.matches(selector)) result.push(child);
            result.push(...child.querySelectorAll(selector));
        }
        return result;
    }
    querySelector(selector) { return this.querySelectorAll(selector)[0] ?? null; }
    closest(selector) { return this.matches(selector) ? this : this.parent?.closest(selector) ?? null; }
    contains(element) { return element === this || this.children.some(child => child.contains(element)); }
    appendChild(child) { child.remove(); child.parent = this; this.children.push(child); return child; }
    insertBefore(child, reference) {
        child.remove();
        const index = this.children.indexOf(reference);
        assert.ok(index >= 0, 'Reference node must belong to this list');
        child.parent = this;
        this.children.splice(index, 0, child);
    }
    remove() {
        if (this.parent) this.parent.children.splice(this.parent.children.indexOf(this), 1);
        this.parent = null;
    }
    get previousElementSibling() { return this.parent?.children[this.parent.children.indexOf(this) - 1] ?? null; }
    get nextElementSibling() { return this.parent?.children[this.parent.children.indexOf(this) + 1] ?? null; }
    cloneNode(deep) {
        const copy = new Element(this.tagName, this.attributes, deep ? this.children.map(child => child.cloneNode(true)) : []);
        copy.value = this.value;
        copy.checked = this.checked;
        return copy;
    }
    setCustomValidity(message) { this.validityMessage = message; }
    focus() { this.focusCount++; }
    addEventListener(type, callback) {
        if (!this.events.has(type)) this.events.set(type, []);
        this.events.get(type).push(callback);
    }
    emit(type, target = this) {
        const event = { target, prevented: false, preventDefault() { this.prevented = true; } };
        for (const callback of this.events.get(type) ?? []) callback(event);
        return event;
    }
}

const element = (tag, attributes, children) => new Element(tag, attributes, children);
const field = (row, key) => row.querySelector(`[data-faq-field="${key}"]`);
const button = (row, action) => row.querySelector(`[data-faq-${action}]`);

function faqRow({ id = 0, question = '', answer = '', published = true } = {}) {
    const row = element('div', { 'data-faq-row': '' }, [
        element('span', { 'data-faq-number': '' }),
        ...['up', 'down', 'remove'].map(action => element('button', { [`data-faq-${action}`]: '' }, [element('i')])),
        element('input', { type: 'hidden', value: id, 'data-faq-field': 'Id' }),
        ...['Question', 'Answer'].flatMap(key => [
            element('label', { 'data-faq-label': key }),
            element(key === 'Answer' ? 'textarea' : 'input', { 'data-faq-field': key }),
            element('span', { 'data-faq-validation': key })
        ]),
        element('input', { type: 'checkbox', value: 'true', 'data-faq-field': 'IsPublished' }),
        element('label', { 'data-faq-label': 'IsPublished' }),
        element('input', { type: 'hidden', value: 'false', 'data-faq-field': 'IsPublished' }),
        element('span', { 'data-faq-validation': 'Id' })
    ]);
    field(row, 'Question').value = question;
    field(row, 'Answer').value = answer;
    field(row, 'IsPublished').checked = published;
    return row;
}

function fixture(initialRows = []) {
    const list = element('div', { 'data-faq-list': '' }, initialRows.map(faqRow));
    const template = element('template', { 'data-faq-template': '' });
    template.content = { firstElementChild: faqRow() };
    const empty = element('p', { 'data-faq-empty': '' });
    const add = element('button', { 'data-faq-add': '' }, [element('i')]);
    const editor = element('section', { 'data-faq-editor': '' }, [add, empty, list, template]);
    const form = element('form', {}, [editor]);
    form.reports = 0;
    form.checkValidity = () => list.querySelectorAll('[data-faq-field]').every(input => !input.validityMessage);
    form.reportValidity = () => { form.reports++; return form.checkValidity(); };
    return { list, editor, empty, form,
        rows: () => list.querySelectorAll('[data-faq-row]'),
        add: () => editor.emit('click', add.children[0]),
        click: (row, action) => editor.emit('click', button(row, action).children[0]),
        input(row, key, value) { field(row, key).value = value; editor.emit('input', field(row, key)); },
        submit: () => form.emit('submit') };
}

function start(...editors) {
    const document = { querySelectorAll: selector => editors.map(item => item.editor).filter(item => item.matches(selector)) };
    vm.runInNewContext(code, { document }, { filename: scriptPath });
}

function postedValues(form) {
    const posted = new Map();
    for (const input of form.querySelectorAll('[data-faq-field]')) {
        if (input.disabled || (input.type === 'checkbox' && !input.checked)) continue;
        if (!posted.has(input.name)) posted.set(input.name, []);
        posted.get(input.name).push(input.value);
    }
    return posted;
}

function assertBindable(editor, expected) {
    const rows = editor.rows();
    const posted = postedValues(editor.form);
    assert.equal(rows.length, expected.length);
    assert.equal(posted.size, expected.length * 4, 'Only live FAQ fields should be submitted');
    for (let index = 0; index < rows.length; index++) {
        const row = rows[index];
        const prefix = `Faqs[${index}].`;
        assert.equal(row.querySelector('[data-faq-number]').textContent, String(index + 1));
        assert.deepEqual(posted.get(prefix + 'Id'), [String(expected[index].id)]);
        assert.deepEqual(posted.get(prefix + 'Question'), [expected[index].question]);
        assert.deepEqual(posted.get(prefix + 'Answer'), [expected[index].answer]);
        assert.equal(posted.get(prefix + 'IsPublished')[0], String(expected[index].published));
        assert.equal(posted.get(prefix + 'IsPublished').at(-1), 'false', 'Unchecked values must remain in the same row');
        for (const input of row.querySelectorAll('[data-faq-field]')) {
            assert.equal(input.name, prefix + input.dataset.faqField);
            if (input.type !== 'hidden' || input.dataset.faqField !== 'IsPublished') {
                assert.equal(input.id, `Faqs_${index}__${input.dataset.faqField}`);
            }
        }
        for (const label of row.querySelectorAll('[data-faq-label]')) {
            assert.equal(label.htmlFor, `Faqs_${index}__${label.dataset.faqLabel}`);
        }
        for (const message of row.querySelectorAll('[data-faq-validation]')) {
            assert.equal(message.attributes['data-valmsg-for'], prefix + message.dataset.faqValidation);
        }
        assert.equal(button(row, 'up').disabled, index === 0);
        assert.equal(button(row, 'down').disabled, index === rows.length - 1);
    }
    assert.equal(editor.empty.hidden, rows.length > 0);
}

{
    const a = { id: 11, question: 'Birinci soru', answer: 'Birinci yanıt', published: true };
    const b = { id: 12, question: 'İkinci soru', answer: 'İkinci yanıt', published: true };
    const c = { id: 13, question: 'Üçüncü soru', answer: 'Üçüncü yanıt', published: false };
    const editor = fixture([a, b, c]);
    start(editor);
    assertBindable(editor, [a, b, c]);
    editor.add();
    const added = editor.rows().at(-1);
    assert.equal(field(added, 'Question').focusCount, 1, 'Adding a question must focus its input');
    assert.equal(field(added, 'IsPublished').checked, true, 'New questions start published');
    editor.input(added, 'Question', 'Yeni soru');
    editor.input(added, 'Answer', 'Yeni yanıt');
    field(added, 'IsPublished').checked = false;
    const d = { id: 0, question: 'Yeni soru', answer: 'Yeni yanıt', published: false };
    editor.click(added, 'up');
    assertBindable(editor, [a, b, d, c]);
    editor.click(editor.rows()[1], 'remove');
    assertBindable(editor, [a, d, c]);
    editor.click(editor.rows()[0], 'down');
    assertBindable(editor, [d, a, c]);
    assert.equal(editor.submit().prevented, false);
    while (editor.rows().length) editor.click(editor.rows()[0], 'remove');
    assertBindable(editor, []);
    assert.equal(postedValues(editor.form).size, 0, 'Removing questions must also remove their hidden values');
    console.log(`PASS ${variant}: add, draft, move, remove and MVC field/label/error associations`);
}

{
    const editor = fixture([{}]);
    start(editor);
    const row = editor.rows()[0];
    assert.equal(editor.submit().prevented, false, 'Completely blank rows can be ignored by the server');
    editor.input(row, 'Question', 'Yalnızca soru');
    assert.equal(field(row, 'Answer').validityMessage, 'Lütfen yanıt giriniz.');
    assert.equal(editor.submit().prevented, true);
    assert.equal(editor.form.reports, 1, 'The incomplete pair must produce visible validation feedback');
    editor.input(row, 'Answer', 'Tamamlanan yanıt');
    assert.equal(field(row, 'Answer').validityMessage, '');
    assert.equal(editor.submit().prevented, false);
    editor.input(row, 'Question', '   ');
    assert.equal(field(row, 'Question').validityMessage, 'Lütfen soru giriniz.');
    assert.equal(editor.submit().prevented, true);
    editor.input(row, 'Answer', '\n  ');
    assert.equal(field(row, 'Question').validityMessage, '');
    assert.equal(editor.submit().prevented, false, 'Whitespace-only pairs remain optional');
    // Programmatic edits are checked again on submit, even without an input event.
    field(row, 'Answer').value = 'Programatik yanıt';
    assert.equal(editor.submit().prevented, true);
    assert.equal(field(row, 'Question').validityMessage, 'Lütfen soru giriniz.');
    console.log(`PASS ${variant}: partial-pair feedback, submit blocking, recovery and optional blank rows`);
}

{
    const first = fixture();
    const second = fixture([{ id: 21, question: 'Diğer hizmetin sorusu', answer: 'Yanıt', published: false }]);
    start(first, second);
    first.add();
    first.add();
    assert.equal(first.rows().length, 2);
    assert.equal(second.rows().length, 1);
    assert.equal(field(second.rows()[0], 'Question').focusCount, 0);
    assert.equal(postedValues(second.form).get('Faqs[0].Id')[0], '21');
    first.input(first.rows()[0], 'Question', 'Eksik yanıt');
    assert.equal(first.submit().prevented, true);
    assert.equal(second.submit().prevented, false, 'An incomplete editor cannot block another form');
    first.click(first.rows()[0], 'remove');
    first.click(first.rows()[0], 'remove');
    assert.equal(first.empty.hidden, false);
    assert.equal(second.empty.hidden, true);
    second.add();
    assert.equal(first.rows().length, 0);
    assert.equal(second.rows().length, 2);
    console.log(`PASS ${variant}: multiple editors keep rows, focus and validation scoped to their own form`);
}

console.log('FAQ editor checks passed; no browser, network or database requests were made.');
