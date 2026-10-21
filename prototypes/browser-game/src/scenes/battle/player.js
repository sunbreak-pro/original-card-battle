// The battle scene's playback core: a port of unity-port/unity-project-kit/Assets/View/Depiction/DepictionPlayer.cs
// (the flow, the plan's waits, the cue beats, the input states). It computes no rule: every number,
// label, verdict and line of text comes from ctx.source (DepictionFrame / DepictionEvent / Cue /
// PlayVerdict) and the ported Script/ helpers. Positions come from the modules (env.geom); how long an
// event holds the flow is EffectPlan's (HoldBeat waits each step the plan lists, #495).
//
// Every state-changing call goes to ctx.source itself (AdvanceAuto, EndTurn, TryPlay), never to a kept
// reference, so the save recorder sees it. The modules (hand / field / hud, docs/BATTLE_SCENE.md §6)
// draw; this file decides what plays when.
//
// Several enemies (scene addition with the JS writer's extension, docs/port-notes/depiction-multi-enemy.md;
// Unity's DepictionPlayer plays one enemy): each cue plays on the enemy its Cue.Unit names — its figure
// (field, the trailing `unit` argument), its number panel (hud.status(side, unit)) and its omen badge
// (hud.omenOf(unit)); a card released over enemy k goes to ctx.source.TryPlay(id, zone, k). With one
// enemy every call is the one Unity makes (no target argument reaches the source).
import {
  DepictionEventKind, CueKind, UnitSide, CardAim, DropZone, PlayVerdict, DepictionText, BattleOutcome, Cue, CardValueKind,
} from '../../bridge/depictionScript/DepictionTypes.js';
import { EffectId, EffectSwitches, EffectPlan, StatusBeat, EffectStrength, EffectCatalog } from '../../bridge/depictionScript/Effects.js';
import { EffectFlow } from '../../bridge/depictionScript/EffectAudit.js';
import { EffectGrammar, FxMark, FxSystem } from '../../bridge/depictionScript/EffectGrammar.js';
import { StanceGlow } from '../../bridge/depictionScript/StanceGlow.js';
import { FigurePose } from '../../bridge/depictionScript/FigureMotion.js';
import { PileKind } from '../../bridge/depictionScript/PileLayout.js';
import { StrikeSystem } from '../../bridge/depictionScript/DepictionTypes.js';
import { BattleSpeed } from '../../bridge/depictionScript/BattleSpeed.js';
import { CoreText } from '../../bridge/depiction/CoreText.js';
import { StatusKind, EnumTokens } from '../../core/battle/Types.js';
import { Constants } from '../../core/battle/Constants.js';
import { L, numberFont, enemyNameOf, hatchGlyph } from './layout.js';
import { PLAN_DROP_MS } from './hud/omen.js';
import { stepOfSpeed, speedOfStep } from './clock.js';

/** Cue.Unchanged (−1), read once; the port keeps it as a static of Cue. */
const UNCHANGED = Cue && Cue.Unchanged != null ? Cue.Unchanged : -1;
/** How far under an omen badge a float that would cross it stops (scene choice; UNDER_BADGES). */
const FLOAT_UNDER_BADGE = 8;
/**
 * How far over the figures' feet line a float's path stops (scene choice, review fix): floats of one action
 * stand one under another, and one that would reach the floor strip (its cells, the gap number) stands beside
 * them instead (field/fx.js placeFloat).
 */
const FLOAT_OVER_FEET = 16;
/**
 * A blow Guard took part of (battle_ui_ux_v2 §5.6 順 2〜3): the "Guard −n" tag and the HP number rise side by side,
 * HP_BESIDE_GUARD_GAP apart, on the path every blow's number takes (chest − 40 toward the head, §5.0), the pair centred
 * on the chest — the tag on the side the blow came from (over the shield), the HP number on the figure's back. Review
 * 17: with the old opaque 150×170 shield the tag rose from 113 px over it, so a guarded hit's numbers hung ~145 px over
 * every other blow's, at the omen badges' height; the shield is now a see-through rim (field/fx.js), and the
 * 「beside the shield」 placement that followed from it (it put the tag on the next enemy's head) is gone with it.
 */
const HP_BESIDE_GUARD_GAP = 24;
const STAGE_EDGE = 16;

/**
 * battle_ui_ux_v2 §5.7 順 9 / 順 11: a move the core stopped (呪縛, 鉤爪: CoreText.MoveBlockedLine) rises in grey,
 * not in the warm of a fired trait.
 */
const MOVE_BLOCKED_LINES = new Set([StatusKind.Binding, StatusKind.Hook].map((k) => CoreText.MoveBlockedLine(k)).filter(Boolean));
/**
 * scene text (battle_ui_ux_v2 §3.8 俊敏): the guide while the free cell's choice waits; the refusal line for a card
 * tried meanwhile (選ぶまで手札は掴めません); and §5.7 順 9's grey word for a step that moved no cell with no line of
 * its own (鈍足 takes the cell to 0: the writer writes nothing for it).
 */
const FREE_STEP_GUIDE = '俊敏で 1 マス動けます。矢印か「留まる」を選んでください（← / → と Enter）';
const FREE_STEP_REFUSAL = '先に、動く先の矢印か「留まる」を選んでください';
const FREE_STEP_STUCK = '動けず';
/**
 * scene text (battle_ui_ux_v2 §3.5 「一覧ドロワーが開いている → 掴めない。ホバーで持ち上がるだけ。閉じるボタン脇に「閉じると
 * プレイできます」」; review 16): the guide line while the pile list is open, and the refusal line for a card pressed
 * or picked by its digit meanwhile (the list's own 「閉じるとプレイできます」 stands far from the hand).
 */
const DRAWER_GUIDE = '一覧を閉じるとプレイできます（Esc）';
/**
 * scene text (battle_ui_ux_v2 §2.4 / §3.5 「手記ドロワー…が開いている → 掴めない。ホバーで持ち上がるだけ」; review 17: with the
 * journal open a pressed card did nothing and the guide still asked for a release): the guide line while the journal is
 * open, and the refusal line for a card pressed meanwhile (the journal's own 「閉じるとプレイできます」 is by its close button).
 */
const JOURNAL_GUIDE = '手記を閉じるとプレイできます（Esc）';
/**
 * scene text (battle_ui_ux_v2 §3.14 「…枠の外で離す → 案内の行に「敵の上で離すと出せます」」; review 17): the round receiver
 * is not drawn any more (battle-visual-v1: the corner hooks only), so the ported DepictionText.WrongZoneText's
 * 「敵の受け皿の上」 names a thing the screen does not show. A single-target card's wrong-zone line says 敵の上 instead.
 */
const wrongZoneOnEnemy = (name) => '「' + name + '」は敵の上で離すと出せます';
/**
 * battle_ui_ux_v2 §5.7 順 7 (review 19): the event CoreScriptWriter.OpenIfBoundary opens for a player shoved into the end
 * of the line (its title; the writer writes no other mark for a wall). Its blow cues are the wall's.
 */
const WALL_TITLE = '壁に当たる';
/** scene text (§5.0 「−3 壁」): the side label of a wall's number. */
const WALL_SIDE = '壁';
/** §5.7 順 7: the edge the pushed figure hit lights for this long (nominal ms). */
const WALL_EDGE_MS = 80;
/**
 * TraitFire cues that are no fired trait of a card or an action (review 19): a move the core stopped (MOVE_BLOCKED_LINES)
 * and the stance lines (構え・ a stance set, 構え停止・ / 構え復帰・ CoreText.StanceStoppedLine / StanceResumedLine).
 */
const STANCE_LINE = /^構え/;
const STANCE_SET_PREFIX = '構え・';
/**
 * battle_ui_ux_v2 §5.9 順 1 / 順 12 (review 21): a fired trait's condition is named by one word — 「間合い」「連動」 —, not by
 * the lamp line's short form with its threshold (CoreText.ConditionWord: 「間合い0」「残4」「連動(攻)」), which the player
 * never reads elsewhere (the card prints 「温存 / 残り 4 以上」). The lamp word's head → the condition's name.
 */
const CONDITION_NAMES = [
  [/^間合い/, () => '間合い'],
  [/^残\d/, () => '温存'],
  [/^連動/, () => '連動'],
  [/^予兆/, () => '予兆'],
  [/^相手(.+)$/, (m) => m[1] + '中'],
  [/^自分(.+)$/, (m) => m[1] + '中'],
];
function conditionName(word) {
  for (const [re, name] of CONDITION_NAMES) {
    const m = re.exec(word);
    if (m) return name(m);
  }
  return word;
}
/**
 * §5.0 / §5.9 順 9 「主数字に「+6 重撃」」: the trait effects that are named rather than counted carry their own word on the
 * number; 重撃's +6 is the core's HeavyBlowPower.
 */
const NAMED_EFFECTS = { 重撃: () => '+' + Constants.HeavyBlowPower + ' 重撃', 転換: () => '転換', 追撃: () => '追撃' };
/**
 * battle_ui_ux_v2 §5.0 「−12 ×1.5 脆化」, §5.6 順 7, §5.10 強化 「次のアタック面の数字に「×1.5 強化」」 (review 21; Unity has none):
 * the multiplier a blow carried, read off the event's own cues — the 脆化 the struck side spent, the 強化 the striker spent
 * (§5.10: one per blow, 脆化 first). The factors are the core's.
 */
const FRAGILE_WORD = EnumTokens.ToLabel(StatusKind.Fragile);
const EMPOWER_WORD = EnumTokens.ToLabel(StatusKind.Empower);
const MULT_LABEL = {
  [FRAGILE_WORD]: '×' + Constants.FragileMult + ' ' + FRAGILE_WORD,
  [EMPOWER_WORD]: '×' + Constants.EmpowerMult + ' ' + EMPOWER_WORD,
};
/** scene text (§5.7 順 6 「押し出し」「引き寄せ」): the muted word over a figure pulled toward the other side. */
const PULLED_WORD = '引き寄せ';
/** §5.0: the floating HP loss carries its sign (「−18」; the DS prototype's floatAt('hp', '−' + n)). */
const MINUS = '−';
/**
 * §5.12 順 5 (review 21): an enemy action the writer splits into two events (「二段斬り（2 撃目）」, 「退き撃ち（下がる）」) is one
 * action on the screen — one step in, one step back, the badge lit throughout; the second blow follows this long after the
 * first one's beats (「チップの着地の 80 ms 後に 2 撃目」).
 */
const CONTINUE_PAUSE_MS = 80;
/**
 * scene text (battle_ui_ux_v2 §1.1 C2 / C4, §5.1 順 6, §5.9, §5.11; review 19): the pile list's log tab — the kinds of line
 * the spec names (特性 / 状態 / スタンス / 山札作り直し), each from the script's own words, under the event's title.
 */
const LOG = {
  turn: (n) => '— ターン ' + n + ' —',
  enemyTurn: '— 敵の番 —',
  mine: (title) => '自分: ' + title,
  foe: (who, title) => who + ': ' + title,
  trait: (text) => '　特性: ' + text,
  status: (who, text, stacks) => '　' + (who ? who + ' ' : '') + '状態: ' + text + (stacks > 0 ? ' ' + stacks : ' 消えた'),
  stance: (name, count) => '　スタンス: ' + name + (count > 1 ? '（' + count + ' つ目）' : ''),
  rebuild: '　山札を作り直した',
  hp: (side, n) => '　' + side + ' HP −' + n,
  guard: (side, n) => '　' + side + ' Guard −' + n,
  wall: (side, n) => '　' + side + ' 壁 −' + n,
  fell: (side) => '　' + side + 'が倒れた',
};
/** scene text: who a log line's number is about (with two or three enemies, each by its name: logWho). */
const LOG_WHO = { Player: '自分', Enemy: '敵' };
/** At most this many log lines are kept (the oldest go). */
const LOG_MAX = 400;
/**
 * battle_ui_ux_v2 §5.19 順 1〜4 (review 19; scene addition, Unity goes from the fall to 負け): a loss — the foot lights
 * shrink out (600, in), the ink floods up from the bottom (800, in), 「力尽きた」 comes in (300, out), one second's hold —
 * before the result card. Nominal ms on the scene clock.
 */
const LOSS = { feetMs: 600, inkMs: 800, wordMs: 300, holdMs: 1000, paperMs: 400 };
/**
 * scene text (review 21; battle_ui_ux_v2 §2.5 / §5.19 順 5〜7 結果画面, the DS prototype's end card): the end of the battle's
 * words around the frame's numbers and the scene's own count of the battle.
 */
const RESULT_TEXT = {
  fellAt: (chain, layer) => (chain && chain.total > 1 ? chain.index + ' 戦目で倒れた' : '第 ' + layer + ' 層で倒れた'),
  hpLeft: (hp, max) => '残 HP ' + hp + ' / ' + max,
  playedHead: '出した札',
  traits: (n) => '特性の発動 ' + n + ' 回',
  turns: (n) => n + ' ターン戦った',
  toJournal: '手記へ',
  beat: (names) => (names ? names + 'を倒した。' : '敵を倒した。'),
  wonLine: (turn, hp, max) => 'ターン ' + turn + ' ・ 残りの HP ' + hp + ' / ' + max,
  chainAt: (chain) => chain.index + ' / ' + chain.total + ' 戦目',
  switchTitle: (index) => index + ' 戦目',
};
/** §2.5: the four attributes' bars on the result screen (the DS's attribute names and frame colours; a move card is a skill). */
const RESULT_BARS = [
  { kinds: ['Attack'], label: '攻撃', color: 'var(--violet)' },
  { kinds: ['Guard'], label: '防御', color: 'var(--guard)' },
  { kinds: ['Skill', 'Move'], label: 'スキル', color: 'var(--amber)' },
  { kinds: ['Stance'], label: 'スタンス', color: 'var(--crimson)' },
];
/** §5.18 順 7: the chain's next enemy comes out of the haze over this (out, nominal ms). */
const CHAIN_APPEAR_MS = 400;
/** scene text (§5.19 順 3). */
const LOSS_WORD = '力尽きた';

/** sound.md: the hit sound of a blow by its system (打 for pushes and pulls). */
const HIT_SOUND = {
  [StrikeSystem.Slash]: 'hitSlash',
  [StrikeSystem.Thrust]: 'hitThrust',
  [StrikeSystem.Sweep]: 'hitSlash',
  [StrikeSystem.Strike]: 'hitBlunt',
};

/**
 * @param env   the scene environment (docs/BATTLE_SCENE.md §2)
 * @param view  { hand, field, hud } — the three modules
 * @param ctx   the scene's ctx (source, settings, audio, chain, onFinished, auto …)
 */
export function createPlayer(env, view, ctx) {
  const { hand, field, hud } = view;
  const { clock, real } = env;
  const switches = EffectSwitches.AllOn();
  const ms = (id) => switches.Ms(id);

  let alive = true;
  let halted = false;
  let state = 'opening';
  let busy = false;
  const listeners = new Set();
  /** The plan of the event playing now (EffectPlan.StepsOf), taken in order by holdBeat. */
  let planned = [];
  let plannedEvent = null;
  /** Sounds left in the event's two-sound budget (sound.md 「1 回の行動につき 2 つまで」). */
  let budget = 2;
  /**
   * Of the budget, how many the event's own action and result still need (review fix: first come, first served let
   * an enemy's incidental guardUp take the slot of the deciding blow's hitBody): sound.md row 26 gives the two
   * slots to the blow's system hit and the Guard's or the body's answer, so an incidental sound plays only on
   * what those leave.
   */
  let essentialLeft = 0;
  let heldFace = null;
  /** The enemy slot the held / selected single-target card frames now (hand's onHoldStart / onTarget); −1 for none. */
  let heldUnit = -1;
  let hoveredFace = null;
  let reachFor = null;
  let frameStanceHint = '';
  let finishedCalled = false;
  let resultSkip = null;
  /** When (real ms) the result card had risen (null before), and the last press that asked to skip it. */
  let resultUpAt = null;
  let resultLastPress = -Infinity;
  let autoTimer = 0;
  let playedThisTurn = 0;
  /** Whether an overlay (journal, settings) holds the input and the scene; set by the scene. */
  let overlayOpen = () => false;
  /** Whether the pile list is open (the cards are not taken, §3.5); set by the scene. */
  let drawerOpen = () => false;
  /** Whether the journal is open (the cards lift on hover but are not taken, §2.4); set by the scene. */
  let journalOpen = () => false;
  /** A refused card is sliding home (returnToHand): an end-turn asked for meanwhile waits for it (review fix). */
  let returningCard = false;
  let endTurnQueued = false;
  /** Tab while nothing is held (battle_ui_ux_v2 §3.4, two or more enemies): the enemy the floor's one N is read to. */
  let restingGapUnit = null;
  /**
   * 俊敏's free cell (battle_ui_ux_v2 §3.8; the source's JS extension CanTakeFreeStep / TakeFreeStep): while the
   * choice waits, { ways: { [-1]: bool, [1]: bool } } (the source's answers); else null.
   */
  let freeStep = null;
  /** The turn whose free cell was asked about (it opens at the turn start only, so the source is asked once a turn). */
  let freeStepTurn = -1;

  const src = () => ctx.source;
  const frameNow = () => src().Frame;
  /** The enemy a cue is about (Cue.Unit), or undefined (the player alone / a one-enemy source that writes none). */
  const unitOf = (x) => (x && x.Unit != null && x.Unit >= 0 ? x.Unit : undefined);
  /** Every enemy frame of `frame`, in the core's order (one-enemy sources: the one). */
  const enemyFrames = (frame) => (frame && frame.Enemies && frame.Enemies.length ? frame.Enemies : frame && frame.Enemy ? [frame.Enemy] : []);
  const unitNo = (uf, k) => (uf && uf.Unit >= 0 ? uf.Unit : k);
  /** The battle has two or more enemies: only then does a target reach the source (Unity's calls otherwise). */
  const multiNow = () => enemyFrames(frameNow()).length > 1;
  /** Enemy `unit`'s omen in `frame` (Frame.Omens[k]); the frame's one omen otherwise. */
  function omenFrameOf(frame, unit) {
    if (frame && frame.Omens && frame.Omens.length && unit != null) {
      const o = frame.Omens.find((x) => x && x.Unit === unit);
      if (o) return o;
    }
    return frame ? frame.Omen : null;
  }
  /** The unit whose omen is lit as acting (§4.4 敵の番), or null. */
  let actingUnit = null;
  /**
   * The enemy's turn is playing (from the turn-end event to the next turn start). Review fix: the writer
   * marks a frame EnemyTurn only while the battle goes on (`_enemyTurn && after.Result == Ongoing`, after =
   * the state the whole move left), so every frame of the turn that ends the battle reads false. The
   * shade and the plate's 40 % then hold from the 敵の番 banner until the result.
   */
  let enemyPhase = false;
  /** Plays the source accepted (the keyboard's auto-select follows a confirmed play only, §3.4). */
  let acceptedPlays = 0;
  /** The enemy whose phase played last in this enemy turn (several enemies: the 600 ms pause, §3.11). */
  let lastEnemyUnit = null;
  /** The last "Guard −n" tag raised: { side, unit, beside, away, span: { x0, x1, y } } (the HP number lines up with it). */
  let guardTag = null;
  /** Notified when a screen event has played out with its after-wait (the scene's one-press fast-forward ends there). */
  const eventEndListeners = new Set();
  /** How many screen events have played out (with their after-waits), and whether one is in its after-wait now. */
  let eventEnds = 0;
  let afterWaiting = false;
  function eventEnded() {
    afterWaiting = false;
    eventEnds += 1;
    eventEndListeners.forEach((fn) => { try { fn(); } catch (err) { console.error(err); } });
  }
  /** The after-wait of the event that has just played (review 19: a fast-forward pressed in it carries on to the next event). */
  async function afterWait(ms) {
    afterWaiting = true;
    await clock.wait(ms);
  }
  /**
   * §5.0 / §5.9 (review 19): the side label the next number of this event carries — a fired trait's 「+5 間合い」 — and the
   * TraitFire cue it came from (its fallback float plays at the event's end when no number took it).
   */
  let pendingSide = null;
  /**
   * §5.12 (review 21): an enemy action's last beats — its badge spent, its step back, a side label still waiting for its
   * number — held at the end of an event whose last cue is the enemy's blow on the player, until the next event shows
   * whether the action goes on (the writer's 「…（2 撃目）」「…（下がる）」 events): { unit, title, side }.
   */
  let heldEnd = null;
  /** The held action the event playing now goes on with (its step in is not taken again), or null. */
  let carried = null;
  /** The card the player's event plays (its face: a push's way, a move's length), or null. */
  let eventFace = null;
  /** The shade and the plate's 40 % are up (the enemy's turn): the 構えの予告 stays off (battle-visual-v1 §4.9). */
  let enemyTurnShown = false;
  /** The pile list's log (scene text lines from the script's words, §1.1 C2). */
  const log = [];
  /** What the battle played, for the result screen (§2.5): cards by CardKind, traits fired. */
  const tally = { played: {}, traits: 0 };
  /**
   * The end-turn plate or X pressed while the flow runs, and pressed again within L.END_TURN_MASH_MS (real time) of the
   * last such press: the press is swallowed and the window slides on (review fix: clicks every 100 ms to hurry the
   * enemy's turn ended the next turn 106 ms after it opened, with six cards unplayed). A press after a pause counts.
   */
  let mashUntil = 0;
  /** When (real ms) the plate or X was last pressed (review 17: the window follows the press rhythm, requestEndTurn). */
  let lastPressAt = -Infinity;
  /** When (real ms) the player was last asked again after a turn start (a turn opening): END_TURN_OPEN_GUARD_MS. */
  let turnOpenedAt = -Infinity;
  /** When (real ms) the player was last asked again (any wait: after a turn start, a play, a free step). */
  let askedAt = -Infinity;
  /** A TurnStart event played since the player was last asked. */
  let turnStarted = false;

  /** SetHandInteractable: the hand takes input or not, and the dim / guide line follow at once. */
  function setHandInteractable(on) {
    hand.setInteractable(on);
    refreshPlayable();
  }

  function setState(s) {
    if (state === s) return;
    state = s;
    listeners.forEach((fn) => { try { fn(s); } catch (err) { console.error(err); } });
  }

  function sfx(name, delay, mult) {
    if (!name) return;
    env.audio.play(name, delay, mult);
  }
  /**
   * A result sound inside the event's two-sound budget. `essential`: the blow's system hit, the body's hit, the
   * Guard's block or break (the slots sound.md keeps for the action and its result); anything else plays only on a
   * slot those do not need.
   */
  function sfxBudget(name, essential) {
    if (!name || budget <= 0) return;
    if (essential) essentialLeft = Math.max(0, essentialLeft - 1);
    else if (budget - essentialLeft <= 0) return;
    budget -= 1;
    sfx(name);
  }
  /** How many essential sounds `ev`'s cues will ask for (the same choices playCue makes). */
  function essentialSoundsOf(ev) {
    let n = 0;
    ev.Cues.forEach((cue, i) => {
      if (cue.Kind === CueKind.Slash) n += 1;
      else if (cue.Kind === CueKind.Hit && cue.Target === UnitSide.Player) n += 1;
      else if (cue.Kind === CueKind.GuardBlock) {
        const next = ev.Cues[i + 1];
        if (!(next && next.Kind === CueKind.Hit && next.Target === cue.Target)) n += 1;
      }
    });
    return n;
  }

  // ---- effects and the plan's waits (#495) ------------------------------------------------

  function safe(play) {
    try {
      const p = play ? play() : undefined;
      return Promise.resolve(p).catch((err) => console.error(err));
    } catch (err) {
      console.error(err);
      return Promise.resolve();
    }
  }

  /** Effect: a beat beside the flow. Off, it settles at once. */
  function effect(id, play, settle) {
    if (!switches.IsOn(id)) {
      if (settle) settle();
      return Promise.resolve();
    }
    return safe(play);
  }

  /** FloatEffect: a rising number, as an effect. */
  function floatEffect(id, at, text, role, size, o) {
    if (!switches.IsOn(id)) return;
    safe(() => field.floatText(at, text, role, size, o));
  }

  /**
   * Scene addition (review: the numbers rose into the omen badge): a float stops 8 px under every omen badge
   * standing over its path, so the badge's kind word and hit mark stay readable (battle_ui_ux_v2 §5.0:
   * 数字は札に入りません). The field measures the float and keeps its top under the badges it would cross.
   */
  function badgeBoxes() {
    return env.geom.omenBoxes ? env.geom.omenBoxes() : [];
  }
  /** The floor line a float's path stays over (FLOAT_OVER_FEET). */
  const floatFloor = L.FEET_Y - FLOAT_OVER_FEET;
  const UNDER_BADGES = { avoid: badgeBoxes, avoidGap: FLOAT_UNDER_BADGE, lowest: floatFloor };

  /** The x of `side`'s figure now, for a float to ride along with (fx.floatText's `track`). */
  function trackOf(side, unit) {
    return () => {
      const g = env.geom.figure(side, unit);
      return g ? g.x : null;
    };
  }

  function beginPlan(ev) {
    planned = EffectPlan.StepsOf(ev).slice();
    plannedEvent = ev;
    budget = 2;
    essentialLeft = Math.min(budget, essentialSoundsOf(ev));
    pendingSide = null;
    multLabels = multLabelsOf(ev);
    logEvent(ev);
  }

  /**
   * The pile list's log (battle_ui_ux_v2 §1.1 C2 / C4; review 19 — Unity's PileListView keeps three tabs and waits for a
   * log in the script): one heading line per event the player sees (whose move, by the event's title) and under it the
   * kinds of line the spec names, each in the script's own words — 特性 (TraitFire), 状態 (StatusChange), スタンス (a stance
   * set, with its count from the event's frame), 山札作り直し (DrawHand.RebuildAfter) — and the numbers the floats show
   * (HP / Guard / the wall's, the HP a killing blow took). The hud shows them on the list's ログ全文 tab.
   */
  function logEvent(ev) {
    if (!ev || !ev.Cues) return;
    const lines = [];
    if (ev.Kind === DepictionEventKind.TurnStart) lines.push(LOG.turn(ev.After && ev.After.Corner ? ev.After.Corner.Turn : ''));
    else if (ev.Kind === DepictionEventKind.TurnEnd) lines.push(LOG.enemyTurn);
    // Review 21: the writer's turn-start beat of an enemy (its Guard back to 0, its recovery, its ticks) is titled 敵の番 —
    // no action of its own, so no heading (it read 「敵: 敵の番」); its lines say whose they are.
    else if (ev.Kind === DepictionEventKind.EnemyAction && ev.Title && ev.Title !== DepictionText.EnemyTurn) lines.push(LOG.foe(logWho(UnitSide.Enemy, unitOf(ev), ev.After), ev.Title));
    else if ((ev.Kind === DepictionEventKind.PlayCard || ev.Kind === 'FreeStep') && ev.Title) lines.push(LOG.mine(ev.Title));
    const hpNow = new Map();
    const hpOf = (c) => {
      const key = c.Target + ':' + (c.Target === UnitSide.Enemy ? unitOf(c) : '');
      if (!hpNow.has(key)) { const S = hud.status(c.Target, c.Target === UnitSide.Enemy ? unitOf(c) : undefined); hpNow.set(key, S ? S.hp : null); }
      return key;
    };
    const lost = (c) => {
      const key = hpOf(c);
      const before = hpNow.get(key);
      if (c.HpAfter !== UNCHANGED) hpNow.set(key, c.HpAfter);
      return before != null && c.HpAfter !== UNCHANGED && before - c.HpAfter > 0 && before - c.HpAfter < c.Amount ? before - c.HpAfter : c.Amount;
    };
    ev.Cues.forEach((c) => {
      const who = logWho(c.Target, unitOf(c), ev.After);
      switch (c.Kind) {
        case CueKind.TraitFire:
          if (String(c.Text).startsWith(STANCE_SET_PREFIX)) {
            const name = String(c.Text).slice(STANCE_SET_PREFIX.length);
            lines.push(LOG.stance(name, stanceCount(ev.After, c, name)));
          } else if (MOVE_BLOCKED_LINES.has(c.Text) || STANCE_LINE.test(c.Text)) lines.push('　' + c.Text);
          else {
            // §5.9 (review 21): the condition by its name, as the floats say it (「特性: 温存 Guard+3」, not 「残4」).
            const tr = traitOf(ev, c);
            lines.push(LOG.trait(tr && tr.condition ? tr.condition + ' ' + tr.effect : c.Text));
          }
          break;
        case CueKind.StatusChange: lines.push(LOG.status(c.Target === UnitSide.Enemy ? who : '', c.Text, c.StacksAfter)); break;
        case CueKind.DrawHand: if (c.RebuildAfter >= 0 && c.Amount > 0) lines.push(LOG.rebuild); break;
        case CueKind.Slash: if (c.HpAfter !== UNCHANGED && c.Amount > 0) lines.push(LOG.hp(who, lost(c))); break;
        case CueKind.GuardBlock: lines.push((wallOf(ev, c) ? LOG.wall : LOG.guard)(who, c.Amount)); break;
        case CueKind.Hit: lines.push((wallOf(ev, c) ? LOG.wall : LOG.hp)(who, lost(c))); break;
        case CueKind.HpChange: hpOf(c); if (c.HpAfter !== UNCHANGED) hpNow.set(hpOf(c), c.HpAfter); lines.push('　' + who + ' ' + c.Text + ' ' + (c.Amount > 0 ? '+' : '') + c.Amount); break;
        case CueKind.Defeat: lines.push(LOG.fell(who)); break;
        default: break;
      }
    });
    if (!lines.length) return;
    log.push(...lines);
    if (log.length > LOG_MAX) log.splice(0, log.length - LOG_MAX);
    if (hud.setLog) hud.setLog(log);
  }
  /**
   * Review 21 (the trio's log read 「敵: 払い斬り」 / 「　敵 HP −23」 for all three): who a log line is about — 自分, or with one
   * enemy 敵; with two or three each enemy by its roster name (UnitFrame.ArtId), with its hatch glyph (／＼｜) when another
   * enemy of the fight bears the same name.
   */
  function logWho(side, unit, frame) {
    if (side !== UnitSide.Enemy) return LOG_WHO[side] || '';
    const list = enemyFrames(frame || frameNow());
    if (list.length < 2) return LOG_WHO.Enemy;
    const uf = list.find((x, k) => unitNo(x, k) === unit);
    const name = uf ? enemyNameOf(uf.ArtId) : '';
    if (!name) return LOG_WHO.Enemy;
    const twin = list.some((x, k) => unitNo(x, k) !== unit && enemyNameOf(x.ArtId) === name);
    return twin ? name + hatchGlyph(uf.Hatch) : name;
  }
  /** How many of stance `name` the unit a cue is about holds in `frame` (its StanceRow.Count; 1 when unknown). */
  function stanceCount(frame, cue, name) {
    if (!frame) return 1;
    const uf = cue.Target === UnitSide.Player ? frame.Player : (enemyFrames(frame).find((x, k) => unitNo(x, k) === unitOf(cue)) || frame.Enemy);
    const row = uf && (uf.Stances || []).find((r) => r.Name === name);
    return row ? row.Count | 0 : 1;
  }

  /**
   * §5.7 順 7 (review 19): whether `cue` (a GuardBlock or a Hit) is a wall's — and which way the one it struck was pushed
   * (−1 left, +1 right). The writer marks a player shoved into the end of the line by its event (WALL_TITLE); the wall a
   * player's push drives an enemy into is the blow on that enemy after the push in the card's own event (§5.4: the card's
   * blow comes before its push, so a blow after it is the wall's).
   */
  function wallOf(ev, cue) {
    if (!ev || (cue.Kind !== CueKind.Hit && cue.Kind !== CueKind.GuardBlock)) return null;
    if (ev.Kind === DepictionEventKind.EnemyAction && ev.Title === WALL_TITLE && cue.Target === UnitSide.Player) return { dir: -1 };
    if (ev.Kind !== DepictionEventKind.PlayCard || cue.Target !== UnitSide.Enemy) return null;
    const i = ev.Cues.indexOf(cue);
    const push = ev.Cues.slice(0, i).find((c) => c.Kind === CueKind.RangeSwitch && c.Unit != null && c.Unit >= 0 && c.Unit === cue.Unit && c.UnitCellAfter > 0);
    if (!push) return null;
    const pulls = eventFace && (eventFace.Values || []).some((v) => v.Kind === CardValueKind.Pull);
    return { dir: pulls ? -1 : 1 };
  }

  /**
   * The side label waiting for this event's next number, taken by the float that shows it (§5.0 主数字 + 脇ラベル), and
   * after it the multiplier the blow `cue` carried (§5.0 順 特性 → 状態: 「−18 +5 間合い ×1.5 強化」).
   */
  function takeSide(kind, cue) {
    let text = '';
    if (pendingSide && (pendingSide.kind === kind || pendingSide.kind === 'any')) {
      text = pendingSide.text;
      pendingSide = null;
    }
    const mult = cue ? multLabels.get(cue) : '';
    return mult ? (text ? text + ' ' + mult : mult) : text;
  }
  /** The multiplier label of each blow of the event playing (multLabelsOf), by the cue whose number shows it. */
  let multLabels = new Map();
  /**
   * §5.0 / §5.6 順 7 / §5.10 (review 21): which blows of `ev` carried a status multiplier, from the event's own cues — a
   * StatusChange that spends 脆化 on the side struck or 強化 on the striker belongs to the nearest blow of that pair (the one
   * before it, else the one after: the writer spends 強化 after its blow, and opens a second blow's event where its 脆化 is
   * spent). One label per blow, 脆化 first. Keyed by the cue whose number the blow shows: its Hit, else its GuardBlock, else
   * its Slash with a settled HP.
   */
  function multLabelsOf(ev) {
    const map = new Map();
    if (!ev || !ev.Cues) return map;
    const groups = [];
    ev.Cues.forEach((c, i) => {
      if (c.Kind === CueKind.Slash) {
        groups.push({ at: i, end: i, slash: c, number: c.HpAfter !== UNCHANGED ? c : null, fragile: false, empower: false });
      } else if ((c.Kind === CueKind.GuardBlock || c.Kind === CueKind.Hit) && groups.length) {
        const g = groups[groups.length - 1];
        if (g.end === i - 1 && g.slash.Target === c.Target && unitOf(g.slash) === unitOf(c)) { g.end = i; g.number = c; }
      }
    });
    if (!groups.length) return map;
    const sameUnit = (side, a, b) => side === UnitSide.Player || unitOf(a) === unitOf(b);
    ev.Cues.forEach((c, i) => {
      if (c.Kind !== CueKind.StatusChange || !(c.Amount < 0)) return;
      const word = String(c.Text || '');
      const fragile = word === FRAGILE_WORD;
      if (!fragile && word !== EMPOWER_WORD) return;
      const fits = groups.filter((g) => (fragile ? g.slash.Target : EffectPlan.AttackerOf(g.slash)) === c.Target && sameUnit(c.Target, g.slash, c));
      const before = fits.filter((g) => g.at < i);
      const g = before.length ? before[before.length - 1] : fits.find((x) => x.at > i);
      if (!g) return;
      if (fragile) g.fragile = true; else g.empower = true;
    });
    for (const g of groups) {
      if (!g.number || (!g.fragile && !g.empower)) continue;
      map.set(g.number, MULT_LABEL[g.fragile ? FRAGILE_WORD : EMPOWER_WORD]);
    }
    return map;
  }
  /**
   * The event is over and its fired trait's label found no number to stand beside (a trait of スタミナ / ドロー / 状態 …):
   * it rises alone, a word on the chest's path, as 「動けず」 does (§5.0's examples).
   */
  function flushSide() {
    const p = pendingSide;
    pendingSide = null;
    if (!p) return;
    // A blow that found nobody (空振り, §5.8) added nothing: its bonus is struck off with it, and no label rises.
    if (plannedEvent && plannedEvent.Cues.some((c) => c.Kind === CueKind.SideBonusMiss)) return;
    const g = env.geom.figure(p.side, p.unit);
    if (!g) return;
    field.floatText({ x: g.chest.x, y: g.chest.y }, p.text, p.side === UnitSide.Player ? 'warm' : 'ink', 32,
      Object.assign({ plain: true, rise: Math.max(0, g.chest.y - g.head.y), track: trackOf(p.side, p.unit) }, UNDER_BADGES));
  }

  function endPlan() {
    if (plannedEvent && planned.length > 0) {
      console.error('[battle] event ' + plannedEvent.Order + ' ' + plannedEvent.Title
        + ': EffectPlan lists waits the screen did not hold: ' + planned.map((s) => s.Id + ' x' + s.Count).join(', '));
    }
    planned = [];
    plannedEvent = null;
  }

  function takePlanned(id, count) {
    if (!plannedEvent) return { Id: id, Count: count, LeadMs: null };
    const where = '[battle] event ' + plannedEvent.Order + ' ' + plannedEvent.Title + ': ';
    if (planned.length === 0 || planned[0].Id !== id) {
      console.error(where + 'the screen waits for ' + id + ' where EffectPlan lists ' + (planned.length ? planned[0].Id : 'nothing more'));
      return { Id: id, Count: count, LeadMs: null };
    }
    const step = planned.shift();
    if (step.Count !== count) console.error(where + id + ' plays ' + count + ' items where EffectPlan lists ' + step.Count);
    return step;
  }

  /**
   * HoldBeat: the flow is held for the plan's wait of this step while `play` (a thunk returning a
   * Promise) runs beside it; a step with a lead holds the lead only and lets the beat play on.
   * `played` runs once the beat has played out (or settled).
   */
  async function holdBeat(id, play, settle, count = 1, played = null) {
    const step = takePlanned(id, count);
    if (!switches.IsOn(id)) {
      if (settle) settle();
      if (played) played();
      return;
    }
    const waitMs = EffectPlan.WaitMs(step, switches);
    if (step.LeadMs != null) {
      safe(play).then(() => { if (alive && played) played(); });
      await clock.wait(waitMs);
      return;
    }
    const waited = clock.wait(waitMs);
    await safe(play);
    await waited;
    if (played) played();
  }

  // ---- frame → screen ----------------------------------------------------------------------

  /** Whether the screen shows the enemy's turn for `frame` (EnemyTurn, held to the result on the deciding turn). */
  function enemyTurnOf(frame) {
    if (!frame) return false;
    return !!frame.EnemyTurn || (enemyPhase && frame.Outcome !== BattleOutcome.Ongoing);
  }

  function applyFrame(frame, first = false) {
    if (!frame) return;
    // Review 21 (§5.12: the badge vanished between a two-blow action's blows): while an action is held across its events,
    // its badge keeps the omen it acts on until the action ends (playHeldEnd fades it).
    const held = heldEnd || carried;
    hud.applyFrame(frame, { first, keepOmen: held ? held.unit : null });
    field.applyFrame(frame, { first });
    showEnemyTurn(enemyTurnOf(frame));
    // §5.11 順 2 (#501): the body's light while the list holds a stance, faded beside the flow.
    if (!first) {
      applyStanceGlow(UnitSide.Player, frame.Player);
      enemyFrames(frame).forEach((uf, k) => { if (!uf.Down) applyStanceGlow(UnitSide.Enemy, uf, unitNo(uf, k)); });
    }
    // §3.5 集中 / 威圧, §5.10 強化 (review 21): the statuses the player holds mark the cards they act on.
    if (hand.setStatusMarks) hand.setStatusMarks(statusMarksOf(frame.Player));
    if (!hand.sameHand(frame.Hand)) hand.build(frame.Hand);
    else hand.bind(frame.Hand);
    hand.layout();
    frameStanceHint = frame.StanceHint || '';
    showReserveHint();
    showHpLoss();
    showLowHp(frame.Player ? frame.Player.Hp : 0);
  }

  /** The player's 集中 / 強化 / 威圧 chips (UnitFrame.Statuses, by their labels) as the hand's marks. */
  function statusMarksOf(uf) {
    const held = new Set(((uf && uf.Statuses) || []).filter((c) => c && c.Stacks > 0).map((c) => c.Label));
    return {
      focus: held.has(EnumTokens.ToLabel(StatusKind.Focus)),
      empower: held.has(EMPOWER_WORD),
      intimidate: held.has(EnumTokens.ToLabel(StatusKind.Intimidate)),
    };
  }
  /** §5.6 順 5: the low-HP vignette rests while the player's HP is under 15 (and above 0). */
  function showLowHp(hp) {
    if (field.setLowHp) field.setLowHp(hp > 0 && hp < L.HEARTBEAT_HP);
  }
  /**
   * §5.6 順 5: a blow that landed on the player with HP under 15 pulses the vignette (beside the flow). The spec's
   * low heartbeat with it is not played: sound.md keeps the heartbeat to the turn start (3 times, 「常には鳴らしません」),
   * and a blow's two sounds (its system's hit and the body's) already fill the action's budget of two.
   */
  function pulseLowHp(hp) {
    showLowHp(hp);
    if (hp > 0 && hp < L.HEARTBEAT_HP && field.pulseLowHp) safe(() => field.pulseLowHp(L.VIGNETTE_PULSE_MS));
  }

  /**
   * The enemy a held single-target card is read on now (the one it frames; −1 for a self card): its cost and 構えの
   * 予告 come from that enemy's slot (CardFace.CostFor / StanceHintAfterPayFor; review fix — 石礫 framed on the enemy at
   * N 1 showed the 1 it costs on the one at N 2 and then charged 2).
   */
  function heldSlot() {
    return heldFace && heldFace.Aim === CardAim.Single && heldUnit >= 0 ? heldUnit : -1;
  }
  /** §4.7 / §5.2: "▸ n" and the pay rings: the held card's cost on the enemy it frames. */
  function showHeldCost() {
    if (!heldFace) { hud.stamina.showCost(0); return; }
    const u = heldSlot();
    hud.stamina.showCost(u >= 0 && typeof heldFace.CostFor === 'function' ? heldFace.CostFor(u) : heldFace.Cost);
  }
  /** §4.9 / §5.3: while a card is held, the 構えの予告 reads as it would once the card is paid for. */
  function showReserveHint() {
    const u = heldSlot();
    const afterPay = !heldFace ? null : u >= 0 && typeof heldFace.StanceHintAfterPayFor === 'function' ? heldFace.StanceHintAfterPayFor(u) : heldFace.StanceHintAfterPay;
    // battle-visual-v1 §4.9 「敵の番には出しません」 (review 19: it stood through the whole 敵の番 banner).
    hud.reserveHint(enemyTurnShown ? '' : afterPay != null ? afterPay : frameStanceHint);
  }

  /**
   * §4.5 HP の減る分: the wick stripes on an enemy's HP bar while a card is held (CardFace.HpLossFor(k)). With one
   * enemy this is Unity's HpLossFor(0) for any held card. With several, a card aimed at one enemy wicks the enemy
   * it is framed on only; a card aimed at nobody (one that strikes every enemy in reach) wicks each by its own.
   */
  function showHpLoss() {
    const frame = frameNow();
    const list = enemyFrames(frame);
    const aimed = !!heldFace && heldFace.Aim === CardAim.Single;
    let framed = aimed && hand.framedUnit ? hand.framedUnit() : -1;
    if (aimed && framed < 0) framed = heldFace.TargetUnit >= 0 ? heldFace.TargetUnit : 0;
    // Several enemies: the floor's one N is the framed enemy's while the card is up (field.showGapTo), else
    // the one Tab moved it to (§3.4), else the frame's own (the nearest).
    if (restingGapUnit != null && !list.some((uf, k) => unitNo(uf, k) === restingGapUnit && !uf.Down)) restingGapUnit = null;
    if (field.showGapTo) field.showGapTo(list.length > 1 ? (aimed ? framed : restingGapUnit) : null);
    // Review 19 (a Tab at rest changed only the floor's N, over the same empty cells): the enemy it chose wears the
    // battle's focus, the amber edge, on its number panel while nothing is held (a held card's frame is its focus then).
    if (hud.setRestFocus) hud.setRestFocus(list.length > 1 && !heldFace && restingGapUnit != null ? restingGapUnit : null);
    if (list.length <= 1) {
      hud.status(UnitSide.Enemy).showHpLoss(heldFace && heldFace.HpLossFor ? heldFace.HpLossFor(0) : 0);
      return;
    }
    list.forEach((uf, k) => {
      const u = unitNo(uf, k);
      const n = heldFace && heldFace.HpLossFor && (!aimed || u === framed) ? heldFace.HpLossFor(u) : 0;
      hud.status(UnitSide.Enemy, u).showHpLoss(n);
    });
  }

  /**
   * The badge of each enemy that shows the held / selected card's prediction tag stands higher for it. An enemy
   * framed with no tag (out of reach: steel hooks) keeps its badge 14 px over the head (OmenOverHead), as Unity.
   */
  function liftOmenForHeld() {
    if (!hud.liftOmen) return;
    hud.liftOmen(heldFace && hand.taggedUnits ? hand.taggedUnits() : []);
  }

  /** UpdateFloorReach: the cells an enemy-aimed card reaches light while it is hovered (not busy) or held. */
  function updateFloorReach() {
    let face = heldFace || (!busy ? hoveredFace : null);
    if (!face || face.Aim !== CardAim.Single) face = null;
    if (face === reachFor) return;
    reachFor = face;
    if (face) field.showReach(face.ReachCells || []);
    else field.hideReach();
  }

  /** ShowEnemyTurn: the shade below y 790 and the end-turn plate at 40%; the 構えの予告 is off meanwhile (§4.9). */
  function showEnemyTurn(on) {
    on = !!on;
    field.setEnemyTurnShade(on);
    hud.setEnemyTurn(on);
    if (on !== enemyTurnShown) { enemyTurnShown = on; showReserveHint(); }
  }

  // ---- the panels and the badge follow the figures (FollowFigures / FollowPanels) -------------

  let snapPanels = true;
  /**
   * Where the panels and the badge follow a figure: its home (Unity's FollowPanels; a lunge never drags them), but
   * the figure itself while it walks on the floor (review fix: MoveOnFloor sets the home to the new cell as the walk
   * starts, so the panel's exponential follow raced up to 170 px ahead of a hero still easing out of its cell).
   */
  const followX = (g) => (g.walking ? g.x : g.homeX);
  function followFigures() {
    const frame = frameNow();
    if (!frame) return;
    const p = env.geom.figure(UnitSide.Player);
    const e = env.geom.figure(UnitSide.Enemy);
    if (!p || !e) return;
    const units = [];
    enemyFrames(frame).forEach((uf, k) => {
      const u = unitNo(uf, k);
      const g = env.geom.figure(UnitSide.Enemy, u);
      if (g) units.push({ unit: u, x: followX(g), top: L.FEET_Y - g.height, size: Math.max(1, uf.Size || 1), down: !!uf.Down || !!g.down, walking: !!g.walking });
    });
    hud.follow({
      playerX: followX(p),
      playerWalking: !!p.walking,
      enemyX: followX(e),
      enemyTop: L.FEET_Y - e.height,
      cells: frame.Floor ? frame.Floor.Cells : 0,
      enemySize: frame.Floor ? Math.max(1, frame.Floor.EnemySize) : 1,
      units,
      snap: snapPanels,
    });
    snapPanels = false;
  }

  // ---- stance glows (§5.11 順 2, #501) ------------------------------------------------------

  function applyStanceGlow(side, unitFrame, unit) {
    const shown = field.stanceGlowShown(side, unit);
    const change = StanceGlow.Change(shown, unitFrame);
    if (change === 'Keep') return;
    const on = change === 'Light';
    effect(EffectId.StanceGlow, () => field.fadeStanceGlow(side, on, ms(EffectId.StanceGlow), unit), () => field.setStanceGlow(side, on, unit));
  }

  function lightStanceGlowWithCrest(ev, cue) {
    if (cue.Fx !== FxSystem.Form || !ev.After) return;
    if (cue.Target === UnitSide.Player) { applyStanceGlow(UnitSide.Player, ev.After.Player); return; }
    const u = unitOf(cue);
    const uf = u != null ? enemyFrames(ev.After).find((x, k) => unitNo(x, k) === u) : ev.After.Enemy;
    applyStanceGlow(UnitSide.Enemy, uf || ev.After.Enemy, u);
  }

  // ---- flow -----------------------------------------------------------------------------------

  async function runAutomaticEvents() {
    busy = true;
    setState('auto');
    setHandInteractable(false);
    updateFloorReach();
    let next = null;
    while (alive && !halted && (next || (!src().Finished && !src().WaitingForPlayer))) {
      let ev = next;
      next = null;
      if (!ev) {
        try { ev = ctx.source.AdvanceAuto(); } catch (err) { sourceFailed(err); return; }
      }
      // battle_ui_ux_v2 §3.11 / §5.13: enemies act one after another with a 600 ms pause between them
      // (none between an elite's two actions): the wait after the last enemy's event is made up to it.
      if (startsOtherEnemy(ev)) {
        await clock.wait(Math.max(0, L.BETWEEN_ENEMIES_MS - EffectFlow.AfterAutoEventMs));
        if (!alive) return;
      }
      await playEvent(ev);
      if (!alive) return;
      if (heldEnd) {
        // §5.12 (review 21): the next event is looked at before the pause — the same action going on (its second blow, its
        // step back) plays on from where this one left the enemy; anything else first lets the action end.
        if (!src().Finished && !src().WaitingForPlayer) {
          try { next = ctx.source.AdvanceAuto(); } catch (err) { sourceFailed(err); return; }
        }
        if (next && continuesHeld(next)) {
          carried = heldEnd;
          heldEnd = null;
          await afterWait(CONTINUE_PAUSE_MS);
          if (!alive) return;
          eventEnded();
          continue;
        }
        await playHeldEnd();
        if (!alive) return;
      }
      await afterWait(EffectFlow.AfterAutoEventMs);
      if (!alive) return;
      eventEnded();
    }
    busy = false;
    if (!alive || halted) return;
    if (src().Finished) {
      await finish();
      return;
    }
    if (turnStarted) { turnStarted = false; turnOpenedAt = real.now; }
    askedAt = real.now;
    setState('idle');
    openFreeStep();
    setHandInteractable(true);
    refreshPlayable();
    scheduleAuto();
  }

  /** The event opens another enemy's phase than the one that played last in this enemy turn (several enemies). */
  function startsOtherEnemy(ev) {
    if (!ev) return false;
    if (ev.Kind !== DepictionEventKind.EnemyAction && ev.Kind !== DepictionEventKind.NextOmen) {
      if (ev.Kind !== DepictionEventKind.Defeat) lastEnemyUnit = null;
      return false;
    }
    const u = unitOf(ev);
    if (u == null || !multiNow()) return false;
    const other = lastEnemyUnit != null && u !== lastEnemyUnit;
    lastEnemyUnit = u;
    return other;
  }

  /** `ev` goes on with the held action: the same enemy's event titled after it (「二段斬り（2 撃目）」, 「退き撃ち（下がる）」). */
  function continuesHeld(ev) {
    const h = heldEnd;
    if (!h || !ev || ev.Kind !== DepictionEventKind.EnemyAction || !h.title) return false;
    const u = unitOf(ev);
    if (u != null && h.unit != null && u !== h.unit) return false;
    return String(ev.Title || '').startsWith(h.title + '（');
  }
  /** Whether enemy `u` (a playEvent unit: undefined with one enemy) is the one whose action is held. */
  function isHeld(u) {
    return !!heldEnd && (u === undefined || heldEnd.unit == null || u === heldEnd.unit);
  }
  /**
   * The held action ends (review 21): its side label rises if no number took it, its badge is spent and it steps back home —
   * the OmenSpend and EnemyReturn beats its blow's event left out (Hit), as long as they would have held there.
   */
  async function playHeldEnd() {
    const h = heldEnd || carried;
    heldEnd = null;
    carried = null;
    if (!h) return;
    if (h.side) { pendingSide = h.side; flushSide(); }
    const badge = hud.omenOf(h.unit);
    await effect(EffectId.OmenSpend, () => badge.fadeOut(ms(EffectId.OmenSpend)), () => badge.hideNow());
    if (!alive) return;
    const u = h.unit;
    if (field.enemyAway(u)) await effect(EffectId.EnemyReturn, () => field.returnHome(UnitSide.Enemy, ms(EffectId.EnemyReturn), u), () => field.snapHome(UnitSide.Enemy, u));
    else field.setPose(UnitSide.Enemy, FigurePose.Idle, u);
  }

  async function playEvent(ev) {
    beginPlan(ev);
    // The held action goes on (review 21): the label its first event's trait left waits for this event's number.
    if (carried && carried.side) { pendingSide = carried.side; carried.side = null; }
    if (ev.Kind === DepictionEventKind.TurnStart) enemyPhase = false;
    else if (ev.Kind === DepictionEventKind.TurnEnd) enemyPhase = true;
    else if (ev.After && !ev.After.EnemyTurn && ev.After.Outcome === BattleOutcome.Ongoing) enemyPhase = false;
    // §4.4 敵の番: the acting light goes out with any other event, and when another enemy's phase begins.
    if (ev.Kind !== DepictionEventKind.EnemyAction || (actingUnit != null && unitOf(ev) !== actingUnit)) {
      hud.omen.setActing(false);
      actingUnit = null;
    }
    // §5.5: the screen comes back up as the event that ends the enemy's turn starts.
    if (ev.After && !enemyTurnOf(ev.After)) showEnemyTurn(false);
    if (ev.Kind === DepictionEventKind.TurnStart) {
      playedThisTurn = 0;
      turnStarted = true;
      // Review fix (§2.2 「静止時はいちばん近い敵」): a Tab choice of the floor's N holds for the turn it was made in;
      // a new turn reads the nearest enemy again (it stayed on a far enemy for turns while the near one closed in).
      restingGapUnit = null;
      const hp = ev.After ? ev.After.Player.Hp : 0;
      if (hp > 0 && hp < L.HEARTBEAT_HP) for (let k = 0; k < 3; k++) sfx('heartbeat', k * 900);
      await holdBeat(EffectId.TurnBanner, () => hud.banner(DepictionText.YourTurn, 'player', ms(EffectId.TurnBanner)), null);
    }
    // §5.1 順 8 「順 6 の後」 (review 16; Unity blinks right after the banner): the standing omens blink once the turn
    // start's draw has played — after its DrawHand cue, or after its cues when it draws nothing.
    let blinked = ev.Kind !== DepictionEventKind.TurnStart;
    for (const cue of ev.Cues) {
      if (!alive) return;
      await playCue(ev, cue);
      if (!blinked && cue.Kind === CueKind.DrawHand) { blinked = true; blinkStanding(ev); }
    }
    if (!alive) return;
    if (!blinked) blinkStanding(ev);
    if (ev.Kind === DepictionEventKind.TurnEnd) {
      sfx('foeTurn');
      // battle-visual-v1 §4.9 / §5.5 (review 19; Unity shades only once the TurnEnd frame lands, after the banner): the
      // enemy's turn begins with its banner — the hand area sinks and the plate goes to 40 % as it comes in.
      if (!ev.After || ev.After.Outcome === BattleOutcome.Ongoing) showEnemyTurn(true);
      await holdBeat(EffectId.EnemyTurnBanner, () => hud.banner(DepictionText.EnemyTurn, 'enemy', ms(EffectId.EnemyTurnBanner)), null);
    }
    // §4.2: with a floor, home is each enemy's cell after the event, so the step back carries it there.
    const floorShown = ev.After && ev.After.Floor && ev.After.Floor.Cells > 0;
    const after = enemyFrames(ev.After);
    const list = after.length > 1 ? after.map((uf, k) => ({ u: unitNo(uf, k), uf })) : [{ u: undefined, uf: after[0] }];
    for (const { u, uf } of list) {
      if (floorShown) {
        if (u === undefined) field.setEnemyHomeCell(ev.After.Floor.EnemyCell, ev.After.Floor.EnemySize);
        else if (uf && !uf.Down) field.setEnemyHomeCell(uf.Cell, uf.Size, u);
      }
      // Review 21: an enemy whose action is held stays where its blow left it until the next event says whether it goes on.
      if (isHeld(u)) continue;
      if (field.enemyAway(u)) {
        effect(EffectId.EnemyReturn, () => field.returnHome(UnitSide.Enemy, ms(EffectId.EnemyReturn), u), () => field.snapHome(UnitSide.Enemy, u));
      } else field.setPose(UnitSide.Enemy, FigurePose.Idle, u);
    }
    field.setPose(UnitSide.Player, FigurePose.Idle);
    // A label still waiting goes with the held action (退き撃ち's 「+3 温存」 belongs to the Guard of its step back).
    if (heldEnd && pendingSide) { heldEnd.side = pendingSide; pendingSide = null; }
    flushSide();
    endPlan();
    applyFrame(ev.After);
    // The event went on with a held action and did not hold it again: the action ends here.
    if (carried && !heldEnd) await playHeldEnd();
    else carried = null;
  }

  /**
   * The turn start's omen blink (§5.1 順 8): every omen already standing blinks once, 120 apart in the enemies' action
   * order (Frame.Omens is in the core's enemy order, which is the phase order); one shown in this event pops instead.
   */
  function blinkStanding(ev) {
    const popping = new Set(ev.Cues.filter((c) => c.Kind === CueKind.OmenShow).map((c) => unitOf(c) ?? 0));
    const omens = ev.After ? (ev.After.Omens && ev.After.Omens.length ? ev.After.Omens : [ev.After.Omen]) : [];
    const anyShown = ev.Cues.some((c) => c.Kind === CueKind.OmenShow);
    let blinkAt = 0;
    omens.forEach((o, k) => {
      if (!o || !o.Visible) return;
      const u = o.Unit >= 0 && omens.length > 1 ? o.Unit : k;
      if (omens.length > 1 ? popping.has(u) : anyShown) return;
      const delay = blinkAt * L.OMEN_BLINK_STAGGER_MS;
      blinkAt += 1;
      const badge = hud.omenOf(omens.length > 1 ? u : undefined);
      effect(EffectId.OmenBlink, () => (delay > 0 ? clock.wait(delay) : Promise.resolve()).then(() => (alive ? badge.blink(ms(EffectId.OmenBlink)) : undefined)));
    });
  }

  async function playCue(ev, cue) {
    const T = cue.Target;
    /** The enemy of the exchange (Cue.Unit); the target's unit when the target is an enemy. */
    const foe = unitOf(cue);
    const tu = T === UnitSide.Enemy ? foe : undefined;
    const S = hud.status(T, tu);
    const fig = env.geom.figure(T, tu) || { chest: { x: 960, y: 423 }, head: { x: 960, y: 380 } };
    const chest = fig.chest;
    const head = fig.head;
    // battle_ui_ux_v2 §5.0 / the DS Notices (review 19; Unity starts 40 px over the chest and rises 96): every floating
    // number and word starts on the chest (65 % of the figure's height) and rises to the head (84 %) in 600 ms.
    const numberAt = { x: chest.x, y: chest.y };
    const labelAt = numberAt;
    const rise = Math.max(0, chest.y - head.y);
    // Review fix (§5.7 順 6: 「押し出し」 belongs to the side that is moved): a number or a label rides with the figure
    // it is about, so a shove carries them along instead of leaving them over the cell it left.
    const under = Object.assign({ track: trackOf(T, tu), rise }, UNDER_BADGES);
    // §5.7 順 7 (review 19): a wall's blow — the edge it hit lights, the figure takes 打's ripple, the number says 壁.
    const wall = wallOf(ev, cue);

    // §5.3 (#57): a beat of a play draws its system's shape beside it.
    switch (EffectGrammar.MarkOf(cue)) {
      case FxMark.Shape: {
        const crestLight = StanceGlow.CrestCarriesLight(field.stanceGlowShown(T, tu));
        effect(EffectId.GrammarMark, () => field.mark(T, { fx: cue.Fx, intensity: cue.Intensity, ms: ms(EffectId.GrammarMark), crestLight, unit: tu }));
        lightStanceGlowWithCrest(ev, cue);
        break;
      }
      case FxMark.ChestHeave:
        effect(EffectId.BreathHeave, () => field.heave(T, ms(EffectId.BreathHeave), tu));
        break;
      default:
        break;
    }

    switch (cue.Kind) {
      case CueKind.GuardReset:
        S.setGuard(cue.GuardAfter);
        break;

      case CueKind.StaminaChange:
        if (cue.Amount > 0) {
          if (T === UnitSide.Player) for (let k = 0; k < cue.Amount; k++) sfx('pip', k * ms(EffectId.StaminaRecover));
          await holdBeat(EffectId.StaminaRecover,
            () => S.lightPips(cue.StaminaAfter, cue.StaminaMax, ms(EffectId.StaminaRecover)),
            () => S.setStamina(cue.StaminaAfter, cue.StaminaMax), cue.Amount);
        } else {
          S.setStamina(cue.StaminaAfter, cue.StaminaMax);
          effect(EffectId.StaminaChange, () => S.popPips(ms(EffectId.StaminaChange)));
        }
        break;

      case CueKind.StatusChange: {
        const beat = StatusBeat.Of(cue);
        if (beat === EffectId.StatusApply && cue.Fx === FxSystem.Spirit) sfxBudget('seal');
        await holdBeat(beat, () => S.playStatus(cue.Text, cue.StacksAfter, beat, ms(beat)), () => S.setStatus(cue.Text, cue.StacksAfter));
        break;
      }

      case CueKind.DrawHand:
        await drawHand(ev.After.Hand, cue.Amount, cue.RebuildAfter);
        break;

      case CueKind.OmenShow: {
        const badge = hud.omenOf(foe);
        const shownOmen = omenFrameOf(ev.After, foe);
        if (hud.syncOmenLook) hud.syncOmenLook(ev.After, foe != null ? foe : 0);
        badge.bind(shownOmen);
        sfx('omenDrop');
        // §5.8 提示: the floor's aimed cells are drawn left to right as the badge drops (30 ms a cell, scene addition).
        if (field.drawAim) effect(EffectId.OmenShow, () => field.drawAim(ev.After, foe), () => field.drawAim(ev.After, foe, true));
        await holdBeat(EffectId.OmenShow, () => badge.popIn(ms(EffectId.OmenShow)), () => badge.bind(shownOmen));
        break;
      }

      case CueKind.TraitFire: {
        if (cue.Fx === FxSystem.Form) sfxBudget('stance');
        const text = String(cue.Text || '');
        if (text.startsWith(STANCE_SET_PREFIX)) {
          // §5.11 順 1 / 順 8 (review 19): a stance set is told by where it goes — the player's card flies to the stance
          // list (hand.release), an enemy's omen badge flies to the enemy's list — and by its name entering the list; no
          // line over the head.
          await holdBeat(EffectId.TraitFire, () => {
            if (T === UnitSide.Enemy && hud.flyOmenToStances) safe(() => hud.flyOmenToStances(foe, L.STANCE_FLY_MS));
            return clock.wait(ms(EffectId.TraitFire));
          }, null);
          break;
        }
        const trait = traitOf(ev, cue);
        if (trait) {
          // §5.9 順 1 / 順 12 (review 19; Unity floats the line over the head): the player's card trait swelled the lamp
          // and left with the card (hand.release); an enemy's swells its condition word at the right end of its badge.
          // Either way the number it adds to carries the label 「+5 間合い」 (takeSide).
          pendingSide = { text: trait.side, kind: trait.kind, side: T, unit: tu };
          await holdBeat(EffectId.TraitFire, () => {
            if (T === UnitSide.Enemy && trait.condition) { const b = hud.omenOf(foe); if (b.traitWord) safe(() => b.traitWord(trait.condition, L.TRAIT_WORD_MS)); }
            return clock.wait(ms(EffectId.TraitFire));
          }, null);
          break;
        }
        await holdBeat(EffectId.TraitFire, () => fireTrait(T, labelAt, cue.Text, tu, rise), null);
        break;
      }

      case CueKind.Slash:
        await attack(ev, cue, chest, numberAt, under);
        break;

      case CueKind.GuardGain:
        sfxBudget('guardUp');
        await holdBeat(EffectId.GuardGain, () => {
          field.burst(chest, 'guard', 220 + 40 * cue.Intensity);
          field.floatText(numberAt, '+' + cue.Amount, 'guard', numberFont(cue.Intensity), Object.assign({ side: takeSide('guard') }, under));
          return S.popGuard(cue.GuardAfter, ms(EffectId.GuardGain));
        }, () => S.setGuard(cue.GuardAfter));
        break;

      case CueKind.RangeSwitch:
        // Moved by the other side (a shove): a knock-back, and the muted label says so.
        if (cue.Pushed) {
          const pushed = pushedFloat(cue, labelAt, under);
          floatEffect(EffectId.PushMark, pushed.at, pushed.word, 'ink2', 36, pushed.o);
        }
        if (EffectPlan.MovesOnFloor(cue)) {
          // §5.7 順 1〜2 (#586): the walk, 160 a cell, then the gap number counted again on arrival.
          const cells = EffectPlan.CellsOf(cue);
          await holdBeat(EffectId.FloorMove, () => field.moveOnFloor(cue, cells, switches.WaitMs(EffectId.FloorMove, cells)), () => field.snapOnFloor(cue), cells);
          const to = recountTo(ev, cue);
          await holdBeat(EffectId.GapRecount,
            () => field.recountGap(to.gap, cue.PlayerCellAfter, playerSize(), to.cell, ms(EffectId.GapRecount)),
            () => field.setGap(to.gap, cue.PlayerCellAfter, playerSize(), to.cell));
        } else {
          // No floor (the v4.2 sources): the core always writes one, so this is the plan's two beats held as waits.
          await holdBeat(EffectId.RangeSwitch, () => clock.wait(ms(EffectId.RangeSwitch)), null);
          await holdBeat(EffectId.RangeTagFlip, () => clock.wait(ms(EffectId.RangeTagFlip)), null);
        }
        break;

      case CueKind.StanceCue:
        // §5.13 順 1 (#501): pips 1-3 glow first (構え's 息), then the shield and "+3" (順 2).
        sfxBudget('breath');
        await holdBeat(EffectId.ReservePips, () => (T === UnitSide.Player ? hud.stamina.glowReserve(ms(EffectId.ReservePips)) : clock.wait(ms(EffectId.ReservePips))), null);
        sfxBudget('reserve');
        await holdBeat(EffectId.StanceCue, async () => {
          await hud.endTurn.pop(200);
          field.burst(chest, 'guard', 300);
          field.floatText(numberAt, cue.Text, 'guard', 48, under);
          await S.popGuard(cue.GuardAfter, 260);
        }, () => S.setGuard(cue.GuardAfter));
        // battle-visual-v1 §4.9 (review 19): the 構えの予告 has been granted — it goes now, not after the 敵の番 banner.
        if (T === UnitSide.Player) hud.reserveHint('');
        break;

      case CueKind.DiscardHand:
        await discardHand();
        break;

      case CueKind.EnemyWindup: {
        setHandInteractable(false);
        // The moment the enemy does what it foretold: the omen lights up and flares beside its move.
        const badge = hud.omenOf(foe);
        if (actingUnit != null && actingUnit !== foe) hud.omen.setActing(false);
        actingUnit = foe != null ? foe : 0;
        // §3.10 (review fix): an elite's or a boss's second action — its first omen is spent, so the plan drops into
        // the lower slot and turns solid (150 ms, beside the motion), and that badge is the one lit as acting.
        if (badge.promotePlan) safe(() => badge.promotePlan(switches.IsOn(EffectId.OmenShow) ? PLAN_DROP_MS : 0));
        badge.setActing(true);
        effect(EffectId.OmenExecute, () => badge.flare(ms(EffectId.OmenExecute)));
        await holdBeat(EffectId.EnemyMotion, () => field.enemyMotion(cue.System, ms(EffectId.EnemyMotion), foe), () => field.setPose(UnitSide.Enemy, FigurePose.Act, foe));
        break;
      }

      case CueKind.Defeat: {
        // #288 / #349: the fallen enemy's omen and plan go with it — and (review 17) its aimed cells on the floor, the
        // omen's other half, which stayed striped through the whole fall.
        if (T === UnitSide.Enemy) {
          const badge = hud.omenOf(foe);
          effect(EffectId.OmenSpend, () => badge.fadeOutAll(ms(EffectId.OmenSpend)), () => badge.hideAllNow());
          if (field.retireAimOf) field.retireAimOf(foe);
        }
        // battle_ui_ux_v2 §5.15 順 5 (scene addition; Unity draws one enemy, and its 800 holds the whole fall): one enemy
        // falls while others stand — 倒れ 800 → the foot light goes and the cell empties 300 → the panel turns grey 300.
        const othersStand = T === UnitSide.Enemy && multiNow() && enemyFrames(ev.After).some((uf, k) => !uf.Down && unitNo(uf, k) !== foe);
        if (!othersStand) {
          await holdBeat(EffectId.Defeat, () => field.fall(T, ms(EffectId.Defeat), tu), () => field.setDown(T, true, tu));
          break;
        }
        await holdBeat(EffectId.Defeat, () => field.fall(T, ms(EffectId.Defeat), tu, { keepFoot: true }), () => field.setDown(T, true, tu));
        if (!alive) return;
        if (switches.IsOn(EffectId.Defeat)) {
          await safe(() => (field.fadeFoot ? field.fadeFoot(T, L.FALL_FOOT_MS, tu) : field.setDown(T, true, tu)));
          if (!alive) return;
          await safe(() => (S.greyOut ? S.greyOut(L.FALL_GREY_MS) : S.setDown && S.setDown(true)));
        } else {
          field.setDown(T, true, tu);
          if (S.setDown) S.setDown(true);
        }
        break;
      }

      case CueKind.SideBonusMiss:
        await holdBeat(EffectId.SideBonusMiss, () => {
          const box = env.geom.omenBox ? env.geom.omenBox(foe) : null;
          const at = box ? { x: box.x + box.w / 2, y: box.y + box.h / 2 + 64 } : labelAt;
          field.floatText(at, cue.Text, 'whiff', 40, { struck: true, rise: -50, lowest: floatFloor });
          return hud.omenOf(foe).strikeSide(ms(EffectId.SideBonusMiss));
        }, () => hud.omenOf(foe).strikeSideNow());
        break;

      case CueKind.GuardBlock: {
        // The Guard's number is its own colour, and a light stands in for the sound (#76).
        S.setGuard(cue.GuardAfter);
        const next = ev.Cues[ev.Cues.indexOf(cue) + 1];
        const bodyFollows = next && next.Kind === CueKind.Hit && next.Target === T;
        if (!bodyFollows) sfxBudget(cue.GuardAfter === 0 ? 'guardBreak' : 'hitGuard', true);
        if (cue.GuardAfter === 0) effect(EffectId.GuardBreak, () => S.crackGuard(ms(EffectId.GuardBreak)));
        else safe(() => S.popGuard(cue.GuardAfter, 260));
        // §5.6 順 2〜3: "Guard −n" on the side the blow came from (over the shield), the HP number that follows beside it
        // on the figure's back, the pair centred on the chest on the numbers' own path; alone (Guard took it all), centred.
        const blowSide = T === UnitSide.Player ? 1 : -1;
        const half = HP_BESIDE_GUARD_GAP / 2;
        const tagAt = bodyFollows ? { x: numberAt.x + blowSide * half, y: numberAt.y } : numberAt;
        const anchor = bodyFollows ? (blowSide > 0 ? { anchor: 'left' } : { anchor: 'right' }) : {};
        guardTag = { side: T, unit: foe, away: -blowSide, span: null };
        const mark = guardTag;
        if (wall) wallBeat(T, wall, tu);
        // Guard took it all: the tag carries the label (the trait's, or 壁) — else the HP number beside it does.
        const tagSide = bodyFollows ? '' : wall ? WALL_SIDE : takeSide('hp', cue);
        floatEffect(EffectId.GuardNumber, tagAt, 'Guard −' + cue.Amount /* scene text (DepictionPlayer) */, 'guard', L.GUARD_NUMBER_SIZE,
          Object.assign({}, under, anchor, { side: tagSide, onPlaced: (span) => { mark.span = span; } }));
        await holdBeat(EffectId.GuardBlock, () => field.shieldBlock(T, String(cue.Amount), ms(EffectId.GuardBlock), tu, { broken: cue.GuardAfter === 0 }), null);
        break;
      }

      case CueKind.HpChange: {
        // 出血 / 再生 / a heal / a 見切り return (#188): the cause and the number, then the bar.
        // §5.0: the sign is the typographic minus (「出血 −2」), as every other number's.
        const signed = cue.Amount > 0 ? '+' + cue.Amount : cue.Amount < 0 ? MINUS + -cue.Amount : String(cue.Amount);
        floatEffect(EffectId.DamageNumber, numberAt, cue.Text + ' ' + signed, cue.Amount > 0 ? 'heal' : 'omen', 40, under);
        await drainHp(T, cue.HpAfter, tu);
        break;
      }

      case CueKind.Hit: {
        const strong = EffectStrength.IsStrong(cue.Intensity);
        if (T === UnitSide.Player) sfxBudget(wall ? 'hitBlunt' : 'hitBody', true);
        // After a Guard block on the same figure the number stands beside its "Guard −n" tag, on the figure's back.
        const blocked = ev.Cues.slice(0, ev.Cues.indexOf(cue)).some((c) => c.Kind === CueKind.GuardBlock && c.Target === T && unitOf(c) === foe);
        const tag = blocked && guardTag && guardTag.side === T && guardTag.unit === foe ? guardTag : null;
        if (wall && !blocked) wallBeat(T, wall, tu);
        // §5.0 (review 19): the number is the HP the blow took — on a killing blow the HP that was left, as the target
        // frame's tag and the HP wick promised (CardFace.HpLossFor) — with the trait's or the wall's side label.
        const shown = MINUS + hpLostOf(S, cue);
        const side = wall ? WALL_SIDE : takeSide('hp', cue);
        // §5.6 順 3 「順 2 と横に 24 px 並ぶ」: beside the "Guard −n" tag wherever it rose (over the shield or beside it).
        const besideTag = tag && tag.span ? hpBesideTag(tag, shown, numberFont(cue.Intensity), side) : null;
        if (besideTag) {
          // The pair reads side by side: the HP number rides at the tag's height as the tag rises (it starts later, so
          // with a rise of its own it trailed the tag by tens of px), and keeps 24 px off its edge.
          const followY = tag.span.yNow;
          floatEffect(EffectId.DamageNumber, { x: besideTag.at.x, y: followY ? followY() : besideTag.at.y }, shown, 'omen', numberFont(cue.Intensity),
            Object.assign({}, under, { side, anchor: besideTag.anchor, rise: followY ? 0 : tag.span.rise, followY, followRise: tag.span.rise }));
        } else floatEffect(EffectId.DamageNumber, numberAt, shown, 'omen', numberFont(cue.Intensity), Object.assign({ side }, under));
        // battle_ui_ux_v2 §5.5 順 3 「相手が白く光る」 (review 19: Unity's BattleTheme.Omen turned a guarded hit's figure a flat
        // violet, which read as a ghost of its own colour): every hit flashes white.
        effect(EffectId.HitFlash, () => field.flash(T, 'white', ms(EffectId.HitFlash), tu));
        effect(EffectId.TargetRecoil, () => field.recoil(T, strong, ms(EffectId.TargetRecoil), tu));
        if (strong) effect(EffectId.ScreenShake, () => field.shakeArena(ms(EffectId.ScreenShake)));
        await drainHp(T, cue.HpAfter, tu);
        // The player's blow Guard took part of, with a move right after it (#499): as in Attack.
        if (T === UnitSide.Enemy && EffectPlan.MoveFollows(ev, cue)) field.setPose(UnitSide.Player, FigurePose.Idle);
        if (T === UnitSide.Player) {
          // The blow that lands on the player is the enemy's own: its omen is spent and it draws back. Review 21 (§5.12: a
          // two-blow action stepped in and back twice and lost its badge between the blows): when the blow is the last cue
          // of the enemy's event, the two beats are held until the next event shows whether the action goes on (heldEnd).
          if (ev.Kind === DepictionEventKind.EnemyAction && ev.Cues[ev.Cues.length - 1] === cue) {
            takePlanned(EffectId.OmenSpend, 1);
            takePlanned(EffectId.EnemyReturn, 1);
            heldEnd = { unit: foe, title: carried && carried.unit === foe && carried.title ? carried.title : String(ev.Title || ''), side: null };
            carried = null;
          } else {
            const badge = hud.omenOf(foe);
            await holdBeat(EffectId.OmenSpend, () => badge.fadeOut(ms(EffectId.OmenSpend)), () => badge.hideNow());
            await holdBeat(EffectId.EnemyReturn, () => field.returnHome(UnitSide.Enemy, ms(EffectId.EnemyReturn), foe), () => field.snapHome(UnitSide.Enemy, foe));
          }
        }
        break;
      }

      default:
        break;
    }
  }

  /**
   * Where the HP number of a blow stands beside its "Guard −n" tag (§5.6 順 3): 24 px further out on the tag's row, on the
   * figure's back, or — with no room there on the stage — on the tag's other side. `font` sizes the estimate of its width
   * (the heart icon at 0.62 of it, the digits at 0.6 each, the tag's padding).
   */
  function hpBesideTag(tag, text, font, side) {
    const w = font * 0.62 + 6 + text.length * font * 0.6 + 20 + (side ? 6 + side.length * 16 : 0);
    const right = { at: { x: tag.span.x1 + HP_BESIDE_GUARD_GAP, y: tag.span.y }, anchor: 'left' };
    const left = { at: { x: tag.span.x0 - HP_BESIDE_GUARD_GAP, y: tag.span.y }, anchor: 'right' };
    const fitsRight = right.at.x + w <= L.W - STAGE_EDGE;
    const fitsLeft = left.at.x - w >= STAGE_EDGE;
    if (tag.away > 0) return fitsRight || !fitsLeft ? right : left;
    return fitsLeft || !fitsRight ? left : right;
  }

  /**
   * §5.7 順 2's recount counts to the enemy the floor's N is read to (review 19: after a Tab at rest picked the back enemy,
   * a move recounted to the front enemy's N — the writer's GapAfter is the nearest's — and jumped to the chosen one's as the
   * event's frame landed): the Tab's enemy's gap and cell from the event's frame, when this is the event's last move; else
   * the cue's own.
   */
  function recountTo(ev, cue) {
    const own = { gap: cue.GapAfter, cell: cue.EnemyCellAfter };
    if (restingGapUnit == null || !multiNow() || !ev || !ev.After) return own;
    const i = ev.Cues.indexOf(cue);
    if (ev.Cues.slice(i + 1).some((c) => c.Kind === CueKind.RangeSwitch && EffectPlan.MovesOnFloor(c))) return own;
    const uf = enemyFrames(ev.After).find((x, k) => unitNo(x, k) === restingGapUnit);
    return uf && !uf.Down && uf.Gap >= 0 && uf.Cell > 0 ? { gap: uf.Gap, cell: uf.Cell } : own;
  }

  /**
   * 押し出し stands on the side that is moved and rides with it (§5.7 順 6): the RangeSwitch cue's Target is the
   * player, but a push the player gave moves an enemy (Cue.Unit with its UnitCellAfter): then it is that enemy's.
   */
  function pushedFloat(cue, labelAt, under) {
    const enemyMoved = cue.Unit != null && cue.Unit >= 0 && cue.UnitCellAfter > 0;
    const u = multiNow() && cue.Unit != null && cue.Unit >= 0 ? cue.Unit : undefined;
    const hero = env.geom.figure(UnitSide.Player);
    const foe = env.geom.figure(UnitSide.Enemy, u);
    if (!enemyMoved) return { at: labelAt, o: under, word: movedWord(hero, cue.PlayerCellAfter, foe) };
    const at = foe ? { x: foe.chest.x, y: foe.chest.y } : labelAt;
    return { at, word: movedWord(foe, cue.UnitCellAfter, hero), o: Object.assign({}, under, { track: trackOf(UnitSide.Enemy, u), rise: foe ? Math.max(0, foe.chest.y - foe.head.y) : under.rise }) };
  }
  /**
   * §5.7 順 6 (review 21: a pull read 「押し出し」): 「引き寄せ」 when the figure moved (`moved`, to `cell`) ends nearer the one
   * who moved it (`by`), else 「押し出し」 (DepictionText.Pushed). The cue carries no way; the cells say it.
   */
  function movedWord(moved, cell, by) {
    const box = cell > 0 && env.geom.cellBox ? env.geom.cellBox(cell) : null;
    if (!moved || !by || !box) return DepictionText.Pushed;
    const to = box.x + box.w / 2;
    return Math.abs(to - by.homeX) < Math.abs(moved.homeX - by.homeX) ? PULLED_WORD : DepictionText.Pushed;
  }

  async function fireTrait(side, labelAt, text, trackUnit, rise) {
    // Warm belongs to the player's side only (README 色): an enemy's fired trait is written in ink. A move the core
    // stopped (§5.7 順 9 / 11) is no trait: its line rises grey, with no light behind it.
    const blocked = MOVE_BLOCKED_LINES.has(text);
    const role = blocked ? 'ink2' : side === UnitSide.Player ? 'warm' : 'ink';
    if (!blocked) field.burst(labelAt, role, 260);
    // Plain text (no icon, never read as a number): a stance's 構え停止・ / 構え復帰・ line, 呪縛で動けない. On the numbers'
    // own path (§5.0: chest → head).
    field.floatText(labelAt, text, role, 36, { rise: rise != null ? rise : 43, plain: true, avoid: badgeBoxes, avoidGap: FLOAT_UNDER_BADGE, lowest: floatFloor, track: trackOf(side, trackUnit) });
    await clock.wait(ms(EffectId.TraitFire));
  }

  /**
   * A TraitFire cue that is a fired trait of the card played or of the enemy's action (not a stance line, not a stopped
   * move): its condition word and the side label its number carries — 「+5 間合い」 (§5.0: the effect, then the condition,
   * as CoreText.TraitLine's two halves) — and which number takes it: a Guard bonus the next Guard gain, any other the
   * next HP number (a trait with no number of its own rises alone at the event's end, flushSide).
   */
  function traitOf(ev, cue) {
    const text = String(cue.Text || '');
    if (!text || MOVE_BLOCKED_LINES.has(text) || STANCE_LINE.test(text)) return null;
    const own = cue.Target === UnitSide.Player ? ev.Kind === DepictionEventKind.PlayCard : ev.Kind === DepictionEventKind.EnemyAction;
    if (!own) return null;
    const at = text.lastIndexOf(' ');
    const conditionWord = at > 0 ? text.slice(0, at) : '';
    const effectWord = at > 0 ? text.slice(at + 1) : text;
    // §5.9 順 1 (review 21): the condition by its one-word name (「間合い」「温存」), not the lamp line's 「間合い0」「残4」.
    const condition = conditionWord ? conditionName(conditionWord) : '';
    const kind = /^Guard\+/.test(effectWord) ? 'guard' : /^\+\d|^重撃|^転換|^追撃|^崩し/.test(effectWord) ? 'hp' : 'none';
    // On a Guard number the effect's own word is the number's kind already: 「+3 温存」, not 「Guard+3 温存」.
    const shownEffect = kind === 'guard' ? effectWord.replace(/^Guard/, '') : effectWord;
    // §5.0 「−19 +6 重撃」: a named effect is its own label (with 重撃's +6), not effect + condition.
    const named = NAMED_EFFECTS[effectWord];
    const side = named ? named() : condition ? shownEffect + ' ' + condition : shownEffect;
    return { condition, effect: effectWord, side, kind };
  }

  /** §5.0 (review 19): the HP a blow took: its Amount, but never more than the HP its target's panel held (a killing blow). */
  function hpLostOf(S, cue) {
    const before = S ? S.hp : null;
    if (cue.HpAfter === UNCHANGED || before == null) return cue.Amount;
    const lost = before - cue.HpAfter;
    return lost > 0 && lost < cue.Amount ? lost : cue.Amount;
  }

  /** §5.7 順 7 (review 19): the wall's beat beside the flow — the edge it hit lights 80 ms, the figure takes 打's ripple. */
  function wallBeat(T, wall, tu) {
    if (field.wallHit) effect(EffectId.HitFlash, () => field.wallHit(T, wall.dir, WALL_EDGE_MS, tu));
  }

  // ---- beats ------------------------------------------------------------------------------

  async function attack(ev, cue, chest, numberAt, under) {
    if (cue.Source == null) console.error('[battle] event ' + ev.Order + ': Slash cue has no Source');
    const source = EffectPlan.AttackerOf(cue);
    const byPlayer = source === UnitSide.Player;
    const strong = EffectStrength.IsStrong(cue.Intensity);
    const T = cue.Target;
    // Several enemies: the enemy of the exchange is the striker or the one struck (Cue.Unit).
    const foe = unitOf(cue);
    const su = byPlayer ? undefined : foe;
    const tu = T === UnitSide.Enemy ? foe : undefined;
    // #288: both sides step in before the blow.
    const lunge = byPlayer ? EffectId.AttackLunge : EffectId.EnemyLunge;
    // §5.12 順 1 「踏み込みと止めは 1 回」 (review 21): the second blow of a held action strikes from where the first one stood.
    if (!byPlayer && carried && (carried.unit == null || carried.unit === foe) && field.enemyAway(su)) {
      takePlanned(lunge, 1);
      field.setPose(source, FigurePose.Act, su);
    } else await holdBeat(lunge, () => field.lunge(source, L.LUNGE_PX, ms(lunge), su), () => field.setPose(source, FigurePose.Act, su));
    // §5.9 J2 (#57): a fired trait lays its short cue over the blow.
    if (cue.TraitAccent) effect(EffectId.TraitAccent, () => field.traitAccent(T, ms(EffectId.TraitAccent), tu));
    // The action sound of an enemy blow is its system's hit; a player's card already sounded on release.
    if (!byPlayer) sfxBudget(HIT_SOUND[cue.System], true);
    await holdBeat(EffectId.StrikeShape, () => field.strike(T, { byPlayer, system: cue.System, intensity: cue.Intensity, ms: ms(EffectId.StrikeShape), unit: tu, source: su }), null);
    const stop = strong ? EffectId.HitStopStrong : EffectId.HitStop;
    await holdBeat(stop, () => clock.wait(ms(stop)), null);
    if (strong) effect(EffectId.ScreenShake, () => field.shakeArena(ms(EffectId.ScreenShake)));

    // A slash with no settled HP is the swing alone: the GuardBlock / Hit cues that follow carry the numbers.
    if (cue.HpAfter === UNCHANGED) {
      if (byPlayer) sfxBudget(HIT_SOUND[cue.System], true);
      return;
    }
    if (byPlayer) sfxBudget(HIT_SOUND[cue.System], true);
    field.burst(chest, 'omen', 200 + 60 * cue.Intensity);
    // §5.0 (review 19): the HP the blow took (a killing blow's is the HP that was left: the tag's and the wick's number),
    // with a fired trait's side label 「+5 間合い」.
    floatEffect(EffectId.DamageNumber, numberAt, MINUS + hpLostOf(hud.status(T, tu), cue), 'ink', numberFont(cue.Intensity), Object.assign({ side: takeSide('hp', cue) }, under));
    effect(EffectId.HitFlash, () => field.flash(T, 'white', ms(EffectId.HitFlash), tu));
    effect(EffectId.TargetRecoil, () => field.recoil(T, strong, ms(EffectId.TargetRecoil), tu));
    await drainHp(T, cue.HpAfter, tu);
    // §5.4 順 2 / §5.5 順 8 (#499): a move right after the blow carries the player on from the lunge.
    if (byPlayer && !EffectPlan.MoveFollows(ev, cue)) {
      await holdBeat(EffectId.AttackReturn, () => field.returnHome(source, ms(EffectId.AttackReturn)), () => field.snapHome(source));
    } else if (byPlayer) field.setPose(source, FigurePose.Idle);
  }

  /** The bar drops (blocking), then the grey band follows it down beside the flow. */
  async function drainHp(side, hp, unit) {
    const S = hud.status(side, unit);
    if (side === UnitSide.Player && hp < S.hp) pulseLowHp(hp);
    await holdBeat(EffectId.HpDrain, () => S.animateHp(hp, ms(EffectId.HpDrain)), () => S.setHpKeepingTrail(hp), 1,
      () => effect(EffectId.HpTrail, () => S.animateTrail(ms(EffectId.HpTrail)), () => S.snapTrail()));
  }

  function playerSize() {
    const f = frameNow();
    return f && f.Floor ? Math.max(1, f.Floor.PlayerSize) : 1;
  }

  /** §3.14 山札切れ (#455): the discard pile slides onto the draw pile in the middle of the draw. */
  function slideDiscardOntoDraw() {
    sfx('rebuild');
    return field.ghost(env.geom.pileMiddle(PileKind.Discard), env.geom.pileMiddle(PileKind.Draw), { kind: 'sheet', ms: ms(EffectId.PileRebuild), ease: 'out' });
  }

  /** DrawHand: rebuild the hand from the settled faces and fly the newest ones in from the draw pile. */
  async function drawHand(faces, drawn, rebuildAfter) {
    let rebuild = drawn > 0 && rebuildAfter >= 0;
    if (rebuild && !(switches.IsOn(EffectId.PileRebuild) && switches.IsOn(EffectId.CardDraw))) {
      await holdBeat(EffectId.PileRebuild, () => slideDiscardOntoDraw(), null);
      rebuild = false;
    }
    hand.build(faces || []);
    const count = Math.max(0, Math.min(drawn, hand.count()));
    if (count === 0) return;
    const from = env.geom.pileMiddle(PileKind.Draw);
    if (!rebuild) {
      await holdBeat(EffectId.CardDraw, () => hand.flyDrawn(hand.count() - count, UNCHANGED, { from }), null, count);
      return;
    }
    const flying = safe(() => hand.flyDrawn(hand.count() - count, rebuildAfter, { from, onRebuild: () => slideDiscardOntoDraw() }));
    await holdBeat(EffectId.PileRebuild, () => Promise.resolve(), null);
    await holdBeat(EffectId.CardDraw, () => flying, null, count);
  }

  /** Turn end: the whole hand flies to the discard pile one stagger apart. */
  async function discardHand() {
    const count = hand.count();
    if (count > 0) await holdBeat(EffectId.HandDiscard, () => hand.discardAll(env.geom.pileMiddle(PileKind.Discard)), null, count);
    hand.clear();
  }

  /** The played card's ghost flies to the pile the core put it in (DepictionEvent.PlayedTo), beside the beats. */
  async function ghostToPile(from, pile, face, onStart) {
    const exiled = pile === PileKind.Exile;
    if (exiled) await clock.wait(Math.max(0, EffectCatalog.CardToExileDelayMs - ms(EffectId.CardRelease)));
    const at = typeof from === 'function' ? from() : from;
    if (onStart) onStart();
    const id = exiled ? EffectId.CardToExile : EffectId.CardToDiscard;
    // The card itself, shrunk (review fix; Unity: a plain sheet): the hand builds its face, the field flies it.
    const card = hand.ghostCard && face ? hand.ghostCard(face) : null;
    await effect(id, () => field.ghost(at, env.geom.pileMiddle(exiled ? PileKind.Exile : PileKind.Discard),
      { kind: exiled ? 'exile' : 'discard', ms: ms(id), ease: exiled ? 'in' : 'inout', card }));
  }

  // ---- the player's moves -------------------------------------------------------------------

  function receiverCentre(unit) {
    const r = env.geom.receiver(unit) || env.geom.receiver();
    return { x: r.X + r.Width / 2, y: r.Y + r.Height / 2 };
  }

  /**
   * battle_ui_ux_v2 §3.14 「端や相手に塞がれた移動」 / §5.7 順 5 (review 17; scene addition — the core writes no cue for a
   * move that went nowhere, Unity shows nothing): the way a played card meant to move the player (its face's 詰める →
   * +1 / 離れる ← −1 value; 0 for none), when its event moved no one on the floor and no 呪縛 / 鉤爪 line says why.
   */
  function stuckMoveOf(face, ev) {
    if (!face || !ev) return 0;
    const vals = face.Values || [];
    const dir = vals.some((v) => v.Kind === CardValueKind.Close) ? 1 : vals.some((v) => v.Kind === CardValueKind.Back) ? -1 : 0;
    if (!dir) return 0;
    const moved = ev.Cues.some((c) => (c.Kind === CueKind.RangeSwitch && EffectPlan.MovesOnFloor(c))
      || (c.Kind === CueKind.TraitFire && MOVE_BLOCKED_LINES.has(c.Text)));
    return moved ? 0 : dir;
  }

  /**
   * battle_ui_ux_v2 §5.7 順 5 (review 19; the core writes no mark for it): the player's own move cue that stopped short of
   * what the card asked — its 詰める / 離れる value (CardFace.Values) — against the enemy (N 0 after a move forward) or the
   * end of the line (cell 1 after a move back). The last such cue of the event, with its way; null for none. Under 鈍足 a
   * move shortened to land right against the enemy leans too (the script does not tell the two apart).
   */
  function cutShortOf(face, ev) {
    if (!face || !ev) return null;
    const v = (face.Values || []).find((x) => x.Kind === CardValueKind.Close || x.Kind === CardValueKind.Back);
    const want = v ? parseInt(v.Number, 10) : 0;
    if (!(want > 0)) return null;
    const dir = v.Kind === CardValueKind.Close ? 1 : -1;
    const own = ev.Cues.filter((c) => c.Kind === CueKind.RangeSwitch && !c.Pushed && !(c.Unit != null && c.Unit >= 0 && c.UnitCellAfter > 0) && EffectPlan.MovesOnFloor(c));
    if (!own.length) return null;
    const moved = own.reduce((n, c) => n + EffectPlan.CellsOf(c), 0);
    const last = own[own.length - 1];
    const against = dir > 0 ? last.GapAfter === 0 : last.PlayerCellAfter <= 1;
    return moved < want && against ? { cue: last, dir } : null;
  }

  async function playAccepted(cardId, ev, face) {
    busy = true;
    setState('resolving');
    setHandInteractable(false);
    playedThisTurn += 1;
    eventFace = face || null;
    // The result screen's count (review 21, §2.5): the cards played by attribute and the traits they fired.
    if (face) tally.played[face.Kind] = (tally.played[face.Kind] || 0) + 1;
    tally.traits += ev.Cues.filter((c) => c.Kind === CueKind.TraitFire && c.Target === UnitSide.Player && traitOf(ev, c)).length;
    beginPlan(ev);
    budget = 1; // the release sound (the hand's) is the action
    essentialLeft = Math.min(budget, essentialLeft);
    const to = ev.Aim === CardAim.Single ? receiverCentre(unitOf(ev)) : null;
    // §5.9 順 1 (review 19): a trait the card fires swells its lamp and its condition word leaves with the card.
    const traitCue = ev.Cues.find((c) => c.Kind === CueKind.TraitFire && c.Target === UnitSide.Player && traitOf(ev, c));
    const trait = traitCue ? traitOf(ev, traitCue) : null;
    let rel = null;
    await holdBeat(EffectId.CardRelease, () => {
      rel = hand.release(cardId, ev, to, { traitWord: trait ? trait.condition || trait.effect : '' });
      return rel ? rel.done : undefined;
    }, null, 1, () => released(cardId, ev, rel));
    // The move that went nowhere leans its 40 ms where the move would have played: after the stamina is paid.
    let stuck = stuckMoveOf(face, ev);
    const lean = () => {
      const dir = stuck;
      stuck = 0;
      if (dir && field.bump) effect(EffectId.FloorMove, () => field.bump(UnitSide.Player, dir, L.MOVE_BUMP_MS));
    };
    // §5.7 順 5: a move that went part of the way leans where it stopped, on its arrival.
    const cut = cutShortOf(face, ev);
    for (const cue of ev.Cues) {
      if (!alive) return;
      if (stuck && cue.Kind !== CueKind.StaminaChange) lean();
      await playCue(ev, cue);
      if (cut && cue === cut.cue && field.bump) effect(EffectId.FloorMove, () => field.bump(UnitSide.Player, cut.dir, L.MOVE_BUMP_MS));
    }
    if (!alive) return;
    if (stuck) lean();
    field.setPose(UnitSide.Player, FigurePose.Idle);
    flushSide();
    eventFace = null;
    endPlan();
    applyFrame(ev.After);
    await afterWait(EffectFlow.AfterPlayerEventMs);
    if (!alive) return;
    eventEnded();
    await runAutomaticEvents();
  }

  /**
   * The released card is gone: a ghost flies to the pile the core put it in and the hand closes up. A stance card (§5.11
   * 順 1 / 順 4, review 19) stands shrunk at the end of the stance list until its ghost leaves from there for the exile.
   */
  function released(cardId, ev, rel) {
    const from = ev.Aim === CardAim.Single ? receiverCentre(unitOf(ev)) : rel && rel.vanishedAt ? rel.vanishedAt() : receiverCentre();
    if (rel && rel.keep) ghostToPile(() => rel.vanishedAt(), ev.PlayedTo, rel.face, () => hand.dropCard(cardId));
    else {
      hand.dropCard(cardId);
      ghostToPile(from, ev.PlayedTo, rel && rel.face);
    }
    effect(EffectId.HandFan, () => hand.settle(ms(EffectId.HandFan)), () => hand.layout());
  }

  async function returnToHand(cardId, shake) {
    busy = true;
    returningCard = true;
    setState('resolving');
    await safe(() => hand.returnToHand(cardId, shake));
    returningCard = false;
    if (!alive) return;
    busy = false;
    setState('idle');
    refreshPlayable();
    // An end turn asked for while the card slid home (X or the plate) is taken now.
    if (endTurnQueued) { endTurnQueued = false; if (requestEndTurn()) return; }
    scheduleAuto();
  }

  /** The enemy a release names, as the source takes it: only in a battle of several enemies (Unity passes none). */
  function targetFor(unit) {
    return unit != null && unit >= 0 && multiNow() ? unit : undefined;
  }

  /** Release: the card was let go on `zone` (over enemy `unit` when the hand names one). The source decides; the hand shows. */
  function onRelease(cardId, zone, unit) {
    if (!alive) return;
    if (state !== 'idle' || busy) {
      safe(() => hand.returnToHand(cardId, false));
      return;
    }
    const target = targetFor(unit);
    const face = DepictionText.Find(frameNow().Hand || [], cardId);
    let r;
    try { r = target === undefined ? ctx.source.TryPlay(cardId, zone) : ctx.source.TryPlay(cardId, zone, target); } catch (err) { sourceFailed(err); return; }
    const verdict = r && (r.verdict != null ? r.verdict : r.ok);
    const ev = r && (r.played != null ? r.played : r.value);
    if (verdict === PlayVerdict.Accepted && ev) {
      acceptedPlays += 1;
      playAccepted(cardId, ev, face);
      return;
    }
    showRefusal(cardId, verdict, target, face);
    // A card released on a zone it belongs to was refused by a rule: shake it. One released on the wrong zone just slides home.
    returnToHand(cardId, verdict !== PlayVerdict.WrongZone && zone !== DropZone.None);
  }

  function showRefusal(cardId, verdict, target, face) {
    let text = target === undefined ? ctx.source.RefusalText(cardId, verdict) : ctx.source.RefusalText(cardId, verdict, target);
    if (!text) return;
    // The receiver the ported line names is not drawn (wrongZoneOnEnemy): the same line, with 敵の上.
    if (verdict === PlayVerdict.WrongZone && face && face.Aim === CardAim.Single && text === DepictionText.WrongZoneText(face)) text = wrongZoneOnEnemy(face.Name);
    hand.showRefusal(text);
    refusalCause = null;
  }

  /**
   * RefreshPlayableLook: the dark sheet on the cards, and the guide line. Review fix (battle-visual-v1 §5.1 / §6.2,
   * battle_ui_ux_v2 §3.5; Unity dims on any refusal and lights the whole hand while busy): while the player waits,
   * a card is dark when the source would refuse it for what it costs or for 呪縛 — not for reach, which the hover's
   * panel, the steel hooks and the unlit floor say (「カードに黒い板は重ねません（板はスタミナが足りないときだけです）」);
   * while the flow runs (the enemy's turn, a play resolving, the turn start's draw) the whole hand is dark (§6.2 敵の番,
   * §3.5 「敵フェーズ中・演出中 → 手札全体を暗くし」). The verdict is the source's; the scene only reads which one.
   */
  function refreshPlayable() {
    const s = src();
    // RefreshPlayableLook: `waiting` needs a card in hand (`_hand.Exists(c => c && c.Interactable)`): an
    // empty hand prints nothing, as in Unity (review fix).
    const waiting = state === 'idle' && !busy && s.WaitingForPlayer && hand.count() > 0;
    // §3.8: while 俊敏's choice waits, the guide says how to choose (no card is taken until then). Review 16: while the
    // pile list is open the cards are not taken (§3.5), and the guide says so instead of asking for a release.
    const asked = state === 'idle' && !busy && s.WaitingForPlayer;
    // Review 17: the journal first (nothing, not even 俊敏's choice, is taken behind it); then 俊敏's choice, which the
    // pile list does not stop (its marks come out from under the drawer, and the choice is the one thing to make); then
    // the pile list.
    // Review 21 (the pile list said 「close it to play」 three times at once): with the list open the guide line is quiet — its
    // close button and 「閉じるとプレイできます」 beside it say it (§3.5), and a card pressed meanwhile gets DRAWER_GUIDE on the
    // refusal line.
    const guide = asked && journalOpen() ? JOURNAL_GUIDE
      : freeStep && state === 'idle' ? FREE_STEP_GUIDE
        : asked && drawerOpen() ? ''
          : waiting || s.Finished ? s.GuideText : '';
    hand.refreshPlayable((id) => {
      if (!waiting) return true;
      const verdict = s.Inspect(id);
      return verdict !== PlayVerdict.Accepted && verdict !== PlayVerdict.OutOfRange;
    }, guide || '');
    flashIfNothingPlayable(asked);
  }

  /**
   * battle_ui_ux_v2 §3.5 「出せる札が 1 枚も無い: 案内の行に「出せる札がありません」。ターン終了ボタンが 1 回だけ明るくなる」
   * (review 16; the guide is the source's GuideText): once per wait in which the source would take no card of the
   * hand (Inspect, read only), the end-turn plate brightens once.
   */
  let nothingKey = '';
  function flashIfNothingPlayable(asked) {
    if (!asked || freeStep || !hud.endTurn || !hud.endTurn.flash) return;
    let none = true;
    const faces = frameNow().Hand || [];
    try { none = !faces.some((f) => ctx.source.Inspect(f.Id) === PlayVerdict.Accepted); } catch (err) { none = false; }
    const key = none ? (frameNow().Corner ? frameNow().Corner.Turn : 0) + ':' + faces.map((f) => f.Id).join(',') : '';
    if (key === nothingKey) return;
    nothingKey = key;
    if (none) safe(() => hud.endTurn.flash(L.END_TURN_FLASH_MS));
  }

  // ---- 俊敏's free cell (battle_ui_ux_v2 §3.8 / §5.1 順 7 / §5.7 順 10) --------------------------------------
  // Scene addition with the source's JS extension (Unity lets the step go): after the turn start's draw the arrows
  // stand over the open ways and 留まる on the player's cell; no card is taken until one is chosen (a click, or
  // ← / → and Enter). Staying asks nothing of the source (the first card or the turn's end lets the step go);
  // a way is ctx.source.TakeFreeStep(dir) and its event plays as any move of the player's: the walk, the recount.

  /** At the turn's first wait: asks the source once whether the free cell is open, and puts the choice up. */
  function openFreeStep() {
    const s = ctx.source;
    if (halted || typeof s.CanTakeFreeStep !== 'function') return;
    const f = frameNow();
    const turn = f && f.Corner ? f.Corner.Turn : -1;
    if (turn === freeStepTurn) return;
    freeStepTurn = turn;
    let ways;
    try {
      if (!s.CanTakeFreeStep()) return;
      ways = { [-1]: !!s.CanTakeFreeStep(-1), [1]: !!s.CanTakeFreeStep(1) };
    } catch (err) { console.error(err); return; }
    freeStep = { ways };
    hand.setGrabLock(refuseWhileChoosing);
    field.showFreeStep(f.Floor ? f.Floor.PlayerCell : 0, ways);
  }

  /** The choice is over (chosen, the turn ended, the battle stopped): the marks go and the cards can be taken. */
  function closeFreeStep(ms) {
    if (!freeStep) return;
    freeStep = null;
    hand.setGrabLock(null);
    field.hideFreeStep(ms);
    clearRefusal('freestep');
  }

  /** A card was tried while the choice waits (§3.8 選ぶまで手札は掴めません): the reason on the refusal line. */
  function refuseWhileChoosing() {
    sfx('refusal');
    showCauseRefusal('freestep', FREE_STEP_REFUSAL);
  }
  /**
   * Review 21 (a refusal line stood 3 s after its cause was gone, telling the player to close a list already closed): the
   * lines that name something to close or to choose remember it, and go when it goes (clearRefusal).
   */
  let refusalCause = null;
  function showCauseRefusal(cause, text) {
    hand.showRefusal(text);
    refusalCause = cause;
  }
  function clearRefusal(cause) {
    if (refusalCause !== cause) return;
    refusalCause = null;
    if (hand.hideRefusal) hand.hideRefusal();
  }

  /** The choice: −1 back, +1 forward (a way the source said is open), 0 stay. Returns whether it was taken. */
  function pickFreeStep(dir) {
    if (!freeStep || !alive || halted || state !== 'idle' || busy || overlayOpen() || hand.isHolding()) return false;
    if (dir !== 0 && !freeStep.ways[dir]) return false;
    sfx('uiClick');
    closeFreeStep();
    if (dir === 0) {
      refreshPlayable();
      scheduleAuto();
      return true;
    }
    let ev;
    try { ev = ctx.source.TakeFreeStep(dir); } catch (err) { sourceFailed(err); return false; }
    playFreeStep(ev);
    return true;
  }

  /** The step's event: the walk of one cell and the recount (§5.7 順 10 → 順 1〜2), or what stopped it (順 9 / 11). */
  async function playFreeStep(ev) {
    busy = true;
    setState('resolving');
    setHandInteractable(false);
    beginPlan(ev);
    for (const cue of ev.Cues) {
      if (!alive) return;
      await playCue(ev, cue);
    }
    if (!alive) return;
    if (ev.Cues.length === 0) {
      // Nothing moved and nothing says why (鈍足 took the cell to 0, or the line): §5.7 順 9's grey word.
      const g = env.geom.figure(UnitSide.Player);
      if (g) floatEffect(EffectId.PushMark, { x: g.chest.x, y: g.chest.y }, FREE_STEP_STUCK, 'ink2', 36, Object.assign({ rise: Math.max(0, g.chest.y - g.head.y) }, UNDER_BADGES));
    }
    field.setPose(UnitSide.Player, FigurePose.Idle);
    flushSide();
    endPlan();
    applyFrame(ev.After);
    await afterWait(EffectFlow.AfterPlayerEventMs);
    if (!alive) return;
    busy = false;
    eventEnded();
    await runAutomaticEvents();
  }

  /** The pointer is over mark `dir`: the focus follows it. */
  function focusFreeStep(dir) {
    if (!freeStep) return;
    const before = field.freeStepFocus;
    if (field.focusFreeStep(dir) !== before) sfx('hover');
  }

  /**
   * battle_ui_ux_v2 §3.4 / §3.8 「← / → で 1 マス後ろ / 前を選びます」: ← picks back and → picks forward, whatever had the focus
   * (review 19: the keys walked a focus along 後ろへ — 留まる — 前へ). Review 21 (a repeat: after the pointer focused 前へ, →
   * moved the focus to 留まる and Enter stayed): the key of a way always picks that way — pressed again it keeps it. Only the
   * key of a way that is not open falls to 留まる; Esc puts the focus on 留まる (stayFreeStepFocus). Enter chooses.
   */
  function moveFreeStepFocus(key) {
    if (!freeStep || state !== 'idle' || busy) return false;
    const dirs = field.freeStepDirs();
    const now = field.freeStepFocus;
    const want = key < 0 ? -1 : 1;
    const next = dirs.indexOf(want) >= 0 ? want : dirs.indexOf(0) >= 0 ? 0 : now;
    if (next == null) return false;
    focusFreeStep(next);
    return true;
  }
  /** Esc while 俊敏's choice waits: the focus goes to 留まる (the keys' way back from an arrow). Returns whether it moved. */
  function stayFreeStepFocus() {
    if (!freeStep || state !== 'idle' || busy) return false;
    if (field.freeStepFocus === 0 || field.freeStepDirs().indexOf(0) < 0) return false;
    focusFreeStep(0);
    return true;
  }

  function canEndTurn() {
    return alive && !halted && state === 'idle' && !busy && !hand.isHolding() && !overlayOpen() && src().CanEndTurn;
  }

  /**
   * The end-turn plate, X. `queue`: asked for while a refused card is still sliding home, it is taken as
   * soon as the card is back (the press is not lost to the 360 ms return).
   */
  function requestEndTurn(o) {
    if (o && o.queue && returningCard && alive && !halted && !overlayOpen()) { endTurnQueued = true; return true; }
    // A press of the plate or X (o.press): while the flow runs it only marks the mash window (the scene counts a
    // click on the plate as a fast-forward); while the window is open a press is swallowed and slides it on.
    if (o && o.press) {
      const now = real.now;
      const flowing = state === 'auto' || state === 'resolving' || state === 'opening';
      // Review 17 (presses every 0.65–0.8 s slipped between the 600 ms window and the turn's 500 ms guard, and the next
      // turn ended 0.6–1.1 s after it opened): the window a press opens follows the rhythm of the presses before it —
      // L.END_TURN_MASH_RHYTHM × the last interval, at least L.END_TURN_MASH_MS, at most L.END_TURN_MASH_MAX_MS.
      const gap = now - lastPressAt;
      lastPressAt = now;
      const rhythm = gap <= L.END_TURN_MASH_MAX_MS ? gap : 0;
      const win = Math.min(L.END_TURN_MASH_MAX_MS, Math.max(L.END_TURN_MASH_MS, rhythm * L.END_TURN_MASH_RHYTHM));
      // A press while the flow runs (hurrying the enemy's turn or a play) is swallowed and opens the window.
      if (flowing) { mashUntil = now + win; return false; }
      // Review 21 (presses every 0.7–1.4 s after the turn opened slid the window on for good, and the turn could not be
      // ended): the window absorbs only the mash carried over from the flow. A press in the player's own wait may slide it
      // on, but never past L.END_TURN_MASH_MAX_MS after the player was asked — from then on every press counts — and a
      // swallowed press shows it was seen (the plate shakes once).
      const sinceAsked = now - askedAt;
      const guarded = now - turnOpenedAt < L.END_TURN_OPEN_GUARD_MS;
      if (guarded || (now < mashUntil && sinceAsked < L.END_TURN_MASH_MAX_MS)) {
        mashUntil = Math.min(askedAt + L.END_TURN_MASH_MAX_MS, Math.max(mashUntil, now + win));
        if (hud.endTurn && hud.endTurn.refuse) safe(() => hud.endTurn.refuse(L.END_TURN_REFUSE_MS));
        return false;
      }
    }
    if (!canEndTurn()) return false;
    endTurnQueued = false;
    // The turn's end lets 俊敏's cell go (battle_core_v4.md §5): the choice goes with it.
    closeFreeStep(0);
    // The floor's N goes back to the nearest enemy for the enemy's turn (a Tab choice was the player's turn's).
    if (restingGapUnit != null) { restingGapUnit = null; showHpLoss(); }
    if (hand.cancel) hand.cancel();
    let ev;
    try { ev = ctx.source.EndTurn(); } catch (err) { sourceFailed(err); return false; }
    playThenContinue(ev);
    return true;
  }

  async function playThenContinue(ev) {
    busy = true;
    setState('resolving');
    setHandInteractable(false);
    await playEvent(ev);
    if (!alive) return;
    busy = false;
    await afterWait(EffectFlow.AfterPlayerEventMs);
    if (!alive) return;
    eventEnded();
    await runAutomaticEvents();
  }

  /**
   * The source threw (a bridge or core bug: the C# CoreScriptWriter, for one, throws KeyNotFoundException
   * in a two-enemy fight). The battle cannot go on: the scene stops taking input and pulling events, logs
   * the error, and hands it to ctx.onError when the app gave one.
   */
  function sourceFailed(err) {
    console.error('[battle] the source failed; the battle stops here:', err);
    halted = true;
    busy = false;
    closeFreeStep(0);
    setHandInteractable(false);
    setState('halted');
    try { if (ctx.onError) ctx.onError(err); } catch (e) { console.error(e); }
  }

  // ---- the end -------------------------------------------------------------------------------

  /** `ms` of real time that ran while no overlay held the scene (the result hold waits behind the journal). */
  async function holdUnpaused(ms) {
    let left = ms;
    let last = real.now;
    while (left > 0) {
      await real.wait(Math.min(left, 50));
      const now = real.now;
      if (!overlayOpen()) left -= now - last;
      last = now;
    }
  }

  /**
   * The words of the end of the battle (review 21; scene text around the frame's numbers): a win's paper — whom it beat
   * (the roster's names), the turns and the HP kept, the chain's fight — or a loss's result screen (§2.5): where the hero
   * fell, 残 HP, the cards played by attribute, the traits fired, the turns, and 「手記へ」.
   */
  function resultInfo(outcome) {
    const f = frameNow() || {};
    const p = f.Player || { Hp: 0, HpMax: 0 };
    const corner = f.Corner || {};
    const chain = ctx.chain && ctx.chain.total ? ctx.chain : { index: corner.ChainIndex | 0, total: corner.ChainTotal | 0 };
    const turns = corner.Turn | 0;
    if (outcome === BattleOutcome.Won) {
      const names = [...new Set(enemyFrames(f).map((uf) => enemyNameOf(uf.ArtId)).filter(Boolean))];
      return { lines: [RESULT_TEXT.beat(names.join('・')), RESULT_TEXT.wonLine(turns, Math.max(0, p.Hp), p.HpMax)].concat(chain.total > 1 ? [RESULT_TEXT.chainAt(chain)] : []) };
    }
    return {
      title: RESULT_TEXT.fellAt(chain, env.layerNo),
      lines: [RESULT_TEXT.hpLeft(Math.max(0, p.Hp), p.HpMax)],
      barsLabel: RESULT_TEXT.playedHead,
      bars: RESULT_BARS.map((b) => ({ label: b.label, color: b.color, value: b.kinds.reduce((n, k) => n + (tally.played[k] || 0), 0) })),
      stats: [RESULT_TEXT.traits(tally.traits), RESULT_TEXT.turns(turns)],
      button: RESULT_TEXT.toJournal,
    };
  }

  /**
   * battle_ui_ux_v2 §2.5 切替バナー / §5.18 順 6〜7 (review 21; Unity and the scene cut straight to the next fight): a chain's
   * second fight and on opens on the dark board with 「n 戦目」 and the enemy's name, then the enemy comes out of the haze as
   * the board comes up (400, out). ctx.chain is the app's.
   */
  async function chainOpening() {
    const chain = ctx.chain;
    if (!chain || !(chain.total > 1) || !(chain.index > 1)) return;
    const names = [...new Set(enemyFrames(frameNow()).map((uf) => enemyNameOf(uf.ArtId)).filter(Boolean))];
    const on = switches.IsOn(EffectId.TurnBanner);
    if (field.appearEnemies) field.appearEnemies(0, 0);
    await safe(() => hud.chainBanner(RESULT_TEXT.switchTitle(chain.index), names.join('・'), on ? 1 : 0,
      () => (field.appearEnemies ? field.appearEnemies(on ? CHAIN_APPEAR_MS : 0, 0) : undefined)));
    if (field.appearEnemies && !on) field.appearEnemies(0, 1);
  }

  async function finish() {
    closeFreeStep(0);
    setState('over');
    setHandInteractable(false);
    refreshPlayable(); // the guide goes quiet; the outcome is the result card's
    hud.setSpeedVisible(false);
    // Review fix: the pile list closes (it sat bright over the scrim, and its Esc moved past the result).
    if (hud.drawerOpen()) hud.closeDrawer();
    const outcome = frameNow().Outcome;
    // Review fix: no enemy acts again, so a standing enemy's omen badge and plan fade now (OmenSpend), beside
    // the hero's fall on a loss: they stood in the result card's band over the surviving enemy's head.
    if (outcome !== BattleOutcome.Ongoing) {
      effect(EffectId.OmenSpend, () => hud.retireOmens(ms(EffectId.OmenSpend)), () => hud.retireOmens(0));
      // The omens' floor half (their aimed cells) goes with them (review fix: it stayed red under 負け).
      if (field.retireAim) effect(EffectId.OmenSpend, () => field.retireAim(ms(EffectId.OmenSpend)), () => field.retireAim(0));
    }
    // Review fix: a lost battle is the player's fall (the script, like Unity's, never marks the player Down):
    // the hero falls as an enemy does (EffectId.Defeat, DefeatShape) before the result.
    if (outcome === BattleOutcome.Lost && !(frameNow().Player && frameNow().Player.Down)) {
      await effect(EffectId.Defeat, () => field.fall(UnitSide.Player, ms(EffectId.Defeat)), () => field.setDown(UnitSide.Player, true));
      if (!alive) return;
    }
    // battle_ui_ux_v2 §5.19 順 1〜4 (review 19; scene addition — Unity goes from the fall straight to 負け): the foot lights
    // shrink out, the ink floods up from the bottom, 「力尽きた」 comes in, and the screen holds a second before the card.
    if (outcome === BattleOutcome.Lost) {
      if (switches.IsOn(EffectId.Defeat)) {
        await safe(() => (field.dimFeet ? field.dimFeet(LOSS.feetMs) : undefined));
        if (!alive) return;
        await safe(() => (hud.lossInk ? hud.lossInk(LOSS_WORD, LOSS.inkMs, LOSS.wordMs) : undefined));
        if (!alive) return;
        await clock.wait(LOSS.holdMs);
        if (!alive) return;
      } else {
        if (field.dimFeet) field.dimFeet(0);
        if (hud.lossInk) hud.lossInk(LOSS_WORD, 0, 0);
      }
    }
    // Review fix: the field settles under the result (DS 50-game.js finish(): a scrim behind the end card) —
    // the plate goes off, the hand and every fallen unit's panel and stance list fade, the screen dims.
    if (outcome !== BattleOutcome.Ongoing) {
      const settleMs = ms(EffectId.ResultCard);
      effect(EffectId.ResultCard, () => Promise.all([hud.settleEnd(settleMs, outcome), hand.fadeAll(settleMs)]), () => { hud.settleEndNow(outcome); hand.fadeAll(0); });
    }
    // battle_ui_ux_v2 §6.2 「結果の札から終了画面までの 1.2 秒」 (reading time, not scaled by the speed): the hold runs
    // from the card having risen (docs/BATTLE_SCENE.md §4.6). Review 16: Unity raises BattleFinished as the card starts
    // to rise, and the hold and the skip started there too — clicks made to hurry the enemy's turn skipped 負け 0.14 s
    // after it appeared. A skip now counts only once the card has stood L.RESULT_SKIP_AFTER_MS, and only for a press
    // that follows a pause of L.RESULT_SKIP_PAUSE_MS (a press during the mash slides it on; skipResult).
    // Review 21: the end is the design system's paper — a win's (勝ち, whom it beat, the turns, the HP kept) or a loss's result
    // screen (§2.5 / §5.19 順 5〜7), which rises over the ink as 「力尽きた」 gives way, and waits for 「手記へ」.
    resultUpAt = null;
    resultLastPress = -Infinity;
    resultSkip = () => {};
    const info = resultInfo(outcome);
    const onButton = () => { if (resultUpAt != null && resultSkip) resultSkip(); };
    if (outcome !== BattleOutcome.Ongoing) {
      const lost = outcome === BattleOutcome.Lost;
      const paperMs = lost ? LOSS.paperMs : ms(EffectId.ResultCard);
      if (lost && hud.lossWordOut) effect(EffectId.ResultCard, () => hud.lossWordOut(paperMs), () => hud.lossWordOut(0));
      await effect(EffectId.ResultCard, () => hud.resultCard(outcome, paperMs, info, { onButton }), () => hud.resultCardNow(outcome, info, { onButton }));
    }
    if (!alive) return;
    resultUpAt = real.now;
    // A loss waits for 「手記へ」 (or a click, Enter, Space or Esc: skipResult); a win — and the dev page's auto play — goes
    // on after RESULT_HOLD_MS, as Unity's DemoFlow does.
    const waits = outcome === BattleOutcome.Lost && !ctx.auto;
    await Promise.race([waits ? new Promise(() => {}) : holdUnpaused(L.RESULT_HOLD_MS), new Promise((res) => { resultSkip = res; })]);
    resultSkip = null;
    if (!alive || finishedCalled) return;
    finishedCalled = true;
    try { if (ctx.onFinished) ctx.onFinished(); } catch (err) { console.error(err); }
  }

  // ---- auto play (dev / tests): the suggested drag through the hand's own path (AutoDrag) -----

  function scheduleAuto() {
    if (!ctx.auto || state !== 'idle') return;
    const token = ++autoTimer;
    real.wait(L.AUTO_WAIT_MS).then(async () => {
      if (!alive || token !== autoTimer || state !== 'idle' || busy || hand.isHolding()) return;
      if (freeStep) {
        // 俊敏's choice first (ctx.auto.freeStep(source) → −1 / 0 / +1; staying when it has no opinion).
        let dir = 0;
        try { dir = typeof ctx.auto.freeStep === 'function' ? ctx.auto.freeStep(src()) | 0 : 0; } catch (err) { console.error(err); }
        if (!pickFreeStep(dir === 1 || dir === -1 ? dir : 0)) pickFreeStep(0);
        return;
      }
      let id = '';
      let unit;
      try {
        const pick = typeof ctx.auto.choose === 'function' ? ctx.auto.choose(src(), playedThisTurn) : src().SuggestedCardId;
        // choose → a card id, or { id, unit } naming the enemy it goes to (several enemies)
        if (pick && typeof pick === 'object') { id = pick.id || ''; unit = pick.unit; } else id = pick || '';
      } catch (err) { console.error(err); id = ''; }
      if (!id) { requestEndTurn(); return; }
      const face = DepictionText.Find(frameNow().Hand, id);
      if (!face) { requestEndTurn(); return; }
      await safe(() => hand.autoDrag(id, DepictionText.RequiredZone(face.Aim), unit));
    });
  }

  // ---- speed (#348) -------------------------------------------------------------------------

  function showSpeed() {
    const step = stepOfSpeed(env.settings.speed);
    hud.speed(BattleSpeed.Label(step), BattleSpeed.Chevrons(step));
  }

  /** CycleSpeed: ×1.0 → ×1.25 → ×1.5 → ×1.0. Takes effect at once (the clock reads settings every frame). */
  function cycleSpeed() {
    const next = BattleSpeed.Next(stepOfSpeed(env.settings.speed));
    env.settings.speed = speedOfStep(next);
    sfx('uiClick');
    showSpeed();
    try { if (ctx.onSettingsChange) ctx.onSettingsChange(env.settings); } catch (err) { console.error(err); }
  }

  // ---- per frame -----------------------------------------------------------------------------

  const offFrame = clock.onFrame(() => {
    if (!alive) return;
    followFigures();
    updateFloorReach();
  });

  // ---- the hand's callbacks ------------------------------------------------------------------

  const handCallbacks = {
    onRelease,
    onHover(face) { hoveredFace = face || null; updateFloorReach(); },
    onHoldStart(face, unit) {
      heldFace = face || null;
      heldUnit = unit != null && unit >= 0 ? unit : -1;
      // §4.7 / §5.2: the stamina it would pay turns to rings, and "▸ n" shows (on the enemy it frames: CostFor(k)).
      showHeldCost();
      showReserveHint();
      showHpLoss();
      updateFloorReach();
      liftOmenForHeld();
    },
    onHoldEnd() {
      heldFace = null;
      heldUnit = -1;
      hud.stamina.showCost(0);
      showReserveHint();
      showHpLoss();
      updateFloorReach();
      liftOmenForHeld();
    },
    /**
     * The one predicted value, read against enemy `unit` in a battle of several. The source's PreviewFor says whether
     * there is one (the card would be accepted) and gives it; for a blow on an enemy the tag shows the damage that
     * reaches its HP, after its Guard — battle_ui_ux_v2 §1.1 A6 / §3.2 「相手の Guard を引いた後の値」, the same number
     * as the HP wick on its bar (CardFace.HpLossFor(k), the writer's) and as the number that will rise (§5.0). Review
     * fix: PreviewFor (C# CoreBattleSource.PreviewFor too) gives the raw power before the Guard, so over an enemy with
     * Guard 5 the tag read 17 while the wick showed 12.
     */
    preview(cardId, unit) {
      if (!alive) return '';
      const target = targetFor(unit);
      const raw = (target === undefined ? ctx.source.PreviewFor(cardId) : ctx.source.PreviewFor(cardId, target)) || '';
      if (!raw) return '';
      const face = DepictionText.Find(frameNow().Hand, cardId);
      const blow = face && face.Aim === CardAim.Single && face.HpLossOn && face.HpLossOn.length > 0
        && (face.Values || []).some((v) => v.Kind === CardValueKind.Power);
      if (!blow) return raw;
      const u = unit != null && unit >= 0 ? unit : face.TargetUnit >= 0 ? face.TargetUnit : 0;
      return String(face.HpLossFor(u));
    },
    /**
     * Several enemies: the held card is framed over another enemy (`unit`): its HP wick, the stamina's 「▸ n」, the
     * 構えの予告 and the badge lift follow it (port notes 「Holding a card」: CostFor(k), StanceHintAfterPayFor(k)).
     */
    onTarget(face, unit) {
      if (unit != null && unit >= 0) heldUnit = unit;
      showHeldCost();
      showReserveHint();
      showHpLoss();
      liftOmenForHeld();
    },
  };

  return {
    get state() { return state; },
    get busy() { return busy; },
    onState(fn) { listeners.add(fn); return () => listeners.delete(fn); },
    /** fn() after every screen event has played out with its after-wait (the scene's one-press fast-forward). */
    onEventEnd(fn) { eventEndListeners.add(fn); return () => eventEndListeners.delete(fn); },
    /** How many screen events have played out (onEventEnd's count). */
    get eventEnds() { return eventEnds; },
    /**
     * An event has played and its after-wait runs now, its onEventEnd still to come (review 19: a fast-forward pressed in
     * that pause belongs to the next event; BattleScene.armFF).
     */
    get afterWaiting() { return afterWaiting; },
    /** The pile list's log lines so far (dev / tests). */
    logLines() { return log.slice(); },
    handCallbacks,
    /** Start / Restart: the opening frame at once, then the automatic events. */
    start() {
      const frame = frameNow();
      snapPanels = true;
      // A new battle: each stance light is as the first frame says, with no fade (the field's first applyFrame).
      field.setStanceGlow(UnitSide.Player, StanceGlow.IsOn(frame.Player));
      applyFrame(frame, true);
      enemyFrames(frame).forEach((uf, k) => field.snapHome(UnitSide.Enemy, enemyFrames(frame).length > 1 ? unitNo(uf, k) : undefined));
      showSpeed();
      return chainOpening().then(() => (alive ? runAutomaticEvents() : undefined)).catch((err) => console.error(err));
    },
    requestEndTurn,
    canEndTurn,
    /** A refused card is sliding home (the end turn waits for it). */
    get returning() { return returningCard; },
    /**
     * Tab with nothing held (battle_ui_ux_v2 §3.4, two or more enemies): the floor's one N moves to the next
     * standing enemy, left to right (`dir` −1 goes back). Returns whether it moved.
     */
    cycleGapFocus(dir) {
      const list = enemyFrames(frameNow()).map((uf, k) => ({ u: unitNo(uf, k), uf })).filter((x) => !x.uf.Down);
      if (list.length < 2) return false;
      const geomX = (u) => { const g = env.geom.figure(UnitSide.Enemy, u); return g ? g.homeX : 0; };
      list.sort((a, b) => geomX(a.u) - geomX(b.u));
      const fl = frameNow().Floor;
      const now = restingGapUnit != null ? restingGapUnit : (fl && fl.EnemyCell > 0 ? (list.find((x) => x.uf.Cell === fl.EnemyCell) || list[0]).u : list[0].u);
      const i = Math.max(0, list.findIndex((x) => x.u === now));
      restingGapUnit = list[(i + (dir < 0 ? list.length - 1 : 1)) % list.length].u;
      showHpLoss();
      return true;
    },
    /**
     * The enemy a Tab at rest moved the floor's N to (battle_ui_ux_v2 §1.4 / §3.4), while it stands; −1 for none.
     * A selection starts framed on it (hand opts.restingUnit), and the dev check reads it.
     */
    get restingUnit() {
      if (restingGapUnit == null) return -1;
      const standing = enemyFrames(frameNow()).some((uf, k) => unitNo(uf, k) === restingGapUnit && !uf.Down);
      return standing ? restingGapUnit : -1;
    },
    cycleSpeed,
    showSpeed,
    /** The enemies of this fight, front first (for the journal). */
    enemyIds() {
      try { return src().State.Enemies.map((u) => u.Def.Id); } catch (err) { return frameNow() && frameNow().Enemy.ArtId ? [frameNow().Enemy.ArtId] : []; }
    },
    /** Whether the source would take card `id` now (read only: Inspect). */
    isPlayable(id) {
      try { return src().Inspect(id) === PlayVerdict.Accepted; } catch (err) { return false; }
    },
    /** How many plays the source has accepted in this battle (the keyboard path's auto-select). */
    get acceptedPlays() { return acceptedPlays; },
    /** Fast-forward is allowed while the flow runs by itself or plays a move. */
    canFastForward() { return state === 'auto' || state === 'resolving'; },
    setOverlayCheck(fn) { overlayOpen = fn; },
    setDrawerCheck(fn) { drawerOpen = typeof fn === 'function' ? fn : () => false; },
    setJournalCheck(fn) { journalOpen = typeof fn === 'function' ? fn : () => false; },
    /**
     * A card was pressed (or picked by its digit) while the pile list is open: the reason on the refusal line — 俊敏's
     * choice's while that waits (closing the list would not free the cards either).
     */
    refuseWhileDrawer() {
      if (!alive || state !== 'idle') return;
      if (freeStep) { refuseWhileChoosing(); return; }
      sfx('refusal');
      showCauseRefusal('drawer', DRAWER_GUIDE);
    },
    /** A card was pressed while the journal is open (review 17): the reason on the refusal line. */
    refuseWhileJournal() {
      if (!alive || state !== 'idle') return;
      sfx('refusal');
      showCauseRefusal('journal', JOURNAL_GUIDE);
    },
    /**
     * The scene's overlays changed (review 21): a refusal line that asked to close the pile list or the journal goes as soon
     * as that one is closed.
     */
    overlaysChanged(o) {
      if (!o || !o.drawer) clearRefusal('drawer');
      if (!o || !o.journal) clearRefusal('journal');
    },
    /**
     * A click / key once the battle is over moves the result on — once the card has stood L.RESULT_SKIP_AFTER_MS and
     * after a pause of L.RESULT_SKIP_PAUSE_MS since the last such press (a mash keeps sliding the pause on).
     */
    skipResult() {
      if (!resultSkip) return;
      const now = real.now;
      const fresh = now - resultLastPress >= L.RESULT_SKIP_PAUSE_MS;
      resultLastPress = now;
      if (resultUpAt == null || now - resultUpAt < L.RESULT_SKIP_AFTER_MS || !fresh) return;
      resultSkip();
    },
    /** Halt (DepictionPlayer.Halt): no more input, no more events. */
    halt() {
      halted = true;
      closeFreeStep(0);
      if (hand.isHolding()) hand.letGo();
      setHandInteractable(false);
    },
    refreshPlayable,
    /** 俊敏's choice waits (battle_ui_ux_v2 §3.8): the scene's ← / → / Enter go to it. */
    get freeStepPending() { return !!freeStep; },
    /** The ways the source said are open, while the choice waits: { [-1]: bool, [1]: bool }, else null. */
    get freeStepWays() { return freeStep ? Object.assign({}, freeStep.ways) : null; },
    pickFreeStep,
    focusFreeStep,
    moveFreeStepFocus,
    stayFreeStepFocus,
    /** Enter: the focused mark is chosen; with no focus nothing happens (as every focus of the battle). */
    confirmFreeStep() {
      if (!freeStep) return false;
      const f = field.freeStepFocus;
      return f == null ? false : pickFreeStep(f);
    },
    destroy() {
      alive = false;
      autoTimer++;
      offFrame();
    },
  };
}
