// The battle scene's playback core: port of scenes/battle/player.js (itself a port of the Unity DepictionPlayer: the
// flow, the plan's waits, the cue beats, the input states). It computes no rule: every number, label, verdict and line
// of text comes from ctx.Source (DepictionFrame / DepictionEvent / Cue / PlayVerdict) and the ported Script/ helpers.
// Positions come from the modules (env.Geom); how long an event holds the flow is EffectPlan's (HoldBeat waits each
// step the plan lists, #495).
//
// Every state-changing call goes to ctx.Source itself (AdvanceAuto, EndTurn, TryPlay, TakeFreeStep), never to a kept
// reference, so the save recorder that wraps the source sees it. The modules (hand / field / hud, docs/BATTLE_SCENE.md
// §6, BattleContracts.cs) draw; this file decides what plays when.
//
// Several enemies (scene addition with the writer's extension, docs/port-notes/depiction-multi-enemy.md): each cue plays
// on the enemy its Cue.Unit names — its figure (field, the trailing `unit` argument), its number panel (hud.Status(side,
// unit)) and its omen badge (hud.OmenOf(unit)); a card released over enemy k goes to ctx.Source.TryPlay(id, zone, k).
// With one enemy every call is the one Unity makes (no target argument reaches the source). Enemy k is
// DepictionFrame.Enemies[k] (#589's EnemyFrame: its number panel, omen, cells and stripes).
//
// The JS `undefined` unit (one enemy / "the leader") is −1 here, as in the module contracts; a JS `null` "none" state
// (the acting unit, the Tab's enemy, the last enemy's phase) is an int?.
//
// Engine-free (Trial.Battle.Flow). The View rules boundary holds: no `using BattleCore;`, no rule-class call; the three
// core numbers the scene texts quote are copied into CoreNumbers (see there).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Depiction;
using Depiction.Bridge;

namespace Trial.Battle
{
    // ---- the scene's ctx (the player's part of it) ---------------------------------------------------

    /// <summary>player.state: the input states (docs/BATTLE_SCENE.md §4.1).</summary>
    public enum BattlePlayerState
    {
        /// <summary>'opening': before the first automatic run.</summary>
        Opening,
        /// <summary>'auto': RunAutomaticEvents — turn start, the enemy's turn, next omen, falls.</summary>
        Auto,
        /// <summary>'idle': the source waits for the player.</summary>
        Idle,
        /// <summary>'resolving': a player-asked event plays, or a refused card returns.</summary>
        Resolving,
        /// <summary>'over': the source finished — the result, then ctx.OnFinished once.</summary>
        Over,
        /// <summary>'halted': the source threw; input and events stop.</summary>
        Halted,
    }

    /// <summary>ctx.chain: { index, total } — the 1-based fight under way of the chain and how many it holds.</summary>
    public sealed class BattleChain
    {
        public int Index;
        public int Total;

        public BattleChain() { }

        public BattleChain(int index, int total)
        {
            Index = index;
            Total = total;
        }
    }

    /// <summary>ctx.auto.choose's answer: a card id ('' or null: end the turn) and the enemy it goes to (−1: none named).</summary>
    public sealed class BattleAutoPick
    {
        public string Id = "";
        public int Unit = -1;

        public BattleAutoPick() { }

        public BattleAutoPick(string id, int unit = -1)
        {
            Id = id;
            Unit = unit;
        }
    }

    /// <summary>
    /// ctx.auto (dev / tests, docs/BATTLE_SCENE.md §4.7): { choose(source, playedThisTurn) → cardId | { id, unit } | '',
    /// freeStep?(source) → −1 | 0 | 1 }. A missing Choose takes the source's SuggestedCardId.
    /// </summary>
    public sealed class BattleAuto
    {
        public Func<IDepictionSource, int, BattleAutoPick> Choose;
        public Func<IDepictionSource, int> FreeStep;
    }

    /// <summary>ctx.openJournal's options: { enemyIds, disclosureOf, onClose }.</summary>
    public sealed class BattleJournalOpts
    {
        public IReadOnlyList<string> EnemyIds = Array.Empty<string>();
        public Func<string, int> DisclosureOf;
        public Action OnClose;
    }

    /// <summary>
    /// The scene's ctx (main.js hands it to mountBattleScene, BattleScene.js passes it to createPlayer): the source, the
    /// app's settings and sound, where the fight is, the chain, the journal, and the app's callbacks. The player reads
    /// Source (live, at every call), Chain, Auto, OnFinished, OnError and OnSettingsChange; the rest is the scene's.
    /// </summary>
    public sealed class BattleCtx
    {
        /// <summary>ctx.source: the battle (CoreBattleSource, or the save recorder that wraps it). An ITrialSource takes targets and 俊敏's step.</summary>
        public IDepictionSource Source;
        /// <summary>ctx.settings (speed, shortFx), read live.</summary>
        public IBattleSettings Settings;
        /// <summary>ctx.audio.</summary>
        public IBattleAudio Audio;
        /// <summary>ctx.layer: the exploration's layer, 1..7.</summary>
        public int Layer = 1;
        /// <summary>ctx.layerTitle.</summary>
        public string LayerTitle = "";
        /// <summary>ctx.nodeKind.</summary>
        public BattleNodeKind NodeKind;
        /// <summary>ctx.miasmaPercent.</summary>
        public float? MiasmaPercent;
        /// <summary>ctx.chain, or null for a fight on its own (the frame's Corner then says it).</summary>
        public BattleChain Chain;
        /// <summary>ctx.disclosureOf(enemyId) → the journal's 開示度.</summary>
        public Func<string, int> DisclosureOf;
        /// <summary>ctx.openJournal(host, opts) → the journal (Dispose = its destroy()); host is the scene's view handle.</summary>
        public Func<object, BattleJournalOpts, IDisposable> OpenJournal;
        /// <summary>ctx.stage: the app's stage handle (the scene passes it on), or null.</summary>
        public object Stage;
        /// <summary>ctx.settingsInBattle: the gear and Esc's last step open the settings in a fight.</summary>
        public bool SettingsInBattle;
        /// <summary>ctx.onSettings().</summary>
        public Action OnSettings;
        /// <summary>ctx.onSettingsChange(settings): the speed switch changed settings.Speed in place.</summary>
        public Action<IBattleSettings> OnSettingsChange;
        /// <summary>ctx.onError(err): the source threw; the battle stopped (halted).</summary>
        public Action<Exception> OnError;
        /// <summary>ctx.onFinished(): the result has been read; called exactly once.</summary>
        public Action OnFinished;
        /// <summary>ctx.auto (dev / tests): the player plays itself.</summary>
        public BattleAuto Auto;
    }

    /// <summary>scenes/battle/player.js createPlayer(env, view, ctx).</summary>
    public sealed class BattlePlayer
    {
        // ---- scene texts and the scene's own numbers (player.js:34-209) -------------------------------

        /// <summary>Cue.Unchanged (−1).</summary>
        private const int Unchanged = Cue.Unchanged; // js: scenes/battle/player.js:35

        /// <summary>How far under an omen badge a float that would cross it stops (scene choice; UNDER_BADGES).</summary>
        private const float FloatUnderBadge = 8f; // js: scenes/battle/player.js:37

        /// <summary>
        /// How far over the figures' feet line a float's path stops (scene choice, review fix): floats of one action stand
        /// one under another, and one that would reach the floor strip stands beside them instead (field/fx.js placeFloat).
        /// </summary>
        private const float FloatOverFeet = 16f; // js: scenes/battle/player.js:43

        /// <summary>
        /// A blow Guard took part of (battle_ui_ux_v2 §5.6 順 2〜3): the "Guard −n" tag and the HP number rise side by side,
        /// this far apart, the pair centred on the chest — the tag on the side the blow came from, the HP number on the
        /// figure's back (review 17).
        /// </summary>
        private const float HpBesideGuardGap = 24f; // js: scenes/battle/player.js:52

        private const float StageEdge = 16f; // js: scenes/battle/player.js:53

        /// <summary>
        /// The core's numbers player.js reads off BattleCore's Constants (FragileMult, EmpowerMult, HeavyBlowPower) for its
        /// scene texts. The View rules boundary keeps BattleCore's rule classes out of every View file outside a Bridge
        /// folder, and no bridge hands these three out yet, so they are copied here; the flow tests pin them to the core's
        /// (a bridge accessor is asked for in the hand-over, after which these read it).
        /// </summary>
        public static class CoreNumbers
        {
            /// <summary>Constants.FragileMult (§5.1).</summary>
            public const double FragileMult = 1.5;
            /// <summary>Constants.EmpowerMult (§5.1).</summary>
            public const double EmpowerMult = 1.5;
            /// <summary>Constants.HeavyBlowPower (重撃's +6).</summary>
            public const int HeavyBlowPower = 6;
        }

        /// <summary>
        /// battle_ui_ux_v2 §5.7 順 9 / 順 11: a move the core stopped (呪縛, 鉤爪: CoreText.MoveBlockedLine) rises in grey,
        /// not in the warm of a fired trait.
        /// </summary>
        // js: scenes/battle/player.js:59
        private static readonly HashSet<string> MoveBlockedLines = new HashSet<string>(
            new[] { CoreText.MoveBlockedLine(BattleCore.StatusKind.Binding), CoreText.MoveBlockedLine(BattleCore.StatusKind.Hook) }
                .Where(s => !string.IsNullOrEmpty(s)), StringComparer.Ordinal);

        /// <summary>scene text (battle_ui_ux_v2 §3.8 俊敏): the guide while the free cell's choice waits.</summary>
        public const string FreeStepGuide = "俊敏で 1 マス動けます。矢印か「留まる」を選んでください（← / → と Enter）"; // js: scenes/battle/player.js:65

        /// <summary>scene text: the refusal line for a card tried while the choice waits (選ぶまで手札は掴めません).</summary>
        public const string FreeStepRefusal = "先に、動く先の矢印か「留まる」を選んでください"; // js: scenes/battle/player.js:66

        /// <summary>scene text (§5.7 順 9): the grey word for a step that moved no cell with no line of its own.</summary>
        public const string FreeStepStuck = "動けず"; // js: scenes/battle/player.js:67

        /// <summary>scene text (§3.5, review 16): the refusal line for a card pressed while the pile list is open.</summary>
        public const string DrawerGuide = "一覧を閉じるとプレイできます（Esc）"; // js: scenes/battle/player.js:73

        /// <summary>scene text (§2.4 / §3.5, review 17): the guide and the refusal line while the journal is open.</summary>
        public const string JournalGuide = "手記を閉じるとプレイできます（Esc）"; // js: scenes/battle/player.js:79

        /// <summary>scene text (§3.14, review 17): a single-target card's wrong-zone line says 敵の上 (the round receiver is not drawn).</summary>
        // js: scenes/battle/player.js:85
        public static string WrongZoneOnEnemy(string name) => "「" + name + "」は敵の上で離すと出せます";

        /// <summary>§5.7 順 7 (review 19): the event CoreScriptWriter.OpenIfBoundary opens for a player shoved into the end of the line.</summary>
        public const string WallTitle = "壁に当たる"; // js: scenes/battle/player.js:90

        /// <summary>scene text (§5.0 「−3 壁」): the side label of a wall's number.</summary>
        public const string WallSide = "壁"; // js: scenes/battle/player.js:92

        /// <summary>§5.7 順 7: the edge the pushed figure hit lights for this long (nominal ms).</summary>
        private const float WallEdgeMs = 80f; // js: scenes/battle/player.js:94

        /// <summary>The stance lines (構え・ a stance set, 構え停止・ / 構え復帰・): /^構え/.</summary>
        private const string StanceLine = "構え"; // js: scenes/battle/player.js:99
        private const string StanceSetPrefix = "構え・"; // js: scenes/battle/player.js:100

        /// <summary>
        /// battle_ui_ux_v2 §5.9 順 1 / 順 12 (review 21): a fired trait's condition is named by one word. The lamp word's head
        /// → the condition's name. `\d` is JS's ASCII digit; JS's `.` stops at every line terminator and its `$` (no m flag)
        /// is the end of the input only (.NET's `$` also matches before a final \n), so `(.+)$` is written out.
        /// </summary>
        // js: scenes/battle/player.js:106
        private static readonly (Regex Re, Func<Match, string> Name)[] ConditionNames =
        {
            (new Regex("^間合い", RegexOptions.CultureInvariant), m => "間合い"),
            (new Regex("^残[0-9]", RegexOptions.CultureInvariant), m => "温存"),
            (new Regex("^連動", RegexOptions.CultureInvariant), m => "連動"),
            (new Regex("^予兆", RegexOptions.CultureInvariant), m => "予兆"),
            (new Regex(@"^相手([^\n\r\u2028\u2029]+)\z", RegexOptions.CultureInvariant), m => m.Groups[1].Value + "中"),
            (new Regex(@"^自分([^\n\r\u2028\u2029]+)\z", RegexOptions.CultureInvariant), m => m.Groups[1].Value + "中"),
        };

        /// <summary>conditionName(word): the lamp word's head → the condition's one-word name (「間合い0」→「間合い」, 「残4」→「温存」).</summary>
        // js: scenes/battle/player.js:114
        public static string ConditionName(string word)
        {
            foreach (var (re, name) in ConditionNames)
            {
                Match m = re.Match(word);
                if (m.Success) return name(m);
            }
            return word;
        }

        /// <summary>§5.0 / §5.9 順 9 「主数字に「+6 重撃」」: the trait effects named rather than counted carry their own word.</summary>
        // js: scenes/battle/player.js:125
        private static readonly Dictionary<string, Func<string>> NamedEffects = new Dictionary<string, Func<string>>(StringComparer.Ordinal)
        {
            ["重撃"] = () => "+" + Str(CoreNumbers.HeavyBlowPower) + " 重撃",
            ["転換"] = () => "転換",
            ["追撃"] = () => "追撃",
        };

        /// <summary>§5.0 「−12 ×1.5 脆化」, §5.10 強化 (review 21): the multiplier a blow carried; the factors are the core's.</summary>
        private static readonly string FragileWord = BattleCore.EnumTokens.ToLabel(BattleCore.StatusKind.Fragile); // js: scenes/battle/player.js:131
        private static readonly string EmpowerWord = BattleCore.EnumTokens.ToLabel(BattleCore.StatusKind.Empower); // js: scenes/battle/player.js:132

        // js: scenes/battle/player.js:133
        private static readonly Dictionary<string, string> MultLabel = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [FragileWord] = "×" + CoreNumbers.FragileMult.ToString(CultureInfo.InvariantCulture) + " " + FragileWord,
            [EmpowerWord] = "×" + CoreNumbers.EmpowerMult.ToString(CultureInfo.InvariantCulture) + " " + EmpowerWord,
        };

        /// <summary>scene text (§5.7 順 6 「押し出し」「引き寄せ」): the muted word over a figure pulled toward the other side.</summary>
        public const string PulledWord = "引き寄せ"; // js: scenes/battle/player.js:138

        /// <summary>§5.0: the floating HP loss carries its sign, the typographic minus (「−18」).</summary>
        public const string Minus = "−"; // js: scenes/battle/player.js:140

        /// <summary>§5.12 順 5 (review 21): a held action's second blow follows this long after the first one's beats.</summary>
        public const float ContinuePauseMs = 80f; // js: scenes/battle/player.js:146

        /// <summary>scene text (§1.1 C2 / C4, review 19): the pile list's log tab — the kinds of line the spec names.</summary>
        // js: scenes/battle/player.js:151
        public static class LogText
        {
            public static string Turn(string n) => "— ターン " + n + " —";
            public const string EnemyTurn = "— 敵の番 —";
            public static string Mine(string title) => "自分: " + title;
            public static string Foe(string who, string title) => who + ": " + title;
            public static string Trait(string text) => "　特性: " + text;
            public static string Status(string who, string text, int stacks) =>
                "　" + (!string.IsNullOrEmpty(who) ? who + " " : "") + "状態: " + text + (stacks > 0 ? " " + Str(stacks) : " 消えた");
            public static string Stance(string name, int count) => "　スタンス: " + name + (count > 1 ? "（" + Str(count) + " つ目）" : "");
            public const string Rebuild = "　山札を作り直した";
            public static string Hp(string side, int n) => "　" + side + " HP −" + Str(n);
            public static string Guard(string side, int n) => "　" + side + " Guard −" + Str(n);
            public static string Wall(string side, int n) => "　" + side + " 壁 −" + Str(n);
            public static string Fell(string side) => "　" + side + "が倒れた";
        }

        /// <summary>scene text: who a log line's number is about (with two or three enemies, each by its name: LogWho).</summary>
        // js: scenes/battle/player.js:166
        private static string LogWhoOf(UnitSide side) => side == UnitSide.Player ? "自分" : side == UnitSide.Enemy ? "敵" : "";

        /// <summary>At most this many log lines are kept (the oldest go).</summary>
        public const int LogMax = 400; // js: scenes/battle/player.js:168

        /// <summary>§5.19 順 1〜4 (review 19): a loss — feet 600, ink 800, word 300, hold 1000, the paper 400 (nominal ms).</summary>
        // js: scenes/battle/player.js:174
        private static class Loss
        {
            public const float FeetMs = 600f;
            public const float InkMs = 800f;
            public const float WordMs = 300f;
            public const float HoldMs = 1000f;
            public const float PaperMs = 400f;
        }

        /// <summary>scene text (review 21, §2.5 / §5.19 順 5〜7): the end of the battle's words around the frame's numbers.</summary>
        // js: scenes/battle/player.js:179
        public static class ResultText
        {
            public static string FellAt(BattleChain chain, int layer) =>
                chain != null && chain.Total > 1 ? Str(chain.Index) + " 戦目で倒れた" : "第 " + Str(layer) + " 層で倒れた";
            public static string HpLeft(int hp, int max) => "残 HP " + Str(hp) + " / " + Str(max);
            public const string PlayedHead = "出した札";
            public static string Traits(int n) => "特性の発動 " + Str(n) + " 回";
            public static string Turns(int n) => Str(n) + " ターン戦った";
            public const string ToJournal = "手記へ";
            public static string Beat(string names) => !string.IsNullOrEmpty(names) ? names + "を倒した。" : "敵を倒した。";
            public static string WonLine(int turn, int hp, int max) => "ターン " + Str(turn) + " ・ 残りの HP " + Str(hp) + " / " + Str(max);
            public static string ChainAt(BattleChain chain) => Str(chain.Index) + " / " + Str(chain.Total) + " 戦目";
            public static string SwitchTitle(int index) => Str(index) + " 戦目";
        }

        /// <summary>§2.5: the four attributes' bars on the result screen (a move card is a skill).</summary>
        // js: scenes/battle/player.js:192
        private static readonly (CardKind[] Kinds, string Label, string Color)[] ResultBars =
        {
            (new[] { CardKind.Attack }, "攻撃", "var(--violet)"),
            (new[] { CardKind.Guard }, "防御", "var(--guard)"),
            (new[] { CardKind.Skill, CardKind.Move }, "スキル", "var(--amber)"),
            (new[] { CardKind.Stance }, "スタンス", "var(--crimson)"),
        };

        /// <summary>§5.18 順 7: the chain's next enemy comes out of the haze over this (out, nominal ms).</summary>
        private const float ChainAppearMs = 400f; // js: scenes/battle/player.js:199

        /// <summary>scene text (§5.19 順 3).</summary>
        public const string LossWord = "力尽きた"; // js: scenes/battle/player.js:201

        /// <summary>hud/omen.js PLAN_DROP_MS: an elite's plan drops into the lower slot over this (§3.10).</summary>
        private const float PlanDropMs = 150f; // js: scenes/battle/hud/omen.js:26

        /// <summary>sound.md: the hit sound of a blow by its system (打 for pushes and pulls).</summary>
        // js: scenes/battle/player.js:204
        private static string HitSound(StrikeSystem system)
        {
            switch (system)
            {
                case StrikeSystem.Slash: return "hitSlash";
                case StrikeSystem.Thrust: return "hitThrust";
                case StrikeSystem.Sweep: return "hitSlash";
                case StrikeSystem.Strike: return "hitBlunt";
                default: return null;
            }
        }

        /// <summary>A number as the JS writes it (invariant: the ASCII '-').</summary>
        private static string Str(int n) => n.ToString(CultureInfo.InvariantCulture);

        // ---- private records ------------------------------------------------------------------------

        /// <summary>pendingSide: the side label waiting for this event's next number, and the TraitFire it came from.</summary>
        private sealed class PendingSide
        {
            public string Text = "";
            public string Kind = "none";
            public UnitSide Side;
            public int Unit = -1;
        }

        /// <summary>heldEnd / carried: an enemy action held across its events — { unit, title, side }.</summary>
        private sealed class HeldAction
        {
            public int Unit = -1;
            public string Title = "";
            public PendingSide Side;
        }

        /// <summary>guardTag: the last "Guard −n" tag raised — { side, unit, away, span }.</summary>
        private sealed class GuardTag
        {
            public UnitSide Side;
            public int Unit = -1;
            public int Away;
            public FloatSpan Span;
        }

        /// <summary>wallOf's answer: { dir }.</summary>
        private sealed class Wall
        {
            public readonly int Dir;
            public Wall(int dir) { Dir = dir; }
        }

        /// <summary>traitOf's answer: { condition, effect, side, kind }.</summary>
        private sealed class TraitInfo
        {
            public string Condition = "";
            public string Effect = "";
            public string Side = "";
            public string Kind = "none";
        }

        /// <summary>One blow of multLabelsOf.</summary>
        private sealed class BlowGroup
        {
            public int At;
            public int End;
            public Cue Slash;
            public Cue Number;
            public bool Fragile;
            public bool Empower;
        }

        /// <summary>freeStep: { ways } while 俊敏's choice waits.</summary>
        private sealed class FreeStepChoice
        {
            public FreeStepWays Ways;
        }

        /// <summary>
        /// A JS Set of listeners, walked as Set.prototype.forEach walks it: one entry per function, in the order added; one
        /// deleted before its turn is skipped, and one added while the walk runs (also one deleted and added again) is called
        /// in that same walk, at its end.
        /// </summary>
        private sealed class ListenerSet<T> where T : class
        {
            /// <summary>The entries; a null is one deleted while a walk runs (dropped when the outermost walk ends).</summary>
            private readonly List<T> _items = new List<T>();
            private int _walking;

            public Action Add(T fn)
            {
                if (fn != null && !_items.Contains(fn)) _items.Add(fn);
                return () => Delete(fn);
            }

            private void Delete(T fn)
            {
                int i = fn != null ? _items.IndexOf(fn) : -1;
                if (i < 0) return;
                if (_walking > 0) _items[i] = null;
                else _items.RemoveAt(i);
            }

            public void ForEach(Action<T> call)
            {
                _walking++;
                try
                {
                    for (int i = 0; i < _items.Count; i++)
                    {
                        T fn = _items[i];
                        if (fn != null) call(fn);
                    }
                }
                finally
                {
                    if (--_walking == 0) _items.RemoveAll(x => x == null);
                }
            }
        }

        // ---- state (player.js:216-352) --------------------------------------------------------------

        private readonly BattleEnv _env;
        private readonly IHand _hand;
        private readonly IField _field;
        private readonly IHud _hud;
        private readonly BattleCtx _ctx;
        private readonly BattleClock _clock;
        private readonly BattleRealClock _real;
        private readonly EffectSwitches _switches;

        private bool _alive = true;
        private bool _halted;
        private BattlePlayerState _state = BattlePlayerState.Opening;
        private bool _busy;
        private readonly ListenerSet<Action<BattlePlayerState>> _listeners = new ListenerSet<Action<BattlePlayerState>>();
        /// <summary>The plan of the event playing now (EffectPlan.StepsOf), taken in order by HoldBeat.</summary>
        private List<EffectStep> _planned = new List<EffectStep>();
        private DepictionEvent _plannedEvent;
        /// <summary>Sounds left in the event's two-sound budget (sound.md 「1 回の行動につき 2 つまで」).</summary>
        private int _budget = 2;
        /// <summary>Of the budget, how many the event's own action and result still need (review fix).</summary>
        private int _essentialLeft;
        private CardFace _heldFace;
        /// <summary>The enemy slot the held / selected single-target card frames now; −1 for none.</summary>
        private int _heldUnit = -1;
        private CardFace _hoveredFace;
        private CardFace _reachFor;
        private string _frameStanceHint = "";
        private bool _finishedCalled;
        private Action _resultSkip;
        /// <summary>When (real ms) the result card had risen (null before), and the last press that asked to skip it.</summary>
        private float? _resultUpAt;
        private float _resultLastPress = float.NegativeInfinity;
        private int _autoTimer;
        private int _playedThisTurn;
        /// <summary>Whether an overlay (journal, settings) holds the input and the scene; set by the scene.</summary>
        private Func<bool> _overlayOpen = () => false;
        /// <summary>Whether the pile list is open (the cards are not taken, §3.5); set by the scene.</summary>
        private Func<bool> _drawerOpen = () => false;
        /// <summary>Whether the journal is open (§2.4); set by the scene.</summary>
        private Func<bool> _journalOpen = () => false;
        /// <summary>A refused card is sliding home: an end-turn asked for meanwhile waits for it (review fix).</summary>
        private bool _returningCard;
        private bool _endTurnQueued;
        /// <summary>Tab while nothing is held (§3.4, two or more enemies): the enemy the floor's one N is read to.</summary>
        private int? _restingGapUnit;
        /// <summary>俊敏's free cell (§3.8): while the choice waits, its ways; else null.</summary>
        private FreeStepChoice _freeStep;
        /// <summary>The turn whose free cell was asked about (asked once a turn).</summary>
        private int _freeStepTurn = -1;
        /// <summary>The unit whose omen is lit as acting (§4.4 敵の番), or null.</summary>
        private int? _actingUnit;
        /// <summary>The enemy's turn is playing (from the turn-end event to the next turn start); held to the result (review fix).</summary>
        private bool _enemyPhase;
        /// <summary>Plays the source accepted (the keyboard's auto-select follows a confirmed play only, §3.4).</summary>
        private int _acceptedPlays;
        /// <summary>The enemy whose phase played last in this enemy turn (several enemies: the 600 ms pause, §3.11).</summary>
        private int? _lastEnemyUnit;
        /// <summary>The last "Guard −n" tag raised (the HP number lines up with it).</summary>
        private GuardTag _guardTag;
        private readonly ListenerSet<Action> _eventEndListeners = new ListenerSet<Action>();
        private int _eventEnds;
        private bool _afterWaiting;
        /// <summary>§5.0 / §5.9 (review 19): the side label the next number of this event carries.</summary>
        private PendingSide _pendingSide;
        /// <summary>§5.12 (review 21): an enemy action's last beats held until the next event shows whether it goes on.</summary>
        private HeldAction _heldEnd;
        /// <summary>The held action the event playing now goes on with (its step in is not taken again), or null.</summary>
        private HeldAction _carried;
        /// <summary>The card the player's event plays (its face: a push's way, a move's length), or null.</summary>
        private CardFace _eventFace;
        /// <summary>The shade and the plate's 40 % are up (the enemy's turn): the 構えの予告 stays off.</summary>
        private bool _enemyTurnShown;
        /// <summary>The pile list's log.</summary>
        private readonly List<string> _log = new List<string>();
        /// <summary>What the battle played, for the result screen (§2.5): cards by CardKind, traits fired.</summary>
        private readonly Dictionary<CardKind, int> _tallyPlayed = new Dictionary<CardKind, int>();
        private int _tallyTraits;
        /// <summary>The end-turn mash window (real ms, review fixes 16 / 17 / 21).</summary>
        private float _mashUntil;
        private float _lastPressAt = float.NegativeInfinity;
        private float _turnOpenedAt = float.NegativeInfinity;
        private float _askedAt = float.NegativeInfinity;
        private bool _turnStarted;
        /// <summary>The multiplier label of each blow of the event playing, by the cue whose number shows it.</summary>
        private Dictionary<Cue, string> _multLabels = new Dictionary<Cue, string>();
        private bool _snapPanels = true;
        private string _nothingKey = "";
        private string _refusalCause;
        private int _planErrors;
        private readonly Action _offFrame;

        /// <param name="env">The scene environment (docs/BATTLE_SCENE.md §2).</param>
        /// <param name="hand">view.hand.</param>
        /// <param name="field">view.field.</param>
        /// <param name="hud">view.hud.</param>
        /// <param name="ctx">The scene's ctx (source, chain, onFinished, auto …).</param>
        // js: scenes/battle/player.js:216
        public BattlePlayer(BattleEnv env, IHand hand, IField field, IHud hud, BattleCtx ctx)
        {
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _hand = hand ?? throw new ArgumentNullException(nameof(hand));
            _field = field ?? throw new ArgumentNullException(nameof(field));
            _hud = hud ?? throw new ArgumentNullException(nameof(hud));
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _clock = env.Clock;
            _real = env.Real;
            _switches = EffectSwitches.AllOn();

            // js: scenes/battle/player.js:2129 (per frame)
            _offFrame = _clock.OnFrame((dt, dtReal) =>
            {
                if (!_alive) return;
                FollowFigures();
                UpdateFloorReach();
            });

            // js: scenes/battle/player.js:2137 (the hand's callbacks)
            HandCallbacks = new HandOpts
            {
                OnRelease = OnRelease,
                OnHover = face =>
                {
                    _hoveredFace = face;
                    UpdateFloorReach();
                },
                OnHoldStart = (face, unit) =>
                {
                    _heldFace = face;
                    _heldUnit = unit >= 0 ? unit : -1;
                    // §4.7 / §5.2: the stamina it would pay turns to rings, and "▸ n" shows (on the enemy it frames).
                    ShowHeldCost();
                    ShowReserveHint();
                    ShowHpLoss();
                    UpdateFloorReach();
                    LiftOmenForHeld();
                },
                OnHoldEnd = face =>
                {
                    _heldFace = null;
                    _heldUnit = -1;
                    _hud.Stamina.ShowCost(0);
                    ShowReserveHint();
                    ShowHpLoss();
                    UpdateFloorReach();
                    LiftOmenForHeld();
                },
                Preview = PreviewOf,
                OnTarget = (face, unit) =>
                {
                    if (unit >= 0) _heldUnit = unit;
                    ShowHeldCost();
                    ShowReserveHint();
                    ShowHpLoss();
                    LiftOmenForHeld();
                },
            };
        }

        private IDepictionSource Src => _ctx.Source; // js: scenes/battle/player.js:271
        private DepictionFrame FrameNow => Src.Frame; // js: scenes/battle/player.js:272
        private float Ms(EffectId id) => _switches.Ms(id); // js: scenes/battle/player.js:220

        /// <summary>
        /// The enemy a cue is about, or −1 (the JS undefined: the player alone / a one-enemy source). #589's Cue.Unit names the
        /// enemy on the beat's enemy end even on a beat about the player's own numbers, so this reads it the way the JS
        /// writer wrote it: a blow, a wall, a 見切り return (Slash / GuardBlock / Hit), an omen, a windup, a struck-off bonus
        /// and a fall name their enemy; a RangeSwitch names the enemy that moved (UnitCellAfter), else none; any other cue
        /// names its holder when that is an enemy.
        /// </summary>
        // js: scenes/battle/player.js:274
        private static int UnitOf(Cue x)
        {
            if (x == null) return -1;
            switch (x.Kind)
            {
                case CueKind.Slash:
                case CueKind.GuardBlock:
                case CueKind.Hit:
                case CueKind.OmenShow:
                case CueKind.EnemyWindup:
                case CueKind.SideBonusMiss:
                case CueKind.Defeat:
                    return x.Unit;
                case CueKind.RangeSwitch:
                    return x.UnitCellAfter > 0 ? x.Unit : -1;
                default:
                    return x.Target == UnitSide.Enemy ? x.Unit : -1;
            }
        }

        // js: scenes/battle/player.js:274
        private static int UnitOf(DepictionEvent x) => x != null && x.Unit >= 0 ? x.Unit : -1;

        /// <summary>
        /// Every enemy's share of `frame` (#589's EnemyFrame), in the core's order: enemy k is the k-th. A source that keeps
        /// one enemy (the filming script) writes none, so its one enemy and omen stand alone, as one share.
        /// </summary>
        // js: scenes/battle/player.js:276
        private static IReadOnlyList<EnemyFrame> EnemyShares(DepictionFrame frame)
        {
            if (frame != null && frame.Enemies != null && frame.Enemies.Count > 0) return frame.Enemies;
            if (frame != null && frame.Enemy != null) return new[] { new EnemyFrame { Unit = frame.Enemy, Omen = frame.Omen ?? new OmenFrame() } };
            return Array.Empty<EnemyFrame>();
        }

        /// <summary>Every enemy's number panel of `frame`, in the core's order (one-enemy sources: the one).</summary>
        private static IReadOnlyList<UnitFrame> EnemyFrames(DepictionFrame frame) => EnemyShares(frame).Select(e => e.Unit).ToList();

        /// <summary>The battle has two or more enemies: only then does a target reach the source.</summary>
        // js: scenes/battle/player.js:279
        private bool MultiNow() => EnemyFrames(FrameNow).Count > 1;

        /// <summary>Enemy `unit`'s omen in `frame` (Frame.Enemies[unit].Omen); the frame's one omen otherwise.</summary>
        // js: scenes/battle/player.js:281
        private static OmenFrame OmenFrameOf(DepictionFrame frame, int unit)
        {
            if (frame != null && frame.Enemies != null && unit >= 0 && unit < frame.Enemies.Count) return frame.Enemies[unit].Omen;
            return frame?.Omen;
        }

        private void LogError(Exception err) => _env.Log.Error(null, err);

        // js: scenes/battle/player.js:308
        private void EventEnded()
        {
            _afterWaiting = false;
            _eventEnds += 1;
            _eventEndListeners.ForEach(fn =>
            {
                try { fn(); }
                catch (Exception err) { LogError(err); }
            });
        }

        /// <summary>The after-wait of the event that has just played (review 19: a fast-forward pressed in it carries on to the next event).</summary>
        // js: scenes/battle/player.js:314
        private async Promise AfterWait(float ms)
        {
            _afterWaiting = true;
            await _clock.Wait(ms);
        }

        /// <summary>SetHandInteractable: the hand takes input or not, and the dim / guide line follow at once.</summary>
        // js: scenes/battle/player.js:355
        private void SetHandInteractable(bool on)
        {
            _hand.SetInteractable(on);
            RefreshPlayable();
        }

        // js: scenes/battle/player.js:360
        private void SetState(BattlePlayerState s)
        {
            if (_state == s) return;
            _state = s;
            _listeners.ForEach(fn =>
            {
                try { fn(s); }
                catch (Exception err) { LogError(err); }
            });
        }

        // js: scenes/battle/player.js:366
        private void Sfx(string name, float delay = 0f, float? mult = null)
        {
            if (string.IsNullOrEmpty(name)) return;
            _env.Audio.Play(name, delay, mult);
        }

        /// <summary>
        /// A result sound inside the event's two-sound budget. `essential`: the blow's system hit, the body's hit, the Guard's
        /// block or break; anything else plays only on a slot those do not need.
        /// </summary>
        // js: scenes/battle/player.js:375
        private void SfxBudget(string name, bool essential = false)
        {
            if (string.IsNullOrEmpty(name) || _budget <= 0) return;
            if (essential) _essentialLeft = Math.Max(0, _essentialLeft - 1);
            else if (_budget - _essentialLeft <= 0) return;
            _budget -= 1;
            Sfx(name);
        }

        /// <summary>How many essential sounds `ev`'s cues will ask for (the same choices PlayCue makes).</summary>
        // js: scenes/battle/player.js:383
        private static int EssentialSoundsOf(DepictionEvent ev)
        {
            int n = 0;
            for (int i = 0; i < ev.Cues.Count; i++)
            {
                Cue cue = ev.Cues[i];
                if (cue.Kind == CueKind.Slash) n += 1;
                else if (cue.Kind == CueKind.Hit && cue.Target == UnitSide.Player) n += 1;
                else if (cue.Kind == CueKind.GuardBlock)
                {
                    Cue next = i + 1 < ev.Cues.Count ? ev.Cues[i + 1] : null;
                    if (!(next != null && next.Kind == CueKind.Hit && next.Target == cue.Target)) n += 1;
                }
            }
            return n;
        }

        // ---- effects and the plan's waits (#495) ----------------------------------------------------

        /// <summary>Runs the thunk; a throw or a rejection is logged and the answer resolves anyway.</summary>
        // js: scenes/battle/player.js:398
        private Promise Safe(Func<Promise> play)
        {
            try
            {
                Promise p = play?.Invoke();
                if (p == null) return Promise.Resolved();
                return p.Catch(LogError);
            }
            catch (Exception err)
            {
                LogError(err);
                return Promise.Resolved();
            }
        }

        /// <summary>A module's beat started beside the flow and not waited for (the JS drops the promise; a null answer is fine).</summary>
        private static void Unawaited(Promise beat) { }

        /// <summary>Effect: a beat beside the flow. Off, it settles at once.</summary>
        // js: scenes/battle/player.js:409
        private Promise Effect(EffectId id, Func<Promise> play, Action settle = null)
        {
            if (!_switches.IsOn(id))
            {
                settle?.Invoke();
                return Promise.Resolved();
            }
            return Safe(play);
        }

        /// <summary>FloatEffect: a rising number, as an effect.</summary>
        // js: scenes/battle/player.js:418
        private void FloatEffect(EffectId id, StagePoint at, string text, FxRole role, float size, FloatOpts o)
        {
            if (!_switches.IsOn(id)) return;
            Safe(() => _field.FloatText(at, text, role, size, o)).Forget();
        }

        /// <summary>Scene addition (review): a float stops 8 px under every omen badge standing over its path.</summary>
        // js: scenes/battle/player.js:428
        private IReadOnlyList<StageBox> BadgeBoxes()
        {
            return _env.Geom.OmenBoxes != null ? _env.Geom.OmenBoxes() : Array.Empty<StageBox>();
        }

        /// <summary>The floor line a float's path stays over (FLOAT_OVER_FEET).</summary>
        private const float FloatFloor = L.FeetY - FloatOverFeet; // js: scenes/battle/player.js:432

        /// <summary>UNDER_BADGES merged into `o`: { avoid: badgeBoxes, avoidGap: FLOAT_UNDER_BADGE, lowest: floatFloor }.</summary>
        // js: scenes/battle/player.js:433
        private FloatOpts UnderBadges(FloatOpts o)
        {
            o.Avoid = BadgeBoxes;
            o.AvoidGap = FloatUnderBadge;
            o.Lowest = FloatFloor;
            return o;
        }

        /// <summary>A copy of a float's options (the JS Object.assign({}, o, …)).</summary>
        private static FloatOpts Copy(FloatOpts o)
        {
            if (o == null) return new FloatOpts();
            return new FloatOpts
            {
                Rise = o.Rise, Struck = o.Struck, Plain = o.Plain, Side = o.Side, Track = o.Track, Avoid = o.Avoid, AvoidGap = o.AvoidGap,
                Lowest = o.Lowest, Anchor = o.Anchor, FollowY = o.FollowY, FollowRise = o.FollowRise, OnPlaced = o.OnPlaced,
            };
        }

        /// <summary>The x of `side`'s figure now, for a float to ride along with (fx.floatText's `track`).</summary>
        // js: scenes/battle/player.js:436
        private Func<float?> TrackOf(UnitSide side, int unit)
        {
            return () =>
            {
                FigureGeom g = Figure(side, unit);
                return g != null ? g.X : (float?)null;
            };
        }

        private FigureGeom Figure(UnitSide side, int unit = -1) => _env.Geom.Figure?.Invoke(side, unit);

        private StagePoint PileMiddle(PileKind kind) => _env.Geom.PileMiddle != null ? _env.Geom.PileMiddle(kind) : default;

        // js: scenes/battle/player.js:443
        private void BeginPlan(DepictionEvent ev)
        {
            _planned = new List<EffectStep>(EffectPlan.StepsOf(ev));
            _plannedEvent = ev;
            _budget = 2;
            _essentialLeft = Math.Min(_budget, EssentialSoundsOf(ev));
            _pendingSide = null;
            _multLabels = MultLabelsOf(ev);
            LogEvent(ev);
        }

        /// <summary>
        /// The pile list's log (§1.1 C2 / C4; review 19): one heading line per event the player sees and under it the kinds
        /// of line the spec names, each in the script's own words, and the numbers the floats show. The hud shows them on the
        /// list's ログ全文 tab.
        /// </summary>
        // js: scenes/battle/player.js:460
        private void LogEvent(DepictionEvent ev)
        {
            if (ev == null || ev.Cues == null) return;
            var lines = new List<string>();
            if (ev.Kind == DepictionEventKind.TurnStart) lines.Add(LogText.Turn(ev.After != null && ev.After.Corner != null ? Str(ev.After.Corner.Turn) : ""));
            else if (ev.Kind == DepictionEventKind.TurnEnd) lines.Add(LogText.EnemyTurn);
            // Review 21: the writer's turn-start beat of an enemy is titled 敵の番 — no action of its own, so no heading.
            else if (ev.Kind == DepictionEventKind.EnemyAction && !string.IsNullOrEmpty(ev.Title) && ev.Title != DepictionText.EnemyTurn)
                lines.Add(LogText.Foe(LogWho(UnitSide.Enemy, UnitOf(ev), ev.After), ev.Title));
            else if ((ev.Kind == DepictionEventKind.PlayCard || ev.Kind == DepictionEventKind.FreeStep) && !string.IsNullOrEmpty(ev.Title))
                lines.Add(LogText.Mine(ev.Title));
            var hpNow = new Dictionary<string, int?>(StringComparer.Ordinal);
            string HpOf(Cue c)
            {
                string key = c.Target + ":" + (c.Target == UnitSide.Enemy ? Str(UnitOf(c)) : "");
                if (!hpNow.ContainsKey(key))
                {
                    IStatusPanel s = _hud.Status(c.Target, c.Target == UnitSide.Enemy ? UnitOf(c) : -1);
                    hpNow[key] = s != null ? s.Hp : (int?)null;
                }
                return key;
            }
            int Lost(Cue c)
            {
                string key = HpOf(c);
                int? before = hpNow[key];
                if (c.HpAfter != Unchanged) hpNow[key] = c.HpAfter;
                return before != null && c.HpAfter != Unchanged && before.Value - c.HpAfter > 0 && before.Value - c.HpAfter < c.Amount ? before.Value - c.HpAfter : c.Amount;
            }
            foreach (Cue c in ev.Cues)
            {
                string who = LogWho(c.Target, UnitOf(c), ev.After);
                string text = c.Text ?? "";
                switch (c.Kind)
                {
                    case CueKind.TraitFire:
                        if (text.StartsWith(StanceSetPrefix, StringComparison.Ordinal))
                        {
                            string name = text.Substring(StanceSetPrefix.Length);
                            lines.Add(LogText.Stance(name, StanceCount(ev.After, c, name)));
                        }
                        else if (MoveBlockedLines.Contains(text) || text.StartsWith(StanceLine, StringComparison.Ordinal)) lines.Add("　" + text);
                        else
                        {
                            // §5.9 (review 21): the condition by its name, as the floats say it (「特性: 温存 Guard+3」, not 「残4」).
                            TraitInfo tr = TraitOf(ev, c);
                            lines.Add(LogText.Trait(tr != null && tr.Condition.Length > 0 ? tr.Condition + " " + tr.Effect : text));
                        }
                        break;
                    case CueKind.StatusChange:
                        lines.Add(LogText.Status(c.Target == UnitSide.Enemy ? who : "", text, c.StacksAfter));
                        break;
                    case CueKind.DrawHand:
                        if (c.RebuildAfter >= 0 && c.Amount > 0) lines.Add(LogText.Rebuild);
                        break;
                    case CueKind.Slash:
                        if (c.HpAfter != Unchanged && c.Amount > 0) lines.Add(LogText.Hp(who, Lost(c)));
                        break;
                    case CueKind.GuardBlock:
                        lines.Add(WallOf(ev, c) != null ? LogText.Wall(who, c.Amount) : LogText.Guard(who, c.Amount));
                        break;
                    case CueKind.Hit:
                        lines.Add(WallOf(ev, c) != null ? LogText.Wall(who, Lost(c)) : LogText.Hp(who, Lost(c)));
                        break;
                    case CueKind.HpChange:
                        HpOf(c);
                        if (c.HpAfter != Unchanged) hpNow[HpOf(c)] = c.HpAfter;
                        lines.Add("　" + who + " " + text + " " + (c.Amount > 0 ? "+" : "") + Str(c.Amount));
                        break;
                    case CueKind.Defeat:
                        lines.Add(LogText.Fell(who));
                        break;
                }
            }
            if (lines.Count == 0) return;
            _log.AddRange(lines);
            if (_log.Count > LogMax) _log.RemoveRange(0, _log.Count - LogMax);
            _hud.SetLog(_log);
        }

        /// <summary>
        /// Review 21: who a log line is about — 自分, or with one enemy 敵; with two or three each enemy by its roster name,
        /// with its hatch glyph (／＼｜) when another enemy of the fight bears the same name.
        /// </summary>
        // js: scenes/battle/player.js:515
        private string LogWho(UnitSide side, int unit, DepictionFrame frame)
        {
            if (side != UnitSide.Enemy) return LogWhoOf(side);
            IReadOnlyList<EnemyFrame> list = EnemyShares(frame ?? FrameNow);
            if (list.Count < 2) return LogWhoOf(UnitSide.Enemy);
            EnemyFrame uf = unit >= 0 && unit < list.Count ? list[unit] : null;
            string name = uf != null ? BattleLayout.EnemyNameOf(uf.Unit.ArtId) : "";
            if (string.IsNullOrEmpty(name)) return LogWhoOf(UnitSide.Enemy);
            bool twin = false;
            for (int k = 0; k < list.Count; k++)
                if (k != unit && BattleLayout.EnemyNameOf(list[k].Unit.ArtId) == name) { twin = true; break; }
            return twin ? name + BattleLayout.HatchGlyph(uf.StripeDirection) : name;
        }

        /// <summary>How many of stance `name` the unit a cue is about holds in `frame` (its StanceRow.Count; 1 when unknown).</summary>
        // js: scenes/battle/player.js:526
        private static int StanceCount(DepictionFrame frame, Cue cue, string name)
        {
            if (frame == null) return 1;
            UnitFrame uf;
            if (cue.Target == UnitSide.Player) uf = frame.Player;
            else
            {
                uf = null;
                IReadOnlyList<UnitFrame> list = EnemyFrames(frame);
                for (int k = 0; k < list.Count; k++)
                    if (k == UnitOf(cue)) { uf = list[k]; break; }
                uf = uf ?? frame.Enemy;
            }
            StanceRow row = uf != null && uf.Stances != null ? uf.Stances.Find(r => r.Name == name) : null;
            return row != null ? row.Count : 1;
        }

        /// <summary>
        /// §5.7 順 7 (review 19): whether `cue` (a GuardBlock or a Hit) is a wall's — and which way the one it struck was pushed
        /// (−1 left, +1 right).
        /// </summary>
        // js: scenes/battle/player.js:539
        private Wall WallOf(DepictionEvent ev, Cue cue)
        {
            if (ev == null || (cue.Kind != CueKind.Hit && cue.Kind != CueKind.GuardBlock)) return null;
            if (ev.Kind == DepictionEventKind.EnemyAction && ev.Title == WallTitle && cue.Target == UnitSide.Player) return new Wall(-1);
            if (ev.Kind != DepictionEventKind.PlayCard || cue.Target != UnitSide.Enemy) return null;
            int i = ev.Cues.IndexOf(cue);
            Cue push = null;
            for (int j = 0; j < i; j++)
            {
                Cue c = ev.Cues[j];
                if (c.Kind == CueKind.RangeSwitch && UnitOf(c) >= 0 && UnitOf(c) == UnitOf(cue)) { push = c; break; }
            }
            if (push == null) return null;
            bool pulls = _eventFace != null && _eventFace.Values != null && _eventFace.Values.Any(v => v.Kind == CardValueKind.Pull);
            return new Wall(pulls ? -1 : 1);
        }

        /// <summary>
        /// The side label waiting for this event's next number, taken by the float that shows it (§5.0 主数字 + 脇ラベル), and
        /// after it the multiplier the blow `cue` carried (「−18 +5 間合い ×1.5 強化」).
        /// </summary>
        // js: scenes/battle/player.js:554
        private string TakeSide(string kind, Cue cue = null)
        {
            string text = "";
            if (_pendingSide != null && (_pendingSide.Kind == kind || _pendingSide.Kind == "any"))
            {
                text = _pendingSide.Text;
                _pendingSide = null;
            }
            string mult = cue != null && _multLabels.TryGetValue(cue, out string m) ? m : "";
            return !string.IsNullOrEmpty(mult) ? (!string.IsNullOrEmpty(text) ? text + " " + mult : mult) : text;
        }

        /// <summary>
        /// §5.0 / §5.6 順 7 / §5.10 (review 21): which blows of `ev` carried a status multiplier, from the event's own cues;
        /// keyed by the cue whose number the blow shows (its Hit, else its GuardBlock, else its Slash with a settled HP).
        /// </summary>
        // js: scenes/battle/player.js:572
        private static Dictionary<Cue, string> MultLabelsOf(DepictionEvent ev)
        {
            var map = new Dictionary<Cue, string>();
            if (ev == null || ev.Cues == null) return map;
            var groups = new List<BlowGroup>();
            for (int i = 0; i < ev.Cues.Count; i++)
            {
                Cue c = ev.Cues[i];
                if (c.Kind == CueKind.Slash)
                {
                    groups.Add(new BlowGroup { At = i, End = i, Slash = c, Number = c.HpAfter != Unchanged ? c : null });
                }
                else if ((c.Kind == CueKind.GuardBlock || c.Kind == CueKind.Hit) && groups.Count > 0)
                {
                    BlowGroup g = groups[groups.Count - 1];
                    if (g.End == i - 1 && g.Slash.Target == c.Target && UnitOf(g.Slash) == UnitOf(c))
                    {
                        g.End = i;
                        g.Number = c;
                    }
                }
            }
            if (groups.Count == 0) return map;
            bool SameUnit(UnitSide side, Cue a, Cue b) => side == UnitSide.Player || UnitOf(a) == UnitOf(b);
            for (int i = 0; i < ev.Cues.Count; i++)
            {
                Cue c = ev.Cues[i];
                if (c.Kind != CueKind.StatusChange || !(c.Amount < 0)) continue;
                string word = c.Text ?? "";
                bool fragile = word == FragileWord;
                if (!fragile && word != EmpowerWord) continue;
                List<BlowGroup> fits = groups.Where(g => (fragile ? g.Slash.Target : EffectPlan.AttackerOf(g.Slash)) == c.Target && SameUnit(c.Target, g.Slash, c)).ToList();
                List<BlowGroup> before = fits.Where(g => g.At < i).ToList();
                int at = i;
                BlowGroup hit = before.Count > 0 ? before[before.Count - 1] : fits.FirstOrDefault(x => x.At > at);
                if (hit == null) continue;
                if (fragile) hit.Fragile = true;
                else hit.Empower = true;
            }
            foreach (BlowGroup g in groups)
            {
                if (g.Number == null || (!g.Fragile && !g.Empower)) continue;
                map[g.Number] = MultLabel[g.Fragile ? FragileWord : EmpowerWord];
            }
            return map;
        }

        /// <summary>
        /// The event is over and its fired trait's label found no number to stand beside: it rises alone, a word on the
        /// chest's path, as 「動けず」 does (§5.0's examples).
        /// </summary>
        // js: scenes/battle/player.js:607
        private void FlushSide()
        {
            PendingSide p = _pendingSide;
            _pendingSide = null;
            if (p == null) return;
            // A blow that found nobody (空振り, §5.8) added nothing: its bonus is struck off with it, and no label rises.
            if (_plannedEvent != null && _plannedEvent.Cues.Any(c => c.Kind == CueKind.SideBonusMiss)) return;
            FigureGeom g = Figure(p.Side, p.Unit);
            if (g == null) return;
            Unawaited(_field.FloatText(new StagePoint(g.Chest.X, g.Chest.Y), p.Text, p.Side == UnitSide.Player ? FxRole.Warm : FxRole.Ink, 32,
                UnderBadges(new FloatOpts { Plain = true, Rise = Math.Max(0f, g.Chest.Y - g.Head.Y), Track = TrackOf(p.Side, p.Unit) })));
        }

        // js: scenes/battle/player.js:619
        private void EndPlan()
        {
            if (_plannedEvent != null && _planned.Count > 0)
            {
                _planErrors++;
                _env.Log.Error("[battle] event " + Str(_plannedEvent.Order) + " " + _plannedEvent.Title
                    + ": EffectPlan lists waits the screen did not hold: " + string.Join(", ", _planned.Select(s => s.Id + " x" + Str(s.Count))));
            }
            _planned = new List<EffectStep>();
            _plannedEvent = null;
        }

        // js: scenes/battle/player.js:628
        private EffectStep TakePlanned(EffectId id, int count)
        {
            if (_plannedEvent == null) return new EffectStep(id, count);
            string where = "[battle] event " + Str(_plannedEvent.Order) + " " + _plannedEvent.Title + ": ";
            if (_planned.Count == 0 || _planned[0].Id != id)
            {
                _planErrors++;
                _env.Log.Error(where + "the screen waits for " + id + " where EffectPlan lists " + (_planned.Count > 0 ? _planned[0].Id.ToString() : "nothing more"));
                return new EffectStep(id, count);
            }
            EffectStep step = _planned[0];
            _planned.RemoveAt(0);
            if (step.Count != count)
            {
                _planErrors++;
                _env.Log.Error(where + id + " plays " + Str(count) + " items where EffectPlan lists " + Str(step.Count));
            }
            return step;
        }

        /// <summary>
        /// HoldBeat: the flow is held for the plan's wait of this step while `play` (a thunk returning a Promise) runs beside
        /// it; a step with a lead holds the lead only and lets the beat play on. `played` runs once the beat has played out.
        /// </summary>
        // js: scenes/battle/player.js:645
        private async Promise HoldBeat(EffectId id, Func<Promise> play, Action settle, int count = 1, Action played = null)
        {
            EffectStep step = TakePlanned(id, count);
            if (!_switches.IsOn(id))
            {
                settle?.Invoke();
                played?.Invoke();
                return;
            }
            float waitMs = EffectPlan.WaitMs(step, _switches);
            if (step.LeadMs.HasValue)
            {
                Safe(play).Then(() => { if (_alive) played?.Invoke(); }).Forget();
                await _clock.Wait(waitMs);
                return;
            }
            Promise waited = _clock.Wait(waitMs);
            await Safe(play);
            await waited;
            played?.Invoke();
        }

        // ---- frame → screen -------------------------------------------------------------------------

        /// <summary>Whether the screen shows the enemy's turn for `frame` (EnemyTurn, held to the result on the deciding turn).</summary>
        // js: scenes/battle/player.js:667
        private bool EnemyTurnOf(DepictionFrame frame)
        {
            if (frame == null) return false;
            return frame.EnemyTurn || (_enemyPhase && frame.Outcome != BattleOutcome.Ongoing);
        }

        // js: scenes/battle/player.js:672
        private void ApplyFrame(DepictionFrame frame, bool first = false)
        {
            if (frame == null) return;
            // Review 21 (§5.12): while an action is held across its events, its badge keeps the omen it acts on.
            HeldAction held = _heldEnd ?? _carried;
            _hud.ApplyFrame(frame, first, held != null ? held.Unit : -1);
            _field.ApplyFrame(frame, first);
            ShowEnemyTurn(EnemyTurnOf(frame));
            // §5.11 順 2 (#501): the body's light while the list holds a stance, faded beside the flow.
            if (!first)
            {
                ApplyStanceGlow(UnitSide.Player, frame.Player, -1);
                IReadOnlyList<UnitFrame> list = EnemyFrames(frame);
                for (int k = 0; k < list.Count; k++)
                    if (!list[k].Down) ApplyStanceGlow(UnitSide.Enemy, list[k], k);
            }
            // §3.5 集中 / 威圧, §5.10 強化 (review 21): the statuses the player holds mark the cards they act on.
            _hand.SetStatusMarks(StatusMarksOf(frame.Player));
            if (!_hand.SameHand(frame.Hand)) _hand.Build(frame.Hand);
            else _hand.Bind(frame.Hand);
            _hand.Layout();
            _frameStanceHint = frame.StanceHint ?? "";
            ShowReserveHint();
            ShowHpLoss();
            ShowLowHp(frame.Player != null ? frame.Player.Hp : 0);
        }

        /// <summary>The player's 集中 / 強化 / 威圧 chips (UnitFrame.Statuses, by their labels) as the hand's marks.</summary>
        // js: scenes/battle/player.js:697
        private static StatusMarks StatusMarksOf(UnitFrame uf)
        {
            var held = new HashSet<string>(StringComparer.Ordinal);
            if (uf != null && uf.Statuses != null)
                foreach (StatusChip c in uf.Statuses)
                    if (c != null && c.Stacks > 0 && c.Label != null) held.Add(c.Label);
            return new StatusMarks
            {
                Focus = held.Contains(BattleCore.EnumTokens.ToLabel(BattleCore.StatusKind.Focus)),
                Empower = held.Contains(EmpowerWord),
                Intimidate = held.Contains(BattleCore.EnumTokens.ToLabel(BattleCore.StatusKind.Intimidate)),
            };
        }

        /// <summary>§5.6 順 5: the low-HP vignette rests while the player's HP is under 15 (and above 0).</summary>
        // js: scenes/battle/player.js:706
        private void ShowLowHp(int hp)
        {
            Unawaited(_field.SetLowHp(hp > 0 && hp < L.HeartbeatHp));
        }

        /// <summary>§5.6 順 5: a blow that landed on the player with HP under 15 pulses the vignette (beside the flow).</summary>
        // js: scenes/battle/player.js:714
        private void PulseLowHp(int hp)
        {
            ShowLowHp(hp);
            if (hp > 0 && hp < L.HeartbeatHp) Safe(() => _field.PulseLowHp(L.VignettePulseMs)).Forget();
        }

        /// <summary>The enemy a held single-target card is read on now (the one it frames; −1 for a self card).</summary>
        // js: scenes/battle/player.js:724
        private int HeldSlot()
        {
            return _heldFace != null && _heldFace.Aim == CardAim.Single && _heldUnit >= 0 ? _heldUnit : -1;
        }

        /// <summary>§4.7 / §5.2: "▸ n" and the pay rings: the held card's cost on the enemy it frames.</summary>
        // js: scenes/battle/player.js:728
        private void ShowHeldCost()
        {
            if (_heldFace == null)
            {
                _hud.Stamina.ShowCost(0);
                return;
            }
            int u = HeldSlot();
            _hud.Stamina.ShowCost(u >= 0 ? _heldFace.CostFor(u) : _heldFace.Cost);
        }

        /// <summary>§4.9 / §5.3: while a card is held, the 構えの予告 reads as it would once the card is paid for.</summary>
        // js: scenes/battle/player.js:734
        private void ShowReserveHint()
        {
            int u = HeldSlot();
            string afterPay = _heldFace == null ? null : u >= 0 ? _heldFace.StanceHintAfterPayFor(u) : _heldFace.StanceHintAfterPay;
            // battle-visual-v1 §4.9 「敵の番には出しません」 (review 19).
            _hud.ReserveHint(_enemyTurnShown ? "" : afterPay ?? _frameStanceHint);
        }

        /// <summary>
        /// §4.5 HP の減る分: the wick stripes on an enemy's HP bar while a card is held (CardFace.HpLossFor(k)); with several, a
        /// card aimed at one enemy wicks the enemy it is framed on only, one aimed at nobody wicks each by its own.
        /// </summary>
        // js: scenes/battle/player.js:746
        private void ShowHpLoss()
        {
            DepictionFrame frame = FrameNow;
            IReadOnlyList<UnitFrame> list = EnemyFrames(frame);
            bool aimed = _heldFace != null && _heldFace.Aim == CardAim.Single;
            int framed = aimed ? _hand.FramedUnit() : -1;
            if (aimed && framed < 0) framed = _heldFace.TargetUnit >= 0 ? _heldFace.TargetUnit : 0;
            // Several enemies: the floor's one N is the framed enemy's while the card is up, else the Tab's, else the nearest.
            if (_restingGapUnit != null)
            {
                bool stands = false;
                for (int k = 0; k < list.Count; k++)
                    if (k == _restingGapUnit.Value && !list[k].Down) { stands = true; break; }
                if (!stands) _restingGapUnit = null;
            }
            _field.ShowGapTo(list.Count > 1 ? (aimed ? framed : _restingGapUnit ?? -1) : -1);
            // Review 19: the enemy a Tab at rest chose wears the battle's focus, the amber edge, while nothing is held.
            _hud.SetRestFocus(list.Count > 1 && _heldFace == null && _restingGapUnit != null ? _restingGapUnit.Value : -1);
            if (list.Count <= 1)
            {
                _hud.Status(UnitSide.Enemy).ShowHpLoss(_heldFace != null ? _heldFace.HpLossFor(0) : 0);
                return;
            }
            for (int k = 0; k < list.Count; k++)
            {
                int u = k;
                int n = _heldFace != null && (!aimed || u == framed) ? _heldFace.HpLossFor(u) : 0;
                _hud.Status(UnitSide.Enemy, u).ShowHpLoss(n);
            }
        }

        /// <summary>The badge of each enemy that shows the held / selected card's prediction tag stands higher for it.</summary>
        // js: scenes/battle/player.js:774
        private void LiftOmenForHeld()
        {
            _hud.LiftOmen(_heldFace != null ? _hand.TaggedUnits() : Array.Empty<int>());
        }

        /// <summary>UpdateFloorReach: the cells an enemy-aimed card reaches light while it is hovered (not busy) or held.</summary>
        // js: scenes/battle/player.js:780
        private void UpdateFloorReach()
        {
            CardFace face = _heldFace ?? (!_busy ? _hoveredFace : null);
            if (face == null || face.Aim != CardAim.Single) face = null;
            if (face == _reachFor) return;
            _reachFor = face;
            if (face != null) _field.ShowReach((IReadOnlyList<int>)face.ReachCells ?? Array.Empty<int>());
            else _field.HideReach();
        }

        /// <summary>ShowEnemyTurn: the shade below y 790 and the end-turn plate at 40%; the 構えの予告 is off meanwhile (§4.9).</summary>
        // js: scenes/battle/player.js:790
        private void ShowEnemyTurn(bool on)
        {
            _field.SetEnemyTurnShade(on);
            _hud.SetEnemyTurn(on);
            if (on != _enemyTurnShown)
            {
                _enemyTurnShown = on;
                ShowReserveHint();
            }
        }

        // ---- the panels and the badge follow the figures (FollowFigures / FollowPanels) --------------

        /// <summary>Where the panels follow a figure: its home, but the figure itself while it walks on the floor (review fix).</summary>
        // js: scenes/battle/player.js:805
        private static float FollowX(FigureGeom g) => g.Walking ? g.X : g.HomeX;

        // js: scenes/battle/player.js:806
        private void FollowFigures()
        {
            DepictionFrame frame = FrameNow;
            if (frame == null) return;
            FigureGeom p = Figure(UnitSide.Player);
            FigureGeom e = Figure(UnitSide.Enemy);
            if (p == null || e == null) return;
            var units = new List<FollowUnit>();
            IReadOnlyList<EnemyFrame> list = EnemyShares(frame);
            for (int k = 0; k < list.Count; k++)
            {
                EnemyFrame uf = list[k];
                int u = k;
                FigureGeom g = Figure(UnitSide.Enemy, u);
                if (g != null)
                    units.Add(new FollowUnit { Unit = u, X = FollowX(g), Top = L.FeetY - g.Height, Size = Math.Max(1, uf.Size > 0 ? uf.Size : 1), Down = uf.Unit.Down || g.Down, Walking = g.Walking });
            }
            _hud.Follow(new FollowArgs
            {
                PlayerX = FollowX(p),
                PlayerWalking = p.Walking,
                EnemyX = FollowX(e),
                EnemyTop = L.FeetY - e.Height,
                Cells = frame.Floor != null ? frame.Floor.Cells : 0,
                EnemySize = frame.Floor != null ? Math.Max(1, frame.Floor.EnemySize) : 1,
                Units = units,
                Snap = _snapPanels,
            });
            _snapPanels = false;
        }

        // ---- stance glows (§5.11 順 2, #501) ----------------------------------------------------------

        // js: scenes/battle/player.js:833
        private void ApplyStanceGlow(UnitSide side, UnitFrame unitFrame, int unit)
        {
            bool shown = _field.StanceGlowShown(side, unit);
            StanceGlowChange change = StanceGlow.Change(shown, unitFrame);
            if (change == StanceGlowChange.Keep) return;
            bool on = change == StanceGlowChange.Light;
            Effect(EffectId.StanceGlow, () => _field.FadeStanceGlow(side, on, Ms(EffectId.StanceGlow), unit), () => _field.SetStanceGlow(side, on, unit)).Forget();
        }

        // js: scenes/battle/player.js:841
        private void LightStanceGlowWithCrest(DepictionEvent ev, Cue cue)
        {
            if (cue.Fx != FxSystem.Form || ev.After == null) return;
            if (cue.Target == UnitSide.Player)
            {
                ApplyStanceGlow(UnitSide.Player, ev.After.Player, -1);
                return;
            }
            int u = UnitOf(cue);
            UnitFrame uf = null;
            if (u >= 0)
            {
                IReadOnlyList<UnitFrame> list = EnemyFrames(ev.After);
                for (int k = 0; k < list.Count; k++)
                    if (k == u) { uf = list[k]; break; }
            }
            else uf = ev.After.Enemy;
            ApplyStanceGlow(UnitSide.Enemy, uf ?? ev.After.Enemy, u);
        }

        // ---- flow -------------------------------------------------------------------------------------

        // js: scenes/battle/player.js:851
        private async Promise RunAutomaticEvents()
        {
            _busy = true;
            SetState(BattlePlayerState.Auto);
            SetHandInteractable(false);
            UpdateFloorReach();
            DepictionEvent next = null;
            while (_alive && !_halted && (next != null || (!Src.Finished && !Src.WaitingForPlayer)))
            {
                DepictionEvent ev = next;
                next = null;
                if (ev == null)
                {
                    try { ev = _ctx.Source.AdvanceAuto(); }
                    catch (Exception err) { SourceFailed(err); return; }
                }
                // §3.11 / §5.13: enemies act one after another with a 600 ms pause between them (none between an elite's
                // two actions): the wait after the last enemy's event is made up to it.
                if (StartsOtherEnemy(ev))
                {
                    await _clock.Wait(Math.Max(0f, L.BetweenEnemiesMs - EffectFlow.AfterAutoEventMs));
                    if (!_alive) return;
                }
                await PlayEvent(ev);
                if (!_alive) return;
                if (_heldEnd != null)
                {
                    // §5.12 (review 21): the next event is looked at before the pause — the same action going on plays on
                    // from where this one left the enemy; anything else first lets the action end.
                    if (!Src.Finished && !Src.WaitingForPlayer)
                    {
                        try { next = _ctx.Source.AdvanceAuto(); }
                        catch (Exception err) { SourceFailed(err); return; }
                    }
                    if (next != null && ContinuesHeld(next))
                    {
                        _carried = _heldEnd;
                        _heldEnd = null;
                        await AfterWait(ContinuePauseMs);
                        if (!_alive) return;
                        EventEnded();
                        continue;
                    }
                    await PlayHeldEnd();
                    if (!_alive) return;
                }
                await AfterWait(EffectFlow.AfterAutoEventMs);
                if (!_alive) return;
                EventEnded();
            }
            _busy = false;
            if (!_alive || _halted) return;
            if (Src.Finished)
            {
                await Finish();
                return;
            }
            if (_turnStarted)
            {
                _turnStarted = false;
                _turnOpenedAt = _real.Now;
            }
            _askedAt = _real.Now;
            SetState(BattlePlayerState.Idle);
            OpenFreeStep();
            SetHandInteractable(true);
            RefreshPlayable();
            ScheduleAuto();
        }

        /// <summary>The event opens another enemy's phase than the one that played last in this enemy turn (several enemies).</summary>
        // js: scenes/battle/player.js:908
        private bool StartsOtherEnemy(DepictionEvent ev)
        {
            if (ev == null) return false;
            if (ev.Kind != DepictionEventKind.EnemyAction && ev.Kind != DepictionEventKind.NextOmen)
            {
                if (ev.Kind != DepictionEventKind.Defeat) _lastEnemyUnit = null;
                return false;
            }
            int u = UnitOf(ev);
            if (u < 0 || !MultiNow()) return false;
            bool other = _lastEnemyUnit != null && u != _lastEnemyUnit.Value;
            _lastEnemyUnit = u;
            return other;
        }

        /// <summary>`ev` goes on with the held action: the same enemy's event titled after it (「二段斬り（2 撃目）」, 「退き撃ち（下がる）」).</summary>
        // js: scenes/battle/player.js:922
        private bool ContinuesHeld(DepictionEvent ev)
        {
            HeldAction h = _heldEnd;
            if (h == null || ev == null || ev.Kind != DepictionEventKind.EnemyAction || string.IsNullOrEmpty(h.Title)) return false;
            int u = UnitOf(ev);
            if (u >= 0 && h.Unit >= 0 && u != h.Unit) return false;
            return (ev.Title ?? "").StartsWith(h.Title + "（", StringComparison.Ordinal);
        }

        /// <summary>Whether enemy `u` (a PlayEvent unit: −1 with one enemy) is the one whose action is held.</summary>
        // js: scenes/battle/player.js:930
        private bool IsHeld(int u)
        {
            return _heldEnd != null && (u == -1 || _heldEnd.Unit == -1 || u == _heldEnd.Unit);
        }

        /// <summary>
        /// The held action ends (review 21): its side label rises if no number took it, its badge is spent and it steps back
        /// home — the OmenSpend and EnemyReturn beats its blow's event left out (Hit), as long as they would have held there.
        /// </summary>
        // js: scenes/battle/player.js:937
        private async Promise PlayHeldEnd()
        {
            HeldAction h = _heldEnd ?? _carried;
            _heldEnd = null;
            _carried = null;
            if (h == null) return;
            if (h.Side != null)
            {
                _pendingSide = h.Side;
                FlushSide();
            }
            IOmenBadge badge = _hud.OmenOf(h.Unit);
            await Effect(EffectId.OmenSpend, () => badge.FadeOut(Ms(EffectId.OmenSpend)), () => badge.HideNow());
            if (!_alive) return;
            int u = h.Unit;
            if (_field.EnemyAway(u)) await Effect(EffectId.EnemyReturn, () => _field.ReturnHome(UnitSide.Enemy, Ms(EffectId.EnemyReturn), u), () => _field.SnapHome(UnitSide.Enemy, u));
            else _field.SetPose(UnitSide.Enemy, FigurePose.Idle, u);
        }

        // js: scenes/battle/player.js:951
        private async Promise PlayEvent(DepictionEvent ev)
        {
            BeginPlan(ev);
            // The held action goes on (review 21): the label its first event's trait left waits for this event's number.
            if (_carried != null && _carried.Side != null)
            {
                _pendingSide = _carried.Side;
                _carried.Side = null;
            }
            if (ev.Kind == DepictionEventKind.TurnStart) _enemyPhase = false;
            else if (ev.Kind == DepictionEventKind.TurnEnd) _enemyPhase = true;
            else if (ev.After != null && !ev.After.EnemyTurn && ev.After.Outcome == BattleOutcome.Ongoing) _enemyPhase = false;
            // §4.4 敵の番: the acting light goes out with any other event, and when another enemy's phase begins.
            if (ev.Kind != DepictionEventKind.EnemyAction || (_actingUnit != null && UnitOf(ev) != _actingUnit.Value))
            {
                _hud.Omen.SetActing(false);
                _actingUnit = null;
            }
            // §5.5: the screen comes back up as the event that ends the enemy's turn starts.
            if (ev.After != null && !EnemyTurnOf(ev.After)) ShowEnemyTurn(false);
            if (ev.Kind == DepictionEventKind.TurnStart)
            {
                _playedThisTurn = 0;
                _turnStarted = true;
                // Review fix (§2.2 「静止時はいちばん近い敵」): a Tab choice of the floor's N holds for the turn it was made in.
                _restingGapUnit = null;
                int hp = ev.After != null ? ev.After.Player.Hp : 0;
                if (hp > 0 && hp < L.HeartbeatHp) for (int k = 0; k < 3; k++) Sfx("heartbeat", k * 900);
                await HoldBeat(EffectId.TurnBanner, () => _hud.Banner(DepictionText.YourTurn, BannerSide.Player, Ms(EffectId.TurnBanner)), null);
            }
            // §5.1 順 8 「順 6 の後」 (review 16): the standing omens blink once the turn start's draw has played.
            bool blinked = ev.Kind != DepictionEventKind.TurnStart;
            foreach (Cue cue in ev.Cues.ToArray())
            {
                if (!_alive) return;
                await PlayCue(ev, cue);
                if (!blinked && cue.Kind == CueKind.DrawHand)
                {
                    blinked = true;
                    BlinkStanding(ev);
                }
            }
            if (!_alive) return;
            if (!blinked) BlinkStanding(ev);
            if (ev.Kind == DepictionEventKind.TurnEnd)
            {
                Sfx("foeTurn");
                // battle-visual-v1 §4.9 / §5.5 (review 19): the enemy's turn begins with its banner.
                if (ev.After == null || ev.After.Outcome == BattleOutcome.Ongoing) ShowEnemyTurn(true);
                await HoldBeat(EffectId.EnemyTurnBanner, () => _hud.Banner(DepictionText.EnemyTurn, BannerSide.Enemy, Ms(EffectId.EnemyTurnBanner)), null);
            }
            // §4.2: with a floor, home is each enemy's cell after the event, so the step back carries it there.
            bool floorShown = ev.After != null && ev.After.Floor != null && ev.After.Floor.Cells > 0;
            IReadOnlyList<EnemyFrame> after = EnemyShares(ev.After);
            var list = new List<(int U, EnemyFrame Uf)>();
            if (after.Count > 1) for (int k = 0; k < after.Count; k++) list.Add((k, after[k]));
            else list.Add((-1, after.Count > 0 ? after[0] : null));
            foreach (var (u, uf) in list)
            {
                if (floorShown)
                {
                    if (u == -1) _field.SetEnemyHomeCell(ev.After.Floor.EnemyCell, ev.After.Floor.EnemySize);
                    else if (uf != null && !uf.Unit.Down) _field.SetEnemyHomeCell(uf.Cell, uf.Size, u);
                }
                // Review 21: an enemy whose action is held stays where its blow left it until the next event says whether it goes on.
                if (IsHeld(u)) continue;
                if (_field.EnemyAway(u))
                {
                    Effect(EffectId.EnemyReturn, () => _field.ReturnHome(UnitSide.Enemy, Ms(EffectId.EnemyReturn), u), () => _field.SnapHome(UnitSide.Enemy, u)).Forget();
                }
                else _field.SetPose(UnitSide.Enemy, FigurePose.Idle, u);
            }
            _field.SetPose(UnitSide.Player, FigurePose.Idle);
            // A label still waiting goes with the held action (退き撃ち's 「+3 温存」 belongs to the Guard of its step back).
            if (_heldEnd != null && _pendingSide != null)
            {
                _heldEnd.Side = _pendingSide;
                _pendingSide = null;
            }
            FlushSide();
            EndPlan();
            ApplyFrame(ev.After);
            // The event went on with a held action and did not hold it again: the action ends here.
            if (_carried != null && _heldEnd == null) await PlayHeldEnd();
            else _carried = null;
        }

        /// <summary>
        /// The turn start's omen blink (§5.1 順 8): every omen already standing blinks once, 120 apart in the enemies' action
        /// order; one shown in this event pops instead.
        /// </summary>
        // js: scenes/battle/player.js:1022
        private void BlinkStanding(DepictionEvent ev)
        {
            var popping = new HashSet<int>(ev.Cues.Where(c => c.Kind == CueKind.OmenShow).Select(c => UnitOf(c) >= 0 ? UnitOf(c) : 0));
            IReadOnlyList<OmenFrame> omens = ev.After != null
                ? (ev.After.Enemies != null && ev.After.Enemies.Count > 0 ? (IReadOnlyList<OmenFrame>)ev.After.Enemies.Select(e => e.Omen).ToList() : new[] { ev.After.Omen })
                : Array.Empty<OmenFrame>();
            bool anyShown = ev.Cues.Any(c => c.Kind == CueKind.OmenShow);
            int blinkAt = 0;
            for (int k = 0; k < omens.Count; k++)
            {
                OmenFrame o = omens[k];
                if (o == null || !o.Visible) continue;
                int u = k;
                if (omens.Count > 1 ? popping.Contains(u) : anyShown) continue;
                float delay = blinkAt * L.OmenBlinkStaggerMs;
                blinkAt += 1;
                IOmenBadge badge = _hud.OmenOf(omens.Count > 1 ? u : -1);
                Effect(EffectId.OmenBlink, () => (delay > 0 ? _clock.Wait(delay) : Promise.Resolved())
                    .Then((Func<Promise>)(() => _alive ? badge.Blink(Ms(EffectId.OmenBlink)) : null))).Forget();
            }
        }

        // js: scenes/battle/player.js:1038
        private async Promise PlayCue(DepictionEvent ev, Cue cue)
        {
            UnitSide T = cue.Target;
            // The enemy of the exchange (Cue.Unit); the target's unit when the target is an enemy.
            int foe = UnitOf(cue);
            int tu = T == UnitSide.Enemy ? foe : -1;
            IStatusPanel S = _hud.Status(T, tu);
            FigureGeom fig = Figure(T, tu) ?? new FigureGeom { Chest = new StagePoint(960, 423), Head = new StagePoint(960, 380) };
            StagePoint chest = fig.Chest;
            StagePoint head = fig.Head;
            // §5.0 / the DS Notices (review 19): every floating number and word starts on the chest and rises to the head in 600 ms.
            var numberAt = new StagePoint(chest.X, chest.Y);
            StagePoint labelAt = numberAt;
            float rise = Math.Max(0f, chest.Y - head.Y);
            // Review fix (§5.7 順 6): a number or a label rides with the figure it is about.
            FloatOpts under = UnderBadges(new FloatOpts { Track = TrackOf(T, tu), Rise = rise });
            // §5.7 順 7 (review 19): a wall's blow — the edge it hit lights, the figure takes 打's ripple, the number says 壁.
            Wall wall = WallOf(ev, cue);

            // §5.3 (#57): a beat of a play draws its system's shape beside it.
            switch (EffectGrammar.MarkOf(cue))
            {
                case FxMark.Shape:
                {
                    bool crestLight = StanceGlow.CrestCarriesLight(_field.StanceGlowShown(T, tu));
                    Effect(EffectId.GrammarMark, () => _field.Mark(T, new MarkOpts { Fx = cue.Fx, Intensity = cue.Intensity, Ms = Ms(EffectId.GrammarMark), CrestLight = crestLight, Unit = tu })).Forget();
                    LightStanceGlowWithCrest(ev, cue);
                    break;
                }
                case FxMark.ChestHeave:
                    Effect(EffectId.BreathHeave, () => _field.Heave(T, Ms(EffectId.BreathHeave), tu)).Forget();
                    break;
            }

            switch (cue.Kind)
            {
                case CueKind.GuardReset:
                    S.SetGuard(cue.GuardAfter);
                    break;

                case CueKind.StaminaChange:
                    if (cue.Amount > 0)
                    {
                        if (T == UnitSide.Player) for (int k = 0; k < cue.Amount; k++) Sfx("pip", k * Ms(EffectId.StaminaRecover));
                        await HoldBeat(EffectId.StaminaRecover,
                            () => S.LightPips(cue.StaminaAfter, cue.StaminaMax, Ms(EffectId.StaminaRecover)),
                            () => S.SetStamina(cue.StaminaAfter, cue.StaminaMax), cue.Amount);
                    }
                    else
                    {
                        S.SetStamina(cue.StaminaAfter, cue.StaminaMax);
                        Effect(EffectId.StaminaChange, () => S.PopPips(Ms(EffectId.StaminaChange))).Forget();
                    }
                    break;

                case CueKind.StatusChange:
                {
                    EffectId beat = StatusBeat.Of(cue);
                    if (beat == EffectId.StatusApply && cue.Fx == FxSystem.Spirit) SfxBudget("seal");
                    await HoldBeat(beat, () => S.PlayStatus(cue.Text, cue.StacksAfter, beat, Ms(beat)), () => S.SetStatus(cue.Text, cue.StacksAfter));
                    break;
                }

                case CueKind.DrawHand:
                    await DrawHand(ev.After.Hand, cue.Amount, cue.RebuildAfter);
                    break;

                case CueKind.OmenShow:
                {
                    IOmenBadge badge = _hud.OmenOf(foe);
                    OmenFrame shownOmen = OmenFrameOf(ev.After, foe);
                    _hud.SyncOmenLook(ev.After, foe >= 0 ? foe : 0);
                    badge.Bind(shownOmen);
                    Sfx("omenDrop");
                    // §5.8 提示: the floor's aimed cells are drawn left to right as the badge drops (30 ms a cell, scene addition).
                    Effect(EffectId.OmenShow, () => _field.DrawAim(ev.After, foe), () => _field.DrawAim(ev.After, foe, true)).Forget();
                    await HoldBeat(EffectId.OmenShow, () => badge.PopIn(Ms(EffectId.OmenShow)), () => badge.Bind(shownOmen));
                    break;
                }

                case CueKind.TraitFire:
                {
                    if (cue.Fx == FxSystem.Form) SfxBudget("stance");
                    string text = cue.Text ?? "";
                    if (text.StartsWith(StanceSetPrefix, StringComparison.Ordinal))
                    {
                        // §5.11 順 1 / 順 8 (review 19): a stance set is told by where it goes; no line over the head.
                        await HoldBeat(EffectId.TraitFire, () =>
                        {
                            if (T == UnitSide.Enemy) Safe(() => _hud.FlyOmenToStances(foe, L.StanceFlyMs)).Forget();
                            return _clock.Wait(Ms(EffectId.TraitFire));
                        }, null);
                        break;
                    }
                    TraitInfo trait = TraitOf(ev, cue);
                    if (trait != null)
                    {
                        // §5.9 順 1 / 順 12 (review 19): the player's card trait left with the card (hand.Release); an enemy's
                        // swells its condition word at the right end of its badge. The number it adds to carries the label.
                        _pendingSide = new PendingSide { Text = trait.Side, Kind = trait.Kind, Side = T, Unit = tu };
                        await HoldBeat(EffectId.TraitFire, () =>
                        {
                            if (T == UnitSide.Enemy && trait.Condition.Length > 0)
                            {
                                IOmenBadge b = _hud.OmenOf(foe);
                                Safe(() => b.TraitWord(trait.Condition, L.TraitWordMs)).Forget();
                            }
                            return _clock.Wait(Ms(EffectId.TraitFire));
                        }, null);
                        break;
                    }
                    await HoldBeat(EffectId.TraitFire, () => FireTrait(T, labelAt, cue.Text, tu, rise), null);
                    break;
                }

                case CueKind.Slash:
                    await Attack(ev, cue, chest, numberAt, under);
                    break;

                case CueKind.GuardGain:
                    SfxBudget("guardUp");
                    await HoldBeat(EffectId.GuardGain, () =>
                    {
                        _field.Burst(chest, FxRole.Guard, 220 + 40 * cue.Intensity);
                        FloatOpts o = Copy(under);
                        o.Side = TakeSide("guard");
                        _field.FloatText(numberAt, "+" + Str(cue.Amount), FxRole.Guard, BattleLayout.NumberFont(cue.Intensity), o);
                        return S.PopGuard(cue.GuardAfter, Ms(EffectId.GuardGain));
                    }, () => S.SetGuard(cue.GuardAfter));
                    break;

                case CueKind.RangeSwitch:
                    // Moved by the other side (a shove): a knock-back, and the muted label says so.
                    if (cue.Pushed)
                    {
                        var pushed = PushedFloat(cue, labelAt, under);
                        FloatEffect(EffectId.PushMark, pushed.At, pushed.Word, FxRole.Ink2, 36, pushed.O);
                    }
                    if (EffectPlan.MovesOnFloor(cue))
                    {
                        // §5.7 順 1〜2 (#586): the walk, 160 a cell, then the gap number counted again on arrival.
                        int cells = EffectPlan.CellsOf(cue);
                        await HoldBeat(EffectId.FloorMove, () => _field.MoveOnFloor(cue, cells, _switches.WaitMs(EffectId.FloorMove, cells)), () => _field.SnapOnFloor(cue), cells);
                        var to = RecountTo(ev, cue);
                        await HoldBeat(EffectId.GapRecount,
                            () => _field.RecountGap(to.Gap, cue.PlayerCellAfter, PlayerSize(), to.Cell, Ms(EffectId.GapRecount)),
                            () => _field.SetGap(to.Gap, cue.PlayerCellAfter, PlayerSize(), to.Cell));
                    }
                    else
                    {
                        // No floor (the v4.2 sources): the core always writes one, so this is the plan's two beats held as waits.
                        await HoldBeat(EffectId.RangeSwitch, () => _clock.Wait(Ms(EffectId.RangeSwitch)), null);
                        await HoldBeat(EffectId.RangeTagFlip, () => _clock.Wait(Ms(EffectId.RangeTagFlip)), null);
                    }
                    break;

                case CueKind.StanceCue:
                    // §5.13 順 1 (#501): pips 1-3 glow first (構え's 息), then the shield and "+3" (順 2).
                    SfxBudget("breath");
                    await HoldBeat(EffectId.ReservePips, () => T == UnitSide.Player ? _hud.Stamina.GlowReserve(Ms(EffectId.ReservePips)) : _clock.Wait(Ms(EffectId.ReservePips)), null);
                    SfxBudget("reserve");
                    await HoldBeat(EffectId.StanceCue, async () =>
                    {
                        await _hud.EndTurn.Pop(200);
                        _field.Burst(chest, FxRole.Guard, 300);
                        Unawaited(_field.FloatText(numberAt, cue.Text, FxRole.Guard, 48, under));
                        await S.PopGuard(cue.GuardAfter, 260);
                    }, () => S.SetGuard(cue.GuardAfter));
                    // battle-visual-v1 §4.9 (review 19): the 構えの予告 has been granted — it goes now, not after the banner.
                    if (T == UnitSide.Player) _hud.ReserveHint("");
                    break;

                case CueKind.DiscardHand:
                    await DiscardHand();
                    break;

                case CueKind.EnemyWindup:
                {
                    SetHandInteractable(false);
                    // The moment the enemy does what it foretold: the omen lights up and flares beside its move.
                    IOmenBadge badge = _hud.OmenOf(foe);
                    if (_actingUnit != null && _actingUnit.Value != foe) _hud.Omen.SetActing(false);
                    _actingUnit = foe >= 0 ? foe : 0;
                    // §3.10 (review fix): an elite's or a boss's second action — the plan drops into the lower slot (150 ms).
                    Safe(() => badge.PromotePlan(_switches.IsOn(EffectId.OmenShow) ? PlanDropMs : 0)).Forget();
                    badge.SetActing(true);
                    Effect(EffectId.OmenExecute, () => badge.Flare(Ms(EffectId.OmenExecute))).Forget();
                    await HoldBeat(EffectId.EnemyMotion, () => _field.EnemyMotion(cue.System, Ms(EffectId.EnemyMotion), foe), () => _field.SetPose(UnitSide.Enemy, FigurePose.Act, foe));
                    break;
                }

                case CueKind.Defeat:
                {
                    // #288 / #349: the fallen enemy's omen and plan go with it — and (review 17) its aimed cells on the floor.
                    if (T == UnitSide.Enemy)
                    {
                        IOmenBadge badge = _hud.OmenOf(foe);
                        Effect(EffectId.OmenSpend, () => badge.FadeOutAll(Ms(EffectId.OmenSpend)), () => badge.HideAllNow()).Forget();
                        _field.RetireAimOf(foe);
                    }
                    // §5.15 順 5 (scene addition): one enemy falls while others stand — 倒れ 800 → the foot light 300 → grey 300.
                    bool othersStand = T == UnitSide.Enemy && MultiNow() && EnemyFrames(ev.After).Where((uf, k) => !uf.Down && k != foe).Any();
                    if (!othersStand)
                    {
                        await HoldBeat(EffectId.Defeat, () => _field.Fall(T, Ms(EffectId.Defeat), tu), () => _field.SetDown(T, true, tu));
                        break;
                    }
                    await HoldBeat(EffectId.Defeat, () => _field.Fall(T, Ms(EffectId.Defeat), tu, true), () => _field.SetDown(T, true, tu));
                    if (!_alive) return;
                    if (_switches.IsOn(EffectId.Defeat))
                    {
                        await Safe(() => _field.FadeFoot(T, L.FallFootMs, tu));
                        if (!_alive) return;
                        await Safe(() => S.GreyOut(L.FallGreyMs));
                    }
                    else
                    {
                        _field.SetDown(T, true, tu);
                        S.SetDown(true);
                    }
                    break;
                }

                case CueKind.SideBonusMiss:
                    await HoldBeat(EffectId.SideBonusMiss, () =>
                    {
                        StageBox? box = _env.Geom.OmenBox != null ? _env.Geom.OmenBox(foe) : (StageBox?)null;
                        StagePoint at = box.HasValue ? new StagePoint(box.Value.X + box.Value.W / 2, box.Value.Y + box.Value.H / 2 + 64) : labelAt;
                        _field.FloatText(at, cue.Text, FxRole.Whiff, 40, new FloatOpts { Struck = true, Rise = -50, Lowest = FloatFloor });
                        return _hud.OmenOf(foe).StrikeSide(Ms(EffectId.SideBonusMiss));
                    }, () => _hud.OmenOf(foe).StrikeSideNow());
                    break;

                case CueKind.GuardBlock:
                {
                    // The Guard's number is its own colour, and a light stands in for the sound (#76).
                    S.SetGuard(cue.GuardAfter);
                    int at = ev.Cues.IndexOf(cue);
                    Cue next = at + 1 < ev.Cues.Count ? ev.Cues[at + 1] : null;
                    bool bodyFollows = next != null && next.Kind == CueKind.Hit && next.Target == T;
                    if (!bodyFollows) SfxBudget(cue.GuardAfter == 0 ? "guardBreak" : "hitGuard", true);
                    if (cue.GuardAfter == 0) Effect(EffectId.GuardBreak, () => S.CrackGuard(Ms(EffectId.GuardBreak))).Forget();
                    else Safe(() => S.PopGuard(cue.GuardAfter, 260)).Forget();
                    // §5.6 順 2〜3: "Guard −n" on the side the blow came from, the HP number that follows beside it on the
                    // figure's back, the pair centred on the chest; alone (Guard took it all), centred.
                    int blowSide = T == UnitSide.Player ? 1 : -1;
                    float half = HpBesideGuardGap / 2;
                    StagePoint tagAt = bodyFollows ? new StagePoint(numberAt.X + blowSide * half, numberAt.Y) : numberAt;
                    FloatAnchor anchor = bodyFollows ? (blowSide > 0 ? FloatAnchor.Left : FloatAnchor.Right) : FloatAnchor.None;
                    _guardTag = new GuardTag { Side = T, Unit = foe, Away = -blowSide, Span = null };
                    GuardTag mark = _guardTag;
                    if (wall != null) WallBeat(T, wall, tu);
                    // Guard took it all: the tag carries the label (the trait's, or 壁) — else the HP number beside it does.
                    string tagSide = bodyFollows ? "" : wall != null ? WallSide : TakeSide("hp", cue);
                    FloatOpts o = Copy(under);
                    o.Anchor = anchor;
                    o.Side = tagSide;
                    o.OnPlaced = span => { mark.Span = span; };
                    FloatEffect(EffectId.GuardNumber, tagAt, "Guard −" + Str(cue.Amount) /* scene text (DepictionPlayer) */, FxRole.Guard, L.GuardNumberSize, o);
                    await HoldBeat(EffectId.GuardBlock, () => _field.ShieldBlock(T, Str(cue.Amount), Ms(EffectId.GuardBlock), tu, cue.GuardAfter == 0), null);
                    break;
                }

                case CueKind.HpChange:
                {
                    // 出血 / 再生 / a heal / a 見切り return (#188): the cause and the number (the typographic minus), then the bar.
                    string signed = cue.Amount > 0 ? "+" + Str(cue.Amount) : cue.Amount < 0 ? Minus + Str(-cue.Amount) : Str(cue.Amount);
                    FloatEffect(EffectId.DamageNumber, numberAt, cue.Text + " " + signed, cue.Amount > 0 ? FxRole.Heal : FxRole.Omen, 40, Copy(under));
                    await DrainHp(T, cue.HpAfter, tu);
                    break;
                }

                case CueKind.Hit:
                {
                    bool strong = EffectStrength.IsStrong(cue.Intensity);
                    if (T == UnitSide.Player) SfxBudget(wall != null ? "hitBlunt" : "hitBody", true);
                    // After a Guard block on the same figure the number stands beside its "Guard −n" tag, on the figure's back.
                    int idx = ev.Cues.IndexOf(cue);
                    bool blocked = false;
                    for (int j = 0; j < idx; j++)
                    {
                        Cue c = ev.Cues[j];
                        if (c.Kind == CueKind.GuardBlock && c.Target == T && UnitOf(c) == foe) { blocked = true; break; }
                    }
                    GuardTag tag = blocked && _guardTag != null && _guardTag.Side == T && _guardTag.Unit == foe ? _guardTag : null;
                    if (wall != null && !blocked) WallBeat(T, wall, tu);
                    // §5.0 (review 19): the number is the HP the blow took, with the trait's or the wall's side label.
                    string shown = Minus + Str(HpLostOf(S, cue));
                    string side = wall != null ? WallSide : TakeSide("hp", cue);
                    // §5.6 順 3 「順 2 と横に 24 px 並ぶ」: beside the "Guard −n" tag wherever it rose.
                    BesideTag besideTag = tag != null && tag.Span != null ? HpBesideTag(tag, shown, BattleLayout.NumberFont(cue.Intensity), side) : null;
                    if (besideTag != null)
                    {
                        // The pair reads side by side: the HP number rides at the tag's height as the tag rises.
                        Func<float> followY = tag.Span.YNow;
                        FloatOpts o = Copy(under);
                        o.Side = side;
                        o.Anchor = besideTag.Anchor;
                        o.Rise = followY != null ? 0 : tag.Span.Rise;
                        o.FollowY = followY;
                        o.FollowRise = tag.Span.Rise;
                        FloatEffect(EffectId.DamageNumber, new StagePoint(besideTag.At.X, followY != null ? followY() : besideTag.At.Y), shown, FxRole.Omen, BattleLayout.NumberFont(cue.Intensity), o);
                    }
                    else
                    {
                        FloatOpts o = Copy(under);
                        o.Side = side;
                        FloatEffect(EffectId.DamageNumber, numberAt, shown, FxRole.Omen, BattleLayout.NumberFont(cue.Intensity), o);
                    }
                    // §5.5 順 3 「相手が白く光る」 (review 19): every hit flashes white.
                    Effect(EffectId.HitFlash, () => _field.Flash(T, FxRole.White, Ms(EffectId.HitFlash), tu)).Forget();
                    Effect(EffectId.TargetRecoil, () => _field.Recoil(T, strong, Ms(EffectId.TargetRecoil), tu)).Forget();
                    if (strong) Effect(EffectId.ScreenShake, () => _field.ShakeArena(Ms(EffectId.ScreenShake))).Forget();
                    await DrainHp(T, cue.HpAfter, tu);
                    // The player's blow Guard took part of, with a move right after it (#499): as in Attack.
                    if (T == UnitSide.Enemy && EffectPlan.MoveFollows(ev, cue)) _field.SetPose(UnitSide.Player, FigurePose.Idle);
                    if (T == UnitSide.Player)
                    {
                        // The blow that lands on the player is the enemy's own: its omen is spent and it draws back. Review 21
                        // (§5.12): when the blow is the last cue of the enemy's event, the two beats are held (heldEnd).
                        if (ev.Kind == DepictionEventKind.EnemyAction && ev.Cues[ev.Cues.Count - 1] == cue)
                        {
                            TakePlanned(EffectId.OmenSpend, 1);
                            TakePlanned(EffectId.EnemyReturn, 1);
                            _heldEnd = new HeldAction
                            {
                                Unit = foe,
                                Title = _carried != null && _carried.Unit == foe && !string.IsNullOrEmpty(_carried.Title) ? _carried.Title : ev.Title ?? "",
                                Side = null,
                            };
                            _carried = null;
                        }
                        else
                        {
                            IOmenBadge badge = _hud.OmenOf(foe);
                            await HoldBeat(EffectId.OmenSpend, () => badge.FadeOut(Ms(EffectId.OmenSpend)), () => badge.HideNow());
                            // §5.4 順 2 (#587): the enemy's own move next (退き撃ち) carries it on from the lunge, so it does not
                            // draw back first; it walks in its standing pose, as the return would have left it.
                            if (EffectPlan.EnemyMoveFollows(ev, cue)) _field.SetPose(UnitSide.Enemy, FigurePose.Idle, foe);
                            else await HoldBeat(EffectId.EnemyReturn, () => _field.ReturnHome(UnitSide.Enemy, Ms(EffectId.EnemyReturn), foe), () => _field.SnapHome(UnitSide.Enemy, foe));
                        }
                    }
                    break;
                }
            }
        }

        /// <summary>hpBesideTag's answer: { at, anchor }.</summary>
        private sealed class BesideTag
        {
            public StagePoint At;
            public FloatAnchor Anchor;
        }

        /// <summary>
        /// Where the HP number of a blow stands beside its "Guard −n" tag (§5.6 順 3): 24 px further out on the tag's row, on
        /// the figure's back, or — with no room there on the stage — on the tag's other side. `font` sizes the estimate of its
        /// width (the heart icon at 0.62 of it, the digits at 0.6 each, the tag's padding).
        /// </summary>
        // js: scenes/battle/player.js:1337
        private static BesideTag HpBesideTag(GuardTag tag, string text, float font, string side)
        {
            double w = font * 0.62 + 6 + text.Length * font * 0.6 + 20 + (!string.IsNullOrEmpty(side) ? 6 + side.Length * 16 : 0);
            var right = new BesideTag { At = new StagePoint(tag.Span.X1 + HpBesideGuardGap, tag.Span.Y), Anchor = FloatAnchor.Left };
            var left = new BesideTag { At = new StagePoint(tag.Span.X0 - HpBesideGuardGap, tag.Span.Y), Anchor = FloatAnchor.Right };
            bool fitsRight = right.At.X + w <= L.W - StageEdge;
            bool fitsLeft = left.At.X - w >= StageEdge;
            if (tag.Away > 0) return fitsRight || !fitsLeft ? right : left;
            return fitsLeft || !fitsRight ? left : right;
        }

        /// <summary>
        /// §5.7 順 2's recount counts to the enemy the floor's N is read to (review 19): the Tab's enemy's gap and cell from the
        /// event's frame when this is the event's last move; else the cue's own.
        /// </summary>
        // js: scenes/battle/player.js:1353
        private (int Gap, int Cell) RecountTo(DepictionEvent ev, Cue cue)
        {
            var own = (cue.GapAfter, cue.EnemyCellAfter);
            if (_restingGapUnit == null || !MultiNow() || ev == null || ev.After == null) return own;
            int i = ev.Cues.IndexOf(cue);
            for (int j = i + 1; j < ev.Cues.Count; j++)
                if (ev.Cues[j].Kind == CueKind.RangeSwitch && EffectPlan.MovesOnFloor(ev.Cues[j])) return own;
            IReadOnlyList<EnemyFrame> list = EnemyShares(ev.After);
            int at = _restingGapUnit.Value;
            EnemyFrame uf = at >= 0 && at < list.Count ? list[at] : null;
            return uf != null && !uf.Unit.Down && uf.Gap >= 0 && uf.Cell > 0 ? (uf.Gap, uf.Cell) : own;
        }

        /// <summary>
        /// 押し出し stands on the side that is moved and rides with it (§5.7 順 6): a push the player gave moves an enemy
        /// (Cue.Unit with its UnitCellAfter): then it is that enemy's.
        /// </summary>
        // js: scenes/battle/player.js:1366
        private (StagePoint At, string Word, FloatOpts O) PushedFloat(Cue cue, StagePoint labelAt, FloatOpts under)
        {
            bool enemyMoved = UnitOf(cue) >= 0;
            int u = MultiNow() ? UnitOf(cue) : -1;
            FigureGeom hero = Figure(UnitSide.Player);
            FigureGeom foe = Figure(UnitSide.Enemy, u);
            if (!enemyMoved) return (labelAt, MovedWord(hero, cue.PlayerCellAfter, foe), Copy(under));
            StagePoint at = foe != null ? new StagePoint(foe.Chest.X, foe.Chest.Y) : labelAt;
            FloatOpts o = Copy(under);
            o.Track = TrackOf(UnitSide.Enemy, u);
            o.Rise = foe != null ? Math.Max(0f, foe.Chest.Y - foe.Head.Y) : under.Rise;
            return (at, MovedWord(foe, cue.UnitCellAfter, hero), o);
        }

        /// <summary>§5.7 順 6 (review 21): 「引き寄せ」 when the moved figure ends nearer the one who moved it, else 「押し出し」.</summary>
        // js: scenes/battle/player.js:1379
        private string MovedWord(FigureGeom moved, int cell, FigureGeom by)
        {
            StageBox? box = cell > 0 && _env.Geom.CellBox != null ? _env.Geom.CellBox(cell) : null;
            if (moved == null || by == null || box == null) return DepictionText.Pushed;
            float to = box.Value.X + box.Value.W / 2;
            return Math.Abs(to - by.HomeX) < Math.Abs(moved.HomeX - by.HomeX) ? PulledWord : DepictionText.Pushed;
        }

        // js: scenes/battle/player.js:1386
        private async Promise FireTrait(UnitSide side, StagePoint labelAt, string text, int trackUnit, float? rise)
        {
            // Warm belongs to the player's side only (README 色); a move the core stopped (§5.7 順 9 / 11) rises grey, no light.
            bool blocked = MoveBlockedLines.Contains(text ?? "");
            FxRole role = blocked ? FxRole.Ink2 : side == UnitSide.Player ? FxRole.Warm : FxRole.Ink;
            if (!blocked) _field.Burst(labelAt, role, 260);
            Unawaited(_field.FloatText(labelAt, text, role, 36, new FloatOpts
            {
                Rise = rise ?? 43, Plain = true, Avoid = BadgeBoxes, AvoidGap = FloatUnderBadge, Lowest = FloatFloor, Track = TrackOf(side, trackUnit),
            }));
            await _clock.Wait(Ms(EffectId.TraitFire));
        }

        /// <summary>
        /// A TraitFire cue that is a fired trait of the card played or of the enemy's action (not a stance line, not a stopped
        /// move): its condition word and the side label its number carries — 「+5 間合い」 — and which number takes it.
        /// </summary>
        // js: scenes/battle/player.js:1404
        private static TraitInfo TraitOf(DepictionEvent ev, Cue cue)
        {
            string text = cue.Text ?? "";
            if (text.Length == 0 || MoveBlockedLines.Contains(text) || text.StartsWith(StanceLine, StringComparison.Ordinal)) return null;
            bool own = cue.Target == UnitSide.Player ? ev.Kind == DepictionEventKind.PlayCard : ev.Kind == DepictionEventKind.EnemyAction;
            if (!own) return null;
            int at = text.LastIndexOf(' ');
            string conditionWord = at > 0 ? text.Substring(0, at) : "";
            string effectWord = at > 0 ? text.Substring(at + 1) : text;
            // §5.9 順 1 (review 21): the condition by its one-word name.
            string condition = conditionWord.Length > 0 ? ConditionName(conditionWord) : "";
            string kind = GuardEffect.IsMatch(effectWord) ? "guard" : HpEffect.IsMatch(effectWord) ? "hp" : "none";
            // On a Guard number the effect's own word is the number's kind already: 「+3 温存」, not 「Guard+3 温存」.
            string shownEffect = kind == "guard" ? GuardEffect.Replace(effectWord, "", 1) : effectWord;
            // §5.0 「−19 +6 重撃」: a named effect is its own label.
            string side = NamedEffects.TryGetValue(effectWord, out Func<string> named) ? named() : condition.Length > 0 ? shownEffect + " " + condition : shownEffect;
            return new TraitInfo { Condition = condition, Effect = effectWord, Side = side, Kind = kind };
        }

        // js: scenes/battle/player.js:1414 (/^Guard\+/ and /^\+\d|^重撃|^転換|^追撃|^崩し/; the replace drops /^Guard/)
        private static readonly Regex GuardEffect = new Regex("^Guard(?=\\+)", RegexOptions.CultureInvariant);
        private static readonly Regex HpEffect = new Regex("^\\+[0-9]|^重撃|^転換|^追撃|^崩し", RegexOptions.CultureInvariant);

        /// <summary>§5.0 (review 19): the HP a blow took: its Amount, but never more than the HP its target's panel held.</summary>
        // js: scenes/battle/player.js:1424
        private static int HpLostOf(IStatusPanel S, Cue cue)
        {
            int? before = S != null ? S.Hp : (int?)null;
            if (cue.HpAfter == Unchanged || before == null) return cue.Amount;
            int lost = before.Value - cue.HpAfter;
            return lost > 0 && lost < cue.Amount ? lost : cue.Amount;
        }

        /// <summary>§5.7 順 7 (review 19): the wall's beat beside the flow — the edge it hit lights 80 ms, the figure takes 打's ripple.</summary>
        // js: scenes/battle/player.js:1432
        private void WallBeat(UnitSide T, Wall wall, int tu)
        {
            Effect(EffectId.HitFlash, () => _field.WallHit(T, wall.Dir, WallEdgeMs, tu)).Forget();
        }

        // ---- beats --------------------------------------------------------------------------------------

        // js: scenes/battle/player.js:1438
        private async Promise Attack(DepictionEvent ev, Cue cue, StagePoint chest, StagePoint numberAt, FloatOpts under)
        {
            if (!cue.Source.HasValue) _env.Log.Error("[battle] event " + Str(ev.Order) + ": Slash cue has no Source");
            UnitSide source = EffectPlan.AttackerOf(cue);
            bool byPlayer = source == UnitSide.Player;
            bool strong = EffectStrength.IsStrong(cue.Intensity);
            UnitSide T = cue.Target;
            // Several enemies: the enemy of the exchange is the striker or the one struck (Cue.Unit).
            int foe = UnitOf(cue);
            int su = byPlayer ? -1 : foe;
            int tu = T == UnitSide.Enemy ? foe : -1;
            // #288: both sides step in before the blow.
            EffectId lunge = byPlayer ? EffectId.AttackLunge : EffectId.EnemyLunge;
            // §5.12 順 1 「踏み込みと止めは 1 回」 (review 21): the second blow of a held action strikes from where the first one stood.
            if (!byPlayer && _carried != null && (_carried.Unit == -1 || _carried.Unit == foe) && _field.EnemyAway(su))
            {
                TakePlanned(lunge, 1);
                _field.SetPose(source, FigurePose.Act, su);
            }
            else await HoldBeat(lunge, () => _field.Lunge(source, L.LungePx, Ms(lunge), su), () => _field.SetPose(source, FigurePose.Act, su));
            // §5.9 J2 (#57): a fired trait lays its short cue over the blow.
            if (cue.TraitAccent) Effect(EffectId.TraitAccent, () => _field.TraitAccent(T, Ms(EffectId.TraitAccent), tu)).Forget();
            // The action sound of an enemy blow is its system's hit; a player's card already sounded on release.
            if (!byPlayer) SfxBudget(HitSound(cue.System), true);
            await HoldBeat(EffectId.StrikeShape, () => _field.Strike(T, new StrikeOpts { ByPlayer = byPlayer, System = cue.System, Intensity = cue.Intensity, Ms = Ms(EffectId.StrikeShape), Unit = tu, Source = su }), null);
            EffectId stop = strong ? EffectId.HitStopStrong : EffectId.HitStop;
            await HoldBeat(stop, () => _clock.Wait(Ms(stop)), null);
            if (strong) Effect(EffectId.ScreenShake, () => _field.ShakeArena(Ms(EffectId.ScreenShake))).Forget();

            // A slash with no settled HP is the swing alone: the GuardBlock / Hit cues that follow carry the numbers.
            if (cue.HpAfter == Unchanged)
            {
                if (byPlayer) SfxBudget(HitSound(cue.System), true);
                return;
            }
            if (byPlayer) SfxBudget(HitSound(cue.System), true);
            _field.Burst(chest, FxRole.Omen, 200 + 60 * cue.Intensity);
            // §5.0 (review 19): the HP the blow took, with a fired trait's side label 「+5 間合い」.
            string shown = Minus + Str(HpLostOf(_hud.Status(T, tu), cue));
            FloatOpts o = Copy(under);
            o.Side = TakeSide("hp", cue);
            FloatEffect(EffectId.DamageNumber, numberAt, shown, FxRole.Ink, BattleLayout.NumberFont(cue.Intensity), o);
            Effect(EffectId.HitFlash, () => _field.Flash(T, FxRole.White, Ms(EffectId.HitFlash), tu)).Forget();
            Effect(EffectId.TargetRecoil, () => _field.Recoil(T, strong, Ms(EffectId.TargetRecoil), tu)).Forget();
            await DrainHp(T, cue.HpAfter, tu);
            // §5.4 順 2 / §5.5 順 8 (#499): a move right after the blow carries the player on from the lunge.
            if (byPlayer && !EffectPlan.MoveFollows(ev, cue))
            {
                await HoldBeat(EffectId.AttackReturn, () => _field.ReturnHome(source, Ms(EffectId.AttackReturn)), () => _field.SnapHome(source));
            }
            else if (byPlayer) _field.SetPose(source, FigurePose.Idle);
        }

        /// <summary>The bar drops (blocking), then the grey band follows it down beside the flow.</summary>
        // js: scenes/battle/player.js:1484
        private async Promise DrainHp(UnitSide side, int hp, int unit)
        {
            IStatusPanel S = _hud.Status(side, unit);
            if (side == UnitSide.Player && hp < S.Hp) PulseLowHp(hp);
            await HoldBeat(EffectId.HpDrain, () => S.AnimateHp(hp, Ms(EffectId.HpDrain)), () => S.SetHpKeepingTrail(hp), 1,
                () => Effect(EffectId.HpTrail, () => S.AnimateTrail(Ms(EffectId.HpTrail)), () => S.SnapTrail()));
        }

        // js: scenes/battle/player.js:1491
        private int PlayerSize()
        {
            DepictionFrame f = FrameNow;
            return f != null && f.Floor != null ? Math.Max(1, f.Floor.PlayerSize) : 1;
        }

        /// <summary>§3.14 山札切れ (#455): the discard pile slides onto the draw pile in the middle of the draw.</summary>
        // js: scenes/battle/player.js:1497
        private Promise SlideDiscardOntoDraw()
        {
            Sfx("rebuild");
            return _field.Ghost(PileMiddle(PileKind.Discard), PileMiddle(PileKind.Draw), new GhostOpts { Kind = GhostKind.Sheet, Ms = Ms(EffectId.PileRebuild), Ease = Ease.Out });
        }

        /// <summary>DrawHand: rebuild the hand from the settled faces and fly the newest ones in from the draw pile.</summary>
        // js: scenes/battle/player.js:1503
        private async Promise DrawHand(List<CardFace> faces, int drawn, int rebuildAfter)
        {
            bool rebuild = drawn > 0 && rebuildAfter >= 0;
            if (rebuild && !(_switches.IsOn(EffectId.PileRebuild) && _switches.IsOn(EffectId.CardDraw)))
            {
                await HoldBeat(EffectId.PileRebuild, () => SlideDiscardOntoDraw(), null);
                rebuild = false;
            }
            _hand.Build(faces ?? new List<CardFace>());
            int count = Math.Max(0, Math.Min(drawn, _hand.Count()));
            if (count == 0) return;
            StagePoint from = PileMiddle(PileKind.Draw);
            if (!rebuild)
            {
                await HoldBeat(EffectId.CardDraw, () => _hand.FlyDrawn(_hand.Count() - count, Unchanged, new FlyDrawnOpts { From = from }), null, count);
                return;
            }
            Promise flying = Safe(() => _hand.FlyDrawn(_hand.Count() - count, rebuildAfter, new FlyDrawnOpts { From = from, OnRebuild = () => SlideDiscardOntoDraw() }));
            await HoldBeat(EffectId.PileRebuild, () => Promise.Resolved(), null);
            await HoldBeat(EffectId.CardDraw, () => flying, null, count);
        }

        /// <summary>Turn end: the whole hand flies to the discard pile one stagger apart.</summary>
        // js: scenes/battle/player.js:1523
        private async Promise DiscardHand()
        {
            int count = _hand.Count();
            if (count > 0) await HoldBeat(EffectId.HandDiscard, () => _hand.DiscardAll(PileMiddle(PileKind.Discard)), null, count);
            _hand.Clear();
        }

        /// <summary>The played card's ghost flies to the pile the core put it in (DepictionEvent.PlayedTo), beside the beats.</summary>
        // js: scenes/battle/player.js:1530
        private async Promise GhostToPile(Func<StagePoint> from, PileKind pile, CardFace face, Action onStart = null)
        {
            bool exiled = pile == PileKind.Exile;
            if (exiled) await _clock.Wait(Math.Max(0f, EffectCatalog.CardToExileDelayMs - Ms(EffectId.CardRelease)));
            StagePoint at = from();
            onStart?.Invoke();
            EffectId id = exiled ? EffectId.CardToExile : EffectId.CardToDiscard;
            // The card itself, shrunk (review fix; Unity: a plain sheet): the hand builds its face, the field flies it.
            object card = face != null ? _hand.GhostCard(face) : null;
            await Effect(id, () => _field.Ghost(at, PileMiddle(exiled ? PileKind.Exile : PileKind.Discard),
                new GhostOpts { Kind = exiled ? GhostKind.Exile : GhostKind.Discard, Ms = Ms(id), Ease = exiled ? Ease.In : Ease.InOut, Card = card }));
        }

        // ---- the player's moves ---------------------------------------------------------------------

        // js: scenes/battle/player.js:1544
        private StagePoint ReceiverCentre(int unit = -1)
        {
            ScreenBox? r = _env.Geom.Receiver != null ? (_env.Geom.Receiver(unit) ?? _env.Geom.Receiver(-1)) : null;
            // The field always registers its receiver; without one the JS throws here (scene choice: the stage's middle).
            if (r == null) return new StagePoint(L.W / 2, 423);
            return new StagePoint(r.Value.X + r.Value.Width / 2, r.Value.Y + r.Value.Height / 2);
        }

        /// <summary>
        /// §3.14 / §5.7 順 5 (review 17): the way a played card meant to move the player (詰める → +1 / 離れる ← −1; 0 for
        /// none), when its event moved no one on the floor and no 呪縛 / 鉤爪 line says why.
        /// </summary>
        // js: scenes/battle/player.js:1554
        private static int StuckMoveOf(CardFace face, DepictionEvent ev)
        {
            if (face == null || ev == null) return 0;
            List<CardValue> vals = face.Values ?? new List<CardValue>();
            int dir = vals.Any(v => v.Kind == CardValueKind.Close) ? 1 : vals.Any(v => v.Kind == CardValueKind.Back) ? -1 : 0;
            if (dir == 0) return 0;
            bool moved = ev.Cues.Any(c => (c.Kind == CueKind.RangeSwitch && EffectPlan.MovesOnFloor(c))
                || (c.Kind == CueKind.TraitFire && MoveBlockedLines.Contains(c.Text ?? "")));
            return moved ? 0 : dir;
        }

        /// <summary>
        /// §5.7 順 5 (review 19): the player's own move cue that stopped short of what the card asked, against the enemy (N 0
        /// after a move forward) or the end of the line (cell 1 after a move back): the last such cue with its way, or null.
        /// </summary>
        // js: scenes/battle/player.js:1570
        private static (Cue Cue, int Dir)? CutShortOf(CardFace face, DepictionEvent ev)
        {
            if (face == null || ev == null) return null;
            CardValue v = (face.Values ?? new List<CardValue>()).FirstOrDefault(x => x.Kind == CardValueKind.Close || x.Kind == CardValueKind.Back);
            int want = v != null ? ParseIntJs(v.Number) : 0;
            if (!(want > 0)) return null;
            int dir = v.Kind == CardValueKind.Close ? 1 : -1;
            List<Cue> own = ev.Cues.Where(c => c.Kind == CueKind.RangeSwitch && !c.Pushed && UnitOf(c) < 0 && EffectPlan.MovesOnFloor(c)).ToList();
            if (own.Count == 0) return null;
            int moved = own.Sum(c => EffectPlan.CellsOf(c));
            Cue last = own[own.Count - 1];
            bool against = dir > 0 ? last.GapAfter == 0 : last.PlayerCellAfter <= 1;
            return moved < want && against ? (last, dir) : ((Cue, int)?)null;
        }

        /// <summary>JS parseInt(s, 10): the leading digits (after a sign), or 0 for none (NaN, which compares false).</summary>
        private static int ParseIntJs(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            string t = s.TrimStart();
            int i = 0;
            bool neg = false;
            if (i < t.Length && (t[i] == '+' || t[i] == '-'))
            {
                neg = t[i] == '-';
                i++;
            }
            int n = 0;
            int start = i;
            while (i < t.Length && t[i] >= '0' && t[i] <= '9')
            {
                n = n * 10 + (t[i] - '0');
                i++;
            }
            if (i == start) return 0;
            return neg ? -n : n;
        }

        // js: scenes/battle/player.js:1584
        private async Promise PlayAccepted(string cardId, DepictionEvent ev, CardFace face)
        {
            _busy = true;
            SetState(BattlePlayerState.Resolving);
            SetHandInteractable(false);
            _playedThisTurn += 1;
            _eventFace = face;
            // The result screen's count (review 21, §2.5): the cards played by attribute and the traits they fired.
            if (face != null) _tallyPlayed[face.Kind] = (_tallyPlayed.TryGetValue(face.Kind, out int n) ? n : 0) + 1;
            _tallyTraits += ev.Cues.Count(c => c.Kind == CueKind.TraitFire && c.Target == UnitSide.Player && TraitOf(ev, c) != null);
            BeginPlan(ev);
            _budget = 1; // the release sound (the hand's) is the action
            _essentialLeft = Math.Min(_budget, _essentialLeft);
            StagePoint? to = ev.Aim == CardAim.Single ? ReceiverCentre(UnitOf(ev)) : (StagePoint?)null;
            // §5.9 順 1 (review 19): a trait the card fires swells its lamp and its condition word leaves with the card.
            Cue traitCue = ev.Cues.FirstOrDefault(c => c.Kind == CueKind.TraitFire && c.Target == UnitSide.Player && TraitOf(ev, c) != null);
            TraitInfo trait = traitCue != null ? TraitOf(ev, traitCue) : null;
            ReleaseResult rel = null;
            await HoldBeat(EffectId.CardRelease, () =>
            {
                rel = _hand.Release(cardId, ev, to, new ReleaseOpts { TraitWord = trait != null ? (trait.Condition.Length > 0 ? trait.Condition : trait.Effect) : "" });
                return rel?.Done;
            }, null, 1, () => Released(cardId, ev, rel));
            // The move that went nowhere leans its 40 ms where the move would have played: after the stamina is paid.
            int stuck = StuckMoveOf(face, ev);
            void Lean()
            {
                int dir = stuck;
                stuck = 0;
                if (dir != 0) Effect(EffectId.FloorMove, () => _field.Bump(UnitSide.Player, dir, L.MoveBumpMs)).Forget();
            }
            // §5.7 順 5: a move that went part of the way leans where it stopped, on its arrival.
            var cut = CutShortOf(face, ev);
            foreach (Cue cue in ev.Cues.ToArray())
            {
                if (!_alive) return;
                if (stuck != 0 && cue.Kind != CueKind.StaminaChange) Lean();
                await PlayCue(ev, cue);
                if (cut != null && cue == cut.Value.Cue)
                {
                    int dir = cut.Value.Dir;
                    Effect(EffectId.FloorMove, () => _field.Bump(UnitSide.Player, dir, L.MoveBumpMs)).Forget();
                }
            }
            if (!_alive) return;
            if (stuck != 0) Lean();
            _field.SetPose(UnitSide.Player, FigurePose.Idle);
            FlushSide();
            _eventFace = null;
            EndPlan();
            ApplyFrame(ev.After);
            await AfterWait(EffectFlow.AfterPlayerEventMs);
            if (!_alive) return;
            EventEnded();
            await RunAutomaticEvents();
        }

        /// <summary>
        /// The released card is gone: a ghost flies to the pile the core put it in and the hand closes up. A stance card (§5.11
        /// 順 1 / 順 4, review 19) stands shrunk at the end of the stance list until its ghost leaves from there for the exile.
        /// </summary>
        // js: scenes/battle/player.js:1637
        private void Released(string cardId, DepictionEvent ev, ReleaseResult rel)
        {
            StagePoint from = ev.Aim == CardAim.Single ? ReceiverCentre(UnitOf(ev)) : rel != null && rel.VanishedAt != null ? rel.VanishedAt() : ReceiverCentre();
            if (rel != null && rel.Keep) GhostToPile(() => rel.VanishedAt(), ev.PlayedTo, rel.Face, () => _hand.DropCard(cardId)).Forget();
            else
            {
                _hand.DropCard(cardId);
                GhostToPile(() => from, ev.PlayedTo, rel?.Face).Forget();
            }
            Effect(EffectId.HandFan, () => _hand.Settle(Ms(EffectId.HandFan)), () => _hand.Layout()).Forget();
        }

        // js: scenes/battle/player.js:1647
        private async Promise ReturnToHand(string cardId, bool shake)
        {
            _busy = true;
            _returningCard = true;
            SetState(BattlePlayerState.Resolving);
            await Safe(() => _hand.ReturnToHand(cardId, shake));
            _returningCard = false;
            if (!_alive) return;
            _busy = false;
            SetState(BattlePlayerState.Idle);
            RefreshPlayable();
            // An end turn asked for while the card slid home (X or the plate) is taken now.
            if (_endTurnQueued)
            {
                _endTurnQueued = false;
                if (RequestEndTurn()) return;
            }
            ScheduleAuto();
        }

        /// <summary>The enemy a release names, as the source takes it: only in a battle of several enemies (−1: none).</summary>
        // js: scenes/battle/player.js:1663
        private int TargetFor(int unit)
        {
            return unit >= 0 && MultiNow() ? unit : -1;
        }

        /// <summary>Release: the card was let go on `zone` (over enemy `unit` when the hand names one). The source decides; the hand shows.</summary>
        // js: scenes/battle/player.js:1668
        private void OnRelease(string cardId, DropZone zone, int unit)
        {
            if (!_alive) return;
            if (_state != BattlePlayerState.Idle || _busy)
            {
                Safe(() => _hand.ReturnToHand(cardId, false)).Forget();
                return;
            }
            int target = TargetFor(unit);
            CardFace face = DepictionText.Find(FrameNow.Hand ?? new List<CardFace>(), cardId);
            PlayVerdict verdict;
            DepictionEvent ev;
            try
            {
                verdict = target < 0 || !(_ctx.Source is ITrialSource trial)
                    ? _ctx.Source.TryPlay(cardId, zone, out ev)
                    : trial.TryPlay(cardId, zone, target, out ev);
            }
            catch (Exception err)
            {
                SourceFailed(err);
                return;
            }
            if (verdict == PlayVerdict.Accepted && ev != null)
            {
                _acceptedPlays += 1;
                PlayAccepted(cardId, ev, face).Forget();
                return;
            }
            ShowRefusal(cardId, verdict, target, face);
            // A card released on a zone it belongs to was refused by a rule: shake it. One released on the wrong zone just slides home.
            ReturnToHand(cardId, verdict != PlayVerdict.WrongZone && zone != DropZone.None).Forget();
        }

        // js: scenes/battle/player.js:1690
        private void ShowRefusal(string cardId, PlayVerdict verdict, int target, CardFace face)
        {
            string text = target < 0 || !(_ctx.Source is ITrialSource trial)
                ? _ctx.Source.RefusalText(cardId, verdict)
                : trial.RefusalText(cardId, verdict, target);
            if (string.IsNullOrEmpty(text)) return;
            // The receiver the ported line names is not drawn (WrongZoneOnEnemy): the same line, with 敵の上.
            if (verdict == PlayVerdict.WrongZone && face != null && face.Aim == CardAim.Single && text == DepictionText.WrongZoneText(face)) text = WrongZoneOnEnemy(face.Name);
            _hand.ShowRefusal(text);
            _refusalCause = null;
        }

        /// <summary>
        /// RefreshPlayableLook (review fix; battle-visual-v1 §5.1 / §6.2, battle_ui_ux_v2 §3.5): while the player waits a card
        /// is dark when the source would refuse it for what it costs or for 呪縛 — not for reach; while the flow runs the whole
        /// hand is dark. The guide: the journal's, then 俊敏's choice, then the pile list (quiet), then the source's.
        /// </summary>
        // js: scenes/battle/player.js:1707
        public void RefreshPlayable()
        {
            IDepictionSource s = Src;
            bool waiting = _state == BattlePlayerState.Idle && !_busy && s.WaitingForPlayer && _hand.Count() > 0;
            bool asked = _state == BattlePlayerState.Idle && !_busy && s.WaitingForPlayer;
            string guide = asked && _journalOpen() ? JournalGuide
                : _freeStep != null && _state == BattlePlayerState.Idle ? FreeStepGuide
                : asked && _drawerOpen() ? ""
                : waiting || s.Finished ? s.GuideText : "";
            _hand.RefreshPlayable(id =>
            {
                if (!waiting) return true;
                PlayVerdict verdict = s.Inspect(id);
                return verdict != PlayVerdict.Accepted && verdict != PlayVerdict.OutOfRange;
            }, guide ?? "");
            FlashIfNothingPlayable(asked);
        }

        /// <summary>
        /// §3.5 「出せる札が 1 枚も無い … ターン終了ボタンが 1 回だけ明るくなる」 (review 16): once per wait in which the source would
        /// take no card of the hand (Inspect, read only), the end-turn plate brightens once.
        /// </summary>
        // js: scenes/battle/player.js:1739
        private void FlashIfNothingPlayable(bool asked)
        {
            if (!asked || _freeStep != null || _hud.EndTurn == null) return;
            bool none;
            List<CardFace> faces = FrameNow.Hand ?? new List<CardFace>();
            try { none = !faces.Any(f => _ctx.Source.Inspect(f.Id) == PlayVerdict.Accepted); }
            catch (Exception) { none = false; }
            string key = none ? Str(FrameNow.Corner != null ? FrameNow.Corner.Turn : 0) + ":" + string.Join(",", faces.Select(f => f.Id)) : "";
            if (key == _nothingKey) return;
            _nothingKey = key;
            if (none) Safe(() => _hud.EndTurn.Flash(L.EndTurnFlashMs)).Forget();
        }

        // ---- 俊敏's free cell (§3.8 / §5.1 順 7 / §5.7 順 10) --------------------------------------------

        /// <summary>At the turn's first wait: asks the source once whether the free cell is open, and puts the choice up.</summary>
        // js: scenes/battle/player.js:1757
        private void OpenFreeStep()
        {
            IDepictionSource s = _ctx.Source;
            if (_halted || !(s is ITrialSource trial)) return;
            DepictionFrame f = FrameNow;
            int turn = f != null && f.Corner != null ? f.Corner.Turn : -1;
            if (turn == _freeStepTurn) return;
            _freeStepTurn = turn;
            FreeStepWays ways;
            try
            {
                if (!trial.CanTakeFreeStep()) return;
                bool back = trial.CanTakeFreeStep(-1);
                bool forward = trial.CanTakeFreeStep(1);
                ways = new FreeStepWays(back, forward);
            }
            catch (Exception err)
            {
                LogError(err);
                return;
            }
            _freeStep = new FreeStepChoice { Ways = ways };
            _hand.SetGrabLock(RefuseWhileChoosing);
            Unawaited(_field.ShowFreeStep(f.Floor != null ? f.Floor.PlayerCell : 0, ways));
        }

        /// <summary>The choice is over (chosen, the turn ended, the battle stopped): the marks go and the cards can be taken.</summary>
        // js: scenes/battle/player.js:1775
        private void CloseFreeStep(float? ms = null)
        {
            if (_freeStep == null) return;
            _freeStep = null;
            _hand.SetGrabLock(null);
            Unawaited(_field.HideFreeStep(ms));
            ClearRefusal("freestep");
        }

        /// <summary>A card was tried while the choice waits (§3.8 選ぶまで手札は掴めません): the reason on the refusal line.</summary>
        // js: scenes/battle/player.js:1784
        private void RefuseWhileChoosing()
        {
            Sfx("refusal");
            ShowCauseRefusal("freestep", FreeStepRefusal);
        }

        /// <summary>Review 21: the lines that name something to close or to choose remember it, and go when it goes.</summary>
        // js: scenes/battle/player.js:1793
        private void ShowCauseRefusal(string cause, string text)
        {
            _hand.ShowRefusal(text);
            _refusalCause = cause;
        }

        // js: scenes/battle/player.js:1797
        private void ClearRefusal(string cause)
        {
            if (_refusalCause != cause) return;
            _refusalCause = null;
            _hand.HideRefusal();
        }

        /// <summary>The choice: −1 back, +1 forward (a way the source said is open), 0 stay. Returns whether it was taken.</summary>
        // js: scenes/battle/player.js:1804
        public bool PickFreeStep(int dir)
        {
            if (_freeStep == null || !_alive || _halted || _state != BattlePlayerState.Idle || _busy || _overlayOpen() || _hand.IsHolding()) return false;
            if (dir != 0 && !_freeStep.Ways[dir]) return false;
            Sfx("uiClick");
            CloseFreeStep();
            if (dir == 0)
            {
                RefreshPlayable();
                ScheduleAuto();
                return true;
            }
            DepictionEvent ev;
            try { ev = ((ITrialSource)_ctx.Source).TakeFreeStep(dir); }
            catch (Exception err)
            {
                SourceFailed(err);
                return false;
            }
            PlayFreeStep(ev).Forget();
            return true;
        }

        /// <summary>The step's event: the walk of one cell and the recount (§5.7 順 10 → 順 1〜2), or what stopped it (順 9 / 11).</summary>
        // js: scenes/battle/player.js:1821
        private async Promise PlayFreeStep(DepictionEvent ev)
        {
            _busy = true;
            SetState(BattlePlayerState.Resolving);
            SetHandInteractable(false);
            BeginPlan(ev);
            foreach (Cue cue in ev.Cues.ToArray())
            {
                if (!_alive) return;
                await PlayCue(ev, cue);
            }
            if (!_alive) return;
            if (ev.Cues.Count == 0)
            {
                // Nothing moved and nothing says why (鈍足 took the cell to 0, or the line): §5.7 順 9's grey word.
                FigureGeom g = Figure(UnitSide.Player);
                if (g != null)
                    FloatEffect(EffectId.PushMark, new StagePoint(g.Chest.X, g.Chest.Y), FreeStepStuck, FxRole.Ink2, 36, UnderBadges(new FloatOpts { Rise = Math.Max(0f, g.Chest.Y - g.Head.Y) }));
            }
            _field.SetPose(UnitSide.Player, FigurePose.Idle);
            FlushSide();
            EndPlan();
            ApplyFrame(ev.After);
            await AfterWait(EffectFlow.AfterPlayerEventMs);
            if (!_alive) return;
            _busy = false;
            EventEnded();
            await RunAutomaticEvents();
        }

        /// <summary>The pointer is over mark `dir`: the focus follows it.</summary>
        // js: scenes/battle/player.js:1848
        public void FocusFreeStep(int dir)
        {
            if (_freeStep == null) return;
            int? before = _field.FreeStepFocus;
            if (_field.FocusFreeStep(dir) != before) Sfx("hover");
        }

        /// <summary>
        /// §3.4 / §3.8 「← / → で 1 マス後ろ / 前を選びます」 (review 21): the key of a way always picks that way — pressed again it
        /// keeps it; only the key of a way that is not open falls to 留まる.
        /// </summary>
        // js: scenes/battle/player.js:1860
        public bool MoveFreeStepFocus(int key)
        {
            if (_freeStep == null || _state != BattlePlayerState.Idle || _busy) return false;
            IReadOnlyList<int> dirs = _field.FreeStepDirs() ?? Array.Empty<int>();
            int? now = _field.FreeStepFocus;
            int want = key < 0 ? -1 : 1;
            int? next = dirs.Contains(want) ? want : dirs.Contains(0) ? 0 : now;
            if (next == null) return false;
            FocusFreeStep(next.Value);
            return true;
        }

        /// <summary>Esc while 俊敏's choice waits: the focus goes to 留まる. Returns whether it moved.</summary>
        // js: scenes/battle/player.js:1871
        public bool StayFreeStepFocus()
        {
            if (_freeStep == null || _state != BattlePlayerState.Idle || _busy) return false;
            if (_field.FreeStepFocus == 0 || !(_field.FreeStepDirs() ?? Array.Empty<int>()).Contains(0)) return false;
            FocusFreeStep(0);
            return true;
        }

        // js: scenes/battle/player.js:1878
        public bool CanEndTurn()
        {
            return _alive && !_halted && _state == BattlePlayerState.Idle && !_busy && !_hand.IsHolding() && !_overlayOpen() && Src.CanEndTurn;
        }

        /// <summary>
        /// The end-turn plate, X. `queue`: asked for while a refused card is still sliding home, it is taken as soon as the
        /// card is back. `press`: a press of the plate or X — while the flow runs it only marks the mash window; while the
        /// window is open a press is swallowed and slides it on (review fixes 16 / 17 / 21). Returns whether the turn ended
        /// (or the request was queued).
        /// </summary>
        // js: scenes/battle/player.js:1886
        public bool RequestEndTurn(bool queue = false, bool press = false)
        {
            if (queue && _returningCard && _alive && !_halted && !_overlayOpen())
            {
                _endTurnQueued = true;
                return true;
            }
            if (press)
            {
                float now = _real.Now;
                bool flowing = _state == BattlePlayerState.Auto || _state == BattlePlayerState.Resolving || _state == BattlePlayerState.Opening;
                // Review 17: the window a press opens follows the rhythm of the presses before it.
                float gap = now - _lastPressAt;
                _lastPressAt = now;
                float rhythm = gap <= L.EndTurnMashMaxMs ? gap : 0f;
                float win = Math.Min(L.EndTurnMashMaxMs, Math.Max(L.EndTurnMashMs, rhythm * L.EndTurnMashRhythm));
                // A press while the flow runs (hurrying the enemy's turn or a play) is swallowed and opens the window.
                if (flowing)
                {
                    _mashUntil = now + win;
                    return false;
                }
                // Review 21: the window absorbs only the mash carried over from the flow; never past 1.5 s after the player
                // was asked, and a swallowed press shows it was seen (the plate shakes once).
                float sinceAsked = now - _askedAt;
                bool guarded = now - _turnOpenedAt < L.EndTurnOpenGuardMs;
                if (guarded || (now < _mashUntil && sinceAsked < L.EndTurnMashMaxMs))
                {
                    _mashUntil = Math.Min(_askedAt + L.EndTurnMashMaxMs, Math.Max(_mashUntil, now + win));
                    if (_hud.EndTurn != null) Safe(() => _hud.EndTurn.Refuse(L.EndTurnRefuseMs)).Forget();
                    return false;
                }
            }
            if (!CanEndTurn()) return false;
            _endTurnQueued = false;
            // The turn's end lets 俊敏's cell go (battle_core_v4.md §5): the choice goes with it.
            CloseFreeStep(0);
            // The floor's N goes back to the nearest enemy for the enemy's turn.
            if (_restingGapUnit != null)
            {
                _restingGapUnit = null;
                ShowHpLoss();
            }
            _hand.Cancel();
            DepictionEvent ev;
            try { ev = _ctx.Source.EndTurn(); }
            catch (Exception err)
            {
                SourceFailed(err);
                return false;
            }
            PlayThenContinue(ev).Forget();
            return true;
        }

        // js: scenes/battle/player.js:1927
        private async Promise PlayThenContinue(DepictionEvent ev)
        {
            _busy = true;
            SetState(BattlePlayerState.Resolving);
            SetHandInteractable(false);
            await PlayEvent(ev);
            if (!_alive) return;
            _busy = false;
            await AfterWait(EffectFlow.AfterPlayerEventMs);
            if (!_alive) return;
            EventEnded();
            await RunAutomaticEvents();
        }

        /// <summary>
        /// The source threw (a bridge or core bug). The battle cannot go on: the scene stops taking input and pulling
        /// events, logs the error, and hands it to ctx.OnError when the app gave one.
        /// </summary>
        // js: scenes/battle/player.js:1945
        private void SourceFailed(Exception err)
        {
            _env.Log.Error("[battle] the source failed; the battle stops here:", err);
            _halted = true;
            _busy = false;
            CloseFreeStep(0);
            SetHandInteractable(false);
            SetState(BattlePlayerState.Halted);
            try { _ctx.OnError?.Invoke(err); }
            catch (Exception e) { LogError(e); }
        }

        // ---- the end ------------------------------------------------------------------------------------

        /// <summary>`ms` of real time that ran while no overlay held the scene (the result hold waits behind the journal).</summary>
        // js: scenes/battle/player.js:1958
        private async Promise HoldUnpaused(float ms)
        {
            float left = ms;
            float last = _real.Now;
            while (left > 0)
            {
                await _real.Wait(Math.Min(left, 50f));
                float now = _real.Now;
                if (!_overlayOpen()) left -= now - last;
                last = now;
            }
        }

        /// <summary>
        /// The words of the end of the battle (review 21): a win's paper — whom it beat, the turns and the HP kept, the
        /// chain's fight — or a loss's result screen (§2.5).
        /// </summary>
        // js: scenes/battle/player.js:1974
        private Trial.Battle.ResultInfo ResultInfo(BattleOutcome outcome)
        {
            DepictionFrame f = FrameNow ?? new DepictionFrame();
            UnitFrame p = f.Player ?? new UnitFrame { Hp = 0, HpMax = 0 };
            CornerFrame corner = f.Corner ?? new CornerFrame();
            BattleChain chain = _ctx.Chain != null && _ctx.Chain.Total != 0 ? _ctx.Chain : new BattleChain(corner.ChainIndex, corner.ChainTotal);
            int turns = corner.Turn;
            if (outcome == BattleOutcome.Won)
            {
                string names = string.Join("・", DistinctInOrder(EnemyFrames(f).Select(uf => BattleLayout.EnemyNameOf(uf.ArtId)).Where(s => s.Length > 0)));
                var lines = new List<string> { ResultText.Beat(names), ResultText.WonLine(turns, Math.Max(0, p.Hp), p.HpMax) };
                if (chain.Total > 1) lines.Add(ResultText.ChainAt(chain));
                return new Trial.Battle.ResultInfo { Lines = lines };
            }
            return new Trial.Battle.ResultInfo
            {
                Title = ResultText.FellAt(chain, _env.LayerNo),
                Lines = new[] { ResultText.HpLeft(Math.Max(0, p.Hp), p.HpMax) },
                BarsLabel = ResultText.PlayedHead,
                Bars = ResultBars.Select(b => new ResultBar(b.Label, b.Color, b.Kinds.Sum(k => _tallyPlayed.TryGetValue(k, out int n) ? n : 0))).ToList(),
                Stats = new[] { ResultText.Traits(_tallyTraits), ResultText.Turns(turns) },
                Button = ResultText.ToJournal,
            };
        }

        /// <summary>[...new Set(list)]: the first of each, in order.</summary>
        private static List<string> DistinctInOrder(IEnumerable<string> list)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<string>();
            foreach (string s in list)
                if (seen.Add(s)) result.Add(s);
            return result;
        }

        /// <summary>
        /// §2.5 切替バナー / §5.18 順 6〜7 (review 21): a chain's second fight and on opens on the dark board with 「n 戦目」 and the
        /// enemy's name, then the enemy comes out of the haze as the board comes up (400, out).
        /// </summary>
        // js: scenes/battle/player.js:1999
        private async Promise ChainOpening()
        {
            BattleChain chain = _ctx.Chain;
            if (chain == null || !(chain.Total > 1) || !(chain.Index > 1)) return;
            string names = string.Join("・", DistinctInOrder(EnemyFrames(FrameNow).Select(uf => BattleLayout.EnemyNameOf(uf.ArtId)).Where(s => s.Length > 0)));
            bool on = _switches.IsOn(EffectId.TurnBanner);
            Unawaited(_field.AppearEnemies(0, 0));
            await Safe(() => _hud.ChainBanner(ResultText.SwitchTitle(chain.Index), names, on ? 1 : 0,
                () => Unawaited(_field.AppearEnemies(on ? ChainAppearMs : 0, 0))));
            if (!on) Unawaited(_field.AppearEnemies(0, 1));
        }

        // js: scenes/battle/player.js:2010
        private async Promise Finish()
        {
            CloseFreeStep(0);
            SetState(BattlePlayerState.Over);
            SetHandInteractable(false);
            RefreshPlayable(); // the guide goes quiet; the outcome is the result card's
            _hud.SetSpeedVisible(false);
            // Review fix: the pile list closes (it sat bright over the scrim, and its Esc moved past the result).
            if (_hud.DrawerOpen()) _hud.CloseDrawer();
            BattleOutcome outcome = FrameNow.Outcome;
            // Review fix: no enemy acts again, so a standing enemy's omen badge and plan fade now, with their aimed cells.
            if (outcome != BattleOutcome.Ongoing)
            {
                Effect(EffectId.OmenSpend, () => _hud.RetireOmens(Ms(EffectId.OmenSpend)), () => Unawaited(_hud.RetireOmens(0))).Forget();
                Effect(EffectId.OmenSpend, () => _field.RetireAim(Ms(EffectId.OmenSpend)), () => Unawaited(_field.RetireAim(0))).Forget();
            }
            // Review fix: a lost battle is the player's fall (the script never marks the player Down).
            if (outcome == BattleOutcome.Lost && !(FrameNow.Player != null && FrameNow.Player.Down))
            {
                await Effect(EffectId.Defeat, () => _field.Fall(UnitSide.Player, Ms(EffectId.Defeat)), () => _field.SetDown(UnitSide.Player, true));
                if (!_alive) return;
            }
            // §5.19 順 1〜4 (review 19; scene addition): the foot lights shrink out, the ink floods up, 「力尽きた」 comes in, a hold.
            if (outcome == BattleOutcome.Lost)
            {
                if (_switches.IsOn(EffectId.Defeat))
                {
                    await Safe(() => _field.DimFeet(Loss.FeetMs));
                    if (!_alive) return;
                    await Safe(() => _hud.LossInk(LossWord, Loss.InkMs, Loss.WordMs));
                    if (!_alive) return;
                    await _clock.Wait(Loss.HoldMs);
                    if (!_alive) return;
                }
                else
                {
                    Unawaited(_field.DimFeet(0));
                    Unawaited(_hud.LossInk(LossWord, 0, 0));
                }
            }
            // Review fix: the field settles under the result — the plate goes off, the hand and the fallen panels fade.
            if (outcome != BattleOutcome.Ongoing)
            {
                float settleMs = Ms(EffectId.ResultCard);
                Effect(EffectId.ResultCard, () => Promise.All(_hud.SettleEnd(settleMs, outcome), _hand.FadeAll(settleMs)), () =>
                {
                    _hud.SettleEndNow(outcome);
                    Unawaited(_hand.FadeAll(0));
                }).Forget();
            }
            // §6.2 「結果の札から終了画面までの 1.2 秒」 (reading time, not scaled): the hold runs from the card having risen; a
            // skip counts only once the card has stood L.ResultSkipAfterMs and after a pause of L.ResultSkipPauseMs (review 16).
            _resultUpAt = null;
            _resultLastPress = float.NegativeInfinity;
            _resultSkip = () => { };
            Trial.Battle.ResultInfo info = ResultInfo(outcome);
            Action onButton = () =>
            {
                if (_resultUpAt != null && _resultSkip != null) _resultSkip();
            };
            if (outcome != BattleOutcome.Ongoing)
            {
                bool lost = outcome == BattleOutcome.Lost;
                float paperMs = lost ? Loss.PaperMs : Ms(EffectId.ResultCard);
                if (lost) Effect(EffectId.ResultCard, () => _hud.LossWordOut(paperMs), () => Unawaited(_hud.LossWordOut(0))).Forget();
                await Effect(EffectId.ResultCard, () => _hud.ResultCard(outcome, paperMs, info, onButton), () => _hud.ResultCardNow(outcome, info, onButton));
            }
            if (!_alive) return;
            _resultUpAt = _real.Now;
            // A loss waits for 「手記へ」 (or a click, Enter, Space or Esc: SkipResult); a win — and the auto play — goes on
            // after RESULT_HOLD_MS, as Unity's DemoFlow does.
            bool waits = outcome == BattleOutcome.Lost && _ctx.Auto == null;
            Promise hold = waits ? Promise.Never() : HoldUnpaused(L.ResultHoldMs);
            var skipped = new PromiseSource();
            _resultSkip = () => skipped.TryResolve();
            await Promise.Race(hold, skipped.Promise);
            _resultSkip = null;
            if (!_alive || _finishedCalled) return;
            _finishedCalled = true;
            try { _ctx.OnFinished?.Invoke(); }
            catch (Exception err) { LogError(err); }
        }

        // ---- auto play (dev / tests): the suggested drag through the hand's own path (AutoDrag) ---------

        // js: scenes/battle/player.js:2085
        private void ScheduleAuto()
        {
            if (_ctx.Auto == null || _state != BattlePlayerState.Idle) return;
            int token = ++_autoTimer;
            _real.Wait(L.AutoWaitMs).Then((Func<Promise>)(() => AutoMove(token))).Forget();
        }

        /// <summary>The body of scheduleAuto's `real.wait(L.AUTO_WAIT_MS).then(async () => …)`.</summary>
        // js: scenes/battle/player.js:2088
        private async Promise AutoMove(int token)
        {
            if (!_alive || token != _autoTimer || _state != BattlePlayerState.Idle || _busy || _hand.IsHolding()) return;
            if (_freeStep != null)
            {
                // 俊敏's choice first (ctx.auto.freeStep(source) → −1 / 0 / +1; staying when it has no opinion).
                int dir = 0;
                try { dir = _ctx.Auto.FreeStep != null ? _ctx.Auto.FreeStep(Src) : 0; }
                catch (Exception err) { LogError(err); }
                if (!PickFreeStep(dir == 1 || dir == -1 ? dir : 0)) PickFreeStep(0);
                return;
            }
            string id = "";
            int unit = -1;
            try
            {
                // choose → a card id and the enemy it goes to (several enemies)
                if (_ctx.Auto.Choose != null)
                {
                    BattleAutoPick pick = _ctx.Auto.Choose(Src, _playedThisTurn);
                    if (pick != null)
                    {
                        id = pick.Id ?? "";
                        unit = pick.Unit;
                    }
                }
                else id = Src.SuggestedCardId ?? "";
            }
            catch (Exception err)
            {
                LogError(err);
                id = "";
            }
            if (id.Length == 0)
            {
                RequestEndTurn();
                return;
            }
            CardFace face = DepictionText.Find(FrameNow.Hand, id);
            if (face == null)
            {
                RequestEndTurn();
                return;
            }
            await Safe(() => _hand.AutoDrag(id, DepictionText.RequiredZone(face.Aim), unit));
        }

        // ---- speed (#348) -----------------------------------------------------------------------------

        // js: scenes/battle/player.js:2113
        public void ShowSpeed()
        {
            BattleSpeedStep step = BattleClock.StepOfSpeed(_env.Settings.Speed);
            _hud.Speed(BattleSpeed.Label(step), BattleSpeed.Chevrons(step));
        }

        /// <summary>CycleSpeed: ×1.0 → ×1.25 → ×1.5 → ×1.0. Takes effect at once (the clock reads settings every frame).</summary>
        // js: scenes/battle/player.js:2119
        public void CycleSpeed()
        {
            BattleSpeedStep next = BattleSpeed.Next(BattleClock.StepOfSpeed(_env.Settings.Speed));
            _env.Settings.Speed = BattleClock.SpeedOfStep(next);
            Sfx("uiClick");
            ShowSpeed();
            try { _ctx.OnSettingsChange?.Invoke(_env.Settings); }
            catch (Exception err) { LogError(err); }
        }

        // ---- the hand's callbacks ---------------------------------------------------------------------

        /// <summary>
        /// The one predicted value, read against enemy `unit` in a battle of several; for a blow on an enemy, the damage that
        /// reaches its HP after its Guard (CardFace.HpLossFor(k), the same number as the HP wick; review fix).
        /// </summary>
        // js: scenes/battle/player.js:2167
        private string PreviewOf(string cardId, int unit)
        {
            if (!_alive) return "";
            int target = TargetFor(unit);
            string raw = (target < 0 || !(_ctx.Source is ITrialSource trial) ? _ctx.Source.PreviewFor(cardId) : trial.PreviewFor(cardId, target)) ?? "";
            if (raw.Length == 0) return "";
            CardFace face = DepictionText.Find(FrameNow.Hand, cardId);
            bool blow = face != null && face.Aim == CardAim.Single && face.HpLossOn != null && face.HpLossOn.Count > 0
                && face.Values != null && face.Values.Any(v => v.Kind == CardValueKind.Power);
            if (!blow) return raw;
            int u = unit >= 0 ? unit : face.TargetUnit >= 0 ? face.TargetUnit : 0;
            return Str(face.HpLossFor(u));
        }

        // ---- the api (player.js:2192-2328) ----------------------------------------------------------------

        /// <summary>player.state.</summary>
        // js: scenes/battle/player.js:2193
        public BattlePlayerState State => _state;

        /// <summary>player.busy.</summary>
        // js: scenes/battle/player.js:2194
        public bool Busy => _busy;

        /// <summary>onState(fn): notified on every state change; returns the unsubscribe.</summary>
        // js: scenes/battle/player.js:2195
        public Action OnState(Action<BattlePlayerState> fn) => _listeners.Add(fn);

        /// <summary>onEventEnd(fn): fn() after every screen event has played out with its after-wait (the scene's one-press fast-forward).</summary>
        // js: scenes/battle/player.js:2197
        public Action OnEventEnd(Action fn) => _eventEndListeners.Add(fn);

        /// <summary>How many screen events have played out (OnEventEnd's count).</summary>
        // js: scenes/battle/player.js:2199
        public int EventEnds => _eventEnds;

        /// <summary>An event has played and its after-wait runs now, its OnEventEnd still to come (review 19; BattleScene.armFF).</summary>
        // js: scenes/battle/player.js:2204
        public bool AfterWaiting => _afterWaiting;

        /// <summary>The pile list's log lines so far (dev / tests).</summary>
        // js: scenes/battle/player.js:2206
        public IReadOnlyList<string> LogLines() => _log.ToArray();

        /// <summary>
        /// handCallbacks: onRelease, onHover, onHoldStart, onHoldEnd, preview, onTarget — the hand's options the scene
        /// forwards to this player (RestingUnit is the scene's: `() => player.RestingUnit`).
        /// </summary>
        // js: scenes/battle/player.js:2137
        public HandOpts HandCallbacks { get; }

        /// <summary>Start / Restart: the opening frame at once, the chain's banner, then the automatic events.</summary>
        // js: scenes/battle/player.js:2209
        public Promise Start()
        {
            DepictionFrame frame = FrameNow;
            _snapPanels = true;
            // A new battle: each stance light is as the first frame says, with no fade (the field's first ApplyFrame).
            _field.SetStanceGlow(UnitSide.Player, StanceGlow.IsOn(frame.Player));
            ApplyFrame(frame, true);
            IReadOnlyList<UnitFrame> enemies = EnemyFrames(frame);
            for (int k = 0; k < enemies.Count; k++) _field.SnapHome(UnitSide.Enemy, enemies.Count > 1 ? k : -1);
            ShowSpeed();
            return ChainOpening().Then((Func<Promise>)(() => _alive ? RunAutomaticEvents() : null)).Catch(LogError);
        }

        /// <summary>A refused card is sliding home (the end turn waits for it).</summary>
        // js: scenes/battle/player.js:2222
        public bool Returning => _returningCard;

        /// <summary>
        /// Tab with nothing held (§3.4, two or more enemies): the floor's one N moves to the next standing enemy, left to
        /// right (`dir` −1 goes back). Returns whether it moved.
        /// </summary>
        // js: scenes/battle/player.js:2227
        public bool CycleGapFocus(int dir)
        {
            IReadOnlyList<EnemyFrame> frames = EnemyShares(FrameNow);
            var list = new List<(int U, EnemyFrame Uf)>();
            for (int k = 0; k < frames.Count; k++)
                if (!frames[k].Unit.Down) list.Add((k, frames[k]));
            if (list.Count < 2) return false;
            float GeomX(int u)
            {
                FigureGeom g = Figure(UnitSide.Enemy, u);
                return g != null ? g.HomeX : 0f;
            }
            list = list.OrderBy(x => GeomX(x.U)).ToList(); // stable, as Array.prototype.sort
            FloorFrame fl = FrameNow.Floor;
            int now;
            if (_restingGapUnit != null) now = _restingGapUnit.Value;
            else if (fl != null && fl.EnemyCell > 0)
            {
                int at = list.FindIndex(x => x.Uf.Cell == fl.EnemyCell);
                now = (at >= 0 ? list[at] : list[0]).U;
            }
            else now = list[0].U;
            int i = Math.Max(0, list.FindIndex(x => x.U == now));
            _restingGapUnit = list[(i + (dir < 0 ? list.Count - 1 : 1)) % list.Count].U;
            ShowHpLoss();
            return true;
        }

        /// <summary>The enemy a Tab at rest moved the floor's N to (§1.4 / §3.4), while it stands; −1 for none.</summary>
        // js: scenes/battle/player.js:2243
        public int RestingUnit
        {
            get
            {
                if (_restingGapUnit == null) return -1;
                IReadOnlyList<UnitFrame> list = EnemyFrames(FrameNow);
                for (int k = 0; k < list.Count; k++)
                    if (k == _restingGapUnit.Value && !list[k].Down) return _restingGapUnit.Value;
                return -1;
            }
        }

        /// <summary>The enemies of this fight, front first (for the journal).</summary>
        // js: scenes/battle/player.js:2251
        public IReadOnlyList<string> EnemyIds()
        {
            try
            {
                if (!(Src is ITrialSource trial)) throw new InvalidOperationException("the source keeps no state");
                return trial.State.Enemies.Select(u => u.Def.Id).ToList();
            }
            catch (Exception)
            {
                DepictionFrame f = FrameNow;
                return f != null && f.Enemy != null && !string.IsNullOrEmpty(f.Enemy.ArtId) ? new[] { f.Enemy.ArtId } : Array.Empty<string>();
            }
        }

        /// <summary>Whether the source would take card `id` now (read only: Inspect).</summary>
        // js: scenes/battle/player.js:2255
        public bool IsPlayable(string id)
        {
            try { return Src.Inspect(id) == PlayVerdict.Accepted; }
            catch (Exception) { return false; }
        }

        /// <summary>How many plays the source has accepted in this battle (the keyboard path's auto-select).</summary>
        // js: scenes/battle/player.js:2259
        public int AcceptedPlays => _acceptedPlays;

        /// <summary>Fast-forward is allowed while the flow runs by itself or plays a move.</summary>
        // js: scenes/battle/player.js:2261
        public bool CanFastForward() => _state == BattlePlayerState.Auto || _state == BattlePlayerState.Resolving;

        // js: scenes/battle/player.js:2262
        public void SetOverlayCheck(Func<bool> fn) => _overlayOpen = fn ?? (() => false);

        // js: scenes/battle/player.js:2263
        public void SetDrawerCheck(Func<bool> fn) => _drawerOpen = fn ?? (() => false);

        // js: scenes/battle/player.js:2264
        public void SetJournalCheck(Func<bool> fn) => _journalOpen = fn ?? (() => false);

        /// <summary>A card was pressed while the pile list is open: the reason on the refusal line (俊敏's choice's while that waits).</summary>
        // js: scenes/battle/player.js:2269
        public void RefuseWhileDrawer()
        {
            if (!_alive || _state != BattlePlayerState.Idle) return;
            if (_freeStep != null)
            {
                RefuseWhileChoosing();
                return;
            }
            Sfx("refusal");
            ShowCauseRefusal("drawer", DrawerGuide);
        }

        /// <summary>A card was pressed while the journal is open (review 17): the reason on the refusal line.</summary>
        // js: scenes/battle/player.js:2276
        public void RefuseWhileJournal()
        {
            if (!_alive || _state != BattlePlayerState.Idle) return;
            Sfx("refusal");
            ShowCauseRefusal("journal", JournalGuide);
        }

        /// <summary>The scene's overlays changed (review 21): a refusal line that asked to close one goes as soon as it is closed.</summary>
        // js: scenes/battle/player.js:2285
        public void OverlaysChanged(bool drawer, bool journal)
        {
            if (!drawer) ClearRefusal("drawer");
            if (!journal) ClearRefusal("journal");
        }

        /// <summary>
        /// A click / key once the battle is over moves the result on — once the card has stood L.ResultSkipAfterMs and after a
        /// pause of L.ResultSkipPauseMs since the last such press (a mash keeps sliding the pause on).
        /// </summary>
        // js: scenes/battle/player.js:2293
        public void SkipResult()
        {
            if (_resultSkip == null) return;
            float now = _real.Now;
            bool fresh = now - _resultLastPress >= L.ResultSkipPauseMs;
            _resultLastPress = now;
            if (_resultUpAt == null || now - _resultUpAt.Value < L.ResultSkipAfterMs || !fresh) return;
            _resultSkip();
        }

        /// <summary>Halt (DepictionPlayer.Halt): no more input, no more events.</summary>
        // js: scenes/battle/player.js:2302
        public void Halt()
        {
            _halted = true;
            CloseFreeStep(0);
            if (_hand.IsHolding()) _hand.LetGo();
            SetHandInteractable(false);
        }

        /// <summary>俊敏's choice waits (§3.8): the scene's ← / → / Enter go to it.</summary>
        // js: scenes/battle/player.js:2310
        public bool FreeStepPending => _freeStep != null;

        /// <summary>The ways the source said are open, while the choice waits; else null.</summary>
        // js: scenes/battle/player.js:2312
        public FreeStepWays? FreeStepWays => _freeStep != null ? _freeStep.Ways : (FreeStepWays?)null;

        /// <summary>Enter: the focused mark is chosen; with no focus nothing happens.</summary>
        // js: scenes/battle/player.js:2318
        public bool ConfirmFreeStep()
        {
            if (_freeStep == null) return false;
            int? f = _field.FreeStepFocus;
            return f != null && PickFreeStep(f.Value);
        }

        /// <summary>How many EffectPlan mismatches were logged (the screen waited for another step than the plan lists): 0 when every beat matches.</summary>
        public int PlanErrors => _planErrors;

        // js: scenes/battle/player.js:2323
        public void Destroy()
        {
            _alive = false;
            _autoTimer++;
            _offFrame();
        }
    }
}
