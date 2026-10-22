// The overlays over the battle (scenes/battle/BattleScene.js:185-270): the journal (J / 手記), mounted by the app into the
// scene's own over layer, and the app's settings (the gear / Esc's last step, ctx.onSettings). While either is up the scene
// holds still — the scene clock stops, the flow waits with it, and the result's hold counts only the time no overlay
// covered it (review fix: the enemy's turn and the result played on unseen behind them). The pile list is no overlay of
// the turn: it blocks the cards (they lift on hover) but holds neither the clock nor the turn.
using System;
using Trial.Kit;
using UnityEngine;

namespace Trial.Battle
{
    /// <summary>The journal and the settings over the battle: the JS closures overlayUp / syncPause / updateShift / closeJournal / toggleJournal / openSettings / settingsClosed.</summary>
    // js: scenes/battle/BattleScene.js:185
    internal sealed class BattleOverlays
    {
        /// <summary>The journal while it is open: { destroy, host } (BattleScene.js:245).</summary>
        internal sealed class OpenJournal
        {
            /// <summary>What ctx.openJournal gave (its Destroy is the JS destroy()), or null when it threw.</summary>
            public IOverlay Opened;
            /// <summary>The journal's host (.ocbg-battle-journal: the whole stage in the over layer, taking the pointer).</summary>
            public RectTransform Host;
        }

        private readonly BattleSceneHandle _s;
        private OpenJournal _journal;
        /// <summary>The app's settings overlay is up (ctx.onSettings was called and the scene has had no input since).</summary>
        private bool _settingsOpen;

        internal BattleOverlays(BattleSceneHandle scene)
        {
            _s = scene;
        }

        /// <summary>journal: the open journal, or null.</summary>
        internal OpenJournal Journal => _journal;

        /// <summary>
        /// battle-visual-v1 §4.1 and battle_ui_ux_v2 §3.4 (review 19): neither Esc nor a gear opens the settings in a battle,
        /// unless the app asks for them with ctx.settingsInBattle (and gives ctx.onSettings).
        /// </summary>
        // js: scenes/battle/BattleScene.js:138
        internal static bool SettingsInBattleOf(BattleSceneCtx ctx) => ctx != null && ctx.SettingsInBattle && ctx.OnSettings != null;

        // js: scenes/battle/BattleScene.js:138
        internal bool SettingsInBattle() => SettingsInBattleOf(_s.Ctx);

        /// <summary>The geometry and the player's checks the overlays answer (BattleScene.js:210-212, 268-270).</summary>
        internal void Attach()
        {
            BattleViewEnv env = _s.Env;
            // js: scenes/battle/BattleScene.js:210
            env.Geom.JournalCovers = p => _journal != null && p.X >= L.W * (1f - BattleScene.JournalShare);
            // js: scenes/battle/BattleScene.js:212
            env.Geom.JournalEdge = () => _journal != null ? L.W * (1f - BattleScene.JournalShare) : (float?)null;
            // The pile list does not hold the turn: as in Unity (DepictionPlayer.UpdateEndTurn never looks at it), the plate
            // and X end the turn with the list open, and the list stays open and follows the new piles. Only the cards wait
            // for it to close (§3.5, syncPause).
            // js: scenes/battle/BattleScene.js:268
            _s.PlayerCore.SetOverlayCheck(OverlayUp);
            _s.PlayerCore.SetDrawerCheck(() => _s.HudView.DrawerOpen());
            _s.PlayerCore.SetJournalCheck(() => _journal != null);
        }

        // js: scenes/battle/BattleScene.js:192
        internal bool OverlayUp() => _journal != null || _settingsOpen;

        /// <summary>pause(on)'s half: settingsOpen = on.</summary>
        // js: scenes/battle/BattleScene.js:527
        internal void SetSettingsOpen(bool on) => _settingsOpen = on;

        /// <summary>
        /// syncPause(): the scene clock holds while an overlay is up; behind the journal or the pile list the cards can still be
        /// hovered (not taken), the settings' modal holds all. A card pressed or picked by its digit with the pile list or the
        /// journal open says why nothing happens (onTry → the refusal line); a refusal line that asked to close one goes with it.
        /// </summary>
        // js: scenes/battle/BattleScene.js:193
        internal void SyncPause()
        {
            BattleSceneHandle s = _s;
            s.Env.Clock.SetPaused(OverlayUp());
            bool drawer = s.HudView.DrawerOpen();
            Action onTry = _journal != null && !_settingsOpen ? (Action)(() => s.PlayerCore.RefuseWhileJournal())
                : drawer && !OverlayUp() ? (Action)(() => s.PlayerCore.RefuseWhileDrawer()) : null;
            s.HandView.SetBlocked(OverlayUp() || drawer, !_settingsOpen && (_journal != null || drawer), onTry);
            s.PlayerCore.OverlaysChanged(drawer, _journal != null);
            s.PlayerCore.RefreshPlayable();
            s.Input?.UpdateFF();
        }

        /// <summary>
        /// updateShift(): battle_ui_ux_v2 §2.3 / §2.4 — the figures slide 15 % left while the journal drawer (right) is open,
        /// 15 % right while the pile list (left) is open, and stay when both are.
        /// </summary>
        // js: scenes/battle/BattleScene.js:217
        internal void UpdateShift()
        {
            if (_s.Destroyed) return;
            _s.FieldModule.SetShift((_s.HudView.DrawerOpen() ? 1 : 0) - (_journal != null ? 1 : 0));
        }

        /// <summary>
        /// closeJournal(): the journal's destroy(), its host removed. No journalOpen / journalClose here — the journal plays its
        /// own (docs/AUDIO.md 「戦闘でも」), and the scene's copy made each one ring twice.
        /// </summary>
        // js: scenes/battle/BattleScene.js:221
        internal void CloseJournal()
        {
            if (_journal == null) return;
            OpenJournal j = _journal;
            _journal = null;
            try { j.Opened?.Destroy(); }
            catch (Exception err) { _s.Env.Log.Error(null, err); }
            if (j.Host) Ui.Remove(j.Host);
            SyncPause();
            UpdateShift();
        }

        /// <summary>
        /// toggleJournal(): open (a held card goes home and a click / key selection drops: letGo) or close the journal. ctx.
        /// openJournal(host, { enemyIds, disclosureOf, onClose }) mounts it into a host over the whole stage in the over layer.
        /// </summary>
        // js: scenes/battle/BattleScene.js:232
        internal void ToggleJournal()
        {
            if (_journal != null) { CloseJournal(); return; }
            BattleSceneHandle s = _s;
            if (s.Ctx.OpenJournal == null || s.PlayerCore.State == BattlePlayerState.Over) return;
            s.HandView.LetGo();
            // .ocbg-battle-journal { position: absolute; inset: 0; pointer-events: auto }
            RectTransform jhost = Ui.Div(s.Env.Layer.Over, "ocbg-battle-journal", 0f, 0f, L.W, L.H);
            Hit.Area(jhost);
            IOverlay opened = null;
            try
            {
                opened = s.Ctx.OpenJournal(jhost, new BattleJournalOpts
                {
                    EnemyIds = s.PlayerCore.EnemyIds(),
                    DisclosureOf = s.Ctx.DisclosureOf,
                    OnClose = CloseJournal,
                });
            }
            catch (Exception err)
            {
                s.Env.Log.Error(null, err);
            }
            _journal = new OpenJournal { Opened = opened, Host = jhost };
            SyncPause();
            UpdateShift();
        }

        /// <summary>
        /// openSettings(): only with ctx.settingsInBattle — a held card goes home, ctx.onSettings(), and the scene holds until a
        /// key of its own or a click on it says no modal is up (the app's refresh(), main.js closeOverlay, says so too).
        /// </summary>
        // js: scenes/battle/BattleScene.js:249
        internal void OpenSettings()
        {
            if (!SettingsInBattle()) return;
            _s.HandView.LetGo();
            _s.Ctx.OnSettings();
            // The app's overlay is modal: it holds the scene until it closes. Review fix: a modal passes on the keys it has no
            // use for (Shift, Ctrl, Alt, F-keys …), so a key that reaches the scene proves nothing. Only one of the scene's own
            // keys (sceneKey) or a click on the scene means no overlay is up.
            _settingsOpen = true;
            SyncPause();
        }

        // js: scenes/battle/BattleScene.js:260
        internal void SettingsClosed()
        {
            if (!_settingsOpen) return;
            _settingsOpen = false;
            SyncPause();
        }

        /// <summary>Whether t lies in the open journal's host (.ocbg-battle-journal).</summary>
        internal bool InJournal(Transform t) => _journal != null && _journal.Host && t && t.IsChildOf(_journal.Host);
    }
}
