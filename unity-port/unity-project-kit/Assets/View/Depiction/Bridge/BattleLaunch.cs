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

        /// <summary>The roster id of the enemy to fight. The slice knows one: polearm_warped.</summary>
        public string EnemyId = Enemies.PolearmWarpedId;

        /// <summary>Fixes every shuffle. The same seed deals the same hands here and under `dotnet test`.</summary>
        public int Seed = DefaultSeed;

        /// <summary>The gap on turn 1 (battle_core_v4 §7.3 `START_GAP`): the player stands on cell 2 and the enemy 1 + this further right.</summary>
        public int StartGap = Constants.StartGap;

        /// <summary>Unattended runs only: the source plays the leftmost payable card by itself.</summary>
        public bool AutoPlay;

        /// <summary>Unattended runs only: stop once this many turns have closed. 0 = fight to the end.</summary>
        public int StopAfterTurns;

        /// <summary>The ten-kind prototype deck × 2 (#71). The deck builder of 出立 (#58) replaces this.</summary>
        public List<CardInstance> BuildDeck()
        {
            return PrototypeDeck.Build();
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
            return new BattleSetup(
                enemy, BuildDeck(),
                EnemyStartCell: Constants.PlayerStartCell + 1 + StartGap);
        }

        public CoreBattleSource CreateSource()
        {
            return new CoreBattleSource(BuildSetup(), Seed, AutoPlay, StopAfterTurns);
        }
    }
}
