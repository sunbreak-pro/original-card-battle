// Plays one turn of the vertical-slice battle scene (Assets/Scenes/Battle.unity) with nobody at the
// mouse, and holds every event to the 2.0 s budget. The battle is the v4.2 core's: BattleBootstrap
// hands the player a CoreBattleSource. The views live in the predefined Assembly-CSharp, which an
// asmdef cannot reference, so both components are reached by name.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Depiction.PlayModeTests
{
    public class BattlePlaybackTests
    {
        private const string ScenePath = "Assets/Scenes/Battle.unity";
        private const string PlayerTypeName = "Depiction.View.DepictionPlayer";
        private const string BootstrapTypeName = "Depiction.View.BattleBootstrap";
        private const float SecondsPerEventLimit = 2.0f;
        private const float PlaybackTimeoutSeconds = 60f;

        [UnityTest]
        public IEnumerator OneTurnAgainstThePolearm_PlaysThrough_AndEveryEventSettlesWithinTwoSeconds()
        {
            // The scene belongs to the Unity project, not to the kit this file is synced from.
            if (!File.Exists(ScenePath)) Assert.Ignore(ScenePath + " does not exist yet (issue #74, manual Unity steps).");

            int vSyncBefore = QualitySettings.vSyncCount;
            int frameRateBefore = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;

            // None of these flags is saved in the scene. sceneLoaded runs after the scene's Awake calls
            // and before any Start, so the bootstrap has already built a source from the scene's own
            // values: set the flags, then run its Awake again so the player's Start reads the new one.
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> onLoaded = (scene, mode) =>
            {
                MonoBehaviour bootstrap = Find(BootstrapTypeName);
                MonoBehaviour loaded = Find(PlayerTypeName);
                if (bootstrap == null || loaded == null) return;
                Set(bootstrap, "randomSeed", false);
                Set(bootstrap, "autoPlay", true);
                Set(bootstrap, "stopAfterTurns", 1);
                Set(loaded, "autoPlayDrags", true);
                Set(loaded, "autoEndTurn", true);
                MethodInfo awake = bootstrap.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(awake, Is.Not.Null, BootstrapTypeName + " has no Awake");
                awake.Invoke(bootstrap, null);
            };
            SceneManager.sceneLoaded += onLoaded;
            try
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            }
            finally
            {
                SceneManager.sceneLoaded -= onLoaded;
            }

            MonoBehaviour player = Find(PlayerTypeName);
            Assert.That(player, Is.Not.Null, PlayerTypeName + " is missing from " + ScenePath);
            Assert.That(Find(BootstrapTypeName), Is.Not.Null, BootstrapTypeName + " is missing from " + ScenePath);

            float deadline = Time.realtimeSinceStartup + PlaybackTimeoutSeconds;
            while (!(bool)Get(player, "Finished") && Time.realtimeSinceStartup < deadline) yield return null;
            QualitySettings.vSyncCount = vSyncBefore;
            Application.targetFrameRate = frameRateBefore;
            Assert.That(Get(player, "Finished"), Is.EqualTo(true), "one turn did not finish in " + PlaybackTimeoutSeconds + " s");

            // Seed 20260921, turn 1 (as BattleLaunchTests under dotnet test): turn start, one card (the pilot keeps
            // its distance from gap 2 on, #192), turn end, the enemy's action, the next omen.
            var seconds = (List<float>)Get(player, "EventSeconds");
            Assert.That(seconds.Count, Is.EqualTo(5));
            string all = string.Join(" / ", seconds.ConvertAll(s => s.ToString("0.00")));
            for (int i = 0; i < seconds.Count; i++)
            {
                Assert.That(seconds[i], Is.LessThan(SecondsPerEventLimit), "event " + (i + 1) + " ran over; seconds per event: " + all);
            }

            // The last settled frame, as printed on screen: untouched behind Guard, the enemy untouched
            // at 60, and the next attack announced.
            Assert.That(Get(player, "playerStatus.hpText.text"), Is.EqualTo("50"));
            Assert.That(Get(player, "enemyStatus.hpText.text"), Is.EqualTo("60"));
            Assert.That(Get(player, "omenBadge.kindText.text"), Is.EqualTo("攻撃"));
        }

        private static MonoBehaviour Find(string typeName)
        {
            foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour.GetType().FullName == typeName) return behaviour;
            }
            return null;
        }

        /// <summary>Reads a dotted path of public fields / properties ("playerStatus.hpText.text").</summary>
        private static object Get(object root, string path)
        {
            object current = root;
            foreach (string name in path.Split('.'))
            {
                Assert.That(current, Is.Not.Null, "null before '" + name + "' in " + path);
                Type type = current.GetType();
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public);
                if (field != null)
                {
                    current = field.GetValue(current);
                    continue;
                }
                PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                Assert.That(property, Is.Not.Null, type.Name + " has no public member '" + name + "'");
                current = property.GetValue(current);
            }
            return current;
        }

        private static void Set(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, target.GetType().Name + " has no public field '" + fieldName + "'");
            field.SetValue(target, value);
        }
    }
}
#endif
