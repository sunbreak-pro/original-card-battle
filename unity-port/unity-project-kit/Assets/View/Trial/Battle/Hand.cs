// The hand module of the battle scene (docs/BATTLE_SCENE.md §6a): the cards in hand, the fan, the hover and its
// panels, hold and release, the drop zones, the target hooks, the throw line, the prediction tag, the refusal line, and
// the keyboard / click path. Port of scenes/battle/hand.js (Unity: CardView, CardPanels, ReceiverView, ThrowLineView,
// TargetMarkView, RefusalLineView and the hover / drag / fan / guide parts of DepictionPlayer: LayoutHand, HomeOf,
// HoverPlace, UpdateHover, UpdateHoverPanels, CardUnderPointer, BeginHold, ShowTargetMark, SetZoneHot, ZoneAt,
// ReceiverTakes, UpdateHot, DragPosition, UpdateSnap, LetGo, Release, FlyDrawn, FlyDiscarded, FlyCard, SettleHand,
// ReturnToHand, RefreshPlayableLook, Refusal, AutoDrag), drawn with the design system.
//
// It computes no rule: whether a card plays is the source's (the player's OnRelease → TryPlay), what the cards say is the
// CardFace's, and where they stand is the ported HandSpec / HandFan / ReceiverDrop / RefusalLineLayout. Every length runs
// on env.Clock (battle speed, 演出短縮, fast-forward); input timings (the drag threshold, the long press, the refusal
// line's 3.5 s) on env.Real.
//
// Feel: Unity's numbers, and the design system's prototype (build/src/50-game.js beginHold / updateHold / zoneTest /
// accept / refuse / backToHand, FEEL_DEFS) where Unity does not say otherwise.
//
// Several enemies (scene addition, docs/port-notes/depiction-multi-enemy.md): a single-target card has one receiver per
// standing enemy (env.Geom.Receivers); the enemy whose receiver takes the card is the one it is framed over and released
// on (OnRelease(cardId, zone, unit), Preview(cardId, unit), CardFace.LitFor(unit)); with no enemy named the source sends it
// to CardFace.TargetUnit.
//
// Input (Unity): the scene routes the raw pointer to the hand (OnPointerDown / Move / Up / Cancel / Context / Leave /
// Blur, in the browser's order, battle-flow §3.1); the hand hit-tests its cards by geometry (rotated rects, CardUnder).
// The uGUI raycast only tells what the DOM's e.target told: BattlePointer.Layer (a card is a hit in layer hand / held, as
// .ocbg-battle-hit; another module's piece in hud / over / panels is foreign). Fills env.Geom.HandCard. Plays its own sounds.
using System;
using System.Collections.Generic;
using Depiction;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;

namespace Trial.Battle
{
    // js: scenes/battle/hand.js:79 (createHand)
    public sealed class Hand : IHand
    {
        // ---- constants (hand.js:33-52) -------------------------------------------------------------------

        // js: scenes/battle/hand.js:33
        private const float W2 = HandCard.CardW / 2f;
        // js: scenes/battle/hand.js:34
        private const float H2 = HandCard.CardH / 2f;
        /// <summary>CARD_SPACING / FAN_DEGREES — DepictionPlayer inspector: cardSpacing (five or fewer) and fanDegreesPerCard (battle-visual-v1 §4.8).</summary>
        // js: scenes/battle/hand.js:36
        private const float CardSpacing = 160f;
        // js: scenes/battle/hand.js:37
        private const float FanDegrees = 2.5f;
        /// <summary>PILE_SCALE — 50-game.js: a drawn card comes out of the 64×84 pile small and grows (0.3 → 1); a discarded one shrinks into it.</summary>
        // js: scenes/battle/hand.js:41
        private const float PileScale = 0.3f;
        /// <summary>STANCE_CARD_SCALE — §5.11 順 1: a stance card shrinks into the stance list's slot to its ghost's width (76 of 216, field/fx.js ghost).</summary>
        // js: scenes/battle/hand.js:43
        private const float StanceCardScale = 76f / 216f;
        /// <summary>LAMP_SWELL — §5.9 順 1: the lamp swells to 1.4 and back over this (nominal ms, out).</summary>
        // js: scenes/battle/hand.js:45
        private const float LampSwellScale = 1.4f;
        private const float LampSwellMs = 150f;
        /// <summary>HOVER_SOUND_GAP_MS — sound.md: the hover sound is not repeated within 50 ms.</summary>
        // js: scenes/battle/hand.js:47
        private const float HoverSoundGapMs = 50f;
        // js: scenes/battle/hand.js:48
        private static readonly StagePoint Far = new StagePoint(-99999f, -99999f);
        /// <summary>LONG_PRESS_RETURN_MS — battle_ui_ux_v2 §3.2 / §3.13: a long-pressed card goes back to its rest place over 200 ms before the detail opens.</summary>
        // js: scenes/battle/hand.js:50
        private const float LongPressReturnMs = 200f;
        /// <summary>END_HAND_ALPHA — the battle is over (review fix): the hand fades to this under the result card's scrim.</summary>
        // js: scenes/battle/hand.js:52
        private const float EndHandAlpha = 0.35f;

        /// <summary>hand.css .ocbg-battle-card-trait: the box of the fired trait's condition word (review 19, §5.9 順 1).</summary>
        private const string TraitWordBoxCss =
            "padding: 2px 10px; background: var(--panel-solid); border: var(--stroke-frame) solid var(--wick); border-radius: var(--radius-tag, 4px); box-shadow: 0 0 12px color-mix(in srgb, var(--amber) 45%, transparent);";
        /// <summary>hand.css .ocbg-battle-card-trait: its text.</summary>
        private const string TraitWordTextCss = "font: 700 20px/26px var(--font-body); color: var(--ink); white-space: nowrap;";
        /// <summary>The word box: padding 2 / 10, border --stroke-frame (2), line height 26, margin-bottom 6 over the trait box.</summary>
        private const float TraitWordPadX = 10f, TraitWordPadY = 2f, TraitWordBorder = 2f, TraitWordLine = 26f, TraitWordGap = 6f;

        /// <summary>The DOM order an appendChild gives (after every element made before): above UiNode's creation counter.</summary>
        private const double AppendedDom = 1e9;
        /// <summary>A ghost card the field did not take within this many real ms is dropped (the JS element would be collected).</summary>
        private const float GhostKeepMs = 1000f;

        /// <summary>The release sound by the card's attribute (sound.md 「動作」).</summary>
        // js: scenes/battle/hand.js:39 (RELEASE_SOUND)
        private static string ReleaseSoundOf(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.Attack: return "releaseAttack";
                case CardKind.Guard: return "releaseDefense";
                case CardKind.Stance: return "releaseStance";
                default: return "releaseSkill";
            }
        }

        /// <summary>--opacity-unpayable (0.6 when the token is missing).</summary>
        // js: scenes/battle/hand.js:54
        private static float DimAlpha()
        {
            float v = DsTokens.OpacityUnpayable;
            return float.IsNaN(v) || float.IsInfinity(v) ? 0.6f : v;
        }

        // js: scenes/battle/hand.js:61
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>Mathf.MoveTowards: steps `cur` toward `target` by at most `step` and stops on it (no overshoot, no swing).</summary>
        // js: scenes/battle/hand.js:64
        private static float MoveTowards(float cur, float target, float step)
        {
            if (Math.Abs(target - cur) <= step) return target;
            return cur + Math.Sign(target - cur) * step;
        }

        /// <summary>The axis-aligned box a w×h card centred at (x, y) and turned `rot`° covers (DepictionPlayer.BoxOn: a tilted card counts its whole outline).</summary>
        // js: scenes/battle/hand.js:70
        private static StageBox Aabb(float x, float y, float rot)
        {
            double a = rot * Math.PI / 180.0;
            float c = (float)Math.Abs(Math.Cos(a));
            float s = (float)Math.Abs(Math.Sin(a));
            float hw = W2 * c + H2 * s;
            float hh = W2 * s + H2 * c;
            return new StageBox(x - hw, y - hh, 2f * hw, 2f * hh);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        /// <summary>Math.round: half up (toward +∞).</summary>
        private static float JsRound(float v) => (float)Math.Floor(v + 0.5);

        private static float Hypot(float x, float y) => (float)Math.Sqrt((double)x * x + (double)y * y);

        // ---- the per-card and per-gesture state --------------------------------------------------------

        private enum CardMode
        {
            Fan,
            Held,
            Free,
        }

        /// <summary>A card's pose { x, y, rot, s, a }: centre px, CSS degrees (clockwise), scale, alpha. The clock tweens it by key.</summary>
        private sealed class Pose : ITweenable
        {
            public float X = 960f;
            public float Y = 1300f;
            public float Rot;
            public float S = 1f;
            public float A = 1f;

            public float GetTween(string key)
            {
                switch (key)
                {
                    case "x": return X;
                    case "y": return Y;
                    case "rot": return Rot;
                    case "s": return S;
                    case "a": return A;
                    default: return 0f;
                }
            }

            public void SetTween(string key, float value)
            {
                switch (key)
                {
                    case "x": X = value; break;
                    case "y": Y = value; break;
                    case "rot": Rot = value; break;
                    case "s": S = value; break;
                    case "a": A = value; break;
                }
            }
        }

        /// <summary>One card: { id, face, key, wrap, view, mode, pose, hw, shake, dim, dimA, dimToken, litFor, costFor, motion }.</summary>
        // js: scenes/battle/hand.js:128
        private sealed class CardItem
        {
            public string Id;
            public CardFace Face;
            public string Key = "";
            /// <summary>.ocbg-battle-card.ocbg-battle-hit: 216×304 at (0, 0) of its layer, transform-origin 50% 50%.</summary>
            public RectTransform Wrap;
            public UiNode Node;
            public CardFaceView View;
            public CardMode Mode = CardMode.Fan;
            public readonly Pose Pose = new Pose();
            /// <summary>hw: the hover weight 0..1.</summary>
            public float Hw;
            public float Shake;
            public bool Dim;
            public float DimA;
            public int DimToken;
            public bool? LitFor;
            public int? CostFor;
            public int Motion;
        }

        /// <summary>What a held card and a selected card share: the card, hot, the framed unit, its base unit.</summary>
        private abstract class FrameState
        {
            public CardItem Card;
            public bool Hot;
            /// <summary>unit: the enemy the pointer or Tab named (−1 for none).</summary>
            public int Unit = -1;
            /// <summary>base: the enemy the card is framed on before one is named (baseUnit).</summary>
            public int Base;
        }

        /// <summary>held: a drag — { card, offX, offY, hot, snapW, auto, zone, unit, base }.</summary>
        private sealed class HoldState : FrameState
        {
            public float OffX;
            public float OffY;
            public float SnapW;
            public bool Auto;
            public DropZone Zone = DropZone.None;
        }

        /// <summary>sel: the click / key path's selection — { card, hot, unit, tabbed, tabAt, base, auto }.</summary>
        private sealed class SelState : FrameState
        {
            public bool Tabbed;
            public StagePoint? TabAt;
            public bool Auto;
        }

        /// <summary>down: a press on a card that is not yet a drag — { card, x, y, token, long }.</summary>
        private sealed class PressState
        {
            public CardItem Card;
            public float X;
            public float Y;
            public int Token;
            public bool Long;
        }

        /// <summary>A receiver row { unit, box } (one with a single enemy, unit −1 when the field names none).</summary>
        private readonly struct Recv
        {
            public readonly int Unit;
            public readonly ScreenBox Box;

            public Recv(int unit, ScreenBox box)
            {
                Unit = unit;
                Box = box;
            }
        }

        /// <summary>zoneAt's answer: { zone, unit }.</summary>
        private readonly struct ZoneHit
        {
            public readonly DropZone Zone;
            public readonly int Unit;

            public ZoneHit(DropZone zone, int unit)
            {
                Zone = zone;
                Unit = unit;
            }
        }

        private readonly BattleViewEnv _env;
        private readonly HandOpts _opts;
        private readonly float _dim;

        /// <summary>cards: in the fan, left to right.</summary>
        private List<CardItem> _cards = new List<CardItem>();
        /// <summary>leaving: released cards on their way out (a Map by id, in insertion order).</summary>
        private readonly List<CardItem> _leaving = new List<CardItem>();
        /// <summary>returning: cards sliding home (ReturnToHand / a cancel): the hand is busy meanwhile (Unity's _busy).</summary>
        private readonly HashSet<CardItem> _returning = new HashSet<CardItem>();
        private bool _interactable;
        private CardItem _hovered;
        private HoldState _held;
        private SelState _sel;
        private PressState _down;
        /// <summary>bgDown: a press anywhere else in the scene.</summary>
        private StagePoint? _bgDown;
        private StagePoint _ptr = Far;
        /// <summary>ptrClient: the pointer where the last pointer event put it (null once it left): the term tips test it.</summary>
        private StagePoint? _ptrClient;
        /// <summary>ptrForeign: the pointer is over another module's panel or an overlay.</summary>
        private bool _ptrForeign;
        private Func<string, bool> _dimOf = _ => false;
        private float _lastHoverSound = -1e9f;
        private int _pressToken;
        private bool _destroyed;
        /// <summary>An overlay (the journal, the settings) is up: the hand takes no press, hover or grab (review fix).</summary>
        private bool _blocked;
        /// <summary>The overlay that blocks is the journal: the cards can still be hovered (battle_ui_ux_v2 §2.4).</summary>
        private bool _hoverWhileBlocked;
        /// <summary>While blocked by the pile list: what a press on a card (or its digit) calls — the reason, on the refusal line.</summary>
        private Action _blockedTry;
        /// <summary>
        /// battle_ui_ux_v2 §3.8: while 俊敏's choice waits, no card is taken (a press, a digit); the cards still lift on hover
        /// so the hand can be read. What to do when a card is tried meanwhile (the player's refusal).
        /// </summary>
        private Action _grabLock;

        private readonly HandMarks _marks;
        private readonly HandPanels _panels;

        /// <summary>§3.5 / §5.10 (review 21): the player's 集中 / 強化 / 威圧 as marks beside the values of the cards they act on.</summary>
        private StatusMarks _statusMarks;
        /// <summary>The card being handed to the player right now: the dark sheet the flow puts on the hand skips it (it is played).</summary>
        private CardItem _handingOver;
        /// <summary>A press that closed the locked detail: its release does nothing more (no selection, no drop).</summary>
        private bool _swallowUp;
        /// <summary>The card a long press sent back to its rest place (§3.13): it is not raised again until the pointer leaves it.</summary>
        private CardItem _longRest;
        /// <summary>The card raised when the refusal line came up (its own press was refused): the line does not step aside for it.</summary>
        private CardItem _refusalOwner;

        private readonly Action _offFrame;
        private double _domSeq;
        /// <summary>Where GhostCard builds a face before the field takes it (unseen: opacity 0), and when each was built.</summary>
        private RectTransform _ghostHold;
        private readonly List<KeyValuePair<RectTransform, float>> _ghosts = new List<KeyValuePair<RectTransform, float>>();

        private Hand(BattleViewEnv env, HandOpts opts)
        {
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _opts = opts ?? new HandOpts();
            _dim = DimAlpha();
            _marks = HandMarks.Create(env);
            _panels = HandPanels.Create(env);
            // window pointerdown / move / up / cancel, blur, visibilitychange, the root's pointerleave and contextmenu: the
            // scene's pointer pump calls OnPointerDown … OnBlur in the browser's order.
            _offFrame = env.Clock.OnFrame(OnFrame);
            env.Geom.HandCard = HandCardAt;
        }

        /// <summary>
        /// createHand(env, opts): the marks (HandMarks.Create) and the panels (HandPanels.Create) are made, the clock's frame
        /// loop subscribed, env.Geom.HandCard registered.
        /// </summary>
        // js: scenes/battle/hand.js:79
        public static Hand Create(BattleViewEnv env, HandOpts opts) => new Hand(env, opts);

        private void Play(string name, float delayMs = 0f) => _env.Audio.Play(name, delayMs);

        private static bool ViewAlive(CardItem c) => c.View != null && c.View.Root;

        /// <summary>layer.appendChild(c.wrap): the card moves to the end of `layer`.</summary>
        private void AppendTo(CardItem c, RectTransform layer)
        {
            if (!c.Wrap || !layer) return;
            if (c.Wrap.parent != layer) c.Wrap.SetParent(layer, false);
            c.Node.DomOrder = AppendedDom + ++_domSeq;
            UiNode.ResortSiblings(layer);
        }

        // ---- places (HandSpec / HandFan, battle-visual-v1 §4.8) ---------------------------------------

        private readonly struct Place3
        {
            public readonly float X;
            public readonly float Y;
            public readonly float Rot;

            public Place3(float x, float y, float rot)
            {
                X = x;
                Y = y;
                Rot = rot;
            }
        }

        // js: scenes/battle/hand.js:116
        private static float SpacingOf(int n) => HandFan.Spacing(n, CardSpacing);

        /// <summary>HomeOf: the resting centre and tilt (CSS degrees, clockwise) of card i of n.</summary>
        // js: scenes/battle/hand.js:118
        private static Place3 Rest(int i, int n)
        {
            FanPlace p = HandSpec.Rest(n, i, SpacingOf(n), FanDegrees);
            return new Place3(p.X, p.Y, -p.Degrees);
        }

        /// <summary>HoverPlace: upright, raised until its top is at y 760.</summary>
        // js: scenes/battle/hand.js:120
        private static Place3 HoverPlace(int i, int n)
        {
            FanPlace p = HandSpec.Hover(n, i, SpacingOf(n));
            return new Place3(p.X, p.Y, 0f);
        }

        // ---- cards --------------------------------------------------------------------------------------

        // js: scenes/battle/hand.js:123
        private CardItem Make(CardFace face)
        {
            // h('div', { class: 'ocbg-battle-card ocbg-battle-hit', 'data-card': face.Id }), appended to layer.hand.
            RectTransform wrap = Ui.Div(_env.Layer.Hand, "ocbg-battle-card " + face.Id, 0f, 0f, HandCard.CardW, HandCard.CardH);
            UiNode node = UiNode.Of(wrap);
            node.DomOrder = AppendedDom + ++_domSeq;
            // .ocbg-battle-hit (pointer-events: auto): the card's own box takes the pointer, so the pointer over a card is not
            // over the piece under it (IsForeign). The hand hit-tests the cards by geometry (CardUnder).
            Hit.Area(wrap);
            // A card made mid-flow (the turn start's draw) comes in under the sheet the hand wears now (refreshPlayable's
            // dimOf: the whole hand is dark while the flow runs), with no fade.
            bool dark = _dimOf(face.Id);
            var c = new CardItem { Id = face.Id, Face = face, Wrap = wrap, Node = node, Dim = dark, DimA = dark ? _dim : 0f };
            Draw(c);
            // wrap.addEventListener('contextmenu', preventDefault): no browser menu in Unity.
            return c;
        }

        /// <summary>CardView.Bind: a new face only when something it prints changed. The lamp and the dark sheet carry over.</summary>
        // js: scenes/battle/hand.js:135
        private void Draw(CardItem c)
        {
            string key = HandCard.FaceKey(c.Face);
            if (key == c.Key && ViewAlive(c)) { ApplyLamp(c); ApplyMods(c); return; }
            c.Key = key;
            // c.wrap.innerHTML = ''; c.wrap.appendChild(c.view.el)
            if (c.View != null && c.View.Root) Ui.Remove(c.View.Root);
            c.View = HandCard.BuildCard(c.Face, c.Wrap);
            c.View.SetDim(c.DimA);
            ApplyLamp(c);
            ApplyMods(c);
        }

        // js: scenes/battle/hand.js:148
        private void ApplyMods(CardItem c)
        {
            if (ViewAlive(c)) c.View.SetMods(HandCard.ModsOf(c.Face, _statusMarks));
        }

        /// <summary>§3.13 (review 21): what the detail says the card does now — its lamp, the source's prediction, the marks on it.</summary>
        // js: scenes/battle/hand.js:152 (detailExtra)
        private DetailExtra DetailExtraOf(CardItem c)
        {
            string preview;
            try { preview = _opts.Preview != null ? _opts.Preview(c.Id, -1) ?? "" : ""; }
            catch (Exception) { preview = ""; }
            return new DetailExtra { Lit = c.View != null && c.View.Lit, Preview = preview, Mods = HandCard.ModsOf(c.Face, _statusMarks) };
        }

        /// <summary>ShowLamp: the face's lamp, or the framed enemy's verdict (CardFace.LitFor(unit)) while a single-target card is up.</summary>
        // js: scenes/battle/hand.js:158
        private static void ApplyLamp(CardItem c)
        {
            if (!ViewAlive(c)) return;
            c.View.SetLit(c.LitFor ?? c.Face.TraitLit);
            if (c.CostFor.HasValue) c.View.SetCost(c.Face.CostFor(c.CostFor.Value), c.Face.CostDropFor(c.CostFor.Value));
            else c.View.SetCost(c.Face.Cost, c.Face.CostDrop);
        }

        /// <summary>
        /// While a single-target card is up (held or selected) its lamp and its cost circle are read on the enemy it frames
        /// (CardFace.LitFor(k), CostFor(k) / CostDropFor(k): port notes 「Holding a card」, review fix — the circle kept the
        /// face's own cost over a dearer enemy); off, the face's own.
        /// </summary>
        // js: scenes/battle/hand.js:172
        private static void SetLitFor(CardItem c, bool on, int unit = -1)
        {
            int u = unit >= 0 ? unit : DefaultUnit(c.Face);
            bool framed = on && c.Face.Aim == CardAim.Single;
            c.LitFor = framed ? c.Face.LitFor(u) : (bool?)null;
            c.CostFor = framed ? u : (int?)null;
            ApplyLamp(c);
        }

        /// <summary>The enemy a single-target card goes to when no receiver names one (CardFace.TargetUnit; 0 with one enemy).</summary>
        // js: scenes/battle/hand.js:180
        private static int DefaultUnit(CardFace face) => face != null && face.TargetUnit >= 0 ? face.TargetUnit : 0;

        /// <summary>
        /// The enemy a lifted card is framed on before the pointer or Tab names one (review fix, battle_ui_ux_v2 §1.4
        /// 「選んでいる相手との N（静止時はいちばん近い敵。Tab か対象の枠で移る）」): the enemy a Tab at rest chose (RestingUnit, the
        /// floor's N), when it still stands; else the card's own target (CardFace.TargetUnit).
        /// </summary>
        // js: scenes/battle/hand.js:188
        private int BaseUnit(CardFace face)
        {
            if (face == null || face.Aim != CardAim.Single) return DefaultUnit(face);
            int u = _opts.RestingUnit != null ? _opts.RestingUnit() : -1;
            if (u >= 0)
                foreach (Recv r in Receivers())
                    if (r.Unit == u) return u;
            return DefaultUnit(face);
        }

        /// <summary>The enemy a held / selected card is framed on now: the one the pointer or Tab named, else its base.</summary>
        // js: scenes/battle/hand.js:195
        private static int FramedOf(FrameState st) => st.Unit >= 0 ? st.Unit : st.Base;

        /// <summary>Where a card in the fan stands now: its rest, eased toward its hover place by the hover weight (smoothstep, Unity).</summary>
        // js: scenes/battle/hand.js:200
        private static void FanPose(CardItem c, int i, int n)
        {
            Place3 r = Rest(i, n);
            Place3 hv = HoverPlace(i, n);
            float w = Ease.Smooth(Clamp01(c.Hw));
            Pose p = c.Pose;
            p.X = Lerp(r.X, hv.X, w);
            p.Y = Lerp(r.Y, hv.Y, w);
            p.Rot = r.Rot * (1f - w);
            p.S = 1f;
            p.A = 1f;
        }

        // js: scenes/battle/hand.js:207
        private void Render()
        {
            List<CardItem> list = _cards;
            int n = list.Count;
            for (int i = 0; i < n; i++)
                if (list[i].Mode == CardMode.Fan) FanPose(list[i], i, n);
            for (int i = 0; i < n; i++)
            {
                CardItem c = list[i];
                // Cards draw left to right; the hovered one in front, and one still settling from a hover stays in front of its neighbours (50-game.js).
                int z = 10 + i;
                if (_sel != null && _sel.Card == c) z = 300;
                else if (c == _hovered) z = 250;
                else if (c.Mode == CardMode.Fan && c.Hw > 0.01f) z = 200 + (int)JsRound(c.Hw * 10f);
                if (_returning.Contains(c)) z = 400;
                Paint(c, z);
            }
            foreach (CardItem c in _leaving) Paint(c, 450);
        }

        /// <summary>
        /// paint(c, z): transform translate(x − W2 + shake, y − H2) rotate(rot) scale(s) about the card's middle, opacity a
        /// (clamped), z-index z.
        /// </summary>
        // js: scenes/battle/hand.js:212
        private static void Paint(CardItem c, int z)
        {
            if (!c.Wrap) return;
            Pose p = c.Pose;
            c.Node.SetTransform(p.X - W2 + c.Shake, p.Y - H2, p.S, p.S, p.Rot);
            c.Node.Opacity = Clamp01(p.A);
            c.Node.Z = z;
        }

        // ---- hover (UpdateHover / CardUnderPointer) -----------------------------------------------------

        // js: scenes/battle/hand.js:231
        private static bool InRect(StagePoint p, Place3 place)
        {
            double a = -place.Rot * Math.PI / 180.0;
            double dx = p.X - place.X;
            double dy = p.Y - place.Y;
            double lx = dx * Math.Cos(a) - dy * Math.Sin(a);
            double ly = dx * Math.Sin(a) + dy * Math.Cos(a);
            return Math.Abs(lx) <= W2 && Math.Abs(ly) <= H2;
        }

        /// <summary>
        /// CardUnderPointer: the hovered card is drawn in front, so it wins where two overlap, and it is caught on its raised
        /// part too (§4.8); the others by their resting places, the right one over the left. A card under the open pile list,
        /// or under any other panel, is not hovered through it.
        /// </summary>
        // js: scenes/battle/hand.js:244
        private CardItem CardUnder(StagePoint p)
        {
            if (_ptrForeign) return null;
            if (_env.Geom.CoversPointer != null && _env.Geom.CoversPointer(p)) return null;
            int n = _cards.Count;
            if (_sel != null && Raised(_sel.Card, p, n)) return _sel.Card;
            if (_hovered != null && Raised(_hovered, p, n)) return _hovered;
            for (int i = n - 1; i >= 0; i--)
            {
                CardItem c = _cards[i];
                if (c.Mode == CardMode.Fan && InRect(p, Rest(i, n))) return c;
            }
            return null;
        }

        // js: scenes/battle/hand.js:248 (raised)
        private bool Raised(CardItem c, StagePoint p, int n)
        {
            int i = _cards.IndexOf(c);
            return i >= 0 && c.Mode == CardMode.Fan && (InRect(p, Rest(i, n)) || InRect(p, HoverPlace(i, n)));
        }

        // js: scenes/battle/hand.js:260
        private bool Busy() => _returning.Count > 0;

        /// <summary>
        /// battle_ui_ux_v2 §3.13 / §3.5 (review fix): the detail a right click or a long press opened stays up, over its card,
        /// until Esc, a click or another right click closes it, and while it is up the hand is locked — the cards lift on hover
        /// but none is taken. The selected card's own detail (I) is a reading aid of the selection and locks nothing.
        /// </summary>
        // js: scenes/battle/hand.js:267
        private bool DetailLocked() => _panels.DetailShown && !(_sel != null && _panels.DetailFor == _sel.Card.Id);

        // While a card is selected (a click or a digit), the others do not lift on hover (review fix: two raised cards made it
        // unclear which one Enter or a click on the enemy would play). Clicking another card, or its digit, still moves the
        // selection. An overlay (blocked) takes no press and no grab; the journal still lets the cards be hovered (§2.4): its
        // veil leaves the pointer to the hand outside its drawer (IsForeign), the settings' modal never does.
        // js: scenes/battle/hand.js:273
        private bool CanHover() => _interactable && (!_blocked || _hoverWhileBlocked) && _held == null && _down == null && _sel == null && !Busy() && !_destroyed;

        // js: scenes/battle/hand.js:274
        private bool CanGrab() => _interactable && !_blocked && _grabLock == null && _held == null && !Busy() && !_destroyed && !DetailLocked();

        /// <summary>A card was tried while the pile list blocks the hand: its answer (a refusal line), once per press or digit.</summary>
        // js: scenes/battle/hand.js:276
        private bool TriedBlocked(CardItem c)
        {
            if (c == null || !_blocked || _blockedTry == null || !_interactable || _held != null || Busy() || _destroyed) return false;
            _blockedTry();
            return true;
        }

        /// <summary>A card was tried while the grab lock holds: the lock's own answer (a refusal line), once per press.</summary>
        // js: scenes/battle/hand.js:282
        private bool TriedLocked()
        {
            if (_grabLock != null && _interactable && !_blocked && _held == null && !Busy() && !_destroyed) { _grabLock(); return true; }
            return false;
        }

        // js: scenes/battle/hand.js:287
        private void SetHovered(CardItem c)
        {
            if (c == _hovered) return;
            _hovered = c;
            // The detail belongs to the card it was opened on and stays with it while the hover moves (§3.13, DetailLocked).
            if (c != null)
            {
                float now = _env.Real.Now;
                if (now - _lastHoverSound >= HoverSoundGapMs)
                {
                    Play("hover");
                    _lastHoverSound = now;
                }
            }
            _opts.OnHover?.Invoke(c?.Face);
        }

        /// <summary>The upright box of a card as it stands now (CardPanels.Bounds).</summary>
        // js: scenes/battle/hand.js:299
        private static StageBox BoxOf(CardItem c) => Aabb(c.Pose.X, c.Pose.Y, c.Pose.Rot);

        /// <summary>UpdateHoverPanels: the hover line, the reach hint and the detail follow the hovered card; a term under the pointer shows its meaning.</summary>
        // js: scenes/battle/hand.js:302
        private void UpdatePanels()
        {
            // §3.4 I: the selected card's detail stands over it while it is selected.
            if (_sel != null && _panels.DetailShown && _panels.DetailFor == _sel.Card.Id)
            {
                _panels.HideHover();
                _panels.PlaceDetail(BoxOf(_sel.Card));
                return;
            }
            // The detail stands over its own card (where the hover line was); with it open, the hover panels are down.
            if (_panels.DetailShown)
            {
                CardItem dc = FindCard(_panels.DetailFor);
                _panels.HideHover();
                if (dc != null && _held == null) _panels.PlaceDetail(BoxOf(dc));
                else _panels.HideDetail();
                return;
            }
            // Review 21 (§3.4 「同じ結果と同じ判断材料」: a card raised by a click or a digit hid its hover line and reach panel): the
            // selected card shows the panels a hovered one does.
            CardItem c = _held != null ? null : (_hovered ?? _sel?.Card);
            if (c == null || c == _longRest)
            {
                _panels.HideHover();
                return;
            }
            StageBox box = BoxOf(c);
            _panels.ShowHover(c.Face, box, c.View != null && c.View.Lit);
            bool shown = false;
            if (_ptrClient.HasValue && ViewAlive(c) && c.View.Terms != null && c.View.Terms.Count > 0)
            {
                StagePoint pc = _ptrClient.Value;
                foreach (CardTerm t in c.View.Terms)
                {
                    if (t == null || !t.Rect) continue;
                    // t.getBoundingClientRect(), in stage px
                    StageBox r = _env.StageBoxOf(t.Rect);
                    if (pc.X >= r.X && pc.X <= r.Right && pc.Y >= r.Y && pc.Y <= r.Bottom)
                    {
                        _panels.ShowTerm(r, t.Mark);
                        shown = true;
                        break;
                    }
                }
            }
            if (!shown) _panels.HideTerm();
        }

        // ---- the zone (ZoneAt / ReceiverTakes / SetZoneHot) --------------------------------------------

        /// <summary>Every standing enemy's receiver, { unit, box } (one with a single enemy).</summary>
        // js: scenes/battle/hand.js:336
        private List<Recv> Receivers()
        {
            var list = new List<Recv>();
            if (_env.Geom.Receivers != null)
            {
                IReadOnlyList<ReceiverBox> rs = _env.Geom.Receivers();
                if (rs != null) foreach (ReceiverBox r in rs) list.Add(new Recv(r.Unit, r.Box));
                return list;
            }
            ScreenBox? one = _env.Geom.Receiver != null ? _env.Geom.Receiver(-1) : null;
            if (one.HasValue) list.Add(new Recv(-1, one.Value));
            return list;
        }

        // js: scenes/battle/hand.js:341 (receiverBox)
        private ScreenBox? ReceiverBoxOf(int unit)
        {
            List<Recv> list = Receivers();
            if (list.Count == 0) return null;
            FrameState st = (FrameState)_held ?? _sel;
            int baseUnit = st != null ? st.Base : DefaultUnit(null);
            if (unit >= 0)
                foreach (Recv r in list)
                    if (r.Unit == unit) return r.Box;
            foreach (Recv r in list)
                if (r.Unit == baseUnit) return r.Box;
            return list[0].Box;
        }

        // js: scenes/battle/hand.js:350
        private static bool InBox(ScreenBox r, StagePoint p) => p.X >= r.X && p.X < r.Right && p.Y >= r.Y && p.Y < r.Bottom;

        /// <summary>
        /// ZoneAt: a single-target card is taken by a receiver when half of the card lies on it or the pointer is inside it
        /// (ReceiverDrop.Takes, #568), measured where the pointer carries the card before the snap; a self card when the pointer
        /// is above the throw line (ThrowLineView.IsAbove). Returns the zone and the enemy whose receiver took the card (−1 for
        /// none / a self card); the one under the pointer wins.
        /// </summary>
        // js: scenes/battle/hand.js:357
        private ZoneHit ZoneAt(CardFace face, float cx, float cy, float rot, StagePoint p)
        {
            if (face.Aim == CardAim.Single)
            {
                List<Recv> list = Receivers();
                if (list.Count == 0) return new ZoneHit(DropZone.None, -1);
                StageBox b = Aabb(cx, cy, rot);
                var card = new ScreenBox(b.X, b.Y, b.W, b.H);
                foreach (Recv r in list)
                    if (InBox(r.Box, p)) return new ZoneHit(DropZone.Receiver, r.Unit);
                foreach (Recv r in list)
                    if (ReceiverDrop.Takes(card, r.Box, p.X, p.Y)) return new ZoneHit(DropZone.Receiver, r.Unit);
                return new ZoneHit(DropZone.None, -1);
            }
            return new ZoneHit(p.Y < L.Throw.Y ? DropZone.AboveThrowLine : DropZone.None, -1);
        }

        /// <summary>The framed enemy changed: the hooks, the tag, the lamp and the player's HP wick follow it.</summary>
        // js: scenes/battle/hand.js:370
        private void SetFramed(FrameState state, int unit)
        {
            if (state == null || state.Unit == unit) return;
            state.Unit = unit;
            CardItem c = state.Card;
            int u = FramedOf(state);
            _marks.SetTarget(u);
            SetLitFor(c, true, u);
            _opts.OnTarget?.Invoke(c.Face, u);
        }

        /// <summary>SetZoneHot (+ the design system's frame sounds: in, out, or the dull one over an enemy out of reach).</summary>
        // js: scenes/battle/hand.js:380
        private void SetHot(FrameState state, bool on, CardFace face)
        {
            if (state == null || state.Hot == on) return;
            state.Hot = on;
            _marks.SetHot(on);
            if (on) Play(face.Aim == CardAim.Single && !string.IsNullOrEmpty(face.ReachHint) ? "noReach" : "frameIn");
            else Play("frameOut");
        }

        // ---- hold (BeginHold … Release) -----------------------------------------------------------------

        /// <summary>The zone and the hooks, the lamp, and the player's pay preview (BeginHold's shared half).</summary>
        // js: scenes/battle/hand.js:390
        private void Lift(CardItem c, int baseUnit)
        {
            SetLitFor(c, true, baseUnit);
            string id = c.Id;
            _marks.Show(c.Face, unit => _opts.Preview != null ? _opts.Preview(id, unit) ?? "" : "", baseUnit);
            _opts.OnHoldStart?.Invoke(c.Face, c.Face.Aim == CardAim.Single ? (baseUnit >= 0 ? baseUnit : DefaultUnit(c.Face)) : -1);
        }

        // js: scenes/battle/hand.js:395
        private void Drop(CardItem c)
        {
            _marks.Hide();
            SetLitFor(c, false);
            _opts.OnHoldEnd?.Invoke(c.Face);
        }

        // js: scenes/battle/hand.js:401
        private void BeginHold(CardItem c, bool auto)
        {
            if (_held != null || _cards.IndexOf(c) < 0) return;
            if (_sel != null) EndSelection(false);
            SetHovered(null);
            _panels.HideAll();
            // The hover stops while a card is held: settle any card it left half lifted (BeginHold).
            foreach (CardItem o in _cards)
                if (o != c && o.Mode == CardMode.Fan) o.Hw = 0f;
            c.Mode = CardMode.Held;
            _held = new HoldState
            {
                Card = c, OffX = c.Pose.X - _ptr.X, OffY = c.Pose.Y - _ptr.Y, Hot = false, SnapW = 0f, Auto = auto,
                Zone = DropZone.None, Unit = -1, Base = BaseUnit(c.Face),
            };
            if (auto)
            {
                _held.OffX = 0f;
                _held.OffY = 0f;
            }
            c.Pose.A = L.HeldAlpha;
            c.Pose.S = 1f;
            AppendTo(c, _env.Layer.Held);
            Play("grab");
            // EffectId.CardGrab: the held card stands upright (out). §0.1 decision 7: its size and shadow stay.
            float tilt = c.Pose.Rot;
            float ms = _env.Fx.Ms(EffectId.CardGrab);
            HoldState mine = _held;
            if (ms > 0f) _env.Clock.Run(ms, Ease.Out, t => { if (_held == mine) c.Pose.Rot = tilt * (1f - t); }).Forget();
            else c.Pose.Rot = 0f;
            Lift(c, _held.Base);
        }

        /// <summary>OnDragMoved + UpdateSnap: under the pointer (the grabbed point kept), drawn toward the receiver while over it.</summary>
        // js: scenes/battle/hand.js:425
        private void UpdateHold(float dt)
        {
            HoldState held = _held;
            CardItem c = held.Card;
            float fx = _ptr.X + held.OffX;
            float fy = _ptr.Y + held.OffY;
            ZoneHit at = ZoneAt(c.Face, fx, fy, c.Pose.Rot, _ptr);
            DropZone zone = at.Zone;
            held.Zone = zone;
            SetFramed(held, at.Unit);
            SetHot(held, zone != DropZone.None, c.Face);
            if (_held != held) return;
            float snapMs = _env.Fx.Ms(EffectId.ReceiverSnap);
            float target = c.Face.Aim == CardAim.Single && held.Hot ? 1f : 0f;
            held.SnapW = snapMs > 0f ? MoveTowards(held.SnapW, target, dt / snapMs) : target;
            ScreenBox? r = ReceiverBoxOf(held.Unit);
            float pull = r.HasValue ? L.SnapPull * Ease.Smooth(held.SnapW) : 0f;
            c.Pose.X = r.HasValue ? Lerp(fx, r.Value.X + r.Value.Width / 2f, pull) : fx;
            c.Pose.Y = r.HasValue ? Lerp(fy, r.Value.Y + r.Value.Height / 2f, pull) : fy;
        }

        /// <summary>The hold's decorations go; the card stays where it was let go, for the player to fly or send home.</summary>
        // js: scenes/battle/hand.js:444
        private CardItem EndHold()
        {
            CardItem c = _held.Card;
            _held = null;
            c.Mode = CardMode.Free;
            Drop(c);
            return c;
        }

        /// <summary>Release: the source decides through the player (OnRelease → TryPlay); the hand shows the answer.</summary>
        // js: scenes/battle/hand.js:453
        private void ReleaseHeld(DropZone zone, int unit)
        {
            CardItem c = EndHold();
            HandOver(c, zone, unit);
        }

        // js: scenes/battle/hand.js:459
        private void HandOver(CardItem c, DropZone zone, int unit)
        {
            _handingOver = c;
            try
            {
                _opts.OnRelease?.Invoke(c.Id, zone, zone == DropZone.Receiver && unit >= 0 ? unit : -1);
            }
            finally
            {
                _handingOver = null;
            }
            // Nobody took the card (no player): it slides home.
            if (_cards.IndexOf(c) >= 0 && c.Mode == CardMode.Free && !_returning.Contains(c)) SlideHome(c, false, false).Forget();
        }

        /// <summary>ReturnToHand's motion: home over CardReturn (out), upright → its tilt, then the refusal shake (UiTween.Shake 8 px × 2).</summary>
        // js: scenes/battle/hand.js:469
        private Promise SlideHome(CardItem c, bool shake, bool sound)
        {
            if (sound)
            {
                if (shake)
                {
                    Play("refusal");
                    Play("refusal", 70f);
                }
                else Play("cancel");
            }
            _returning.Add(c);
            c.Mode = CardMode.Free;
            c.Hw = 0f;
            int my = ++c.Motion;
            AppendTo(c, _env.Layer.Held);
            int i = _cards.IndexOf(c);
            Place3 r = Rest(Math.Max(0, i), _cards.Count);
            return _env.Clock.Tween(c.Pose, new[] { ("x", r.X), ("y", r.Y), ("rot", r.Rot), ("s", 1f) }, _env.Fx.Ms(EffectId.CardReturn), Ease.Out)
                .Then((Func<Promise>)(() =>
                {
                    if (c.Motion != my) return null;
                    c.Pose.A = 1f;
                    SetLitFor(c, false);
                    float shakeMs = _env.Fx.Ms(EffectId.RefusalShake);
                    if (!shake || shakeMs <= 0f) return null;
                    return _env.Clock.Run(shakeMs, Ease.Linear, t =>
                    {
                        if (c.Motion == my) c.Shake = (float)Math.Sin(t * Math.PI * 2.0 * L.Shake.Cycles) * L.Shake.Px * (1f - t);
                    });
                }))
                .Then(() =>
                {
                    c.Shake = 0f;
                    _returning.Remove(c);
                    if (c.Motion != my || _cards.IndexOf(c) < 0) return;
                    // LayoutHand: every card home, the hover dropped.
                    AppendTo(c, _env.Layer.Hand);
                    c.Mode = CardMode.Fan;
                    c.Hw = 0f;
                    Render();
                });
        }

        /// <summary>
        /// §5.9 順 1 「カードのランプが 1.4 倍に膨らんで戻り、条件名 1 語がカードと一緒に消える」 (review 19): on the released card, the
        /// lamp swells and back over LAMP_SWELL.ms (out) and the trait's condition word stands over its trait box — a child of
        /// the card, so it shrinks and fades away with it.
        /// </summary>
        // js: scenes/battle/hand.js:504
        private void TraitSignal(CardItem c, string word)
        {
            RectTransform lamp = c.View != null ? c.View.Lamp : null;
            RectTransform box = c.View != null ? c.View.TraitBox : null;
            if (lamp)
            {
                UiNode ln = UiNode.Of(lamp);
                // lamp.style.scale = 1 + (1.4 − 1) · sin(πt), then ''
                _env.Clock.Run(LampSwellMs, Ease.Out, t => { if (ln) ln.PropScale = 1f + (LampSwellScale - 1f) * (float)Math.Sin(t * Math.PI); })
                    .Then(() => { if (ln) ln.PropScale = 1f; }).Forget();
            }
            if (box && !string.IsNullOrEmpty(word)) TraitWordTag(box, word);
        }

        /// <summary>
        /// h('span', { class: 'ocbg-battle-card-trait' }, word) appended to the trait box (hand.css): position absolute, left 0,
        /// bottom 100% + margin-bottom 6, padding 2 10, panel-solid, 2 px wick border, radius 4, 700 20/26 body ink, nowrap,
        /// glow 0 0 12 amber 45 %. An absolute child is placed from its containing block's padding box, so left 0 and bottom
        /// 100% are measured inside the trait box's own 1 px border (.ocb-trait); `box` is that border box.
        /// </summary>
        private static void TraitWordTag(RectTransform box, string word)
        {
            TextStyle style = TextStyle.FromCss(TraitWordTextCss);
            float tw = Trial.Kit.TextMeasure.Width(word, style);
            float w = tw + 2f * (TraitWordPadX + TraitWordBorder);
            float h = TraitWordLine + 2f * (TraitWordPadY + TraitWordBorder);
            Insets inner = Ocb.Rule(Ocb.TraitCss).Box.BorderWidths;
            BoxGraphic tag = BoxGraphic.Make(box, TraitWordBoxCss, inner.Left, inner.Top - TraitWordGap - h, w, h, "ocbg-battle-card-trait");
            Ocb.Absolute(tag); // out of the trait box's flex row
            Label.Make((RectTransform)tag.transform, word, style, TraitWordPadX + TraitWordBorder, TraitWordPadY + TraitWordBorder, tw, TraitWordLine, "word");
        }

        // ---- the keyboard / click path (battle_ui_ux_v2 §3.4) -------------------------------------------

        /// <summary>A card is lifted to its hover place with the same hooks / line and prediction a drag shows; it does not follow the pointer.</summary>
        // js: scenes/battle/hand.js:513
        private bool SelectCard(CardItem c, bool auto)
        {
            // §3.4 one key, one role: a digit selects (the selected card's own digit keeps it); Enter plays.
            if (_sel != null && _sel.Card == c)
            {
                if (!auto) _sel.Auto = false;
                return true;
            }
            if (_sel != null) EndSelection(false);
            _panels.HideAll();
            // `auto` (review 19): the scene picked it after an Enter play (§3.4 確定後は扇の同じ位置の札を自動で選択する), not the player.
            _sel = new SelState { Card = c, Hot = false, Unit = -1, Tabbed = false, TabAt = null, Base = BaseUnit(c.Face), Auto = auto };
            Play("grab");
            Lift(c, _sel.Base);
            return true;
        }

        // js: scenes/battle/hand.js:524
        private void EndSelection(bool sound)
        {
            if (_sel == null) return;
            CardItem c = _sel.Card;
            _sel = null;
            if (_panels.DetailFor == c.Id) _panels.HideDetail();
            Drop(c);
            if (sound) Play("cancel");
        }

        // js: scenes/battle/hand.js:532
        private static bool InRectBox(StageBox? b, StagePoint p) =>
            b.HasValue && p.X >= b.Value.X && p.X <= b.Value.X + b.Value.W && p.Y >= b.Value.Y && p.Y <= b.Value.Y + b.Value.H;

        /// <summary>
        /// The enemy whose omen badge or number panel stands under `p`, or null (review fix: they move with the enemy and read
        /// as part of it, so a click there on the click path, §3.4, is a click on that enemy — it dropped the selection). A
        /// fallen enemy's grey panel counts for nothing.
        /// </summary>
        // js: scenes/battle/hand.js:538
        private int? PartOfEnemyAt(StagePoint p)
        {
            BattleGeom geom = _env.Geom;
            foreach (Recv r in Receivers())
            {
                int u = r.Unit >= 0 ? r.Unit : -1;
                StageBox? badge = geom.OmenBox != null ? geom.OmenBox(u) : (StageBox?)null;
                bool shownBadge = badge.HasValue && (geom.OmenShown == null || geom.OmenShown(u));
                if (shownBadge && InRectBox(badge, p)) return r.Unit;
                if (geom.StatusBox != null)
                {
                    PanelBox? panel = geom.StatusBox(UnitSide.Enemy, u);
                    if (panel.HasValue && InRectBox(panel.Value.Box, p)) return r.Unit;
                }
            }
            return null;
        }

        /// <summary>Where a click would play the selected card: inside an enemy's receiver, hooks, omen badge or panel, or above the throw line.</summary>
        // js: scenes/battle/hand.js:549
        private ZoneHit SelectionZone(StagePoint p)
        {
            CardItem c = _sel.Card;
            if (c.Face.Aim == CardAim.Single)
            {
                foreach (Recv r in Receivers())
                {
                    HookBox? b = _marks.BoxOf(UnitSide.Enemy, r.Unit >= 0 ? r.Unit : -1);
                    if (InBox(r.Box, p) || InRectBox(b.HasValue ? b.Value.Box : (StageBox?)null, p)) return new ZoneHit(DropZone.Receiver, r.Unit);
                }
                int? part = PartOfEnemyAt(p);
                if (part.HasValue) return new ZoneHit(DropZone.Receiver, part.Value);
                return new ZoneHit(DropZone.None, -1);
            }
            return new ZoneHit(p.Y < L.Throw.Y ? DropZone.AboveThrowLine : DropZone.None, -1);
        }

        /// <summary>The detail on the card under the pointer (§7.4: right click, or a 400 ms press; `open`: never closes it).</summary>
        // js: scenes/battle/hand.js:564
        private void ToggleDetailOn(CardItem c, bool open = false)
        {
            if (c == null || c.Face.Detail == null)
            {
                _panels.HideDetail();
                return;
            }
            if (_sel != null && _sel.Card != c) EndSelection(false); // the detail locks the hand: no selection stands under it
            if (_hovered != c) SetHovered(c);
            if (open && _panels.DetailShown && _panels.DetailFor == c.Id) return;
            _panels.ToggleDetail(c.Face, BoxOf(c), DetailExtraOf(c));
        }

        // ---- pointer --------------------------------------------------------------------------------------

        /// <summary>cards.some((c) => c.wrap.contains(target)): the pointer's target is a card (layer hand / held).</summary>
        // js: scenes/battle/hand.js:579
        private static bool IsMine(BattlePointer e) => e.Layer == BattleLayer.Hand || e.Layer == BattleLayer.Held;

        /// <summary>
        /// Another module's panel, a button or an overlay (the pile list, a drawer's own panel): the hand leaves it alone. `p`
        /// (stage px): an enemy's number panel (its chips take the pointer for their tooltips) is the enemy's while a card is
        /// selected (PartOfEnemyAt), and the journal's veil outside its drawer lets the hand be read (§2.4).
        /// </summary>
        // js: scenes/battle/hand.js:587
        private bool IsForeign(BattlePointer e, StagePoint? p)
        {
            if (e == null || e.Outside) return true;
            if (e.Layer == null || IsMine(e)) return false;
            BattleLayer layer = e.Layer.Value;
            if (p.HasValue && _sel != null && layer == BattleLayer.Panels && PartOfEnemyAt(p.Value).HasValue) return false;
            if (p.HasValue && layer == BattleLayer.Over && UnderJournalVeil(e, p.Value)) return false;
            return layer == BattleLayer.Hud || layer == BattleLayer.Over || layer == BattleLayer.Panels;
        }

        /// <summary>
        /// battle_ui_ux_v2 §2.4 (review fix: no card could be hovered with the journal open): 「開いている間はカードをホバー
        /// できるがプレイできません」. The journal's host covers the screen (its veil closes it on a click); over the veil, outside
        /// the drawer (env.Geom.JournalCovers), the cards lift and show their panels, and nothing is taken.
        /// </summary>
        // js: scenes/battle/hand.js:599
        private bool UnderJournalVeil(BattlePointer e, StagePoint p)
        {
            if (!e.OnJournal) return false;
            return !(_env.Geom.JournalCovers != null && _env.Geom.JournalCovers(p));
        }

        /// <summary>window pointerdown: a press (the hand takes the primary button only; a foreign target is left alone).</summary>
        // js: scenes/battle/hand.js:604
        public void OnPointerDown(BattlePointer p)
        {
            if (_destroyed || p == null) return;
            _ptr = new StagePoint(p.X, p.Y);
            _ptrClient = _ptr;
            if (p.Button != 0 || _held != null) return;
            bool foreign = IsForeign(p, _ptr);
            // §3.13: a click closes the locked detail (on the scene's own ground it does nothing more).
            if (DetailLocked() && !p.Outside)
            {
                _panels.HideDetail();
                if (!foreign)
                {
                    _swallowUp = true;
                    return;
                }
            }
            if (foreign)
            {
                _ptrForeign = true;
                return;
            }
            if (TriedBlocked(CardUnder(_ptr))) return;
            if (_grabLock != null && CardUnder(_ptr) != null && TriedLocked()) return;
            CardItem c = CanGrab() ? CardUnder(_ptr) : null;
            if (c != null)
            {
                int token = ++_pressToken;
                _down = new PressState { Card = c, X = _ptr.X, Y = _ptr.Y, Token = token, Long = false };
                // env.root.setPointerCapture(e.pointerId): the scene's pump routes this pointer to the hand wherever it goes.
                // §3.13: a press held 400 ms without moving opens the detail, and the release then plays nothing. Review fix: a
                // press that moves 8 px after that still becomes a drag (the detail closes) — a player who pauses before dragging
                // gets the drag, and a drag whose moves came late (a busy frame) is not lost to the timer.
                _env.Real.Wait(L.LongPressMs).Then(() =>
                {
                    if (_destroyed || _down == null || _down.Token != token || _held != null) return;
                    _down.Long = true;
                    // §3.2 / §3.13 (review 16): the press is spent, the card goes back to its rest place over
                    // LONG_PRESS_RETURN_MS, and then the detail opens (it stayed raised under the detail). It stays down until
                    // the pointer leaves it.
                    CardItem pressed = _down.Card;
                    _longRest = pressed;
                    _panels.HideHover();
                    _env.Clock.Wait(LongPressReturnMs).Then(() =>
                    {
                        if (_destroyed || _pressToken != token || _held != null || _cards.IndexOf(pressed) < 0 || _longRest != pressed) return;
                        ToggleDetailOn(pressed, true);
                    }).Forget();
                }).Forget();
                return;
            }
            _bgDown = _ptr;
        }

        /// <summary>The press turns into a drag: the card is held as if the pointer had moved off it from the press point.</summary>
        // js: scenes/battle/hand.js:644
        private bool DragFromPress(PressState d)
        {
            if (d.Long)
            {
                _panels.HideDetail();
                _longRest = null;
            }
            if (!CanGrab()) return false;
            StagePoint now = _ptr;
            _ptr = new StagePoint(d.X, d.Y);
            BeginHold(d.Card, false);
            _ptr = now;
            return _held != null;
        }

        /// <summary>
        /// Review 16 (after a window blur a held card stayed glued to the pointer and played on the next click): the window lost
        /// focus or the page was hidden, so the release will not come here — a held card slides home unplayed and a press under
        /// way is dropped, as Esc does. A mouse move with no button down (the release was lost) does the same.
        /// </summary>
        // js: scenes/battle/hand.js:658
        private void DropLostHold()
        {
            if (_destroyed) return;
            if (_down != null)
            {
                _down = null;
                _pressToken++;
            }
            _bgDown = null;
            if (_held != null && !_held.Auto) Cancel();
        }

        /// <summary>window blur, or the page hidden (visibilitychange): a held card goes home unplayed, a press under way drops.</summary>
        // js: scenes/battle/hand.js:664 (onBlur) and :665 (onVisibility: document.hidden)
        public void OnBlur()
        {
            DropLostHold();
        }

        /// <summary>window pointermove: the pointer moved (a mouse move with no button down while holding drops the hold).</summary>
        // js: scenes/battle/hand.js:666
        public void OnPointerMove(BattlePointer p)
        {
            if (_destroyed || p == null) return;
            if ((_held != null && !_held.Auto) || _down != null)
            {
                if (p.IsMouse && p.Buttons == 0)
                {
                    DropLostHold();
                    return;
                }
            }
            _ptr = new StagePoint(p.X, p.Y);
            _ptrClient = _ptr;
            _ptrForeign = _held == null && _down == null && IsForeign(p, _ptr);
            if (_down != null && _held == null)
            {
                // §3.2: 8 px from the press the card leaves the fan (stage px, not scaled by speed).
                if (Hypot(_ptr.X - _down.X, _ptr.Y - _down.Y) >= L.DragThreshold)
                {
                    PressState d = _down;
                    _down = null;
                    DragFromPress(d);
                }
            }
        }

        /// <summary>window pointerup: a release (drop, click on a card, click on a frame).</summary>
        // js: scenes/battle/hand.js:683
        public void OnPointerUp(BattlePointer p)
        {
            if (_destroyed || p == null) return;
            _ptr = new StagePoint(p.X, p.Y);
            _ptrClient = _ptr;
            // env.root.releasePointerCapture(e.pointerId): nothing to release in Unity.
            if (_swallowUp)
            {
                _swallowUp = false;
                _bgDown = null;
                return;
            }
            // A press that left its card by 8 px with no move reported in between (a busy frame): it was a drag all along.
            if (_down != null && _held == null && p.Button == 0 && Hypot(_ptr.X - _down.X, _ptr.Y - _down.Y) >= L.DragThreshold && _cards.IndexOf(_down.Card) >= 0)
            {
                PressState d = _down;
                _down = null;
                DragFromPress(d);
            }
            if (_held != null && !_held.Auto)
            {
                CardItem c = _held.Card;
                // OnDragEnded: the zone where the pointer lets go, measured with the card where the pointer carries it.
                ZoneHit at = ZoneAt(c.Face, _ptr.X + _held.OffX, _ptr.Y + _held.OffY, c.Pose.Rot, _ptr);
                ReleaseHeld(at.Zone, at.Unit);
                return;
            }
            if (_down != null)
            {
                CardItem c = _down.Card;
                bool isLong = _down.Long;
                _down = null;
                if (isLong) return; // the long press opened the detail; letting go leaves it up (§3.13)
                if (p.Button != 0 || !CanGrab() || _cards.IndexOf(c) < 0) return;
                // A click on a card selects it; on the selected card again, lets it go back (§3.4). Review 19 (after an Enter play
                // the card the scene picked was dropped by the player's click on it, and the next click on the enemy did
                // nothing): a click on a card the scene picked takes it as the player's own selection — the next click on the
                // enemy plays it.
                if (_sel != null && _sel.Card == c && _sel.Auto)
                {
                    _sel.Auto = false;
                    Play("grab");
                }
                else if (_sel != null && _sel.Card == c) EndSelection(true);
                else SelectCard(c, false);
                return;
            }
            if (_bgDown.HasValue)
            {
                StagePoint bg = _bgDown.Value;
                float moved = Hypot(_ptr.X - bg.X, _ptr.Y - bg.Y);
                _bgDown = null;
                if (moved >= L.DragThreshold || p.Button != 0) return;
                _panels.HideDetail();
                if (_sel != null && _interactable && !Busy())
                {
                    // §3.4: a click on the enemy's frame (or above the line for a self card) plays; anywhere else drops the selection.
                    ZoneHit at = SelectionZone(_ptr);
                    if (at.Zone != DropZone.None) ConfirmOn(at.Unit);
                    else EndSelection(true);
                }
            }
        }

        /// <summary>window pointercancel.</summary>
        // js: scenes/battle/hand.js:729
        public void OnPointerCancel()
        {
            _down = null;
            _bgDown = null;
            if (_held != null && !_held.Auto) Cancel();
        }

        /// <summary>root pointerleave: the pointer left the stage (no hover).</summary>
        // js: scenes/battle/hand.js:734
        public void OnLeave()
        {
            if (_held != null) return;
            _ptr = Far;
            _ptrClient = null;
        }

        /// <summary>§7.4: a right click on the hovered card toggles its detail; elsewhere it closes it. The browser menu never opens in battle.</summary>
        // js: scenes/battle/hand.js:740
        public void OnContext(BattlePointer p)
        {
            if (_destroyed || _held != null || p == null) return;
            _ptr = new StagePoint(p.X, p.Y);
            CardItem c = _interactable && !Busy() ? CardUnder(_ptr) : null;
            if (c != null) ToggleDetailOn(c);
            else _panels.HideDetail();
        }

        // ---- the frame loop -------------------------------------------------------------------------------

        // js: scenes/battle/hand.js:758
        private void OnFrame(float dtScene, float dtReal)
        {
            if (_destroyed) return;
            // While an overlay holds the scene clock still (the journal), the hover runs on real time (§2.4).
            float dt = _env.Clock.Paused ? dtReal : dtScene;
            if (CanHover()) SetHovered(CardUnder(_ptr));
            else if (_down == null) SetHovered(null);
            // EffectId.CardHover: the weight moves linearly over the effect's length (smoothstep on the place).
            float hoverMs = _env.Fx.Ms(EffectId.CardHover);
            float step = hoverMs > 0f ? dt / hoverMs : 1f;
            // A long-pressed card stays at rest until the pointer has left it (§3.13).
            if (_longRest != null && _hovered != _longRest && !(_down != null && _down.Card == _longRest)) _longRest = null;
            List<CardItem> list = _cards;
            for (int i = 0; i < list.Count; i++)
            {
                CardItem c = list[i];
                if (c.Mode != CardMode.Fan) continue;
                if (c == _longRest)
                {
                    // §3.2 詳細を見る: back to its rest place over LONG_PRESS_RETURN_MS.
                    c.Hw = MoveTowards(c.Hw, 0f, LongPressReturnMs > 0f ? dt / LongPressReturnMs : 1f);
                    continue;
                }
                bool up = c == _hovered || (_sel != null && _sel.Card == c) || (_down != null && _down.Card == c)
                    || (_panels.DetailShown && _panels.DetailFor == c.Id);
                c.Hw = MoveTowards(c.Hw, up ? 1f : 0f, step);
            }
            if (_held != null && !_held.Auto) UpdateHold(dt);
            if (_sel != null)
            {
                SelState sel = _sel;
                ZoneHit at = _interactable && !_ptrForeign ? SelectionZone(_ptr) : new ZoneHit(DropZone.None, -1);
                // A Tab focus holds until the pointer moves (§3.4: Tab moves which frame Enter plays on).
                if (sel.Tabbed && sel.TabAt.HasValue && Hypot(_ptr.X - sel.TabAt.Value.X, _ptr.Y - sel.TabAt.Value.Y) >= L.DragThreshold) sel.Tabbed = false;
                if (!sel.Tabbed && at.Zone != DropZone.None) SetFramed(sel, at.Unit);
                if (_sel == sel) SetHot(sel, sel.Tabbed || at.Zone != DropZone.None, sel.Card.Face);
            }
            Render();
            _marks.Update();
            UpdatePanels();
            // The guide steps aside while a hovered card's panels or the detail stand in its band (review fix: it showed
            // through them in torn pieces), and while a card is selected (review 16: the selected card stands at the hover
            // place, top y 760, across the guide's band, and the guide's drag wording is not the click path's). It stays up
            // under a held card, which goes where the pointer takes it.
            _panels.StepGuide(dt, _panels.PanelsUp || _sel != null);
            _panels.StepRefusal(dtReal, RaisedOverRefusal());
            // root classList ocbg-battle--grab / --grabbing (the cursor in the JS; the hud's tip reads Grabbing)
            _env.Grab = _hovered != null && _interactable && _held == null;
            _env.Grabbing = _held != null;
            SweepGhosts();
        }

        /// <summary>
        /// A card raised out of the fan (hovered, pressed or selected) stands over the refusal line (review 16: after a
        /// refusal, the line covered the name and the values of the next card hovered, for up to 3.5 s).
        /// </summary>
        // js: scenes/battle/hand.js:805
        private bool RaisedOverRefusal()
        {
            StageBox? rb = _panels.RefusalBox();
            if (!rb.HasValue) return false;
            StageBox r = rb.Value;
            // The card whose own press brought the line up (a locked press) keeps it: the reason is about that card.
            if (_refusalOwner != null && (_refusalOwner.Hw <= 0.3f || _refusalOwner.Mode != CardMode.Fan || _cards.IndexOf(_refusalOwner) < 0)) _refusalOwner = null;
            foreach (CardItem c in _cards)
            {
                if (c == _refusalOwner || c.Mode != CardMode.Fan || c.Hw <= 0.3f) continue;
                StageBox b = BoxOf(c);
                if (b.X < r.X + r.W && b.X + b.W > r.X && b.Y < r.Y + r.H && b.Y + b.H > r.Y) return true;
            }
            return false;
        }

        /// <summary>Plays the selection on its own zone; on enemy `unit` when a click named one, else where the source sends it.</summary>
        // js: scenes/battle/hand.js:818
        private bool ConfirmOn(int unit)
        {
            if (_sel == null || !_interactable || Busy()) return false;
            CardItem c = _sel.Card;
            _sel = null;
            Drop(c);
            c.Mode = CardMode.Free;
            HandOver(c, DepictionText.RequiredZone(c.Face.Aim), unit);
            return true;
        }

        // ---- the dim (RefreshPlayableLook / CardView.FadeDimmed) ----------------------------------------

        // js: scenes/battle/hand.js:829
        private void SetDim(CardItem c, bool dim)
        {
            if (dim == c.Dim) return;
            c.Dim = dim;
            int token = ++c.DimToken;
            float from = c.DimA;
            float to = dim ? _dim : 0f;
            float ms = _env.Fx.Ms(EffectId.UnpayableDim);
            if (ms <= 0f)
            {
                c.DimA = to;
                if (ViewAlive(c)) c.View.SetDim(to);
                return;
            }
            _env.Clock.Run(ms, Ease.Linear, t =>
            {
                if (token != c.DimToken) return;
                c.DimA = Lerp(from, to, t);
                if (ViewAlive(c)) c.View.SetDim(c.DimA);
            }).Forget();
        }

        // ---- api: frame ---------------------------------------------------------------------------------

        // js: scenes/battle/hand.js:846
        public bool SameHand(IReadOnlyList<CardFace> faces)
        {
            int n = faces?.Count ?? 0;
            if (n != _cards.Count) return false;
            for (int i = 0; i < n; i++)
                if (_cards[i].Id != faces[i].Id) return false;
            return true;
        }

        /// <summary>BuildHand: new card elements at their fan places, undimmed.</summary>
        // js: scenes/battle/hand.js:853
        public void Build(IReadOnlyList<CardFace> faces)
        {
            Clear();
            var made = new List<CardItem>();
            if (faces != null)
                foreach (CardFace f in faces)
                    if (f != null) made.Add(Make(f));
            _cards = made;
            Layout();
        }

        /// <summary>Same ids: each face is bound again in place (cost, lamp, values, hover line).</summary>
        // js: scenes/battle/hand.js:859
        public void Bind(IReadOnlyList<CardFace> faces)
        {
            if (faces == null) return;
            for (int i = 0; i < faces.Count; i++)
            {
                if (i >= _cards.Count) return;
                CardItem c = _cards[i];
                if (c == null || faces[i] == null) continue;
                c.Face = faces[i];
                Draw(c);
            }
        }

        /// <summary>LayoutHand: every card to its place at once, the hover dropped.</summary>
        // js: scenes/battle/hand.js:868
        public void Layout()
        {
            SetHovered(null);
            foreach (CardItem c in _cards)
            {
                if ((_held != null && _held.Card == c) || (_sel != null && _sel.Card == c) || _returning.Contains(c)) continue;
                c.Mode = CardMode.Fan;
                c.Motion++;
                c.Hw = 0f;
                c.Shake = 0f;
                if (c.Wrap && c.Wrap.parent != _env.Layer.Hand) AppendTo(c, _env.Layer.Hand);
            }
            Render();
        }

        // js: scenes/battle/hand.js:880
        public void Clear()
        {
            if (_held != null) EndHold();
            if (_sel != null) EndSelection(false);
            _down = null;
            foreach (CardItem c in _cards) Ui.Remove(c.Wrap);
            _cards = new List<CardItem>();
            _returning.Clear();
            _hovered = null;
            _longRest = null;
            _panels.HideAll();
        }

        // js: scenes/battle/hand.js:891
        public int Count() => _cards.Count;

        // js: scenes/battle/hand.js:892
        public IReadOnlyList<CardFace> Faces()
        {
            var o = new CardFace[_cards.Count];
            for (int i = 0; i < o.Length; i++) o[i] = _cards[i].Face;
            return o;
        }

        // ---- api: state set by the player ---------------------------------------------------------------

        /// <summary>SetHandInteractable: off, no hover and no grab; a held card is let go and the refusal line goes.</summary>
        // js: scenes/battle/hand.js:896
        public void SetInteractable(bool on)
        {
            _interactable = on;
            if (!_interactable)
            {
                if (_held != null && !_held.Auto) LetGo();
                if (_sel != null) EndSelection(false);
                _down = null;
                _bgDown = null;
                SetHovered(null);
                _panels.HideAll();
                _panels.HideRefusal();
            }
        }

        /// <summary>RefreshPlayableLook: the cards the source would refuse now darken (UnpayableDim), and the guide line prints.</summary>
        // js: scenes/battle/hand.js:909
        public void RefreshPlayable(Func<string, bool> dimOf, string guide)
        {
            _dimOf = dimOf ?? (_ => false);
            foreach (CardItem c in _cards)
            {
                if ((_held != null && _held.Card == c) || c == _handingOver) continue;
                SetDim(c, _dimOf(c.Id));
            }
            _panels.SetGuide(guide ?? "");
        }

        /// <summary>
        /// Review 21 (battle_ui_ux_v2 §3.5 / §5.10): the player holds 集中 / 強化 / 威圧 — `marks` = { focus, empower, intimidate };
        /// the cards they act on carry the mark beside their values.
        /// </summary>
        // js: scenes/battle/hand.js:921
        public void SetStatusMarks(StatusMarks marks)
        {
            string key = KeyOf(marks);
            string was = KeyOf(_statusMarks);
            _statusMarks = marks;
            if (key != was) foreach (CardItem c in _cards) ApplyMods(c);
        }

        // [marks.focus, marks.empower, marks.intimidate].map(Number).join('')
        private static string KeyOf(StatusMarks m) =>
            m == null ? "" : (m.Focus ? "1" : "0") + (m.Empower ? "1" : "0") + (m.Intimidate ? "1" : "0");

        // js: scenes/battle/hand.js:927
        public bool IsHolding() => _held != null;

        // js: scenes/battle/hand.js:928
        public CardFace HeldFace() => _held != null ? _held.Card.Face : _sel?.Card.Face;

        // js: scenes/battle/hand.js:929
        public CardFace HoveredFace() => _hovered?.Face;

        /// <summary>Dev / tests: what decides whether a press takes a card now (the playtest logs it when a drag misses).</summary>
        // js: scenes/battle/hand.js:931
        public HandDebugState DebugState()
        {
            var modes = new List<string>();
            foreach (CardItem c in _cards) modes.Add(c.Id + ":" + ModeName(c.Mode));
            return new HandDebugState
            {
                Interactable = _interactable,
                Blocked = _blocked,
                GrabLock = _grabLock != null,
                Held = _held != null,
                Sel = _sel != null,
                Down = _down != null,
                Returning = _returning.Count,
                PtrForeign = _ptrForeign,
                Hovered = _hovered?.Id,
                Modes = modes,
                Ptr = new StagePoint(JsRound(_ptr.X), JsRound(_ptr.Y)),
            };
        }

        private static string ModeName(CardMode m) => m == CardMode.Held ? "held" : m == CardMode.Free ? "free" : "fan";

        /// <summary>Addition: a card is selected by a click or a digit (not held).</summary>
        // js: scenes/battle/hand.js:936
        public bool HasSelection() => _sel != null;

        /// <summary>Addition: the right-click detail is open.</summary>
        // js: scenes/battle/hand.js:938
        public bool DetailOpen() => _panels.DetailShown;

        /// <summary>Addition (several enemies): the enemy the held / selected card is framed over now, else its default target; −1 for none.</summary>
        // js: scenes/battle/hand.js:940
        public int FramedUnit()
        {
            FrameState st = (FrameState)_held ?? _sel;
            if (st == null || st.Card.Face.Aim != CardAim.Single) return -1;
            return FramedOf(st);
        }

        // ---- api: beats ---------------------------------------------------------------------------------

        /// <summary>
        /// FlyDrawn: cards [first..] fly in from `from` (the draw pile) one stagger apart (EffectPlan.DrawCardStartMs), out,
        /// growing from the pile's size; a draw that ran the pile out calls OnRebuild at RebuildStartMs.
        /// </summary>
        // js: scenes/battle/hand.js:951
        public Promise FlyDrawn(int first, int rebuildAfter, FlyDrawnOpts o)
        {
            o = o ?? new FlyDrawnOpts();
            StagePoint from = o.From ?? new StagePoint(64f, 990f);
            int start = Math.Max(0, first);
            var flying = new List<CardItem>();
            for (int i = start; i < _cards.Count; i++) flying.Add(_cards[i]);
            foreach (CardItem c in flying)
            {
                c.Mode = CardMode.Free;
                c.Hw = 0f;
                c.Motion++;
                c.Pose.X = from.X;
                c.Pose.Y = from.Y;
                c.Pose.Rot = 0f;
                c.Pose.S = PileScale;
                c.Pose.A = 0f;
            }
            Render();
            float ms = _env.Fx.Ms(EffectId.CardDraw);
            var lands = new List<Promise>();
            for (int k = 0; k < flying.Count; k++)
            {
                CardItem c = flying[k];
                int my = c.Motion;
                lands.Add(_env.Clock.Wait(EffectPlan.DrawCardStartMs(k, rebuildAfter, _env.Fx.Switches)).Then((Func<Promise>)(() =>
                {
                    Play("draw");
                    int i = _cards.IndexOf(c);
                    if (i < 0 || c.Motion != my) return null;
                    Place3 r = Rest(i, _cards.Count);
                    return _env.Clock.Tween(c.Pose, new[] { ("x", r.X), ("y", r.Y), ("rot", r.Rot), ("s", 1f), ("a", 1f) }, ms, Ease.Out);
                })).Then(() =>
                {
                    if (c.Motion == my && _cards.IndexOf(c) >= 0 && c.Mode == CardMode.Free) c.Mode = CardMode.Fan;
                }));
            }
            if (rebuildAfter >= 0 && rebuildAfter < flying.Count && o.OnRebuild != null)
            {
                Action onRebuild = o.OnRebuild;
                _env.Clock.Wait(EffectPlan.RebuildStartMs(rebuildAfter, _env.Fx.Switches)).Then(() => onRebuild()).Forget();
            }
            return Promise.All(lands).Then(() => { });
        }

        /// <summary>FlyDiscarded: the whole hand to the discard pile one stagger apart (HandDiscard, in), then cleared.</summary>
        // js: scenes/battle/hand.js:974
        public Promise DiscardAll(StagePoint to)
        {
            float ms = _env.Fx.Ms(EffectId.HandDiscard);
            float stagger = _env.Fx.Stagger(EffectId.HandDiscard);
            if (_sel != null) EndSelection(false);
            SetHovered(null);
            var list = new List<CardItem>(_cards);
            var flights = new List<Promise>();
            for (int i = 0; i < list.Count; i++)
            {
                CardItem c = list[i];
                flights.Add(_env.Clock.Wait(stagger * i).Then((Func<Promise>)(() =>
                {
                    c.Mode = CardMode.Free;
                    c.Motion++;
                    _returning.Remove(c);
                    Play("discard");
                    return _env.Clock.Tween(c.Pose, new[] { ("x", to.X), ("y", to.Y), ("rot", 0f), ("s", PileScale), ("a", 0f) }, ms, Ease.In);
                })));
            }
            return Promise.All(flights).Then(() => Clear());
        }

        /// <summary>
        /// PlayAccepted's CardRelease: the card leaves the hand list at once and flies to `to` (a single-target card: the
        /// receiver's centre) or 140 px up (a self card), shrinking to half and fading, ease in. The rest of the fan stays where
        /// it is until Settle().
        /// </summary>
        // js: scenes/battle/hand.js:995
        public ReleaseResult Release(string cardId, DepictionEvent ev, StagePoint? to, ReleaseOpts o)
        {
            CardItem c = FindCard(cardId);
            if (c == null)
            {
                StagePoint at = to ?? new StagePoint(960f, 700f);
                return new ReleaseResult { Done = Promise.Resolved(), VanishedAt = () => at };
            }
            if (_sel != null && _sel.Card.Id == cardId) EndSelection(false);
            if (_held != null && _held.Card.Id == cardId) EndHold();
            int i = _cards.IndexOf(c);
            if (i >= 0) _cards.RemoveAt(i);
            if (c == _hovered) SetHovered(null);
            _returning.Remove(c);
            // The others keep their places (Unity sets them only in LayoutHand / SettleHand).
            foreach (CardItem other in _cards)
            {
                if (other.Mode != CardMode.Fan) continue;
                other.Mode = CardMode.Free;
                other.Hw = 0f;
            }
            LeavingSet(c);
            c.Mode = CardMode.Free;
            c.Motion++;
            c.Shake = 0f;
            AppendTo(c, _env.Layer.Held);
            Play(ReleaseSoundOf(c.Face.Kind));
            // battle_ui_ux_v2 §5.9 順 1 (review 19): a trait the card fires — its lamp swells 1.4× and back, and its condition
            // word stands on the card and leaves with it.
            if (o != null && !string.IsNullOrEmpty(o.TraitWord)) TraitSignal(c, o.TraitWord);
            c.Pose.A = 1f;
            // §5.11 順 1 / 順 4 (review 19; Unity tosses every self card up and fades it): a stance card flies to the end of the
            // stance list and shrinks there to its ghost's size; it stands in the slot (`Keep`) until its ghost leaves for the
            // exile pile (the player's released → ghostToPile). The flow holds CardRelease's wait only; the card flies on beside it.
            StageBox? slot = c.Face.Kind == CardKind.Stance && _env.Geom.StanceSlot != null ? _env.Geom.StanceSlot(UnitSide.Player) : null;
            if (slot.HasValue)
            {
                StageBox s = slot.Value;
                _env.Clock.Tween(c.Pose, new[] { ("x", s.X + s.W / 2f), ("y", s.Y + s.H / 2f), ("rot", 0f), ("s", StanceCardScale) }, L.StanceFlyMs, Ease.Out).Forget();
                return new ReleaseResult
                {
                    Done = _env.Clock.Wait(_env.Fx.Ms(EffectId.CardRelease)),
                    VanishedAt = () => new StagePoint(c.Pose.X, c.Pose.Y),
                    Face = c.Face,
                    Keep = true,
                };
            }
            StagePoint target = ev != null && ev.Aim == CardAim.Single && to.HasValue ? to.Value : new StagePoint(c.Pose.X, c.Pose.Y - L.ReleaseRise);
            Promise done = _env.Clock.Tween(c.Pose, new[] { ("x", target.X), ("y", target.Y), ("s", L.ReleaseScale), ("a", 0f) }, _env.Fx.Ms(EffectId.CardRelease), Ease.In);
            return new ReleaseResult { Done = done, VanishedAt = () => new StagePoint(c.Pose.X, c.Pose.Y), Face = c.Face };
        }

        /// <summary>
        /// The played card's face for its ghost on the way to its pile (review fix: Unity's FlyGhostTo flies a plain pale
        /// sheet, which read as a missing texture): a fresh card face for `face`, full size (216×304, its top left at (0, 0));
        /// the field shrinks it to the ghost's 76×104. Returned as its root RectTransform, unseen until the field re-parents it
        /// (the JS element is detached); one the field does not take within GhostKeepMs real ms is dropped.
        /// </summary>
        // js: scenes/battle/hand.js:1032
        public object GhostCard(CardFace face)
        {
            if (face == null || _destroyed) return null;
            try
            {
                if (!_ghostHold)
                {
                    _ghostHold = Ui.Div(_env.Layer.Float, "ocbg-battle-ghost-hold", 0f, 0f, 0f, 0f);
                    UiNode hold = UiNode.Of(_ghostHold);
                    hold.Opacity = 0f;
                    hold.PointerEvents = PointerEvents.None;
                }
                CardFaceView view = HandCard.BuildCard(face, _ghostHold);
                if (view == null || !view.Root) return null;
                _ghosts.Add(new KeyValuePair<RectTransform, float>(view.Root, _env.Real.Now));
                return view.Root;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Ghost faces still in the hold after GhostKeepMs: the field never took them (its effect was off).</summary>
        private void SweepGhosts()
        {
            if (_ghosts.Count == 0) return;
            float now = _env.Real.Now;
            for (int i = _ghosts.Count - 1; i >= 0; i--)
            {
                RectTransform g = _ghosts[i].Key;
                if (!g || g.parent != _ghostHold)
                {
                    _ghosts.RemoveAt(i);
                    continue;
                }
                if (now - _ghosts[i].Value < GhostKeepMs) continue;
                Ui.Remove(g);
                _ghosts.RemoveAt(i);
            }
        }

        /// <summary>Released: the card's element goes.</summary>
        // js: scenes/battle/hand.js:1037
        public void DropCard(string cardId)
        {
            CardItem c = LeavingGet(cardId);
            if (c == null) return;
            Ui.Remove(c.Wrap);
            _leaving.Remove(c);
        }

        /// <summary>SettleHand: the rest slide from where they are to their new fan places (out).</summary>
        // js: scenes/battle/hand.js:1042
        public Promise Settle(float ms)
        {
            int n = _cards.Count;
            var all = new List<Promise>();
            for (int i = 0; i < n; i++)
            {
                CardItem c = _cards[i];
                if (_returning.Contains(c))
                {
                    all.Add(Promise.Resolved());
                    continue;
                }
                c.Mode = CardMode.Free;
                int my = ++c.Motion;
                Place3 r = Rest(i, n);
                all.Add(_env.Clock.Tween(c.Pose, new[] { ("x", r.X), ("y", r.Y), ("rot", r.Rot), ("s", 1f), ("a", 1f) }, ms, Ease.Out).Then(() =>
                {
                    if (c.Motion == my && _cards.IndexOf(c) >= 0 && c.Mode == CardMode.Free)
                    {
                        c.Mode = CardMode.Fan;
                        c.Hw = 0f;
                    }
                }));
            }
            return Promise.All(all).Then(() => { });
        }

        /// <summary>ReturnToHand: CardReturn home, then the RefusalShake; 'cancel', or 'refusal' twice when it shakes.</summary>
        // js: scenes/battle/hand.js:1055
        public Promise ReturnToHand(string cardId, bool shake)
        {
            CardItem c = FindCard(cardId);
            if (c == null) return Promise.Resolved();
            if (_held != null && _held.Card == c) EndHold();
            if (_sel != null && _sel.Card == c) EndSelection(false);
            return SlideHome(c, shake, true);
        }

        /// <summary>
        /// LetGo (Halt): a held card goes back to its place at once, unplayed, and its decorations go. A press that has not yet
        /// become a drag (and a press on the background) is dropped too (review fix: a card pressed just before J could still be
        /// dragged and played under the journal).
        /// </summary>
        // js: scenes/battle/hand.js:1067
        public void LetGo()
        {
            if (_sel != null) EndSelection(false);
            if (_down != null)
            {
                _down = null;
                _pressToken++;
            }
            _bgDown = null;
            if (_held == null) return;
            CardItem c = EndHold();
            c.Pose.A = 1f;
            c.Mode = CardMode.Fan;
            c.Hw = 0f;
            AppendTo(c, _env.Layer.Hand);
            Layout();
        }

        /// <summary>
        /// An overlay is up (true) or gone (false): while up, no press or grab reaches the hand, and no hover unless `hover`
        /// (the journal alone: its cards can be read but not played, battle_ui_ux_v2 §2.4).
        /// </summary>
        // js: scenes/battle/hand.js:1083
        public void SetBlocked(bool on, bool hover, Action onTry)
        {
            _blocked = on;
            _hoverWhileBlocked = on && hover;
            // Review 16 (the pile list): a press on a card or its digit while blocked calls onTry (the reason on the refusal
            // line) instead of doing nothing.
            _blockedTry = _blocked ? onTry : null;
            if (_blocked)
            {
                if (_down != null)
                {
                    _down = null;
                    _pressToken++;
                }
                _bgDown = null;
                SetHovered(null);
            }
        }

        /// <summary>
        /// battle_ui_ux_v2 §3.8 (俊敏's choice): `onTry` set, no card can be taken (hover still lifts it); a press on a card or
        /// its digit calls onTry instead. null lifts the lock. A selection or a press under way is dropped.
        /// </summary>
        // js: scenes/battle/hand.js:1099
        public void SetGrabLock(Action onTry)
        {
            _grabLock = onTry;
            if (_grabLock != null)
            {
                if (_sel != null) EndSelection(false);
                if (_down != null)
                {
                    _down = null;
                    _pressToken++;
                }
            }
        }

        /// <summary>The enemies whose frame shows the held / selected card's prediction tag (HandMarks.TaggedUnits).</summary>
        // js: scenes/battle/hand.js:1107
        public IReadOnlyList<int> TaggedUnits() => _marks.Shown ? _marks.TaggedUnits() : Array.Empty<int>();

        /// <summary>RefusalLineView: the reason on its own line above the hand for 3.5 s (real time); the guide steps aside.</summary>
        // js: scenes/battle/hand.js:1109
        public void ShowRefusal(string text)
        {
            _refusalOwner = (_down != null ? _down.Card : null) ?? _hovered ?? _sel?.Card;
            _panels.ShowRefusal(text);
        }

        // js: scenes/battle/hand.js:1113
        public void HideRefusal()
        {
            _panels.HideRefusal();
        }

        /// <summary>The battle is over (review fix): the hand fades under the result card; 0 ms sets it at once.</summary>
        // js: scenes/battle/hand.js:1162
        public Promise FadeAll(float ms)
        {
            UiNode hand = _env.Layer.Node(BattleLayer.Hand);
            // parseFloat(layer.hand.style.opacity || '1') || 1
            float from = hand.Opacity;
            if (!(from > 0f) && !(from < 0f)) from = 1f;
            if (!(ms > 0f))
            {
                hand.Opacity = EndHandAlpha;
                return Promise.Resolved();
            }
            return _env.Clock.Run(ms, Ease.Out, t => { if (hand) hand.Opacity = Lerp(from, EndHandAlpha, t); });
        }

        // ---- api: input paths (battle_ui_ux_v2 §3.4) ----------------------------------------------------

        /// <summary>
        /// Key 1–9: selects card `index` (§3.4: another digit moves the selection; Enter plays it). `o.Auto`: the scene's own
        /// pick after an Enter play (a click on it then keeps it, review 19).
        /// </summary>
        // js: scenes/battle/hand.js:1120
        public bool Select(int index, SelectOpts o = null)
        {
            CardItem c = index >= 0 && index < _cards.Count ? _cards[index] : null;
            if (c != null && c.Mode == CardMode.Fan && TriedBlocked(c)) return false;
            if (c != null && c.Mode == CardMode.Fan && TriedLocked()) return false;
            if (c == null || !CanGrab() || c.Mode != CardMode.Fan) return false;
            return SelectCard(c, o != null && o.Auto);
        }

        /// <summary>
        /// Enter: plays the selected card on its own zone (DepictionText.RequiredZone), on the frame the screen shows as focused
        /// — the one Tab or the pointer last framed, which stays framed (its light, the HP wick, the floor's N) once the pointer
        /// moves off (review fix: Enter then went to CardFace.TargetUnit while another enemy stood framed).
        /// </summary>
        // js: scenes/battle/hand.js:1132
        public bool Confirm()
        {
            if (_sel == null) return false;
            return ConfirmOn(_sel.Card.Face.Aim == CardAim.Single ? FramedOf(_sel) : -1);
        }

        /// <summary>The fan position of the selected card (−1 for none): §3.4, the card then at that place is selected next.</summary>
        // js: scenes/battle/hand.js:1137
        public int SelectedIndex() => _sel != null ? _cards.IndexOf(_sel.Card) : -1;

        /// <summary>
        /// Tab (§3.4, two or more enemies): the selected single-target card's focus moves to the next standing enemy's frame,
        /// left to right (`dir` −1 goes back); Enter then plays it there. Returns whether it moved.
        /// </summary>
        // js: scenes/battle/hand.js:1142
        public bool CycleTarget(int dir)
        {
            if (_sel == null || _held != null || _sel.Card.Face.Aim != CardAim.Single) return false;
            List<Recv> list = Receivers();
            // a stable sort by the receiver's middle x (Array.prototype.sort is stable)
            var keyed = new List<KeyValuePair<int, Recv>>();
            for (int k = 0; k < list.Count; k++) keyed.Add(new KeyValuePair<int, Recv>(k, list[k]));
            keyed.Sort((a, b) =>
            {
                float ca = a.Value.Box.X + a.Value.Box.Width / 2f, cb = b.Value.Box.X + b.Value.Box.Width / 2f;
                int byX = ca.CompareTo(cb);
                return byX != 0 ? byX : a.Key.CompareTo(b.Key);
            });
            if (keyed.Count < 2) return false;
            int now = FramedOf(_sel);
            int i = 0;
            for (int k = 0; k < keyed.Count; k++)
                if (keyed[k].Value.Unit == now) { i = k; break; }
            int next = keyed[(i + (dir < 0 ? keyed.Count - 1 : 1)) % keyed.Count].Value.Unit;
            SelState sel = _sel;
            sel.Tabbed = true;
            sel.TabAt = _ptr;
            SetFramed(sel, next);
            SetHot(sel, true, sel.Card.Face);
            return true;
        }

        /// <summary>I (§3.4 / §3.13): the selected card's detail opens over it, or closes.</summary>
        // js: scenes/battle/hand.js:1156
        public bool ToggleSelectedDetail()
        {
            if (_sel == null || _sel.Card.Face.Detail == null) return false;
            _panels.ToggleDetail(_sel.Card.Face, BoxOf(_sel.Card), DetailExtraOf(_sel.Card));
            return true;
        }

        /// <summary>Esc: a held card slides home unplayed, or the selection drops, or the detail closes.</summary>
        // js: scenes/battle/hand.js:1168
        public bool Cancel()
        {
            if (_held != null && !_held.Auto)
            {
                CardItem c = EndHold();
                SlideHome(c, false, true).Forget();
                return true;
            }
            if (_sel != null)
            {
                EndSelection(true);
                return true;
            }
            if (_panels.DetailShown)
            {
                _panels.HideDetail();
                return true;
            }
            return false;
        }

        /// <summary>AutoDrag: the suggested card is held, carried 450 ms (inout) onto its zone (enemy `unit`'s receiver), made hot and released there.</summary>
        // js: scenes/battle/hand.js:1179
        public Promise AutoDrag(string cardId, DropZone zone, int unit = -1)
        {
            CardItem c = FindCard(cardId);
            if (c == null || _held != null || !_interactable || Busy()) return Promise.Resolved();
            BeginHold(c, true);
            var from = new StagePoint(c.Pose.X, c.Pose.Y);
            StagePoint to;
            if (c.Face.Aim == CardAim.Single)
            {
                int named = unit >= 0 ? unit : DefaultUnit(c.Face);
                if (_held != null) SetFramed(_held, named);
                ScreenBox? r = ReceiverBoxOf(named);
                to = r.HasValue ? new StagePoint(r.Value.X + r.Value.Width / 2f, r.Value.Y + r.Value.Height / 2f) : from;
            }
            else to = new StagePoint(from.X, L.Throw.Y - (from.Y - L.Throw.Y) * 0.35f);
            HoldState mine = _held;
            return _env.Clock.Run(L.AutoDragMs, Ease.InOut, t =>
            {
                if (_held != mine) return;
                c.Pose.X = Lerp(from.X, to.X, t);
                c.Pose.Y = Lerp(from.Y, to.Y, t);
            }).Then(() =>
            {
                if (_held != mine || mine == null) return;
                SetHot(_held, true, c.Face);
                if (_held != mine) return;
                ReleaseHeld(zone, zone == DropZone.Receiver ? _held.Unit : -1);
            });
        }

        // js: scenes/battle/hand.js:1202
        public void Destroy()
        {
            _destroyed = true;
            _offFrame?.Invoke();
            // the window / document / root listeners: the scene's pump stops routing to a destroyed hand
            _env.Grab = false;
            _env.Grabbing = false;
            _marks.Destroy();
            _panels.Destroy();
            foreach (CardItem c in _cards) Ui.Remove(c.Wrap);
            foreach (CardItem c in _leaving) Ui.Remove(c.Wrap);
            _cards = new List<CardItem>();
            _leaving.Clear();
            if (_ghostHold) Ui.Remove(_ghostHold);
            _ghostHold = null;
            _ghosts.Clear();
        }

        // ---- geometry and lookups -----------------------------------------------------------------------

        /// <summary>env.geom.handCard(cardId): the centre of that card now (in the fan or leaving it), or null.</summary>
        // js: scenes/battle/hand.js:1223
        private StagePoint? HandCardAt(string cardId)
        {
            CardItem c = FindCard(cardId) ?? LeavingGet(cardId);
            return c != null ? new StagePoint(c.Pose.X, c.Pose.Y) : (StagePoint?)null;
        }

        private CardItem FindCard(string id)
        {
            foreach (CardItem c in _cards)
                if (c.Id == id) return c;
            return null;
        }

        // leaving.get(id)
        private CardItem LeavingGet(string id)
        {
            foreach (CardItem c in _leaving)
                if (c.Id == id) return c;
            return null;
        }

        // leaving.set(c.id, c): a Map keeps the first insertion's place for a key set again
        private void LeavingSet(CardItem c)
        {
            for (int i = 0; i < _leaving.Count; i++)
            {
                if (_leaving[i].Id != c.Id) continue;
                _leaving[i] = c;
                return;
            }
            _leaving.Add(c);
        }
    }
}
