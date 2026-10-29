using System;
using System.Collections.Generic;
using System.Linq;

namespace BattleCore
{
    /// <summary>
    /// What a battle starts from. FieldCells has no default (2026-09-23, #169): the layer and the
    /// battle hand it in (5〜8, the dungeon side picks it in #168). The rest are §7.3's: the player
    /// on cell 2, the enemy START_GAP (3) cells further (<see cref="Field.EnemyStartCell"/> for a
    /// line too short), both with full stamina and Guard 0.
    ///
    /// MoreEnemies (§7.4, #47) are the second and third enemy, in phase order. Each starts on the
    /// first free cell behind the one before it; a line too short to hold them all is refused.
    ///
    /// The last three are the departure tools that act inside a battle (§9.1, #452), as amounts:
    /// PlayerStartStaminaBonus is 息継ぎの笛 (+3 a flute), FirstHitGuard 竜鱗の欠片 (Guard 5 a
    /// scale, given once, just before the first enemy blow or wall that reaches the player) and
    /// BossWordWard 竜語の符 (the boss-only words sit 1 stack lower a charm). 0 is no tool.
    /// </summary>
    public sealed record BattleSetup(
        EnemyDef Enemy,
        IReadOnlyList<CardInstance> Deck,
        int FieldCells,
        int StartGap = Constants.StartGap,
        int PlayerStartCell = Constants.PlayerStartCell,
        int PlayerMaxHp = Constants.PlayerMaxHp,
        int PlayerMaxStamina = Constants.BaseMaxStamina,
        IReadOnlyList<EnemyDef>? MoreEnemies = null,
        int? PlayerStartHp = null,
        int? PlayerStartStamina = null,
        int PlayerStartStaminaBonus = 0,
        int FirstHitGuard = 0,
        int BossWordWard = 0)
    {
        /// <summary>
        /// §12 連戦 (#191): the HP and the current stamina the player carries in from the battle
        /// before; null is full. HP above max is kept to max and stamina to 0..max. A carried HP of 0
        /// or less throws (#574): a fallen character never starts a battle, so it is a caller's bug,
        /// not a value to lift to 1. Nothing else carries: the deck is shuffled afresh (2026-09-23),
        /// statuses, Guard and the stance slot start empty.
        /// </summary>
        public int StartHp
        {
            get
            {
                if (!PlayerStartHp.HasValue) return PlayerMaxHp;
                if (PlayerStartHp.Value < 1)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(PlayerStartHp), PlayerStartHp.Value, "A carried HP is 1 or more: a fallen character does not start a battle.");
                }
                return Math.Min(PlayerMaxHp, PlayerStartHp.Value);
            }
        }

        /// <summary>
        /// The stamina the player opens with: the carried stamina kept to 0..max (full without one),
        /// then 息継ぎの笛's bonus on top (§9.1, #452). The bonus may take it past the maximum, never
        /// past <see cref="Constants.MaxStaminaCeil"/>. Recovery and gains add nothing to stamina at
        /// or above the maximum (<see cref="Combat.RecoverStamina"/>), so the extra is only spent.
        /// </summary>
        public int StartStamina
        {
            get
            {
                int carried = PlayerStartStamina.HasValue ? Math.Max(0, Math.Min(PlayerMaxStamina, PlayerStartStamina.Value)) : PlayerMaxStamina;
                int bonus = Math.Max(0, PlayerStartStaminaBonus);
                return bonus == 0 ? carried : Math.Min(Math.Max(carried, Constants.MaxStaminaCeil), carried + bonus);
            }
        }

        /// <summary>§9.1 (#452): what the tools hold for the battle (竜鱗の欠片 and 竜語の符); null without them.</summary>
        public CarriedTools? Tools =>
            FirstHitGuard > 0 || BossWordWard > 0
                ? new CarriedTools(ScaleGuard: Math.Max(0, FirstHitGuard), BossWordWard: Math.Max(0, BossWordWard))
                : null;

        /// <summary>The width the vertical slice is fought on until the dungeon hands one in (#168).</summary>
        public const int SliceFieldCells = 6;

        /// <summary>Every enemy of the battle, in phase order: <see cref="Enemy"/> first.</summary>
        public IReadOnlyList<EnemyDef> AllEnemies
        {
            get
            {
                var all = new List<EnemyDef> { Enemy };
                if (MoreEnemies != null) all.AddRange(MoreEnemies);
                return all;
            }
        }

        /// <summary>Where the first enemy's near edge stands on turn 1.</summary>
        public int EnemyStartCell => Field.EnemyStartCell(FieldCells, PlayerStartCell, 1, Enemy.Size, StartGap);

        /// <summary>
        /// §7.4: every enemy's near edge on turn 1. The first stands START_GAP from the player; each
        /// next one on the cell right behind the far edge of the one before.
        /// </summary>
        public IReadOnlyList<int> EnemyStartCells
        {
            get
            {
                var cells = new List<int>();
                var all = AllEnemies;
                for (int i = 0; i < all.Count; i++)
                {
                    cells.Add(i == 0 ? EnemyStartCell : cells[i - 1] + all[i - 1].Size);
                }
                return cells;
            }
        }

        /// <summary>The vertical slice: the polearm against the ten-kind prototype deck.</summary>
        public static BattleSetup Slice() => new BattleSetup(Enemies.PolearmWarped, PrototypeDeck.Build(), SliceFieldCells);
    }

    /// <summary>
    /// The settled numbers a held card would produce. See <see cref="TurnLoop.Preview"/>. InReach is
    /// false when the card aims at the opponent and N is outside its reach — the one reason a card
    /// with payable cost still cannot be released (§2.4). Cost is what playing it would take now,
    /// after a コスト −1 trait (#188) and a コストの割引 stance (§4), as <see cref="TurnLoop.CostNow"/>.
    ///
    /// RawPower and Damage are the whole card on the enemy it reads (#248): every blow of a face
    /// with hits, plus the wall damage of a push it could not take (§7.3). RawPower is before that
    /// enemy's Guard, Damage the HP it would lose after it. Out of reach they are one blow's, worked
    /// out without resolving (the screen shows no number on an enemy it cannot reach).
    /// </summary>
    public sealed record PlayPreview(bool TraitHolds, bool Attacks, int RawPower, int Damage, int GuardGain, bool InReach, int Cost);

    /// <summary>
    /// What an enemy's shown omen would do to the player if the turn ended now, as one number
    /// (#248). See <see cref="TurnLoop.PreviewOmen"/>. RawPower is every blow plus the wall damage
    /// before the player's Guard, Damage the HP the player would lose after the Guard they would
    /// hold by then (構え included). Lands is whether it reaches the player where they stand now;
    /// when it does not, the two numbers are what it would do if it did. Rests is true when the
    /// enemy cannot pay for it (§6) or falls to 出血 before it acts, and then both numbers are 0.
    /// </summary>
    public sealed record OmenPreview(int RawPower, int Damage, bool Lands, bool Rests);

    /// <summary>Why a card cannot be played right now. None when it can.</summary>
    public enum PlayRefusal
    {
        None,
        BattleOver,
        NotPlayerTurn,
        NotInHand,
        NotEnoughStamina,

        /// <summary>§2.4: the opponent stands outside the card's reach.</summary>
        OutOfReach,

        /// <summary>§7.4: the card aims at one enemy and the one named is not a standing enemy.</summary>
        NoSuchTarget,

        /// <summary>roster §4.1 呪縛 (#51): the player is bound and the card is one of movement only.</summary>
        Bound,
    }
}
