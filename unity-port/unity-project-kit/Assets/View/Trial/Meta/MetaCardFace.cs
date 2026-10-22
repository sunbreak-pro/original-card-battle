// A card face as the design system draws it (Ocb.Card, 216×304), from the face the bridge writes (CoreText.FaceOf →
// CardFace, filled by CardFaceText). Port of scenes/meta/cardFace.js and the meta.css rules for cards inside a meta
// scene (meta.css:173, :199-200, :389-394). Like Unity's CardView.Bind it only maps fields: the kind to the frame colour
// (a movement card is drawn as a skill), every value column to its icon (no limit on the columns), the rows, the trait
// box. Nothing here judges a card.
//
// What the meta screens change on the DS card (meta-b.md §2.3): the description keeps its rows (white-space: pre-line)
// and drops to 14px/19px (.is-small) when there are more than three rows or a row runs past eleven characters; a
// stance card's ∞ rows wrap (line-height 20, 18 when small, at least 22 tall, the icon 1 px down); the plain card's box
// prints the bridge's own two rows; the glossary words of the trait box get the dotted underline and their meaning on
// hover (the browser's title: TitleTip).
using System;
using System.Collections.Generic;
using System.Linq;
using Depiction;
using Depiction.Bridge;
using TMPro;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;
using TextMeasure = Trial.Kit.TextMeasure;

namespace Trial.Meta
{
    public static class MetaCardFace
    {
        // ---- meta.css, verbatim ---------------------------------------------------------------------------------------

        /// <summary>meta.css:390 .ocbg-meta .ocb-card .ocb-desc.</summary>
        public const string DescCss = "white-space: pre-line;";

        /// <summary>meta.css:391 .ocbg-meta .ocb-card .ocb-desc.is-small.</summary>
        public const string DescSmallCss = "font-size: 14px; line-height: 19px;";

        /// <summary>meta.css:392 .ocbg-meta .ocb-card .ocb-desc .ocb-inf.</summary>
        public const string InfCss = "white-space: normal; height: auto; min-height: 22px; align-items: flex-start; line-height: 20px;";

        /// <summary>meta.css:393 .ocbg-meta .ocb-card .ocb-desc.is-small .ocb-inf.</summary>
        public const string InfSmallCss = "line-height: 18px;";

        /// <summary>meta.css:394 .ocbg-meta .ocb-card .ocb-desc .ocb-inf svg.</summary>
        public const string InfSvgCss = "flex: none; margin-top: 1px;";

        /// <summary>meta.css:173 .ocbg-meta .ocbg-meta-cardslot.</summary>
        public const string CardSlotCss = "position: relative; flex: none;";

        /// <summary>meta.css:199 .ocbg-meta .ocbg-meta-term.</summary>
        public const string TermCss = "font: 400 16px/1.4 var(--font-body);";

        /// <summary>meta.css:200 .ocbg-meta .ocbg-meta-term b.</summary>
        public const string TermBCss = "font: 700 16px/1.4 var(--font-display); margin-right: 10px;";

        // ---- the maps ---------------------------------------------------------------------------------------------

        // js: scenes/meta/cardFace.js:10
        /// <summary>CardKind (DepictionTypes) → the design system's attribute key (a movement card is a skill).</summary>
        public static readonly IReadOnlyDictionary<CardKind, string> AttrOf = new Dictionary<CardKind, string>
        {
            [CardKind.Attack] = "attack", [CardKind.Guard] = "defense", [CardKind.Skill] = "skill", [CardKind.Stance] = "stance", [CardKind.Move] = "skill",
        };

        // js: scenes/meta/cardFace.js:13
        /// <summary>CardValueKind → icon (battle-visual-v1 付録 A): 詰める / 押す point forward, 離れる / 引く back.</summary>
        public static readonly IReadOnlyDictionary<CardValueKind, string> ValueIcon = new Dictionary<CardValueKind, string>
        {
            [CardValueKind.Power] = "sword", [CardValueKind.Guard] = "shield", [CardValueKind.Heal] = "heal", [CardValueKind.Close] = "forward",
            [CardValueKind.Back] = "back", [CardValueKind.Push] = "forward", [CardValueKind.Pull] = "back", [CardValueKind.Draw] = "draw",
        };

        // js: scenes/meta/cardFace.js:16
        /// <summary>faceOf(def): the face of a catalog card as the deck screen shows it.</summary>
        public static CardFace FaceOf(BattleCore.CardDef def) => CoreText.FaceOf(def);

        // js: scenes/meta/cardFace.js:25
        /// <summary>
        /// descriptionRows(face): the rows the description prints, as Unity's CardTextFit.DescriptionRows picks them — the
        /// face's TextLines when it has any (a stance's lasting rows among them), else one row per sentence of its
        /// Description (split after every 「。」, blank rows dropped).
        /// </summary>
        public static List<string> DescriptionRows(CardFace face)
        {
            if (face == null) return new List<string>();
            if (face.TextLines != null && face.TextLines.Count > 0) return face.TextLines.ToList();
            return (face.Description ?? "").Replace("。", "。\n").Split('\n').Where(r => r.Trim().Length > 0).ToList();
        }

        // js: scenes/meta/cardFace.js:31
        /// <summary>ocbDef(face): CardFace → the def Ocb.Card reads.</summary>
        public static OcbCardDef OcbDef(CardFace face)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));
            string attr = AttrOf.TryGetValue(face.Kind, out string a) ? a : "skill";
            List<string> rows = DescriptionRows(face);
            var def = new OcbCardDef
            {
                Name = face.Name ?? "",
                Attr = attr,
                Cost = face.PrintedCost != 0 ? face.PrintedCost : face.Cost,   // face.PrintedCost || face.Cost
                Desc = face.Lasting ? "" : string.Join("\n", rows),
                Lines = face.Lasting ? rows : new List<string>(),
                Trait = face.Plain ? null : new OcbCardTrait(face.TraitTop, face.TraitBottom),
            };
            if (!string.IsNullOrEmpty(face.RequiredRangeGlyph)) def.WithReach(face.RequiredRangeGlyph, face.RequiredRangeGlyph);
            else def.ReachSelf = true;
            foreach (CardValue v in face.Values ?? new List<CardValue>())
                def.Values.Add(new OcbCardValue(ValueIcon.TryGetValue(v.Kind, out string icon) ? icon : "plus", v.Number, v.Word));
            return def;
        }

        /// <summary>Whether the description drops to 14px/19px (undecided look 5, recommendation B): more than three rows, or a row past eleven characters.</summary>
        public static bool IsSmall(IReadOnlyList<string> rows) => rows.Count > 3 || rows.Any(r => r.Length > 11);

        // js: scenes/meta/cardFace.js:50
        /// <summary>
        /// cardEl(face, { lit, unpayable }): one card at full size (216×304) at (0, 0) of parent. The plain card keeps the
        /// bridge's own two rows (CardFaceText.PlainTop / PlainBottom) in the dashed box.
        /// </summary>
        public static OcbCard CardEl(RectTransform parent, CardFace face, bool lit = false, bool unpayable = false)
        {
            OcbCardDef def = OcbDef(face);
            OcbCard card = Ocb.Card(parent, def, new OcbCardState { Cost = face.Cost, Lamp = lit, Unpayable = unpayable });
            List<string> rows = DescriptionRows(face);
            bool small = IsSmall(rows);
            Relay(card, def, small);
            if (card.TraitRows.Count >= 2)
            {
                TextMeshProUGUI top = card.TraitRows[0], bottom = card.TraitRows[1];
                if (face.Plain)
                {
                    Label.SetText(top, face.TraitTop ?? "");
                    Label.SetText(bottom, face.TraitBottom ?? "");
                }
                else
                {
                    // battle-visual-v1 §7.1: the glossary words the trait box prints get a dotted underline and their
                    // meaning on hover (CardFace.Terms: row, start, length, word, meaning — all the bridge's).
                    List<TermMark> terms = face.Terms ?? new List<TermMark>();
                    Underline(top, face.TraitTop ?? "", terms.Where(m => m.Row == TermRow.TraitTop).ToList());
                    Underline(bottom, face.TraitBottom ?? "", terms.Where(m => m.Row == TermRow.TraitBottom).ToList());
                }
            }
            card.Root.name = "ocb-card " + face.Id;   // el.dataset.card
            return card;
        }

        /// <summary>The description laid out by the meta rules: pre-line rows, .is-small, and a stance card's wrapping ∞ rows.</summary>
        private static void Relay(OcbCard card, OcbCardDef def, bool small)
        {
            if (card.DescEl == null || !card.DescEl.Root) return;
            if (!card.IsStance)
            {
                card.SetDesc(def.Desc, Ocb.TextOf(Ocb.DescCss + DescCss + (small ? DescSmallCss : ""), card.Vars));
                return;
            }
            // the ∞ rows: .ocb-inf blocks, each the icon (18, 1 px down) and its text (wrapping in what is left), at
            // least 22 tall, one under the other
            RectTransform desc = card.DescEl.Root;
            Ui.Clear(desc);
            card.DescLabels.Clear();
            string infCss = Ocb.DescCss + Ocb.StanceDescCss + (small ? DescSmallCss : "") + Ocb.DescInfCss + InfCss + (small ? InfSmallCss : "");
            TextStyle ts = Ocb.TextOf(infCss, card.Vars);
            float gap = Ocb.Rule(Ocb.DescInfCss).Layout.ColumnGap ?? 6f;
            float textX = 18f + gap;
            float textW = Math.Max(0f, card.DescEl.W - textX);
            float lh = ts.LineHeightPx;
            float y = 0f;
            foreach (string line in def.Lines ?? new List<string>())
            {
                RectTransform inf = Ocb.El(desc, "ocb-inf");
                Ui.Place(Ocb.Icon(inf, "infinity", 18f, DsTokens.HpText).rectTransform, 0f, 1f);   // svg margin-top: 1px
                float w = Math.Min(TextMeasure.Width(line, ts), textW);
                float h = string.IsNullOrEmpty(line) ? 0f : TextMeasure.Lines(line, ts, Math.Max(1f, w + 0.01f)) * lh;
                TextMeshProUGUI t = Label.Make(inf, line, ts, textX, 0f, w + 0.01f, h, "text");
                card.DescLabels.Add(t);
                float rowH = Math.Max(22f, Math.Max(h, 1f + 18f));
                UiNode.Of(inf).Place(0f, y, card.DescEl.W, rowH);
                y += rowH;
            }
        }

        // js: scenes/meta/cardFace.js:71
        /// <summary>
        /// underline(p, text, marks): each mark's characters get .ocb-term's dotted underline (--ink-muted, 2px, offset 4)
        /// and the title 「word：meaning」 while the pointer is on them. Nothing when the row does not print `text`.
        /// </summary>
        private static void Underline(TextMeshProUGUI p, string text, IReadOnlyList<TermMark> marks)
        {
            if (!p || marks == null || marks.Count == 0) return;
            LabelState st = Label.StateOf(p);
            if ((st.Text ?? p.text) != text) return;
            var kept = new List<TermMark>();
            int at = 0;
            foreach (TermMark m in marks.OrderBy(m => m.Start))
            {
                if (m.Start < at) continue;
                Ocb.TermUnderline(p, m.Start, m.Length);
                kept.Add(m);
                at = m.Start + m.Length;
            }
            if (kept.Count == 0) return;
            TitleTip.On(p, e =>
            {
                if (e == null) return null;
                int i = TMP_TextUtilities.FindIntersectingCharacter(p, e.position, e.enterEventCamera, true);
                if (i < 0) return null;
                foreach (TermMark m in kept)
                    if (i >= m.Start && i < m.Start + m.Length) return m.Word + "：" + m.Meaning;
                return null;
            });
        }

        // js: scenes/meta/cardFace.js:85
        /// <summary>
        /// scaledCard(face, scale, opts): a holder of the card's scaled size (round(216 × scale) × round(304 × scale),
        /// .ocbg-meta-cardslot) with the card inside at (0, 0), scaled from its top-left (the deck grid uses 0.72, the
        /// owned grid 0.68). The holder sits at (0, 0) of parent; place it.
        /// </summary>
        public static RectTransform ScaledCard(RectTransform parent, CardFace face, float scale, bool lit = false, bool unpayable = false) =>
            ScaledCard(parent, face, scale, out _, lit, unpayable);

        /// <summary>scaledCard, also handing out the card.</summary>
        public static RectTransform ScaledCard(RectTransform parent, CardFace face, float scale, out OcbCard card, bool lit = false, bool unpayable = false)
        {
            RectTransform holder = Ui.Div(parent, "ocbg-meta-cardslot", 0f, 0f, MetaUtil.JsRound(DsTokens.CardW * scale), MetaUtil.JsRound(DsTokens.CardH * scale));
            UiNode.Of(holder);
            Ocb.NoShrink(holder);
            card = CardEl(holder, face, lit, unpayable);
            card.Node.SetOrigin(0f, 0f);
            card.Node.SetStyleTransform(sx: scale, sy: scale);
            card.Node.Place(0f, 0f);
            return holder;
        }

        // js: scenes/meta/cardFace.js:98
        /// <summary>
        /// termRows(face): the right-click detail's terms (CardDetail.Terms) as 「<b>語</b>意味」 rows (.ocbg-meta-term:
        /// 400 16px/1.4 body; the word 700 16px/1.4 display, 10 px before the meaning), each a block `width` wide at
        /// (0, 0) of parent, as tall as its lines. Stack them (the departure's terms box: gap 4).
        /// </summary>
        public static List<TextMeshProUGUI> TermRows(RectTransform parent, CardFace face, float width)
        {
            var rows = new List<TextMeshProUGUI>();
            List<TermDefinition> terms = face?.Detail?.Terms;
            if (terms == null) return rows;
            TextStyle s = MetaStyle.Text(TermCss);
            s.RichText = true;
            string bold = "<font=\"" + TrialFonts.Tag(Family.Display, 700) + "\">";
            foreach (TermDefinition t in terms)
            {
                string text = bold + Escape(t.Word) + "</font><space=10>" + Escape(t.Meaning);
                float h = TextMeasure.Lines(text, s, width) * s.LineHeightPx;
                rows.Add(Label.Make(parent, text, s, 0f, 0f, width, h, "ocbg-meta-term"));
            }
            return rows;
        }

        /// <summary>Text made safe for TMP rich text (a literal '&lt;' cannot open a tag).</summary>
        private static string Escape(string text) => string.IsNullOrEmpty(text) ? "" : text.Replace("<", "<noparse><</noparse>");
    }
}
