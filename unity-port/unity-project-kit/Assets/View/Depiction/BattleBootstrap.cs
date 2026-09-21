// Starts the vertical-slice battle scene (Assets/Scenes/Battle.unity): on Play it hands the
// DepictionPlayer a battle run by the v4.2 core — the polearm warped soldier against the prototype
// deck — instead of the hand-written LiveTurn. It decides nothing itself: the Inspector fields are
// copied into a BattleLaunch (pure C#, tested under `dotnet test`), which builds the source.
//
// BattleDepiction.unity (the filming scene) has no bootstrap and keeps playing as before.
#if UNITY_2021_2_OR_NEWER
using Depiction.Bridge;
using UnityEngine;

namespace Depiction.View
{
    [DefaultExecutionOrder(-100)]
    public class BattleBootstrap : MonoBehaviour
    {
        [Header("Scene instances")]
        [Tooltip("The player that shows the battle. Left empty, the one in the scene is found.")]
        public DepictionPlayer player;

        [Header("Battle")]
        [Tooltip("Roster id of the enemy (enemy_roster_v4). The slice knows one: polearm_warped.")]
        public string enemyId = "polearm_warped";
        [Tooltip("Fixes every shuffle. The same seed deals the same hands here and under dotnet test.")]
        public int seed = BattleLaunch.DefaultSeed;
        [Tooltip("Draws a fresh seed on every Play and logs it, so a fight worth replaying can be typed back in.")]
        public bool randomSeed;
        [Tooltip("Where the player stands on turn 1.")]
        public RangeSide startSide = RangeSide.Near;

        [Header("Unattended runs (captures, PlayMode test)")]
        [Tooltip("The battle suggests the leftmost card it can pay for. Pair it with Auto Play Drags and Auto End Turn on the player.")]
        public bool autoPlay;
        [Tooltip("Stops once this many turns have closed on their next omen. 0 = fight to the end.")]
        public int stopAfterTurns;

        /// <summary>The seed the running battle was started with (the drawn one when Random Seed is on).</summary>
        public int SeedInUse { get; private set; }

        // Awake, not Start: every Awake in the scene runs before any Start, and the player reads its
        // source in Start.
        private void Awake()
        {
            if (!player) player = FindFirstObjectByType<DepictionPlayer>();
            if (!player)
            {
                Debug.LogError("[BattleBootstrap] no DepictionPlayer in the scene; the battle cannot start.");
                return;
            }

            SeedInUse = randomSeed ? Random.Range(1, int.MaxValue) : seed;
            var launch = new BattleLaunch
            {
                EnemyId = enemyId,
                Seed = SeedInUse,
                StartSide = startSide,
                AutoPlay = autoPlay,
                StopAfterTurns = Mathf.Max(0, stopAfterTurns),
            };

            try
            {
                player.UseSource(launch.CreateSource());
            }
            catch (System.ArgumentException e)
            {
                // A wrong id must not quietly fall back to the hand-written LiveTurn: stop the player instead.
                Debug.LogError("[BattleBootstrap] " + e.Message);
                player.enabled = false;
                return;
            }
            Debug.Log("[BattleBootstrap] " + enemyId + " / seed " + SeedInUse + " / start " + startSide);
        }
    }
}
#endif
