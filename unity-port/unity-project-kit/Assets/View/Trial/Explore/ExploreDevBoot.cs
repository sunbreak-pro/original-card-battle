// A development boot for the exploration scene, before the router (Trial.Boot) exists: put it on an empty GameObject of
// an empty scene and press Play. It makes a stage, starts a life through the bridges with a fixed lineage seed
// (Inheritance.NewLineage → StartLife, Trial.Bridge Life.MakeStart / BeginLife with the trial options: the same path
// as main.js beginLife), and mounts ExploreScene with a ctx shaped like main.js toExplore's. Battles are fought
// unattended with the source's AutoCardId (main.js autoFight, the ?autobattle switch) and the scene is mounted again
// afterwards (toExploreBack). A life that ends records its end on the lineage and the next heir sets out at once.
// No app overlays: the scene's own fallback journal / page stand in (ctx.onOverlay left out).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Depiction;
using Depiction.Bridge;
using DungeonContent;
using DungeonCore;
using Exploration.Bridge;
using InheritanceCore;
using Trial.Audio;
using Trial.Bridge;
using Trial.Kit;
using UnityEngine;

namespace Trial.Explore
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Trial/Explore Dev Boot")]
    public sealed class ExploreDevBoot : MonoBehaviour
    {
        [Tooltip("The lineage seed (はじめから with a fixed seed): the same seed walks the same lives.")]
        public long LineageSeed = 20261023;

        [Tooltip("演出短縮 (settings.shortFx): the scene's JS timers run at 35 %.")]
        public bool ShortFx;

        [Tooltip("Make the trial's sound (TrialAudio).")]
        public bool WithAudio = true;

        [Tooltip("The tools carried (DungeonContent ItemCatalogue ids).")]
        public string[] Tools = { "bosho_no_men", "kokugen_no_suna", "ikitsugi_no_fue" };

        [Tooltip("The consumables carried (up to three).")]
        public string[] Consumables = { "yakuso", "joka_no_ko", "shirube_no_ishi" };

        private TrialStage _stage;
        private ITrialAudio _audio;
        private TrialSettings _settings;
        private Lineage _lineage;
        private LiveLife _live;
        private RectTransform _host;
        private IScene _scene;

        private void Start()
        {
            _stage = TrialStage.Create("TrialStage (explore dev)");
            _settings = TrialSettings.Defaults();
            _settings.ShortFx = ShortFx;
            if (WithAudio) _audio = MakeAudio(_settings);
            _lineage = Inheritance.NewLineage(unchecked((ulong)LineageSeed));
            BeginLife();
        }

        /// <summary>
        /// TrialAudio.Create(master, effects, ambience, muted): the trial's sound, or null. Called by reflection: both
        /// Create overloads name Trial.Audio.Synth's SoundSettings, which Trial.Explore does not reference, so the
        /// compiler cannot pick one here.
        /// </summary>
        private static ITrialAudio MakeAudio(TrialSettings s)
        {
            try
            {
                MethodInfo create = typeof(TrialAudio).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "Create" && m.GetParameters().Length == 6 && m.GetParameters()[0].ParameterType == typeof(double));
                if (create == null) return TrialAudio.Current;
                return (ITrialAudio)create.Invoke(null, new object[] { s.Master, s.Effects, s.Ambience, s.Muted, null, true });
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }

        /// <summary>出立 (main.js onSetOut + beginLife): the next heir sets out with the prototype deck and the carried items.</summary>
        private void BeginLife()
        {
            LifeStart ls = Inheritance.StartLife(_lineage);
            _lineage = ls.Lineage;
            var tools = (Tools ?? Array.Empty<string>()).Where(id => !string.IsNullOrEmpty(id)).ToList();
            var consumables = (Consumables ?? Array.Empty<string>()).Where(id => !string.IsNullOrEmpty(id)).Take(3).ToList();
            var choice = new DepartureChoice
            {
                Deck = BattleCore.PrototypeDeck.Build(),
                BattleToolIds = tools.Where(id => ItemCatalogue.Tool(id).Side == EffectSide.Battle).ToList(),
                Loadout = Loadout.Of(tools, consumables, ls.ToolSlots),
                Talent = null,
                ConsumableIds = consumables,
            };
            LifeStartData start = Trial.Bridge.Life.MakeStart(ls, choice);
            _live = Trial.Bridge.Life.BeginLife(start, new LifeLog(), "", null, _lineage);
            Debug.Log("[explore dev] 第 " + ls.Life.Generation + " 代 " + ls.Life.Name + " sets out (seed " + ls.Seed + ")");
            Show();
        }

        /// <summary>show(mountExploreScene, ctx) (main.js toExplore): the old scene goes, a fresh one is mounted.</summary>
        private void Show()
        {
            if (_scene != null)
            {
                IScene old = _scene;
                _scene = null;
                old.Destroy();
            }
            if (_host) Destroy(_host.gameObject);
            if (_live == null) return;
            _host = _stage.Layer("ocbg-explore", 0);
            _scene = ExploreScene.Mount(_host, MakeCtx());
        }

        // js: app/main.js:360 (toExplore's ctx)
        private ExploreSceneCtx MakeCtx() => new ExploreSceneCtx
        {
            Stage = _stage,
            Audio = _audio,
            Settings = _settings,
            Expedition = _live.Expedition,
            Inheritance = new ExploreInheritance
            {
                TraceView = (layer, nodeId) => Inheritance.TraceNodeView(_lineage, layer),
                Scribbles = () => _live != null ? _live.Scribbles.ToList() : new List<Scribble>(),
                Take = Take,
            },
            OnBattle = OnBattle,
            OnLifeEnd = OnLifeEnd,
            OnOverlay = null,
            OnSaveQuit = () => Debug.Log("[explore dev] 保存して閉じる (no save in the dev boot)"),
            OnPersist = () => { },
            OnCarving = () => _lineage = Inheritance.NoteCarving(_lineage),
        };

        // js: app/main.js:370 (inheritance.take)
        private ExploreTaken Take(IReadOnlyList<string> picks)
        {
            if (_live == null || _live.Raw.Session.State.IsOver) return null;
            int layer = _live.Raw.Session.State.Profile.Layer;
            TraceNodeView view = Inheritance.TraceNodeView(_lineage, layer);
            if (view == null || !view.CanTake) return null;
            TakeResult r = Inheritance.TakeTrace(_lineage, view.TraceId, picks ?? Array.Empty<string>(), layer);
            _lineage = r.Lineage;
            // `live.expedition.Inherit(taken) || {}`: the result always comes with the 走り書き rows
            InheritResult result = _live.Expedition.Inherit(r.Taken) ?? new InheritResult(null, null, 0);
            AddedScribbles added = Trial.Bridge.Life.AddScribbles(_live.Scribbles, r.Taken.Journal != null ? r.Taken.Journal.Scribbles : null);
            _live.Scribbles = added.List;
            return new ExploreTaken(r.Taken, result.WithScribbles(added.Rows));
        }

        // js: app/main.js:391 (onBattle: runEncounter, then toExploreBack)
        private Promise OnBattle()
        {
            RunEncounter();
            return Promise.Resolved().Then(() =>
            {
                if (_live == null) return;
                Show();
            });
        }

        // js: app/main.js:428 (runEncounter, AUTOBATTLE)
        private void RunEncounter()
        {
            RecordedExpedition ex = _live.Expedition;
            RecordedBattle battle = ex.Battle ?? ex.StartEncounter();
            if (battle == null) return;
            try { _audio?.DuckAmbience(0.4); } catch (Exception) { /* optional */ }
            RecordedSource resumed = _live.OpenSource;
            for (int guard = 0; guard < 16 && !battle.Over; guard++)
            {
                SettlingFight fight = Recorder.SettleWhenDecided(battle, resumed ?? battle.StartNext(true));
                resumed = null;
                if (fight.Settled) continue;
                AutoFight(fight.Source);
                fight.Finish();
            }
            try { _audio?.DuckAmbience(1); } catch (Exception) { /* optional */ }
        }

        // js: app/main.js:409
        /// <summary>autoFight(source): every turn plays the source's AutoCardId (its suggestion), else ends the turn.</summary>
        private static void AutoFight(ITrialSource source)
        {
            for (int guard = 0; guard < 20000 && !source.Finished; guard++)
            {
                if (source.WaitingForPlayer)
                {
                    string id = source.AutoCardId;
                    CardFace face = !string.IsNullOrEmpty(id) ? DepictionText.Find(source.Frame.Hand, id) : null;
                    if (face != null) source.TryPlay(id, DepictionText.RequiredZone(face.Aim), out DepictionEvent _);
                    else source.EndTurn();
                }
                else
                {
                    try { source.AdvanceAuto(); }
                    catch (Exception e) { Debug.LogException(e); break; }
                }
            }
        }

        // js: app/main.js:485 (endLife: the end goes on the lineage; here the next heir sets out at once)
        private void OnLifeEnd(ExploreLifeEnd end)
        {
            if (_live == null) return;
            Expedition raw = _live.Raw;
            ExplorationState state = raw.Session.State;
            // a life still exploring has not ended (main.js:488)
            if (!state.IsOver || state.Phase == RunPhase.Exploring) return;
            LifePlace place = Inheritance.PlaceFromState(state);
            _lineage = Inheritance.RecordEnd(_lineage, null, place, raw.Mastery, Names.LifeJournal(raw, _live), Names.EndOptions(raw, state));
            Debug.Log("[explore dev] the life ended: " + end.Ending + " on layer " + end.Layer + " (" + end.Title + ")");
            _live = null;
            BeginLife();
        }

        private void OnDestroy()
        {
            if (_scene != null)
            {
                try { _scene.Destroy(); }
                catch (Exception e) { Debug.LogException(e); }
                _scene = null;
            }
            if (_audio is TrialAudio a)
            {
                try { a.Destroy(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            if (_stage) Destroy(_stage.gameObject);
        }
    }
}
