// A card as the design system's OCB.Card draws it, from the face the ported bridge writes (port of
// src/scenes/explore/cardFace.js: CoreText.FaceOf → CardFaceText). Only the shape changes here: every word and number
// is the face's. Owned by EX-LEAF.
//
// The JS def also carries `hover` (face.HoverLine); OCB.Card does not read it and OcbCardDef has no slot for it, so it
// comes out of the CardDefOf overload with an out parameter.
using System;
using System.Collections.Generic;
using Depiction.Bridge;
using Trial.Kit;

namespace Trial.Explore
{
    public static class ExploreCardFace
    {
        // js: scenes/explore/cardFace.js:6
        /// <summary>ATTR: the face's CardKind (by name) → the design system's attribute; a movement card is drawn as a skill.</summary>
        public static readonly IReadOnlyDictionary<string, string> Attr = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Attack"] = "attack", ["Guard"] = "defense", ["Skill"] = "skill", ["Stance"] = "stance", ["Move"] = "skill",
        };

        // js: scenes/explore/cardFace.js:7
        /// <summary>VALUE_ICON: a value column's CardValueKind (by name) → its icon; anything else draws `plus`.</summary>
        public static readonly IReadOnlyDictionary<string, string> ValueIcon = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Power"] = "sword", ["Guard"] = "shield", ["Heal"] = "heal", ["Close"] = "forward", ["Back"] = "back", ["Push"] = "forward", ["Pull"] = "back", ["Draw"] = "draw",
        };

        // js: scenes/explore/cardFace.js:10
        /// <summary>cardDefOf(def): the OCB.Card definition of a CardDef, through the bridge's face.</summary>
        public static OcbCardDef CardDefOf(BattleCore.CardDef def) => CardDefOf(def, out _);

        /// <summary>cardDefOf(def) with the def's `hover` (face.HoverLine, "" when none).</summary>
        public static OcbCardDef CardDefOf(BattleCore.CardDef def, out string hover)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            global::Depiction.CardFace face = CoreText.FaceOf(def);
            string attr = Attr.TryGetValue(face.Kind.ToString(), out string a) ? a : "skill";
            var o = new OcbCardDef
            {
                Name = face.Name ?? "",
                Attr = attr,
                // face.PrintedCost != null ? face.PrintedCost : face.Cost — CardFaceText.Fill always writes PrintedCost
                Cost = face.PrintedCost,
            };
            // CoreText.ReachText is empty for a card aimed at the one playing it: the card prints 自分.
            if (!string.IsNullOrEmpty(face.RequiredRangeGlyph)) o.WithReach(face.RequiredRangeGlyph, face.RequiredRangeGlyph);
            else o.ReachSelf = true;
            foreach (global::Depiction.CardValue v in face.Values ?? new List<global::Depiction.CardValue>())
                o.Values.Add(new OcbCardValue(ValueIcon.TryGetValue(v.Kind.ToString(), out string icon) ? icon : "plus", v.Number, v.Word));
            List<string> lines = face.TextLines ?? new List<string>();
            o.Desc = face.Lasting ? "" : string.Join("", lines);
            o.Lines = face.Lasting ? new List<string>(lines) : new List<string>();
            o.Trait = face.Plain || (string.IsNullOrEmpty(face.TraitTop) && string.IsNullOrEmpty(face.TraitBottom))
                ? null
                : new OcbCardTrait(face.TraitTop, face.TraitBottom);
            hover = face.HoverLine ?? "";
            return o;
        }

        // js: scenes/explore/cardFace.js:31
        /// <summary>
        /// cardDefById(id, book): the OCB.Card definition of a card id; `book` (a MasteryBook) grows the card to this life's
        /// step. Throws on an unknown id (CardCatalog.ById), as the JS does (its callers catch and draw a stub).
        /// </summary>
        public static OcbCardDef CardDefById(string id, BattleCore.MasteryBook book)
        {
            BattleCore.CardDef def = BattleCore.CardCatalog.ById(id);
            if (book != null) def = book.Mastered(def);
            return CardDefOf(def);
        }

        // js: scenes/explore/cardFace.js:38
        /// <summary>cardName(id, book): the card's name as this life knows it (the id when it is unknown).</summary>
        public static string CardName(string id, BattleCore.MasteryBook book)
        {
            try
            {
                BattleCore.CardDef def = BattleCore.CardCatalog.ById(id);
                if (book != null) def = book.Mastered(def);
                return def.Name;
            }
            catch (Exception)
            {
                return id;
            }
        }
    }
}
