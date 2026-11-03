// The Trial battle's own pacing: where the Unity build plays a beat slower than the JS trial.
//
// Koudai-san (2026-11-02): in the Unity Editor the stance, guard and movement animations read too fast; each is slowed
// by about 0.2–0.4 s so the eye can follow it. This is the one place the Trial departs from the JS lengths (TRIAL-PORT.md
// "the JS look and feel wins"): every other length still comes from EffectCatalog unchanged, and the old Depiction view
// (Battle.unity) keeps the catalog, because only the Trial's switches are built here.
using System;
using Depiction;

namespace Trial.Battle
{
    /// <summary>The lengths the Trial battle plays slower than the catalog (nominal ms at battle speed ×1.25).</summary>
    public static class BattlePacing
    {
        /// <summary>FloorMove: the walk, 400 for the first cell and 200 for each next (catalog 160 a cell: +240 / +280 / +320 for 1 / 2 / 3 cells).</summary>
        public const float FloorMoveMs = 400f;
        public const float FloorMoveStaggerMs = 200f;

        /// <summary>GuardGain: the Guard badge's pop (catalog 260, +240).</summary>
        public const float GuardGainMs = 500f;

        /// <summary>GuardBlock: the shield that takes a blow (catalog 320, +280). Fx.ShieldBlock keeps its 3 : 1 pop to hold.</summary>
        public const float GuardBlockMs = 600f;

        /// <summary>GuardBreak: the badge that cracks at 0 (catalog 200, +200).</summary>
        public const float GuardBreakMs = 400f;

        /// <summary>StanceCue (構え): the end-turn button's 200 pop, then the shield and "+n" (catalog 460, +300).</summary>
        public const float StanceCueMs = 760f;

        /// <summary>The end-turn button's pop that opens StanceCue (unchanged from the JS 200).</summary>
        public const float StanceCuePopMs = 200f;

        /// <summary>StanceGlow: the body's light as a stance is taken or dropped (catalog 300, +300).</summary>
        public const float StanceGlowMs = 600f;

        /// <summary>
        /// The 盾 ring and the 型 crest of GrammarMark (catalog 300, +250). The 気 sigil keeps the catalog length, since only
        /// stance and guard were asked to slow.
        /// </summary>
        public const float GuardStanceMarkMs = 550f;

        /// <summary>STANCE_FLY_MS: a stance card (an enemy's stance omen) flies to the stance list (JS 300, +250).</summary>
        public const float StanceFlyMs = 550f;

        /// <summary>
        /// A stance card leaves for the exile pile this long after its release (catalog 600, +250), so it still rests in the
        /// slot about 300 ms after the slower flight, as it did at 300 + 300.
        /// </summary>
        public const float StanceExileDelayMs = 850f;

        /// <summary>A fresh set of switches, everything on, with the slower lengths above.</summary>
        public static EffectSwitches Switches() => Apply(EffectSwitches.AllOn());

        /// <summary>Gives <paramref name="switches"/> the slower lengths (the on / off state is left as it is).</summary>
        public static EffectSwitches Apply(EffectSwitches switches)
        {
            if (switches == null) throw new ArgumentNullException(nameof(switches));
            // #832: the old Depiction view plays §5.1's turn-start rows together; the Trial keeps the JS
            // trial's turn start, every beat after the one before.
            return switches
                .SetTurnStartEndToEnd(true)
                .SetLength(EffectId.FloorMove, FloorMoveMs, FloorMoveStaggerMs)
                .SetLength(EffectId.GuardGain, GuardGainMs)
                .SetLength(EffectId.GuardBlock, GuardBlockMs)
                .SetLength(EffectId.GuardBreak, GuardBreakMs)
                .SetLength(EffectId.StanceCue, StanceCueMs)
                .SetLength(EffectId.StanceGlow, StanceGlowMs);
        }

        /// <summary>GrammarMark's length for a system: the 盾 ring and the 型 crest play slower, the rest keep <paramref name="markMs"/>.</summary>
        public static float MarkMs(FxSystem? fx, float markMs)
        {
            if (!(markMs > 0f) || !fx.HasValue) return markMs;
            FxForm form;
            try { form = EffectGrammar.FormOf(fx.Value); }
            catch (Exception) { return markMs; }
            return form == FxForm.Ring || form == FxForm.Crest ? Math.Max(markMs, GuardStanceMarkMs) : markMs;
        }
    }
}
