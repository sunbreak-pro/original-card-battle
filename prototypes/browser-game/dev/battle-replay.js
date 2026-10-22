// Dev only: an IDepictionSource that replays a battle the C# CoreBattleSource played (oracle/out/scripts.json,
// extracted by dev/battle-replays/extract.mjs). Every frame, event, cue, guide line, preview and refusal
// text is the C# bridge's own output, so the scene can be checked against Unity's script while the JS
// core is incomplete. Like DepictionRunner it follows one recorded order: the recorded card is the only
// one it accepts (others answer OffScript), and the turn ends where the record ends it.
import * as T from '../src/bridge/depictionScript/DepictionTypes.js';

const { PlayVerdict, DepictionText } = T;

/** Expands { $ref: n } with the case's defs into instances of the DepictionTypes classes ($type). */
function expand(value, defs) {
  const done = new Array(defs.length);
  const make = (type) => {
    const C = type && T[type];
    return typeof C === 'function' ? new C() : {};
  };
  const walk = (v) => {
    if (v === null || typeof v !== 'object') return v;
    if (Array.isArray(v)) return v.map(walk);
    const keys = Object.keys(v);
    if (keys.length === 1 && keys[0] === '$ref') {
      const i = v.$ref;
      if (done[i] === undefined) done[i] = walk(defs[i]);
      return done[i];
    }
    const o = make(v.$type);
    for (const k of keys) {
      if (k === '$type') continue;
      const desc = Object.getOwnPropertyDescriptor(Object.getPrototypeOf(o) || {}, k);
      if (desc && desc.get && !desc.set) continue; // computed members (PlanTag, RowText, CostDrop, WaitsForDrag)
      o[k] = walk(v[k]);
    }
    return o;
  };
  return walk(value);
}

export function createReplaySource(c) {
  const out = expand(c.output, c.defs || []);
  const moves = out.Moves || [];
  let i = 0;
  const src = {
    replay: c.id,
    Frame: out.Opening,
    get Finished() { return i >= moves.length; },
    get WaitingForPlayer() { return i < moves.length && moves[i].Action !== 'AdvanceAuto'; },
    get CanEndTurn() { return src.WaitingForPlayer && moves[i].Action === 'EndTurn'; },
    get SuggestedCardId() { return src.WaitingForPlayer && moves[i].Action === 'TryPlay' ? moves[i].Suggested || '' : ''; },
    get GuideText() { return src.WaitingForPlayer ? moves[i].Guide || '' : ''; },
    get State() { return null; },
    AdvanceAuto() {
      if (src.Finished) throw new Error('The battle is over.');
      if (src.WaitingForPlayer) throw new Error('The turn waits for the player.');
      return take();
    },
    EndTurn() {
      if (!src.CanEndTurn) throw new Error('The turn cannot be ended right now.');
      return take();
    },
    Inspect(cardId) {
      if (!src.WaitingForPlayer) return PlayVerdict.NotWaiting;
      const row = (moves[i].Hand || []).find((r) => r.Id === cardId);
      if (!row) return PlayVerdict.NotInHand;
      if (row.Inspect !== PlayVerdict.Accepted) return row.Inspect;
      return moves[i].Action === 'TryPlay' && moves[i].Suggested === cardId ? PlayVerdict.Accepted : PlayVerdict.OffScript;
    },
    TryPlay(cardId, zone) {
      let played = null;
      const r = (v) => ({ ok: v, value: played, verdict: v, played });
      const verdict = src.Inspect(cardId);
      if (verdict !== PlayVerdict.Accepted) return r(verdict);
      const face = DepictionText.Find(src.Frame.Hand, cardId);
      if (!face) return r(PlayVerdict.NotInHand);
      if (zone !== DepictionText.RequiredZone(face.Aim)) return r(PlayVerdict.WrongZone);
      played = take();
      return r(PlayVerdict.Accepted);
    },
    PreviewFor(cardId) {
      if (!src.WaitingForPlayer) return '';
      const row = (moves[i].Hand || []).find((r) => r.Id === cardId);
      return row ? row.PreviewFor || '' : '';
    },
    RefusalText(cardId, verdict) {
      const face = DepictionText.Find(src.Frame.Hand, cardId);
      if (!face) return '';
      const row = src.WaitingForPlayer ? (moves[i].Hand || []).find((r) => r.Id === cardId) : null;
      if (verdict === PlayVerdict.WrongZone) return row ? row.WrongZoneText : DepictionText.WrongZoneText(face);
      if (verdict === PlayVerdict.OffScript) {
        const next = DepictionText.NameOf(src.Frame.Hand, src.SuggestedCardId);
        return '「' + face.Name + '」はまだ出せません。台本の次は' + (next ? '「' + next + '」です' : '「ターン終了」です'); // DepictionRunner's wording (dev)
      }
      return row ? row.RefusalText || '' : '';
    },
  };
  function take() {
    const ev = moves[i].Event;
    i += 1;
    src.Frame = ev.After;
    return ev;
  }
  return src;
}
