// CoreScriptWriter (see CoreScriptWriter.cs): the enemy a card goes to (several enemies).
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;

namespace Depiction.Bridge
{
    public sealed partial class CoreScriptWriter
    {
        // ---- the enemy a card goes to (several enemies) -----------------------------------------

        /// <summary>
        /// js: CoreScriptWriter.js:927 — the enemy a card aimed at one enemy goes to when the screen names
        /// none: CoreBattleSource.TargetFor, and the target every number on the card's face is read
        /// against. The core takes the target from whoever releases the card (TurnLoop.PlayCard's
        /// <c>target</c>, §7.4 受け皿で 1 体); this picks the receiver the way a player dropping on "the
        /// enemy" means it: among the standing enemies the card can be played on now (TurnLoop.CanPlay),
        /// the one it costs least on, the nearest the player (lowest cell) of those; else, among the ones
        /// inside its reach (TurnLoop.Preview's InReach), the one it costs least on, the nearest of those;
        /// else the nearest standing one (BattleState.Nearest, the enemy the core itself reads when the
        /// one named does not stand); 0 when none stands. The cost comes first so that the face, whose
        /// cost is read on this enemy, shows the lowest cost the card has on any enemy in reach — the same
        /// "any enemy" reading as its lamp (battle-visual-v1 §6.3 決定 6): a lit コスト −1 lamp never sits
        /// over an undiscounted cost circle. The cost differs between enemies only through a trait read on
        /// the gap (石礫), so in every other case this is the nearest. A card aimed at nobody or at every
        /// enemy in reach ignores the target and gets the same fallback. With one enemy this is always 0,
        /// the target the one-enemy source passed.
        /// </summary>
        public static int TargetOf(BattleState state, string instanceId)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            int nearest = state.Nearest;
            int fallback = nearest < 0 ? 0 : nearest;
            CardInstance card = null;
            foreach (CardInstance c in state.Hand)
            {
                if (c.InstanceId == instanceId)
                {
                    card = c;
                    break;
                }
            }
            if (card == null || !AimsAtOne(card.Def)) return fallback;
            List<int> line = LineOf(state);
            int playable = Cheapest(state, instanceId, line.Where(unit => TurnLoop.CanPlay(state, instanceId, unit) == PlayRefusal.None).ToList());
            if (playable >= 0) return playable;
            int inReach = Cheapest(state, instanceId, line.Where(unit =>
            {
                PlayPreview preview = TurnLoop.Preview(state, instanceId, unit);
                return preview != null && preview.InReach;
            }).ToList());
            return inReach >= 0 ? inReach : fallback;
        }

        /// <summary>
        /// js: CoreScriptWriter.js:953 — of <paramref name="units"/> (nearest first), the one card
        /// <paramref name="instanceId"/> costs least on (TurnLoop.Preview's Cost), the first of them on a
        /// tie; −1 when <paramref name="units"/> is empty.
        /// </summary>
        private static int Cheapest(BattleState state, string instanceId, IReadOnlyList<int> units)
        {
            int best = -1;
            int bestCost = 0;
            foreach (int unit in units)
            {
                PlayPreview preview = TurnLoop.Preview(state, instanceId, unit);
                int cost = preview != null ? preview.Cost : int.MaxValue;
                if (best < 0 || cost < bestCost)
                {
                    best = unit;
                    bestCost = cost;
                }
            }
            return best;
        }

        /// <summary>
        /// js: CoreScriptWriter.js:973 — battle-visual-v1 §5.2 スタミナ / §6.3 予測値もその敵で, for each
        /// enemy slot the cost the core charges for <paramref name="card"/> played on that enemy now
        /// (TurnLoop.Preview's Cost on it). A fallen slot, or a card the state no longer holds, gets
        /// <paramref name="shown"/> (the face's Cost). Empty for a card that does not aim at one enemy:
        /// its cost does not depend on a target.
        /// </summary>
        private static List<int> CostOn(BattleState state, CardInstance card, int shown)
        {
            var on = new List<int>();
            if (!AimsAtOne(card.Def)) return on;
            var living = new HashSet<int>(state.Living);
            for (int k = 0; k < state.Enemies.Count; k++)
            {
                PlayPreview preview = living.Contains(k) ? TurnLoop.Preview(state, card.InstanceId, k) : null;
                on.Add(preview != null ? preview.Cost : shown);
            }
            return on;
        }

        /// <summary>js: CoreScriptWriter.js:985 — a card the player releases on one enemy (targets one, aimed at the opponent).</summary>
        private static bool AimsAtOne(CardDef def)
        {
            return def.Targets == TargetKind.One && EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets);
        }

        /// <summary>js: CoreScriptWriter.js:990 — the standing enemies, nearest the player first (living enemies never share a cell).</summary>
        private static List<int> LineOf(BattleState state)
        {
            return state.Living.OrderBy(unit => state.Enemies[unit].Body.Cell).ToList();
        }

        /// <summary>
        /// js: CoreScriptWriter.js:1000 — battle-visual-v1 §5.2, for each enemy slot whether
        /// <paramref name="def"/> can be aimed at that enemy now: it stands and its N is inside the card's
        /// reach, the core's own test (Reach.Contains on BattleState.GapTo, as TurnLoop's aim check and
        /// Reached read it). Empty for a card aimed at nobody.
        /// </summary>
        private static List<bool> ReachOn(BattleState state, CardDef def)
        {
            var on = new List<bool>();
            if (!EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets)) return on;
            Reach reach = def.Face.ReachOrDefault;
            for (int k = 0; k < state.Enemies.Count; k++) on.Add(state.Enemies[k].Alive && reach.Contains(state.GapTo(k)));
            return on;
        }
    }
}
