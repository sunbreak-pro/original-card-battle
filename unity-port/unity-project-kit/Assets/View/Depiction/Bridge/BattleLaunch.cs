// What the battle scene is started with, and the source it turns into. Pure C#: the MonoBehaviour
// that owns the Inspector fields (BattleBootstrap) only copies them here, so everything that can go
// wrong with them — an enemy id nobody defined, a negative turn limit — is caught under `dotnet test`.
using System;
using System.Collections.Generic;
using BattleCore;

namespace Depiction.Bridge
{
    public sealed class BattleLaunch
    {
        /// <summary>The slice's seed: the battle pinned in BattleCore.Tests (TurnLoopTests) uses the same one.</summary>
        public const int DefaultSeed = 20260921;

        /// <summary>The slice's line until the dungeon hands one in (#168). The bootstrap's Inspector reads it here.</summary>
        public const int SliceFieldCells = BattleSetup.SliceFieldCells;

        /// <summary>battle_core_v4 §7.3 `START_GAP` (3 since 2026-09-23, #169).</summary>
        public const int DefaultStartGap = Constants.StartGap;

        /// <summary>The slice's enemy, 錆槍の竜兵. The bootstrap's Inspector reads it here, so the View spells no id itself (#576).</summary>
        public const string DefaultEnemyId = Enemies.PolearmWarpedId;

        /// <summary>The roster id of the enemy to fight: any of BattleCore's Enemies.All (an unknown one is refused when the battle is built).</summary>
        public string EnemyId = DefaultEnemyId;

        /// <summary>Fixes every shuffle. The same seed deals the same hands here and under `dotnet test`.</summary>
        public int Seed = DefaultSeed;

        /// <summary>
        /// The cells on the line (battle_core_v4 §7.1, 5〜8). Handed in per battle since 2026-09-23
        /// (#169); the dungeon side picks it per layer (#168), the slice fights on 6 until then.
        /// </summary>
        public int FieldCells = SliceFieldCells;

        /// <summary>
        /// The gap on turn 1 (battle_core_v4 §7.3 `START_GAP`, 3): the player stands on cell 2 and the
        /// enemy this many empty cells further right — or at the right end of a line too short for it.
        /// </summary>
        public int StartGap = DefaultStartGap;

        /// <summary>
        /// The journal's 開示度 for the enemy (battle_ui_ux_v2 §7, #500), handed to the omen once as the
        /// battle starts: it does not move during a battle. Below 2 the omen shows no numbers; −1 (the
        /// 100% run's enemies) has no parts yet and reads as 0. 2 until the journal's data (#107) reaches it.
        /// </summary>
        public int Disclosure = DefaultDisclosure;

        /// <summary>battle_ui_ux_v2 §7: 開示度 2, everything the omen has.</summary>
        public const int DefaultDisclosure = OmenDisclosure.Full;

        /// <summary>battle_ui_ux_v2 §7: −1, the enemies of the 100% run, hidden whole.</summary>
        public const int LowestDisclosure = -1;

        /// <summary>Unattended runs only: the source plays the leftmost payable card by itself.</summary>
        public bool AutoPlay;

        /// <summary>Unattended runs only: stop once this many turns have closed. 0 = fight to the end.</summary>
        public int StopAfterTurns;

        /// <summary>
        /// The deck to fight with: the one the deck screen built (#190), or null for the slice's
        /// ten-kind prototype deck × 2 (#71). A deck that breaks §8 is refused when the battle is built.
        /// </summary>
        public List<CardInstance> Deck;

        // ---- What the exploration hands in (#99). The defaults are a fresh battle, as before. ----

        /// <summary>
        /// The enemies standing behind <see cref="EnemyId"/> on the same line, in phase order (roster
        /// §9's pair: 錆槍 in front, 灰弩 behind). Null or empty for one enemy.
        /// </summary>
        public List<string> MoreEnemyIds;

        /// <summary>The player's HP maximum (PLAYER_MAX_HP until the exploration carries another).</summary>
        public int PlayerMaxHp = Constants.PlayerMaxHp;

        /// <summary>
        /// The max stamina the battle starts from: the exploration's temporary changes and 瘴気 already
        /// counted (battle_core_v4 §1, concept-v3 §6). The battle's own 瘴気纏い goes on top inside it.
        /// </summary>
        public int MaxStamina = Constants.BaseMaxStamina;

        /// <summary>The HP the player walks in with; null is full.</summary>
        public int? StartHp;

        /// <summary>The current stamina the player walks in with; null is full.</summary>
        public int? StartStamina;

        /// <summary>The player's cell on turn 1 (§7.3: 2; 間合いの履 moves it to 1, widening <see cref="StartGap"/> by the same).</summary>
        public int PlayerStartCell = Constants.PlayerStartCell;

        /// <summary>§9.1 (#452): the in-battle tools, as amounts — 息継ぎの笛, 竜鱗の欠片 and 竜語の符. 0 is no tool.</summary>
        public int StartStaminaBonus;
        public int FirstHitGuard;
        public int BossWordWard;

        /// <summary>Which battle of a back-to-back encounter this is, for the header (1 of 1 for a lone battle).</summary>
        public int ChainIndex = 1;
        public int ChainTotal = 1;

        public List<CardInstance> BuildDeck()
        {
            if (Deck == null) return PrototypeDeck.Build();
            DeckValidation validation = Cards.Validate(Deck);
            if (!validation.Ok)
            {
                throw new ArgumentException("BattleLaunch: the deck breaks §8 — " + string.Join(" / ", validation.Errors));
            }
            return new List<CardInstance>(Deck);
        }

        public BattleSetup BuildSetup()
        {
            EnemyDef enemy = EnemyById(EnemyId, nameof(BattleLaunch));
            List<EnemyDef> more = null;
            if (MoreEnemyIds != null && MoreEnemyIds.Count > 0)
            {
                more = new List<EnemyDef>();
                foreach (string id in MoreEnemyIds) more.Add(EnemyById(id, nameof(BattleLaunch)));
            }

            if (StartGap < 0) throw new ArgumentOutOfRangeException(nameof(StartGap), StartGap, "0 or more.");
            if (FieldCells < Constants.FieldCellsMin || FieldCells > Constants.FieldCellsMax)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(FieldCells), FieldCells, Constants.FieldCellsMin + ".." + Constants.FieldCellsMax + " cells.");
            }
            if (PlayerMaxHp < 1) throw new ArgumentOutOfRangeException(nameof(PlayerMaxHp), PlayerMaxHp, "1 or more.");
            if (MaxStamina < Constants.MaxStaminaFloor || MaxStamina > Constants.MaxStaminaCeil)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MaxStamina), MaxStamina, Constants.MaxStaminaFloor + ".." + Constants.MaxStaminaCeil + " (battle_core_v4 §1).");
            }
            if (PlayerStartCell < 1) throw new ArgumentOutOfRangeException(nameof(PlayerStartCell), PlayerStartCell, "cell 1 or further right.");
            return new BattleSetup(enemy, BuildDeck(), FieldCells, StartGap, PlayerStartCell, PlayerMaxHp, MaxStamina, more,
                StartHp, StartStamina, StartStaminaBonus, FirstHitGuard, BossWordWard);
        }

        /// <summary>
        /// <see cref="Enemies.ById"/>, with an unknown id as an <see cref="ArgumentException"/> that lists
        /// the known ids, the type the callers (BattleBootstrap, DemoFlow) catch. <paramref name="caller"/>
        /// heads the message.
        /// </summary>
        internal static EnemyDef EnemyById(string id, string caller)
        {
            try
            {
                return Enemies.ById(id ?? "");
            }
            catch (KeyNotFoundException)
            {
                var known = new List<string>();
                foreach (EnemyDef def in Enemies.All) known.Add(def.Id);
                throw new ArgumentException(
                    caller + ": no enemy has the id \"" + id + "\". Known ids: " + string.Join(", ", known) + ".");
            }
        }

        public CoreBattleSource CreateSource()
        {
            if (Disclosure < LowestDisclosure || Disclosure > OmenDisclosure.Full)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(Disclosure), Disclosure, LowestDisclosure + ".." + OmenDisclosure.Full + " (battle_ui_ux_v2 §7).");
            }
            return new CoreBattleSource(BuildSetup(), Seed, AutoPlay, StopAfterTurns, ChainIndex, ChainTotal, OmenDisclosure.Clamp(Disclosure));
        }
    }
}
