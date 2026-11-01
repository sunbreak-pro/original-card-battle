// The exploration scene's constants (explore.md §1-§3): the stage layout of ExploreScene.js:47-56 and the DOM tree's
// z-order (explore.css), the tokens and fonts the scene reads, the time base (dur()'s 演出短縮 factor and the easings
// of stage.js and of the stylesheet), and the CSS of the elements the scene core builds (copied verbatim from
// explore.css so a reviewer can diff them). The parts keep the CSS of their own elements in their own files.
using System;
using System.Collections.Generic;
using DungeonCore;
using Exploration;
using Trial.Geometry;

namespace Trial.Explore
{
    public static class ExploreStyle
    {
        // ------------------------------------------------------------------ §1 layout (1920×1080 stage px)

        // js: scenes/explore/ExploreScene.js:47
        /// <summary>AREA: the map area (ExplorationScreenView MapWidth × MapHeight, centred in the map).</summary>
        public const float AreaCx = 652f, AreaCy = 548f, AreaW = 1060f, AreaH = 680f;

        // js: scenes/explore/ExploreScene.js:48
        public const float Node = 88f;

        // js: scenes/explore/ExploreScene.js:50
        /// <summary>WALKER_UP: the walker stands on the top edge of its node (clear of the icon and of the label above).</summary>
        public const float WalkerUp = Node / 2f + 6f;

        // js: scenes/explore/ExploreScene.js:51
        public const float WalkerBox = 52f;

        // js: scenes/explore/ExploreScene.js:53
        /// <summary>EDGE_INSET: where an edge line starts and ends, measured from a node's centre.</summary>
        public const float EdgeInset = Node / 2f + 8f;

        // js: scenes/explore/ExploreScene.js:54
        public const float DetailX = 1304f, DetailY = 112f, DetailW = 592f, DetailH = 604f;

        // js: scenes/explore/ExploreScene.js:55
        public const float CarriedX = 1304f, CarriedY = 728f, CarriedW = 592f, CarriedH = 232f;

        // js: scenes/explore/ExploreScene.js:56
        /// <summary>MAP_CENTER: the cards over the map centre here.</summary>
        public const float MapCenterX = 652f, MapCenterY = 540f;

        // js: scenes/explore/ExploreScene.js:258
        /// <summary>SEL: the selection frame's box (node and label).</summary>
        public const float SelW = Node + 44f, SelH = Node + 58f;

        // js: scenes/explore/ExploreScene.js:276
        public const float EndLifeX = 24f, EndLifeY = 980f, EndLifeW = 288f, EndLifeH = 68f;

        // js: scenes/explore/ExploreScene.js:277
        public const float HintX = 336f, HintY = 980f, HintW = 724f, HintH = 68f;

        // js: scenes/explore/ExploreScene.js:358
        /// <summary>HINT.gap: the room kept between the hint and the legend's first key.</summary>
        public const float HintGap = 28f;

        // js: scenes/explore/ExploreScene.js:279
        public const float LegendX = 1068f, LegendY = 976f, LegendW = 828f, LegendH = 76f;

        // js: scenes/explore/ExploreScene.js:359
        public const float LegendRight = LegendX + LegendW;

        /// <summary>explore.css:214 .ocbg-explore-legend gap.</summary>
        public const float LegendGap = 20f;

        /// <summary>explore.css:215 .ocbg-explore-legend span gap (between the keys and the label).</summary>
        public const float LegendItemGap = 6f;

        /// <summary>explore.css:26 the band: 0, 0, 1920 × 96.</summary>
        public const float BandH = 96f;

        public const float StageW = 1920f, StageH = 1080f;

        /// <summary>The node centre of a builder's map fraction (ExploreScene.js:410 nodePos).</summary>
        public static float NodeX(float x) => AreaCx + (x - 0.5f) * AreaW;

        public static float NodeY(float y) => AreaCy + (0.5f - y) * AreaH;

        // ------------------------------------------------------------------ the DOM's z-index (explore.css)

        /// <summary>.ocbg-explore-bdhost z-index (explore.css:15).</summary>
        public const int ZBackdrop = 0;
        /// <summary>.ocbg-explore-map (explore.css:68).</summary>
        public const int ZMap = 2;
        /// <summary>.ocbg-explore-detail (explore.css:134).</summary>
        public const int ZDetail = 3;
        /// <summary>.ocbg-explore-banner (explore.css:115).</summary>
        public const int ZBanner = 4;
        /// <summary>.ocbg-explore-lullview (explore.css:351).</summary>
        public const int ZLull = 4;
        /// <summary>.ocbg-explore-cards (explore.css:218).</summary>
        public const int ZCards = 5;
        /// <summary>.ocbg-explore-carried, -endlife, -hint, -legend (explore.css:181, 211, 212, 214).</summary>
        public const int ZCarried = 6, ZEndLife = 6, ZHint = 6, ZLegend = 6;
        /// <summary>.ocbg-explore-band (explore.css:26).</summary>
        public const int ZBand = 7;
        /// <summary>.ocbg-explore-overlays (explore.css:374).</summary>
        public const int ZOverlays = 8;

        // ------------------------------------------------------------------ class names (KIND_CLASS, STANDING_CLASS)

        // js: scenes/explore/ExploreScene.js:58
        /// <summary>KIND_CLASS: the node kind's class word (kind-battle …).</summary>
        public static string KindClass(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Battle: return "battle";
                case NodeKind.Elite: return "elite";
                case NodeKind.Boss: return "boss";
                case NodeKind.Rest: return "rest";
                case NodeKind.Survey: return "survey";
                case NodeKind.Event: return "event";
                case NodeKind.Trace: return "trace";
                case NodeKind.Carving: return "carving";
                default: return kind.ToString().ToLowerInvariant();
            }
        }

        // js: scenes/explore/ExploreScene.js:62
        /// <summary>STANDING_CLASS: the standing's state class (is-current …).</summary>
        public static string StandingClass(NodeStanding standing)
        {
            switch (standing)
            {
                case NodeStanding.Current: return "is-current";
                case NodeStanding.Reachable: return "is-reachable";
                case NodeStanding.Revisit: return "is-revisit";
                case NodeStanding.Resolved: return "is-resolved";
                case NodeStanding.Far: return "is-far";
                default: return "";
            }
        }

        // js: scenes/explore/ExploreScene.js:70
        /// <summary>STEP_RESULT: the sound a fresh node of this kind adds after the step (docs/AUDIO.md); null = none.</summary>
        public static string StepResult(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Survey: return "infoGained";
                case NodeKind.Event: return "eventResolve";
                case NodeKind.Carving: return "infoGained";
                default: return null;
            }
        }

        // js: scenes/explore/ExploreScene.js:72
        /// <summary>TRIAL_ACTIONS: trial mode's card buttons (the JS TrialAction enum; the C# keeps them in ScreenAction).</summary>
        public static readonly IReadOnlyCollection<ScreenAction> TrialActions = Array.AsReadOnly(new[]
        {
            ScreenAction.AcceptEvent, ScreenAction.DeclineEvent, ScreenAction.TakeFound, ScreenAction.LeaveFound, ScreenAction.ChooseSurvey,
        });

        public static bool IsTrialAction(ScreenAction a) =>
            a == ScreenAction.AcceptEvent || a == ScreenAction.DeclineEvent || a == ScreenAction.TakeFound ||
            a == ScreenAction.LeaveFound || a == ScreenAction.ChooseSurvey;

        /// <summary>The plate kinds (openPlate's `kind`).</summary>
        public const string PlateScreen = "screen", PlateChoice = "choice", PlateResult = "result", PlateTrace = "trace", PlateTrain = "train";

        // ------------------------------------------------------------------ §2 tokens and colours (vendor/ds/tokens.css, dusk)

        public static readonly Rgba Ground = DsTokens.Ground;
        public static readonly Rgba Panel = DsTokens.Panel;
        public static readonly Rgba PanelSolid = DsTokens.PanelSolid;
        public static readonly Rgba CardBg = DsTokens.Card;
        public static readonly Rgba Ink = DsTokens.Ink;
        public static readonly Rgba Rule = DsTokens.Rule;
        public static readonly Rgba Steel = DsTokens.Steel;
        public static readonly Rgba Amber = DsTokens.Amber;
        public static readonly Rgba Wick = DsTokens.Wick;
        public static readonly Rgba Crimson = DsTokens.Crimson;
        public static readonly Rgba HpText = DsTokens.HpText;
        public static readonly Rgba Violet = DsTokens.Violet;
        public static readonly Rgba Phosphor = DsTokens.Phosphor;
        public static readonly Rgba Caution = DsTokens.Caution;
        public static readonly Rgba Boss = DsTokens.Boss;
        public static readonly Rgba Washi = DsTokens.Washi;
        public static readonly Rgba WashiInk = DsTokens.WashiInk;

        /// <summary>color-mix(in srgb, X p%, transparent) = X at alpha p.</summary>
        public static Rgba Mix(Rgba x, float pct) => x.MulAlpha(pct / 100f);

        /// <summary>color-mix(in srgb, A p%, B): lerp(B, A, p) (both opaque here).</summary>
        public static Rgba Mix(Rgba a, float pct, Rgba b)
        {
            float t = pct / 100f;
            return new Rgba(b.R + (a.R - b.R) * t, b.G + (a.G - b.G) * t, b.B + (a.B - b.B) * t, b.A + (a.A - b.A) * t);
        }

        /// <summary>The sealed run of a layer name (explore.css:8): ink at 34%; on washi (explore.css:9): washi-ink at 38%.</summary>
        public static readonly Rgba Sealed = Mix(DsTokens.Ink, 34f);
        public static readonly Rgba SealedOnWashi = Mix(DsTokens.WashiInk, 38f);

        /// <summary>explore.css:8 .ocbg-explore-sealed letter-spacing.</summary>
        public const float SealedSpacingEm = 0.06f;

        // ------------------------------------------------------------------ §3 time base and easings

        // js: scenes/explore/ExploreScene.js:188
        /// <summary>dur(): JS timers shrink to 35 % with 演出短縮 or reduced motion (CSS transitions and keyframes keep their length).</summary>
        public const float ShortFxFactor = 0.35f;

        /// <summary>stage.js EASE.out = 1 − (1 − t)³.</summary>
        public static readonly EaseFn Out = Ease.Out;
        /// <summary>stage.js EASE.inout (cubic in-out).</summary>
        public static readonly EaseFn InOut = Ease.InOut;
        /// <summary>stage.js EASE.smooth = t²(3 − 2t).</summary>
        public static readonly EaseFn Smooth = Ease.Smooth;
        /// <summary>CSS `ease` (a transition with no function given).</summary>
        public static readonly EaseFn CssEase = Ease.Css;
        public static readonly EaseFn CssOut = Ease.CssOut;
        public static readonly EaseFn CssIn = Ease.CssIn;
        public static readonly EaseFn CssInOut = Ease.CssInOut;
        /// <summary>cubic-bezier(.22,.61,.36,1): the scene's "decel".</summary>
        public static readonly EaseFn Decel = Ease.Decel;
        /// <summary>cubic-bezier(.4,0,.2,1): the scene's "std".</summary>
        public static readonly EaseFn Std = Ease.Standard;

        public const string DecelCss = "cubic-bezier(.22,.61,.36,1)";
        public const string StdCss = "cubic-bezier(.4,0,.2,1)";

        // ------------------------------------------------------------------ the core's CSS (explore.css, verbatim)

        /// <summary>explore.css:5 .ocbg-explore.</summary>
        public const string RootCss = "position: absolute; inset: 0; overflow: hidden; background: var(--ground); color: var(--ink); font-family: var(--font-body);";

        /// <summary>explore.css:10 .ocbg-explore.is-returning.</summary>
        public const string ReturningAnim = "ocbg-explore-fade 420ms ease-out both";

        /// <summary>explore.css:6 .ocbg-explore .ocbg-explore-num.</summary>
        public const string NumCss = "font-family: var(--font-mono); font-weight: 600; letter-spacing: 0;";

        /// <summary>explore.css:11 .ocbg-explore kbd (border-box: .ocb-root * in bundle.css:3).</summary>
        public const string KbdCss = "display: inline-flex; align-items: center; justify-content: center; min-width: 24px; height: 24px; padding: 0 6px; font: 600 16px/1 var(--font-mono); color: var(--ink); border: 1px solid var(--steel); border-radius: var(--radius-tag); background: var(--panel-solid);";

        /// <summary>explore.css:68 .ocbg-explore-map.</summary>
        public const string MapCss = "position: absolute; inset: 0; z-index: 2; pointer-events: none; overflow: hidden; transition: opacity 500ms ease-in-out, filter 400ms ease-in-out;";

        /// <summary>explore.css:70 .ocbg-explore.is-engaging .ocbg-explore-map.</summary>
        public const string MapEngagingCss = "filter: brightness(.55);";

        /// <summary>explore.css:370 .ocbg-explore.is-interlude .ocbg-explore-map / -detail / -banner.</summary>
        public const string InterludeHiddenCss = "opacity: 0; pointer-events: none;";

        /// <summary>explore.css:134 .ocbg-explore-detail's transition (its look is the panel unit's).</summary>
        public const string DetailTransition = "opacity 400ms ease-in-out";

        /// <summary>explore.css:211 .ocbg-explore-endlife (over .ocbg-btn).</summary>
        public const string EndLifeCss = "z-index: 6; font-size: 22px; letter-spacing: .08em;";

        /// <summary>explore.css:212 .ocbg-explore-hint.</summary>
        public const string HintCss = "z-index: 6; display: flex; align-items: center; font: 400 20px/30px var(--font-body); text-shadow: 0 0 8px var(--ground), 0 1px 2px var(--ground); transition: opacity 400ms;";

        /// <summary>explore.css:213 .ocbg-explore-hint &gt; span.</summary>
        public const string HintSpanCss = "text-wrap: balance;";

        /// <summary>explore.css:214 .ocbg-explore-legend.</summary>
        public const string LegendCss = "z-index: 6; display: flex; align-items: center; justify-content: flex-end; gap: 20px; font: 400 16px/22px var(--font-body); white-space: nowrap; text-shadow: 0 0 6px var(--ground); transition: opacity 400ms; pointer-events: none;";

        /// <summary>explore.css:215 .ocbg-explore-legend span.</summary>
        public const string LegendSpanCss = "display: inline-flex; align-items: center; gap: 6px; white-space: nowrap;";

        /// <summary>explore.css:351 .ocbg-explore-lullview.</summary>
        public const string LullViewCss = "position: absolute; inset: 0; z-index: 4; pointer-events: none;";

        /// <summary>explore.css:218 .ocbg-explore-cards.</summary>
        public const string CardsCss = "position: absolute; inset: 0; z-index: 5; pointer-events: none;";

        /// <summary>explore.css:374 .ocbg-explore-overlays.</summary>
        public const string OverlaysCss = "position: absolute; inset: 0; z-index: 8; pointer-events: none;";

        /// <summary>explore.css:375 .ocbg-explore-ovshade.</summary>
        public const string OvShadeCss = "position: absolute; inset: 0; pointer-events: auto; background: color-mix(in srgb, var(--ground) 70%, transparent); animation: ocbg-explore-fade 200ms ease-out both;";
        public const string OvShadeAnim = "ocbg-explore-fade 200ms ease-out both";

        /// <summary>explore.css:376 .ocbg-explore-journal (the fallback journal drawer).</summary>
        public const string JournalCss = "position: absolute; right: 0; top: 0; width: 576px; height: 1080px; pointer-events: auto; display: flex; flex-direction: column; gap: 14px; padding: 28px 32px; color: var(--washi-ink); background: var(--washi); box-shadow: var(--shadow-tip); animation: ocbg-explore-drawer 220ms cubic-bezier(.22,.61,.36,1) both;";
        public const string JournalInAnim = "ocbg-explore-drawer 220ms cubic-bezier(.22,.61,.36,1) both";
        /// <summary>explore.css:377 .ocbg-explore-journal.is-leaving.</summary>
        public const string JournalOutAnim = "ocbg-explore-drawer-out 200ms ease-in both";

        /// <summary>explore.css:380-382 .ocbg-explore-jhead (h2, .ocbg-btn).</summary>
        public const string JHeadH2Css = "margin: 0; font: 800 36px/44px var(--font-display);";
        public const string JHeadBtnCss = "font-size: 18px; padding: 10px 18px;";

        /// <summary>explore.css:384-386 .ocbg-explore-jtabs button (and .is-on, [disabled]).</summary>
        public const string JTabCss = "padding: 6px 14px; font: 700 16px/22px var(--font-body); color: var(--washi-ink); background: color-mix(in srgb, var(--washi-ink) 8%, transparent); border: 1px solid color-mix(in srgb, var(--washi-ink) 30%, transparent); border-radius: var(--radius-tag); cursor: pointer;";
        public const string JTabOnCss = "background: var(--panel-solid); color: var(--ink);";
        public const string JTabDisabledCss = "opacity: .45; cursor: default;";

        /// <summary>explore.css:388-391 .ocbg-explore-jbody h3 / h3 small / h4 / p.</summary>
        public const string JBodyH3Css = "margin: 6px 0 12px; display: flex; align-items: baseline; justify-content: space-between; font: 700 28px/36px var(--font-display);";
        public const string JBodyH3SmallCss = "font: 400 18px/24px var(--font-body);";
        public const string JBodyH4Css = "margin: 14px 0 2px; font: 700 18px/24px var(--font-body);";
        public const string JBodyPCss = "margin: 0 0 6px; font: 400 18px/28px var(--font-body);";

        /// <summary>explore.css:392 .ocbg-explore-ovpage (the fallback 所持カード / 設定 page).</summary>
        public const string OvPageCss = "position: absolute; left: 430px; top: 150px; width: 1060px; height: 780px; pointer-events: auto; display: flex; flex-direction: column; gap: 18px; padding: 36px 44px; background: var(--panel-solid); border: 1px solid var(--steel); border-radius: var(--radius-panel); box-shadow: var(--shadow-tip); animation: ocbg-explore-plate-in 260ms cubic-bezier(.22,.61,.36,1) both;";
        public const string OvPageInAnim = "ocbg-explore-plate-in 260ms cubic-bezier(.22,.61,.36,1) both";
        /// <summary>explore.css:393 .ocbg-explore-ovpage.is-leaving.</summary>
        public const string OvPageOutAnim = "ocbg-explore-plate-out 200ms ease-in both";
        /// <summary>explore.css:394 .ocbg-explore-ovlines p.</summary>
        public const string OvLinesPCss = "margin: 0 0 8px; font: 400 22px/34px var(--font-body);";

        /// <summary>explore.css:397-398: the shared confirm card inside this scene (pre-line body, the plate's entry).</summary>
        public const string ConfirmCardAnim = "ocbg-explore-plate-in 240ms cubic-bezier(.22,.61,.36,1) both";
    }
}
