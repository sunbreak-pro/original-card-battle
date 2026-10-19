// Starts the vertical-slice battle scene (Assets/Scenes/Battle.unity): on Play it hands the
// DepictionPlayer a battle run by the v4.3 core — the polearm warped soldier against the prototype
// deck. It decides nothing itself: the Inspector fields are copied into a BattleLaunch (pure C#,
// tested under `dotnet test`), which builds the source.
//
// BattleDepiction.unity (the filming scene) has no bootstrap: it replays the fixed script
// (DepictionPlayer.scriptedPlayback, #571).
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
        [Tooltip("The cells on the line (battle_core_v4 §7.1: 5〜8, handed in per battle). The slice fights on 6.")]
        public int fieldCells = BattleLaunch.SliceFieldCells;
        [Tooltip("The gap N on turn 1 (battle_core_v4 §7.3: 3). The player stands on cell 2; a line too short puts the enemy at its end.")]
        public int startGap = BattleLaunch.DefaultStartGap;
        [Tooltip("The journal's 開示度 for the enemy (battle_ui_ux_v2 §7: 0〜2, −1 reads as 0), handed to the omen once as the battle starts. Below 2 the omen shows no numbers. Stands in for the journal until its data reaches the battle (#107); the demo flow reads it for every enemy.")]
        [Range(BattleLaunch.LowestDisclosure, 2)]
        public int disclosure = BattleLaunch.DefaultDisclosure;

        [Header("Demo (#187)")]
        [Tooltip("Opens the deck screen (#190), then the departure screen (#58: tools, consumables, talent), the mode screen (#191: one enemy or the chain) and the end screen. Off: the Inspector's battle starts at once with the prototype deck, as the slice did.")]
        public bool demoFlow = true;

        [Header("Unattended runs (captures, PlayMode test)")]
        [Tooltip("The battle suggests the leftmost card it can pay for. Pair it with Auto Play Drags and Auto End Turn on the player.")]
        public bool autoPlay;
        [Tooltip("Stops once this many turns have closed on their next omen. 0 = fight to the end.")]
        public int stopAfterTurns;

        /// <summary>The seed the running battle was started with (the drawn one when Random Seed is on).</summary>
        public int SeedInUse { get; private set; }

        // #99: a battle handed in from the exploration scene, which loads this scene on top of itself.
        // Static because this scene's Awake runs while it loads, before the loader can reach it.
        private static CoreBattleSource s_handedIn;

        /// <summary>
        /// #99: the exploration hands its battle in here just before it loads this scene. The next
        /// Awake plays it in place of the Inspector's battle and the demo flow, once.
        /// </summary>
        public static void HandIn(CoreBattleSource source) => s_handedIn = source;

        // Awake, not Start: every Awake in the scene runs before any Start, and the player reads its
        // source in Start.
        private void Awake()
        {
            // The scene holds one DepictionPlayer, so FindAny (no order guarantee) still finds the one (#235).
            if (!player) player = FindAnyObjectByType<DepictionPlayer>();
            if (!player)
            {
                Debug.LogError("[BattleBootstrap] no DepictionPlayer in the scene; the battle cannot start.");
                return;
            }

            if (s_handedIn != null)
            {
                CoreBattleSource handed = s_handedIn;
                s_handedIn = null;
                DemoFlow screens = GetComponent<DemoFlow>();
                if (screens) Destroy(screens);
                player.UseSource(handed);
                Debug.Log("[BattleBootstrap] handed in by the exploration: " + handed.State.EnemyDef.Id + " / " + handed.State.FieldCells + " cells");
                return;
            }

            SeedInUse = randomSeed ? Random.Range(1, int.MaxValue) : seed;

            // The demo flow (#190 / #191): the player waits; the deck screen, then the mode screen
            // (one enemy or the chain), pick the battles, fought from this seed. An unattended run
            // (autoPlay) keeps starting the Inspector's battle at once, as the PlayMode test expects.
            DemoFlow flow = GetComponent<DemoFlow>();
            if (demoFlow && !autoPlay)
            {
                player.Hold();
                if (!flow) flow = gameObject.AddComponent<DemoFlow>();
                flow.Begin(player, SeedInUse, _ => disclosure);
                Debug.Log("[BattleBootstrap] demo flow / seed " + SeedInUse + " / disclosure " + disclosure);
                return;
            }
            // Awake run again with the flow switched off (the PlayMode test does): its screens go.
            if (flow) Destroy(flow);

            var launch = new BattleLaunch
            {
                EnemyId = enemyId,
                Seed = SeedInUse,
                FieldCells = fieldCells,
                StartGap = Mathf.Max(0, startGap),
                Disclosure = disclosure,
                AutoPlay = autoPlay,
                StopAfterTurns = Mathf.Max(0, stopAfterTurns),
            };

            try
            {
                player.UseSource(launch.CreateSource());
            }
            catch (System.ArgumentException e)
            {
                // A wrong id must not quietly start some other battle: stop the player instead.
                Debug.LogError("[BattleBootstrap] " + e.Message);
                player.enabled = false;
                return;
            }
            Debug.Log("[BattleBootstrap] " + enemyId + " / seed " + SeedInUse + " / " + fieldCells + " cells / start gap " + startGap
                + " / disclosure " + disclosure);
        }
    }
}
#endif
