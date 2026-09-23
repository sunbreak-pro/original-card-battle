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

        /// <summary>The roster id of the enemy to fight. The slice knows one: polearm_warped.</summary>
        public string EnemyId = Enemies.PolearmWarpedId;

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

        /// <summary>Unattended runs only: the source plays the leftmost payable card by itself.</summary>
        public bool AutoPlay;

        /// <summary>Unattended runs only: stop once this many turns have closed. 0 = fight to the end.</summary>
        public int StopAfterTurns;

        /// <summary>
        /// The deck to fight with: the one the deck screen built (#190), or null for the slice's
        /// ten-kind prototype deck × 2 (#71). A deck that breaks §8 is refused when the battle is built.
        /// </summary>
        public List<CardInstance> Deck;

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
            EnemyDef enemy;
            try
            {
                enemy = Enemies.ById(EnemyId ?? "");
            }
            catch (KeyNotFoundException)
            {
                var known = new List<string>();
                foreach (EnemyDef def in Enemies.All) known.Add(def.Id);
                throw new ArgumentException(
                    "BattleLaunch: no enemy has the id \"" + EnemyId + "\". Known ids: " + string.Join(", ", known) + ".");
            }

            if (StartGap < 0) throw new ArgumentOutOfRangeException(nameof(StartGap), StartGap, "0 or more.");
            if (FieldCells < Constants.FieldCellsMin || FieldCells > Constants.FieldCellsMax)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(FieldCells), FieldCells, Constants.FieldCellsMin + ".." + Constants.FieldCellsMax + " cells.");
            }
            return new BattleSetup(enemy, BuildDeck(), FieldCells, StartGap);
        }

        public CoreBattleSource CreateSource()
        {
            return new CoreBattleSource(BuildSetup(), Seed, AutoPlay, StopAfterTurns);
        }
    }
}
