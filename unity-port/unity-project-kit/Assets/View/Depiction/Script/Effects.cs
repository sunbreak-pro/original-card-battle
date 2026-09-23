// The depiction's effects by id: how long each one plays, whether it is switched on, and a
// record of what actually played for the measurement of #78. Pure C#, like the rest of Script/:
// the View asks these types how long to animate and whether to animate at all, so `dotnet test`
// holds the same numbers the screen plays.
//
// Every effect can be switched off on its own (#75-#77). Off means "settle at once": the View sets
// the end state immediately and waits 0 ms, so a switched-off effect never holds the flow up.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Depiction
{
    /// <summary>
    /// One effect the screen plays (battle_ui_ux_v2 §3.2 / §5). The ids are stable names #78's
    /// measurement reports under; the lengths live in <see cref="EffectCatalog"/>.
    /// </summary>
    public enum EffectId
    {
        // ---- Card motion (#75, battle_ui_ux_v2 §3.2 / §5.2) ----

        /// <summary>One card flies from the draw pile into the fan. Cards leave one stagger apart.</summary>
        CardDraw,

        /// <summary>The other cards slide to their new fan places (HandFan.Place) after the hand changed.</summary>
        HandFan,

        /// <summary>The card under the pointer rises 28 px and grows to 1.05.</summary>
        CardHover,

        /// <summary>A picked-up card stands upright and grows to 1.08.</summary>
        CardGrab,

        /// <summary>The receiver fades in over the target while a single-target card is held.</summary>
        ReceiverShow,

        /// <summary>The throw line fades in above the hand while a self card is held.</summary>
        ThrowLineShow,

        /// <summary>A held card over the receiver is drawn toward the dish's middle.</summary>
        ReceiverSnap,

        /// <summary>The released card is swallowed by the dish (or tossed upward for a self card).</summary>
        CardRelease,

        /// <summary>The played card's ghost settles on the discard pile, beside the event's beats.</summary>
        CardToDiscard,

        /// <summary>A card released elsewhere slides back to its fan place.</summary>
        CardReturn,

        /// <summary>A card refused by a rule shakes once it is home.</summary>
        RefusalShake,

        /// <summary>Turn end: the whole hand flies to the discard pile, one stagger apart.</summary>
        HandDiscard,

        /// <summary>A card the stamina cannot pay for darkens (the reason line is the source's text).</summary>
        UnpayableDim,

        // ---- Attack and defence (#76, battle_ui_ux_v2 §5.5 / §5.6) ----

        /// <summary>The player steps toward the enemy before the blow.</summary>
        AttackLunge,

        /// <summary>The blow's shape (Cue.System: arc, line, band, ripple) across the target.</summary>
        StrikeShape,

        /// <summary>Both sides freeze for a moment after a normal blow (intensity 1-2).</summary>
        HitStop,

        /// <summary>The longer freeze after a strong blow (intensity 3-4).</summary>
        HitStopStrong,

        /// <summary>The struck figure blinks white or red.</summary>
        HitFlash,

        /// <summary>The struck figure reels back and shakes.</summary>
        TargetRecoil,

        /// <summary>The damage number rises from the chest.</summary>
        DamageNumber,

        /// <summary>The HP bar shrinks to its new width.</summary>
        HpDrain,

        /// <summary>The grey band left behind follows the bar down, late.</summary>
        HpTrail,

        /// <summary>A strong blow shakes the arena.</summary>
        ScreenShake,

        /// <summary>The player steps back after a blow that landed.</summary>
        AttackReturn,

        /// <summary>The enemy's move into its action, shaped by Cue.System (sweep, thrust, shove, guard, step).</summary>
        EnemyMotion,

        /// <summary>The spent omen fades once its blow has landed on the player.</summary>
        OmenSpend,

        /// <summary>The enemy steps back to where it stood.</summary>
        EnemyReturn,

        /// <summary>The shield snaps up and shows how much Guard stopped.</summary>
        GuardBlock,

        /// <summary>"Guard −n" rises in the Guard colour.</summary>
        GuardNumber,

        /// <summary>The Guard badge cracks when a blow takes it to 0.</summary>
        GuardBreak,

        /// <summary>The Guard badge pops with the Guard just gained.</summary>
        GuardGain,

        // ---- The other beats (lengths as the View plays them; #77 works them out) ----

        /// <summary>The omen badge drops in over the enemy.</summary>
        OmenShow,

        /// <summary>A trait's condition held: the burst and its label.</summary>
        TraitFire,

        /// <summary>The stamina pips change.</summary>
        StaminaChange,

        /// <summary>構え: the shield and "+3" at turn end.</summary>
        StanceCue,

        /// <summary>The player moves to the stand point N maps to, and the tag flips.</summary>
        RangeSwitch,

        /// <summary>A bonus the enemy did not get (or a whiffed blow) is struck off the omen.</summary>
        SideBonusMiss,

        // ---- Status and turn (#77, battle_ui_ux_v2 §5.1 / §5.10 / §5.13) ----

        /// <summary>A status word appears: its chip pops in.</summary>
        StatusApply,

        /// <summary>A chip's stacks grow.</summary>
        StatusStack,

        /// <summary>A chip loses a stack at its holder's turn start (or when used).</summary>
        StatusTick,

        /// <summary>A chip's last stack goes and the chip fades away.</summary>
        StatusVanish,

        /// <summary>At the player's turn start, the standing omen blinks once.</summary>
        OmenBlink,

        /// <summary>The omen flares the moment the enemy carries it out.</summary>
        OmenExecute,

        /// <summary>"押し出し" rises on the figure the other side moved.</summary>
        PushMark,

        /// <summary>"あなたの番": the banner that opens the player's turn.</summary>
        TurnBanner,

        /// <summary>"敵の番": the banner that hands the turn to the enemy.</summary>
        EnemyTurnBanner,

        /// <summary>The recovered pips light one by one.</summary>
        StaminaRecover,

        /// <summary>The one card at the end: 勝ち or 負け.</summary>
        ResultCard,
    }

    /// <summary>
    /// How one effect plays. <see cref="Ms"/> is the length of one item; an effect that plays per card
    /// starts each next item <see cref="StaggerMs"/> later. A blocking effect is waited for by the
    /// event it belongs to; the others run beside the flow and never lengthen an event.
    /// </summary>
    public sealed class EffectSpec
    {
        public EffectSpec(EffectId id, float ms, float staggerMs, bool blocking, string source)
        {
            Id = id;
            Ms = ms;
            StaggerMs = staggerMs;
            Blocking = blocking;
            Source = source;
        }

        public EffectId Id { get; }
        public float Ms { get; }
        public float StaggerMs { get; }
        public bool Blocking { get; }

        /// <summary>Where the length is written: "battle_ui_ux_v2 §3.2", or the issue that chose it.</summary>
        public string Source { get; }

        /// <summary>Start to finish for <paramref name="count"/> items: the last one starts after count − 1 staggers.</summary>
        public float TotalMs(int count)
        {
            if (count <= 0) return 0f;
            return Ms + StaggerMs * (count - 1);
        }
    }

    /// <summary>The nominal length of every effect. The View animates for exactly these.</summary>
    public static class EffectCatalog
    {
        private static readonly Dictionary<EffectId, EffectSpec> Specs = Build();

        public static IReadOnlyCollection<EffectSpec> All => Specs.Values;

        public static EffectSpec Of(EffectId id)
        {
            if (!Specs.TryGetValue(id, out EffectSpec spec))
            {
                throw new KeyNotFoundException("No length is written for effect " + id + ".");
            }
            return spec;
        }

        private static Dictionary<EffectId, EffectSpec> Build()
        {
            var list = new[]
            {
                new EffectSpec(EffectId.CardDraw, 260f, 60f, blocking: true, "battle_ui_ux_v2 §3.2 ドロー"),
                new EffectSpec(EffectId.HandFan, 120f, 0f, blocking: false, "battle_ui_ux_v2 §3.2 手札の扇"),
                new EffectSpec(EffectId.CardHover, 90f, 0f, blocking: false, "battle_ui_ux_v2 §3.2 ホバー"),
                new EffectSpec(EffectId.CardGrab, 90f, 0f, blocking: false, "battle_ui_ux_v2 §3.2 掴む"),
                new EffectSpec(EffectId.ReceiverShow, 150f, 0f, blocking: false, "battle_ui_ux_v2 §3.2 受け皿が出る"),
                new EffectSpec(EffectId.ThrowLineShow, 150f, 0f, blocking: false, "battle_ui_ux_v2 §3.2 投げ上げ線が出る"),
                new EffectSpec(EffectId.ReceiverSnap, 80f, 0f, blocking: false, "#75 受け皿への吸い付き"),
                new EffectSpec(EffectId.CardRelease, 160f, 0f, blocking: true, "battle_ui_ux_v2 §3.2 離す（皿の上 / 線より上）"),
                new EffectSpec(EffectId.CardToDiscard, 200f, 0f, blocking: false, "#75 捨て札への移動"),
                new EffectSpec(EffectId.CardReturn, 200f, 0f, blocking: false, "battle_ui_ux_v2 §3.2 離す（それ以外）"),
                new EffectSpec(EffectId.RefusalShake, 160f, 0f, blocking: false, "#75 出せない札の揺れ"),
                new EffectSpec(EffectId.HandDiscard, 240f, 60f, blocking: true, "battle_ui_ux_v2 §3.2 全捨て"),
                new EffectSpec(EffectId.UnpayableDim, 120f, 0f, blocking: false, "#75 出せない札の暗い幕"),

                // #76: the waits are the slice's (the 2.0 s budget is already spent to 1990 ms by 牽制),
                // and what the issue adds runs beside them.
                new EffectSpec(EffectId.AttackLunge, 100f, 0f, blocking: true, "#76 踏み込み（§5.5 順 1 は 180。2.0 s の予算に合わせて 100）"),
                new EffectSpec(EffectId.StrikeShape, 120f, 0f, blocking: true, "#76 系統の形（§5.3。長さは系統で変えず 120）"),
                new EffectSpec(EffectId.HitStop, 30f, 0f, blocking: true, "#76 ヒットストップ・通常（強弱 2 段の仮置き）"),
                new EffectSpec(EffectId.HitStopStrong, 90f, 0f, blocking: true, "#76 ヒットストップ・強（強弱 2 段の仮置き）"),
                new EffectSpec(EffectId.HitFlash, 260f, 0f, blocking: false, "#76 被弾の点滅"),
                new EffectSpec(EffectId.TargetRecoil, 260f, 0f, blocking: false, "#76 のけぞり"),
                new EffectSpec(EffectId.DamageNumber, 1000f, 0f, blocking: false, "#76 ダメージ数字の湧き（上昇して消えるまで）"),
                new EffectSpec(EffectId.HpDrain, 300f, 0f, blocking: true, "battle_ui_ux_v2 §5.5 順 7 HP バー"),
                new EffectSpec(EffectId.HpTrail, 500f, 0f, blocking: false, "battle_ui_ux_v2 §5.5 順 7 灰の残像"),
                new EffectSpec(EffectId.ScreenShake, 180f, 0f, blocking: false, "#76 画面の揺れ（強だけ）"),
                new EffectSpec(EffectId.AttackReturn, 120f, 0f, blocking: true, "#76 戻り（§5.5 順 8 は 220。予算に合わせて 120）"),
                new EffectSpec(EffectId.EnemyMotion, 220f, 0f, blocking: true, "#76 敵の動き（系統ごとに形を変え、長さは同じ）"),
                new EffectSpec(EffectId.OmenSpend, 200f, 0f, blocking: true, "#76 予兆の消え方"),
                new EffectSpec(EffectId.EnemyReturn, 140f, 0f, blocking: true, "#76 敵の戻り"),
                new EffectSpec(EffectId.GuardBlock, 320f, 0f, blocking: true, "battle_ui_ux_v2 §5.6 被弾（盾が受ける 240 + 止め 80）"),
                new EffectSpec(EffectId.GuardNumber, 1000f, 0f, blocking: false, "#76 Guard の数字（上昇して消えるまで）"),
                new EffectSpec(EffectId.GuardBreak, 200f, 0f, blocking: false, "battle_ui_ux_v2 §5.6 順 1 盾バッジが割れる"),
                new EffectSpec(EffectId.GuardGain, 260f, 0f, blocking: true, "#76 盾バッジのポップ"),

                new EffectSpec(EffectId.OmenShow, 240f, 0f, blocking: true, "縦切りの長さ（#77 で詰める）"),
                new EffectSpec(EffectId.TraitFire, 200f, 0f, blocking: true, "#76 特性の合図（光は 260 で並行。待つのは 200）"),
                new EffectSpec(EffectId.StaminaChange, 220f, 0f, blocking: true, "縦切りの長さ（#77 で詰める）"),
                new EffectSpec(EffectId.StanceCue, 460f, 0f, blocking: true, "縦切りの長さ（#77 で詰める）"),
                new EffectSpec(EffectId.RangeSwitch, 470f, 0f, blocking: true, "battle_ui_ux_v2 §5.7 移動 320 + 札の裏返し 150"),
                new EffectSpec(EffectId.SideBonusMiss, 260f, 0f, blocking: true, "縦切りの長さ"),

                // #77
                new EffectSpec(EffectId.StatusApply, 200f, 0f, blocking: true, "battle_ui_ux_v2 §5.10 順 2 チップが膨らんで収まる"),
                new EffectSpec(EffectId.StatusStack, 150f, 0f, blocking: true, "battle_ui_ux_v2 §5.10 順 3"),
                new EffectSpec(EffectId.StatusTick, 120f, 0f, blocking: true, "#77 スタックの減少（§5.10 順 4 は 150。敵の番の予算に合わせて 120）"),
                new EffectSpec(EffectId.StatusVanish, 150f, 0f, blocking: true, "battle_ui_ux_v2 §5.10 順 6 薄くなって消える"),
                new EffectSpec(EffectId.OmenBlink, 250f, 0f, blocking: false, "battle_ui_ux_v2 §5.1 順 8 予兆の札が明滅 1 回"),
                new EffectSpec(EffectId.OmenExecute, 200f, 0f, blocking: false, "battle_ui_ux_v2 §5.8 実行（札が一瞬塗られる）"),
                new EffectSpec(EffectId.PushMark, 1000f, 0f, blocking: false, "battle_ui_ux_v2 §5.7 順 6 押し出し（上昇して消えるまで）"),
                new EffectSpec(EffectId.TurnBanner, 300f, 0f, blocking: true, "battle_ui_ux_v2 §5.1 順 1 ターン開始のバナー"),
                new EffectSpec(EffectId.EnemyTurnBanner, 400f, 0f, blocking: true, "battle_ui_ux_v2 §5.13 順 5 敵の番のバナー"),
                new EffectSpec(EffectId.StaminaRecover, 80f, 80f, blocking: true, "battle_ui_ux_v2 §5.1 順 3 回復ピップ 1 個ずつ 80 × n"),
                new EffectSpec(EffectId.ResultCard, 400f, 0f, blocking: false, "battle_ui_ux_v2 §5.19 順 5 せり上がり（勝ち / 負けの 1 枚）"),
            };
            var map = new Dictionary<EffectId, EffectSpec>();
            foreach (EffectSpec spec in list) map.Add(spec.Id, spec);
            return map;
        }
    }

    /// <summary>
    /// Which effects play. Everything is on until switched off. A switched-off effect settles at once:
    /// <see cref="WaitMs"/> answers 0, so nothing that waits on it can stall.
    /// </summary>
    public sealed class EffectSwitches
    {
        private readonly HashSet<EffectId> _off = new HashSet<EffectId>();

        public static EffectSwitches AllOn() => new EffectSwitches();

        public static EffectSwitches AllOff()
        {
            var switches = new EffectSwitches();
            foreach (EffectId id in Enum.GetValues(typeof(EffectId))) switches.Set(id, false);
            return switches;
        }

        /// <summary>
        /// Switches off the effects named (the Inspector's list). Names that are not effects are handed
        /// back in <paramref name="unknown"/> so the caller can say so instead of ignoring a typo.
        /// </summary>
        public static EffectSwitches WithOff(IEnumerable<string> names, out List<string> unknown)
        {
            var switches = new EffectSwitches();
            unknown = new List<string>();
            if (names == null) return switches;
            foreach (string raw in names)
            {
                string name = (raw ?? "").Trim();
                if (name.Length == 0) continue;
                // Names only: Enum.TryParse would also take "7" and switch off whichever effect sits at 7.
                if (Array.IndexOf(Enum.GetNames(typeof(EffectId)), name) >= 0) switches.Set((EffectId)Enum.Parse(typeof(EffectId), name), false);
                else unknown.Add(name);
            }
            return switches;
        }

        public bool IsOn(EffectId id) => !_off.Contains(id);

        public void Set(EffectId id, bool on)
        {
            if (on) _off.Remove(id);
            else _off.Add(id);
        }

        /// <summary>How long to animate one item: the catalog length when on, 0 when off.</summary>
        public float Ms(EffectId id) => IsOn(id) ? EffectCatalog.Of(id).Ms : 0f;

        /// <summary>How long to wait between items: the catalog stagger when on, 0 when off.</summary>
        public float StaggerMs(EffectId id) => IsOn(id) ? EffectCatalog.Of(id).StaggerMs : 0f;

        /// <summary>How long the flow waits for <paramref name="count"/> items of the effect.</summary>
        public float WaitMs(EffectId id, int count = 1) => IsOn(id) ? EffectCatalog.Of(id).TotalMs(count) : 0f;
    }

    /// <summary>One effect an event waits for, with how many items it plays.</summary>
    public sealed class EffectStep
    {
        public EffectStep(EffectId id, int count)
        {
            Id = id;
            Count = count;
        }

        public EffectId Id { get; }
        public int Count { get; }
    }

    /// <summary>§5.10: which of the four chip beats a StatusChange cue is, read off its change and what is left.</summary>
    public static class StatusBeat
    {
        public static EffectId Of(Cue cue)
        {
            if (cue == null) throw new ArgumentNullException(nameof(cue));
            if (cue.StacksAfter <= 0) return EffectId.StatusVanish;
            if (cue.Amount > 0) return cue.StacksAfter == cue.Amount ? EffectId.StatusApply : EffectId.StatusStack;
            return EffectId.StatusTick;
        }
    }

    /// <summary>§5.3 強弱, held at two steps for now (#76): intensity 3 and 4 are strong.</summary>
    public static class EffectStrength
    {
        public static bool IsStrong(int intensity) => intensity >= 3;
    }

    /// <summary>
    /// The blocking effects an event plays, in order, read off its kind and its cues. The View waits
    /// for exactly these, and the tests hold the event lengths against the 2.0 s budget with them.
    /// Effects that run beside the flow (numbers, flashes, the HP trail) are not listed: they never
    /// lengthen an event.
    /// </summary>
    public static class EffectPlan
    {
        public static List<EffectStep> StepsOf(DepictionEvent ev)
        {
            if (ev == null) throw new ArgumentNullException(nameof(ev));
            var steps = new List<EffectStep>();
            if (ev.Kind == DepictionEventKind.PlayCard) steps.Add(new EffectStep(EffectId.CardRelease, 1));
            if (ev.Kind == DepictionEventKind.TurnStart) steps.Add(new EffectStep(EffectId.TurnBanner, 1));
            foreach (Cue cue in ev.Cues) AddSteps(steps, cue);
            if (ev.Kind == DepictionEventKind.TurnEnd) steps.Add(new EffectStep(EffectId.EnemyTurnBanner, 1));
            return steps;
        }

        private static void AddSteps(List<EffectStep> steps, Cue cue)
        {
            switch (cue.Kind)
            {
                // Both hand cues carry how many cards move: the ones drawn, the ones thrown away.
                case CueKind.DrawHand:
                    steps.Add(new EffectStep(EffectId.CardDraw, Math.Max(0, cue.Amount)));
                    break;
                case CueKind.DiscardHand:
                    steps.Add(new EffectStep(EffectId.HandDiscard, Math.Max(0, cue.Amount)));
                    break;
                case CueKind.StaminaChange:
                    // A gain lights its pips one by one; a spend pops beside the card's beats.
                    if (cue.Amount > 0) steps.Add(new EffectStep(EffectId.StaminaRecover, cue.Amount));
                    break;
                case CueKind.OmenShow:
                    steps.Add(new EffectStep(EffectId.OmenShow, 1));
                    break;
                case CueKind.TraitFire:
                    steps.Add(new EffectStep(EffectId.TraitFire, 1));
                    break;
                case CueKind.Slash:
                {
                    // A slash with no settled HP is the swing alone: the GuardBlock / Hit cues carry the rest.
                    bool byPlayer = cue.Source == UnitSide.Player;
                    if (byPlayer) steps.Add(new EffectStep(EffectId.AttackLunge, 1));
                    steps.Add(new EffectStep(EffectId.StrikeShape, 1));
                    steps.Add(new EffectStep(EffectStrength.IsStrong(cue.Intensity) ? EffectId.HitStopStrong : EffectId.HitStop, 1));
                    if (cue.HpAfter != Cue.Unchanged)
                    {
                        steps.Add(new EffectStep(EffectId.HpDrain, 1));
                        if (byPlayer) steps.Add(new EffectStep(EffectId.AttackReturn, 1));
                    }
                    break;
                }
                case CueKind.GuardGain:
                    steps.Add(new EffectStep(EffectId.GuardGain, 1));
                    break;
                case CueKind.RangeSwitch:
                    steps.Add(new EffectStep(EffectId.RangeSwitch, 1));
                    break;
                case CueKind.StanceCue:
                    steps.Add(new EffectStep(EffectId.StanceCue, 1));
                    break;
                case CueKind.EnemyWindup:
                    steps.Add(new EffectStep(EffectId.EnemyMotion, 1));
                    break;
                case CueKind.SideBonusMiss:
                    steps.Add(new EffectStep(EffectId.SideBonusMiss, 1));
                    break;
                case CueKind.GuardBlock:
                    steps.Add(new EffectStep(EffectId.GuardBlock, 1));
                    break;
                case CueKind.Hit:
                    steps.Add(new EffectStep(EffectId.HpDrain, 1));
                    if (cue.Target == UnitSide.Player)
                    {
                        // The blow on the player is the enemy's own: its omen is spent and it draws back.
                        steps.Add(new EffectStep(EffectId.OmenSpend, 1));
                        steps.Add(new EffectStep(EffectId.EnemyReturn, 1));
                    }
                    break;
                case CueKind.StatusChange:
                    steps.Add(new EffectStep(StatusBeat.Of(cue), 1));
                    break;
                case CueKind.GuardReset:
                    break; // settles with the frame
                default:
                    throw new ArgumentOutOfRangeException(nameof(cue), cue.Kind, "No effect is planned for this cue.");
            }
        }

        /// <summary>The time the event waits on its effects with these switches.</summary>
        public static float BlockingMs(DepictionEvent ev, EffectSwitches switches)
        {
            if (switches == null) throw new ArgumentNullException(nameof(switches));
            float ms = 0f;
            foreach (EffectStep step in StepsOf(ev)) ms += switches.WaitMs(step.Id, step.Count);
            return ms;
        }
    }

    /// <summary>
    /// What played, when and for how long (#78 reads it). The clock is injected, so the View passes
    /// the unscaled game time and a test passes its own. A switched-off effect is recorded as skipped
    /// with 0 s, so the measurement can tell "off" from "never happened".
    /// </summary>
    public sealed class EffectTrace
    {
        public sealed class Entry
        {
            public EffectId Id;
            public int EventOrder;
            public double StartSeconds;
            public double Seconds;
            public float NominalMs;
            public bool Skipped;
            public bool Finished;
        }

        private readonly Func<double> _clock;
        private readonly List<Entry> _entries = new List<Entry>();

        public EffectTrace(Func<double> clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public IReadOnlyList<Entry> Entries => _entries;

        /// <summary>Starts timing one effect; hand the returned number to <see cref="End"/>.</summary>
        public int Begin(EffectId id, int eventOrder, int count = 1)
        {
            _entries.Add(new Entry
            {
                Id = id,
                EventOrder = eventOrder,
                StartSeconds = _clock(),
                NominalMs = EffectCatalog.Of(id).TotalMs(count),
            });
            return _entries.Count - 1;
        }

        public void End(int handle)
        {
            if (handle < 0 || handle >= _entries.Count) throw new ArgumentOutOfRangeException(nameof(handle));
            Entry entry = _entries[handle];
            if (entry.Finished) return;
            entry.Seconds = _clock() - entry.StartSeconds;
            entry.Finished = true;
        }

        public void Skip(EffectId id, int eventOrder)
        {
            _entries.Add(new Entry { Id = id, EventOrder = eventOrder, StartSeconds = _clock(), Skipped = true, Finished = true });
        }

        public void Clear() => _entries.Clear();

        /// <summary>One line per effect: id, event, start, measured ms, nominal ms, skipped.</summary>
        public string ToCsv()
        {
            var sb = new StringBuilder("effect,event,start_s,measured_ms,nominal_ms,skipped\n");
            foreach (Entry e in _entries)
            {
                sb.Append(e.Id).Append(',')
                  .Append(e.EventOrder.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(e.StartSeconds.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                  .Append((e.Seconds * 1000.0).ToString("0", CultureInfo.InvariantCulture)).Append(',')
                  .Append(e.NominalMs.ToString("0", CultureInfo.InvariantCulture)).Append(',')
                  .Append(e.Skipped ? "1" : "0").Append('\n');
            }
            return sb.ToString();
        }
    }
}
