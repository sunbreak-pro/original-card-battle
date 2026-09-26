// Tools > Depiction > Build Battle Scene / Build Battle Demo (Windows)
// Makes the demo (#187) something to open and play without hand steps: the battle scene is a copy of
// the filming scene with a BattleBootstrap on it (demo flow on), listed first in the build settings,
// and the Windows build is one menu click (or one batchmode call, -executeMethod ...BuildWindows).
// An existing Assets/Scenes/Battle.unity is left alone, like the prefab builder does.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Depiction.View
{
    public static class BattleDemoBuilder
    {
        public const string BattleScenePath = "Assets/Scenes/Battle.unity";
        public const string WindowsBuildPath = "Builds/BattleDemo/BattleDemo.exe";

        [MenuItem("Tools/Depiction/Build Battle Scene")]
        public static void BuildBattleScene()
        {
            if (!File.Exists(BattleScenePath))
            {
                if (!File.Exists(DepictionPrefabBuilder.ScenePath)) DepictionPrefabBuilder.BuildAll();
                if (!AssetDatabase.CopyAsset(DepictionPrefabBuilder.ScenePath, BattleScenePath))
                    throw new IOException("could not copy " + DepictionPrefabBuilder.ScenePath + " to " + BattleScenePath);

                var scene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
                DepictionPlayer player = Object.FindFirstObjectByType<DepictionPlayer>();
                if (!player) throw new MissingReferenceException("no DepictionPlayer in " + BattleScenePath);
                // The battle is played by hand: none of the filming / unattended switches.
                player.scriptedPlayback = false;
                player.autoPlayDrags = false;
                player.autoEndTurn = false;
                EditorUtility.SetDirty(player);

                var go = new GameObject("BattleBootstrap");
                var bootstrap = go.AddComponent<BattleBootstrap>();
                bootstrap.player = player;
                bootstrap.demoFlow = true;
                bootstrap.randomSeed = true;
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[BattleDemo] created " + BattleScenePath);
            }
            PutFirstInBuildSettings(BattleScenePath);
        }

        [MenuItem("Tools/Depiction/Build Battle Demo (Windows)")]
        public static void BuildWindows()
        {
            BuildBattleScene();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { BattleScenePath },
                locationPathName = WindowsBuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[BattleDemo] build " + report.summary.result + " -> " + Path.GetFullPath(WindowsBuildPath));
            // A failed build must fail a batchmode run too.
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        private static void PutFirstInBuildSettings(string path)
        {
            var rest = EditorBuildSettings.scenes.Where(s => s.path != path);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) }.Concat(rest).ToArray();
        }
    }
}
