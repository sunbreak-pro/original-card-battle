// Plays the BattleDepiction scene from start to finish and holds every event to the
// 2.0 s budget (one action settles within 2.0 s). The views live in the predefined
// Assembly-CSharp, which an asmdef cannot reference, so the player is reached by name.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Depiction.PlayModeTests
{
    public class DepictionPlaybackTests
    {
        private const string ScenePath = "Assets/Scenes/BattleDepiction.unity";
        private const string PlayerTypeName = "Depiction.View.DepictionPlayer";
        private const float SecondsPerEventLimit = 2.0f;
        private const float PlaybackTimeoutSeconds = 40f;

        [UnityTest]
        public IEnumerator WholeSliceSettlesEveryEventWithinTwoSeconds()
        {
            // Fix the measuring conditions: each tween may run one frame long, so a slow frame rate
            // would eat the budget for reasons that have nothing to do with the choreography.
            int vSyncBefore = QualitySettings.vSyncCount;
            int frameRateBefore = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;

            // Neither flag is saved in the scene; switch both on before the player's Start runs.
            // scriptedPlayback is what this test measures: the filmed slice, in its written order.
            // The scene itself now opens on the live turn, where the player picks the cards.
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> onLoaded = (scene, mode) =>
            {
                MonoBehaviour loaded = FindPlayer();
                if (loaded == null) return;
                Set(loaded, "scriptedPlayback", true);
                Set(loaded, "autoPlayDrags", true);
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

            MonoBehaviour player = FindPlayer();
            Assert.That(player, Is.Not.Null, PlayerTypeName + " is missing from " + ScenePath);
            Assert.That(Get(player, "scriptedPlayback"), Is.EqualTo(true));
            Assert.That(Get(player, "autoPlayDrags"), Is.EqualTo(true));

            float deadline = Time.realtimeSinceStartup + PlaybackTimeoutSeconds;
            while (!(bool)Get(player, "Finished") && Time.realtimeSinceStartup < deadline) yield return null;
            QualitySettings.vSyncCount = vSyncBefore;
            Application.targetFrameRate = frameRateBefore;
            Assert.That(Get(player, "Finished"), Is.EqualTo(true), "playback did not finish in " + PlaybackTimeoutSeconds + " s");

            var seconds = (List<float>)Get(player, "EventSeconds");
            Assert.That(seconds.Count, Is.EqualTo(7));
            string all = string.Join(" / ", seconds.ConvertAll(s => s.ToString("0.00")));
            for (int i = 0; i < seconds.Count; i++)
            {
                Assert.That(seconds[i], Is.LessThan(SecondsPerEventLimit), "event " + (i + 1) + " ran over; seconds per event: " + all);
            }

            // The last settled frame, as printed on screen.
            Assert.That(Get(player, "playerStatus.hpText.text"), Is.EqualTo("47"));
            Assert.That(Get(player, "playerStatus.guardText.text"), Is.EqualTo("0"));
            Assert.That(Get(player, "omenBadge.kindText.text"), Is.EqualTo("防御"));
        }

        private static MonoBehaviour FindPlayer()
        {
            foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour.GetType().FullName == PlayerTypeName) return behaviour;
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
