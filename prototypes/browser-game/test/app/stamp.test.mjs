// One save, several tabs (tier1 R1-16 「死亡と生の終了は確定と同時に保存し、リロードで遺産を選び直せない
// ようにする」, INHERITANCE.md R-21): every write carries a stamp { rev, tab } at the head of the stored
// text, so a page can tell, before it writes, whether another tab has written since it last read or wrote
// the save (src/app/main.js persist / inSync). These are the save module's halves of that check.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import * as Inh from '../../src/core/inheritance/Inheritance.js';

function fakeStorage() {
  const m = new Map();
  return {
    getItem(k) { return m.has(k) ? m.get(k) : null; },
    setItem(k, v) { m.set(k, String(v)); },
    removeItem(k) { m.delete(k); },
  };
}

test('writeSave stamps the stored text; storedStamp reads it back without parsing the save', async () => {
  const save = await import('../../src/app/save.js');
  const had = Object.getOwnPropertyDescriptor(globalThis, 'window');
  try {
    const st = fakeStorage();
    globalThis.window = { localStorage: st };
    assert.equal(save.storedStamp(), null, 'nothing stored');
    const lineage = Inh.newLineage(5);
    assert.ok(save.writeSave({ lineage, life: null }, { rev: 3, tab: 'abc' }));
    assert.ok(st.getItem(save.SAVE_KEY).startsWith('{"v":1,"rev":3,"tab":"abc",'), 'the stamp leads the text');
    assert.deepEqual(save.storedStamp(), { rev: 3, tab: 'abc' });
    const o = save.openSave();
    assert.deepEqual(o.stamp, { rev: 3, tab: 'abc' }, 'openSave hands the stamp it read');
    assert.equal(Inh.serialize(o.save.lineage), Inh.serialize(lineage), 'the save itself reads as before');

    // a save written by an older build (no stamp) reads as rev 0, and so does text that does not load
    save.writeSave({ lineage, life: null });
    assert.deepEqual(save.storedStamp(), { rev: 0, tab: '' });
    st.setItem(save.SAVE_KEY, '{"v":1,"lin');
    assert.deepEqual(save.storedStamp(), { rev: 0, tab: '' });
    const warn = console.warn; console.warn = () => {};
    try { assert.deepEqual(save.openSave().stamp, { rev: 0, tab: '' }, 'the unreadable text keeps a stamp too'); } finally { console.warn = warn; }

    // a tab id cannot break out of its string
    save.writeSave({ lineage, life: null }, { rev: 4, tab: 'a"b\\c' });
    assert.deepEqual(save.storedStamp(), { rev: 4, tab: 'abc' });
  } finally {
    if (had) Object.defineProperty(globalThis, 'window', had); else delete globalThis.window;
  }
});

test('sameStamp: the same write, nothing stored on both sides, and every other case differs', async () => {
  const { sameStamp, stampOf } = await import('../../src/app/save.js');
  assert.ok(sameStamp(null, null));
  assert.ok(sameStamp({ rev: 2, tab: 'x' }, { rev: 2, tab: 'x' }));
  assert.ok(!sameStamp({ rev: 2, tab: 'x' }, { rev: 2, tab: 'y' }), 'two tabs that both wrote rev 2');
  assert.ok(!sameStamp({ rev: 2, tab: 'x' }, { rev: 3, tab: 'x' }));
  assert.ok(!sameStamp(null, { rev: 0, tab: '' }), 'a save appeared');
  assert.ok(!sameStamp({ rev: 0, tab: '' }, null), 'the save went away');
  assert.equal(stampOf(''), null);
});

// The two-tab story the stamps stop (b13-revive / b12-twotabs): tab B holds the save as it was before tab A
// recorded a death. B's next write would bring the dead heir back and wipe the trace; the stamp check
// (main.js inSync) sees that the stored save is no longer the one B read.
test('a stale page can tell that another tab wrote after it', async () => {
  const save = await import('../../src/app/save.js');
  const had = Object.getOwnPropertyDescriptor(globalThis, 'window');
  try {
    globalThis.window = { localStorage: fakeStorage() };
    const lineage = Inh.newLineage(9);
    save.writeSave({ lineage, life: null }, { rev: 1, tab: 'A' });
    const a = save.openSave().stamp;
    const b = save.openSave().stamp;
    assert.ok(save.sameStamp(save.storedStamp(), a) && save.sameStamp(save.storedStamp(), b), 'both tabs are in step');
    // A writes (the death): A's own stamp moves on, B's no longer matches
    const next = { rev: a.rev + 1, tab: 'A' };
    save.writeSave({ lineage, life: null }, next);
    assert.ok(save.sameStamp(save.storedStamp(), next), 'A is in step');
    assert.ok(!save.sameStamp(save.storedStamp(), b), 'B is stale and must not write');
  } finally {
    if (had) Object.defineProperty(globalThis, 'window', had); else delete globalThis.window;
  }
});
