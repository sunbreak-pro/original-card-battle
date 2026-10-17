// Tools > Depiction > Capture Effect Grammar Samples (#57)
// One picture per system of the effect grammar (battle_ui_ux_v2 §5.3: 斬 突 払 打 盾 型 気 息) and one of
// the trait cue over a blow (§5.9 J2, on 袈裟斬り), taken from a real play: the filming scene is opened,
// Play is entered, and each sample (Bridge GrammarSamples: which card, from which gap, which effect) is
// handed to the DepictionPlayer as a core battle whose auto drag plays the card. In the middle of the
// sample's effect every tween is frozen, the Game view is saved as PNG and the tweens run on. Play is
// left again at the end. Nothing in the scene is saved: the switches are set on the running copy only.
//
// The PNGs land in <project>/Captures/EffectGrammar/ (outside Assets, so nothing is imported), named
// 01-slash.png … 08-breath.png and 09-slash-trait-accent.png, with index.txt saying which card each is.
using System.Collections.Generic;
using System.IO;
using System.Text;
using Depiction.Bridge;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Depiction.View
{
    [InitializeOnLoad]
    public static class GrammarSampleCapture
    {
        /// <summary>Where the pictures go, relative to the project folder.</summary>
        public const string OutputFolder = "Captures/EffectGrammar";

        /// <summary>The picture is taken this far into the watched effect (0.5 = its middle).</summary>
        private const float CaptureAt = 0.5f;

        /// <summary>A sample whose effect has not started by then is given up and logged.</summary>
        private const double SampleTimeoutSeconds = 12.0;

        /// <summary>How long a saved file is waited for before the run moves on.</summary>
        private const double SaveTimeoutSeconds = 5.0;

        // The menu asks for the run, then Play reloads the domain: the request survives in SessionState.
        private const string PendingKey = "Depiction.GrammarSampleCapture.Pending";

        private enum Step { WaitForPlayer, WaitForEffect, WaitForMoment, WaitForFile, Done }

        private static Step _step;
        private static DepictionPlayer _player;
        private static int _index;
        private static double _sampleStartedAt;
        private static double _momentAt;
        private static double _savedAt;
        private static string _path;
        private static float _speedBeforeFreeze;
        private static readonly List<string> Lines = new List<string>();
        private static bool _running;

        static GrammarSampleCapture()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Tools/Depiction/Capture Effect Grammar Samples")]
        public static void CaptureAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[GrammarSamples] stop Play first; the capture enters Play by itself.");
                return;
            }
            if (!OpenFilmingScene()) return;
            SessionState.SetBool(PendingKey, true);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Tools/Depiction/Capture Effect Grammar Samples", true)]
        private static bool CanCapture()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        /// <summary>
        /// The capture runs in BattleDepiction.unity: it has no BattleBootstrap, so no deck screen covers the
        /// battle. From Battle.unity (or any other scene) it switches over, asking to save changes first.
        /// </summary>
        private static bool OpenFilmingScene()
        {
            string scene = DepictionPrefabBuilder.ScenePath;
            if (EditorSceneManager.GetActiveScene().path == scene) return true;
            if (!File.Exists(scene))
            {
                Debug.LogWarning("[GrammarSamples] " + scene + " is missing; run Tools/Depiction/Build Prefabs And Scene first.");
                return false;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            return true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingKey, false))
            {
                SessionState.EraseBool(PendingKey);
                Begin();
            }
            else if (change == PlayModeStateChange.ExitingPlayMode && _running)
            {
                Debug.LogWarning("[GrammarSamples] Play was left before every sample was taken (" + _index + " of " + GrammarSamples.All.Count + ").");
                Stop();
            }
        }

        private static void Begin()
        {
            Directory.CreateDirectory(OutputFolder);
            Lines.Clear();
            _index = 0;
            _player = null;
            _step = Step.WaitForPlayer;
            _running = true;
            EditorApplication.update += Tick;
        }

        private static void Stop()
        {
            EditorApplication.update -= Tick;
            _running = false;
            _player = null;
        }

        private static double Now => Time.unscaledTimeAsDouble;

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            switch (_step)
            {
                case Step.WaitForPlayer:
                    if (!_player) _player = Object.FindAnyObjectByType<DepictionPlayer>();
                    if (!_player)
                    {
                        Finish("no DepictionPlayer in " + EditorSceneManager.GetActiveScene().path + ".");
                        return;
                    }
                    if (_player.Trace == null) return; // Start has not run yet
                    // The running copy only: the battle is driven by the source's suggestion, and no debug
                    // gate may hold or capture on its own.
                    _player.autoPlayDrags = true;
                    _player.autoEndTurn = false;
                    _player.debugStepMode = false;
                    _player.debugCaptureDir = "";
                    StartSample();
                    return;

                case Step.WaitForEffect:
                    WaitForEffect();
                    return;

                case Step.WaitForMoment:
                    if (Now < _momentAt) return;
                    // Freeze every tween (as the debug gates do), so the frame saved is the frame of this moment.
                    _speedBeforeFreeze = UiTween.Speed;
                    UiTween.Speed = 0f;
                    if (File.Exists(_path)) File.Delete(_path);
                    ScreenCapture.CaptureScreenshot(_path);
                    _savedAt = Now;
                    _step = Step.WaitForFile;
                    return;

                case Step.WaitForFile:
                {
                    bool saved = File.Exists(_path);
                    if (!saved && Now - _savedAt < SaveTimeoutSeconds) return;
                    UiTween.Speed = _speedBeforeFreeze;
                    GrammarSample sample = GrammarSamples.All[_index];
                    Record(sample, saved ? "saved" : "NOT saved (is the Game view open?)");
                    NextSample();
                    return;
                }
            }
        }

        private static void StartSample()
        {
            GrammarSample sample = GrammarSamples.All[_index];
            _path = Path.GetFullPath(Path.Combine(OutputFolder, sample.FileName));
            _player.Restart(sample.CreateSource());
            _sampleStartedAt = Now;
            _step = Step.WaitForEffect;
        }

        private static void WaitForEffect()
        {
            GrammarSample sample = GrammarSamples.All[_index];
            if (_player.Trace != null)
            {
                foreach (EffectTrace.Entry entry in _player.Trace.Entries)
                {
                    if (entry.Id != sample.Watch) continue;
                    if (entry.Skipped)
                    {
                        Record(sample, "skipped: " + sample.Watch + " is switched off (DepictionPlayer.effectsOff)");
                        NextSample();
                        return;
                    }
                    // NominalMs is already the wall length at the speed in force (#348).
                    _momentAt = entry.StartSeconds + entry.NominalMs / 1000.0 * CaptureAt;
                    _step = Step.WaitForMoment;
                    return;
                }
            }
            if (Now - _sampleStartedAt > SampleTimeoutSeconds)
            {
                Record(sample, "skipped: " + sample.Watch + " never started");
                NextSample();
            }
        }

        private static void NextSample()
        {
            _index += 1;
            if (_index < GrammarSamples.All.Count) StartSample();
            else Finish(null);
        }

        private static void Record(GrammarSample sample, string outcome)
        {
            string line = sample.FileName + "  " + sample.Label + "  " + outcome;
            Lines.Add(line);
            if (outcome == "saved") Debug.Log("[GrammarSamples] " + line);
            else Debug.LogWarning("[GrammarSamples] " + line);
        }

        private static void Finish(string error)
        {
            Stop();
            _step = Step.Done;
            if (error != null)
            {
                Debug.LogError("[GrammarSamples] " + error);
            }
            else
            {
                var index = new StringBuilder();
                index.Append("# #57 effect grammar samples (Tools/Depiction/Capture Effect Grammar Samples)\n");
                index.Append("# Game view ").Append(Screen.width).Append('x').Append(Screen.height).Append(", taken ")
                    .Append(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append('\n');
                foreach (string line in Lines) index.Append(line).Append('\n');
                string indexPath = Path.GetFullPath(Path.Combine(OutputFolder, "index.txt"));
                File.WriteAllText(indexPath, index.ToString(), new UTF8Encoding(false));
                Debug.Log("[GrammarSamples] " + Lines.Count + " samples in " + Path.GetFullPath(OutputFolder) + " (index.txt lists them).");
                EditorUtility.RevealInFinder(indexPath);
            }
            EditorApplication.ExitPlaymode();
        }
    }
}
