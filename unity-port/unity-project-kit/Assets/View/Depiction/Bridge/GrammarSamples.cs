// #57: the battles the Editor's sample capture plays, one per system of the effect grammar
// (battle_ui_ux_v2 §5.3) and one for the trait cue over a blow (§5.9 J2). Pure C#: BattleCore +
// Depiction.Script, no UnityEngine, so which card stands for a system, from which gap it is played
// and which effect the capture waits for are all checked under `dotnet test`. The capture itself
// (Editor/GrammarSampleCapture.cs) only plays these sources and takes the picture.
using System;
using System.Collections.Generic;
using BattleCore;

namespace Depiction.Bridge
{
    /// <summary>One sample: a card, the gap it is played from, and the effect whose middle is the picture.</summary>
    public sealed class GrammarSample
    {
        /// <summary>1-based, in the order of §5.3's table; the trait cue comes last.</summary>
        public int Index;

        /// <summary>The system the picture shows (the blow's own system for the trait cue).</summary>
        public FxSystem System;

        /// <summary>The sample shows the trait cue (§5.9 J2) laid over the blow, not the system alone.</summary>
        public bool TraitAccent;

        public CardDef Card;

        /// <summary>The gap on turn 1: the first one from which the card plays the beat this sample shows.</summary>
        public int StartGap;

        /// <summary>The effect the capture waits for: the picture is taken in its middle.</summary>
        public EffectId Watch;

        /// <summary>"01-slash.png" … "08-breath.png", "09-slash-trait-accent.png".</summary>
        public string FileName =>
            Index.ToString("00") + "-" + System.ToString().ToLowerInvariant() + (TraitAccent ? "-trait-accent" : "") + ".png";

        /// <summary>One line for the log and the index file: "斬 袈裟斬り (kesa_cut) 間合い 1 StrikeShape".</summary>
        public string Label =>
            EffectGrammar.Glyph(System) + (TraitAccent ? " + 特性の合図" : "") + " " + Card.Name + " (" + Card.Id + ") 間合い " + StartGap + " " + Watch;

        /// <summary>The battle the capture plays: the sample card only, the leftmost one suggested for the auto drag.</summary>
        public CoreBattleSource CreateSource()
        {
            return GrammarSamples.SourceFor(Card, StartGap);
        }
    }

    public static class GrammarSamples
    {
        /// <summary>
        /// Copies of the sample card in the deck: the opening hand holds nothing else, and the pile keeps
        /// cards past the hand (HandDraw 6) so a draw face has something to draw.
        /// </summary>
        public const int DeckCopies = Constants.HandDraw * 2;

        /// <summary>The gaps tried, nearest first (the slice's line holds 0〜3 from the player's start cell).</summary>
        public const int MaxGap = 4;

        /// <summary>The player opens at half HP, so a heal (息) has room to show its number and the chest's rise.</summary>
        public const int StartHp = Constants.PlayerMaxHp / 2;

        /// <summary>The nine samples: the eight systems in §5.3's order, then the trait cue over a blow.</summary>
        public static IReadOnlyList<GrammarSample> All => _all ?? (_all = Build());

        private static IReadOnlyList<GrammarSample> _all;

        public static BattleSetup SetupFor(CardDef card, int startGap)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            return new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck(new[] { card }, DeckCopies),
                BattleSetup.SliceFieldCells, StartGap: startGap, PlayerStartHp: StartHp);
        }

        /// <summary>The same source the capture hands the screen; suggestCards drives the screen's auto drag.</summary>
        public static CoreBattleSource SourceFor(CardDef card, int startGap)
        {
            return new CoreBattleSource(SetupFor(card, startGap), BattleLaunch.DefaultSeed, suggestCards: true);
        }

        /// <summary>
        /// Plays the source the way the screen's auto drag does: the turn starts, the suggested card is
        /// released on the zone its aim needs, and the events up to the next wait are read. Returns the
        /// card's play, or null when no card could be played.
        /// </summary>
        public static DepictionEvent PlayFirstCard(IDepictionSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            for (int guard = 0; !source.WaitingForPlayer; guard++)
            {
                if (source.Finished || guard > 50) return null;
                source.AdvanceAuto();
            }
            string id = source.SuggestedCardId;
            if (string.IsNullOrEmpty(id)) return null;
            CardFace face = DepictionText.Find(source.Frame.Hand, id);
            if (face == null) return null;
            if (source.TryPlay(id, DepictionText.RequiredZone(face.Aim), out DepictionEvent played) != PlayVerdict.Accepted) return null;
            if (played.Kind == DepictionEventKind.PlayCard) return played;
            for (int guard = 0; !source.WaitingForPlayer && !source.Finished && guard < 50; guard++)
            {
                DepictionEvent next = source.AdvanceAuto();
                if (next.Kind == DepictionEventKind.PlayCard) return next;
            }
            return null;
        }

        /// <summary>
        /// The effect the screen starts for <paramref name="system"/> in this play, or null when the play
        /// does not show it. A blow is its StrikeShape (§5.3) and the trait cue its TraitAccent (§5.9);
        /// 盾 / 型 / 気 draw their mark (GrammarMark); 息 is the chest's rise on a heal, else the pips lighting.
        /// </summary>
        public static EffectId? WatchOf(DepictionEvent play, FxSystem system, bool traitAccent)
        {
            if (play == null) return null;
            if (EffectGrammar.IsBlow(system))
            {
                foreach (Cue cue in play.Cues)
                {
                    if (cue.Kind != CueKind.Slash || cue.Source != UnitSide.Player || cue.Fx != system) continue;
                    if (cue.TraitAccent != traitAccent) continue;
                    return traitAccent ? EffectId.TraitAccent : EffectId.StrikeShape;
                }
                return null;
            }
            if (traitAccent) return null; // the cue rides a blow only
            if (system == FxSystem.Breath)
            {
                foreach (Cue cue in play.Cues)
                {
                    if (cue.Fx == system && EffectGrammar.MarkOf(cue) == FxMark.ChestHeave) return EffectId.BreathHeave;
                }
                foreach (Cue cue in play.Cues)
                {
                    if (cue.Fx == system && cue.Kind == CueKind.StaminaChange && cue.Amount > 0) return EffectId.StaminaRecover;
                }
                return null;
            }
            foreach (Cue cue in play.Cues)
            {
                if (cue.Fx == system && EffectGrammar.MarkOf(cue) == FxMark.Shape) return EffectId.GrammarMark;
            }
            return null;
        }

        /// <summary>
        /// The sample for one system: the first card in canon order (§2's initial forty first) whose row is
        /// that system alone, played from the first gap that shows it with no trait firing, so the picture
        /// holds the system's shape and nothing of a trait. Failing that, a gap where a trait fires; failing
        /// that, a card that shares its row with another system.
        /// </summary>
        public static GrammarSample For(FxSystem system)
        {
            Func<IReadOnlyList<FxSystem>, bool> alone = row => row.Count == 1 && row[0] == system;
            Func<IReadOnlyList<FxSystem>, bool> among = row => Contains(row, system);
            GrammarSample sample = FirstPlaying(system, false, alone, plainOnly: true)
                ?? FirstPlaying(system, false, alone, plainOnly: false)
                ?? FirstPlaying(system, false, among, plainOnly: false);
            if (sample == null) throw new InvalidOperationException("No card plays " + EffectGrammar.Glyph(system) + ".");
            return sample;
        }

        /// <summary>
        /// A trait of the card fired in this play: its beat (§5.9 順 1) is on the script. The stance a card
        /// sets is written as a TraitFire beat too (「構え・…」), but that one is the 型 face and carries its
        /// system; a trait's own beat carries none.
        /// </summary>
        public static bool TraitFired(DepictionEvent play)
        {
            if (play == null) return false;
            foreach (Cue cue in play.Cues)
            {
                if (cue.Kind == CueKind.TraitFire && !cue.Fx.HasValue) return true;
            }
            return false;
        }

        private static IReadOnlyList<GrammarSample> Build()
        {
            var samples = new List<GrammarSample>();
            foreach (FxSystem system in EffectGrammar.Systems)
            {
                GrammarSample sample = For(system);
                sample.Index = samples.Count + 1;
                samples.Add(sample);
            }
            // §5.9 J2: the trait cue on the 斬 sample's own card when its trait can be lit, so the two
            // pictures differ only by the cue; else on the first blow that lights one.
            GrammarSample slash = samples[0];
            GrammarSample accent = TryCard(slash.Card, slash.System, traitAccent: true, plainOnly: false);
            if (accent == null)
            {
                foreach (FxSystem system in EffectGrammar.Systems)
                {
                    if (!EffectGrammar.IsBlow(system)) continue;
                    accent = FirstPlaying(system, true, row => Contains(row, system), plainOnly: false);
                    if (accent != null) break;
                }
            }
            if (accent == null) throw new InvalidOperationException("No blow lights its trait from any gap.");
            accent.Index = samples.Count + 1;
            samples.Add(accent);
            return samples.AsReadOnly();
        }

        private static GrammarSample FirstPlaying(FxSystem system, bool traitAccent, Func<IReadOnlyList<FxSystem>, bool> rowFits, bool plainOnly)
        {
            foreach (CardDef def in CardCatalog.All)
            {
                if (!rowFits(CardSystems.Of(def))) continue;
                GrammarSample sample = TryCard(def, system, traitAccent, plainOnly);
                if (sample != null) return sample;
            }
            return null;
        }

        /// <param name="plainOnly">Only from a gap where no trait of the card fires.</param>
        private static GrammarSample TryCard(CardDef card, FxSystem system, bool traitAccent, bool plainOnly)
        {
            for (int gap = 0; gap <= MaxGap; gap++)
            {
                DepictionEvent play = PlayFirstCard(SourceFor(card, gap));
                if (plainOnly && TraitFired(play)) continue;
                EffectId? watch = WatchOf(play, system, traitAccent);
                if (watch.HasValue)
                {
                    return new GrammarSample { System = system, TraitAccent = traitAccent, Card = card, StartGap = gap, Watch = watch.Value };
                }
            }
            return null;
        }

        private static bool Contains(IReadOnlyList<FxSystem> row, FxSystem system)
        {
            foreach (FxSystem s in row)
            {
                if (s == system) return true;
            }
            return false;
        }
    }
}
