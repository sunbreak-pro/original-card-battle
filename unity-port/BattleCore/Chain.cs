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
        /// <summary>The attributes in §2.2 order, for a breakdown printed the same way every time.</summary>
        public static readonly IReadOnlyList<BattleAttribute> Order = new[]
        {
            BattleAttribute.Attack, BattleAttribute.Move, BattleAttribute.Guard, BattleAttribute.Skill, BattleAttribute.Stance,
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
                    foreach (var attribute in BattleTally.Order)
                    {
                        if (!card.Card.Def.Attributes.HasFlag(attribute)) continue;
                        attributes.TryGetValue(attribute, out int n);
                        attributes[attribute] = n + 1;
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
