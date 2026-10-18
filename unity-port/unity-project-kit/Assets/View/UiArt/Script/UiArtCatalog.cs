// Which parts of the battle UI are drawn as art, and where each one loads from (battle-visual-v1 §11, #92).
// Pure C#, so `dotnet test` holds the list the art is ordered from and the screen loads.
//
// The rule of §11: a part whose job is to carry the world's feel at a glance (the four attributes, the
// statuses, the marks the effects leave) is drawn; a part whose job is to be read exactly (frames, bars,
// pips, arrows, the gap, the speed chevrons, the numbers) stays code, because a line drawn in code stays
// sharp at every size and changes colour with the role tokens for free. Every drawn part is one white
// shape on a clear ground, 128×128: the code tints it with the role colour, as it tints the line icons
// now, so the colour rules of §2 hold with or without art. Until a part's art is in, the screen keeps
// the code-drawn shape named in Fallback (the Appendix A name).
namespace UiArt
{
    public enum UiArtKind
    {
        /// <summary>The attribute icon on a card and in the played-attribute row (§2.2 / §6.1).</summary>
        Attribute,
        /// <summary>A status chip's 18 px icon, and the 20 px status icon on an omen (§4.4 / §7.3).</summary>
        Status,
        /// <summary>A boss-only status: drawn like a status, framed double in the boss colour (§7.3).</summary>
        BossStatus,
        /// <summary>The stamp that says a card or an omen puts a status on (Appendix A 状態付与).</summary>
        Stamp,
        /// <summary>A mark an effect leaves on a figure (battle_ui_ux_v2 §8: adaptation, wall, agility, stay).</summary>
        EffectMark,
    }

    /// <summary>One drawn part: its id (the file name), its name on screen, and the code shape it replaces.</summary>
    public sealed class UiArtPart
    {
        public string Id;
        public string Name;
        public UiArtKind Kind;
        /// <summary>The Appendix A icon the code draws until the art is in; empty when the code draws a text chip instead.</summary>
        public string Fallback;
        /// <summary>The largest size the part is shown at on the 1920×1080 screen, in px.</summary>
        public int ShownPx;

        /// <summary>Where the art loads from under Resources (no extension): UiArt/icon-&lt;id&gt;.</summary>
        public string ResourcePath => UiArtCatalog.Folder + "/icon-" + Id;
    }

    public static class UiArtCatalog
    {
        /// <summary>The Resources folder the art goes in.</summary>
        public const string Folder = "UiArt";

        /// <summary>Every drawn part is drawn at this square size (px), 4 times the 34 px omen icon rounded up.</summary>
        public const int SourcePx = 128;

        public static readonly UiArtPart[] Parts =
        {
            // §2.2 / §6.1: one attribute per card, four in all (battle_core_v4 §2.1).
            Part("attack", "攻撃", UiArtKind.Attribute, "威力（剣）", 34),
            Part("defense", "防御", UiArtKind.Attribute, "Guard（盾）", 34),
            Part("skill", "スキル", UiArtKind.Attribute, "スキル", 34),
            Part("stance", "スタンス", UiArtKind.Attribute, "スタンス（旗）", 34),

            // §7.3: put on the foe (corners 3 px) ...
            Part("bleed", "出血", UiArtKind.Status, "出血", 20),
            Part("brittle", "脆化", UiArtKind.Status, "脆化", 20),
            Part("slow", "鈍足", UiArtKind.Status, "鈍足", 20),
            Part("intimidate", "威圧", UiArtKind.Status, "威圧", 20),
            Part("fatigue", "疲労", UiArtKind.Status, "疲労", 20),
            // ... and put on oneself (corners 15 px).
            Part("empower", "強化", UiArtKind.Status, "強化", 20),
            Part("focus", "集中", UiArtKind.Status, "集中", 20),
            Part("foresight", "見切り", UiArtKind.Status, "見切り", 20),
            Part("agile", "俊敏", UiArtKind.Status, "俊敏", 20),
            Part("regen", "再生", UiArtKind.Status, "再生", 20),

            // §4.4 / §7.3: boss-only statuses have no Appendix A icon; the code writes the name instead.
            Part("miasma-cloak", "瘴気纏い", UiArtKind.BossStatus, "", 20),
            Part("binding", "呪縛", UiArtKind.BossStatus, "", 20),

            Part("inflict", "状態付与", UiArtKind.Stamp, "状態付与（印）", 20),

            // battle_ui_ux_v2 §8: the marks the effects add (adaptation 3, wall 1, agility arrow and stay 2).
            Part("adapt-1", "適応の印 1", UiArtKind.EffectMark, "", 24),
            Part("adapt-2", "適応の印 2", UiArtKind.EffectMark, "", 24),
            Part("adapt-3", "適応の印 3", UiArtKind.EffectMark, "", 24),
            Part("wall", "壁の印", UiArtKind.EffectMark, "", 24),
            Part("agile-arrow", "俊敏の矢印", UiArtKind.EffectMark, "前へ", 24),
            Part("stay", "留まる", UiArtKind.EffectMark, "", 24),
        };

        /// <summary>
        /// The Appendix A icons that stay code (§11): they are read for exactly what they say, so a crisp
        /// line beats a drawn shape. Listed so a new icon has to be put on one side or the other.
        /// </summary>
        public static readonly string[] StaysCode =
        {
            "HP（ハート）", "回復", "前へ", "後ろへ", "間合い", "永続（∞）", "スタミナの粒", "ドロー / 山札", "捨札", "除外",
            "ターン（砂時計）", "階層", "瘴気", "手記", "自分（人型）", "狙うマス（斜線）", "「+n」", "休み（横線）",
            "速さ ×1.0（山形 1 本）", "速さ ×1.25（山形 2 本）", "速さ ×1.5（山形 3 本）",
        };

        /// <summary>The part called <paramref name="id"/>, or null when nothing of that name is drawn.</summary>
        public static UiArtPart Find(string id)
        {
            foreach (UiArtPart part in Parts)
            {
                if (part.Id == id) return part;
            }
            return null;
        }

        private static UiArtPart Part(string id, string name, UiArtKind kind, string fallback, int shownPx)
        {
            return new UiArtPart { Id = id, Name = name, Kind = kind, Fallback = fallback, ShownPx = shownPx };
        }
    }
}
