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

    /// <summary>
    /// The blocking effects an event plays, in order, read off its kind and its cues. The View waits
    /// for exactly these, and the tests hold the event lengths against the 2.0 s budget with them.
    /// Beats that are not effects yet (the numbers, the slash) are not listed here; #76 / #77 add theirs.
    /// </summary>
    public static class EffectPlan
    {
        public static List<EffectStep> StepsOf(DepictionEvent ev)
        {
            if (ev == null) throw new ArgumentNullException(nameof(ev));
            var steps = new List<EffectStep>();
            if (ev.Kind == DepictionEventKind.PlayCard) steps.Add(new EffectStep(EffectId.CardRelease, 1));
            foreach (Cue cue in ev.Cues)
            {
                // Both cues carry how many cards move: the ones drawn, the ones thrown away.
                switch (cue.Kind)
                {
                    case CueKind.DrawHand:
                        steps.Add(new EffectStep(EffectId.CardDraw, Math.Max(0, cue.Amount)));
                        break;
                    case CueKind.DiscardHand:
                        steps.Add(new EffectStep(EffectId.HandDiscard, Math.Max(0, cue.Amount)));
                        break;
                }
            }
            return steps;
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
