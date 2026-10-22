// Port of scenes/meta/hall.js: 継承の間 (CAMP_FACILITIES_DESIGN.md §3.1, concept-v3 §8) — what the earlier lives hand
// on, read off Inheritance.HallView(lineage): the past lives with their 装束 and how and where each ended, every untaken
// trace on a column of the seven layers, the survivor and its bonus, and the next heir's 襲名. Nothing here takes a
// trace (R-12); 「出立の支度へ」 starts the life through Inheritance.StartLife and hands it to the router.
//
// The parts (cards, strata, marks, the trace body, the heir's pieces) and meta.css's hall rules, verbatim, are in
// HallParts.cs; this file is mountHall: the header and the corner buttons, the order the parts are mounted in, the
// selection and the keys, the scrolling and its edge fades, the arrival with the naming, and the two ways out.
// hallBackdropSpec is Trial.Script.HallBackdropSpec.For, pastTraceLine Trial.Script.MetaLines.PastTraceLine, and the
// arrival's computed times (sealAt, the letters, the attire after the stamp) Trial.Script.MetaTimelines.
//
// Stylers behave as the JS's: every Styler.Of is a fresh proxy and TrialClock replaces keys per proxy (stage.js
// tween), so two stylers of one element do not cut each other's tweens; hall.js never runs two on one element at once.
//
// Deviations, all of look-alike kind (meta-a.md §8):
// - The lists show no scrollbar (the reference shots are headless Chrome's, which shows none), so the cards keep 434.
using System;
using System.Collections.Generic;
using System.Linq;
using InheritanceCore;
using TMPro;
using Trial.Geometry;
using Trial.Kit;
using Trial.Script;
using UnityEngine;

namespace Trial.Meta
{
    public sealed class HallScene : IScene
    {
        // ---- hall.js's layout numbers (stage px) ---------------------------------------------------------------------

        // js: scenes/meta/hall.js:89-90 the title and the lead
        private const float TitleX = 72f, TitleY = 36f, LeadX = 74f, LeadY = 110f;

        // js: scenes/meta/hall.js:98-120 the corner: from 1848 leftwards, 16 apart, y 44, 52 tall
        private const float CornerRight = 1848f, CornerY = 44f, CornerH = 52f, CornerGap = 16f;
        private const float GearW = 120f, BookW = 56f, ToTitleW = 168f;

        // js: scenes/meta/hall.js:125-126, :153 the left column
        private const float LivesX = 72f, LivesHeadY = 184f, LivesY = 236f, LivesW = 440f, LivesH = 486f;
        private const float RulesY = 742f, RulesH = 244f;

        // js: scenes/meta/hall.js:157-159, :210 the middle column and its key
        private const float ColumnX = 552f, ColumnY = 236f, ColumnW = 360f, ColumnH = 750f;
        private const float KeyY = 1000f, KeyH = 40f;

        // js: scenes/meta/hall.js:214 the selected trace
        private const float DetailX = 936f, DetailY = 236f, DetailW = 452f, DetailH = 750f;

        // js: scenes/meta/hall.js:292-294, :337, :339 the right column, the start button, the keys line
        private const float HeirX = 1428f, HeirY = 236f, HeirW = 420f, HeirH = 696f;
        private const float StartX = 1428f, StartY = 948f, StartW = 420f, StartH = 76f;
        private const float KeysX = 72f, KeysY = 1028f;

        // js: scenes/meta/hall.js:413, :415 ↑↓ scroll the candidates by 84 px; a page is the panel less 84
        private const float ScrollStep = 84f;

        // ---- the scene -----------------------------------------------------------------------------------------------

        private readonly HallCtx _ctx;
        private readonly MetaSound _sound;
        private readonly Lineage _lineage;
        private readonly HallView _view;
        private readonly RectTransform _root;
        private readonly MetaBackdrop _backdrop;
        private readonly SceneClock _clock;
        private readonly List<MetaButton> _corner = new List<MetaButton>();
        private readonly ScrollArea _lives;
        private readonly List<HallStratum> _strata = new List<HallStratum>();
        private readonly List<UiNode> _flameEls = new List<UiNode>();
        private readonly RectTransform _detail;
        private int _sel;
        private HallTraceBody _traceBody;
        private TextMeshProUGUI _keys;
        private readonly Styler _fade;
        private readonly Action _popKeys;
        private bool _alive = true;
        private bool _busy;

        // While mountHall runs nothing has been drawn yet: the first drawTrace's .is-sel does not transition.
        private bool _mounting = true;

        // js: scenes/meta/hall.js:77
        private HallScene(RectTransform host, HallCtx ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _sound = MetaUtil.SoundOf(ctx);
            _lineage = ctx.Lineage ?? throw new ArgumentNullException(nameof(ctx.Lineage));
            _view = Inheritance.HallView(_lineage);
            _root = MetaUtil.SceneRoot(host, "hall");
            _backdrop = MetaBackdrop.Create(_root, HallBackdropSpec.For(), new MetaBackdrop.Options { Vignette = 0.5f });
            _clock = MetaUtil.SceneClock((dt, now) => { _backdrop.Paint(now); Flames(now); }, ctx.Stage);
            _backdrop.Alpha = 0f;

            // ---- header ----
            TextMeshProUGUI title = Label.Make(_root, _view.Title, MetaStyle.Text(HallParts.TitleCss), TitleX, TitleY, null, null, "ocbg-meta-hall-title");
            HallParts.Glow(title);   // text-shadow 0 0 30px amber 30%
            TextMeshProUGUI lead = Label.Make(_root, _view.Lead, MetaStyle.P(HallParts.LeadCss), LeadX, LeadY, null, null, "ocbg-meta-hall-lead");
            // Top right (CAMP_FACILITIES_DESIGN.md §3.5 / §3.3): 設定 (Esc) and, when the router gives one, the journal (J),
            // laid from the right edge leftwards as on 出立; then, when the router gives the way back, 「タイトルへ」.
            // The lineage is saved the moment a life closes or a trace is taken (tier1 R1-16), so leaving the hall keeps
            // everything: the title's つづきから comes back here with the same next heir (INHERITANCE.md R-02).
            float cornerX = CornerRight;
            if (ctx.OnSettings != null)
            {
                cornerX -= GearW;
                MetaButton gear = MetaButton.Chip(_root, MetaText.Settings, "Esc", cornerX, CornerY, GearW, CornerH);
                gear.Click += () =>
                {
                    if (_busy) return;
                    _sound.Play("uiClick");
                    _ctx.OnSettings();
                };
                _corner.Add(gear);
                cornerX -= CornerGap;
            }
            if (ctx.OnJournal != null)
            {
                cornerX -= BookW;
                MetaButton book = MetaButton.ChipIcon(_root, "journal", 30f, cornerX, CornerY, BookW, CornerH, DsTokens.Ink);
                TitleTip.On(book.Box, global::Journal.JournalBook.Heading);   // title / aria-label: 手記
                book.Click += () => { if (!_busy) _ctx.OnJournal(); };
                _corner.Add(book);
                cornerX -= CornerGap;
            }
            if (ctx.OnTitle != null)
            {
                cornerX -= ToTitleW;
                MetaButton toTitle = MetaButton.ChipIconText(_root, "back", 20f, MetaText.BackToTitle, cornerX, CornerY, ToTitleW, CornerH, DsTokens.Ink);
                toTitle.Root.name = "ocbg-meta-chip-btn ocbg-meta-hall-totitle";
                toTitle.Click += () => LeaveToTitle().Forget();
                _corner.Add(toTitle);
            }

            // ---- past lives (newest first) ----
            RectTransform livesHead = MetaStyle.H2(_root, _view.PastLivesHeading, LivesX, LivesHeadY);
            RectTransform livesBox = Ui.Div(_root, "ocbg-meta-hall-lives", LivesX, LivesY, LivesW, LivesH);
            UiNode.Of(livesBox);
            _lives = ScrollArea.Attach(livesBox);
            _lives.ShowThumb = false;
            _lives.SetFades(HallParts.LivesMoreBelow, HallParts.LivesMoreAbove, HallParts.LivesMoreBoth);
            HallTouchScroll.On(_lives);
            float cardW = LivesW - Ocb.BoxOf(HallParts.LivesCss).Padding.Right;   // padding-right: 6px
            float livesGap = HallParts.RowGap(HallParts.LivesCss);
            var lifeCards = new List<RectTransform>();
            float ly = 0f;
            if (_view.PastLives.Count == 0)
            {
                RectTransform none = HallParts.LivesNone(_lives.Content, string.IsNullOrEmpty(_view.Empty) ? InheritanceText.HallEmpty : _view.Empty, cardW);
                ly = UiNode.Of(none).H;
            }
            foreach (PastLifeView life in _view.PastLives)
            {
                if (lifeCards.Count > 0) ly += livesGap;
                RectTransform card = HallParts.LifeCard(_lives.Content, life, TraceLineOf(life), cardW);
                UiNode.Of(card).Place(0f, ly);
                ly += UiNode.Of(card).H;
                lifeCards.Add(card);
            }
            _lives.ScrollHeight = ly;
            // More lives than fit: the list fades at the edge it continues past (wheel or drag to read on).
            _lives.Scrolled += Edge;
            Edge();
            // The rules of traces (R-12: the hall only shows them), under the lives.
            RectTransform rules = HallParts.Rules(_root, _view.TraceRules, LivesX, RulesY, LivesW, RulesH);

            // ---- the seven layers, with where each life ended and the traces still lying there ----
            RectTransform traceHead = MetaStyle.H2(_root, _view.TraceHeading, ColumnX, LivesHeadY);
            RectTransform column = Ui.Div(_root, "ocbg-meta-hall-column", ColumnX, ColumnY, ColumnW, ColumnH);
            UiNode.Of(column);
            for (int layer = 1; layer <= DungeonCore.SevenLayers.Count; layer++)
            {
                DungeonCore.LayerProfile profile = DungeonCore.SevenLayers.Of(layer);
                _strata.Add(HallParts.Stratum(column, layer, profile.DisplayName(_lineage.Player.CarvingTaken)));
            }
            // Each closed life marks the layer it ended on (the lineage keeps the layer; HallView prints the line).
            foreach (LifeRecord life in _lineage.Lives)
            {
                HallStratum s = StratumOf(life.Layer);
                if (s == null) continue;
                bool lived = !HallParts.IsDeath(life.Ending);
                int traceIndex = !string.IsNullOrEmpty(life.TraceId) ? IndexOfTrace(life.TraceId) : -1;
                bool untaken = traceIndex >= 0;
                // the mark's hover line says whose life it was, how it ended and where its trace stands (the three looks)
                PastLifeView past = _view.PastLives.FirstOrDefault(p => p.Generation == life.Generation);
                string tip = past != null
                    ? MetaText.MarkTitle(past.Line, past.EndingLabel, MetaLines.PastTraceLine(_lineage, past))
                    : InheritanceText.HeirLine(life.Generation, life.Name);
                HallMark mark = HallParts.Mark(s, life.Generation, lived, untaken, life.CollectedBy != null, tip);
                mark.TraceIndex = traceIndex;
                mark.Area.Click += _ => MarkClick(mark);
                if (untaken) _flameEls.Add(mark.Flame);
                // The whole stratum answers the pointer for the trace lying there (the mark is small).
                if (untaken && !s.Live)
                {
                    HallStratum band = s;
                    band.MakeLive(traceIndex, mark, () => Select(band.TraceIndex, true));
                }
            }
            foreach (HallStratum s in _strata) s.LayMarks();
            // What the marks' looks mean, under the column (review: 「2」 dashed, 「3」 ringed and 「4」 glowing had no key):
            // only the looks the column shows.
            var looks = new List<(string Cls, string Word)>();
            if (_lineage.Lives.Any(l => !string.IsNullOrEmpty(l.TraceId) && _view.Traces.Any(t => t.Id == l.TraceId))) looks.Add(("is-trace", MetaText.MarkUntaken));
            if (_lineage.Lives.Any(l => l.CollectedBy != null)) looks.Add(("is-taken", MetaText.MarkTaken));
            if (_lineage.Lives.Any(l => !HallParts.IsDeath(l.Ending))) looks.Add(("is-alive", MetaText.MarkAlive));
            if (looks.Count > 0) HallParts.MarkKey(_root, looks, ColumnX, KeyY, ColumnW, KeyH);

            // ---- the selected trace ----
            _detail = Ocb.El(_root, "ocbg-meta-hall-trace", HallParts.TraceCss);
            UiNode.Of(_detail).Place(DetailX, DetailY, DetailW, DetailH);
            _sel = _view.Traces.Count > 0 ? 0 : -1;
            DrawTrace();

            // ---- the next heir ----
            HeirView heir = _view.NextHeir;
            RectTransform heirHead = MetaStyle.H2(_root, _view.NextHeirHeading, HeirX, LivesHeadY);
            RectTransform heirBox = Ui.Div(_root, "ocbg-meta-hall-heir", HeirX, HeirY, HeirW, HeirH);
            UiNode.Of(heirBox);
            float heirGap = HallParts.RowGap(HallParts.HeirCss), hy = 0f;
            RectTransform gen = HallParts.HeirGen(heirBox, heir, HeirW);
            UiNode.Of(gen).Place(0f, hy);
            hy += UiNode.Of(gen).H + heirGap;
            (RectTransform nameRow, List<RectTransform> nameChars, RectTransform seal) = HallParts.NameRow(heirBox, heir.Name, HeirW);
            UiNode.Of(nameRow).Place(0f, hy);
            hy += UiNode.Of(nameRow).H + heirGap;
            RectTransform attire = HallParts.Attire(heirBox, heir);
            UiNode.Of(attire).Place(0f, hy);
            hy += UiNode.Of(attire).H;
            RectTransform fig = HallParts.Figure(heirBox, HeirW);
            RectTransform survivor = null;
            if (_view.Survivor != null)
            {
                SurvivorView s = _view.Survivor;
                survivor = HallParts.Survivor(heirBox, _view.SurvivorHeading, s.Line, s.Lines, s.DurationLine, false, HeirW);
            }
            else if (_view.SurvivorSpent != null)
            {
                // The life that used the bonus has closed with no new survivor: the block says the bonus ended (R-14).
                SurvivorSpentView s = _view.SurvivorSpent;
                survivor = HallParts.Survivor(heirBox, _view.SurvivorHeading, s.Line, s.Lines, null, true, HeirW);
            }
            if (survivor)
            {
                // margin-top: auto: the box sits at the bottom of the column (or right under the 装束 when it does not fit)
                float sh = UiNode.Of(survivor).H;
                UiNode.Of(survivor).Place(0f, Math.Max(hy + heirGap, HeirH - sh));
            }

            MetaButton startBtn = MetaButton.Primary(_root, _view.StartButton, "Enter", StartX, StartY, StartW, StartH);
            startBtn.Click += () => Depart().Forget();
            _keys = MetaStyle.Keys(_root, "", KeysX, KeysY);
            PaintKeys();

            // ---- arrival and the naming ----
            _fade = Styler.Of(MetaStyle.Fade(_root), o: 1f);
            _sound.Play("hallArrive", 80);
            _clock.Tween(_fade, "o", 0f, 700f, Ease.Out);
            _clock.Tween(_backdrop, "v", 1f, 1600f, Ease.Out, 150f);   // the lamp: backdrop.alpha
            foreach (RectTransform el in new[] { title.rectTransform, lead.rectTransform })
            {
                Styler p = Styler.Of(el, o: 0f, y: 10f);
                _clock.Tween(p, new[] { ("o", 1f), ("y", 0f) }, 600f, Ease.Out, 250f);
            }
            var left = new List<RectTransform> { livesHead };
            left.AddRange(lifeCards);
            left.Add(rules);
            MetaUtil.RiseIn(_clock, left, delay: 380f, step: 90f);
            var middle = new List<RectTransform> { traceHead };
            middle.AddRange(_strata.Select(s => s.Root));
            MetaUtil.RiseIn(_clock, middle, delay: 480f, step: 60f, dy: 10f);
            Styler pd = Styler.Of(_detail, o: 0f, x: 16f);
            _clock.Tween(pd, new[] { ("o", 1f), ("x", 0f) }, 500f, Ease.Out, 900f);
            MetaUtil.RiseIn(_clock, new[] { heirHead, gen }, delay: 1000f, step: 80f);
            var attireIn = new List<RectTransform> { attire, fig };
            if (survivor) attireIn.Add(survivor);
            foreach (RectTransform el in attireIn) Styler.Of(el, o: 0f);
            for (int i = 0; i < nameChars.Count; i++)
            {
                Styler p = Styler.Of(nameChars[i], o: 0f, y: 14f);
                _clock.Tween(p, new[] { ("o", 1f), ("y", 0f) }, 420f, Ease.Out, MetaTimelines.HallNameCharAt(i));
            }
            int sealAt = MetaTimelines.SealAt(nameChars.Count);
            Styler ps = Styler.Of(seal, o: 0f, s: 1.9f, r: -14f);
            _clock.Wait(sealAt).Then(() =>
            {
                if (!_alive) return;
                _clock.Tween(ps, new[] { ("o", 1f), ("s", 1f), ("r", -4f) }, 170f, Ease.In).Then(() =>
                {
                    if (!_alive) return;
                    _sound.Play("heirNamed");
                    // seal.classList.add('is-stamped'): the ring spreads (@keyframes ocbg-meta-stamp, .7s ease-out)
                    CssAnimator.Play(seal, HallParts.SealStampedAnimation);
                    for (int i = 0; i < attireIn.Count; i++)
                    {
                        Styler p = Styler.Of(attireIn[i], o: 0f, y: 10f);
                        _clock.Tween(p, new[] { ("o", 1f), ("y", 0f) }, 520f, Ease.Out, MetaTimelines.HallAttireAfterStamp(i));
                    }
                });
            });
            Styler pb = startBtn.Styler(o: 0f, y: 12f);
            _clock.Tween(pb, new[] { ("o", 1f), ("y", 0f) }, 520f, Ease.Out, MetaTimelines.HallStartButtonAt(sealAt));
            Styler pk = Styler.Of(_keys.rectTransform, o: 0f);
            _clock.Tween(pk, "o", 1f, 600f, Ease.Out, MetaTimelines.HallKeysAt(sealAt));
            foreach (MetaButton el in _corner)
            {
                MetaButton b = el;
                Styler pg = b.Styler(o: 0f);
                _clock.Tween(pg, "o", 1f, 500f, Ease.Out, 600f).Then(() => b.ClearTransform());
            }

            _popKeys = KeyStack.Push(OnKey);
            _mounting = false;
        }

        // js: scenes/meta/hall.js:77
        /// <summary>mountHall(host, ctx) → { destroy() }.</summary>
        public static IScene Mount(RectTransform host, HallCtx ctx) => new HallScene(host, ctx);

        // js: scenes/meta/hall.js:124
        /// <summary>traceLineOf(life): the card's trace line (pastTraceLine).</summary>
        private string TraceLineOf(PastLifeView life) => MetaLines.PastTraceLine(_lineage, life);

        private HallStratum StratumOf(int? layer) =>
            layer.HasValue && layer.Value >= 1 && layer.Value <= _strata.Count ? _strata[layer.Value - 1] : null;

        private int IndexOfTrace(string id)
        {
            for (int i = 0; i < _view.Traces.Count; i++)
            {
                if (_view.Traces[i].Id == id) return i;
            }
            return -1;
        }

        // js: scenes/meta/hall.js:189, :197
        /// <summary>
        /// A mark's click: its own trace when it has one (mark.onclick), then the click goes on up to its stratum, which
        /// selects the band's trace unless the click was inside the band's own mark (the DOM's bubbling, kept as is).
        /// </summary>
        private void MarkClick(HallMark mark)
        {
            if (mark.TraceIndex >= 0) Select(mark.TraceIndex, true);
            HallStratum s = mark.Stratum;
            if (s != null && s.Live && s.LiveMark != mark) Select(s.TraceIndex, true);
        }

        // js: scenes/meta/hall.js:145
        /// <summary>edge(): .is-more-below / .is-more-above on the lives list (ScrollArea.MoreBelow / MoreAbove, the fades).</summary>
        private void Edge()
        {
            if (_lives) _lives.Refresh();
        }

        // js: scenes/meta/hall.js:219
        /// <summary>paintKeys(): the keys line for the traces there are and whether the candidates run past the panel.</summary>
        private void PaintKeys()
        {
            if (_keys) MetaUtil.SetText(_keys, MetaText.HallKeysFor(_view.Traces.Count > 1, TraceScrolls()));
        }

        // js: scenes/meta/hall.js:223
        /// <summary>drawTrace(): the trace section's body made again for `sel`; its stratum .is-sel; the keys line.</summary>
        private void DrawTrace()
        {
            Ui.Clear(_detail);   // detail.innerHTML = ''
            HallStratum selected = _sel >= 0 ? StratumOf(_view.Traces[_sel].Layer) : null;
            foreach (HallStratum s in _strata) s.SetSel(s == selected, instant: _mounting);
            _traceBody = HallParts.TraceBody(_detail, _view, _sel, _lineage);
            // A trace with more candidates than fit: the list fades at the edge it continues past, like the lives list
            // (R-12: every candidate is shown; the wheel, a drag or ↑↓ / PgUp PgDn read on).
            _traceBody.Area.Scrolled += TraceEdge;
            TraceEdge();
            PaintKeys();
        }

        // js: scenes/meta/hall.js:265
        /// <summary>traceEdge(): the trace body's fades.</summary>
        private void TraceEdge()
        {
            if (_traceBody == null || !_traceBody.Area) return;
            _traceBody.Area.Refresh();
        }

        // js: scenes/meta/hall.js:272
        /// <summary>traceScrolls(): the body's content runs more than 2 px past it.</summary>
        private bool TraceScrolls() =>
            _traceBody != null && _traceBody.Area && _traceBody.Area.ScrollHeight - _traceBody.Area.ClientHeight > ScrollArea.EdgeSlackPx;

        // js: scenes/meta/hall.js:273
        /// <summary>scrollTrace(by): the candidates scrolled by `by` px at once; whether they moved.</summary>
        private bool ScrollTrace(float by)
        {
            if (!TraceScrolls()) return false;
            ScrollArea a = _traceBody.Area;
            float before = a.ScrollTop;
            a.ScrollTop = Math.Max(0f, Math.Min(a.ScrollHeight - a.ClientHeight, before + by));
            TraceEdge();
            return Math.Abs(a.ScrollTop - before) > 0f;
        }

        // js: scenes/meta/hall.js:280
        /// <summary>select(i, byPointer): another trace into the panel (uiClick from the pointer, nodeHover from the keys).</summary>
        private void Select(int i, bool byPointer)
        {
            if (i == _sel || i < 0 || i >= _view.Traces.Count) return;
            _sel = i;
            _sound.Play(byPointer ? "uiClick" : "nodeHover");
            DrawTrace();
            Styler p = Styler.Of(_traceBody.Node, o: 0f, y: 8f);
            _clock.Tween(p, new[] { ("o", 1f), ("y", 0f) }, 220f, Ease.Out);
        }

        // js: scenes/meta/hall.js:381
        /// <summary>flames(now): each untaken mark's flame breathes (opacity 0.55〜1, scale 0.85〜1.10, 1900 ms, 700 ms apart).</summary>
        private void Flames(double now)
        {
            for (int i = 0; i < _flameEls.Count; i++)
            {
                UiNode f = _flameEls[i];
                if (!f) continue;
                f.Opacity = (float)MetaTimelines.HallFlameOpacity(now, i);
                f.Scale = (float)MetaTimelines.HallFlameScale(now, i);
            }
        }

        // js: scenes/meta/hall.js:389
        /// <summary>leaveToTitle(): uiCancel, the lamp down to .35, the curtain closes, then onTitle().</summary>
        private async Promise LeaveToTitle()
        {
            if (_busy || _ctx.OnTitle == null) return;
            _busy = true;
            _sound.Play("uiCancel");
            _clock.Tween(_backdrop, "v", 0.35f, 600f, Ease.InOut).Forget();
            await _clock.Tween(_fade, "o", 1f, 500f, Ease.In, 100f);
            if (_alive) _ctx.OnTitle();
        }

        // js: scenes/meta/hall.js:398
        /// <summary>
        /// depart(): uiConfirm; the life starts here (Inheritance.StartLife) unless the lineage has one in progress (null:
        /// the router resumes it); the lamp down, the curtain closes, then onDepart(lifeStart).
        /// </summary>
        private async Promise Depart()
        {
            if (_busy) return;
            _busy = true;
            _sound.Play("uiConfirm");
            LifeStart lifeStart = _lineage.Current != null ? null : Inheritance.StartLife(_lineage, _ctx.Seed);
            _clock.Tween(_backdrop, "v", 0.35f, 700f, Ease.InOut).Forget();
            await _clock.Tween(_fade, "o", 1f, 650f, Ease.In, 150f);
            if (_alive) _ctx.OnDepart?.Invoke(lifeStart);
        }

        // js: scenes/meta/hall.js:408
        private bool OnKey(KeyEvent e)
        {
            if (_busy) return false;
            int n = _view.Traces.Count;
            // ←→ go from trace to trace; ↑↓ and PgUp PgDn read the selected trace's list when it runs past the panel.
            if (e.Key == "ArrowRight") { if (n > 0) Select((_sel + 1) % n, false); return true; }
            if (e.Key == "ArrowLeft") { if (n > 0) Select((_sel - 1 + n) % n, false); return true; }
            if (e.Key == "ArrowDown" || e.Key == "ArrowUp") { ScrollTrace(e.Key == "ArrowDown" ? ScrollStep : -ScrollStep); return true; }
            if (e.Key == "PageDown" || e.Key == "PageUp")
            {
                float page = _traceBody != null && _traceBody.Area ? Math.Max(ScrollStep, _traceBody.Area.ClientHeight - ScrollStep) : 0f;
                ScrollTrace(e.Key == "PageDown" ? page : -page);
                return true;
            }
            if (e.Key == "Enter") { Depart().Forget(); return true; }
            // a held key repeats: only a fresh press opens an overlay (the overlays close on a fresh press only, too)
            if (e.Key == "Escape" && _ctx.OnSettings != null)
            {
                if (!e.Repeat)
                {
                    _sound.Play("uiClick");
                    _ctx.OnSettings();
                }
                return true;
            }
            if ((e.Key == "j" || e.Key == "J") && _ctx.OnJournal != null)
            {
                if (!e.Repeat) _ctx.OnJournal();
                return true;
            }
            return false;
        }

        // js: scenes/meta/hall.js:427
        public void Destroy()
        {
            _alive = false;
            _popKeys?.Invoke();
            _clock.Stop();
            _backdrop.Destroy();
            Ui.Remove(_root);
        }

        /// <summary>The JS scene has no refresh(): nothing to do when an overlay over it closes.</summary>
        public void Refresh() { }
    }
}
