const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const messages = [];
const document = {
  body: {
    appendChild(element) { element.parentNode = this; },
    removeChild(element) {
      assert.equal(element.parentNode, this, 'a node must only be removed once');
      element.parentNode = null;
      if (this.blurDuringRemove) element.listeners.blur();
    },
  },
  createElement(tag) {
    return {
      tag, style: {}, listeners: {}, value: '',
      setAttribute(name, value) { this[name] = value; },
      addEventListener(name, callback) { this.listeners[name] = callback; },
      focus() { this.focused = true; },
      setSelectionRange() {},
    };
  },
};
const context = {
  LibraryManager: { library: {} },
  mergeInto(target, library) { Object.assign(target, library); },
  UTF8ToString(value) { return value; },
  stringToNewUTF8(value) { return value; },
  SendMessage(receiver, method, value) { messages.push({ receiver, method, value }); },
  Module: { canvas: { getBoundingClientRect() { return { left: 0, top: 0, width: 1280, height: 720 }; } } },
  document,
};
context.globalThis = context;
vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../client/Assets/AboutMe/Plugins/WebGL/AboutMePlatform.jslib'), 'utf8'), context);
const bridge = context.LibraryManager.library;

bridge.AM_InputOpen('room', '', '房间码', 0, 6, .2, .4, .3, .1);
let input = context.aboutMeTextInput;
assert.equal(input.focused, true, 'focus must be attempted during the original click');
assert.equal(input.style.opacity, '1', 'Safari must leave a visible field to tap again');
assert.equal(input.placeholder, '房间码');

// Safari can finish composition during deselection without another input event.
input.value = 'a1b2c3';
document.body.blurDuringRemove = true;
bridge.AM_InputClose('room');
document.body.blurDuringRemove = false;
assert.equal(bridge.AM_InputValue('room', ''), 'a1b2c3', 'Join reads the final native value');
assert.deepEqual(messages.at(-1), { receiver: 'room', method: 'OnBrowserInput', value: 'a1b2c3' });
assert.equal(messages.length, 1, 'reentrant blur cannot submit or remove twice');

bridge.AM_InputStore('room', '');
assert.equal(bridge.AM_InputValue('room', 'fallback'), '', 'Create clears the prior room code');

bridge.AM_InputOpen('room', '', '房间码', 0, 6, .2, .4, .3, .1);
input = context.aboutMeTextInput;
input.value = 'd4e5f6';
input.listeners.blur();
assert.equal(bridge.AM_InputValue('room', ''), 'd4e5f6', 'natural blur also preserves the code');
assert.deepEqual(messages.at(-1), { receiver: 'room', method: 'OnBrowserBlur', value: 'd4e5f6' });
console.log('Web input bridge close/blur regression passed');
