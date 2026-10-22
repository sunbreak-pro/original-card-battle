// The contracts between the battle's player (player.js → BattlePlayer) and its three view modules: the hand
// (hand.js), the field (field.js) and the hud (hud.js) — docs/BATTLE_SCENE.md §6a-§6c, plus every addition the player
// and the scene call (battle-flow §4.5). The modules implement these on the Unity side; the player and the flow tests
// code against them.
//
// Conventions (docs/BATTLE_SCENE.md §5):
// - A JS method that returns a Promise returns a Trial.Promise here (never a Task / Awaitable); it runs on env.Clock with
//   the nominal ms it is handed, and `ms = 0` (an effect switched off) puts the end state at once and resolves.
// - After the scene is destroyed, pending promises never resolve (the clock is stopped without resolving them).
// - A unit argument of −1 stands for the JS `undefined` (or null): the player, or the frame's focus / leading enemy, or
//   "none". It never names an enemy: the modules key their enemies by unit ≥ 0 (UnitFrame.Unit, else the frame's index,
//   hud.js / field.js enemiesOf), so a lookup of −1 falls back exactly where the JS lookup of undefined does.
// - Stage px, y down (StagePoint / StageBox, BattleEnv.cs). Missing data never throws: a null frame field draws nothing.
// - The modules never read ctx.source and never call each other: geometry goes through env.Geom, input up through the
//   callbacks in their options.
// - Each JS factory createX(env, opts) → api becomes a class with `static X Create(<view env>, XOpts o)` on the Unity side;
//   the api's destroy() is Destroy().
using System;
using System.Collections.Generic;
using Depiction;

namespace Trial.Battle
{
    // ---- shared vocabulary ------------------------------------------------------------------------

    /// <summary>
    /// The colour roles handed to floatText / burst / flash ('ink' | 'ink2' | 'omen' | 'guard' | 'warm' | 'heal' | 'whiff' |
    /// 'white', docs/BATTLE_SCENE.md §4.3). The field maps them to tokens: omen → --phosphor / --violet (enemy side), guard →
    /// --guard, warm → --amber (player side only; on an enemy target --ink), heal → --hp-text, whiff → --miss, ink2 →
    /// --ink-muted, white → the ink white of a hit flash.
    /// </summary>
    public enum FxRole
    {
        Ink,
        Ink2,
        Omen,
        Guard,
        Warm,
        Heal,
        Whiff,
        White,
    }

    /// <summary>hud.banner's side: 'player' (あなたの番) | 'enemy' (敵の番).</summary>
    public enum BannerSide
    {
        Player,
        Enemy,
    }

    /// <summary>field.ghost's kind: 'discard' | 'exile' (the played card to its pile) | 'sheet' (the discard pile sliding onto the draw pile).</summary>
    public enum GhostKind
    {
        Discard,
        Exile,
        Sheet,
    }

    /// <summary>floatText's o.anchor: 'left' / 'right' puts that edge of the tag on at.x (it grows away from it); none = the middle.</summary>
    public enum FloatAnchor
    {
        None,
        Left,
        Right,
    }

    /// <summary>
    /// A pointer event the scene routes to a module (the JS modules listen on window / the root; Unity polls the Input
    /// System once a frame and calls them in the browser's order, battle-flow §3.1). What the DOM's `e.target` told is
    /// carried as the layer of the topmost raycast hit and a few flags.
    /// </summary>
    public sealed class BattlePointer
    {
        /// <summary>env.toStage(e).x: the pointer in stage px.</summary>
        public float X;
        /// <summary>env.toStage(e).y.</summary>
        public float Y;
        /// <summary>e.clientX: the pointer in screen px (the hand keeps it beside the stage point).</summary>
        public float ScreenX;
        /// <summary>e.clientY.</summary>
        public float ScreenY;
        /// <summary>e.button: the button this event is about — 0 primary, 1 middle, 2 secondary.</summary>
        public int Button;
        /// <summary>e.buttons: the buttons held now (1 primary, 2 secondary, 4 middle); 0 on a move = a lost release.</summary>
        public int Buttons;
        /// <summary>e.pointerType === 'mouse'.</summary>
        public bool IsMouse = true;
        /// <summary>e.pointerId.</summary>
        public int PointerId;
        /// <summary>e.target is outside the scene's root (an app overlay over the battle).</summary>
        public bool Outside;
        /// <summary>The layer e.target lies in; null for the scene root itself (nothing interactive under the pointer).</summary>
        public BattleLayer? Layer;
        /// <summary>e.target lies in the end-turn plate (.ocbg-battle-endturn: it counts as the arena while the flow runs).</summary>
        public bool OnEndTurn;
        /// <summary>e.target lies in an interactive piece (.ocbg-battle-hit: a button, a chip, a mark).</summary>
        public bool OnControl;
        /// <summary>e.target lies in the journal's host (.ocbg-battle-journal; outside its drawer the hand may still hover, §2.4).</summary>
        public bool OnJournal;
    }

    // ---- 6a. the hand -----------------------------------------------------------------------------

    /// <summary>createHand(env, opts): the callbacks the hand raises (the scene binds them to the player made after it).</summary>
    // js: scenes/battle/BattleScene.js:173 (the opts) and docs/BATTLE_SCENE.md §6a
    public sealed class HandOpts
    {
        /// <summary>onRelease(cardId, zone, unit): a held card was let go on `zone` (over enemy `unit`, −1 for none).</summary>
        public Action<string, DropZone, int> OnRelease;
        /// <summary>onHover(face | null): the hovered face changed (the player lights the floor reach).</summary>
        public Action<CardFace> OnHover;
        /// <summary>onHoldStart(face, unit): a card was picked up / selected, framed on enemy `unit` (−1 for none).</summary>
        public Action<CardFace, int> OnHoldStart;
        /// <summary>onHoldEnd(face): the hold ended (release, cancel, letGo) — before onRelease.</summary>
        public Action<CardFace> OnHoldEnd;
        /// <summary>onTarget(face, unit): the held / selected single-target card is framed on another enemy.</summary>
        public Action<CardFace, int> OnTarget;
        /// <summary>preview(cardId, unit) → string: the one predicted value against enemy `unit` ('' for a refused card).</summary>
        public Func<string, int, string> Preview;
        /// <summary>restingUnit() → the enemy a Tab at rest chose (−1 for none): a selection starts framed on it.</summary>
        public Func<int> RestingUnit;
    }

    /// <summary>hand.setStatusMarks(marks): the player's 集中 / 強化 / 威圧 (review 21) — their marks beside the card values.</summary>
    // js: scenes/battle/player.js:697 (statusMarksOf)
    public sealed class StatusMarks
    {
        public bool Focus;
        public bool Empower;
        public bool Intimidate;
    }

    /// <summary>hand.flyDrawn's o: { from, onRebuild }.</summary>
    public sealed class FlyDrawnOpts
    {
        /// <summary>o.from: where the cards fly in from (the draw pile's middle); null = (64, 990).</summary>
        public StagePoint? From;
        /// <summary>o.onRebuild(): called at EffectPlan.RebuildStartMs when the draw ran the pile out (rebuildAfter in range).</summary>
        public Action OnRebuild;
    }

    /// <summary>hand.release's o: { traitWord }.</summary>
    public sealed class ReleaseOpts
    {
        /// <summary>o.traitWord: a trait the card fires — its lamp swells 1.4× and this condition word leaves with the card ('' for none).</summary>
        public string TraitWord = "";
    }

    /// <summary>hand.release's answer: { done, vanishedAt, face, keep }.</summary>
    public sealed class ReleaseResult
    {
        /// <summary>done: the CardRelease flight (a stance card: the CardRelease wait while it flies on to the stance slot).</summary>
        public Promise Done;
        /// <summary>vanishedAt(): where the card stands now (its ghost leaves from there).</summary>
        public Func<StagePoint> VanishedAt;
        /// <summary>face: the released card's face (null when the card was not in the hand).</summary>
        public CardFace Face;
        /// <summary>keep: a stance card stands at the stance slot until its ghost leaves for the exile (the player then drops it).</summary>
        public bool Keep;
    }

    /// <summary>hand.select's o: { auto }.</summary>
    public sealed class SelectOpts
    {
        /// <summary>o.auto: the scene's own pick after an Enter play (a click on it then keeps it as the player's selection, review 19).</summary>
        public bool Auto;
    }

    /// <summary>hand.debugState() (dev / tests): what decides whether a press takes a card now.</summary>
    // js: scenes/battle/hand.js:931
    public sealed class HandDebugState
    {
        public bool Interactable;
        public bool Blocked;
        public bool GrabLock;
        public bool Held;
        public bool Sel;
        public bool Down;
        /// <summary>returning.size: cards sliding home.</summary>
        public int Returning;
        public bool PtrForeign;
        /// <summary>hovered card's id, or null.</summary>
        public string Hovered;
        /// <summary>modes: "id:mode" per card ('fan' | 'free' | …).</summary>
        public IReadOnlyList<string> Modes;
        /// <summary>ptr: the pointer, rounded to whole px.</summary>
        public StagePoint Ptr;
    }

    /// <summary>
    /// hand.js (docs/BATTLE_SCENE.md §6a): the cards in hand — fan, hover, hold and release, drop zones, the target hooks
    /// and prediction tag, the guide and refusal lines, the detail. Unity: CardView, CardPanels, ReceiverView,
    /// ThrowLineView, TargetMarkView, RefusalLineView and the hover / drag / fan / guide parts of DepictionPlayer. Fills
    /// env.Geom.HandCard. Plays its own sounds (hover, grab, frameIn, frameOut, noReach, cancel, refusal ×2,
    /// releaseAttack/Defense/Skill/Stance, draw per card, discard per card).
    /// </summary>
    public interface IHand
    {
        // ---- frame ----

        /// <summary>sameHand(faces): the same ids in the same order.</summary>
        // js: scenes/battle/hand.js:846
        bool SameHand(IReadOnlyList<CardFace> faces);

        /// <summary>build(faces) = BuildHand: clear, then new cards at their fan places (HandSpec.Rest), under the dim the hand wears now.</summary>
        // js: scenes/battle/hand.js:853
        void Build(IReadOnlyList<CardFace> faces);

        /// <summary>bind(faces): same ids — each face bound again in place (cost, lamp, values, hover line).</summary>
        // js: scenes/battle/hand.js:859
        void Bind(IReadOnlyList<CardFace> faces);

        /// <summary>layout() = LayoutHand: every card not held, selected or returning to its place at once, the hover dropped.</summary>
        // js: scenes/battle/hand.js:868
        void Layout();

        /// <summary>clear(): every card gone, the hold / selection / press ended, the panels hidden.</summary>
        // js: scenes/battle/hand.js:880
        void Clear();

        /// <summary>count(): the cards in the fan.</summary>
        // js: scenes/battle/hand.js:891
        int Count();

        /// <summary>faces(): the fan's faces, left to right.</summary>
        // js: scenes/battle/hand.js:892
        IReadOnlyList<CardFace> Faces();

        // ---- state set by the player ----

        /// <summary>setInteractable(on) = SetHandInteractable: off — no hover, no grab; a held card is let go, the selection, the press and the refusal line go.</summary>
        // js: scenes/battle/hand.js:896
        void SetInteractable(bool on);

        /// <summary>
        /// refreshPlayable(dimOf, guide) = RefreshPlayableLook: cards with dimOf(id) darken (UnpayableDim, env.Fx.Ms), the
        /// held card and the one being played excepted; `guide` prints on the guide line ('' quiet).
        /// </summary>
        // js: scenes/battle/hand.js:909
        void RefreshPlayable(Func<string, bool> dimOf, string guide);

        /// <summary>setStatusMarks(marks) (review 21): the cards the player's 集中 / 強化 / 威圧 act on carry their marks; null clears.</summary>
        // js: scenes/battle/hand.js:921
        void SetStatusMarks(StatusMarks marks);

        /// <summary>isHolding(): a card is dragged.</summary>
        // js: scenes/battle/hand.js:927
        bool IsHolding();

        /// <summary>heldFace(): the held card's face, else the selected card's, else null.</summary>
        // js: scenes/battle/hand.js:928
        CardFace HeldFace();

        /// <summary>hoveredFace(): the hovered card's face, or null.</summary>
        // js: scenes/battle/hand.js:929
        CardFace HoveredFace();

        /// <summary>debugState() (dev / tests).</summary>
        // js: scenes/battle/hand.js:931
        HandDebugState DebugState();

        /// <summary>hasSelection() (addition): a card is selected by a click or a digit (not held).</summary>
        // js: scenes/battle/hand.js:936
        bool HasSelection();

        /// <summary>detailOpen() (addition): the right-click / long-press detail is open.</summary>
        // js: scenes/battle/hand.js:938
        bool DetailOpen();

        /// <summary>framedUnit() (addition, several enemies): the enemy the held / selected single-target card is framed over now, else its default target; −1 for none.</summary>
        // js: scenes/battle/hand.js:940
        int FramedUnit();

        /// <summary>taggedUnits(): the enemies whose frame shows the held / selected card's prediction tag (the hud lifts their badges).</summary>
        // js: scenes/battle/hand.js:1107
        IReadOnlyList<int> TaggedUnits();

        // ---- beats ----

        /// <summary>
        /// flyDrawn(first, rebuildAfter, o) = FlyDrawn: cards [first..] fly in from o.From one stagger apart
        /// (EffectPlan.DrawCardStartMs, CardDraw ms, out, growing from the pile's size), 'draw' per card; a draw that ran the
        /// pile out calls o.OnRebuild at EffectPlan.RebuildStartMs. rebuildAfter Cue.Unchanged (−1) = no rebuild.
        /// </summary>
        // js: scenes/battle/hand.js:951
        Promise FlyDrawn(int first, int rebuildAfter, FlyDrawnOpts o);

        /// <summary>discardAll(to) = FlyDiscarded: the whole hand to `to` one HandDiscard stagger apart (in), 'discard' per card, then cleared.</summary>
        // js: scenes/battle/hand.js:974
        Promise DiscardAll(StagePoint to);

        /// <summary>
        /// release(cardId, ev, to, o) = PlayAccepted's CardRelease: the card leaves the hand list at once and flies to `to` (a
        /// single-target card: the receiver's centre) or L.ReleaseRise up (a self card), shrinking to L.ReleaseScale and fading,
        /// ease in; release sound by face.Kind. A stance card flies to geom.StanceSlot(Player) over L.StanceFlyMs (out) and
        /// stays (Keep). The rest of the fan stays where it is until Settle.
        /// </summary>
        // js: scenes/battle/hand.js:995
        ReleaseResult Release(string cardId, DepictionEvent ev, StagePoint? to, ReleaseOpts o);

        /// <summary>ghostCard(face) (addition): a fresh card view of `face` for the played card's ghost (GhostOpts.Card), or null.</summary>
        // js: scenes/battle/hand.js:1032
        object GhostCard(CardFace face);

        /// <summary>dropCard(cardId) = Released: the released card's view goes.</summary>
        // js: scenes/battle/hand.js:1037
        void DropCard(string cardId);

        /// <summary>settle(ms) = SettleHand: the rest slide from where they are to their new fan places (out).</summary>
        // js: scenes/battle/hand.js:1042
        Promise Settle(float ms);

        /// <summary>returnToHand(cardId, shake) = ReturnToHand: CardReturn home, then the RefusalShake when `shake`; 'cancel', or 'refusal' twice when it shakes.</summary>
        // js: scenes/battle/hand.js:1055
        Promise ReturnToHand(string cardId, bool shake);

        /// <summary>letGo() = Halt / an overlay opening: a held card back to its place at once, unplayed; the selection and any press under way dropped.</summary>
        // js: scenes/battle/hand.js:1067
        void LetGo();

        /// <summary>
        /// setBlocked(on, hover, onTry): an overlay is up — no press or grab reaches the hand, no hover unless `hover` (the
        /// journal or the pile list: the cards lift on hover, on real time while the scene clock holds); a press on a card
        /// or its digit calls onTry (null: nothing).
        /// </summary>
        // js: scenes/battle/hand.js:1083
        void SetBlocked(bool on, bool hover, Action onTry);

        /// <summary>setGrabLock(onTry) (俊敏's choice): set — no card is taken, a press or a digit calls onTry, hover still lifts; null lifts the lock.</summary>
        // js: scenes/battle/hand.js:1099
        void SetGrabLock(Action onTry);

        /// <summary>showRefusal(text) = RefusalLineView: the reason on its own line above the hand for 3.5 s (real time, counted while it shows); the guide steps aside.</summary>
        // js: scenes/battle/hand.js:1109
        void ShowRefusal(string text);

        /// <summary>hideRefusal().</summary>
        // js: scenes/battle/hand.js:1113
        void HideRefusal();

        /// <summary>fadeAll(ms): the battle is over — the hand fades under the result card; 0 sets it at once.</summary>
        // js: scenes/battle/hand.js:1162
        Promise FadeAll(float ms);

        // ---- input paths (battle_ui_ux_v2 §3.4) ----

        /// <summary>select(index, o): key 1–9 / a click — card `index` is selected (lifted, its decorations shown); false when it cannot be.</summary>
        // js: scenes/battle/hand.js:1120
        bool Select(int index, SelectOpts o = null);

        /// <summary>confirm(): Enter — plays the selection on its own zone, on the frame the screen shows as focused; false without one.</summary>
        // js: scenes/battle/hand.js:1132
        bool Confirm();

        /// <summary>selectedIndex(): the selected card's fan place, −1 for none.</summary>
        // js: scenes/battle/hand.js:1137
        int SelectedIndex();

        /// <summary>cycleTarget(dir): Tab — the selected single-target card's focus to the next (−1: previous) standing enemy's frame; whether it moved.</summary>
        // js: scenes/battle/hand.js:1142
        bool CycleTarget(int dir);

        /// <summary>toggleSelectedDetail(): I — the selected card's detail opens over it, or closes; false without a selection or a detail.</summary>
        // js: scenes/battle/hand.js:1156
        bool ToggleSelectedDetail();

        /// <summary>cancel(): Esc — a held card slides home unplayed, or the selection drops, or the detail closes; whether anything did.</summary>
        // js: scenes/battle/hand.js:1168
        bool Cancel();

        /// <summary>autoDrag(cardId, zone, unit) = AutoDrag: held, carried L.AutoDragMs (inout) onto its zone (enemy `unit`'s receiver), made hot and released through the same path.</summary>
        // js: scenes/battle/hand.js:1179
        Promise AutoDrag(string cardId, DropZone zone, int unit = -1);

        // ---- raw input routed by the scene (the JS listens on window / the root) ----

        /// <summary>window pointerdown: a press (the hand takes the primary button only; a foreign target is left alone).</summary>
        // js: scenes/battle/hand.js:604
        void OnPointerDown(BattlePointer p);

        /// <summary>window pointermove: the pointer moved (a mouse move with no button down while holding drops the hold).</summary>
        // js: scenes/battle/hand.js:666
        void OnPointerMove(BattlePointer p);

        /// <summary>window pointerup: a release (drop, click on a card, click on a frame).</summary>
        // js: scenes/battle/hand.js:683
        void OnPointerUp(BattlePointer p);

        /// <summary>window pointercancel.</summary>
        // js: scenes/battle/hand.js:729
        void OnPointerCancel();

        /// <summary>root contextmenu (Windows browsers fire it on the right button's release): the hovered card's detail toggles.</summary>
        // js: scenes/battle/hand.js:740
        void OnContext(BattlePointer p);

        /// <summary>root pointerleave: the pointer left the stage (no hover).</summary>
        // js: scenes/battle/hand.js:734
        void OnLeave();

        /// <summary>window blur, or the page hidden (visibilitychange): a held card goes home unplayed, a press under way drops.</summary>
        // js: scenes/battle/hand.js:664
        void OnBlur();

        /// <summary>destroy().</summary>
        // js: scenes/battle/hand.js:1202
        void Destroy();
    }

    // ---- 6b. the field ----------------------------------------------------------------------------

    /// <summary>createField(env, opts).</summary>
    // js: scenes/battle/BattleScene.js:144 and scenes/battle/field.js:112
    public sealed class FieldOpts
    {
        /// <summary>opts.layer: the exploration's layer (BackdropSpec.For); 0 = env.LayerNo.</summary>
        public int Layer;
        /// <summary>opts.nodeKind.</summary>
        public BattleNodeKind NodeKind;
        /// <summary>opts.onFreeStepPick(dir): a click on one of 俊敏's marks (−1 / 0 / +1).</summary>
        public Action<int> OnFreeStepPick;
        /// <summary>opts.onFreeStepFocus(dir): the pointer over a mark.</summary>
        public Action<int> OnFreeStepFocus;
    }

    /// <summary>俊敏's open ways, the source's CanTakeFreeStep(−1) / CanTakeFreeStep(+1) (JS `{ [-1]: bool, [1]: bool }`).</summary>
    public readonly struct FreeStepWays
    {
        /// <summary>ways[-1]: one cell back is open.</summary>
        public readonly bool Back;
        /// <summary>ways[1]: one cell forward is open.</summary>
        public readonly bool Forward;

        public FreeStepWays(bool back, bool forward)
        {
            Back = back;
            Forward = forward;
        }

        /// <summary>ways[dir]: −1 Back, +1 Forward, anything else false (JS undefined).</summary>
        public bool this[int dir] => dir < 0 ? (dir == -1 && Back) : (dir == 1 && Forward);
    }

    /// <summary>field.strike's o: { byPlayer, system, intensity, ms, unit, source }.</summary>
    public sealed class StrikeOpts
    {
        /// <summary>byPlayer: the player's blow (ink) or an enemy's (violet).</summary>
        public bool ByPlayer;
        /// <summary>system: the blow's shape (Slash arc / Thrust line / Sweep band / Strike ripple).</summary>
        public StrikeSystem System;
        /// <summary>intensity: Cue.Intensity (1..4).</summary>
        public int Intensity;
        /// <summary>ms: StrikeShape's length.</summary>
        public float Ms;
        /// <summary>unit: the struck enemy (−1: the player or the focus enemy).</summary>
        public int Unit = -1;
        /// <summary>source: the striking enemy's unit (−1: the player, or the enemy acting now).</summary>
        public int Source = -1;
    }

    /// <summary>field.mark's o: { fx, intensity, ms, crestLight, unit }.</summary>
    public sealed class MarkOpts
    {
        /// <summary>fx: Cue.Fx — 盾 ring, 型 crest at the feet, 気 sigil; 息 and blows draw nothing.</summary>
        public FxSystem? Fx;
        /// <summary>intensity: Cue.Intensity.</summary>
        public int Intensity;
        /// <summary>ms: GrammarMark's length.</summary>
        public float Ms;
        /// <summary>crestLight: the crest lights the body too (StanceGlow.CrestCarriesLight; JS `o.crestLight !== false`, so true by default).</summary>
        public bool CrestLight = true;
        /// <summary>unit: the enemy (−1: the player or the focus enemy).</summary>
        public int Unit = -1;
    }

    /// <summary>Where a float stands once placed (floatText's o.onPlaced(span)).</summary>
    public sealed class FloatSpan
    {
        /// <summary>x0, x1: its span across.</summary>
        public float X0;
        public float X1;
        /// <summary>y: its start (middle).</summary>
        public float Y;
        /// <summary>rise: how far it rises.</summary>
        public float Rise;
        /// <summary>yNow(): its middle y now (the HP number beside a "Guard −n" tag rides with it).</summary>
        public Func<float> YNow;
    }

    /// <summary>field.floatText's o (fx.js floatText / placeFloat).</summary>
    public sealed class FloatOpts
    {
        /// <summary>rise: px it rises over its 600 ms (negative sinks; null = 43, the fx's default).</summary>
        public float? Rise;
        /// <summary>struck: drawn struck through (空振り).</summary>
        public bool Struck;
        /// <summary>plain: text only, no icon (a word, never read as a number).</summary>
        public bool Plain;
        /// <summary>side: a 16 px label after the number (「+5 間合い」, 「壁」, 「×1.5 脆化」); '' none.</summary>
        public string Side = "";
        /// <summary>track(): the x of the figure it rides with now (null: stays).</summary>
        public Func<float?> Track;
        /// <summary>avoid(): boxes its path stops under (the omen badges), AvoidGap px under them.</summary>
        public Func<IReadOnlyList<StageBox>> Avoid;
        /// <summary>avoidGap.</summary>
        public float AvoidGap;
        /// <summary>lowest: the floor line its path stays over (null: none).</summary>
        public float? Lowest;
        /// <summary>anchor: which edge stands on at.x.</summary>
        public FloatAnchor Anchor;
        /// <summary>followY(): the middle y of the float it stands beside; it rides at that height (its own rise is then 0).</summary>
        public Func<float> FollowY;
        /// <summary>followRise: that float's rise (for the swept box).</summary>
        public float FollowRise;
        /// <summary>onPlaced(span): where it stands, settled.</summary>
        public Action<FloatSpan> OnPlaced;
    }

    /// <summary>field.ghost's o: { kind, ms, ease, card }.</summary>
    public sealed class GhostOpts
    {
        /// <summary>kind: 'discard' / 'exile' (76×104) or 'sheet' (a PileLayout-sized card back).</summary>
        public GhostKind Kind;
        /// <summary>ms: the flight's length (JS `o.ms || 0`).</summary>
        public float Ms;
        /// <summary>
        /// ease: the player hands 'out' (sheet) / 'in' (exile) / 'inout' (discard); null = 'out' for a sheet, 'inout' for the
        /// others (fx.js ghost: `o.ease || (sheet ? 'out' : 'inout')`).
        /// </summary>
        public EaseFn Ease;
        /// <summary>card: the played card's own view (IHand.GhostCard), shrunk to 76 wide and fading late; null = the plain ghost.</summary>
        public object Card;
    }

    /// <summary>
    /// field.js (docs/BATTLE_SCENE.md §6b): the arena — backdrop, floor, figures, fx (strikes, numbers, ghosts), the
    /// enemy-turn shade, the low-HP vignette and 俊敏's marks. Unity: FigureView, FloorView, DepictionFx, EnemyTurnShadeView
    /// and the figure / floor parts of DepictionPlayer. Fills env.Geom.Figure / Receiver / Receivers / FreeStepBoxes / CellBox.
    /// Plays no sound.
    /// </summary>
    public interface IField
    {
        // ---- frame ----

        /// <summary>
        /// applyFrame(frame, { first }): art ids / Down / heights; the floor; the player on its cells at once; each enemy's
        /// HOME on its cells (moved there now only when first); stance glows snapped when first; the enemy-turn shade; the gap
        /// number hidden once the outcome is decided.
        /// </summary>
        // js: scenes/battle/field.js:509
        void ApplyFrame(DepictionFrame frame, bool first = false);

        /// <summary>showGapTo(unit) (several enemies): the floor's one N is enemy `unit`'s while a held card frames it; −1 = the frame's own (the nearest).</summary>
        // js: scenes/battle/field.js:552
        void ShowGapTo(int unit);

        /// <summary>enemyUnits(): the enemies the field draws (standing or not), by unit.</summary>
        // js: scenes/battle/field.js:563
        IReadOnlyList<int> EnemyUnits();

        /// <summary>setExtras(list): kept for the contract; draws nothing (every enemy has its own figure).</summary>
        // js: scenes/battle/field.js:569
        void SetExtras(IReadOnlyList<object> list);

        /// <summary>drawAim(frame, unit, now) (§5.8 提示): the floor takes `frame`'s cells now; newly aimed ones drawn left to right, 30 ms a cell (`now`: at once).</summary>
        // js: scenes/battle/field.js:576
        Promise DrawAim(DepictionFrame frame, int unit, bool now = false);

        /// <summary>showReach(cells): the amber reach light on these floor cells (150 ms in, out).</summary>
        // js: scenes/battle/field.js:582
        void ShowReach(IReadOnlyList<int> cells);

        /// <summary>retireAim(ms): the battle is over — every aimed / dense / move cell fades over `ms` and stays clear.</summary>
        // js: scenes/battle/field.js:584
        Promise RetireAim(float ms);

        /// <summary>retireAimOf(unit): enemy `unit` falls — its aimed / dense / move cells go now, with its badge.</summary>
        // js: scenes/battle/field.js:589
        void RetireAimOf(int unit);

        /// <summary>focusAim(unit) (§2.2.2): only enemy `unit`'s aimed cells stay lit (its badge is hovered); −1 for all.</summary>
        // js: scenes/battle/field.js:591
        void FocusAim(int unit);

        /// <summary>hideReach(): the reach light goes (120 ms, linear).</summary>
        // js: scenes/battle/field.js:592
        void HideReach();

        /// <summary>setShift(dir) (§2.3 / §2.4): the arena slides dir × 15 % (−1 left for the journal, +1 right for the pile list, 0 back) over 220 ms on real time.</summary>
        // js: scenes/battle/field.js:598
        void SetShift(int dir);

        /// <summary>shiftX: the arena's slide now (px).</summary>
        // js: scenes/battle/field.js:602
        float ShiftX { get; }

        /// <summary>nudgeOf(side, unit): how far the figure stands moved in from its cell's middle (0 for most).</summary>
        // js: scenes/battle/field.js:604
        float NudgeOf(UnitSide side, int unit = -1);

        /// <summary>setLowHp(on) (§5.6 順 5): the vignette rests at 0.25 while `on` (HP under 15), at 0 otherwise (200 ms out).</summary>
        // js: scenes/battle/field.js:607
        Promise SetLowHp(bool on);

        /// <summary>pulseLowHp(ms): a blow landed under 15 HP — the vignette 0.5 → its rest over `ms` (out).</summary>
        // js: scenes/battle/field.js:616
        Promise PulseLowHp(float ms);

        /// <summary>appearEnemies(ms, from) (§5.18 順 7): every enemy figure from `from` to full over ms (out); ms 0 puts them at `from`.</summary>
        // js: scenes/battle/field.js:629
        Promise AppearEnemies(float ms, float from = 0f);

        /// <summary>setEnemyTurnShade(on) = EnemyTurnShadeView.Show: the black below y 790 on or off (no fade).</summary>
        // js: scenes/battle/field.js:636
        void SetEnemyTurnShade(bool on);

        // ---- figure state ----

        /// <summary>setPose(side, pose, unit) = FigureView.SetPose (a falling figure stays down).</summary>
        // js: scenes/battle/field.js:645
        void SetPose(UnitSide side, FigurePose pose, int unit = -1);

        /// <summary>setDown(side, down, unit) = FigureView.SetDown: gone or standing, at rest either way.</summary>
        // js: scenes/battle/field.js:653
        void SetDown(UnitSide side, bool down, int unit = -1);

        /// <summary>enemyAway(unit): the enemy stands off its home (not when down).</summary>
        // js: scenes/battle/field.js:662
        bool EnemyAway(int unit = -1);

        /// <summary>setEnemyHomeCell(cell, size, unit): PlayEvent — the enemy's home is its cell after the event (no move now).</summary>
        // js: scenes/battle/field.js:667
        void SetEnemyHomeCell(int cell, int size, int unit = -1);

        /// <summary>snapHome(side, unit) = SnapHome: onto its home at once, standing (pose Idle).</summary>
        // js: scenes/battle/field.js:673
        void SnapHome(UnitSide side, int unit = -1);

        /// <summary>stanceGlowShown(side, unit): the stance's body light is on.</summary>
        // js: scenes/battle/field.js:684
        bool StanceGlowShown(UnitSide side, int unit = -1);

        /// <summary>setStanceGlow(side, on, unit) = FigureView.SetStanceGlow: on or off at once.</summary>
        // js: scenes/battle/field.js:686
        void SetStanceGlow(UnitSide side, bool on, int unit = -1);

        /// <summary>fadeStanceGlow(side, on, ms, unit) = FigureView.FadeStanceGlow: in or out over ms (out); a newer call takes the light over.</summary>
        // js: scenes/battle/field.js:695
        Promise FadeStanceGlow(UnitSide side, bool on, float ms, int unit = -1);

        // ---- beats ----

        /// <summary>lunge(side, px, ms, unit) = Lunge: Act pose, px toward the other side (out); an enemy's stops 8 px short of an ally in its way.</summary>
        // js: scenes/battle/field.js:706
        Promise Lunge(UnitSide side, float px, float ms, int unit = -1);

        /// <summary>returnHome(side, ms, unit) = ReturnHome: back to its home (inout), then Idle.</summary>
        // js: scenes/battle/field.js:717
        Promise ReturnHome(UnitSide side, float ms, int unit = -1);

        /// <summary>enemyMotion(system, ms, unit) = EnemyMotion: reach by EnemyMotionShape.Reach, stretch FigureFacing.During, Sweep / Strike lean, Shield rise 16; it stays there until it returns.</summary>
        // js: scenes/battle/field.js:733
        Promise EnemyMotion(StrikeSystem system, float ms, int unit = -1);

        /// <summary>recoil(side, strong, ms, unit) = Recoil: Hit pose, leans away 7° (12° strong), shakes 10 px (20) ×3.</summary>
        // js: scenes/battle/field.js:763
        Promise Recoil(UnitSide side, bool strong, float ms, int unit = -1);

        /// <summary>flash(side, role, ms, unit) = FigureView.Flash: the body blinks the role's colour and back (out).</summary>
        // js: scenes/battle/field.js:776
        Promise Flash(UnitSide side, FxRole role, float ms, int unit = -1);

        /// <summary>heave(side, ms, unit) (§5.3 息): y-scale 1 → 1.05 → 1 (inout).</summary>
        // js: scenes/battle/field.js:784
        Promise Heave(UnitSide side, float ms, int unit = -1);

        /// <summary>
        /// fall(side, ms, unit, { keepFoot }) = FigureView.Fall (DefeatShape): tilt about the feet, sink 40, darken to 0.35, fade
        /// from 60 %, then down. keepFoot (an enemy falling while others stand): the foot light stays and the figure is not put
        /// down (FadeFoot does that).
        /// </summary>
        // js: scenes/battle/field.js:793
        Promise Fall(UnitSide side, float ms, int unit = -1, bool keepFoot = false);

        /// <summary>fadeFoot(side, ms, unit) (§5.15 順 5): after a keepFoot fall, the foot light goes over ms (linear) and the figure is down.</summary>
        // js: scenes/battle/field.js:826
        Promise FadeFoot(UnitSide side, float ms, int unit = -1);

        /// <summary>dimFeet(ms) (§5.19 順 1): a lost battle — every standing figure's foot light shrinks out (in).</summary>
        // js: scenes/battle/field.js:840
        Promise DimFeet(float ms);

        /// <summary>bump(side, dir, ms, unit) (§3.14 / §5.7 順 5): a move that went nowhere — leans 12 px that way and back (inout).</summary>
        // js: scenes/battle/field.js:851
        Promise Bump(UnitSide side, int dir, float ms, int unit = -1);

        /// <summary>shakeArena(ms) = ShakeArena: the figures' layer shakes 6 px ×3.</summary>
        // js: scenes/battle/field.js:860
        Promise ShakeArena(float ms);

        /// <summary>
        /// moveOnFloor(cue, cells, ms) = MoveOnFloor: the player walks to FeetOf(cue.PlayerCellAfter), an enemy that moved
        /// (Cue.Unit with its UnitCellAfter; one enemy: EnemyCellAfter) slides to its own; out when Pushed, else inout. The
        /// homes change as the walk starts (geom.Figure().Walking says to follow X meanwhile).
        /// </summary>
        // js: scenes/battle/field.js:870
        Promise MoveOnFloor(Cue cue, int cells, float ms);

        /// <summary>snapOnFloor(cue) = SnapOnFloor: the walk's end state at once.</summary>
        // js: scenes/battle/field.js:892
        void SnapOnFloor(Cue cue);

        // ---- 俊敏's choice (§4.8, field/freestep.js) ----

        /// <summary>showFreeStep(playerCell, ways): the arrows over the open ways and 留まる on the player's cell (150 ms out).</summary>
        // js: scenes/battle/field.js:907
        Promise ShowFreeStep(int playerCell, FreeStepWays ways);

        /// <summary>focusFreeStep(dir): the amber edge on mark `dir` (−1 / 0 / +1); returns the focus it took (null when that mark is not shown).</summary>
        // js: scenes/battle/field.js:909
        int? FocusFreeStep(int dir);

        /// <summary>hideFreeStep(ms): the marks go (null = their own 120 ms; 0 at once).</summary>
        // js: scenes/battle/field.js:910
        Promise HideFreeStep(float? ms = null);

        /// <summary>freeStepShown.</summary>
        // js: scenes/battle/field.js:911
        bool FreeStepShown { get; }

        /// <summary>freeStepFocus: −1 / 0 / +1, or null for none.</summary>
        // js: scenes/battle/field.js:912
        int? FreeStepFocus { get; }

        /// <summary>freeStepDirs(): which marks stand, [−1?, 0, +1?].</summary>
        // js: scenes/battle/field.js:913
        IReadOnlyList<int> FreeStepDirs();

        /// <summary>freeStepCentre(dir): the middle of mark `dir`, or null.</summary>
        // js: scenes/battle/field.js:914
        StagePoint? FreeStepCentre(int dir);

        /// <summary>recountGap(gap, playerCell, playerSize, enemyCell, ms): the gap chip — old number up and out, new in from below.</summary>
        // js: scenes/battle/field.js:915
        Promise RecountGap(int gap, int playerCell, int playerSize, int enemyCell, float ms);

        /// <summary>setGap(gap, playerCell, playerSize, enemyCell): the gap chip at once.</summary>
        // js: scenes/battle/field.js:916
        void SetGap(int gap, int playerCell, int playerSize, int enemyCell);

        // ---- fx ----

        /// <summary>strike(side, o) = DepictionFx.Strike across `side`'s chest; reaches no further than 8 px short of another standing figure.</summary>
        // js: scenes/battle/field.js:926
        Promise Strike(UnitSide side, StrikeOpts o);

        /// <summary>wallHit(side, dir, ms, unit) (§5.7 順 7): the edge `side` was pushed into lights (the floor's end, or the figure in the way flashes white) and it takes 打's ripple.</summary>
        // js: scenes/battle/field.js:940
        Promise WallHit(UnitSide side, int dir, float ms, int unit = -1);

        /// <summary>traitAccent(side, ms, unit) = DepictionFx.TraitAccent at the point of the blow.</summary>
        // js: scenes/battle/field.js:960
        Promise TraitAccent(UnitSide side, float ms, int unit = -1);

        /// <summary>mark(side, o) = DepictionFx.Mark: 盾 ring, 型 crest at the feet (+ body light when CrestLight), 気 sigil; they ride with the figure.</summary>
        // js: scenes/battle/field.js:965
        Promise Mark(UnitSide side, MarkOpts o);

        /// <summary>burst(at, role, size): a fire-and-forget radial flash at stage point `at`.</summary>
        // js: scenes/battle/field.js:976
        void Burst(StagePoint at, FxRole role, float size);

        /// <summary>
        /// floatText(at, text, role, fontSize, o) = DepictionFx.FloatText: rises o.Rise px over 600 ms (Unity 1000), fading in
        /// its last 40 %, on runPaced; numbers keep the DS FloatNum tag with its icon, words (o.Plain) a black 80 % tag.
        /// </summary>
        // js: scenes/battle/field.js:977
        Promise FloatText(StagePoint at, string text, FxRole role, float fontSize, FloatOpts o = null);

        /// <summary>shieldBlock(side, amountText, ms, unit, { broken }) = DepictionFx.ShieldBlock: a 100×114 rim of light 40 px toward the blow; broken: a crack and its halves part.</summary>
        // js: scenes/battle/field.js:982
        Promise ShieldBlock(UnitSide side, string amountText, float ms, int unit = -1, bool broken = false);

        /// <summary>shieldAt(side, unit): where the shield of a blocked blow stands.</summary>
        // js: scenes/battle/field.js:987
        StagePoint ShieldAt(UnitSide side, int unit = -1);

        /// <summary>ghost(from, to, o) = FlyGhostTo / SlideDiscardOntoDraw.</summary>
        // js: scenes/battle/field.js:993
        Promise Ghost(StagePoint from, StagePoint to, GhostOpts o);

        /// <summary>destroy().</summary>
        // js: scenes/battle/field.js:995
        void Destroy();
    }

    // ---- 6c. the hud ------------------------------------------------------------------------------

    /// <summary>createHud(env, opts).</summary>
    // js: scenes/battle/BattleScene.js:151
    public sealed class HudOpts
    {
        /// <summary>opts.chain.index: the 1-based fight under way (ctx.chain, else { index: 1, total: 1 }).</summary>
        public int ChainIndex = 1;
        /// <summary>opts.chain.total.</summary>
        public int ChainTotal = 1;
        /// <summary>opts.nodeKind.</summary>
        public BattleNodeKind NodeKind;
        /// <summary>onEndTurn(): the plate was clicked (the player decides whether it counts).</summary>
        public Action OnEndTurn;
        /// <summary>onSpeed(): the speed button was clicked.</summary>
        public Action OnSpeed;
        /// <summary>onJournal(): the 手記 button.</summary>
        public Action OnJournal;
        /// <summary>onSettings(): the gear's click — only with SettingsInBattle (Esc is the scene's).</summary>
        public Action OnSettings;
        /// <summary>settingsInBattle: the gear only when the app opts in.</summary>
        public bool SettingsInBattle;
        /// <summary>onDrawer(open): the pile list opened / closed.</summary>
        public Action<bool> OnDrawer;
        /// <summary>onOmenHover(unit): the pointer over enemy `unit`'s badge (several enemies); −1 when it leaves.</summary>
        public Action<int> OnOmenHover;
    }

    /// <summary>One enemy figure the panels and badges follow (hud.follow's o.units row).</summary>
    public sealed class FollowUnit
    {
        public int Unit;
        /// <summary>x: the figure's home x, or its x while it walks.</summary>
        public float X;
        /// <summary>top: L.FeetY − its height.</summary>
        public float Top;
        /// <summary>size: its cells (≥ 1).</summary>
        public int Size = 1;
        public bool Down;
        public bool Walking;
    }

    /// <summary>hud.follow's o: { playerX, playerWalking, enemyX, enemyTop, cells, enemySize, units, snap }.</summary>
    // js: scenes/battle/player.js:818
    public sealed class FollowArgs
    {
        public float PlayerX;
        public bool PlayerWalking;
        /// <summary>enemyX / enemyTop: the focus enemy's.</summary>
        public float EnemyX;
        public float EnemyTop;
        /// <summary>cells: Floor.Cells (0 without a floor).</summary>
        public int Cells;
        /// <summary>enemySize: max(1, Floor.EnemySize).</summary>
        public int EnemySize = 1;
        public IReadOnlyList<FollowUnit> Units = Array.Empty<FollowUnit>();
        /// <summary>snap: put the panels straight on their places (the battle's first frame).</summary>
        public bool Snap;
    }

    /// <summary>One bar of a loss's result screen: { label, color, value } (RESULT_BARS).</summary>
    public readonly struct ResultBar
    {
        /// <summary>攻撃 / 防御 / スキル / スタンス.</summary>
        public readonly string Label;
        /// <summary>The CSS colour as the JS writes it ('var(--violet)', 'var(--guard)', 'var(--amber)', 'var(--crimson)').</summary>
        public readonly string Color;
        public readonly int Value;

        public ResultBar(string label, string color, int value)
        {
            Label = label;
            Color = color;
            Value = value;
        }
    }

    /// <summary>The result paper's words (player.js resultInfo; scene text): a win's Lines only, a loss's everything.</summary>
    // js: scenes/battle/player.js:1974
    public sealed class ResultInfo
    {
        /// <summary>title: a loss's 「i 戦目で倒れた」 / 「第 L 層で倒れた」; null for a win.</summary>
        public string Title;
        public IReadOnlyList<string> Lines = Array.Empty<string>();
        /// <summary>barsLabel: 出した札 (a loss).</summary>
        public string BarsLabel;
        public IReadOnlyList<ResultBar> Bars = Array.Empty<ResultBar>();
        public IReadOnlyList<string> Stats = Array.Empty<string>();
        /// <summary>button: 手記へ (a loss); null for none.</summary>
        public string Button;
    }

    /// <summary>
    /// hud.status(side, unit): a number panel (status.js; StatusBarView). The player's stamina routes to the stamina tag.
    /// </summary>
    public interface IStatusPanel
    {
        /// <summary>hp: the HP the panel shows now (the bar's number; 0 before its first bind).</summary>
        // js: scenes/battle/hud/status.js:357
        int Hp { get; }

        /// <summary>shown: shown and holding its place (a fallen enemy's panel that gave its place up is not).</summary>
        // js: scenes/battle/hud/status.js:359
        bool Shown { get; }

        /// <summary>box(): its box as it stands, from its layout numbers (asked every frame).</summary>
        // js: scenes/battle/hud/status.js:361
        PanelBox Box();

        /// <summary>setGuard(n): the Guard number at once (n &lt; 0 ignored).</summary>
        // js: scenes/battle/hud/status.js:460
        void SetGuard(int n);

        /// <summary>popGuard(n, ms = 260) = StatusBarView.PopGuard: the badge pops with the new number.</summary>
        // js: scenes/battle/hud/status.js:462
        Promise PopGuard(int n, float ms = 260f);

        /// <summary>crackGuard(ms) = StatusBarView.CrackGuard: the shield flashes white and the badge shakes as it breaks.</summary>
        // js: scenes/battle/hud/status.js:467
        Promise CrackGuard(float ms);

        /// <summary>setHp(hp): the bar and its trail at once.</summary>
        // js: scenes/battle/hud/status.js:471
        void SetHp(int hp);

        /// <summary>animateHp(hp, ms) = StatusBarView.AnimateHp: the number lands at once; the bar slides (out).</summary>
        // js: scenes/battle/hud/status.js:473
        Promise AnimateHp(int hp, float ms);

        /// <summary>animateTrail(ms) = StatusBarView.AnimateTrail: the grey band catches up with the bar (inout).</summary>
        // js: scenes/battle/hud/status.js:483
        Promise AnimateTrail(float ms);

        /// <summary>snapTrail(): the grey band onto the bar at once.</summary>
        // js: scenes/battle/hud/status.js:488
        void SnapTrail();

        /// <summary>setHpKeepingTrail(hp): the number and the bar at once, the grey band where it was.</summary>
        // js: scenes/battle/hud/status.js:489
        void SetHpKeepingTrail(int hp);

        /// <summary>showHpLoss(n) = StatusBarView.ShowHpLoss: the wick stripes (HpWickSpec.Span); 0 takes them away. An enemy's panel only.</summary>
        // js: scenes/battle/hud/status.js:491
        void ShowHpLoss(int n);

        /// <summary>setStatus(label, stacks) = StatusBarView.SetStatus: one word's chip settled at once — added, restacked, or gone at 0.</summary>
        // js: scenes/battle/hud/status.js:498
        void SetStatus(string label, int stacks);

        /// <summary>playStatus(label, stacks, beat, ms) = StatusBarView.PlayStatus: StatusApply swells in, StatusStack pops, StatusTick shrinks for a moment, StatusVanish fades.</summary>
        // js: scenes/battle/hud/status.js:515
        Promise PlayStatus(string label, int stacks, EffectId beat, float ms);

        /// <summary>setStamina(stamina, max) = StatusBarView.SetStamina (the player's goes to the stamina tag).</summary>
        // js: scenes/battle/hud/status.js:541
        void SetStamina(int stamina, int max);

        /// <summary>lightPips(stamina, max, perPipMs) = StatusBarView.LightPips: the recovered points light one at a time, perPipMs apart.</summary>
        // js: scenes/battle/hud/status.js:547
        Promise LightPips(int stamina, int max, float perPipMs);

        /// <summary>popPips(ms): a spend pops beside the card's beats.</summary>
        // js: scenes/battle/hud/status.js:559
        Promise PopPips(float ms);

        /// <summary>greyOut(ms) (§5.15 順 5): a fallen enemy's panel turns grey over ms (linear), then SetDown(true).</summary>
        // js: scenes/battle/hud/status.js:429
        Promise GreyOut(float ms);

        /// <summary>setDown(on) (§2.2.6): a fallen enemy's panel stays, grey (a GreyOut under way ends here).</summary>
        // js: scenes/battle/hud/status.js:411
        void SetDown(bool on);

        /// <summary>setFocus(on) (review 19): the amber edge — the enemy a Tab at rest chose.</summary>
        // js: scenes/battle/hud/status.js:392
        void SetFocus(bool on);
    }

    /// <summary>hud.omenOf(unit): one enemy's omen badge (+ its plan) — hud/omen.js; OmenBadgeView.</summary>
    public interface IOmenBadge
    {
        /// <summary>bind(o) = OmenBadgeView.Bind: the badge as the frame has it (shown when Visible), the plan when PlanVisible; null ignored.</summary>
        // js: scenes/battle/hud/omen.js:154
        void Bind(OmenFrame o);

        /// <summary>setSwatchShown(on, aimed) (several enemies): the hatch swatch stands on the badge while `aimed`.</summary>
        // js: scenes/battle/hud/omen.js:169
        void SetSwatchShown(bool on, bool aimed);

        /// <summary>setTail(dx): the ▼ under the badge at dx px from its left edge (its enemy's head), or null for none.</summary>
        // js: scenes/battle/hud/omen.js:181
        void SetTail(float? dx);

        /// <summary>setBehind(on): a walking enemy's badge stands behind the others until it arrives.</summary>
        // js: scenes/battle/hud/omen.js:190
        void SetBehind(bool on);

        /// <summary>setMoveDir(dir): the way a move omen's enemy goes on screen (−1 / +1; 0 keeps ←).</summary>
        // js: scenes/battle/hud/omen.js:197
        void SetMoveDir(int dir);

        /// <summary>promotePlan(ms) (§3.10): an elite's second action — the plan drops into the lower slot over ms and turns solid; resolves true when it promoted, false when there was nothing to.</summary>
        // js: scenes/battle/hud/omen.js:210
        Promise<bool> PromotePlan(float ms);

        /// <summary>popIn(ms) (§5.8 提示): the badge (and its plan) drops 24 px into place and fades in (out); 0 at once.</summary>
        // js: scenes/battle/hud/omen.js:244
        Promise PopIn(float ms);

        /// <summary>blink(ms): the standing omen dips to 25 % and back once (inout).</summary>
        // js: scenes/battle/hud/omen.js:263
        Promise Blink(float ms);

        /// <summary>flare(ms): the omen swells 18 % and back as the enemy carries it out, its kind word flashing (out).</summary>
        // js: scenes/battle/hud/omen.js:268
        Promise Flare(float ms);

        /// <summary>reveal(ms) (scene addition): the badge was read further (Disclosure rose) — it swells once as its numbers come in.</summary>
        // js: scenes/battle/hud/omen.js:278
        Promise Reveal(float ms);

        /// <summary>traitWord(word, ms) (§5.9 順 12): the enemy's fired trait's condition word swells in inside the badge's right end over ms, holds 900, fades 200; '' resolves at once.</summary>
        // js: scenes/battle/hud/omen.js:289
        Promise TraitWord(string word, float ms);

        /// <summary>setActing(on) (§4.4 敵の番): the phosphor edge and the violet light while the enemy acts on this omen.</summary>
        // js: scenes/battle/hud/omen.js:302
        void SetActing(bool on);

        /// <summary>fadeOut(ms) = OmenBadgeView.FadeOut: the badge fades (in); the plan stays.</summary>
        // js: scenes/battle/hud/omen.js:307
        Promise FadeOut(float ms);

        /// <summary>hideNow(): the badge gone at once (the plan stays).</summary>
        // js: scenes/battle/hud/omen.js:311
        void HideNow();

        /// <summary>fadeOutAll(ms): a fall takes the badge and its plan together (in).</summary>
        // js: scenes/battle/hud/omen.js:313
        Promise FadeOutAll(float ms);

        /// <summary>hideAllNow().</summary>
        // js: scenes/battle/hud/omen.js:318
        void HideAllNow();

        /// <summary>strikeSide(ms): the punished side did not apply — the reach glyph greys out, is struck and shakes.</summary>
        // js: scenes/battle/hud/omen.js:320
        Promise StrikeSide(float ms);

        /// <summary>strikeSideNow().</summary>
        // js: scenes/battle/hud/omen.js:326
        void StrikeSideNow();

        /// <summary>box(): the badge's box (200×98) as it stands.</summary>
        // js: scenes/battle/hud/omen.js:337
        StageBox Box();

        /// <summary>visible: the badge shows (alpha over 0.001).</summary>
        // js: scenes/battle/hud/omen.js:339
        bool Visible { get; }
    }

    /// <summary>hud.omen: the badge facade — the leader's badge, except Bind (by the omen's Unit) and SetActing(false) (every badge).</summary>
    // js: scenes/battle/hud.js:516
    public interface IOmenFacade
    {
        /// <summary>bind(o): the badge of o.Unit (several enemies) or the leader's.</summary>
        // js: scenes/battle/hud.js:517
        void Bind(OmenFrame o);

        // js: scenes/battle/hud.js:522
        Promise PopIn(float ms);

        // js: scenes/battle/hud.js:523
        Promise Blink(float ms);

        // js: scenes/battle/hud.js:524
        Promise Flare(float ms);

        /// <summary>setActing(on): false puts the light out on every badge; true lights the leader's.</summary>
        // js: scenes/battle/hud.js:525
        void SetActing(bool on);

        // js: scenes/battle/hud.js:530
        Promise FadeOut(float ms);

        // js: scenes/battle/hud.js:531
        void HideNow();

        // js: scenes/battle/hud.js:532
        Promise FadeOutAll(float ms);

        // js: scenes/battle/hud.js:533
        void HideAllNow();

        // js: scenes/battle/hud.js:534
        Promise StrikeSide(float ms);

        // js: scenes/battle/hud.js:535
        void StrikeSideNow();
    }

    /// <summary>hud.stamina: the stamina tag's two calls of the player.</summary>
    // js: scenes/battle/hud.js:575
    public interface IHudStamina
    {
        /// <summary>showCost(n): "▸ n" and the pay rings while a card is held (0 clears).</summary>
        void ShowCost(int n);

        /// <summary>glowReserve(ms): pips 1–3 glow (構え's 息).</summary>
        Promise GlowReserve(float ms);
    }

    /// <summary>hud.endTurn: the end-turn plate's beats.</summary>
    // js: scenes/battle/hud.js:581
    public interface IHudEndTurn
    {
        /// <summary>pop(ms): StanceCue's 200 ms pop.</summary>
        Promise Pop(float ms);

        /// <summary>flash(ms): the plate brightens once (nothing playable); off at the enemy's turn and at the result.</summary>
        Promise Flash(float ms);

        /// <summary>refuse(ms) (review 21): the plate dims and shakes 6 px twice (the mash window turned a press away).</summary>
        Promise Refuse(float ms);
    }

    /// <summary>
    /// hud.js (docs/BATTLE_SCENE.md §6c): panels and chrome — status panels, omen badges, stance lists, stamina tag, corner,
    /// speed, piles + list, played, reserve hint, journal / end-turn buttons, banners, result card. Unity: StatusBarView,
    /// OmenBadgeView, StanceListView, StaminaTagView, PilesView, PileListView, BottomRightViews, CornerInfoView,
    /// BattleSpeedButton, the end-turn plate, Banner, ShowResult. Fills env.Geom.PileMiddle / OmenBox / OmenShown / OmenBoxes /
    /// EndTurnBox / StatusBox / StatusBoxes / StanceBox / StanceSlot / CoversPointer / DrawerRight. Plays 'uiClick' for its buttons.
    /// </summary>
    public interface IHud
    {
        /// <summary>
        /// applyFrame(frame, { first, keepOmen }): corner, panels, badges, stance lists, stamina tag, piles (+ the open list's
        /// rows), played, the plate's alpha. keepOmen: the unit whose badge keeps the omen it acts on (an action held across
        /// its events); −1 for none — it keeps nothing then, also with one enemy (JS `o.keepOmen != null &amp;&amp; o.keepOmen === e.unit`).
        /// </summary>
        // js: scenes/battle/hud.js:215
        void ApplyFrame(DepictionFrame frame, bool first = false, int keepOmen = -1);

        /// <summary>follow(o): per frame — panels at y 614 under their figures (eased 1 − e^(−14·dt·speed)), badges over the heads (+ their lift).</summary>
        // js: scenes/battle/hud.js:273
        void Follow(FollowArgs o);

        /// <summary>status(side, unit): side's number panel (an enemy's by unit; −1 the leader's).</summary>
        // js: scenes/battle/hud.js:550
        IStatusPanel Status(UnitSide side, int unit = -1);

        /// <summary>omen: the badge facade.</summary>
        // js: scenes/battle/hud.js:555
        IOmenFacade Omen { get; }

        /// <summary>syncOmenLook(frame, unit): an omen shown mid-event — enemy `unit`'s badge takes its swatch and move arrow from `frame`.</summary>
        // js: scenes/battle/hud.js:560
        void SyncOmenLook(DepictionFrame frame, int unit);

        /// <summary>omenOf(unit): enemy `unit`'s badge; the leader's when unknown (−1).</summary>
        // js: scenes/battle/hud.js:570
        IOmenBadge OmenOf(int unit = -1);

        /// <summary>enemyUnits(): the enemies the hud draws now, by unit (the frame's order).</summary>
        // js: scenes/battle/hud.js:574
        IReadOnlyList<int> EnemyUnits();

        /// <summary>stamina: { showCost, glowReserve }.</summary>
        // js: scenes/battle/hud.js:575
        IHudStamina Stamina { get; }

        /// <summary>reserveHint(text) = ReserveHintView (x 1560, y 984); '' hides it.</summary>
        // js: scenes/battle/hud.js:579
        void ReserveHint(string text);

        /// <summary>setEnemyTurn(on): ShowEnemyTurn's plate half — the end-turn plate at EnemyTurnShadeLayout.EndTurnAlpha (0.4).</summary>
        // js: scenes/battle/hud.js:580
        void SetEnemyTurn(bool on);

        /// <summary>endTurn: { pop, flash, refuse }.</summary>
        // js: scenes/battle/hud.js:581
        IHudEndTurn EndTurn { get; }

        /// <summary>banner(text, side, ms): a band at y 400 (DS .ocb-banner), fade in 25 % / hold / out 25 % (Unity's curve).</summary>
        // js: scenes/battle/hud.js:582
        Promise Banner(string text, BannerSide side, float ms);

        /// <summary>resultCard(outcome, ms, info, { onButton }): the DS end paper in the Over layer (a win's 720-wide paper / a loss's result screen); onButton its button's click.</summary>
        // js: scenes/battle/hud.js:587
        Promise ResultCard(BattleOutcome outcome, float ms, ResultInfo info, Action onButton);

        /// <summary>resultCardNow(outcome, info, { onButton }): the paper at once.</summary>
        // js: scenes/battle/hud.js:588
        void ResultCardNow(BattleOutcome outcome, ResultInfo info, Action onButton);

        /// <summary>chainBanner(title, name, scale, onOut) (§2.5 / §5.18 順 6): the dark board, 「n 戦目」 and the enemy's name (300 in, 700 hold, 400 out, × scale; 0 at once); onOut() as the board comes up.</summary>
        // js: scenes/battle/hud.js:590
        Promise ChainBanner(string title, string name, float scale, Action onOut);

        /// <summary>lossWordOut(ms) (review 21): 「力尽きた」 fades as the result screen rises over it.</summary>
        // js: scenes/battle/hud.js:592
        Promise LossWordOut(float ms);

        /// <summary>settleEnd(ms, outcome): the battle is over — the scrim comes up, the plate goes off, the fallen units' panels and stance lists fade.</summary>
        // js: scenes/battle/hud.js:594
        Promise SettleEnd(float ms, BattleOutcome outcome);

        /// <summary>settleEndNow(outcome).</summary>
        // js: scenes/battle/hud.js:595
        void SettleEndNow(BattleOutcome outcome);

        /// <summary>retireOmens(ms): the battle is over — the standing enemies' badges and plans fade (0 at once).</summary>
        // js: scenes/battle/hud.js:597
        Promise RetireOmens(float ms);

        /// <summary>liftOmen(units): the badges of these enemies rise just clear of the prediction tag (hand.TaggedUnits()); empty lets them down.</summary>
        // js: scenes/battle/hud.js:599
        void LiftOmen(IReadOnlyList<int> units);

        /// <summary>setRestFocus(unit) (review 19): enemy `unit`'s panel wears the amber edge; −1 clears.</summary>
        // js: scenes/battle/hud.js:604
        void SetRestFocus(int unit);

        /// <summary>setLog(lines) (review 19): the pile list's ログ全文 tab — the player's event log, newest last.</summary>
        // js: scenes/battle/hud.js:608
        void SetLog(IReadOnlyList<string> lines);

        /// <summary>flyOmenToStances(unit, ms) (§5.11 順 8): a copy of the badge flies to the end of the enemy stance list over ms (out), the badge hidden behind it.</summary>
        // js: scenes/battle/hud.js:613
        Promise FlyOmenToStances(int unit, float ms);

        /// <summary>lossInk(word, inkMs, wordMs) (§5.19 順 2〜3): the ink floods up from the bottom over inkMs, then `word` comes in over wordMs.</summary>
        // js: scenes/battle/hud.js:641
        Promise LossInk(string word, float inkMs, float wordMs);

        /// <summary>speed(label, chevrons) = BattleSpeedButton.Show (tooltip BattleSpeed.Tooltip).</summary>
        // js: scenes/battle/hud.js:642
        void Speed(string label, int chevrons);

        /// <summary>setSpeedVisible(on): the speed switch (and the gear) shown or not — gone once the battle is over.</summary>
        // js: scenes/battle/hud.js:644
        void SetSpeedVisible(bool on);

        /// <summary>closePinned() (review 21): Esc closes a chip's / stance's detail pinned by a right click; whether one was open.</summary>
        // js: scenes/battle/hud.js:646
        bool ClosePinned();

        /// <summary>drawerOpen(): the pile list is open.</summary>
        // js: scenes/battle/hud.js:647
        bool DrawerOpen();

        /// <summary>closeDrawer().</summary>
        // js: scenes/battle/hud.js:648
        void CloseDrawer();

        // ---- raw input routed by the scene (the JS listens on window / the root) ----

        /// <summary>window pointerdown, capture phase (runs before the scene's and the hand's): a primary press unpins a pinned detail.</summary>
        // js: scenes/battle/hud.js:459
        void OnPinDown(BattlePointer p);

        /// <summary>root contextmenu: a right click elsewhere than the chip it pinned unpins the detail.</summary>
        // js: scenes/battle/hud.js:460
        void OnPinContext(BattlePointer p);

        /// <summary>window pointermove: the pointer over an enemy's badge (several enemies) → opts.OnOmenHover(unit / −1).</summary>
        // js: scenes/battle/hud.js:440
        void OnPointerMove(BattlePointer p);

        /// <summary>destroy().</summary>
        // js: scenes/battle/hud.js:649
        void Destroy();
    }
}
