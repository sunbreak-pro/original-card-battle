using System;
using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>
    /// What one finished battle adds up to (§12 の結果画面, #191), counted from its event stream: who
    /// won, in how many turns, the HP left, the attributes of the cards played (a two-attribute card
    /// counts once for each) and how often a card's trait held.
    /// </summary>
    public sealed record BattleTally(
        string EnemyId,
        GameResult Result,
        int Turns,
        int HpLeft,
        int MaxHp,
        int CardsPlayed,
        IReadOnlyDictionary<BattleAttribute, int> Attributes,
        int TraitsFired)
    {
        /// <summary>The four attributes (§2.1), for a breakdown printed the same way every time.</summary>
        public static readonly IReadOnlyList<BattleAttribute> Order = new[]
        {
            BattleAttribute.Attack, BattleAttribute.Guard, BattleAttribute.Skill, BattleAttribute.Stance,
        };

        public int CountOf(BattleAttribute attribute) => Attributes.TryGetValue(attribute, out int n) ? n : 0;
    }

    /// <summary>
    /// §12 連戦モード, the demo's first shape (#191): what carries from one battle to the next, the
    /// rest that may come between, and the tally of each battle. The order of the battles and the
    /// choices are the demo flow's (Depiction.Bridge.DemoSession); this holds the rules.
    ///
    /// Carried: HP and current stamina. Not carried (2026-09-23): the order of the draw and discard
    /// piles — the deck is shuffled afresh each battle — nor statuses, Guard or the stance slot.
    /// </summary>
    public static class Chain
    {
        /// <summary>The HP and current stamina a finished battle hands the next one (BattleSetup.PlayerStartHp / PlayerStartStamina).</summary>
        public static (int Hp, int Stamina) Carry(BattleState finished)
        {
            if (finished == null) throw new ArgumentNullException(nameof(finished));
            return (finished.Player.Hp, finished.Player.Stamina);
        }

        /// <summary>
        /// §12 / §3.1 階層間の休憩: HP back by 30% of the maximum (rounded away from zero) up to the
        /// maximum, and stamina full.
        /// </summary>
        public static (int Hp, int Stamina) Rest(int hp, int maxHp, int maxStamina)
        {
            int gain = (int)Math.Round(maxHp * Constants.ChainRestHpPercent / 100.0, MidpointRounding.AwayFromZero);
            return (Math.Min(maxHp, hp + gain), maxStamina);
        }

        /// <summary>
        /// §12's result screen for a whole chain: the cards, their attributes and the traits that
        /// held, added up over every battle; turns summed; the result and the HP of the last battle.
        /// </summary>
        public static BattleTally Total(IReadOnlyList<BattleTally> tallies)
        {
            if (tallies == null) throw new ArgumentNullException(nameof(tallies));
            if (tallies.Count == 0) throw new ArgumentException("A chain total needs at least one battle.", nameof(tallies));
            var attributes = new Dictionary<BattleAttribute, int>();
            int turns = 0, played = 0, fired = 0;
            foreach (var tally in tallies)
            {
                turns += tally.Turns;
                played += tally.CardsPlayed;
                fired += tally.TraitsFired;
                foreach (var attribute in BattleTally.Order)
                {
                    int n = tally.CountOf(attribute);
                    if (n == 0) continue;
                    attributes.TryGetValue(attribute, out int sum);
                    attributes[attribute] = sum + n;
                }
            }
            var last = tallies[tallies.Count - 1];
            return new BattleTally("", last.Result, turns, last.HpLeft, last.MaxHp, played, attributes, fired);
        }

        // ---- 瘴気 across a chain (§12, #52) ----

        /// <summary>§12: before each battle the gauge gains the layer's density × this many percent (濃度 1 なら 3%).</summary>
        public const int MiasmaPerDensity = 3;

        /// <summary>concept-v3 §6: 100% is 瘴気死. The chain keeps the gauge at or under it.</summary>
        public const int MiasmaMax = 100;

        /// <summary>concept-v3 §6: one point of max stamina per full 20%.</summary>
        public const int MiasmaStepPercent = 20;

        /// <summary>battle_core_v4 §13: the penalty stops at −4 (max stamina 6 at 80%).</summary>
        public const int MiasmaMaxPenalty = 4;

        /// <summary>§12: the gauge after one more battle's worth at this density, kept to 0..100.</summary>
        public static int AccumulateMiasma(int percent, int density)
        {
            if (density < 0) throw new ArgumentOutOfRangeException(nameof(density), density, "A density is never negative.");
            return Math.Min(MiasmaMax, Math.Max(0, percent) + density * MiasmaPerDensity);
        }

        /// <summary>
        /// concept-v3 §6 / battle_core_v4 §1: the max stamina a battle starts from at this gauge —
        /// BASE_MAX_STAMINA less 1 per full 20% (at most 4), kept to 3〜14. The same rule as the
        /// exploration side's (DungeonCore.Miasma.MaxStamina with no temporary modifier).
        /// </summary>
        public static int MaxStaminaAt(int miasmaPercent)
        {
            int clamped = Math.Max(0, Math.Min(MiasmaMax - 1, miasmaPercent));
            int penalty = Math.Min(MiasmaMaxPenalty, clamped / MiasmaStepPercent);
            return Math.Max(3, Math.Min(14, Constants.BaseMaxStamina - penalty));
        }

        /// <summary>The tally of one battle, from its final state and every event it emitted.</summary>
        public static BattleTally Tally(BattleState finished, IEnumerable<BattleEvent> events)
        {
            if (finished == null) throw new ArgumentNullException(nameof(finished));
            if (events == null) throw new ArgumentNullException(nameof(events));

            var attributes = new Dictionary<BattleAttribute, int>();
            int played = 0;
            int fired = 0;
            foreach (var e in events)
            {
                if (e is CardPlayed card && card.Actor == Actor.Player)
                {
                    played++;
                    // §2.1 (v4.4): a card is counted once, as the one attribute it folds to.
                    var counted = card.Card.Def.Attribute;
                    if (counted != BattleAttribute.None)
                    {
                        attributes.TryGetValue(counted, out int n);
                        attributes[counted] = n + 1;
                    }
                }
                else if (e is TraitEvaluated trait && trait.Actor == Actor.Player && trait.Outcome.Triggered)
                {
                    fired++;
                }
            }
            return new BattleTally(
                finished.EnemyDef.Id, finished.Result, finished.Turn, finished.Player.Hp, finished.Player.MaxHp,
                played, attributes, fired);
        }
    }
}
