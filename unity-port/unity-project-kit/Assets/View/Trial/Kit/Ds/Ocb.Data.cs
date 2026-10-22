// The static data the DS components read (vendor/ds/bundle.js:165-190, 304-312, 454-455): STATUS, ATTR, OMEN_KIND,
// PILE_ICON, PILE_NAME and reachText. Every string is the DS's, byte for byte; the colours are the tokens the JS
// names with var(--…) (the CSS text is kept beside each for diffing).
using System;
using System.Collections.Generic;
using Trial.Geometry;

namespace Trial.Kit
{
    /// <summary>One row of OCB.STATUS: the chip's word, whether the status is one you put on yourself, the tooltip texts.</summary>
    public sealed class OcbStatusInfo
    {
        public readonly string Name;
        /// <summary>self: the chip is round (is-self) and the tip reads 自分に付ける系.</summary>
        public readonly bool Self;
        /// <summary>The tip's 効き方 row.</summary>
        public readonly string How;
        /// <summary>The tip's 減り方 row.</summary>
        public readonly string Decay;

        public OcbStatusInfo(string name, bool self, string how, string decay)
        {
            Name = name;
            Self = self;
            How = how;
            Decay = decay;
        }
    }

    /// <summary>One row of OCB.ATTR: a card attribute's word, icon, frame colour (--attr) and value ink (--attr-ink).</summary>
    public sealed class OcbAttrInfo
    {
        public readonly string Name;
        public readonly string Icon;
        /// <summary>The CSS text of the frame colour ("var(--violet)").</summary>
        public readonly string FrameCss;
        /// <summary>The CSS text of the ink ("var(--phosphor)").</summary>
        public readonly string InkCss;

        public OcbAttrInfo(string name, string icon, string frameCss, string inkCss)
        {
            Name = name;
            Icon = icon;
            FrameCss = frameCss;
            InkCss = inkCss;
        }

        public Rgba Frame => CssColor.Parse(FrameCss);
        public Rgba Ink => CssColor.Parse(InkCss);
    }

    /// <summary>One row of OMEN_KIND: an omen kind's icon and colour.</summary>
    public sealed class OcbOmenKind
    {
        public readonly string Icon;
        public readonly string ColorCss;

        public OcbOmenKind(string icon, string colorCss)
        {
            Icon = icon;
            ColorCss = colorCss;
        }

        public Rgba Color => CssColor.Parse(ColorCss);
    }

    public static partial class Ocb
    {
        // js: vendor/ds/bundle.js:168
        /// <summary>OCB.STATUS: status id → word, self, 効き方, 減り方.</summary>
        public static readonly IReadOnlyDictionary<string, OcbStatusInfo> Status = new Dictionary<string, OcbStatusInfo>(StringComparer.Ordinal)
        {
            ["bleed"] = new OcbStatusInfo("出血", false, "相手のターン開始に HP −2×スタック", "ターンごとに 1 減る"),
            ["brittle"] = new OcbStatusInfo("脆化", false, "次に受ける攻撃の威力が 1.5 倍（Guard で引く前の値）", "効くと 1 減る"),
            ["slow"] = new OcbStatusInfo("鈍足", false, "次のターン、自分が動くマスと、押す / 引くマスが 1 減る", "ターンごとに 1 減る"),
            ["intimidate"] = new OcbStatusInfo("威圧", false, "次の行動の威力と Guard が −3", "効くと 1 減る"),
            ["fatigue"] = new OcbStatusInfo("疲労", false, "次のターン開始のスタミナ回復が −1", "ターンごとに 1 減る"),
            ["empower"] = new OcbStatusInfo("強化", true, "次の攻撃の威力が 1.5 倍", "効くと 1 減る"),
            ["focus"] = new OcbStatusInfo("集中", true, "次に出す札の値が 1 段強くなる（コストは変わらない）", "効くと 1 減る"),
            ["insight"] = new OcbStatusInfo("見切り", true, "次に攻撃を受けたとき、Guard で防いだ分の半分を相手に返す", "効くと 1 減る（Guard 0 で受けたときは減らない）"),
            ["agile"] = new OcbStatusInfo("俊敏", true, "ターン開始に、1 マスだけ無料で前か後ろへ動ける", "ターンごとに 1 減る"),
            ["regen"] = new OcbStatusInfo("再生", true, "ターン開始に HP +2×スタック", "ターンごとに 1 減る"),
        };

        /// <summary>STATUS's ids in the DS's order.</summary>
        public static readonly IReadOnlyList<string> StatusIds = new[] { "bleed", "brittle", "slow", "intimidate", "fatigue", "empower", "focus", "insight", "agile", "regen" };

        // js: vendor/ds/bundle.js:182
        /// <summary>OCB.ATTR: card attribute → word, icon, frame, ink.</summary>
        public static readonly IReadOnlyDictionary<string, OcbAttrInfo> Attr = new Dictionary<string, OcbAttrInfo>(StringComparer.Ordinal)
        {
            ["attack"] = new OcbAttrInfo("攻撃", "sword", "var(--violet)", "var(--phosphor)"),
            ["defense"] = new OcbAttrInfo("防御", "shield", "var(--guard)", "var(--guard)"),
            ["skill"] = new OcbAttrInfo("スキル", "star", "var(--amber)", "var(--amber)"),
            ["stance"] = new OcbAttrInfo("スタンス", "flag", "var(--crimson)", "var(--hp-text)"),
        };

        /// <summary>The attribute keys in the DS's order (PlayedAttrs walks them so).</summary>
        public static readonly IReadOnlyList<string> AttrKeys = new[] { "attack", "defense", "skill", "stance" };

        /// <summary>ATTR[key] || ATTR.attack.</summary>
        public static OcbAttrInfo AttrOf(string key) => key != null && Attr.TryGetValue(key, out OcbAttrInfo a) ? a : Attr["attack"];

        // js: vendor/ds/bundle.js:190
        /// <summary>reachText(r): "n" when both ends match, else "a〜b".</summary>
        public static string ReachText(string min, string max) => min == max ? min : min + "〜" + max;

        /// <summary>reachText for numbers.</summary>
        public static string ReachText(int min, int max) => ReachText(min.ToString(System.Globalization.CultureInfo.InvariantCulture), max.ToString(System.Globalization.CultureInfo.InvariantCulture));

        // js: vendor/ds/bundle.js:304
        /// <summary>OMEN_KIND: an omen's kind (or icon key) → icon and colour.</summary>
        public static readonly IReadOnlyDictionary<string, OcbOmenKind> OmenKind = new Dictionary<string, OcbOmenKind>(StringComparer.Ordinal)
        {
            ["攻撃"] = new OcbOmenKind("sword", "var(--phosphor)"),
            ["防御"] = new OcbOmenKind("shield", "var(--guard)"),
            ["移動"] = new OcbOmenKind("back", "var(--ink)"),
            ["構え"] = new OcbOmenKind("flag", "var(--ink)"),
            ["技"] = new OcbOmenKind("star", "var(--ink)"),
            ["状態"] = new OcbOmenKind("seal", "var(--ink)"),
            ["休み"] = new OcbOmenKind("rest", "var(--steel)"),
        };

        /// <summary>OMEN_KIND[d.icon || d.kind] || OMEN_KIND['技'] (bundle.js:317, 334).</summary>
        public static OcbOmenKind OmenKindOf(string icon, string kind)
        {
            string key = !string.IsNullOrEmpty(icon) ? icon : kind;
            return key != null && OmenKind.TryGetValue(key, out OcbOmenKind k) ? k : OmenKind["技"];
        }

        // js: vendor/ds/bundle.js:454
        /// <summary>PILE_ICON: pile kind → icon.</summary>
        public static readonly IReadOnlyDictionary<string, string> PileIcon = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["deck"] = "draw", ["discard"] = "discard", ["exile"] = "exile",
        };

        // js: vendor/ds/bundle.js:455
        /// <summary>PILE_NAME: pile kind → its word.</summary>
        public static readonly IReadOnlyDictionary<string, string> PileName = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["deck"] = "山札", ["discard"] = "捨札", ["exile"] = "除外",
        };
    }
}
