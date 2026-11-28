// The hud module of the battle scene (docs/BATTLE_SCENE.md §6c): everything on the screen that is neither a card in hand
// nor the arena — the header band, the number panels, the omen badges (+ plans), the stance lists, the stamina tag, the
// piles + list, the played attributes, the end-turn plate, the banners and the result card — and the panel half of
// FollowFigures (follow). Port of scenes/battle/hud.js (Unity: StatusBarView, OmenBadgeView, StanceListView,
// StaminaTagView, PilesView, PileListView, BottomRightViews, the end-turn plate, Banner, ShowResult).
//
// The shared header band (#1074, Trial.Header's HeaderBar) takes the place of the JS's corner tag, speed switch, journal
// button and gear: the layer and the turn (and the fight of a run of fights) on the left, then the calendar, the hero's HP
// and stamina; the speed, the owned cards, the journal and the settings on the right. What it shows comes from the frame,
// the panels' own calls and the app (HudHeader); its words and boxes are Trial.Bridge's HeaderContent / HeaderBand.
//
// Parts: HeaderBar (Trial.Header), HudStatusPanel (Status.cs), HudOmenBadge (Omen.cs), HudStanceLists (Stances.cs),
// HudStaminaTag (Stamina.cs), HudPiles (Piles.cs), HudPlayed / HudEndTurn (Bottom.cs), HudResultCard / HudChainBanner /
// HudLossInk / HudChrome.PlayBanner (Chrome.cs), HudUtil / HudTip.
// Fills env.Geom.PileMiddle / OmenBox / OmenShown / OmenBoxes / EndTurnBox / StatusBox / StatusBoxes / StanceBox /
// StanceSlot / CoversPointer / DrawerRight. Plays 'uiClick' for its buttons.
//
// It computes no rule. Every number and word comes from the frame and the cues the player hands over and from the ported
// texts; every box from the ported layout scripts (StatusPanelLayout, StanceList, FloorSpec, FigureFrame, HeaderBand) or
// L. Every length runs on env.Clock with the nominal ms handed in.
//
// Several enemies (scene addition; Unity draws Enemies[0] only): when the frame carries more than one enemy
// (#589's DepictionFrame.Enemies: each one's EnemyFrame), each standing enemy gets its own number panel and omen badge,
// under and over its cells. Status(Enemy) and Omen are the frame's enemy (Frame.Enemy, the first standing in the line);
// Status(Enemy, unit) and OmenOf(unit) reach any one of them (enemy k is DepictionFrame.Enemies[k]).
//
// Unity differences kept to the JS: the window / root listeners (pointermove, pointerdown capture, contextmenu) are routed
// by the scene (BattleInput) to OnPointerMove / OnPinDown / OnPinContext. The omen flight's copy of the badge
// (face.cloneNode(true)) is a frozen snapshot of the badge's hierarchy (CloneFace below): uGUI has no cloneNode for the
// kit's vector graphics, whose shapes live in unserialized state.
using System;
using System.Collections.Generic;
using System.Reflection;
using Depiction;
using Trial.Bridge;
using Trial.Geometry;
using Trial.Header;
using Trial.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace Trial.Battle
{
    /// <summary>
    /// What the battle's header band shows that the frame does not carry (#1074): the app's, through BattleSceneCtx.
    /// </summary>
    public sealed class HudHeader
    {
        /// <summary>The place's name (ExplorationScreen.LayerTitle); empty: 「第 n 層」 of the scene's layer.</summary>
        public string LayerTitle = "";

        /// <summary>
        /// The collapse calendar; null when the app hands none (a fight outside an exploration), and the band leaves the
        /// calendar out rather than read 「残り 0 日」, which would say the collapse has come.
        /// </summary>
        public HeaderCalendar Calendar;

        /// <summary>The deck's count on the owned cards' corner.</summary>
        public int DeckCount;
    }

    // js: scenes/battle/hud.js:105 (createHud)
    public sealed class Hud : IHud
    {
        // ---- constants (hud.js:38-103) ---------------------------------------------------------------------

        /// <summary>EDGE — DepictionPlayer.KeepX: the parts tied to the enemy keep 24 px inside the screen's edges.</summary>
        private const float Edge = 24f; // js: scenes/battle/hud.js:39
        /// <summary>MIN_GAP — StatusPanelLayout.Place pushes two panels apart to this gap; several badges keep the same.</summary>
        private const float MinGap = StatusPanelLayout.MinGap; // js: scenes/battle/hud.js:41
        /// <summary>SNAP_SCALE — above this scene-clock factor (fast-forward, 演出短縮) the panels are put on their places at once.</summary>
        private const float SnapScale = 50f; // js: scenes/battle/hud.js:43
        /// <summary>OMEN_LIFT_MS — the badge's lift for a prediction tag (DS tag.lift) eases in and out over this (ReceiverShow's length).</summary>
        private const float OmenLiftMs = 120f; // js: scenes/battle/hud.js:45
        /// <summary>
        /// BADGE_SLIDE_MAX / BADGE_SLIDE_MS — battle_ui_ux_v2 §5.8 (several enemies): badges that would overlap slide apart,
        /// 「最大 75 px ずつ、200 ms、out」, and their ▼ stays on the head. The slide is eased toward its place with a 200 ms
        /// out-like curve (exponential, time constant 200 / 3), on the scene's speed.
        /// </summary>
        private const float BadgeSlideMax = 75f; // js: scenes/battle/hud.js:51
        private const float BadgeSlideMs = 200f; // js: scenes/battle/hud.js:52
        /// <summary>
        /// DRAWER_CLEAR — review fix (the pile list's drawer covered the left 36 px of the hero's panel): while the drawer is
        /// open, the panels keep this far right of its edge (DS README 余白 24).
        /// </summary>
        private const float DrawerClear = 24f; // js: scenes/battle/hud.js:57
        /// <summary>FALLEN_GAP / FALLEN_SLIDE — a fallen enemy's grey panel keeps this far off its neighbours, sliding at most this far.</summary>
        private const float FallenGap = 4f; // js: scenes/battle/hud.js:90
        private const float FallenSlide = 24f; // js: scenes/battle/hud.js:91
        /// <summary>FALLEN_KEEP_UNDER — a standing panel counts as under its figure while the figure's x stays this far inside its edges (a quarter of a narrow one).</summary>
        private const float FallenKeepUnder = 24f; // js: scenes/battle/hud.js:93

        // ---- one enemy's views ---------------------------------------------------------------------------------

        /// <summary>One enemy's views: { unit, status, omen, frame, shown, disclosure, lift, tagged, badgeOff } (hud.js:131 enemyView).</summary>
        private sealed class EnemyView
        {
            public int Unit;
            public HudStatusPanel Status;
            public HudOmenBadge Omen;
            public UnitFrame Frame;
            /// <summary>Its EnemyFrame (#589: its cells); null for the one enemy of a frame that carries no list.</summary>
            public EnemyFrame Share;
            public bool Shown = true;
            /// <summary>v.disclosure: the 開示度 its badge last showed (null before the first omen).</summary>
            public int? Disclosure;
            /// <summary>v.lift.y: the badge's lift for a prediction tag now.</summary>
            public float Lift;
            /// <summary>v.tagged: its frame shows the held card's prediction tag (liftOmen).</summary>
            public bool Tagged;
            /// <summary>v.badgeOff: the badge's slide off its home now (null before the first follow).</summary>
            public float? BadgeOff;
        }

        /// <summary>enemiesOf's row: { unit, unitFrame, omenFrame }.</summary>
        private readonly struct EnemyEntry
        {
            public readonly int Unit;
            public readonly UnitFrame UnitFrame;
            public readonly OmenFrame OmenFrame;
            /// <summary>The enemy's EnemyFrame (#589); null for the one enemy of a frame that carries no list.</summary>
            public readonly EnemyFrame Share;

            public EnemyEntry(int unit, UnitFrame unitFrame, OmenFrame omenFrame, EnemyFrame share)
            {
                Unit = unit;
                UnitFrame = unitFrame;
                OmenFrame = omenFrame;
                Share = share;
            }
        }

        /// <summary>follow's row: { v, x, top, w, down, walking }.</summary>
        private sealed class FollowRow
        {
            public EnemyView V;
            public float X;
            public float Top;
            public float W;
            public bool Down;
            public bool Walking;
        }

        /// <summary>spreadRow's item: { x, w } (+ the panel it places, and whether it must stay under its figure).</summary>
        private struct RowItem
        {
            public float X;
            public float W;
            public HudStatusPanel Panel;
            public bool Keep;
        }

        /// <summary>A panel's span on the row: { left, right }.</summary>
        private readonly struct Span
        {
            public readonly float Left;
            public readonly float Right;

            public Span(float left, float right)
            {
                Left = left;
                Right = right;
            }
        }

        /// <summary>hud.omen: the badge of the frame's enemy (a forwarder, so the player's calls reach whoever leads).</summary>
        // js: scenes/battle/hud.js:516
        private sealed class OmenForwarder : IOmenFacade
        {
            private readonly Hud _hud;
            public OmenForwarder(Hud hud) { _hud = hud; }

            // js: scenes/battle/hud.js:517
            public void Bind(OmenFrame o)
            {
                if (o == null) return;
                // The omen's enemy: the share of the last frame that holds it (#589: DepictionFrame.Enemies[k].Omen).
                DepictionFrame last = _hud._lastFrame;
                int unit = last != null && last.Enemies != null ? last.Enemies.FindIndex(e => e != null && e.Omen == o) : -1;
                EnemyView target = _hud._multi && unit >= 0 && _hud._enemies.ContainsKey(unit) ? _hud._enemies[unit] : _hud.PrimaryView();
                target.Omen.Bind(o);
            }

            // js: scenes/battle/hud.js:522
            public Promise PopIn(float ms) => _hud.PrimaryView().Omen.PopIn(ms);
            // js: scenes/battle/hud.js:523
            public Promise Blink(float ms) => _hud.PrimaryView().Omen.Blink(ms);
            // js: scenes/battle/hud.js:524
            public Promise Flare(float ms) => _hud.PrimaryView().Omen.Flare(ms);

            // js: scenes/battle/hud.js:525
            public void SetActing(bool on)
            {
                // The light goes out on every badge (an event other than the enemy's action), and comes on the leader's.
                if (!on) foreach (EnemyView v in _hud._order) v.Omen.SetActing(false);
                else _hud.PrimaryView().Omen.SetActing(true);
            }

            // js: scenes/battle/hud.js:530
            public Promise FadeOut(float ms) => _hud.PrimaryView().Omen.FadeOut(ms);
            // js: scenes/battle/hud.js:531
            public void HideNow() => _hud.PrimaryView().Omen.HideNow();
            // js: scenes/battle/hud.js:532
            public Promise FadeOutAll(float ms) => _hud.PrimaryView().Omen.FadeOutAll(ms);
            // js: scenes/battle/hud.js:533
            public void HideAllNow() => _hud.PrimaryView().Omen.HideAllNow();
            // js: scenes/battle/hud.js:534
            public Promise StrikeSide(float ms) => _hud.PrimaryView().Omen.StrikeSide(ms);
            // js: scenes/battle/hud.js:535
            public void StrikeSideNow() => _hud.PrimaryView().Omen.StrikeSideNow();
        }

        // ---- state -----------------------------------------------------------------------------------------------

        private readonly BattleViewEnv _env;
        private readonly HudOpts _opts;
        private readonly HudTip _tip;
        private readonly HudStanceLists _stances;
        private readonly HudStaminaTag _stamina;
        private readonly HudStatusPanel _player;
        /// <summary>unit → its views (the JS Map); <see cref="_order"/> keeps the Map's insertion order for every walk.</summary>
        private readonly Dictionary<int, EnemyView> _enemies = new Dictionary<int, EnemyView>();
        private readonly List<EnemyView> _order = new List<EnemyView>();
        private readonly HudPiles _piles;
        private readonly HudPlayed _played;
        private readonly HudEndTurn _endTurn;
        private readonly HeaderBar _header;
        private readonly HudPlayerPanel _playerFacade;
        private readonly HudResultCard _result;
        private readonly HudLossInk _lossInk;
        private readonly HudChainBanner _chainBanner;
        private readonly OmenForwarder _omen;
        private readonly Action _offFrame;
        /// <summary>The end of the battle's scrim (.ocbg-battle-endscrim), the first child of layer.over.</summary>
        private RectTransform _scrim;
        /// <summary>Badges in flight to the stance list (removed on destroy).</summary>
        private readonly HashSet<RectTransform> _flights = new HashSet<RectTransform>();
        /// <summary>A walking figure's panel: the offset it had from its figure as the walk began, closing as it walks (follow).</summary>
        private readonly Dictionary<HudStatusPanel, float> _carriedOff = new Dictionary<HudStatusPanel, float>();

        /// <summary>The header's turn and fight tags as last shown (they change once a turn).</summary>
        private string _tagKey;
        /// <summary>The journal's button is lit: the journal is open (JournalEdge stands).</summary>
        private bool _journalLit;
        /// <summary>The battle is over: the journal's button stays off, as the end-turn plate does.</summary>
        private bool _ended;
        /// <summary>The speed and the settings take a press (SetSpeedVisible): off once the battle is over.</summary>
        private bool _switchesOn = true;
        private int _primary;
        private bool _multi;
        private float _dtReal = 16f;
        private bool _snapNext = true;
        private DepictionFrame _lastFrame;
        /// <summary>lastWidths: the row's (unit, width) pairs of the last follow (the JS joins them into a string).</summary>
        private readonly List<(int Unit, float W)> _lastWidths = new List<(int Unit, float W)>();

        // follow's per-frame lists, kept between frames (the JS builds them anew each frame; nothing keeps them past the call)
        private readonly Dictionary<int, FollowUnit> _fByUnit = new Dictionary<int, FollowUnit>();
        private readonly List<FollowRow> _fRowPool = new List<FollowRow>();
        private readonly List<FollowRow> _fRows = new List<FollowRow>();
        private readonly List<FollowRow> _fStanding = new List<FollowRow>();
        private readonly List<FollowRow> _fFallen = new List<FollowRow>();
        private readonly List<RowItem> _fItems = new List<RowItem>();
        private readonly List<float> _fLefts = new List<float>();
        private readonly List<float> _fSpread = new List<float>();
        private readonly List<Span> _fTaken = new List<Span>();
        private readonly HashSet<HudStatusPanel> _fCarried = new HashSet<HudStatusPanel>();
        /// <summary>follow's targets (a Map: panel → left, in insertion order).</summary>
        private readonly List<HudStatusPanel> _tPanels = new List<HudStatusPanel>();
        private readonly List<float> _tLefts = new List<float>();
        /// <summary>hoverUnit: the enemy whose badge the pointer is over (−1 = JS null).</summary>
        private int _hoverUnit = -1;

        // js: scenes/battle/hud.js:105
        private Hud(BattleViewEnv env, HudOpts opts, HudHeader header)
        {
            _env = env;
            _opts = opts ?? new HudOpts();
            HudHeader h = header ?? new HudHeader();
            _tip = HudUtil.CreateTip(env);

            // ---- the header band: the layer and the turn, the calendar, the hero's HP and stamina, the battle's switches ----
            // Its empty parts let the pointer through to what stands under them (#1074); its bubbles open over the battle.
            _header = HeaderBar.Create(env.Layer.Hud,
                HeaderContent.ForBattle(string.IsNullOrEmpty(h.LayerTitle) ? HudChrome.LayerWord(env.LayerNo) : h.LayerTitle,
                    0, _opts.ChainIndex, _opts.ChainTotal, h.Calendar, 0, 0, 0, 0, h.DeckCount, SpeedStepNow(),
                    canOpenSettings: _opts.SettingsInBattle),
                new HeaderActions
                {
                    OnSpeed = () => _opts.OnSpeed?.Invoke(),
                    OnJournal = () => _opts.OnJournal?.Invoke(),
                    OnSettings = () =>
                    {
                        _env.Audio.Play("uiClick");
                        _opts.OnSettings?.Invoke();
                    },
                },
                tipLayer: env.Layer.Over, blockPointer: false);
            foreach (RectTransform rt in _header.PointerTargets) BattleHitTag.Mark(rt);

            // ---- the parts, bottom → top inside their layers ----
            // Review 21: the stance lists' high place (stances.js keepClear) — under the header band.
            // js: scenes/battle/hud.js:113
            _stances = HudStanceLists.Create(env, _tip);
            _stamina = HudStaminaTag.Create(env);
            _player = HudStatusPanel.Create(env, UnitSide.Player, new StatusPanelDeps
            {
                Tip = _tip,
                OnStamina = (s, max) =>
                {
                    _stamina.SetStamina(s, max);
                    _header.SetStamina(s, max);
                },
                OnPip = (index, ms) => _stamina.LightPip(index),
                OnStaminaPop = ms => _stamina.PopNumber(ms),
            });
            _playerFacade = new HudPlayerPanel(_player, _header);
            EnemyViewOf(0);

            _piles = HudPiles.Create(env, new PilesOpts
            {
                Tip = _tip,
                OnToggle = kind =>
                {
                    _tip.Hide();
                    _env.Audio.Play("uiClick");
                    _piles.Toggle(kind);
                },
                // The list opened or closed (battle_ui_ux_v2 §2.4: the figures slide right while it is open).
                OnDrawer = open => _opts.OnDrawer?.Invoke(open),
            });
            _played = HudPlayed.Create(env);
            _endTurn = HudEndTurn.Create(env, () => _opts.OnEndTurn?.Invoke());
            _result = HudResultCard.Create(env);
            _lossInk = HudLossInk.Create(env);
            _chainBanner = HudChainBanner.Create(env);

            // ---- follow the figures: the frame's real time ----
            // js: scenes/battle/hud.js:268 (clock.onFrame((dtScene, real) => { dtReal = real || 16; }))
            // The journal opens and closes by its button, J and its own close, all outside the hud, so whether it stands is
            // read here; the frame walk runs while the journal holds the scene clock, too.
            _offFrame = env.Clock.OnFrame((dtScene, real) =>
            {
                _dtReal = real > 0f ? real : 16f;
                SyncJournalButton();
            });

            // ---- the end of the battle: the field settles under the result card (review fix) ----
            // js: scenes/battle/hud.js:465
            BuildScrim();

            _omen = new OmenForwarder(this);
            RegisterGeom();
        }

        /// <summary>createHud(env, opts), with what the header band shows from the app.</summary>
        // js: scenes/battle/hud.js:105
        public static Hud Create(BattleViewEnv env, HudOpts opts, HudHeader header = null) => new Hud(env, opts, header);

        // ---- the header band ---------------------------------------------------------------------------------------

        private BattleSpeedStep SpeedStepNow() => BattleClock.StepOfSpeed(_env.Settings.Speed);

        /// <summary>The turn (the frame's) and the fight of a run of fights: the app's, else the frame's, as the JS corner read them.</summary>
        private void ShowTurn(CornerFrame corner)
        {
            CornerFrame c = corner ?? new CornerFrame();
            bool app = _opts.ChainTotal != 0;
            int index = app ? _opts.ChainIndex : c.ChainIndex;
            int total = app ? _opts.ChainTotal : c.ChainTotal;
            string key = c.Turn + ":" + index + "/" + total;
            if (key == _tagKey) return;
            _tagKey = key;
            _header.SetTags(HeaderBand.BattleTags(c.Turn, index, total));
        }

        private void SyncJournalButton()
        {
            bool open = _env.Geom.JournalEdge?.Invoke() != null;
            if (open == _journalLit) return;
            _journalLit = open;
            ShowJournalButton();
        }

        private void ShowJournalButton() =>
            _header.SetButton(HeaderButtonKind.Journal,
                _journalLit ? HeaderButtonState.On : _ended ? HeaderButtonState.Off : HeaderButtonState.Ready);

        /// <summary>
        /// The speed and the settings as the battle allows them, off once it is over. They stay standing, dimmed, so the
        /// buttons beside them keep their places under the result card.
        /// </summary>
        private void ShowSwitches()
        {
            _header.SetButton(HeaderButtonKind.Speed, _switchesOn ? HeaderButtonState.Ready : HeaderButtonState.Off);
            _header.SetButton(HeaderButtonKind.Settings,
                _switchesOn && _opts.SettingsInBattle ? HeaderButtonState.Ready : HeaderButtonState.Off);
        }

        // ---- row helpers (hud.js:62-103) ---------------------------------------------------------------------------

        /// <summary>
        /// Lays several boxes on one row (scene addition, for two or three enemies): each centred on its x, kept inside the
        /// edges, then pushed apart evenly where two would come closer than MIN_GAP — the StatusPanelLayout.Place rule widened
        /// to any count. `items` are { x, w }, sorted left to right.
        /// </summary>
        // js: scenes/battle/hud.js:67
        private static void SpreadRow(IReadOnlyList<RowItem> items, float lo, float hi, List<float> left)
        {
            int n = items.Count;
            left.Clear();
            for (int i = 0; i < n; i++) left.Add(HudUtil.Clamp(items[i].X - items[i].W / 2f, lo, hi - items[i].W));
            for (int pass = 0; pass < 24; pass++)
            {
                bool moved = false;
                for (int i = 1; i < n; i++)
                {
                    float shortBy = left[i - 1] + items[i - 1].W + MinGap - left[i];
                    if (shortBy <= 0.01f) continue;
                    left[i - 1] -= shortBy / 2f;
                    left[i] += shortBy / 2f;
                    moved = true;
                }
                for (int i = 0; i < n; i++) left[i] = HudUtil.Clamp(left[i], lo, hi - items[i].W);
                if (!moved) break;
            }
        }

        /// <summary>
        /// Where a fallen enemy's grey panel stands (review fix: with the MIN_GAP padding a neighbour that merely touched it
        /// hid it, so on adjacent cells a defeated enemy left no trace): under its emptied cell at `left`, or slid at most
        /// FALLEN_SLIDE toward the free side to keep FALLEN_GAP off the panels already placed (`taken`); null — it gives its
        /// place up — only when a panel actually stands over its cell.
        /// </summary>
        // js: scenes/battle/hud.js:94
        private static float? FallenPlace(float left, float w, List<Span> taken, float lo, float hi)
        {
            if (FallenClear(left, w, taken, lo, hi)) return left;
            // options: each placed panel's right side, then its left side, in `taken`'s order; the best is the one nearest
            // `left` (the stable sort's first: the earliest of equally near ones).
            float? best = null;
            float bestD = float.PositiveInfinity;
            foreach (Span t in taken)
            {
                for (int side = 0; side < 2; side++)
                {
                    float l = side == 0 ? t.Right + FallenGap : t.Left - FallenGap - w;
                    float d = Math.Abs(l - left);
                    if (!(d <= FallenSlide) || !FallenClear(l, w, taken, lo, hi)) continue;
                    if (d < bestD)
                    {
                        bestD = d;
                        best = l;
                    }
                }
            }
            return best;
        }

        /// <summary>fallenPlace's clear(l): inside the row's edges and FALLEN_GAP off every panel already placed.</summary>
        // js: scenes/battle/hud.js:95
        private static bool FallenClear(float l, float w, List<Span> taken, float lo, float hi)
        {
            if (!(l >= lo - 0.5f && l + w <= hi + 0.5f)) return false;
            foreach (Span t in taken)
                if (l < t.Right + FallenGap && l + w > t.Left - FallenGap) return false;
            return true;
        }

        /// <summary>Array.prototype.sort (stable, as the JS engines' TimSort): an insertion sort for the few rows the hud has.</summary>
        private static void StableSort<T>(List<T> list, Comparison<T> cmp)
        {
            for (int i = 1; i < list.Count; i++)
            {
                T item = list[i];
                int j = i - 1;
                while (j >= 0 && cmp(list[j], item) > 0)
                {
                    list[j + 1] = list[j];
                    j--;
                }
                list[j + 1] = item;
            }
        }

        // ---- the enemies' views ------------------------------------------------------------------------------------

        // js: scenes/battle/hud.js:131
        private EnemyView EnemyViewOf(int unit)
        {
            if (_enemies.TryGetValue(unit, out EnemyView v)) return v;
            v = new EnemyView
            {
                Unit = unit,
                Status = HudStatusPanel.Create(_env, UnitSide.Enemy, new StatusPanelDeps { Tip = _tip }),
                Omen = HudOmenBadge.Create(_env),
            };
            // (the JS marks both elements with data-unit for its tests; nothing reads it on screen)
            // The boss colour's double frame belongs to the bosses' own words and statuses (battle-visual-v1 §2.1);
            // the panel keeps the rule frame, or the elite's phosphor one (StatusBarView.SetElite).
            _enemies[unit] = v;
            _order.Add(v);
            return v;
        }

        // js: scenes/battle/hud.js:144
        private EnemyView PrimaryView() => _enemies.TryGetValue(_primary, out EnemyView v) ? v : EnemyViewOf(_primary);

        // ---- frame → hud -------------------------------------------------------------------------------------------

        /// <summary>The enemies the frame describes: every one when it carries the list, else its one enemy.</summary>
        // js: scenes/battle/hud.js:176
        private static List<EnemyEntry> EnemiesOf(DepictionFrame f)
        {
            if (f.Enemies == null || f.Enemies.Count == 0) return new List<EnemyEntry> { new EnemyEntry(0, f.Enemy, f.Omen, null) };
            var rows = new List<EnemyEntry>(f.Enemies.Count);
            for (int k = 0; k < f.Enemies.Count; k++)
            {
                EnemyFrame e = f.Enemies[k];
                rows.Add(new EnemyEntry(k, e?.Unit, e?.Omen, e));
            }
            return rows;
        }

        /// <summary>
        /// battle-visual-v1 §4.4〜§4.6: the battle has two or more enemies, so each one's HP row, omen plate and stance frame
        /// carry its stripe direction's swatch (#589's EnemyFrame.Swatch, the same on every enemy of the battle).
        /// </summary>
        private static bool SwatchesOf(DepictionFrame f) => f != null && f.Enemies != null && f.Enemies.Count > 0 && f.Enemies[0] != null && f.Enemies[0].Swatch;

        /// <summary>
        /// The way enemy `e`'s move omen goes on screen (battle-visual-v1 §4.4): its destination cells as the frame's floor
        /// gives them (FloorFrame.MoveCells, the violet dashed cells) against the cell it stands on. 0 when the frame shows none.
        /// </summary>
        // js: scenes/battle/hud.js:191
        private static int MoveDirOf(DepictionFrame f, EnemyEntry e)
        {
            FloorFrame fl = f.Floor;
            if (fl == null) return 0;
            List<int> cells = e.Share != null ? e.Share.MoveCells : fl.MoveCells;
            int cell = e.Share != null && e.Share.Cell > 0 ? e.Share.Cell : fl.EnemyCell;
            if (cells == null || cells.Count == 0 || !(cell > 0)) return 0;
            int min = int.MaxValue;
            foreach (int c in cells) if (c < min) min = c;
            return min < cell ? -1 : 1;
        }

        /// <summary>Whether enemy `e`'s shown omen hatches cells on the frame's floor (EnemyFrame.AimCells / FloorFrame.AimCells).</summary>
        // js: scenes/battle/hud.js:205
        private static bool AimsOf(DepictionFrame f, EnemyEntry e)
        {
            FloorFrame fl = f.Floor;
            if (fl == null) return false;
            if (e.Share != null)
            {
                return !(e.UnitFrame != null && e.UnitFrame.Down) && e.Share.AimCells != null && e.Share.AimCells.Count > 0;
            }
            return fl.AimCells != null && fl.AimCells.Count > 0;
        }

        // js: scenes/battle/hud.js:215
        public void ApplyFrame(DepictionFrame frame, bool first = false, int keepOmen = -1)
        {
            if (frame == null) return;
            DepictionFrame f = frame;
            ShowTurn(f.Corner);
            // StaminaTagView: the divider and the dashed dots before the bind below shows the stamina.
            _stamina.SetDivider(f.StanceDivider);
            _stamina.SetNextRecovery(f.Player != null ? f.Player.NextRecovery : 0);
            _player.Bind(f.Player);
            // The bind's stamina reaches the band through the panel's OnStamina; its HP, and the max the events' calls keep, here.
            if (f.Player != null) _playerFacade.Bound(f.Player);
            List<EnemyEntry> list = EnemiesOf(f);
            _multi = list.Count > 1;
            int focus = f.Enemies != null && f.Enemy != null ? f.Enemies.FindIndex(x => x != null && x.Unit == f.Enemy) : -1;
            _primary = focus >= 0 ? focus : list.Count > 0 ? list[0].Unit : 0;
            bool swatches = SwatchesOf(f);
            var listed = new HashSet<int>();
            foreach (EnemyEntry e in list)
            {
                EnemyView v = EnemyViewOf(e.Unit);
                listed.Add(e.Unit);
                v.Frame = e.UnitFrame;
                v.Share = e.Share;
                if (e.UnitFrame != null) v.Status.Bind(e.UnitFrame);
                v.Omen.SetMoveDir(MoveDirOf(f, e));
                // §4.4 (several enemies): the badge's swatch ties it to its enemy's red cells on the floor, while it has some.
                v.Omen.SetSwatchShown(swatches, AimsOf(f, e), e.Share != null ? e.Share.StripeDirection : 0);
                // Review 21: the badge of an action held across its events keeps what it shows (player.applyFrame's keepOmen).
                bool keep = keepOmen >= 0 && keepOmen == e.Unit && v.Omen.Visible;
                if (e.OmenFrame != null && !keep)
                {
                    v.Omen.Bind(e.OmenFrame);
                    // scene addition: a 開示度 that rose mid-battle shows itself once (the writer re-blanks the frame)
                    int d = e.OmenFrame.Disclosure;
                    if (!first && v.Disclosure != null && d > v.Disclosure.Value) v.Omen.Reveal(_env.Fx.Ms(EffectId.OmenShow)).Forget();
                    v.Disclosure = d;
                }
                // battle_ui_ux_v2 §2.2.6: a fallen enemy's panel stays under its emptied cell, grey.
                v.Shown = true;
                v.Status.SetDown(e.UnitFrame != null && e.UnitFrame.Down);
                v.Status.SetSolid(_multi);
                v.Status.SetSwatch(swatches && e.Share != null ? BattleLayout.HatchAngle(e.Share.StripeDirection) : (float?)null);
            }
            foreach (EnemyView v in _order)
            {
                if (!listed.Contains(v.Unit)) v.Shown = false;
                v.Status.Show(v.Shown);
                v.Omen.Show(v.Shown);
            }
            _stances.Bind(f, first);
            _lastFrame = f;
            _piles.Bind(f);
            _played.Bind(f.Played, !first);
            _endTurn.SetEnemyTurn(f.EnemyTurn);
            if (first) _snapNext = true;
        }

        // ---- follow the figures (FollowFigures / FollowPanels / KeepX / KeepBottom) ------------------------------------

        // js: scenes/battle/hud.js:273
        public void Follow(FollowArgs o)
        {
            if (o == null) return;
            int cells = o.Cells;
            // o.units (the player's, from each enemy's figure): [{ unit, x, top, size }]; else the frame's cells.
            Dictionary<int, FollowUnit> byUnit = _fByUnit;
            byUnit.Clear();
            if (o.Units != null)
                foreach (FollowUnit u in o.Units)
                    if (u != null) byUnit[u.Unit] = u;
            List<FollowRow> rows = _fRows;
            rows.Clear();
            foreach (EnemyView v in _order)
            {
                if (!v.Shown) continue;
                bool isPrimary = v.Unit == _primary;
                bool down = v.Frame != null && v.Frame.Down;
                FollowRow row = RowOf(rows.Count);
                row.V = v;
                row.Down = down;
                if (byUnit.TryGetValue(v.Unit, out FollowUnit fig))
                {
                    row.X = fig.X;
                    row.Top = fig.Top;
                    row.W = StatusPanelLayout.EnemyPanelWidth(cells, Math.Max(1, fig.Size));
                    row.Walking = fig.Walking;
                }
                else
                {
                    int size = isPrimary ? Math.Max(1, o.EnemySize) : Math.Max(1, v.Share != null ? v.Share.Size : 0);
                    row.X = isPrimary || cells <= 0 || v.Share == null ? o.EnemyX : FloorSpec.FeetX(cells, v.Share.Cell, size);
                    row.Top = isPrimary ? o.EnemyTop : L.FeetY - FigureFrame.HeightOf(v.Frame != null ? v.Frame.ArtId : null, size);
                    row.W = StatusPanelLayout.EnemyPanelWidth(cells, size);
                    row.Walking = false;
                }
                rows.Add(row);
            }
            StableSort(rows, (a, b) => a.X.CompareTo(b.X));
            // battle-visual-v1 §4.5 (review fix): the panels say whose number it is by position alone. A fallen enemy's
            // grey panel (§2.2.6) takes no part in the row's spread, so it never pushes a standing enemy's panel off its
            // figure; it stands under its emptied cell, and gives the place up (hidden) while a standing enemy's panel or the
            // hero's would overlap it — an enemy that stepped into the fallen one's cell. One enemy: as Unity.
            List<FollowRow> standing = rows;
            List<FollowRow> fallen = _fFallen;
            fallen.Clear();
            if (rows.Count > 1)
            {
                standing = _fStanding;
                standing.Clear();
                foreach (FollowRow r in rows)
                {
                    if (r.Down) fallen.Add(r);
                    else standing.Add(r);
                }
            }

            // A new line (other widths) puts the panels straight on their places, as a battle's first frame does.
            // (JS: rows.map((r) => r.v.unit + ':' + r.w).join(',') against the last one; compared here pair by pair.)
            bool sameWidths = _lastWidths.Count == rows.Count;
            for (int i = 0; i < rows.Count && sameWidths; i++)
                sameWidths = _lastWidths[i].Unit == rows[i].V.Unit && _lastWidths[i].W == rows[i].W;
            bool snap = o.Snap || _snapNext || !sameWidths || _env.Clock.Scale > SnapScale;
            _lastWidths.Clear();
            foreach (FollowRow r in rows) _lastWidths.Add((r.V.Unit, r.W));
            _snapNext = false;
            foreach (FollowRow r in rows) r.V.Status.SetWidth(r.W);
            _player.SetWidth(StatusPanelLayout.PlayerWidth);

            // The pile list's drawer (left) covers its share of the screen: the row keeps DRAWER_CLEAR right of it. Review 19: the
            // journal's (right) the same — the row and the badges keep DRAWER_CLEAR left of its edge (with the trio on 8 cells
            // the third enemy's panel and badge stood under it).
            float? journalEdge = _env.Geom.JournalEdge?.Invoke();
            float lo = Math.Max(StatusPanelLayout.EdgeInset, _piles.Open ? _piles.DrawerRight() + DrawerClear : 0f);
            float hi = journalEdge != null ? Math.Min(L.W - StatusPanelLayout.EdgeInset, journalEdge.Value - DrawerClear) : L.W - StatusPanelLayout.EdgeInset;
            float badgeLo = Math.Max(Edge, _piles.Open ? _piles.DrawerRight() + DrawerClear : 0f);
            float badgeHi = journalEdge != null ? Math.Min(L.W - Edge, journalEdge.Value - DrawerClear) : L.W - Edge;

            // StatusPanelLayout.Place for one enemy; the same rule spread over the row for several.
            _tPanels.Clear();
            _tLefts.Clear();
            List<RowItem> items = _fItems;
            List<float> lefts = _fLefts;
            if (standing.Count == 1)
            {
                StatusPanelLayout.Place(o.PlayerX, StatusPanelLayout.PlayerWidth, standing[0].X, standing[0].W, out float pPlayerLeft, out float pEnemyLeft);
                float playerLeft = Math.Max(pPlayerLeft, lo);
                SetTarget(_player, playerLeft);
                SetTarget(standing[0].V.Status, playerLeft == pPlayerLeft ? pEnemyLeft : Math.Max(pEnemyLeft, playerLeft + StatusPanelLayout.PlayerWidth + MinGap));
            }
            else if (standing.Count > 1)
            {
                items.Clear();
                items.Add(new RowItem { X = o.PlayerX, W = StatusPanelLayout.PlayerWidth, Panel = _player });
                foreach (FollowRow r in standing) items.Add(new RowItem { X = r.X, W = r.W, Panel = r.V.Status });
                StableSort(items, (a, b) => a.X.CompareTo(b.X));
                SpreadRow(items, lo, hi, lefts);
                for (int i = 0; i < items.Count; i++) SetTarget(items[i].Panel, lefts[i]);
            }
            else
            {
                SetTarget(_player, HudUtil.Clamp(o.PlayerX - StatusPanelLayout.PlayerWidth / 2f, lo, hi - StatusPanelLayout.PlayerWidth));
            }
            // Review 17 (one enemy fell while another stood: the hero's 300 px panel slid back under the hero, into the fallen
            // hound's place, and the grey panel gave its place up for the rest of the fight though nobody stood on its cell): the
            // row is first laid out with the grey panels in it, as it stood before the fall. That layout is kept when every
            // standing panel still stands under its own figure (FALLEN_KEEP_UNDER inside its edges); only when one would be
            // pushed off its figure (an enemy that stepped into the fallen one's cell) does the grey panel slide or give way.
            bool fallenDone = false;
            if (fallen.Count > 0 && standing.Count > 0)
            {
                items.Clear();
                items.Add(new RowItem { X = o.PlayerX, W = StatusPanelLayout.PlayerWidth, Panel = _player, Keep = true });
                foreach (FollowRow r in standing) items.Add(new RowItem { X = r.X, W = r.W, Panel = r.V.Status, Keep = true });
                foreach (FollowRow r in fallen) items.Add(new RowItem { X = r.X, W = r.W, Panel = r.V.Status, Keep = false });
                StableSort(items, (a, b) => a.X.CompareTo(b.X));
                SpreadRow(items, lo, hi, lefts);
                bool under = true;
                for (int i = 0; i < items.Count && under; i++)
                {
                    RowItem it = items[i];
                    if (!it.Keep) continue;
                    float inset = Math.Min(FallenKeepUnder, it.W / 4f);
                    under = it.X >= lefts[i] + inset && it.X <= lefts[i] + it.W - inset;
                }
                if (under)
                {
                    for (int i = 0; i < items.Count; i++) SetTarget(items[i].Panel, lefts[i]);
                    foreach (FollowRow r in fallen) r.V.Status.SetYielded(false);
                    fallenDone = true;
                }
            }
            List<Span> taken = _fTaken;
            taken.Clear();
            for (int i = 0; i < _tPanels.Count; i++)
            {
                HudStatusPanel panel = _tPanels[i];
                if (fallenDone && IsFallenPanel(fallen, panel)) continue;
                taken.Add(new Span(_tLefts[i], _tLefts[i] + panel.Width));
            }
            if (!fallenDone)
            {
                foreach (FollowRow r in fallen)
                {
                    float? left = FallenPlace(HudUtil.Clamp(r.X - r.W / 2f, lo, hi - r.W), r.W, taken, lo, hi);
                    r.V.Status.SetYielded(left == null);
                    if (left != null)
                    {
                        SetTarget(r.V.Status, left.Value);
                        taken.Add(new Span(left.Value, left.Value + r.W));
                    }
                }
            }
            // They ease toward their places by the same share each frame (DepictionPlayer.PanelFollowRate, at the battle
            // speed's frame-motion factor, on real time).
            // A figure walking on the floor (MoveOnFloor) carries its panel along with it (review fix: the eased follow ran
            // ahead of the walk's slow start, or trailed its fast middle by 120 px): the panel moves with the figure, and only
            // the offset it had as the walk began (a lunge's 46 px) closes at the follow rate; once it stands, the ease is back.
            float speed = BattleSpeed.TweenSpeed(BattleClock.StepOfSpeed(_env.Settings.Speed));
            float share = (float)(1.0 - Math.Exp(-L.PanelFollowRate * speed * (_dtReal / 1000.0)));
            HashSet<HudStatusPanel> carried = _fCarried;
            carried.Clear();
            foreach (FollowRow r in rows) if (r.Walking) carried.Add(r.V.Status);
            if (o.PlayerWalking) carried.Add(_player);
            for (int i = 0; i < _tPanels.Count; i++)
            {
                HudStatusPanel panel = _tPanels[i];
                float to = _tLefts[i];
                float now = panel.Left;
                float left;
                if (snap) left = to;
                else if (carried.Contains(panel))
                {
                    float off = _carriedOff.TryGetValue(panel, out float had) ? had * (1f - share) : now - to;
                    _carriedOff[panel] = Math.Abs(off) < 0.25f ? 0f : off;
                    left = to + _carriedOff[panel];
                }
                else left = Math.Abs(to - now) < 0.25f ? to : now + (to - now) * share;
                if (!carried.Contains(panel)) _carriedOff.Remove(panel);
                panel.PlaceAt(left);
            }

            // KeepX / KeepBottom: each badge's middle on its enemy's home x, 24 px inside the edges; its bottom 14 px over the
            // head (OmenOverHead). Several badges keep apart on one row (scene addition, §5.8): each slides at most
            // BADGE_SLIDE_MAX off its head, eased over BADGE_SLIDE_MS, and its ▼ stays on the head.
            List<FollowRow> badgeRows = rows.Count > 1 ? standing : rows;
            List<float> spread = _fSpread;
            if (badgeRows.Count > 1)
            {
                items.Clear();
                foreach (FollowRow r in badgeRows) items.Add(new RowItem { X = r.X, W = HudOmenBadge.BadgeW });
                SpreadRow(items, badgeLo, badgeHi, spread);
            }
            else
            {
                spread.Clear();
                foreach (FollowRow r in badgeRows)
                    spread.Add(HudUtil.Clamp(r.X - HudOmenBadge.BadgeW / 2f, badgeLo, badgeHi - HudOmenBadge.BadgeW));
            }
            float slideShare = (float)(1.0 - Math.Exp(-3.0 * speed * _dtReal / BadgeSlideMs));
            // battle_ui_ux_v2 §5.16 「2 体以上の戦いでは、精鋭の札 2 段とほかの敵の札 1 段（下段の高さ）を並べます」 (review 19: each badge
            // stood 14 px over its own head, so a 264 px elite's stood 38 px over a 226 px enemy's): with two or more enemies every
            // badge's bottom is on one row, 14 px over the highest head; the ▼ still points down at its own enemy.
            float? rowTop = null;
            if (badgeRows.Count > 1)
            {
                float min = float.PositiveInfinity;
                foreach (FollowRow r in badgeRows) if (r.Top < min) min = r.Top;
                rowTop = min;
            }
            // Review 19: with a drawer open the row has less room (the trio beside the journal); its badges may slide twice as far
            // apart, so none covers its neighbour's swatch and hit mark.
            float slideMax = journalEdge != null || _piles.Open ? BadgeSlideMax * 2f : BadgeSlideMax;
            float liftShare = (float)(1.0 - Math.Exp(-3.0 * speed * _dtReal / OmenLiftMs));
            for (int i = 0; i < badgeRows.Count; i++)
            {
                FollowRow r = badgeRows[i];
                float home = r.X - HudOmenBadge.BadgeW / 2f;
                // KeepX's own shift at the screen's (or an open drawer's) edge, and the row's slide apart (at most 75 px) from there.
                float edgeFree = HudUtil.Clamp(home, badgeLo, badgeHi - HudOmenBadge.BadgeW) - home;
                float baseLeft = home + edgeFree;
                float off = (_multi ? HudUtil.Clamp(spread[i], baseLeft - slideMax, baseLeft + slideMax) : spread[i]) - baseLeft;
                EnemyView v = r.V;
                v.BadgeOff = snap || v.BadgeOff == null || Math.Abs(off - v.BadgeOff.Value) < 0.25f
                    ? off
                    : v.BadgeOff.Value + (off - v.BadgeOff.Value) * slideShare;
                float left = home + edgeFree + v.BadgeOff.Value;
                // battle-visual-v1 §4.3 大型の敵 (#843): over an enemy taller than 400 px (the row's tallest, with several) the
                // badge's top is fixed on y 162 instead of 14 px over a head that may be off the screen.
                float headTop = rowTop ?? r.Top;
                bool tall = FigureFrame.IsTall(L.FeetY - headTop);
                float bottom = tall ? OmenRowLayout.TallBadgeTop + HudOmenBadge.BadgeH : headTop - L.Omen.OverHead;
                // DS tag.lift (review 19): a badge over a frame showing the held card's prediction tag (on the frame's top edge) rises
                // just clear of the tag, eased over OMEN_LIFT_MS; every other badge rests on the row. §4.3 fixes a tall row's place, so
                // its badges never lift: the tag on a head that high is under the badge or off the screen either way.
                float tagTop = r.Top - L.MarkPad.Top - L.PredTagH / 2f;
                float need = v.Tagged && !tall ? HudUtil.Clamp(bottom - (tagTop - L.TagLiftGap), 0f, L.TagLift) : 0f;
                v.Lift = snap || Math.Abs(need - v.Lift) < 0.25f ? need : v.Lift + (need - v.Lift) * liftShare;
                v.Omen.Place(left, bottom - HudOmenBadge.BadgeH - v.Lift);
                // Review 17: the ▼ ties every badge to its enemy's head, one enemy too (battle_ui_ux_v2 §5.8 「▼ で敵に結びます」,
                // §5.16 「1 手目（▼）が下」); Unity draws none.
                v.Omen.SetTail(r.X - left);
                // Review 17 (two enemies passing: the walking one's badge rode over its neighbour's swatch and hit mark): a walking
                // enemy's badge goes behind the others until it arrives, as its figure does (battle_ui_ux_v2 §5.7).
                v.Omen.SetBehind(r.Walking && badgeRows.Count > 1);
            }
            // Review 21: a stance list a standing figure's art comes under (a slid arena, a wide silhouette on the last cell) moves to
            // its high place in the top bar, and back once the figures leave it.
            Func<UnitSide, int, FigureGeom> figure = _env.Geom.Figure;
            if (figure != null)
            {
                var arts = new List<ArtBox>();
                AddArt(arts, figure(UnitSide.Player, -1));
                foreach (FollowRow r in rows) if (!r.Down) AddArt(arts, figure(UnitSide.Enemy, _multi ? r.V.Unit : -1));
                _stances.KeepClear(arts);
            }
        }

        /// <summary>follow's rows are kept between frames (the JS makes new ones each frame; nothing outlives the call).</summary>
        private FollowRow RowOf(int index)
        {
            while (_fRowPool.Count <= index) _fRowPool.Add(new FollowRow());
            return _fRowPool[index];
        }

        /// <summary>targets.set(panel, left): Map.set — a key already there keeps its place in the order.</summary>
        private void SetTarget(HudStatusPanel panel, float left)
        {
            int at = _tPanels.IndexOf(panel);
            if (at >= 0) _tLefts[at] = left;
            else
            {
                _tPanels.Add(panel);
                _tLefts.Add(left);
            }
        }

        /// <summary>fallen.some((r) => r.v.status === panel).</summary>
        private static bool IsFallenPanel(List<FollowRow> fallen, HudStatusPanel panel)
        {
            foreach (FollowRow r in fallen) if (r.V.Status == panel) return true;
            return false;
        }

        /// <summary>
        /// follow's add(g): a standing figure's art box { x0, x1, y0, y1 }. JS `g.artX0 != null ? g.artX0 : g.x − g.width / 2`:
        /// geom.figure (field.js:1020, FieldView) always gives artX0 / artX1 (x − cx and x − cx + svgW), so the art box is
        /// taken as it is, a 0-wide one too.
        /// </summary>
        // js: scenes/battle/hud.js:418
        private static void AddArt(List<ArtBox> arts, FigureGeom g)
        {
            if (g == null || g.Down) return;
            arts.Add(new ArtBox(g.ArtX0, g.ArtX1, g.Top, g.FeetY));
        }

        // ---- the badge and the held card's prediction tag (DS motion.md `tag.lift`) ------------------------------------

        /// <summary>
        /// `units`: the enemies whose frame shows the held / selected card's prediction tag (the hand's taggedUnits; −1 for the
        /// one enemy). Review 19 (the tag pushed under the badge stood on the enemy's head and hid the ▼; the DS TargetFrame
        /// puts it on the frame's top edge and lifts the badge for it): Follow raises their badges just clear of the tag
        /// (L.TagLiftGap, at most L.TagLift); the others stay on the row.
        /// </summary>
        // js: scenes/battle/hud.js:432
        private void LiftOmenOf(IReadOnlyList<int> units)
        {
            var lifted = new HashSet<int>();
            if (units != null)
                foreach (int u in units) lifted.Add(_multi && u >= 0 && _enemies.ContainsKey(u) ? u : _primary);
            foreach (EnemyView v in _order) v.Tagged = v.Shown && !(v.Frame != null && v.Frame.Down) && lifted.Contains(v.Unit);
        }

        // ---- the end of the battle: the field settles under the result card (review fix) ---------------------------------

        /// <summary>
        /// h('div', { class: 'ocbg-battle-endscrim' }) inserted as layer.over's first child, opacity 0, display none. hud.css:155
        /// .ocbg-battle-endscrim { position: absolute; inset: 0; background: color-mix(in srgb, var(--ground) 62%, transparent);
        /// pointer-events: none } — the field dims under the result card (the DS's end card: a scrim of the ground at 62 %).
        /// </summary>
        // js: scenes/battle/hud.js:465
        private void BuildScrim()
        {
            RectTransform over = _env != null ? _env.Layer.Over : null;
            if (!over) return;
            BoxGraphic g = BoxGraphic.Make(over, "background: color-mix(in srgb, var(--ground) 62%, transparent);", 0f, 0f, L.W, L.H, "ocbg-battle-endscrim");
            g.raycastTarget = false;
            _scrim = g.rectTransform;
            UiNode n = UiNode.Of(_scrim);
            // insertBefore(scrim, layer.over.firstChild): first in the DOM order too, should the layer's children be re-sorted by z
            // (below every creation number, which counts up from 1; small enough that a child the re-sort numbers 1e-6 after it
            // still sorts after it).
            n.DomOrder = -1e6;
            _scrim.SetAsFirstSibling();
            n.Opacity = 0f;
            n.Visible = false;
        }

        /// <summary>Every fallen unit's panel and stance list (and badge), as elements to fade.</summary>
        // js: scenes/battle/hud.js:470
        private List<RectTransform> FallenParts(BattleOutcome outcome)
        {
            var parts = new List<RectTransform>();
            foreach (EnemyView v in _order)
            {
                if (v.Frame != null && v.Frame.Down)
                {
                    parts.Add(v.Status.El);
                    parts.Add(v.Omen.El);
                }
                // Review fix: the battle is over, so no enemy acts again: a standing enemy's omen badge and plan go too
                // (on a loss they stood in the result card's band, over the surviving enemy's head, and stuck out of it).
                else if (outcome != BattleOutcome.Ongoing) parts.Add(v.Omen.El);
            }
            // The enemies' stance list fades once every enemy has fallen (it keeps the stances of the standing ones).
            if (outcome == BattleOutcome.Won) parts.Add(_stances.ElOf(UnitSide.Enemy));
            // The script never marks the player Down: a lost battle is the player's fall.
            if (outcome == BattleOutcome.Lost || (_lastFrame != null && _lastFrame.Player != null && _lastFrame.Player.Down))
            {
                parts.Add(_player.El);
                parts.Add(_stances.ElOf(UnitSide.Player));
            }
            parts.RemoveAll(el => !el);
            return parts;
        }

        // js: scenes/battle/hud.js:484
        private Promise SettleEndOf(float ms, BattleOutcome outcome)
        {
            _endTurn.SetOff(true);
            // The journal's button goes off with the plate (a click there moves the result on, as anywhere on the screen).
            _ended = true;
            ShowJournalButton();
            _tip?.Hide();
            UiNode scrim = _scrim ? UiNode.Of(_scrim) : null;
            if (scrim) scrim.Visible = true;
            List<RectTransform> parts = FallenParts(outcome);
            // A part already faded (a standing enemy's omen, retireOmens) stays at 0 (review fix: `|| 1` lit it again), and a
            // fallen enemy's grey panel fades from its class's 55 % (review 17: the inline 1 lit it up for the first frame).
            var from = new float[parts.Count];
            for (int i = 0; i < parts.Count; i++) from[i] = OpacityOf(parts[i]);
            return _env.Clock.Run(ms, Ease.Out, t =>
            {
                if (scrim) scrim.Opacity = Fixed(t, 3);
                for (int i = 0; i < parts.Count; i++) SetOpacity(parts[i], Fixed(from[i] * (1f - t), 3));
            });
        }

        /// <summary>
        /// The battle is over (review fix): every standing enemy's omen badge and plan fade (Ease.In, OmenSpend's length),
        /// since no enemy acts again. On a loss this runs beside the hero's fall, so the badge over the surviving enemy's head
        /// is gone before the result card rises into that band.
        /// </summary>
        // js: scenes/battle/hud.js:509
        private Promise RetireOmensOf(float ms)
        {
            var els = new List<RectTransform>();
            foreach (EnemyView v in _order)
                if (!(v.Frame != null && v.Frame.Down) && v.Omen.El) els.Add(v.Omen.El);
            var from = new float[els.Count];
            for (int i = 0; i < els.Count; i++) from[i] = OpacityOf(els[i]);
            return _env.Clock.Run(ms, Ease.In, t =>
            {
                for (int i = 0; i < els.Count; i++) SetOpacity(els[i], Fixed(from[i] * (1f - t), 3));
            });
        }

        /// <summary>
        /// The element's own opacity now (el.style.opacity, else its class's — getComputedStyle — else 1): the UiNode's CSS
        /// opacity, else its CanvasGroup's alpha. Reading adds nothing to the element.
        /// </summary>
        private static float OpacityOf(RectTransform el)
        {
            if (!el) return 1f;
            UiNode n = el.GetComponent<UiNode>();
            if (n) return n.Opacity;
            CanvasGroup g = el.GetComponent<CanvasGroup>();
            return g ? g.alpha : 1f;
        }

        /// <summary>el.style.opacity = v: through the element's UiNode when it has one, else a CanvasGroup of its own.</summary>
        private static void SetOpacity(RectTransform el, float v)
        {
            if (!el) return;
            UiNode n = el.GetComponent<UiNode>();
            if (n)
            {
                n.Opacity = v;
                return;
            }
            CanvasGroup g = el.GetComponent<CanvasGroup>();
            if (!g) g = el.gameObject.AddComponent<CanvasGroup>();
            g.alpha = Mathf.Clamp01(v);
        }

        /// <summary>Number.prototype.toFixed(digits) read back as a number (the style string the JS writes).</summary>
        private static float Fixed(float x, int digits) => (float)Math.Round((double)x, digits, MidpointRounding.AwayFromZero);

        // ---- api ---------------------------------------------------------------------------------------------------------

        // js: scenes/battle/hud.js:550
        public IStatusPanel Status(UnitSide side, int unit = -1)
        {
            if (side == UnitSide.Player) return _playerFacade;
            if (unit >= 0 && _enemies.TryGetValue(unit, out EnemyView v)) return v.Status;
            return PrimaryView().Status;
        }

        // js: scenes/battle/hud.js:555
        public IOmenFacade Omen => _omen;

        /// <summary>
        /// An omen shown mid-event (OmenShow, before the event's frame is applied): enemy `unit`'s badge takes its swatch and its
        /// move arrow from `frame` (the event's After), so the badge pops in as that frame will leave it.
        /// </summary>
        // js: scenes/battle/hud.js:560
        public void SyncOmenLook(DepictionFrame frame, int unit)
        {
            if (frame == null) return;
            List<EnemyEntry> list = EnemiesOf(frame);
            int at = list.FindIndex(x => x.Unit == unit);
            if (at < 0) at = 0;
            if (list.Count == 0) return;
            EnemyEntry e = list[at];
            if (!_enemies.TryGetValue(e.Unit, out EnemyView v)) return;
            v.Omen.SetMoveDir(MoveDirOf(frame, e));
            v.Omen.SetSwatchShown(SwatchesOf(frame), AimsOf(frame, e), e.Share != null ? e.Share.StripeDirection : 0);
        }

        /// <summary>The badge of enemy `unit` (Cue.Unit, the index in DepictionFrame.Enemies); the leader's when unknown.</summary>
        // js: scenes/battle/hud.js:570
        public IOmenBadge OmenOf(int unit = -1) =>
            unit >= 0 && _enemies.TryGetValue(unit, out EnemyView v) ? v.Omen : PrimaryView().Omen;

        /// <summary>The enemies the hud draws now, by unit (the frame's order).</summary>
        // js: scenes/battle/hud.js:574
        public IReadOnlyList<int> EnemyUnits()
        {
            var units = new List<int>();
            foreach (EnemyView v in _order) if (v.Shown) units.Add(v.Unit);
            return units;
        }

        // js: scenes/battle/hud.js:575
        public IHudStamina Stamina => _stamina;

        // js: scenes/battle/hud.js:580
        public void SetEnemyTurn(bool on) => _endTurn.SetEnemyTurn(on);

        // js: scenes/battle/hud.js:581
        public IHudEndTurn EndTurn => _endTurn;

        // js: scenes/battle/hud.js:582
        public Promise Banner(string text, BannerSide side, float ms) => HudChrome.PlayBanner(_env, text, side, ms);

        /// <summary>
        /// The end of the battle (review 21): the result paper — `info` { title, lines, bars, stats, button } (the player's scene
        /// text), `onButton` its button's click.
        /// </summary>
        // js: scenes/battle/hud.js:587
        public Promise ResultCard(BattleOutcome outcome, float ms, ResultInfo info, Action onButton) => _result.Show(outcome, ms, info, onButton);

        // js: scenes/battle/hud.js:588
        public void ResultCardNow(BattleOutcome outcome, ResultInfo info, Action onButton) => _result.Now(outcome, info, onButton);

        /// <summary>§2.5 / §5.18 順 6 (review 21): a chain's next fight opens with 「n 戦目」 and its enemy's name over the dark board.</summary>
        // js: scenes/battle/hud.js:590
        public Promise ChainBanner(string title, string name, float scale, Action onOut) =>
            _chainBanner.Play(title, name, scale, onOut == null ? (Action<float>)null : _ => onOut());

        /// <summary>§5.19 (review 21): 「力尽きた」 goes as the result screen comes up.</summary>
        // js: scenes/battle/hud.js:592
        public Promise LossWordOut(float ms) => _lossInk.FadeWord(ms);

        /// <summary>The battle is over: the scrim comes up, the plate goes off, the fallen units' panels and stance lists fade.</summary>
        // js: scenes/battle/hud.js:594
        public Promise SettleEnd(float ms, BattleOutcome outcome) => SettleEndOf(ms, outcome);

        // js: scenes/battle/hud.js:595
        public void SettleEndNow(BattleOutcome outcome) => SettleEndOf(0f, outcome).Forget();

        /// <summary>The battle is over: the standing enemies' omen badges and plans fade (no enemy acts again).</summary>
        // js: scenes/battle/hud.js:597
        public Promise RetireOmens(float ms) => RetireOmensOf(ms);

        /// <summary>The enemies whose frame shows the held card's prediction tag (none: []): their badges stand higher.</summary>
        // js: scenes/battle/hud.js:599
        public void LiftOmen(IReadOnlyList<int> units) => LiftOmenOf(units);

        /// <summary>
        /// Review 19 (a Tab at rest chose an enemy and nothing on it said so): enemy `unit`'s number panel wears the battle's
        /// focus (the amber edge) — −1 for none.
        /// </summary>
        // js: scenes/battle/hud.js:604
        public void SetRestFocus(int unit)
        {
            foreach (EnemyView v in _order) v.Status.SetFocus(unit >= 0 && v.Unit == unit && v.Shown && !(v.Frame != null && v.Frame.Down));
        }

        /// <summary>The pile list's log tab (battle_ui_ux_v2 §1.1 C4): the lines so far.</summary>
        // js: scenes/battle/hud.js:608
        public void SetLog(IReadOnlyList<string> lines) => _piles.SetLog(lines);

        /// <summary>The stance list's slot a stance just put down goes to (StanceList.TopOf(0)), or null while the lists are hidden.</summary>
        // js: scenes/battle/hud.js:539
        private StageBox? StanceSlotBox(UnitSide side) => _stances.SlotOf(side);

        /// <summary>
        /// battle_ui_ux_v2 §5.11 順 8 (review 19; Unity lets the badge vanish with the frame): an enemy's stance action — its omen
        /// badge flies to the end of the enemy's stance list and shrinks into the slot there over `ms` (out), fading as it lands.
        /// </summary>
        // js: scenes/battle/hud.js:613
        public Promise FlyOmenToStances(int unit, float ms)
        {
            EnemyView v = unit >= 0 && _enemies.TryGetValue(unit, out EnemyView hit) ? hit : PrimaryView();
            StageBox from = v.Omen.Box();
            StageBox? slotAt = StanceSlotBox(UnitSide.Enemy);
            if (slotAt == null || !v.Omen.Visible) return Promise.Resolved();
            StageBox slot = slotAt.Value;
            // The badge alone (an elite's plan stays for its second action).
            RectTransform face = v.Omen.Face ? v.Omen.Face : v.Omen.El;
            RectTransform floatLayer = _env.Layer.Float;
            RectTransform ghost = null;
            UiNode gn = null;
            // (with ms 0 the JS makes the copy and removes it at once: nothing of it is ever drawn, so none is made here)
            if (ms > 0f && face && floatLayer)
            {
                ghost = CloneFace(face, floatLayer);
                // .ocbg-battle-omenfly { position: absolute; pointer-events: none }
                gn = UiNode.Of(ghost);
                gn.PointerEvents = PointerEvents.None;
                gn.ResetProps();
                gn.SetTransform(0f, 0f, 1f, 1f, 0f);
                gn.Place(from.X, from.Y, from.W, from.H);
                gn.SetOrigin(0f, 0f);
                gn.Opacity = 0.5f; // binds the copied CanvasGroup, if the face had one, before the 1 below
                gn.Opacity = 1f;
                _flights.Add(ghost);
            }
            v.Omen.HideNow();
            float k = Math.Min(slot.W / from.W, slot.H / from.H);
            float toX = slot.X + slot.W / 2f - from.W * k / 2f;
            float toY = slot.Y + slot.H / 2f - from.H * k / 2f;
            if (!(ms > 0f))
            {
                RemoveFlight(ghost);
                return Promise.Resolved();
            }
            return _env.Clock.Run(ms, Ease.Out, t =>
            {
                if (!gn) return;
                gn.Place(Fixed(from.X + (toX - from.X) * t, 1), Fixed(from.Y + (toY - from.Y) * t, 1));
                gn.PropScale = Fixed(1f + (k - 1f) * t, 3);
                gn.Opacity = Fixed(t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f, 3);
            }).Then(() => RemoveFlight(ghost));
        }

        /// <summary>ghost.remove(); flights.delete(ghost).</summary>
        private void RemoveFlight(RectTransform ghost)
        {
            if (!ghost) return;
            _flights.Remove(ghost);
            Ui.Remove(ghost);
        }

        /// <summary>battle_ui_ux_v2 §5.19 順 2〜3: the ink floods up from the bottom over `inkMs`, then `word` comes in over `wordMs`.</summary>
        // js: scenes/battle/hud.js:641
        public Promise LossInk(string word, float inkMs, float wordMs) => _lossInk.Show(word, inkMs, wordMs);

        /// <summary>The speed the settings hold now, in the band's speed button (its word is HeaderBand.SpeedLabel's).</summary>
        // js: scenes/battle/hud.js:642
        public void Speed(string label, int chevrons) => _header.SetSpeed(HeaderBand.SpeedLabel(SpeedStepNow()));

        /// <summary>The battle's own switches (the speed, the settings) take a press or not: off once it is over.</summary>
        // js: scenes/battle/hud.js:644
        public void SetSpeedVisible(bool on)
        {
            _switchesOn = on;
            ShowSwitches();
        }

        /// <summary>Esc (review 21): closes a chip's or a stance's detail pinned by a right click; returns whether one was open.</summary>
        // js: scenes/battle/hud.js:646
        public bool ClosePinned() => _tip.Unpin();

        // js: scenes/battle/hud.js:647
        public bool DrawerOpen() => _piles.Open;

        // js: scenes/battle/hud.js:648
        public void CloseDrawer() => _piles.Close();

        // ---- raw input routed by the scene ----

        /// <summary>
        /// §3.13 (review 21): a pinned chip / stance detail closes on a click anywhere (window pointerdown, capture phase).
        /// </summary>
        // js: scenes/battle/hud.js:459
        public void OnPinDown(BattlePointer p)
        {
            if (p != null && p.Button == 0 && _tip.Pinned) _tip.Unpin();
        }

        /// <summary>… and on a right click elsewhere (a right click on a chip or a frame pins that one instead: the event is marked there).</summary>
        // js: scenes/battle/hud.js:460
        public void OnPinContext(BattlePointer p)
        {
            if (!_env.ContextPinned && _tip.Pinned) _tip.Unpin();
        }

        /// <summary>Hovering an omen badge keeps only that enemy's aimed cells (battle_ui_ux_v2 §2.2.2): opts.OnOmenHover(unit / −1).</summary>
        // js: scenes/battle/hud.js:440
        public void OnPointerMove(BattlePointer p)
        {
            int found = -1;
            // e.target && env.root.contains(e.target): the pointer is over the scene, not an app overlay.
            if (p != null && _multi && !p.Outside)
            {
                float x = p.X, y = p.Y;
                foreach (EnemyView v in _order)
                {
                    if (!v.Shown || !v.Omen.Visible) continue;
                    StageBox b = v.Omen.Box();
                    if (x >= b.X && x <= b.X + b.W && y >= b.Y && y <= b.Y + b.H)
                    {
                        found = v.Unit;
                        break;
                    }
                }
            }
            if (found == _hoverUnit) return;
            _hoverUnit = found;
            _opts.OnOmenHover?.Invoke(found);
        }

        // js: scenes/battle/hud.js:649
        public void Destroy()
        {
            _offFrame();
            // (the window / root listeners are the scene's: BattleInput stops routing with the scene)
            _tip.Unpin();
            if (_scrim) Ui.Remove(_scrim);
            _scrim = null;
            _tip.Hide();
            _piles.Destroy();
            _stances.Destroy();
            _stamina.Destroy();
            _player.Destroy();
            _played.Destroy();
            _endTurn.Destroy();
            _header.Destroy();
            _result.Destroy();
            _lossInk.Destroy();
            _chainBanner.Destroy();
            foreach (RectTransform el in _flights) if (el) Ui.Remove(el);
            _flights.Clear();
            foreach (EnemyView v in _order)
            {
                v.Status.Destroy();
                v.Omen.Destroy();
            }
            _enemies.Clear();
            _order.Clear();
            _carriedOff.Clear();
        }

        // ---- geometry (docs/BATTLE_SCENE.md §2.4) ----------------------------------------------------------------------

        // js: scenes/battle/hud.js:666
        private void RegisterGeom()
        {
            BattleGeom g = _env.Geom;
            g.PileMiddle = kind => _piles.Middle(kind);
            g.OmenBox = unit => OmenOf(unit).Box();
            // Whether enemy `unit`'s badge shows now (a spent or hidden badge is no part of the enemy to click).
            g.OmenShown = unit => OmenOf(unit).Visible;
            // The omen badge's place over every standing enemy (stage px), shown or between two omens: the floats keep under
            // them (a badge that pops in while a number rises must not land on it).
            g.OmenBoxes = () =>
            {
                var boxes = new List<StageBox>();
                foreach (EnemyView v in _order)
                    if (v.Shown && !(v.Frame != null && v.Frame.Down)) boxes.Add(v.Omen.Box());
                return boxes;
            };
            g.EndTurnBox = () => _endTurn.Box();
            // The number panel of `side` (an enemy's by `unit`) as it stands: { x, y, w, h }, or null when not shown.
            g.StatusBox = (side, unit) =>
            {
                IStatusPanel p = Status(side, unit);
                return p != null && p.Shown ? p.Box() : (PanelBox?)null;
            };
            // Every number panel shown, { side, unit, box } (the hand keeps its hover line off them).
            g.StatusBoxes = () =>
            {
                var list = new List<StatusPanelBox>();
                if (_player.Shown) list.Add(new StatusPanelBox(UnitSide.Player, -1, _player.Box()));
                foreach (EnemyView v in _order)
                    if (v.Shown && v.Status.Shown) list.Add(new StatusPanelBox(UnitSide.Enemy, v.Unit, v.Status.Box()));
                return list;
            };
            // The stance list of `side` as it stands (its frames, the empty one included), or null when the lists are hidden.
            g.StanceBox = side => _stances.BoxOf(side);
            // The slot at the end of `side`'s stance list (y 470, where a stance just put down goes; §5.11 順 1), or null when hidden.
            g.StanceSlot = side => StanceSlotBox(side);
            g.CoversPointer = pt => _piles.Covers(pt);
            // The pile list drawer's right edge (stage px) as it slides in; 0 while closed (the guide line keeps right of it).
            g.DrawerRight = () => _piles.DrawerRight();
        }

        // ---- the omen flight's copy of the badge (face.cloneNode(true)) ----------------------------------------------------

        private static FieldInfo _buildTintField;
        private static bool _buildTintLooked;

        /// <summary>
        /// face.cloneNode(true) for a uGUI hierarchy: the badge's elements as they stand now, under `parent`, frozen. The
        /// RectTransforms, CanvasGroups, UiNodes and TMP labels copy as they are; each kit vector graphic, whose shape lives
        /// in unserialized state, becomes a plain VectorGraphic drawing the mesh the original drew last (its tint, build tint
        /// and inherited CSS filter baked in); the kit's behaviours that need their unserialized state (labels' styles, CSS
        /// states, click areas, flex attributes) are left out, as a clone's DOM listeners are. The copy is made under an
        /// inactive holder, so none of them wakes on half a copy.
        /// </summary>
        // js: scenes/battle/hud.js:620
        private static RectTransform CloneFace(RectTransform face, RectTransform parent)
        {
            VectorGraphic[] originals = face.GetComponentsInChildren<VectorGraphic>(true);
            foreach (VectorGraphic o in originals)
                if (o && o.isActiveAndEnabled) o.RebuildNow();

            var holderGo = new GameObject("ocbg-battle-omenfly-holder", typeof(RectTransform));
            holderGo.SetActive(false);
            var holder = (RectTransform)holderGo.transform;
            holder.SetParent(parent, false);
            GameObject go = UnityEngine.Object.Instantiate(face.gameObject, holder, false);
            go.name = "ocbg-battle-omenfly";

            // Behaviours that only work with their unserialized state go first (nothing then depends on a graphic being
            // replaced); graphics (TMP and its sub-meshes, hit areas), masks and the UiNodes stay.
            foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!mb || mb is Graphic || mb is UiNode || mb is RectMask2D || mb is Mask) continue;
                UnityEngine.Object.DestroyImmediate(mb);
            }

            VectorGraphic[] copies = go.GetComponentsInChildren<VectorGraphic>(true);
            int n = Math.Min(originals.Length, copies.Length);
            for (int i = 0; i < n; i++) Freeze(originals[i], copies[i], face);

            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.SetAsLastSibling();
            UnityEngine.Object.DestroyImmediate(holderGo);
            if (!go.activeSelf) go.SetActive(true);
            return rt;
        }

        /// <summary>
        /// One vector graphic of the copy replaced by a plain VectorGraphic drawing what the original draws now. The CSS filters
        /// baked in are those of the copied elements (the face and the elements under it, their inline filters cloned with
        /// them); the face's old ancestors' (the omen wrap, the panels layer) stay behind, and the float layer's own apply live.
        /// </summary>
        private static void Freeze(VectorGraphic original, VectorGraphic copy, Transform face)
        {
            if (!original || !copy) return;
            var snap = new MeshBuilder();
            MeshBuilder geo = original.Geometry;
            if (geo != null && geo.Idx.Count > 0)
            {
                Rgba tint = original.Tint;
                Rgba build = BuildTintOf(original);
                var k = new Rgba(tint.R * build.R, tint.G * build.G, tint.B * build.B, tint.A * build.A);
                ColorFilter filter = FilterWithin(original.transform, face);
                for (int i = 0; i < geo.Pos.Count; i++)
                {
                    Rgba c = geo.Col[i];
                    c = new Rgba(c.R * k.R, c.G * k.G, c.B * k.B, c.A * k.A);
                    if (!filter.IsNone) c = filter.Apply(c);
                    snap.AddVertex(geo.Pos[i], c);
                }
                snap.Idx.AddRange(geo.Idx);
            }
            float opacity = original.Opacity;
            bool screen = original.ScreenBlend;
            bool enabled = copy.enabled;
            bool maskable = copy.maskable;
            GameObject go = copy.gameObject;
            UnityEngine.Object.DestroyImmediate(copy);
            VectorGraphic g = go.AddComponent<VectorGraphic>();
            if (!g) return;
            g.raycastTarget = false;
            g.maskable = maskable;
            g.Opacity = opacity;
            g.ScreenBlend = screen;
            g.Builder = (mb, box, feather) => mb.Append(snap);
            g.enabled = enabled;
        }

        /// <summary>The CSS filters of t and its ancestors up to `top` (inclusive), outermost first (UiNode.FilterAt cut at `top`).</summary>
        private static ColorFilter FilterWithin(Transform t, Transform top)
        {
            var chain = new List<ColorFilter>();
            for (Transform p = t; p != null; p = p.parent)
            {
                UiNode node = p.GetComponent<UiNode>();
                if (node && !node.Filter.IsNone) chain.Add(node.Filter);
                if (p == top) break;
            }
            ColorFilter f = ColorFilter.None;
            for (int i = chain.Count - 1; i >= 0; i--) f = f.Then(chain[i]);
            return f;
        }

        /// <summary>VectorGraphic's build tint (the one-colour shapes' colour: icons, hatches, glows), which the kit keeps protected.</summary>
        private static Rgba BuildTintOf(VectorGraphic g)
        {
            if (!_buildTintLooked)
            {
                _buildTintLooked = true;
                _buildTintField = typeof(VectorGraphic).GetField("_buildTint", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            if (_buildTintField == null) return Rgba.White;
            object value = _buildTintField.GetValue(g);
            return value is Rgba r ? r : Rgba.White;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _buildTintField = null;
            _buildTintLooked = false;
        }
    }

    /// <summary>
    /// hud.status(Player): the hero's number panel, whose HP the header band shows too (#1074). The player's calls set the
    /// number as they land (the bar's slide is the panel's own); the stamina reaches the band through the panel's OnStamina.
    /// </summary>
    internal sealed class HudPlayerPanel : IStatusPanel
    {
        private readonly HudStatusPanel _panel;
        private readonly HeaderBar _header;
        private int _maxHp;

        internal HudPlayerPanel(HudStatusPanel panel, HeaderBar header)
        {
            _panel = panel;
            _header = header;
        }

        /// <summary>The frame bound the panel: its HP and its max, which the events' HP calls keep.</summary>
        internal void Bound(UnitFrame unit)
        {
            _maxHp = unit.HpMax;
            _header.SetHp(unit.Hp, _maxHp);
        }

        public int Hp => _panel.Hp;
        public bool Shown => _panel.Shown;
        public PanelBox Box() => _panel.Box();
        public void SetGuard(int n) => _panel.SetGuard(n);
        public Promise PopGuard(int n, float ms = 260f) => _panel.PopGuard(n, ms);
        public Promise CrackGuard(float ms) => _panel.CrackGuard(ms);

        public void SetHp(int hp)
        {
            _panel.SetHp(hp);
            _header.SetHp(hp, _maxHp);
        }

        public Promise AnimateHp(int hp, float ms)
        {
            _header.SetHp(hp, _maxHp);
            return _panel.AnimateHp(hp, ms);
        }

        public Promise AnimateTrail(float ms) => _panel.AnimateTrail(ms);
        public void SnapTrail() => _panel.SnapTrail();

        public void SetHpKeepingTrail(int hp)
        {
            _panel.SetHpKeepingTrail(hp);
            _header.SetHp(hp, _maxHp);
        }

        public void ShowHpLoss(int n) => _panel.ShowHpLoss(n);
        public void SetStatus(string label, int stacks) => _panel.SetStatus(label, stacks);
        public Promise PlayStatus(string label, int stacks, EffectId beat, float ms) => _panel.PlayStatus(label, stacks, beat, ms);
        public void SetStamina(int stamina, int max) => _panel.SetStamina(stamina, max);
        public Promise LightPips(int stamina, int max, float perPipMs) => _panel.LightPips(stamina, max, perPipMs);
        public Promise PopPips(float ms) => _panel.PopPips(ms);
        public Promise GreyOut(float ms) => _panel.GreyOut(ms);
        public void SetDown(bool on) => _panel.SetDown(on);
        public void SetFocus(bool on) => _panel.SetFocus(on);
    }
}
