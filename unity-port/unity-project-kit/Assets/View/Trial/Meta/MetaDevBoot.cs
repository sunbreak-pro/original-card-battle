// A dev page for the meta screens (port of the JS's dev/meta.js): every screen mounted with sample data — a lineage
// with two past lives (the first fell on layer 3 and left a trace, the second closed alive on layer 4 and is the
// survivor), a crowded one (three deaths), a first heir, the departure's owned kinds, a mastery book, journal pages —
// so the main chat can look at each screen before the router (Trial.Boot) exists.
//
// Put it on an empty GameObject in a scene and press Play. F7 / F8 step back / on through the screens (KeyStack's
// capture phase, so a screen's own keys are untouched); the Inspector's Screen picks the first one (a name of
// Screens, e.g. "hall", "ending-death", "journal-app"). Pages "parts" and "backdrops" show the shared pieces of this
// unit (buttons, keys, h2, washi, the meta card faces, the four backdrops).
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonCore;
using Exploration;
using Exploration.Bridge;
using InheritanceCore;
using TMPro;
using Trial.Audio;
using Trial.Bridge;
using Trial.Geometry;
using Trial.Kit;
using Trial.Script;
using UnityEngine;
using JBook = global::Journal.JournalBook;
using JContext = global::Journal.JournalContext;
using JPage = global::Journal.JournalPage;
using JRow = global::Journal.JournalRow;
using JTab = global::Journal.JournalTab;
using JTerms = global::Journal.JournalTerms;
using LifeJournal = InheritanceCore.Journal;

namespace Trial.Meta
{
    [AddComponentMenu("Trial/Dev/Meta Dev Boot")]
    public sealed class MetaDevBoot : MonoBehaviour
    {
        [Tooltip("The screen shown first (a name of Screens: title, title-first, hall, hall-many, hall-first, departure, departure-app, hall-app, ending-death, ending-alive, ending-cleared, ending-miasma, settings, owned, journal-explore, …, parts, backdrops).")]
        public string Screen = "title";

        [Tooltip("Draw through the gamma target (kit.md §4.12).")]
        public bool UseGammaTarget = true;

        [Tooltip("Write the screen's name in a corner of the stage (dev chrome; off for screenshots).")]
        public bool ShowName;

        private TrialStage _stage;
        private ITrialAudio _audio;
        private TrialAudio _ownAudio;
        private TrialSettings _settings;
        private RectTransform _baseLayer, _overLayer, _nameLayer;
        private IScene _base;
        private IOverlay _over;
        private string _shown;
        private TextMeshProUGUI _nameLabel;
        private readonly Dictionary<string, Action> _screens = new Dictionary<string, Action>(StringComparer.Ordinal);
        private readonly List<string> _order = new List<string>();

        // ---------------------------------------------------------------- sample data (dev only; dev/meta.js:18-91)

        private static readonly Dictionary<string, string> EnemyNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["miasma_priest"] = "大黒蛇 セルク", ["shadow_hound"] = "瘴牙の走竜", ["polearm_warped"] = "錆槍の竜兵", ["armored_warden"] = "鉄壁の門竜",
        };

        // js: dev/meta.js:19
        private static string CardName(string id, int rank)
        {
            BattleCore.MasteryProfile p = BattleCore.MasteryProfiles.Find(id);
            string n = p?.NameAt(rank);
            return !string.IsNullOrEmpty(n) ? n : BattleCore.CardCatalog.ById(id).Name;
        }

        // js: dev/meta.js:21
        private static string EnemyName(string id) => EnemyNames.TryGetValue(id, out string n) ? n : id;

        private static EndOptions NameOpts() => new EndOptions { CardName = CardName, EnemyName = EnemyName };

        private static Dictionary<string, int> Disclosure(params (string Id, int N)[] d) => d.ToDictionary(x => x.Id, x => x.N);

        private static LifeJournal Book(int pages, string memo, params (string Id, int N)[] disclosure) =>
            LifeJournal.WithPageCount(pages, Disclosure(disclosure), memo);

        private Lineage _lineage, _afterDeath, _crowded, _first;
        private LifeStart _lifeStart;
        private List<string> _owned;
        private BattleCore.MasteryBook _book;
        private List<string> _deckIds;
        private Expedition _expedition;
        private string _memo = "門竜は 3 ターン目に必ず構える。\n構えの前に崩しを入れる。";

        // js: dev/meta.js:23
        private void SampleLineage()
        {
            Lineage L = Inheritance.NewLineage(20261020UL);
            LifeStart s1 = Inheritance.StartLife(L);
            L = Inheritance.RecordDeath(s1.Lineage, null, new LifePlace(3, 14, NodeKind.Boss, RunPhase.Fallen),
                new List<CardEntry> { new CardEntry("thrust", 2), new CardEntry("kesa_cut", 1), new CardEntry("brace", 1), new CardEntry("observe", 1), new CardEntry("rock_stance", 0) },
                Book(1, "大黒蛇は懐に入ると締め付けてくる。間合い 2 で待つこと。", ("miasma_priest", 2), ("shadow_hound", 1)), NameOpts());
            _afterDeath = L;
            LifeStart s2 = Inheritance.StartLife(L);
            L = Inheritance.RecordSurvival(s2.Lineage, null, new LifePlace(4, 9, NodeKind.Rest, RunPhase.Survived),
                new List<CardEntry> { new CardEntry("reach_thrust", 1), new CardEntry("footwork", 2) },
                Book(0, "", ("polearm_warped", 2), ("armored_warden", 1)), NameOpts());
            _lineage = L;
        }

        // js: dev/meta.js:40
        private static Lineage CrowdedLineage()
        {
            Lineage L = Inheritance.NewLineage(99UL);
            void Die(int layer, NodeKind kind, RunPhase ending, List<CardEntry> deck, LifeJournal journal)
            {
                LifeStart s = Inheritance.StartLife(L);
                L = Inheritance.RecordDeath(s.Lineage, null, new LifePlace(layer, null, kind, ending), deck, journal, NameOpts());
            }
            Die(2, NodeKind.Battle, RunPhase.Fallen, new List<CardEntry> { new CardEntry("thrust", 1), new CardEntry("brace", 2) }, Book(0, "", ("polearm_warped", 1)));
            Die(2, NodeKind.Event, RunPhase.Fallen, new List<CardEntry> { new CardEntry("observe", 1) }, Book(0, ""));
            Die(5, NodeKind.Elite, RunPhase.MiasmaDeath, new List<CardEntry> { new CardEntry("kesa_cut", 2), new CardEntry("lunge", 1), new CardEntry("focus", 1) },
                Book(2, "門竜は 3 ターン目に必ず構える。", ("armored_warden", 2)));
            return L;
        }

        // js: dev/meta.js:127
        private static Lineage ClearedLineage()
        {
            Lineage L = Inheritance.NewLineage(5UL);
            LifeStart s1 = Inheritance.StartLife(L);
            return Inheritance.RecordSurvival(s1.Lineage, null, new LifePlace(7, 30, NodeKind.Boss, RunPhase.Completed),
                new List<CardEntry> { new CardEntry("thrust", 3) }, Book(0, ""), NameOpts());
        }

        // js: dev/meta.js:59
        private static List<JPage> JournalPages() => new List<JPage>
        {
            new JPage("enemy-miasma_priest", JTab.Enemy, "大黒蛇 セルク", new[]
            {
                new JRow("姿", "壁に巻きついた大蛇。背の結晶が藍紫に光る", 1),
                new JRow("間合いの傾向", "間合い 0 では締め付け、1〜2 では尾で払う。3 以上では動かない", 2),
                new JRow("得意な手", "足を縛る（鈍足）。毎ターン狙ってくる", 2),
                new JRow("弱点", "崩しの後、守りが薄くなる", 2),
            }, 2),
            new JPage("enemy-shadow_hound", JTab.Enemy, "瘴牙の走竜", new[]
            {
                new JRow("姿", "四つ足で低く走る竜", 1),
                new JRow("間合いの傾向", "遠いと駆け寄り、近いと噛みつく", 2),
                new JRow("弱点", "身を低くした次のターン", 2),
            }, 1),
            new JPage("dungeon-3", JTab.Dungeon, "第 3 層　大黒蛇の住処", new[]
            {
                new JRow("瘴気の濃さ", "1 刻限ごとに +4%"),
                new JRow("刻限", "10（寄り道なしの踏破は 6）"),
                new JRow("ノード", "通常戦闘 4・精鋭 1・階層ボス 1・休息 2・情報収集 1"),
                new JRow("情報収集", "まだ"),
            }),
            new JPage("memo", JTab.Memo, "メモ", new[] { new JRow("", "大黒蛇は懐に入ると締め付けてくる。\n間合い 2 で待つこと。") }),
            JTerms.Page(),
        };

        // js: dev/meta.js:86
        private Expedition SampleExpedition()
        {
            if (_expedition != null) return _expedition;
            try
            {
                _expedition = new Expedition(new RunStart { Seed = 20261020UL }, null, null, "thrust", null,
                    new Dictionary<string, int>(StringComparer.Ordinal) { ["armored_warden"] = 1, ["polearm_warped"] = 2 });
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            return _expedition;
        }

        private static readonly IReadOnlyList<Scribble> SampleScribbles = new[] { new Scribble(1, "アルマ", "大黒蛇は懐に入ると締め付けてくる。間合い 2 で待つこと。") };

        private void BuildSamples()
        {
            SampleLineage();
            _crowded = CrowdedLineage();
            _first = Inheritance.NewLineage(7UL);
            _lifeStart = Inheritance.StartLife(_lineage);
            // The kinds a character owns from the start (#1〜#40), and three learned on the way (dev/meta.js:52).
            _owned = BattleCore.CardCatalog.All.Take(40).Select(d => d.Id).Concat(new[] { "dash_in", "gale_thrust", "wolf_stance" }).ToList();
            var records = Inheritance.MergeMastery((IReadOnlyDictionary<string, BattleCore.CardMastery>)null, new[]
            {
                new BattleCore.CardMastery("thrust", 11, 2), new BattleCore.CardMastery("kesa_cut", 5, 1), new BattleCore.CardMastery("water_stance", 4, 1),
                new BattleCore.CardMastery("observe", 12, 3), new BattleCore.CardMastery("footwork", 2, 0), new BattleCore.CardMastery("brace", 3, 1),
            });
            _book = new BattleCore.MasteryBook("thrust", records, null);
            _deckIds = new List<string> { "thrust", "kesa_cut", "water_stance", "observe", "brace", "footwork", "reach_thrust", "rock_stance", "lunge", "back_leap" };
        }

        // ---------------------------------------------------------------- page

        private void Start()
        {
            _stage = TrialStage.Current ? TrialStage.Current : TrialStage.Create("TrialStage (meta dev)", UseGammaTarget);
            try { TrialFonts.LoadAll(); }
            catch (Exception e) { Debug.LogException(e); }
            _settings = TrialSettings.Defaults();
            if (TrialAudio.Current != null) _audio = TrialAudio.Current;
            else
            {
                try { _audio = _ownAudio = MakeAudio(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            try { BuildSamples(); }
            catch (Exception e) { Debug.LogException(e); }
            DefineScreens();
            KeyStack.KeyDownCapture += OnKeyCapture;
            Show(string.IsNullOrEmpty(Screen) ? "title" : Screen);
        }

        /// <summary>
        /// TrialAudio.Create(master .8, effects 1, ambience .6, unmuted) as dev/meta.js makes it. Called by reflection:
        /// Create's signature names Trial.Audio.Synth's SoundSettings, which Trial.Meta does not reference.
        /// </summary>
        private static TrialAudio MakeAudio()
        {
            foreach (System.Reflection.MethodInfo m in typeof(TrialAudio).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (m.Name != "Create") continue;
                System.Reflection.ParameterInfo[] ps = m.GetParameters();
                if (ps.Length == 6 && ps[0].ParameterType == typeof(double))
                    return m.Invoke(null, new object[] { 0.8, 1.0, 0.6, false, null, true }) as TrialAudio;
            }
            return null;
        }

        private void OnDestroy()
        {
            KeyStack.KeyDownCapture -= OnKeyCapture;
            Clear();
            if (_ownAudio != null)
            {
                try { _ownAudio.Destroy(); }
                catch (Exception) { /* gone already */ }
                _ownAudio = null;
            }
        }

        private void OnKeyCapture(KeyEvent e)
        {
            if (e.Repeat || (e.Key != "F7" && e.Key != "F8") || _order.Count == 0) return;
            int i = Math.Max(0, _order.IndexOf(_shown));
            i = (i + (e.Key == "F8" ? 1 : _order.Count - 1)) % _order.Count;
            // after the event: a screen's handler may still be running on the stack
            Microtasks.Post(() => Show(_order[i]));
        }

        private void Clear()
        {
            if (_over != null) { try { _over.Destroy(); } catch (Exception e) { Debug.LogException(e); } _over = null; }
            if (_overLayer) { Ui.Remove(_overLayer); _overLayer = null; }
            if (_base != null) { try { _base.Destroy(); } catch (Exception e) { Debug.LogException(e); } _base = null; }
            if (_baseLayer) { Ui.Remove(_baseLayer); _baseLayer = null; }
        }

        // js: dev/meta.js:111
        private void ScreenOf(Func<RectTransform, IScene> fn)
        {
            _baseLayer = _stage.Layer("dev-base", 0);
            _base = fn(_baseLayer);
        }

        // js: dev/meta.js:112
        private void Overlay(Func<RectTransform, Action, IOverlay> fn)
        {
            if (_over != null) CloseOverlay();
            _overLayer = _stage.Layer("dev-over", 500);
            _over = fn(_overLayer, CloseOverlay);
        }

        private void CloseOverlay()
        {
            if (_over == null) return;
            IOverlay o = _over;
            _over = null;
            try { o.Destroy(); } catch (Exception e) { Debug.LogException(e); }
            if (_overLayer) { Ui.Remove(_overLayer); _overLayer = null; }
            _base?.Refresh();
        }

        private static void Log(string what) => Debug.Log("[meta] " + what);

        private T Common<T>(T ctx) where T : MetaCtx
        {
            ctx.Stage = _stage;
            ctx.Audio = _audio;
            ctx.Settings = _settings;
            return ctx;
        }

        private SettingsCtx SettingsOf(Action close, bool canSaveQuit = true, string kind = null) => Common(new SettingsCtx
        {
            CanSaveQuit = canSaveQuit,
            SaveQuitKind = kind,
            OnSaveQuit = () => Log("save & quit"),
            OnChange = s => { _settings.Assign(s); Log("settings speed " + s.Speed + " shortFx " + s.ShortFx); },
            OnClose = close,
        });

        // js: dev/meta.js:118
        private void DefineScreens()
        {
            void Def(string name, Action a) { _screens[name] = a; _order.Add(name); }

            Def("title", () => ScreenOf(host => TitleScene.Mount(host, Common(new TitleCtx
            {
                HasSave = true, OnNew = () => Show("hall"), OnContinue = () => Show("hall"),
                OnSettings = () => Overlay((o, close) => SettingsOverlay.Mount(o, SettingsOf(close, false))),
            }))));
            Def("title-first", () => ScreenOf(host => TitleScene.Mount(host, Common(new TitleCtx
            {
                HasSave = false, OnNew = () => Show("hall-first"), OnContinue = () => { }, OnSettings = () => { },
            }))));
            Def("title-unreadable", () => ScreenOf(host => TitleScene.Mount(host, Common(new TitleCtx
            {
                HasSave = false, Unreadable = true, OnNew = () => Log("new over an unreadable save"), OnContinue = () => { }, OnSettings = () => { },
            }))));
            Def("hall", () => ScreenOf(host => HallScene.Mount(host, Common(new HallCtx
            {
                Lineage = _lineage,
                OnDepart = ls => { Log("depart " + ls?.Life.Name + " tools " + ls?.ToolSlots + " deckMax " + ls?.DeckMax); Show("departure"); },
                OnSettings = () => Overlay((o, close) => SettingsOverlay.Mount(o, SettingsOf(close, true, SaveQuitKinds.Lineage))),
                OnTitle = () => Show("title"),
            }))));
            Def("hall-many", () => ScreenOf(host => HallScene.Mount(host, Common(new HallCtx
            {
                Lineage = _crowded, OnDepart = _ => Show("departure"), OnSettings = () => { }, OnTitle = () => Show("title"),
            }))));
            Def("hall-first", () => ScreenOf(host => HallScene.Mount(host, Common(new HallCtx
            {
                Lineage = _first, OnDepart = _ => Show("departure"), OnSettings = () => { }, OnTitle = () => Show("title-first"),
            }))));
            Def("departure", () => ScreenOf(host => DepartureScene.Mount(host, Common(new DepartureCtx
            {
                LifeStart = _lifeStart, Owned = _owned,
                OnSetOut = c => { Log("set out " + c.Deck.Count + " cards, tools " + string.Join(",", c.BattleToolIds) + ", talent " + c.Talent); Show("ending-death"); },
                OnBack = () => Show("hall"),
            }))));
            Def("departure-app", () => ScreenOf(host => DepartureScene.Mount(host, Common(new DepartureCtx
            {
                LifeStart = Inheritance.StartLife(_first), Owned = Array.Empty<string>(),
                OnSetOut = c => Log("set out " + c.Deck.Count), OnBack = () => Show("hall-first"),
                OnSettings = () => Overlay((o, close) => SettingsOverlay.Mount(o, SettingsOf(close, true, SaveQuitKinds.Lineage))),
                OnJournal = () => Overlay((o, close) => JournalDrawer.Mount(o, Common(new JournalCtx
                {
                    Mode = global::Trial.Script.JournalCtx.Explore, Expedition = SampleExpedition(), Memo = _memo,
                    Scribbles = Array.Empty<Scribble>(), OnMemo = t => _memo = t, OnClose = close,
                }))),
            }))));
            // js: dev/meta.js:142 — the hall with the 手記 button (the router passes no onJournal; this page shows it)
            Def("hall-app", () => ScreenOf(host => HallScene.Mount(host, Common(new HallCtx
            {
                Lineage = _lineage, OnDepart = _ => Show("departure-app"),
                OnSettings = () => Overlay((o, close) => SettingsOverlay.Mount(o, SettingsOf(close, false))),
                OnJournal = () => Overlay((o, close) => JournalDrawer.Mount(o, Common(new JournalCtx
                {
                    Mode = global::Trial.Script.JournalCtx.Explore, Expedition = SampleExpedition(), Memo = _memo,
                    Scribbles = SampleScribbles, OnMemo = t => _memo = t, OnClose = close,
                }))),
            }))));
            Def("ending-death", () => ScreenOf(host => EndingScene.Mount(host, Common(new EndingCtx
            {
                Ending = Inheritance.EndingView(_afterDeath), Lineage = _afterDeath, OnHall = () => Show("hall"),
            }))));
            Def("ending-alive", () => ScreenOf(host => EndingScene.Mount(host, Common(new EndingCtx
            {
                Ending = Inheritance.EndingView(_lineage), Lineage = _lineage, OnHall = () => Show("hall"),
            }))));
            Def("ending-cleared", () =>
            {
                Lineage L = ClearedLineage();
                ScreenOf(host => EndingScene.Mount(host, Common(new EndingCtx { Ending = Inheritance.EndingView(L), Lineage = L, OnHall = () => Show("hall") })));
            });
            Def("ending-miasma", () =>
            {
                Lineage L = CrowdedLineage();
                ScreenOf(host => EndingScene.Mount(host, Common(new EndingCtx { Ending = Inheritance.EndingView(L), Lineage = L, OnHall = () => Show("hall") })));
            });
            Def("settings", () =>
            {
                _screens["hall"]();
                Overlay((o, close) => SettingsOverlay.Mount(o, SettingsOf(close, true, SaveQuitKinds.Lineage)));
            });
            Def("settings-battle", () =>
            {
                _screens["hall"]();
                Overlay((o, close) =>
                {
                    SettingsCtx c = SettingsOf(close, true);
                    c.InBattle = true;
                    return SettingsOverlay.Mount(o, c);
                });
            });
            Def("settings-nosave", () =>
            {
                _screens["title"]();
                Overlay((o, close) => SettingsOverlay.Mount(o, SettingsOf(close, false)));
            });
            Def("owned", () =>
            {
                _screens["hall"]();
                Overlay((o, close) => OwnedCards.Mount(o, Common(new OwnedCtx { Owned = _owned, Book = _book, Deck = _deckIds, OnClose = close })));
            });
            Def("owned-app", () =>
            {
                _screens["hall"]();
                Overlay((o, close) => OwnedCards.Mount(o, Common(new OwnedCtx { Expedition = SampleExpedition(), OnClose = close })));
            });
            Def("journal-explore", () =>
            {
                _screens["hall"]();
                Overlay((o, close) => JournalDrawer.Mount(o, Common(new JournalCtx
                {
                    Mode = global::Trial.Script.JournalCtx.Explore, Book = new JBook(JournalPages(), JContext.Exploration), OnClose = close,
                })));
            });
            Def("journal-enemies", () =>
            {
                _screens["hall"]();
                Overlay((o, close) => JournalDrawer.Mount(o, Common(new JournalCtx
                {
                    Mode = global::Trial.Script.JournalCtx.Battle,
                    Enemies = new[] { new global::Trial.Script.JournalFoe("armored_warden", "鉄壁の門竜") },
                    DisclosureOf = id => id == "armored_warden" ? 1 : 0,
                    HasEnemyPage = EnemyPages.Has, EnemyPage = EnemyPages.Page,
                    OnClose = close,
                })));
            });
            Def("journal-battle", () =>
            {
                _screens["hall"]();
                Overlay((o, close) => JournalDrawer.Mount(o, Common(new JournalCtx
                {
                    Mode = global::Trial.Script.JournalCtx.Battle, Pages = JournalPages(), PageId = "enemy-miasma_priest", OnClose = close,
                })));
            });
            Def("journal-app", () =>
            {
                _screens["hall"]();
                Overlay((o, close) => JournalDrawer.Mount(o, Common(new JournalCtx
                {
                    Mode = global::Trial.Script.JournalCtx.Explore, Expedition = SampleExpedition(), Lineage = _lineage, Memo = _memo,
                    Scribbles = SampleScribbles, OnMemo = t => { _memo = t; Log("memo " + t); }, PageId = "memo", OnClose = close,
                })));
            });
            Def("journal-app-battle", () =>
            {
                _screens["hall"]();
                Overlay((o, close) => JournalDrawer.Mount(o, Common(new JournalCtx
                {
                    Mode = global::Trial.Script.JournalCtx.Battle, Expedition = SampleExpedition(), Memo = _memo, Scribbles = SampleScribbles,
                    EnemyIds = new[] { "armored_warden", "polearm_warped" },
                    DisclosureOf = id => SampleExpedition()?.DisclosureOf(id) ?? 0,
                    HasEnemyPage = EnemyPages.Has, EnemyPage = EnemyPages.Page,
                    OnClose = close,
                })));
            });
            Def("parts", () => ScreenOf(host => new PartsPage(host, Common(new TitleCtx()), 0)));
            Def("backdrops", () => ScreenOf(host => new PartsPage(host, Common(new TitleCtx()), 1)));
        }

        // js: dev/meta.js:152
        /// <summary>Shows a screen by name (unknown: the title).</summary>
        public void Show(string name)
        {
            Clear();
            if (name == null || !_screens.ContainsKey(name)) name = "title";
            _shown = name;
            Log("show " + name);
            try { _screens[name](); }
            catch (Exception e) { Debug.LogException(e); }
            if (ShowName)
            {
                if (!_nameLayer)
                {
                    _nameLayer = _stage.Layer("dev-name", 9000, raycasts: false);
                    _nameLabel = Label.Make(_nameLayer, "", TextStyle.FromCss("font: 600 14px/18px var(--font-mono); color: var(--ink-muted)"), 1700f, 4f, 216f, 18f, "dev-name");
                }
                Label.SetText(_nameLabel, name + "  (F7 / F8)");
            }
        }

        // ---------------------------------------------------------------- skeletons

        /// <summary>
        /// What a skeleton mount shows until its owner's port lands: a dim sheet with the module's name (a click on it
        /// runs onClick: an overlay's close).
        /// </summary>
        internal static void Placeholder(RectTransform root, string text, Action onClick = null)
        {
            if (!root) return;
            BoxGraphic sheet = BoxGraphic.Make(root, onClick != null ? MetaStyle.ShadeCss : "background: var(--ground)", 0f, 0f, MetaUtil.W, MetaUtil.H, "placeholder");
            if (onClick != null) ClickArea.On(sheet).Click += _ => onClick();
            Label.Make(root, "SKELETON — " + text, TextStyle.FromCss("font: 600 22px/30px var(--font-mono); color: var(--ink-muted)"), 80f, 80f, 1760f, 30f, "placeholder-text");
        }

        // ---------------------------------------------------------------- the shared pieces of unit MVC

        /// <summary>The "parts" (page 0) and "backdrops" (page 1) dev pages: this unit's builders side by side.</summary>
        private sealed class PartsPage : IScene
        {
            private readonly RectTransform _root;
            private readonly SceneClock _clock;
            private readonly List<MetaBackdrop> _backdrops = new List<MetaBackdrop>();

            public PartsPage(RectTransform host, MetaCtx ctx, int page)
            {
                _root = MetaUtil.SceneRoot(host, page == 0 ? "parts" : "backdrops");
                _clock = MetaUtil.SceneClock((dt, now) => { foreach (MetaBackdrop b in _backdrops) b.Paint(now); }, ctx.Stage);
                if (page == 0) BuildParts(ctx);
                else BuildBackdrops();
            }

            private void BuildParts(MetaCtx ctx)
            {
                MetaBackdrop bd = MetaBackdrop.Create(_root, global::Trial.Script.HallBackdropSpec.For(), new MetaBackdrop.Options { Vignette = 0.5f });
                _backdrops.Add(bd);
                MetaStyle.H2(_root, "これまでの踏破者", 72f, 40f);
                MetaStyle.Keys(_root, "←→ 痕跡を選ぶ　Enter 出立　Esc 設定", 72f, 92f);
                MetaStyle.Keys(_root, "Esc 設定", 72f, 92f, 600f, right: true);
                MetaButton chip = MetaButton.Chip(_root, MetaText.Settings, "Esc", 1728f, 44f, 120f, 52f);
                chip.Click += () => Log("chip");
                MetaButton.ChipIconText(_root, "back", 20f, MetaText.BackToTitle, 1544f, 44f, 168f, 52f).Click += () => Log("to title");
                MetaButton.ChipIcon(_root, "journal", 30f, 1472f, 44f, 56f, 52f).Click += () => Log("journal");
                MetaButton start = MetaButton.Primary(_root, "出立の支度へ", "Enter", 1428f, 948f, 420f, 76f);
                start.Click += () => Log("primary");
                MetaButton off = MetaButton.Primary(_root, "出立する", "Enter", 960f, 948f, 420f, 76f);
                off.Disabled = true;
                MetaButton.Primary(_root, MetaText.Close, "Esc", 960f, 860f, null, 56f, compact: true).Click += () => Log("compact");
                MetaButton.Btn(_root, MetaText.Prototype, 72f, 948f, 200f, 50f).Click += () => Log("btn");
                MetaButton.X(_root, 300f, 948f).Click += () => Log("x");
                MetaStyle.Dot(_root, 380f, 966f);

                // washi: a sheet as the ending card is laid (padding 64 92 52, radius 3, its shadows) with its button
                WashiBox sheet = MetaStyle.Washi(_root, 72f, 160f, 600f, 360f,
                    "border-radius: 3px; box-shadow: 0 34px 80px color-mix(in srgb, var(--ground) 85%, transparent), inset 0 0 0 1px color-mix(in srgb, var(--washi-ink) 10%, transparent), inset 0 0 60px color-mix(in srgb, var(--washi-ink) 7%, transparent);");
                Label.Make(sheet.Root, "倒れた", MetaStyle.Text("font: 800 72px/1 var(--font-display); letter-spacing: .32em; color: var(--washi-ink)"), 48f, 48f);
                MetaButton wb = MetaButton.Washi(sheet.Root, "継承の間へ", "Enter");
                wb.Place(600f - 48f - wb.Node.W, 360f - 48f - wb.Node.H);
                wb.Click += () => Log("washi");

                // the meta card faces: full size, the deck grid's .72 and the owned grid's .68, plain, stance, movement
                string[] ids = { "thrust", "rock_stance", "back_leap", "kesa_cut", "brace" };
                float x = 720f;
                foreach (string id in ids)
                {
                    try
                    {
                        global::Depiction.CardFace face = MetaCardFace.FaceOf(BattleCore.CardCatalog.ById(id));
                        RectTransform slot = MetaCardFace.ScaledCard(_root, face, id == "thrust" ? 1f : 0.72f, lit: id == "kesa_cut");
                        Ui.Place(slot, x, 160f);
                        x += UiNode.Of(slot).W + 24f;
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
                try
                {
                    global::Depiction.CardFace face = MetaCardFace.FaceOf(BattleCore.CardCatalog.ById("kesa_cut"));
                    float ty = 520f;
                    foreach (TextMeshProUGUI row in MetaCardFace.TermRows(_root, face, 600f))
                    {
                        Ui.Place(row.rectTransform, 720f, ty);
                        ty += row.rectTransform.sizeDelta.y + 4f;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                RectTransform fade = MetaStyle.Fade(_root);
                global::Trial.Kit.Styler f = global::Trial.Kit.Styler.Of(fade, o: 1f);
                _clock.Tween(f, "o", 0f, 700f, Ease.Out);
            }

            private void BuildBackdrops()
            {
                // the four meta backdrops at quarter size: title (layer 7), hall (lamp lit), departure (half strength), ending (layer 3 + veil)
                (string Name, global::Backdrop.LayerBackdrop Spec, MetaBackdrop.Options Opts)[] all =
                {
                    ("title: layer 7, vignette .55, motes ×.7", MetaBackdrop.LayerBackdrop(7), new MetaBackdrop.Options { Vignette = 0.55f, MoteSpeed = 0.7f }),
                    ("hall: hallBackdropSpec, vignette .5", global::Trial.Script.HallBackdropSpec.For(), new MetaBackdrop.Options { Vignette = 0.5f }),
                    ("departure: hall at .5, vignette .6", global::Trial.Script.HallBackdropSpec.For(0.5), new MetaBackdrop.Options { Vignette = 0.6f }),
                    ("ending: layer 3, veil, vignette .75", MetaBackdrop.LayerBackdrop(3), new MetaBackdrop.Options { Veil = global::Backdrop.ExplorationBackdropSpec.For(3).Veil, Vignette = 0.75f, MoteSpeed = 0.35f }),
                };
                for (int i = 0; i < all.Length; i++)
                {
                    RectTransform cell = Ui.Div(_root, "cell " + i, (i % 2) * 960f, (i / 2) * 540f, 1920f, 1080f);
                    UiNode n = UiNode.Of(cell);
                    n.SetOrigin(0f, 0f);
                    n.SetStyleTransform(sx: 0.5f, sy: 0.5f);
                    _backdrops.Add(MetaBackdrop.Create(cell, all[i].Spec, all[i].Opts));
                    Label.Make(_root, all[i].Name, TextStyle.FromCss("font: 600 16px/20px var(--font-mono); color: var(--ink)"), (i % 2) * 960f + 16f, (i / 2) * 540f + 12f);
                }
            }

            public void Destroy()
            {
                _clock.Stop();
                foreach (MetaBackdrop b in _backdrops) b.Destroy();
                Ui.Remove(_root);
            }

            public void Refresh() { }
        }
    }
}
