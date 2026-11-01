// One card face of the battle's hand (CardView.EnsureParts + Bind, docs/BATTLE_SCENE.md §6a): the design system's
// .ocb-card (bundle.css:15-52) and the scene's additions (hand.css:11-20, 54-64), the value icons and colours, the status
// marks beside the values, the hover line's breaks, the face key and the right-click detail. Port of
// scenes/battle/hand-card.js.
//
// The face is the DS card (Kit/Ds/Ocb.Card.cs) built part by part in its paint order, with what the hand card does
// differently (meta-b.md §2.3): the name, the description rows and the trait rows fitted by the ported CardTextFit
// (min 14 px), the cost circle's 「▼n」 centred on its lower right, at most two value columns, a move card in the card's
// steel, the status marks (modsOf), the trait terms underlined with a box the hand reads for the term tip, and the
// right-click detail with its 「今」 rows. It binds a CardFace and prints what the script wrote: no rule, no number of its
// own. Text is measured with TMP (what is measured is what is drawn), where the JS measures on a canvas.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Depiction;
using TMPro;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;
using UnityEngine.UI;
using FitMeasure = Depiction.TextMeasure;
using KitMeasure = Trial.Kit.TextMeasure;

namespace Trial.Battle
{
    /// <summary>modsOf's rows: { icon, text, word } — a status mark beside a card's values (集中 「→」, 強化 「×1.5」, 威圧 「−3」).</summary>
    // js: scenes/battle/hand-card.js:173
    public sealed class CardMod
    {
        /// <summary>The status icon ('focus' | 'empower' | 'intimidate').</summary>
        public string Icon = "";
        /// <summary>The mark's text ('→', '×1.5', '−3').</summary>
        public string Text = "";
        /// <summary>The status word (集中 / 強化 / 威圧), the mark's title.</summary>
        public string Word = "";

        public CardMod() { }

        public CardMod(string icon, string text, string word)
        {
            Icon = icon;
            Text = text;
            Word = word;
        }
    }

    /// <summary>buildDetail's `extra` (detailExtra, hand.js:152): { lit, preview, mods } — what the detail's 「今」 rows say.</summary>
    // js: scenes/battle/hand-card.js:384
    public sealed class DetailExtra
    {
        /// <summary>The card's lamp is lit now.</summary>
        public bool Lit;
        /// <summary>The source's prediction ('' for none).</summary>
        public string Preview = "";
        /// <summary>The status marks that apply (modsOf).</summary>
        public IReadOnlyList<CardMod> Mods = Array.Empty<CardMod>();
    }

    /// <summary>
    /// One underlined term on a card's trait row (the JS span with its __mark): the mark the script gave and a RectTransform
    /// over the underlined characters (the span's box: the hand reads its stage box for the term tip, hand.js:319-330).
    /// </summary>
    public sealed class CardTerm
    {
        /// <summary>term.__mark (CardFace.Terms' row).</summary>
        public TermMark Mark;
        /// <summary>The span's box (a transparent RectTransform over the underlined characters).</summary>
        public RectTransform Rect;
    }

    /// <summary>
    /// buildCard's answer: { el, lit, terms, setCost, setLit, setDim, setMods } (hand-card.js:284). The lamp, the cost circle,
    /// the dark sheet and the status marks change in place; everything else is a new face (faceKey).
    /// </summary>
    // js: scenes/battle/hand-card.js:284
    public sealed class CardFaceView
    {
        /// <summary>el: the face (.ocb-card, 216×304), its top left at (0, 0) of its parent.</summary>
        public RectTransform Root;

        /// <summary>The face's UiNode.</summary>
        public UiNode Node => Root ? UiNode.Of(Root) : null;

        /// <summary>card.lit: the lamp is on (only a face with a non-plain trait box lights).</summary>
        public bool Lit;

        /// <summary>card.terms: the underlined terms of the trait rows.</summary>
        public IReadOnlyList<CardTerm> Terms = Array.Empty<CardTerm>();

        /// <summary>.ocb-lamp: the lamp (hand.js traitSignal swells it), or null for a face with no lamp.</summary>
        public RectTransform Lamp;

        /// <summary>.ocb-trait: the trait box (hand.js traitSignal stands the condition word over it), or null.</summary>
        public RectTransform TraitBox;

        /// <summary>The face's own state (the DS card's parts, the lamp's verdict, the shown cost, the marks), on its root.</summary>
        private HandFaceState State => Root ? Root.GetComponent<HandFaceState>() : null;

        /// <summary>
        /// setCost(cost, drop): several enemies (port notes 「Holding a card」, review fix) — while the card frames enemy k the
        /// circle reads CardFace.CostFor(k) / CostDropFor(k); at rest the face's own Cost / CostDrop. Redrawn only on a change.
        /// </summary>
        // js: scenes/battle/hand-card.js:292
        public void SetCost(int cost, int drop)
        {
            HandFaceState s = State;
            if (!s) return;
            string key = cost.ToString(CultureInfo.InvariantCulture) + "/" + drop.ToString(CultureInfo.InvariantCulture);
            if (key == s.CostShown || s.Card == null || s.Card.CostEl == null) return;
            s.CostShown = key;
            s.FillCost(cost, drop);
        }

        /// <summary>setLit(on): the lamp and the lit frame (CardView.ShowLamp: a plain card and a card with no trait box never light).</summary>
        // js: scenes/battle/hand-card.js:298
        public void SetLit(bool on)
        {
            HandFaceState s = State;
            Lit = on && s && s.HasLamp;
            if (!s || s.Card == null) return;
            // el.classList.toggle('is-lit') and the lamp's .is-on (restyled only on a change: a toggle to the same class is free)
            if (s.Card.Lit != Lit || (s.Card.Lamp != null && s.Card.LampOn != Lit)) s.Card.SetLamp(Lit);
        }

        /// <summary>setDim(alpha): the dark sheet's opacity, clamped 0..1.</summary>
        // js: scenes/battle/hand-card.js:303
        public void SetDim(float alpha)
        {
            HandFaceState s = State;
            if (!s || s.Card == null) return;
            s.Card.DimOpacity = Math.Max(0f, Math.Min(1f, alpha));
        }

        /// <summary>setMods(mods): the status marks beside the values (modsOf), redrawn only when they change.</summary>
        // js: scenes/battle/hand-card.js:307
        public void SetMods(IReadOnlyList<CardMod> mods)
        {
            HandFaceState s = State;
            if (!s || !s.ModsBox) return;
            IReadOnlyList<CardMod> list = mods ?? Array.Empty<CardMod>();
            string key = string.Join(",", list.Select(m => m.Icon));
            if (key == s.ModsKey) return;
            s.ModsKey = key;
            s.FillMods(list);
        }
    }

    // js: scenes/battle/hand-card.js
    public static class HandCard
    {
        // ---- CardView's §6.1 measures (px from the card's top-left) ---------------------------------------

        // js: scenes/battle/hand-card.js:15
        public const float CardW = 216f;

        // js: scenes/battle/hand-card.js:16
        public const float CardH = 304f;

        private const int NameSize = 22; // js: scenes/battle/hand-card.js:17
        private const float NameBoxW = CardW - 16f - 12f; // js: scenes/battle/hand-card.js:18 (NameAt (16, 60), right 12)
        private const float NameBoxH = 28f; // js: scenes/battle/hand-card.js:18 (height 28)
        private const int TextSize = 16; // js: scenes/battle/hand-card.js:19
        private const float TextLine = 22f; // js: scenes/battle/hand-card.js:20 (CardView.TextLine)
        private const float TextAt = 156f; // js: scenes/battle/hand-card.js:21 (TextAt.y)
        private const float ValuesAt = 94f; // js: scenes/battle/hand-card.js:22 (ValuesAt.y: the lasting rows start here)
        private const float TextW = CardW - 16f - 12f; // js: scenes/battle/hand-card.js:23 (188)

        /// <summary>
        /// CardView.MinFontSize is 12; the scene stops at 14 (undecided.md 5, 案 B「14 px で止める」), so a card's text keeps to
        /// the readable sizes at 1280×720 and a row that would go smaller wraps instead (CardTextFit's own next step).
        /// </summary>
        private const int MinFont = 14; // js: scenes/battle/hand-card.js:28

        /// <summary>Trait rows (§6.1 特性の箱, 161 px of text at 16): Unity lets a long row run over; the scene shrinks it, but not under 14.</summary>
        private const float TraitTextW = CardW - 12f - 7f - (8f + 16f + 8f) - 4f; // js: scenes/battle/hand-card.js:30

        private const int TraitMinFont = MinFont; // js: scenes/battle/hand-card.js:31

        /// <summary>The factor of 威圧's mark: BattleCore's Constants.IntimidatePenalty (no bridge hands it out yet; see the hand-over).</summary>
        private const int IntimidatePenalty = 3;

        // ---- hand.css, verbatim (the .ocbg-battle-face rules over the DS card) ----------------------------------

        /// <summary>hand.css:12 <c>.ocbg-battle-face .ocb-card-name</c></summary>
        private const string NameCss = "overflow: hidden;";

        /// <summary>hand.css:13 <c>.ocbg-battle-face .ocbg-battle-desc</c></summary>
        private const string DescCss = "white-space: pre; overflow: hidden; line-break: auto; overflow-wrap: normal;";

        /// <summary>hand.css:14 <c>.ocbg-battle-face .ocbg-battle-desc-line</c></summary>
        private const string DescLineCss = "white-space: pre;";

        /// <summary>hand.css:15 <c>.ocbg-battle-face .ocbg-battle-lasting</c></summary>
        private const string LastingCss = "color: var(--hp-text);";

        /// <summary>hand.css:38 <c>.ocbg-battle-detail</c></summary>
        private const string DetailCss = "position: absolute; left: 0; top: 0; pointer-events: none; overflow: hidden;";

        /// <summary>hand.css:43 <c>.ocbg-battle-detail.ocb-detail</c></summary>
        private const string DetailSizeCss = "height: auto; min-height: 280px; max-height: 600px;";

        /// <summary>hand.css:44 <c>.ocbg-battle-detail .ocbg-battle-detail-text</c></summary>
        private const string DetailTextCss = "max-height: 264px; overflow: hidden;";

        /// <summary>hand.css:45 <c>.ocbg-battle-detail .ocbg-battle-detail-terms</c></summary>
        private const string DetailTermsCss = "position: relative; box-sizing: content-box; max-height: 330px; overflow: hidden; flex: none; padding-bottom: 0; border-bottom: 8px solid transparent;";

        /// <summary>hand.css:46 <c>.ocbg-battle-detail .ocbg-battle-detail-terms b</c></summary>
        private const string DetailTermsBCss = "margin-right: 0;";

        /// <summary>hand.css:57 <c>.ocbg-battle-detail .ocbg-battle-detail-now</c></summary>
        private const string DetailNowCss = "margin-top: 8px; display: flex; flex-direction: column; gap: 2px; font: 400 16px/22px var(--font-body); color: var(--ink);";

        /// <summary>hand.css:58 <c>.ocbg-battle-detail .ocbg-battle-detail-now p</c></summary>
        private const string DetailNowPCss = "margin: 0;";

        /// <summary>hand.css:59 <c>.ocbg-battle-detail .ocbg-battle-detail-now span</c></summary>
        private const string DetailNowSpanCss = "display: inline-block; min-width: 72px; margin-right: 8px; color: var(--ink-muted);";

        // ---- the maps ---------------------------------------------------------------------------------------

        /// <summary>BattleTheme.KindFrame / KindInk through the DS's attribute classes (.ocb-card.is-…). A move card keeps the card's steel.</summary>
        // js: scenes/battle/hand-card.js:34
        private static string KindClass(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.Attack: return "is-attack";
                case CardKind.Guard: return "is-defense";
                case CardKind.Skill: return "is-skill";
                case CardKind.Stance: return "is-stance";
                default: return "is-move";
            }
        }

        // js: scenes/battle/hand-card.js:35
        private static string KindInk(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.Attack: return "var(--phosphor)";
                case CardKind.Guard: return "var(--guard)";
                case CardKind.Skill: return "var(--amber)";
                case CardKind.Stance: return "var(--hp-text)";
                default: return "var(--ink)";
            }
        }

        /// <summary>VisualArt.KindIcon: sword, shield, flag; a skill (and a move) the star.</summary>
        // js: scenes/battle/hand-card.js:37
        private static string KindIcon(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.Attack: return "sword";
                case CardKind.Guard: return "shield";
                case CardKind.Stance: return "flag";
                default: return "star";
            }
        }

        /// <summary>valueIcon(kind) — VisualArt.ValueIcon (§8).</summary>
        // js: scenes/battle/hand-card.js:40
        public static string ValueIcon(CardValueKind kind)
        {
            switch (kind)
            {
                case CardValueKind.Power: return "sword";
                case CardValueKind.Guard: return "shield";
                case CardValueKind.Heal: return "heal";
                case CardValueKind.Close:
                case CardValueKind.Push: return "forward";
                case CardValueKind.Back:
                case CardValueKind.Pull: return "back";
                default: return "draw";
            }
        }

        /// <summary>valueColor(kind) — VisualArt.ValueColor: the CSS colour ('var(--phosphor)').</summary>
        // js: scenes/battle/hand-card.js:52
        public static string ValueColor(CardValueKind kind)
        {
            switch (kind)
            {
                case CardValueKind.Power: return "var(--phosphor)";
                case CardValueKind.Guard: return "var(--guard)";
                case CardValueKind.Heal: return "var(--hp-text)";
                default: return "var(--ink)";
            }
        }

        /// <summary>The colour a CSS colour string resolves to in the dusk theme.</summary>
        private static Rgba InkOf(string css) => CssColor.Parse(css, Ocb.Theme);

        /// <summary>
        /// The icon beside the predicted number. Unity (DepictionPlayer.ShowTargetMark) takes Values[0], which on a card
        /// that moves first (足捌き・後: ← 1, Guard 12) puts the move's arrow beside the Guard. The scene picks the value
        /// the number is (battle_ui_ux_v2 §3.3: on a self card Guard → 回復 → ドロー, on an enemy the damage), and keeps
        /// Unity's choice for anything else.
        /// </summary>
        private static readonly CardValueKind[] SelfOrder = { CardValueKind.Guard, CardValueKind.Heal, CardValueKind.Draw }; // js: scenes/battle/hand-card.js:67

        /// <summary>
        /// previewKind(face): the icon beside the predicted number — on a self card Guard → 回復 → ドロー (a stance with no
        /// value: Guard), on an enemy the damage (Power), else Values[0]; with no value Guard / Power by Kind.
        /// </summary>
        // js: scenes/battle/hand-card.js:68
        public static CardValueKind PreviewKind(CardFace face)
        {
            if (face == null) return CardValueKind.Power;
            List<CardValue> values = face.Values ?? new List<CardValue>();
            if (face.Aim == CardAim.Self)
            {
                foreach (CardValueKind k in SelfOrder)
                    if (values.Any(v => v != null && v.Kind == k)) return k;
                // A stance's number is the Guard its trait puts up (岩の構え: 置く時 予兆が攻撃 → Guard +3).
                if (values.Count == 0) return CardValueKind.Guard;
            }
            else if (values.Any(v => v != null && v.Kind == CardValueKind.Power)) return CardValueKind.Power;
            if (values.Count > 0 && values[0] != null) return values[0].Kind;
            return face.Kind == CardKind.Guard ? CardValueKind.Guard : CardValueKind.Power;
        }

        // ---- measuring (Unity's Text.preferredWidth / preferredHeight with the font in use) -----------------

        private static readonly Dictionary<string, float> WidthCache = new Dictionary<string, float>(StringComparer.Ordinal); // js: scenes/battle/hand-card.js:81

        /// <summary>A measuring style per face and size (letter-spacing 0, every space kept: the canvas measures the string as given).</summary>
        private static readonly Dictionary<long, TextStyle> MeasureStyles = new Dictionary<long, TextStyle>();

        private static TextStyle MeasureStyle(Family family, int weight, int size)
        {
            long key = ((long)(int)family << 40) | ((long)weight << 20) | (uint)size;
            if (MeasureStyles.TryGetValue(key, out TextStyle s)) return s;
            s = new TextStyle { Family = family, Weight = weight, Size = size, LineHeight = size, WhiteSpace = WhiteSpace.Pre };
            MeasureStyles[key] = s;
            return s;
        }

        // js: scenes/battle/hand-card.js:88
        private static float TextWidth(string text, Family family, int weight, int size)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            string key = ((int)family).ToString(CultureInfo.InvariantCulture) + "|" + weight.ToString(CultureInfo.InvariantCulture) + "|"
                + size.ToString(CultureInfo.InvariantCulture) + "\u0000" + text;
            if (WidthCache.TryGetValue(key, out float hit)) return hit;
            float w = KitMeasure.Width(text, MeasureStyle(family, weight, size));
            // A width measured before the fonts arrived would be the fallback's; keep it only once they are in.
            if (TrialFonts.Available(family, weight)) WidthCache[key] = w;
            return w;
        }

        // js: scenes/battle/hand-card.js:99
        private static string[] Lines(string text) => (text ?? "").Split('\n');

        /// <summary>CardTextFit's TextMeasure for one face: the widest line, and the height the given line pitch gives.</summary>
        // js: scenes/battle/hand-card.js:101
        private static FitMeasure MeasureWith(int weight, Family family, float pitch)
        {
            return (string text, int size, out float width, out float height) =>
            {
                string[] ls = Lines(text);
                float w = 0f;
                foreach (string l in ls) w = Math.Max(w, TextWidth(l, family, weight, size));
                width = w;
                height = ls.Length * size * pitch;
            };
        }

        private sealed class FaceMeasures
        {
            public FitMeasure Name, Desc, TraitTop, TraitBottom;
        }

        private static FaceMeasures _measures; // js: scenes/battle/hand-card.js:109

        // js: scenes/battle/hand-card.js:110
        private static FaceMeasures Fonts()
        {
            if (_measures == null)
            {
                _measures = new FaceMeasures
                {
                    // The name is one line in its 28 px box (22 px over a 28 px pitch).
                    Name = MeasureWith(700, Family.Display, 28f / 22f),
                    // CardView: lineSpacing = TextLine / (TextSize × 1.15), so a line is size × 22 / 16 at any size.
                    Desc = MeasureWith(400, Family.Body, TextLine / TextSize),
                    TraitTop = MeasureWith(400, Family.Body, 22f / 16f),
                    TraitBottom = MeasureWith(700, Family.Body, 22f / 16f),
                };
            }
            return _measures;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            WidthCache.Clear();
            MeasureStyles.Clear();
            _measures = null;
        }

        // ---- the face -----------------------------------------------------------------------------------

        // js: scenes/battle/hand-card.js:128
        private static void CostPart(HandFaceState st, CardFace face)
        {
            st.FillCost(face.Cost, face.CostDrop);
        }

        // js: scenes/battle/hand-card.js:140
        private static void ReachPart(OcbCard card, CardFace face)
        {
            // CardView.BindReach: the number tag (⊢⊣ 1〜2), the self mark (人型 + 自分), or nothing.
            string glyph = face.RequiredRangeGlyph ?? "";
            bool aims = face.Aim == CardAim.Single || glyph.Length > 0;
            if (aims && glyph.Length > 0) { card.ReachPart(false, glyph); return; }
            if (face.Aim == CardAim.Self) card.ReachPart(true, null); // scene text (CardView "自分"; 17 px on the game's stage, common.css:23)
        }

        // js: scenes/battle/hand-card.js:149
        private static OcbElement ValuesPart(OcbCard card, CardFace face, out List<OcbCardValue> columns)
        {
            columns = null;
            List<CardValue> values = face.Values;
            // A v4.2 source writes one value string only: it stands as the one column (CardView.BindValues).
            if ((values == null || values.Count == 0) && !string.IsNullOrEmpty(face.ValueText) && !face.Lasting)
            {
                values = new List<CardValue>
                {
                    new CardValue { Kind = face.Kind == CardKind.Guard ? CardValueKind.Guard : CardValueKind.Power, Number = face.ValueText, Word = "" },
                };
            }
            if (values == null || values.Count == 0) return null;
            columns = new List<OcbCardValue>();
            foreach (CardValue v in values.Take(2)) // CardFaceLimits.Values
            {
                if (v == null) continue;
                // --val-ink: the value's colour (the number's ink), the icon in it too
                columns.Add(new OcbCardValue(ValueIcon(v.Kind), v.Number ?? "", v.Word ?? "", InkOf(ValueColor(v.Kind))));
            }
            return card.ValuesPart(columns);
        }

        /// <summary>
        /// modsOf(face, marks): the status marks a card carries while the player holds 集中 / 強化 / 威圧 (review 21) — 集中 「→」 on
        /// every card with a value, 強化 「×EmpowerMult」 on a card with a Power value, 威圧 「−IntimidatePenalty」 on a Power or
        /// Guard value. The factors are the core's (no `Constants.` here: the View rules boundary — read them through a bridge).
        /// </summary>
        // js: scenes/battle/hand-card.js:173
        public static IReadOnlyList<CardMod> ModsOf(CardFace face, StatusMarks marks)
        {
            if (face == null || marks == null) return Array.Empty<CardMod>();
            List<CardValue> values = face.Values ?? new List<CardValue>();
            if (values.Count == 0) return Array.Empty<CardMod>();
            bool Has(CardValueKind k) => values.Any(v => v != null && v.Kind == k);
            var output = new List<CardMod>();
            if (marks.Focus) output.Add(new CardMod("focus", "→", "集中")); // scene text (§3.5 「→」)
            if (marks.Empower && Has(CardValueKind.Power))
                output.Add(new CardMod("empower", "×" + BattlePlayer.CoreNumbers.EmpowerMult.ToString(CultureInfo.InvariantCulture), "強化"));
            if (marks.Intimidate && (Has(CardValueKind.Power) || Has(CardValueKind.Guard)))
                output.Add(new CardMod("intimidate", "−" + IntimidatePenalty.ToString(CultureInfo.InvariantCulture), "威圧"));
            return output;
        }

        // js: scenes/battle/hand-card.js:195
        private static void DescriptionPart(OcbCard card, CardFace face)
        {
            // §6.1 スタンスの札: the lasting rows start where the value columns would, and take four lines.
            bool lasting = CardTextFit.PrintsLasting(face);
            float boxH = TextLine * (lasting ? 4 : 3);
            List<string> rows = CardTextFit.DescriptionRows(face);
            TextFit fit = CardTextFit.Description(rows, TextSize, MinFont, TextW, boxH, Fonts().Desc);
            float pitch = fit.Size * TextLine / TextSize;
            // the DS's .ocb-desc, with the inline top / height / font-size / line-height of the fit
            OcbElement el = card.DescPart("");
            Ui.Clear(el.Root);
            card.DescLabels.Clear();
            el.Root.name = "ocb-desc ocbg-battle-desc";
            el.Node.Place(16f, lasting ? ValuesAt : TextAt, TextW, boxH);
            if (string.IsNullOrEmpty(fit.Text)) return;
            string css = Ocb.DescCss + (card.IsStance ? Ocb.StanceDescCss : "") + DescCss
                + "font-size: " + Ocb.Num(fit.Size) + "px; line-height: " + Ocb.Num(pitch) + "px;" + DescLineCss;
            TextStyle ts = Ocb.TextOf(css, card.Vars);
            TextStyle mark = Ocb.TextOf(css + LastingCss, card.Vars);
            // overflow: hidden: a fit that ran out of sizes cuts its lines at the box (TMP cuts whole characters)
            if (!fit.Fits) ts = ts.WithOverflow(TextOverflow.Clip);
            float y = 0f;
            foreach (string line in Lines(fit.Text))
            {
                // .ocbg-battle-desc-line: one block line box of `pitch` (an empty line has no line box)
                if (line.Length == 0) continue;
                RectTransform row = Ocb.El(el.Root, "ocbg-battle-desc-line");
                UiNode.Of(row).Place(0f, y, TextW, pitch);
                // The ∞ at the head of a lasting row takes the HP colour (CardView's rich text), printed as the very
                // character the fit measured, so a row is never wider than CardTextFit allowed.
                if (lasting && line.StartsWith(CardTextFit.LastingMark, StringComparison.Ordinal))
                {
                    string rest = line.Substring(CardTextFit.LastingMark.Length);
                    TextMeshProUGUI inf = Label.Make(row, CardTextFit.LastingMark, mark, "ocbg-battle-lasting");
                    float infW = KitMeasure.Width(CardTextFit.LastingMark, mark);
                    card.DescLabels.Add(inf);
                    card.DescLabels.Add(Label.Make(row, rest, ts, infW, 0f, fit.Fits ? (float?)null : Math.Max(0f, TextW - infW), pitch, "text"));
                }
                else
                {
                    card.DescLabels.Add(Label.Make(row, line, ts, 0f, 0f, fit.Fits ? (float?)null : TextW, pitch, "text"));
                }
                y += pitch;
            }
        }

        /// <summary>One trait row, with each term the script marked on it (CardFace.Terms) in a dotted underline (§7.1).</summary>
        // js: scenes/battle/hand-card.js:217
        private static void TraitRow(TextMeshProUGUI p, string text, TermRow row, CardFace face, bool bold, bool plain, List<CardTerm> terms)
        {
            text = text ?? "";
            TextFit fit = CardTextFit.Name(text, TextSize, TraitMinFont, plain ? TraitTextW + 14f : TraitTextW, 22f,
                bold ? Fonts().TraitBottom : Fonts().TraitTop);
            TextStyle style = Label.StyleOf(p)?.Clone() ?? Ocb.TextOf(Ocb.TraitCss + Ocb.TraitPCss);
            if (fit.Size != TextSize)
            {
                // the inline font-size (the row keeps its 22 px line box)
                style.Size = fit.Size;
                Label.Apply(p, style);
            }
            LabelState ls = Label.StateOf(p);
            if ((ls.Text ?? p.text) != text) Label.SetText(p, text);
            List<TermMark> marks = (face.Terms ?? new List<TermMark>())
                .Where(m => m != null && m.Row == row && m.Start >= 0 && m.Length > 0 && m.Start + m.Length <= text.Length)
                .OrderBy(m => m.Start)
                .ToList();
            int at = 0;
            foreach (TermMark m in marks)
            {
                if (m.Start < at) continue;
                // .ocb-term.ocbg-battle-term: underline dotted --ink-muted 2px, offset 4px (hand.css:20)
                Ocb.TermUnderline(p, m.Start, m.Length);
                terms.Add(new CardTerm { Mark = m, Rect = TermBox(p, style, text, m.Start, m.Length) });
                at = m.Start + m.Length;
            }
        }

        /// <summary>
        /// The span's box (getBoundingClientRect of an inline span): across, from the pen position before its first character
        /// to the one after its last; up and down, the face's content area (ascent + descent) centred in the 22 px line box.
        /// </summary>
        private static RectTransform TermBox(TextMeshProUGUI p, TextStyle style, string text, int start, int length)
        {
            float x0 = AdvanceTo(text, start, style);
            float x1 = AdvanceTo(text, start + length, style);
            FontMetrics fm = TrialFonts.MetricsOf(style.Family, style.Weight);
            float content = (fm.AscentEm + fm.DescentEm) * style.Size;
            float lh = style.LineHeightPx;
            RectTransform rt = Ui.Div(p.rectTransform, "ocb-term ocbg-battle-term", x0, (lh - content) * 0.5f, Math.Max(0f, x1 - x0), content);
            return rt;
        }

        /// <summary>The pen position after text[0, i) on one line (a sentinel keeps trailing spaces counted).</summary>
        private static float AdvanceTo(string text, int i, TextStyle style)
        {
            if (i <= 0) return 0f;
            TextStyle s = style.Clone();
            s.WhiteSpace = WhiteSpace.Pre;
            const string sentinel = "口";
            return Math.Max(0f, KitMeasure.Width(text.Substring(0, Math.Min(i, text.Length)) + sentinel, s) - KitMeasure.Width(sentinel, s));
        }

        // js: scenes/battle/hand-card.js:236
        private static OcbElement TraitPart(OcbCard card, CardFace face, List<CardTerm> terms)
        {
            string top = face.TraitTop;
            string bottom = face.TraitBottom;
            // A v4.2 source writes the trait as one line (CardView.BindTrait).
            if (string.IsNullOrEmpty(top) && string.IsNullOrEmpty(bottom) && !string.IsNullOrEmpty(face.TraitText))
            {
                top = face.TraitText;
                bottom = "";
            }
            if (string.IsNullOrEmpty(top) && string.IsNullOrEmpty(bottom)) return null;
            bool plain = face.Plain;
            // §6.1 素直な札: a dashed frame, no lamp, both rows in the note colour (the DS's .is-plain).
            OcbElement box = card.TraitPart(plain ? null : new OcbCardTrait(top ?? "", bottom ?? ""), false);
            box.Root.name = "ocb-trait" + (plain ? " is-plain" : "");
            if (card.TraitRows.Count >= 2)
            {
                TraitRow(card.TraitRows[0], top, TermRow.TraitTop, face, false, plain, terms);
                TraitRow(card.TraitRows[1], bottom, TermRow.TraitBottom, face, true, plain, terms);
                // .ocbg-battle-trait-rows (min-width: 0): its max-content width (the rows keep their 22 px line boxes)
                RectTransform rows = box.PartRect("rows");
                if (rows)
                {
                    rows.name = "ocbg-battle-trait-rows";
                    float w = 0f;
                    foreach (TextMeshProUGUI r in card.TraitRows) if (r) w = Math.Max(w, r.rectTransform.rect.width);
                    UiNode rn = UiNode.Of(rows);
                    rn.W = w;
                    // An empty row is an empty <p>: no line box, 0 tall (a v4.2 face's one-line trait, traitRow's text ''), so the
                    // other row stands alone in the middle of the box (align-items: center), where the DS card keeps two lines.
                    float h1 = string.IsNullOrEmpty(top) ? 0f : (Label.StyleOf(card.TraitRows[0])?.LineHeightPx ?? 22f);
                    float h2 = string.IsNullOrEmpty(bottom) ? 0f : (Label.StyleOf(card.TraitRows[1])?.LineHeightPx ?? 22f);
                    if (Math.Abs(h1 + h2 - rn.H) > 0.01f)
                    {
                        Ui.Place(card.TraitRows[1].rectTransform, 0f, h1);
                        rn.H = h1 + h2;
                        BoxStyle bs = Ocb.BoxOf(Ocb.TraitCss + (plain ? Ocb.TraitPlainCss : ""));
                        float y0 = bs.BorderWidths.Top + bs.Padding.Top;
                        float innerH = box.H - bs.BorderWidths.Vertical - bs.Padding.Vertical;
                        rn.Y = y0 + (innerH - rn.H) * 0.5f;
                    }
                }
            }
            return box;
        }

        /// <summary>
        /// buildCard(OCB, face): one card face (CardView.EnsureParts + Bind), 216×304 at (0, 0) of parent (null: none yet; the
        /// hand or the field adopts it). The lamp starts at face.TraitLit, the sheet at 0.
        /// </summary>
        // js: scenes/battle/hand-card.js:254
        public static CardFaceView BuildCard(CardFace face, RectTransform parent = null)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));
            string kindClass = KindClass(face.Kind);
            OcbCard card = Ocb.CardShell(parent, kindClass.Substring(3)); // .is-attack → attr "attack"; .is-move keeps the steel
            card.Root.name = "ocb-card ocbg-battle-face " + kindClass;
            HandFaceState st = card.Root.gameObject.AddComponent<HandFaceState>();
            st.Card = card;
            CostPart(st, face);
            card.AttrIconPart(KindIcon(face.Kind), InkOf(KindInk(face.Kind)));
            ReachPart(card, face);
            TextFit nameFit = CardTextFit.Name(face.Name ?? "", NameSize, MinFont, NameBoxW, NameBoxH, Fonts().Name);
            TextMeshProUGUI name = card.NamePart(nameFit.Text, nameFit.Size != NameSize ? nameFit.Size : (float?)null);
            if (name)
            {
                name.name = "ocb-card-name";
                // overflow: hidden (hand.css:12): a name that does not fit even at 14 px is cut at its box (TMP cuts whole characters)
                if (!nameFit.Fits) Label.Apply(name, Label.StyleOf(name).WithOverflow(TextOverflow.Clip));
            }
            OcbElement values = ValuesPart(card, face, out List<OcbCardValue> columns);
            // §3.5 / §5.10 (review 21): the status marks stand beside the value (setMods) — right after a lone value column, where
            // the fan leaves it in sight; with two columns at the value row's right end, inside the card's edge.
            if (values != null)
            {
                bool twoValues = values.Root.childCount > 1;
                string css = HandFaceState.ModsCss + (twoValues ? HandFaceState.ModsEndCss : "");
                RectTransform modsBox = Ocb.El(twoValues ? card.Root : values.Root, "ocbg-battle-mods" + (twoValues ? " is-end" : ""), css);
                UiNode.Of(modsBox).Visible = false; // style: display none
                st.ModsBox = modsBox;
                st.ModsAtEnd = twoValues;
                st.ValuesRow = values.Root;
                st.BaseRowW = values.W;
                st.BaseRowH = values.H;
                st.FirstWord = columns != null && columns.Count > 0 ? columns[0].Word ?? "" : "";
            }
            DescriptionPart(card, face);
            var terms = new List<CardTerm>();
            OcbElement trait = TraitPart(card, face, terms);
            card.DimPart(false);
            card.EdgePart();
            // .ocbg-battle-mods.is-end has z-index 1: it paints over the dark sheet and the edge.
            if (st.ModsBox && st.ModsAtEnd) st.ModsBox.SetAsLastSibling();
            st.CostShown = face.Cost.ToString(CultureInfo.InvariantCulture) + "/" + face.CostDrop.ToString(CultureInfo.InvariantCulture);
            // CardView.ShowLamp: a plain card and a card with no trait box never light.
            st.HasLamp = trait != null && !face.Plain;
            var view = new CardFaceView
            {
                Root = card.Root,
                Lit = false,
                Terms = terms,
                Lamp = card.Lamp != null && card.Lamp.Root ? card.Lamp.Root : null,
                TraitBox = trait != null ? trait.Root : null,
            };
            view.SetLit(face.TraitLit);
            view.SetDim(0f);
            return view;
        }

        /// <summary>
        /// wrapLine(text, width, size): the hover line's text broken where CardTextFit allows a break, for its 18 px body text
        /// in the line's width ('\n' between lines).
        /// </summary>
        // js: scenes/battle/hand-card.js:325
        public static string WrapLine(string text, float width, float size)
        {
            if (string.IsNullOrEmpty(text)) return "";
            FitMeasure m = MeasureWith(400, Family.Body, 1.375f);
            int px = (int)Math.Round(size);
            bool Fits(string t)
            {
                m(t, px, out float w, out _);
                return w <= width;
            }
            var output = new List<string>();
            string rest = text;
            while (!string.IsNullOrEmpty(rest) && !Fits(rest))
            {
                // Prefer a phrase edge outside brackets: after a space or 、。, or before an opening bracket — the last one whose line fits.
                int at = -1;
                int depth = 0;
                for (int i = 1; i < rest.Length; i++)
                {
                    char prev = rest[i - 1];
                    if (prev == '（' || prev == '「') depth++;
                    else if ((prev == '）' || prev == '」') && depth > 0) depth--;
                    bool edge = rest[i] == '（' || rest[i] == '「' || (depth == 0 && (prev == ' ' || prev == '、' || prev == '。'));
                    if (!edge || !CardTextFit.CanBreakBefore(rest, i)) continue;
                    if (Fits(rest.Substring(0, i).TrimEnd())) at = i;
                    else break;
                }
                if (at <= 0)
                {
                    output.AddRange(CardTextFit.Wrap(new List<string> { rest }, px, width, m));
                    return string.Join("\n", output);
                }
                output.Add(rest.Substring(0, at).TrimEnd());
                rest = rest.Substring(at).TrimStart();
            }
            if (!string.IsNullOrEmpty(rest)) output.Add(rest);
            return string.Join("\n", output);
        }

        /// <summary>faceKey(f): a key that changes whenever anything the face prints changes (CardView.Bind is called again).</summary>
        // js: scenes/battle/hand-card.js:352
        public static string FaceKey(CardFace f)
        {
            if (f == null) return "";
            string vals = string.Join(",", (f.Values ?? new List<CardValue>()).Where(v => v != null).Select(v => v.Kind + ":" + v.Number + ":" + v.Word));
            string terms = string.Join(",", (f.Terms ?? new List<TermMark>()).Where(t => t != null)
                .Select(t => t.Row + ":" + t.Start.ToString(CultureInfo.InvariantCulture) + ":" + t.Length.ToString(CultureInfo.InvariantCulture)));
            return string.Join("/", new[]
            {
                f.Id, f.Name, f.Cost.ToString(CultureInfo.InvariantCulture), f.PrintedCost.ToString(CultureInfo.InvariantCulture),
                f.Kind.ToString(), f.Aim.ToString(), f.RequiredRangeGlyph, f.ValueText, vals,
                string.Join("|", f.TextLines ?? new List<string>()), f.Description,
                f.Lasting ? "true" : "false", f.Plain ? "true" : "false", f.TraitTop, f.TraitBottom, f.TraitText, terms,
            });
        }

        /// <summary>
        /// buildDetail(OCB, face, extra): CardPanels.ShowDetail (§7.4) — .ocb-detail 560 wide: the card at 80 % on the left, the
        /// name 24, the kind line 16, the full text, the 「今」 rows (nowPart) and the terms box along the bottom. Built under
        /// parent at (0, 0); the panels fit and place it.
        /// </summary>
        // js: scenes/battle/hand-card.js:364
        public static RectTransform BuildDetail(CardFace face, DetailExtra extra, RectTransform parent = null)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));
            CardDetail d = face.Detail ?? new CardDetail();
            string rootCss = Ocb.DetailCss + DetailCss + DetailSizeCss;
            RectTransform el = Ocb.El(parent, "ocb-detail ocbg-battle-detail", rootCss);
            HandDetailState st = el.gameObject.AddComponent<HandDetailState>();
            st.Root = el;
            BoxStyle bs = Ocb.BoxOf(rootCss);
            st.Border = bs.BorderWidths;
            st.Pad = bs.Padding;
            st.ColGap = Ocb.Rule(Ocb.DetailCss).Layout.ColumnGap ?? 16f;
            // overflow: hidden — what runs past the padding box is cut there
            st.Clip = Ui.Div(el, "clip");
            UiNode.Of(st.Clip);
            st.Clip.gameObject.AddComponent<RectMask2D>();

            // .ocb-detail-card (173×243, flex: none) with the card at scale(.8), transform-origin 0 0 (bundle.css:184)
            CssBlock holderRule = Ocb.Rule(Ocb.DetailCardCss);
            st.HolderW = Ocb.Px(holderRule.Layout.Width) ?? 173f;
            st.HolderH = Ocb.Px(holderRule.Layout.Height) ?? 243f;
            st.Holder = Ocb.El(st.Clip, "ocb-detail-card", Ocb.DetailCardCss);
            UiNode.Of(st.Holder).Place(st.Pad.Left, st.Pad.Top, st.HolderW, st.HolderH);
            CardFaceView mini = BuildCard(face, st.Holder);
            mini.Node.SetOrigin(0f, 0f);
            mini.Node.SetStyleTransform(sx: 0.8f, sy: 0.8f);
            if (extra != null && extra.Mods != null) mini.SetMods(extra.Mods);

            // .ocb-detail-body: column, gap 6, flex: 1 (hand.css:39)
            st.Body = Ocb.El(st.Clip, "ocb-detail-body");
            UiNode.Of(st.Body);
            st.BodyGap = Ocb.Rule(Ocb.DetailBodyCss).Layout.RowGap ?? 6f;
            st.H3Style = Ocb.TextOf(Ocb.DetailCss + Ocb.DetailH3Css);
            st.MetaStyle = Ocb.TextOf(Ocb.DetailCss + Ocb.DetailMetaCss);
            st.FullStyle = Ocb.TextOf(Ocb.DetailCss + Ocb.DetailFullCss + DetailTextCss);
            st.TitleText = !string.IsNullOrEmpty(d.Name) ? d.Name : face.Name ?? "";
            st.MetaText = d.KindLine ?? "";
            st.FullText = d.Body ?? "";
            st.Title = Label.Make(st.Body, st.TitleText, st.H3Style, 0f, 0f, 0f, 0f, "h3");
            st.Meta = Label.Make(st.Body, st.MetaText, st.MetaStyle, 0f, 0f, 0f, 0f, "ocb-meta");
            st.Full = Label.Make(st.Body, st.FullText, st.FullStyle, 0f, 0f, 0f, 0f, "ocb-full ocbg-battle-detail-text");

            List<KeyValuePair<string, string>> now = NowPart(face, extra);
            if (now != null)
            {
                CssBlock nowRule = Ocb.Rule(DetailNowCss);
                st.NowMarginTop = Ocb.Px(nowRule.Layout.MarginTop) ?? 8f;
                st.NowGap = nowRule.Layout.RowGap ?? 2f;
                st.NowEl = Ocb.El(st.Body, "ocbg-battle-detail-now", DetailNowCss);
                st.NowStyle = Ocb.TextOf(Ocb.DetailCss + DetailNowCss + DetailNowPCss);
                st.NowStyle.RichText = true;
                TextStyle head = Ocb.TextOf(Ocb.DetailCss + DetailNowCss + DetailNowSpanCss);
                CssBlock spanRule = Ocb.Rule(DetailNowSpanCss);
                float minW = Ocb.Px(spanRule.Layout.MinWidth) ?? 72f;
                float mr = Ocb.Px(spanRule.Layout.MarginRight) ?? 8f;
                foreach (KeyValuePair<string, string> r in now)
                {
                    // <p><span>head</span>text</p>: the head an inline-block at least 72 wide and 8 before the text; a wrapped
                    // line starts under the head, as the browser's line does.
                    float x = Math.Max(minW, KitMeasure.Width(r.Key, head)) + mr;
                    string text = Materials.ColorTag(head.Color) + HandDetailState.Escape(r.Key) + "</color><pos=" + Ocb.Num(x) + ">" + HandDetailState.Escape(r.Value);
                    st.NowTexts.Add(text);
                    st.NowRows.Add(Label.Make(st.NowEl, text, st.NowStyle, 0f, 0f, 0f, 0f, "p"));
                }
            }

            List<TermDefinition> defs = d.Terms ?? new List<TermDefinition>();
            if (defs.Count > 0)
            {
                string termsCss = Ocb.DetailTermsCss + DetailTermsCss;
                st.TermsEl = Ocb.El(st.Body, "ocb-detail-terms ocbg-battle-detail-terms", termsCss);
                BoxStyle tb = Ocb.BoxOf(termsCss);
                st.TermsPad = tb.Padding;
                st.TermsBorder = tb.BorderWidths;
                st.TermsMaxH = Ocb.Px(Ocb.Rule(termsCss).Layout.MaxHeight) ?? 330f;
                st.TermsStyle = Ocb.TextOf(Ocb.DetailCss + termsCss);
                st.TermsStyle.RichText = true;
                // <b> (font-weight: bold in the body face; margin-right 0, hand.css:46)
                TrialFonts.Get(Family.Body, 700);
                string bold = "<font=\"" + TrialFonts.Tag(Family.Body, 700) + "\">";
                float bGap = Ocb.Px(Ocb.Rule(Ocb.DetailTermsBCss + DetailTermsBCss).Layout.MarginRight) ?? 0f;
                string after = bGap > 0f ? "<space=" + Ocb.Num(bGap) + ">" : "";
                foreach (TermDefinition t in defs)
                {
                    if (t == null) continue;
                    // scene text (CardPanels: Word + "：" + Meaning)
                    var row = new HandDetailState.TermRowPart { Prefix = bold + HandDetailState.Escape(t.Word) + "</font>" + after, Tail = "：" + (t.Meaning ?? "") };
                    row.Label = Label.Make(st.TermsEl, row.Text, st.TermsStyle, 0f, 0f, 0f, 0f, "div");
                    st.TermRows.Add(row);
                }
            }
            st.Layout();
            return el;
        }

        /// <summary>
        /// battle_ui_ux_v2 §3.13 (review 21: the detail held the card text and the terms only): what the card does now — 離す先
        /// (対象の枠 / 投げ上げ線, from CardFace.Aim) and, for a card with a trait, 今の判定 (its lamp) with 予測値の内訳 (the source's
        /// prediction against the face's printed value, and the status marks that apply). scene text. 習熟の段と伸びる先, 探索の面の印
        /// and 入手先 are not in the script (CardFace carries none of them; docs/BATTLE_SCENE.md §8.2).
        /// </summary>
        // js: scenes/battle/hand-card.js:386
        private static string ZoneWords(CardAim aim)
        {
            switch (aim)
            {
                case CardAim.Single: return "対象の枠（敵の上で離す）";
                case CardAim.Self: return "投げ上げ線（線より上で離す）";
                default: return null;
            }
        }

        // js: scenes/battle/hand-card.js:387
        private static List<KeyValuePair<string, string>> NowPart(CardFace face, DetailExtra extra)
        {
            var rows = new List<KeyValuePair<string, string>>();
            string zone = ZoneWords(face.Aim);
            if (zone != null) rows.Add(new KeyValuePair<string, string>("離す先", zone));
            bool hasTrait = !face.Plain && (!string.IsNullOrEmpty(face.TraitTop) || !string.IsNullOrEmpty(face.TraitText));
            if (hasTrait && extra != null)
                rows.Add(new KeyValuePair<string, string>("今の判定", extra.Lit ? "条件が成り立っています（ランプ点灯）" : "条件は成り立っていません"));
            CardValue printed = face.Values != null && face.Values.Count > 0 ? face.Values[0] : null;
            if (extra != null && !string.IsNullOrEmpty(extra.Preview))
            {
                var parts = new StringBuilder(extra.Preview);
                if (printed != null && (printed.Number ?? "") != extra.Preview)
                {
                    IReadOnlyList<CardMod> mods = extra.Mods ?? Array.Empty<CardMod>();
                    parts.Append("（表の値 ").Append(printed.Number ?? "")
                        .Append(mods.Count > 0 ? "・" + string.Join("・", mods.Select(m => m.Word + " " + m.Text)) : "")
                        .Append(extra.Lit ? "・特性" : "")
                        .Append("）");
                }
                rows.Add(new KeyValuePair<string, string>("予測値", parts.ToString()));
            }
            return rows.Count > 0 ? rows : null;
        }
    }

    /// <summary>
    /// The state buildCard's closure keeps (hand-card.js:254-317): the DS card's parts, whether the face has a lamp, the
    /// cost the circle shows, and the status marks' box — on the face's root, so the face's handle stays the JS's.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class HandFaceState : MonoBehaviour
    {
        /// <summary>hand.css:18 <c>.ocbg-battle-face .ocb-cost .ocb-down</c></summary>
        internal const string DownTagCss = "left: 42px; top: 42px; bottom: auto; transform: translate(-50%, -50%);";

        /// <summary>hand.css:61 <c>.ocbg-battle-mods</c></summary>
        internal const string ModsCss = "display: flex; flex-direction: column; align-items: flex-start; gap: 2px; align-self: center; margin-left: -4px;";

        /// <summary>hand.css:62 <c>.ocbg-battle-mods.is-end</c></summary>
        internal const string ModsEndCss = "position: absolute; right: 6px; top: 92px; align-items: flex-end; margin-left: 0; z-index: 1;";

        /// <summary>hand.css:63 <c>.ocbg-battle-mod</c></summary>
        internal const string ModCss = "display: inline-flex; align-items: center; gap: 2px; height: 20px; padding: 0 3px; border-radius: 4px; background: color-mix(in srgb, var(--card) 70%, var(--ink) 18%); box-shadow: 0 0 0 1px color-mix(in srgb, var(--ink) 25%, transparent); white-space: nowrap;";

        /// <summary>hand.css:64 <c>.ocbg-battle-mod b</c></summary>
        internal const string ModBCss = "font: 600 16px/20px var(--font-mono); color: var(--ink);";

        internal OcbCard Card;
        internal bool HasLamp;
        internal string CostShown = "";
        /// <summary>.ocbg-battle-mods (null: the face prints no value).</summary>
        internal RectTransform ModsBox;
        /// <summary>.is-end: two value columns, the marks at the card's right edge.</summary>
        internal bool ModsAtEnd;
        internal string ModsKey = "";
        internal RectTransform ValuesRow;
        internal float BaseRowW, BaseRowH;
        internal string FirstWord = "";

        /// <summary>CardView.BindCost: the number, filled and marked "▼n" when a discount took a cost off.</summary>
        // js: scenes/battle/hand-card.js:134
        internal void FillCost(int cost, int drop)
        {
            OcbCard card = Card;
            if (card == null || !card.Root) return;
            int index = -1;
            if (card.CostEl != null && card.CostEl.Root)
            {
                index = card.CostEl.Root.GetSiblingIndex();
                card.CostEl.Remove();
            }
            bool down = drop > 0;
            OcbElement el = card.CostPart(cost.ToString(CultureInfo.InvariantCulture), down,
                down ? "▼" + drop.ToString(CultureInfo.InvariantCulture) : null); // scene text (CardView "▼" + drop)
            el.Root.name = "ocb-cost" + (down ? " is-down" : "");
            if (index >= 0) el.Root.SetSiblingIndex(index);
            OcbElement tag = down ? card.DownTag : null;
            if (tag == null || !tag.Root) return;
            // ▼n at the cost circle's lower right (CardView.BindCost: anchor (1, 0), 2 px in), clear of the name (hand.css:18):
            // left 42 / top 42 of the circle's padding box, translate(−50%, −50%).
            Insets bw = Ocb.BoxOf(Ocb.CostCss + Ocb.CostDownCss).BorderWidths;
            CssBlock r = Ocb.Rule(DownTagCss);
            float left = Ocb.Px(r.Layout.Left) ?? 42f, top = Ocb.Px(r.Layout.Top) ?? 42f;
            tag.Node.Place(bw.Left + left - tag.W * 0.5f, bw.Top + top - tag.H * 0.5f);
        }

        /// <summary>The marks of `modsOf` as one small column after the values (ink 16 px, each with its status icon at 16).</summary>
        // js: scenes/battle/hand-card.js:185
        internal void FillMods(IReadOnlyList<CardMod> mods)
        {
            if (!ModsBox) return;
            Ui.Clear(ModsBox);
            UiNode box = UiNode.Of(ModsBox);
            box.Visible = mods.Count > 0;
            float gap = Ocb.Rule(ModsCss).Layout.RowGap ?? 2f;
            TextStyle bs = Ocb.TextOf(ModCss + ModBCss);
            var tags = new List<RectTransform>();
            float colW = 0f, colH = 0f;
            foreach (CardMod m in mods)
            {
                if (m == null) continue;
                // span.ocbg-battle-mod (title: the status word — the browser's own tooltip; see the hand-over)
                RectTransform tag = Ocb.El(ModsBox, "ocbg-battle-mod " + m.Word, ModCss);
                Ocb.Text(tag, m.Text ?? "", bs, "b");
                Ocb.Icon(tag, m.Icon, 16f, DsTokens.Ink); // an icon the DS lacks draws its `plus`
                Ocb.Flow(tag, ModCss);
                UiNode tn = UiNode.Of(tag);
                colW = Math.Max(colW, tn.W);
                colH += (tags.Count > 0 ? gap : 0f) + tn.H;
                tags.Add(tag);
            }
            float y = 0f;
            foreach (RectTransform tag in tags)
            {
                UiNode tn = UiNode.Of(tag);
                // align-items: flex-start (in the value row) / flex-end (.is-end)
                tn.Place(ModsAtEnd ? colW - tn.W : 0f, y);
                y += tn.H + gap;
            }
            box.W = colW;
            box.H = colH;
            if (ModsAtEnd)
            {
                // position: absolute; right: 6px; top: 92px (of the 216-wide card)
                CssBlock r = Ocb.Rule(ModsEndCss);
                box.Place(HandCard.CardW - (Ocb.Px(r.Layout.Right) ?? 6f) - colW, Ocb.Px(r.Layout.Top) ?? 92f);
                return;
            }
            // A flex item of .ocb-values after the column: 14 px gap, margin-left −4, align-self: center; the row is as tall as
            // its tallest item and stretches the value column to it.
            if (!ValuesRow) return;
            float rowGap = Ocb.Rule(Ocb.ValuesCss).Layout.ColumnGap ?? 14f;
            float ml = Ocb.Px(Ocb.Rule(ModsCss).Layout.MarginLeft) ?? -4f;
            bool shown = mods.Count > 0;
            float rowH = shown ? Math.Max(BaseRowH, colH) : BaseRowH;
            box.Place(BaseRowW + rowGap + ml, (rowH - colH) * 0.5f);
            UiNode row = UiNode.Of(ValuesRow);
            row.W = shown ? BaseRowW + rowGap + ml + colW : BaseRowW;
            row.H = rowH;
            StretchColumn(rowH);
        }

        /// <summary>
        /// The lone value column stretched to the row's height (display: flex, align-items: stretch): align-content: normal
        /// shares the extra height between the grid's two auto rows (Ocb.Card's ValuesPart, measured in Chrome).
        /// </summary>
        private void StretchColumn(float rowH)
        {
            if (!ValuesRow || ValuesRow.childCount == 0) return;
            var val = ValuesRow.GetChild(0) as RectTransform;
            if (!val || val == ModsBox || val.childCount < 3) return;
            float colGap = Ocb.Rule(Ocb.ValCss).Layout.ColumnGap ?? 4f;
            float bLh = Ocb.TextOf(Ocb.ValBCss).LineHeightPx;
            float row1 = Math.Max(24f, bLh);
            float smallH = FirstWord.Length > 0 ? Ocb.TextOf(Ocb.ValSmallCss).LineHeightPx : 0f;
            float row2 = Math.Max(0f, smallH - 2f);
            float extra = Math.Max(0f, rowH - (row1 + row2)) * 0.5f;
            float r1 = row1 + extra, r2 = row2 + extra;
            Ui.Place((RectTransform)val.GetChild(0), 0f, (r1 - 24f) * 0.5f);
            Ui.Place((RectTransform)val.GetChild(1), 24f + colGap, (r1 - bLh) * 0.5f);
            Ui.Place((RectTransform)val.GetChild(2), 24f + colGap, r1 + (r2 - (smallH - 2f)) * 0.5f - 2f);
            UiNode.Of(val).H = rowH;
        }
    }

    /// <summary>
    /// The right-click detail as the browser lays it out (.ocb-detail with hand.css:38-46, 57-59): 560 wide, as tall as its
    /// content between 280 and 600 (border-box); the card slot on the left and the body column (flex: 1) on the right — the
    /// name, the kind line, the full text (max 264, the one item that shrinks: overflow hidden), the 「今」 rows, and the terms
    /// box pushed to the bottom by margin-top: auto (content-box, its content at most 330). hand-panels.js fitDetail reads
    /// and cuts it through the members below, the way it reads the DOM.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class HandDetailState : MonoBehaviour
    {
        /// <summary>One row of the terms box: 「<b>word</b>：meaning」, the text node after the word cut by fitDetail.</summary>
        internal sealed class TermRowPart
        {
            internal string Prefix = "";
            internal string Tail = "";
            internal TextMeshProUGUI Label;
            internal bool Removed;
            internal string Text => Prefix + Escape(Tail);
        }

        /// <summary>Text made safe for TMP rich text (a literal '&lt;' cannot open a tag).</summary>
        internal static string Escape(string text) => string.IsNullOrEmpty(text) ? "" : text.Replace("<", "<noparse><</noparse>");

        private const float MinH = 280f; // hand.css:43 min-height
        private const float MaxH = 600f; // hand.css:43 max-height
        private const float FullMaxH = 264f; // hand.css:44 max-height

        internal RectTransform Root, Clip, Holder, Body, NowEl, TermsEl;
        internal TextMeshProUGUI Title, Meta, Full;
        internal TextStyle H3Style, MetaStyle, FullStyle, NowStyle, TermsStyle;
        internal string TitleText = "", MetaText = "", FullText = "";
        internal readonly List<string> NowTexts = new List<string>();
        internal readonly List<TextMeshProUGUI> NowRows = new List<TextMeshProUGUI>();
        internal readonly List<TermRowPart> TermRows = new List<TermRowPart>();
        internal Insets Border, Pad, TermsPad, TermsBorder;
        internal float ColGap = 16f, BodyGap = 6f, HolderW = 173f, HolderH = 243f, NowMarginTop = 8f, NowGap = 2f, TermsMaxH = 330f;

        /// <summary>el.offsetHeight after the last layout.</summary>
        internal float Height = MinH;

        private float _fullNat, _fullH, _termsClient;
        private readonly Dictionary<string, float> _heights = new Dictionary<string, float>(StringComparer.Ordinal);

        internal float Width => Root ? UiNode.Of(Root).W : 560f;
        internal float BodyW => Math.Max(0f, Width - Border.Horizontal - Pad.Horizontal - HolderW - ColGap);
        internal float TermRowW => Math.Max(0f, BodyW - TermsPad.Horizontal - TermsBorder.Horizontal);

        /// <summary>A block's height at a width (lines × line height), cached per text.</summary>
        private float HeightOf(char which, string text, TextStyle style, float width)
        {
            if (string.IsNullOrEmpty(text) || style == null) return 0f;
            string key = which + "\u0000" + width.ToString("0.###", CultureInfo.InvariantCulture) + "\u0000" + text;
            if (_heights.TryGetValue(key, out float h)) return h;
            h = Ocb.TextHeight(text, style, width);
            _heights[key] = h;
            return h;
        }

        /// <summary>The terms box's clientHeight: its padding-top and its content (max-height 330), no border.</summary>
        internal float TermsClientHeight => _termsClient;

        internal bool HasTerms => TermsEl;

        /// <summary>The body text's scrollHeight is past its clientHeight (+1).</summary>
        internal bool FullOverflows()
        {
            Layout();
            return _fullNat > _fullH + 1f;
        }

        internal void SetFullText(string text)
        {
            FullText = text ?? "";
            if (Full) Label.SetText(Full, FullText);
        }

        internal int TermRowCount => TermRows.Count;

        internal float TermRowHeight(int i) =>
            i < 0 || i >= TermRows.Count || TermRows[i].Removed ? 0f : HeightOf('t', TermRows[i].Text, TermsStyle, TermRowW);

        /// <summary>row.offsetTop (the box is position: relative): its padding-top and the rows above.</summary>
        internal float TermRowTop(int i)
        {
            float y = TermsPad.Top;
            for (int j = 0; j < i && j < TermRows.Count; j++) y += TermRowHeight(j);
            return y;
        }

        internal string TermTail(int i) => i >= 0 && i < TermRows.Count ? TermRows[i].Tail : "";

        internal void SetTermTail(int i, string text)
        {
            if (i < 0 || i >= TermRows.Count) return;
            TermRowPart r = TermRows[i];
            r.Tail = text ?? "";
            if (r.Label) Label.SetText(r.Label, r.Text);
        }

        internal void RemoveTermRow(int i)
        {
            if (i < 0 || i >= TermRows.Count || TermRows[i].Removed) return;
            TermRows[i].Removed = true;
            if (TermRows[i].Label) Ui.Remove(TermRows[i].Label);
        }

        /// <summary>A label's box set; laid out again when its size changed (TMP wraps at the new width).</summary>
        private static void PlaceLabel(TextMeshProUGUI t, float x, float y, float w, float h)
        {
            if (!t) return;
            RectTransform rt = t.rectTransform;
            bool resized = Math.Abs(rt.sizeDelta.x - w) > 0.01f || Math.Abs(rt.sizeDelta.y - h) > 0.01f;
            Ui.Place(rt, x, y, w, h);
            if (resized) Label.Relayout(t);
        }

        /// <summary>The flex layout of the detail with the texts it holds now; places every part and sets Height.</summary>
        internal void Layout()
        {
            float bodyW = BodyW;
            float h3H = HeightOf('h', TitleText, H3Style, bodyW);
            float metaH = HeightOf('m', MetaText, MetaStyle, bodyW);
            _fullNat = HeightOf('f', FullText, FullStyle, bodyW);
            float fullBasis = Math.Min(_fullNat, FullMaxH);
            bool hasNow = NowEl;
            var nowHs = new List<float>();
            float nowH = 0f;
            if (hasNow)
            {
                for (int i = 0; i < NowTexts.Count; i++)
                {
                    float h = HeightOf('n', NowTexts[i], NowStyle, bodyW);
                    nowHs.Add(h);
                    nowH += (i > 0 ? NowGap : 0f) + h;
                }
            }
            bool hasTerms = TermsEl;
            float termsContent = 0f;
            if (hasTerms) for (int i = 0; i < TermRows.Count; i++) termsContent += TermRowHeight(i);
            float termsInner = Math.Min(termsContent, TermsMaxH);
            _termsClient = TermsPad.Vertical + termsInner;
            float termsH = hasTerms ? TermsPad.Vertical + TermsBorder.Vertical + termsInner : 0f;

            float intrinsic = h3H + BodyGap + metaH + BodyGap + fullBasis
                + (hasNow ? BodyGap + NowMarginTop + nowH : 0f)
                + (hasTerms ? BodyGap + termsH : 0f);
            float chrome = Border.Vertical + Pad.Vertical;
            float contentH = Math.Max(HolderH, intrinsic);
            Height = Math.Max(MinH, Math.Min(MaxH, contentH + chrome));
            float innerH = Height - chrome;
            float free = innerH - intrinsic;
            // Only the full text shrinks (overflow: hidden makes its automatic minimum 0); margin-top: auto takes what is left.
            _fullH = Math.Max(0f, fullBasis + Math.Min(0f, free));
            float autoMargin = Math.Max(0f, free);

            if (Root) UiNode.Of(Root).H = Height;
            if (Clip) UiNode.Of(Clip).Place(Border.Left, Border.Top, Width - Border.Horizontal, Height - Border.Vertical);
            if (Body) UiNode.Of(Body).Place(Pad.Left + HolderW + ColGap, Pad.Top, bodyW, innerH);
            float y = 0f;
            PlaceLabel(Title, 0f, y, bodyW, h3H);
            y += h3H + BodyGap;
            PlaceLabel(Meta, 0f, y, bodyW, metaH);
            y += metaH + BodyGap;
            PlaceLabel(Full, 0f, y, bodyW, _fullH);
            y += _fullH;
            if (hasNow)
            {
                y += BodyGap + NowMarginTop;
                UiNode.Of(NowEl).Place(0f, y, bodyW, nowH);
                float ry = 0f;
                for (int i = 0; i < NowRows.Count && i < nowHs.Count; i++)
                {
                    PlaceLabel(NowRows[i], 0f, ry, bodyW, nowHs[i]);
                    ry += nowHs[i] + NowGap;
                }
                y += nowH;
            }
            if (hasTerms)
            {
                y += BodyGap + autoMargin;
                UiNode.Of(TermsEl).Place(0f, y, bodyW, termsH);
                float ry = TermsPad.Top;
                float rw = TermRowW;
                for (int i = 0; i < TermRows.Count; i++)
                {
                    TermRowPart r = TermRows[i];
                    if (r.Removed || !r.Label) continue;
                    float h = TermRowHeight(i);
                    PlaceLabel(r.Label, TermsPad.Left + TermsBorder.Left, ry, rw, h);
                    ry += h;
                }
            }
        }
    }
}
