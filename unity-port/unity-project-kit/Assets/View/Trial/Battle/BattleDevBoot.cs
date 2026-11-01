// A dev boot for the battle scene alone (the JS dev/battle.js: enemy × layer picker, auto play), so the battle can be
// tried before the router exists. Put it on an empty GameObject in an empty scene and press Play: it makes a TrialStage
// and a TrialAudio (unless the scene has them), builds a CoreBattleSource through the bridge (Depiction.Bridge.BattleLaunch,
// as Battle.unity's BattleBootstrap does) and mounts the battle scene into a stage layer. When the battle finishes (or
// fails) it logs it and, with Restart On Finish, mounts the next one with the seed + 1.
using System;
using System.Collections.Generic;
using Depiction;
using Depiction.Bridge;
using Trial.Audio;
using Trial.Bridge;
using Trial.Kit;
using UnityEngine;

namespace Trial.Battle
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Trial/Battle Dev Boot")]
    public sealed class BattleDevBoot : MonoBehaviour
    {
        [Header("Battle (BattleLaunch)")]
        [Tooltip("Roster id of the enemy (enemy_roster_v4), as Battle.unity's BattleBootstrap takes it.")]
        public string enemyId = "polearm_warped";
        [Tooltip("The enemies standing behind the first one on the line (two or three enemies). Empty for one.")]
        public string[] moreEnemyIds = new string[0];
        [Tooltip("Fixes every shuffle.")]
        public int seed = BattleLaunch.DefaultSeed;
        [Tooltip("Draws a fresh seed on every Play and logs it.")]
        public bool randomSeed;
        [Tooltip("The cells on the line (5〜8).")]
        public int fieldCells = BattleLaunch.SliceFieldCells;
        [Tooltip("The gap N on turn 1.")]
        public int startGap = BattleLaunch.DefaultStartGap;
        [Tooltip("The journal's 開示度 for the enemy (−1〜2).")]
        [Range(BattleLaunch.LowestDisclosure, 2)]
        public int disclosure = BattleLaunch.DefaultDisclosure;

        [Header("Scene (ctx)")]
        [Tooltip("ctx.layer: the exploration's layer (1..7), the backdrop and the corner.")]
        [Range(1, 7)]
        public int layer = 1;
        [Tooltip("ctx.nodeKind.")]
        public BattleNodeKind nodeKind = BattleNodeKind.Battle;
        [Tooltip("ctx.chain.index (1-based) and total; total 1 = a fight on its own.")]
        public int chainIndex = 1;
        public int chainTotal = 1;
        [Tooltip("ctx.miasmaPercent; negative = none (the frame's).")]
        public float miasmaPercent = -1f;
        [Tooltip("ctx.settingsInBattle: the gear and Esc's release (logged here: the dev boot has no settings screen).")]
        public bool settingsInBattle;

        [Header("Settings")]
        [Tooltip("settings.speed: 1, 1.25 or 1.5.")]
        public double speed = TrialSettings.DefaultSpeed;
        [Tooltip("settings.shortFx (演出短縮).")]
        public bool shortFx;
        [Tooltip("Plays the sounds (TrialAudio).")]
        public bool sound = true;

        [Header("Dev")]
        [Tooltip("ctx.auto: the player plays itself (the source's AutoCardId; '' ends the turn).")]
        public bool autoPlay;
        [Tooltip("Mounts the next battle (seed + 1) when one finishes.")]
        public bool restartOnFinish;

        private TrialStage _stage;
        private bool _ownStage;
        private TrialAudio _audio;
        private bool _ownAudio;
        private RectTransform _host;
        private BattleSceneHandle _scene;
        private TrialSettings _settings;
        private int _seedInUse;

        /// <summary>The mounted scene (for a debugger or a test), or null.</summary>
        public BattleSceneHandle Scene => _scene;

        private void Start()
        {
            // Domain reload is off: nothing a previous Play session posted may run in this one.
            Microtasks.Reset();
            // The engine-free default writes to the standard error stream, which the Editor's console does not show.
            Microtasks.Unhandled = e => Debug.LogException(e);
            _stage = TrialStage.Current;
            if (!_stage)
            {
                _stage = TrialStage.Create("TrialStage (battle dev)");
                _ownStage = true;
            }
            _settings = new TrialSettings { Speed = speed, ShortFx = shortFx };
            if (sound)
            {
                try
                {
                    _audio = TrialAudio.Current;
                    if (_audio == null)
                    {
                        _audio = CreateAudio(_settings);
                        _ownAudio = _audio != null;
                    }
                }
                catch (Exception e) { Debug.LogException(e); _audio = null; }
            }
            _host = _stage.Layer("battle", 0);
            _seedInUse = randomSeed ? UnityEngine.Random.Range(1, int.MaxValue) : seed;
            Mount();
        }

        /// <summary>Builds the source (BattleLaunch) and mounts the battle scene.</summary>
        [ContextMenu("Mount again")]
        public void Mount()
        {
            Unmount();
            IDepictionSource source;
            var launch = new BattleLaunch
            {
                EnemyId = enemyId,
                Seed = _seedInUse,
                FieldCells = fieldCells,
                StartGap = startGap,
                Disclosure = disclosure,
                MoreEnemyIds = moreEnemyIds != null && moreEnemyIds.Length > 0 ? new List<string>(moreEnemyIds) : null,
                ChainIndex = Math.Max(1, chainIndex),
                ChainTotal = Math.Max(1, chainTotal),
            };
            try { source = launch.CreateSource(); }
            catch (Exception e)
            {
                // A wrong id must not quietly start some other battle.
                Debug.LogError("[BattleDevBoot] " + e.Message);
                return;
            }
            var ctx = new BattleSceneCtx
            {
                Source = source,
                Stage = _stage,
                Audio = _audio,
                Settings = _settings,
                Layer = layer,
                LayerTitle = "",
                MiasmaPercent = miasmaPercent >= 0f ? miasmaPercent : (float?)null,
                NodeKind = nodeKind,
                Chain = chainTotal > 1 ? new BattleChain(Math.Max(1, chainIndex), chainTotal) : null,
                DisclosureOf = _ => disclosure,
                SettingsInBattle = settingsInBattle,
                OnSettings = settingsInBattle ? (Action)(() => Debug.Log("[BattleDevBoot] settings asked for (no settings screen here)")) : null,
                OnSettingsChange = s => Debug.Log("[BattleDevBoot] speed ×" + s.Speed),
                OnError = err => Debug.LogError("[BattleDevBoot] the battle stopped: " + err),
                OnFinished = OnFinished,
                Auto = autoPlay
                    ? new BattleAuto { Choose = (s, played) => new BattleAutoPick(s is ITrialSource t ? t.AutoCardId ?? "" : s.SuggestedCardId ?? "") }
                    : null,
            };
            _scene = BattleScene.MountBattleScene(_host, ctx);
            Debug.Log("[BattleDevBoot] " + enemyId + (moreEnemyIds != null && moreEnemyIds.Length > 0 ? " + " + string.Join(", ", moreEnemyIds) : "")
                + " / seed " + _seedInUse + " / " + fieldCells + " cells / layer " + layer);
        }

        /// <summary>
        /// TrialAudio.Create(master, effects, ambience, muted) from the settings' volumes. Through reflection: its overloads
        /// name Trial.Audio.Synth's SoundSettings, which Trial.Battle does not reference (the dev boot only needs the sound on).
        /// </summary>
        private static TrialAudio CreateAudio(TrialSettings s)
        {
            foreach (System.Reflection.MethodInfo m in typeof(TrialAudio).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (m.Name != "Create") continue;
                System.Reflection.ParameterInfo[] ps = m.GetParameters();
                if (ps.Length != 6 || ps[0].ParameterType != typeof(double)) continue;
                return (TrialAudio)m.Invoke(null, new object[] { s.Master, s.Effects, s.Ambience, s.Muted, null, true });
            }
            return null;
        }

        private void OnFinished()
        {
            Debug.Log("[BattleDevBoot] finished: " + (_scene != null && _scene.Player != null ? _scene.Player.State.ToString() : "?"));
            if (!restartOnFinish) return;
            _seedInUse++;
            // The scene is torn down after the call that finished it has returned (as the app's show() does).
            Microtasks.Post(Mount);
        }

        private void Unmount()
        {
            BattleSceneHandle s = _scene;
            _scene = null;
            if (s != null) s.Destroy();
        }

        private void OnDestroy()
        {
            Unmount();
            // Only the sound this boot made: a TrialAudio the scene already had belongs to whoever made it.
            if (_audio != null && _ownAudio)
            {
                try { _audio.Destroy(); }
                catch (Exception) { /* its objects are gone already */ }
            }
            _audio = null;
            _ownAudio = false;
            if (_ownStage && _stage) Destroy(_stage.gameObject);
        }
    }
}
