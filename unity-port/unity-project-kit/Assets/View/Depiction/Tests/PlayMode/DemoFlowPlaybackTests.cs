// Goes once around the demo (#187) in Assets/Scenes/Battle.unity with nobody at the mouse: the deck
// screen, opened on a saved deck that no longer reads (#211); the mode screen; one battle to its end
// screen; back to the enemies from there for another, given up at once (#211); back to the deck
// screen; then the chain up to its end screen (and its second battle, should the first be won). The
// buttons are pressed by name. A demo battle suggests no card, so the unattended switches only end
// each turn and the enemy wins; the flow is what is checked, not the fight. A screenshot of each
// screen goes to Logs/DemoShots for a look by eye (not in batchmode, which renders nothing). The saved
// deck the demo keeps in PlayerPrefs is put back afterwards. A second walk gives a chain up with
// 「降参する」 (#203) and starts it again. The views live in Assembly-CSharp, which an asmdef cannot
// reference, so everything is reached by name.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Depiction.PlayModeTests
{
    public class DemoFlowPlaybackTests
    {
        private const string ScenePath = "Assets/Scenes/Battle.unity";
        private const string SavedDeckKey = "Depiction.Demo.Deck";
        private const float BattleTimeoutSeconds = 300f;
        private static readonly string ShotFolder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/DemoShots"));

        [UnityTest]
        public IEnumerator TheDemo_GoesFromTheDeckScreen_ThroughABattle_AndTheChain()
        {
            if (!File.Exists(ScenePath)) Assert.Ignore(ScenePath + " does not exist yet (Tools > Depiction > Build Battle Scene).");

            bool hadDeck = PlayerPrefs.HasKey(SavedDeckKey);
            string savedDeck = PlayerPrefs.GetString(SavedDeckKey, "");
            Directory.CreateDirectory(ShotFolder);
            try
            {
                // #211: a card renamed since the deck was saved. The deck screen says so, and the string
                // stays saved until 「戦闘へ」 saves a deck over it.
                const string broken = "thrust:2,old_thrust:1";
                PlayerPrefs.SetString(SavedDeckKey, broken);
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;

                MonoBehaviour player = Find("Depiction.View.DepictionPlayer");
                Assert.That(player, Is.Not.Null, "no DepictionPlayer in " + ScenePath);

                // The deck screen first; the prototype deck is always legal.
                Assert.That(Active("ToBattle"), Is.Not.Null, "the deck screen did not open");
                Assert.That(DeckNotice(), Does.Contain("old_thrust"), "the deck screen did not say the saved deck did not read");
                Press("Prototype");
                Assert.That(PlayerPrefs.GetString(SavedDeckKey), Is.EqualTo(broken), "the deck was saved before 戦闘へ");
                Assert.That(BackdropAlpha("DeckSelect"), Is.EqualTo(1f), "the deck screen lets the battle show through");
                yield return Shot("01-deck");
                Press("ToBattle");
                yield return null;
                Assert.That(PlayerPrefs.GetString(SavedDeckKey), Does.StartWith("thrust:2,kesa_cut:2,"), "戦闘へ did not save the prototype deck");

                // The mode screen: one enemy, the first on the list.
                Assert.That(Active("Chain"), Is.Not.Null, "the mode screen did not open");
                Assert.That(BackdropAlpha("ModeSelect"), Is.EqualTo(1f), "the mode screen lets the battle show through");
                yield return Shot("02-mode");
                // The first turn waits for a hand at the mouse: see the hand dealt, then let the
                // unattended switches end the turns from there (AutoDrag is what the player starts when
                // it begins to wait, so it is started once by hand here).
                Press("Enemy0");
                yield return WaitFor(() => ((ICollection)Private(player, "_hand")).Count > 0 && !(bool)Private(player, "_busy"), 20f, "no hand was dealt");
                yield return Shot("03-battle");
                Assert.That(Active("Surrender"), Is.Not.Null, "no 降参 button during the battle");
                Set(player, "autoPlayDrags", true);
                Set(player, "autoEndTurn", true);
                MethodInfo autoDrag = player.GetType().GetMethod("AutoDrag", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(autoDrag, Is.Not.Null, "DepictionPlayer has no AutoDrag");
                player.StartCoroutine((IEnumerator)autoDrag.Invoke(player, null));

                yield return WaitFor(() => Active("Again") != null, BattleTimeoutSeconds, "the first battle did not reach its end screen");
                yield return Shot("04-end");
                Assert.That(Active("Surrender"), Is.Null, "the 降参 button stayed up on the end screen");

                // #211: a single battle's end screen goes back to the enemies with the same deck.
                Press("ChooseEnemy");
                yield return null;
                Assert.That(Active("Again"), Is.Null, "the end screen stayed up after 敵を選び直す");
                Assert.That(Active("Chain"), Is.Not.Null, "敵を選び直す did not show the mode screen");
                Assert.That(Active("Surrender"), Is.Null, "the 降参 button is up on the mode screen");

                // Another enemy, given up at once: its end screen offers the enemies again too.
                Press("Enemy1");
                yield return null;
                Assert.That(Active("Chain"), Is.Null, "the mode screen stayed up over the battle");
                Assert.That(Active("Surrender"), Is.Not.Null, "no 降参 button in the battle after 敵を選び直す");
                Press("Surrender");
                yield return null; // Show cleared the last end screen's buttons, which live until the frame ends
                Assert.That(Active("ChooseEnemy"), Is.Not.Null, "a single battle given up does not offer the enemies again");

                // Back to the deck screen, then the chain.
                Press("BackToDeck");
                yield return null;
                Assert.That(Active("ToBattle"), Is.Not.Null, "BackToDeck did not show the deck screen");
                Assert.That(DeckNotice(), Is.Empty, "the line about the saved deck stayed after 戦闘へ");
                Press("ToBattle");
                yield return null;
                Press("Chain");
                yield return WaitFor(() => Active("Again") != null, BattleTimeoutSeconds, "the chain's first battle did not reach its end screen");
                yield return Shot("05-chain-end");
                yield return null; // the single battle's buttons, cleared by Show, live until the frame ends
                Assert.That(Active("ChooseEnemy"), Is.Null, "a chain's end screen offers to choose one enemy");

                // A win offers the rest; take it and see the second battle start. A loss (what the
                // unattended run gets) ends the chain.
                if (Active("Rest") != null)
                {
                    Press("Rest");
                    yield return null;
                    Assert.That(Active("Again"), Is.Null, "the end screen stayed up after resting");
                    Assert.That(Get(player, "Finished"), Is.EqualTo(false), "the second battle of the chain did not start");
                    yield return new WaitForSecondsRealtime(3f);
                    yield return Shot("06-chain-second");
                }
                else
                {
                    Assert.That(Active("GoOn"), Is.Null, "a lost chain still offers to go on");
                }
            }
            finally
            {
                if (hadDeck) PlayerPrefs.SetString(SavedDeckKey, savedDeck);
                else PlayerPrefs.DeleteKey(SavedDeckKey);
                PlayerPrefs.Save();
            }
        }

        [UnityTest]
        public IEnumerator Surrendering_EndsTheChainAsALoss_AndTheNextBattleStartsClean()
        {
            // #203: 「降参する」 on the chain's first turn goes straight to its end screen, lost, with no
            // way on; 「最初からもう一度」 deals a fresh battle; the deck screen hides the button.
            if (!File.Exists(ScenePath)) Assert.Ignore(ScenePath + " does not exist yet (Tools > Depiction > Build Battle Scene).");

            bool hadDeck = PlayerPrefs.HasKey(SavedDeckKey);
            string savedDeck = PlayerPrefs.GetString(SavedDeckKey, "");
            Directory.CreateDirectory(ShotFolder);
            try
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;

                MonoBehaviour player = Find("Depiction.View.DepictionPlayer");
                Assert.That(player, Is.Not.Null, "no DepictionPlayer in " + ScenePath);
                Assert.That(Active("Surrender"), Is.Null, "the 降参 button is up on the deck screen");
                Press("Prototype");
                Press("ToBattle");
                yield return null;
                Assert.That(Active("Surrender"), Is.Null, "the 降参 button is up on the mode screen");

                Press("Chain");
                yield return WaitFor(() => ((ICollection)Private(player, "_hand")).Count > 0 && !(bool)Private(player, "_busy"), 20f, "no hand was dealt");
                Press("Surrender");
                yield return null;
                Assert.That(Active("Again"), Is.Not.Null, "降参 did not bring up the end screen");
                Assert.That(Active("BackToDeck"), Is.Not.Null, "the end screen offers no way back to the deck");
                Assert.That(Active("Rest"), Is.Null, "a surrendered chain still offers the rest");
                Assert.That(Active("GoOn"), Is.Null, "a surrendered chain still offers to go on");
                Assert.That(Active("Surrender"), Is.Null, "the 降参 button stayed up on the end screen");
                Assert.That(EndTitle(), Does.Contain("降参"), "the end screen does not say the battle was given up");
                yield return new WaitForSecondsRealtime(1f);
                Assert.That(Get(player, "Finished"), Is.EqualTo(false), "the given-up battle went on to its end");
                yield return Shot("07-surrender-end");

                Press("Again");
                yield return WaitFor(() => ((ICollection)Private(player, "_hand")).Count > 0 && !(bool)Private(player, "_busy"), 20f, "no hand was dealt after 最初からもう一度");
                Assert.That(Active("Surrender"), Is.Not.Null, "no 降参 button in the battle after 最初からもう一度");
                Assert.That(Active("Again"), Is.Null, "the end screen stayed up");

                Press("Surrender");
                yield return null;
                Press("BackToDeck");
                yield return null;
                Assert.That(Active("ToBattle"), Is.Not.Null, "BackToDeck did not show the deck screen");
                Assert.That(Active("Surrender"), Is.Null, "the 降参 button is up on the deck screen");
            }
            finally
            {
                if (hadDeck) PlayerPrefs.SetString(SavedDeckKey, savedDeck);
                else PlayerPrefs.DeleteKey(SavedDeckKey);
                PlayerPrefs.Save();
            }
        }

        /// <summary>The end screen's title line, read by name like the buttons.</summary>
        private static string EndTitle()
        {
            foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>())
            {
                if (t.name != "Title" || !t.gameObject.activeInHierarchy || t.parent == null || t.parent.name != "Panel") continue;
                Transform line = t.Find("Text");
                Component text = line != null ? line.GetComponent("Text") : null;
                if (text != null) return (string)text.GetType().GetProperty("text").GetValue(text);
            }
            return "";
        }

        /// <summary>The deck screen's line about the saved deck (#211), read by name like the buttons; null when the screen is not up.</summary>
        private static string DeckNotice()
        {
            foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>())
            {
                if (t.name != "Notice" || !t.gameObject.activeInHierarchy || t.parent == null || t.parent.name != "DeckSelect") continue;
                Transform line = t.Find("Text");
                Component text = line != null ? line.GetComponent("Text") : null;
                if (text != null) return (string)text.GetType().GetProperty("text").GetValue(text);
            }
            return null;
        }

        private static IEnumerator WaitFor(Func<bool> done, float seconds, string failure)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(done(), Is.True, failure + " in " + seconds + " s");
        }

        private static IEnumerator Shot(string name)
        {
            // Batchmode renders nothing, so the end of the frame never comes: no screenshots there.
            if (Application.isBatchMode) yield break;
            yield return new WaitForEndOfFrame();
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(ShotFolder, name + ".png"), shot.EncodeToPNG());
            UnityEngine.Object.Destroy(shot);
        }

        /// <summary>The active button of that name on the demo's screens, or null (other parts share names).</summary>
        private static GameObject Active(string name)
        {
            foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>())
            {
                if (t.name == name && t.gameObject.activeInHierarchy && t.GetComponent("Button") != null) return t.gameObject;
            }
            return null;
        }

        /// <summary>The alpha of a demo screen's backdrop: the child "Back" of that screen that is not a button.</summary>
        private static float BackdropAlpha(string screen)
        {
            foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>())
            {
                if (t.name != "Back" || t.parent == null || t.parent.name != screen || t.GetComponent("Button") != null) continue;
                Component image = t.GetComponent("Image");
                Assert.That(image, Is.Not.Null, screen + "/Back has no Image");
                return ((Color)image.GetType().GetProperty("color").GetValue(image)).a;
            }
            Assert.Fail("no backdrop under " + screen);
            return 0f;
        }

        private static void Press(string name)
        {
            GameObject go = Active(name);
            Assert.That(go, Is.Not.Null, "no active button '" + name + "'");
            Component button = go.GetComponent("Button");
            Assert.That(button, Is.Not.Null, "'" + name + "' has no Button");
            object onClick = button.GetType().GetProperty("onClick").GetValue(button);
            onClick.GetType().GetMethod("Invoke", Type.EmptyTypes).Invoke(onClick, null);
        }

        private static MonoBehaviour Find(string typeName)
        {
            foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>())
            {
                if (behaviour.GetType().FullName == typeName) return behaviour;
            }
            return null;
        }

        private static object Get(object target, string name)
        {
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, target.GetType().Name + " has no public property '" + name + "'");
            return property.GetValue(target);
        }

        private static object Private(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, target.GetType().Name + " has no field '" + fieldName + "'");
            return field.GetValue(target);
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
