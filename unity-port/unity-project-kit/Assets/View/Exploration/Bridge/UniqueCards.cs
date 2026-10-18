// Which learned cards a won encounter hands the player (encounters_and_rewards_v4.md §4.2, #99). The
// owners of the cards are the card table's (swordsman_cards_v4.md §3, cards lane); this file copies
// them per enemy id, and is changed when the card table moves a card to another owner.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using DungeonCore;

namespace Exploration.Bridge
{
    /// <summary>What a won encounter teaches: cards learned at once, and cards to choose one from.</summary>
    public sealed class UniqueCardReward
    {
        public static readonly UniqueCardReward None = new UniqueCardReward(Array.Empty<string>(), Array.Empty<string>());

        public UniqueCardReward(IReadOnlyList<string> learned, IReadOnlyList<string> offered)
        {
            Learned = learned ?? Array.Empty<string>();
            Offered = offered ?? Array.Empty<string>();
        }

        /// <summary>Card ids learned on the spot, in card-table order.</summary>
        public IReadOnlyList<string> Learned { get; }

        /// <summary>Card ids the player picks one from. Empty when nothing is offered.</summary>
        public IReadOnlyList<string> Offered { get; }

        public bool IsEmpty => Learned.Count == 0 && Offered.Count == 0;
    }

    public static class UniqueCards
    {
        /// <summary>§4.2: a real boss teaches its three cards (roster §4.5 / §5.5 / §6.6 「撃破で 3 種を習得」).</summary>
        private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> BossCards = new Dictionary<string, IReadOnlyList<string>>
        {
            ["miasma_priest"] = Ids(CardCatalog.MiasmaBlade, CardCatalog.PriestPrayer, CardCatalog.PurgeFlash),   // 大黒蛇 セルク #41〜#43
            ["abyss_angler"] = Ids(CardCatalog.LineLash, CardCatalog.AbyssStance, CardCatalog.HaulStep),         // 獄竜 ガルド #44〜#46
            ["distortion_root"] = Ids(CardCatalog.RootStride, CardCatalog.ThornShot, CardCatalog.RootBind),     // 歪みの根 #47〜#49
        };

        /// <summary>§4.2: a real elite offers its two cards, one to pick.</summary>
        private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> EliteCards = new Dictionary<string, IReadOnlyList<string>>
        {
            ["armored_warden"] = Ids(CardCatalog.Bulwark, CardCatalog.ShieldBash),             // 鉄壁の門竜 #50 / #51
            ["pack_alpha"] = Ids(CardCatalog.PackHowl, CardCatalog.FangRush),                  // 統牙の長竜 #52 / #53
            ["polearm_unyielding"] = Ids(CardCatalog.RiposteStance, CardCatalog.SpearWall),    // 不退の槍竜 #55 / #57
        };

        /// <summary>The cards a boss teaches, or an empty list for an enemy that teaches none.</summary>
        public static IReadOnlyList<string> OfBoss(string enemyId) =>
            enemyId != null && BossCards.TryGetValue(enemyId, out var ids) ? ids : Array.Empty<string>();

        /// <summary>The cards an elite offers, or an empty list for an enemy that offers none.</summary>
        public static IReadOnlyList<string> OfElite(string enemyId) =>
            enemyId != null && EliteCards.TryGetValue(enemyId, out var ids) ? ids : Array.Empty<string>();

        /// <summary>
        /// §4.2 for a won encounter on a node of <paramref name="kind"/>. Cards the player already owns
        /// are left out.
        ///
        /// - A real boss (セルク / ガルド / 歪みの根): its three cards, all learned.
        /// - A stand-in boss that is an elite (門竜 on layer 4, 不退 on layer 5): that elite's two cards,
        ///   all learned.
        /// - A real elite: its two cards offered, one to pick.
        ///
        /// Everything else on the table — the layer's card pile, consumables and pages, and the pile a
        /// stand-in elite or a layer 1〜2 stand-in boss draws from — is the rest of the reward table,
        /// not a unique card, and is not handed back here.
        /// </summary>
        public static UniqueCardReward For(NodeKind kind, Encounter encounter, IReadOnlyCollection<string> owned)
        {
            if (encounter == null) throw new ArgumentNullException(nameof(encounter));
            if (owned == null) throw new ArgumentNullException(nameof(owned));

            var enemies = encounter.EnemyIds.ToList();
            if (enemies.Count != 1) return UniqueCardReward.None; // pairs and chains are all stand-ins
            string enemy = enemies[0];

            switch (kind)
            {
                case NodeKind.Boss when !encounter.IsStandIn:
                    return new UniqueCardReward(NotOwned(OfBoss(enemy), owned), null);
                case NodeKind.Boss:
                    return new UniqueCardReward(NotOwned(OfElite(enemy), owned), null);
                case NodeKind.Elite when !encounter.IsStandIn:
                    return new UniqueCardReward(null, NotOwned(OfElite(enemy), owned));
                default:
                    return UniqueCardReward.None;
            }
        }

        private static IReadOnlyList<string> NotOwned(IReadOnlyList<string> ids, IReadOnlyCollection<string> owned) =>
            ids.Where(id => !owned.Contains(id)).ToList();

        private static IReadOnlyList<string> Ids(params CardDef[] defs) => defs.Select(d => d.Id).ToList();
    }
}
