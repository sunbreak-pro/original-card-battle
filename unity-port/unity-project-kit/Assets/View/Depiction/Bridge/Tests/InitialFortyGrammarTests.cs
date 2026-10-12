// #57: the effect grammar on the forty cards a run starts with (swordsman_cards_v4 §2). EffectGrammarTests
// holds the table for all eighty; this file names the forty one by one and plays each through the
// source the screen plays (CoreBattleSource, the auto drag's release), so what is checked is the
// systems the screen is handed for the play, not the table alone. Runs under `dotnet test`
// (Depiction.Bridge.Tests.csproj) and in Unity's EditMode runner.
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class InitialFortyGrammarTests
    {
        /// <summary>swordsman_cards_v4 §2 「初期 40 種」, #1 to #40, by id.</summary>
        private static readonly string[] InitialForty =
        {
            "thrust", "kesa_cut", "overhead", "wrist_cut", "side_sweep",
            "flat_strike", "probe_thrust", "pierce", "throw_blade", "reach_thrust",
            "brace", "iron_block", "low_guard", "riposte_guard", "deep_breath",
            "water_stance", "rock_stance", "flow_stance", "first_aid", "spirit_roar",
            "focus", "observe", "war_cry", "second_wind", "footwork",
            "back_leap", "slide_step", "break_off", "lunge", "feint",
            "boar_rush", "rend", "body_check", "stone_throw", "parry_cut",
            "guard_thrust", "step_in_guard", "step_out_guard", "iron_wall", "twist_away",
        };

        private static IEnumerable<CardDef> Cards40 => InitialForty.Select(CardCatalog.ById);

        /// <summary>What a fired trait adds on screen (§5.9): Guard +n is 盾, a status +1 is 気, stamina / draw / next turn's recovery is 息.</summary>
        private static readonly FxSystem[] TraitSystems = { FxSystem.Shield, FxSystem.Spirit, FxSystem.Breath };

        /// <summary>
        /// What the screen draws for a play, in order: each system a beat of the play is marked with
        /// (Cue.Fx: the blow's shape, the 盾 / 型 / 気 mark, the 息 pips, draw or chest), once.
        /// </summary>
        private static List<FxSystem> Drawn(DepictionEvent play)
        {
            var drawn = new List<FxSystem>();
            foreach (Cue cue in play.Cues)
            {
                if (cue.Fx.HasValue && !drawn.Contains(cue.Fx.Value)) drawn.Add(cue.Fx.Value);
            }
            return drawn;
        }

        /// <summary>The systems the faces draw: the drawn ones that belong to the row, in the order drawn.</summary>
        private static List<FxSystem> FacesDrawn(DepictionEvent play, IReadOnlyList<FxSystem> row)
        {
            return Drawn(play).Where(row.Contains).ToList();
        }

        /// <summary>The look of a list of systems: their looks in order ("fx.arc+fx.ring").</summary>
        private static string LookOf(IEnumerable<FxSystem> systems)
        {
            return string.Join("+", systems.Select(EffectGrammar.LookId));
        }

        /// <summary>
        /// The card's play from the nearest gap where no trait fires, so the screen shows its faces alone;
        /// a card whose trait fires from every gap it can be played from (初手, 手薄 …) is played from the
        /// nearest one. Null when the auto drag cannot play it at all.
        /// </summary>
        private static DepictionEvent PlayFaces(CardDef def, out bool traitFired)
        {
            DepictionEvent firstPlayable = null;
            for (int gap = 0; gap <= GrammarSamples.MaxGap; gap++)
            {
                DepictionEvent play = GrammarSamples.PlayFirstCard(GrammarSamples.SourceFor(def, gap));
                if (play == null) continue;
                if (!GrammarSamples.TraitFired(play))
                {
                    traitFired = false;
                    return play;
                }
                if (firstPlayable == null) firstPlayable = play;
            }
            traitFired = firstPlayable != null;
            return firstPlayable;
        }

        [Test]
        public void TheCoresFirstForty_AreTheCanonsInitialForty_InCanonOrder()
        {
            Assert.That(CardCatalog.All.Take(40).Select(d => d.Id), Is.EqualTo(InitialForty));
        }

        [Test]
        public void EachOfTheForty_IsPlayable_AndTheScreenDrawsItsRow_PlusOnlyWhatAFiredTraitAdds()
        {
            var report = new StringBuilder();
            foreach (CardDef def in Cards40)
            {
                DepictionEvent play = PlayFaces(def, out bool traitFired);
                Assert.That(play, Is.Not.Null, def.Name + " cannot be played from any gap 0〜" + GrammarSamples.MaxGap);
                IReadOnlyList<FxSystem> row = CardSystems.Table[def.Id];
                Assert.That(play.Systems, Is.EqualTo(row), def.Name + ": the play carries its row");
                List<FxSystem> drawn = Drawn(play);
                Assert.That(FacesDrawn(play, row), Is.EqualTo(row), def.Name + ": every system of the row is drawn by a beat, in the row's order");
                // §5.3: a skill face that puts a status on is 気 as a face, but its stamina / draw / heal
                // beats are still 息 (息継ぎ, 集中); a fired trait adds its own effect's system (§5.9).
                FxFaces faces = CardSystems.FacesOf(def.Name, def.Attributes, def.Face);
                bool breathInsideSpirit = faces.PutsStatus && faces.Breathes;
                foreach (FxSystem extra in drawn.Where(s => !row.Contains(s)))
                {
                    bool allowed = (traitFired && TraitSystems.Contains(extra)) || (breathInsideSpirit && extra == FxSystem.Breath);
                    Assert.That(allowed, Is.True, def.Name + ": " + EffectGrammar.Glyph(extra) + " is drawn but is neither a face, a beat of one, nor a fired trait's effect");
                }
                report.Append(def.Name).Append(": ").Append(LookOf(drawn))
                    .Append(traitFired ? "  (特性が発火)" : "").Append(breathInsideSpirit ? "  (気の面に息の拍)" : "").Append('\n');
            }
            TestContext.Out.Write(report.ToString());
        }

        /// <summary>
        /// What the View starts for one cue of a play, read the way the View reads it, and the system that
        /// picture belongs to. A player's blow is drawn by Attack from the cue's StrikeSystem (not its Fx);
        /// 盾 / 型 / 気 only when MarkOf starts a GrammarMark; 息 is the chest on a heal, the pips lighting
        /// on a stamina gain, or the cards flying in on a draw. Null when the cue draws no system.
        /// </summary>
        private static (FxSystem System, EffectId Effect)? ScreenPicture(Cue cue)
        {
            if (cue.Kind == CueKind.Slash && EffectPlan.AttackerOf(cue) == UnitSide.Player)
            {
                FxSystem? blow = EffectGrammar.FromStrike(cue.System);
                return blow.HasValue ? (blow.Value, EffectId.StrikeShape) : ((FxSystem, EffectId)?)null;
            }
            switch (EffectGrammar.MarkOf(cue))
            {
                case FxMark.Shape: return (cue.Fx.Value, EffectId.GrammarMark);
                case FxMark.ChestHeave: return (FxSystem.Breath, EffectId.BreathHeave);
            }
            if (cue.Kind == CueKind.StaminaChange && cue.Amount > 0) return (FxSystem.Breath, EffectId.StaminaRecover);
            if (cue.Kind == CueKind.DrawHand && cue.Amount > 0) return (FxSystem.Breath, EffectId.CardDraw);
            return null;
        }

        [Test]
        public void EachOfTheForty_TheScreenPicturesEveryFace_FromWhatItReads()
        {
            var report = new StringBuilder();
            foreach (CardDef def in Cards40)
            {
                DepictionEvent play = PlayFaces(def, out _);
                Assert.That(play, Is.Not.Null, def.Name);
                IReadOnlyList<FxSystem> row = CardSystems.Table[def.Id];
                List<EffectStep> steps = EffectPlan.StepsOf(play);
                var pictured = new List<FxSystem>();
                var line = new List<string>();
                foreach (Cue cue in play.Cues)
                {
                    var picture = ScreenPicture(cue);
                    if (!picture.HasValue || !row.Contains(picture.Value.System)) continue;
                    if (EffectCatalog.Of(picture.Value.Effect).Blocking)
                    {
                        // A waited picture is one of the plan's steps: the screen holds the flow at it.
                        Assert.That(steps.Any(s => s.Id == picture.Value.Effect), Is.True, def.Name + ": " + picture.Value.Effect + " is not on the plan");
                    }
                    if (!pictured.Contains(picture.Value.System)) pictured.Add(picture.Value.System);
                    line.Add(EffectGrammar.Glyph(picture.Value.System) + " " + picture.Value.Effect);
                }
                Assert.That(pictured, Is.EqualTo(row), def.Name + ": every face of the row has a picture on screen, in the row's order");
                report.Append(def.Name).Append(": ").Append(string.Join(", ", line)).Append('\n');
            }
            TestContext.Out.Write(report.ToString());
            // Every system appears among the forty, so the eight samples are all cards a run starts with.
            var all = new HashSet<FxSystem>(Cards40.SelectMany(d => CardSystems.Table[d.Id]));
            Assert.That(all, Is.EquivalentTo(EffectGrammar.Systems));
        }
    }
}
