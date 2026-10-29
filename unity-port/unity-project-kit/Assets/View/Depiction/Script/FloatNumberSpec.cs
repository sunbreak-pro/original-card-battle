// The numbers that rise off a figure when it is hit (battle-visual-v1 §5.5 被弾, §3.1, §2.1;
// battle_ui_ux_v2 §5.0 浮かぶ数字, §5.6 順 2〜3; #455). Pure arithmetic with no engine types, like
// HpWickSpec and StatusPanelLayout, so the EditMode tests hold it directly.
//
// §5.5: Guard lost is a shield and the number in the Guard colour at 34 px; HP lost is a heart and
// the number in the HP ink at 48 px; each sits on a small black 80% plate. §5.6 順 3: when a blow
// takes both, the HP number stands 24 px to the right of the Guard one — one pair for one blow.
// §5.0: the number's centre rises from the chest (65% of the figure's height above its feet) to the
// head (84%), and a cause rides beside the number as a 16 px label in the body ink ("−2 出血").
//
// The amounts are the script's (Cue.Amount): nothing here counts damage. Which cue pairs with which
// is read off the order the script wrote them in.
using System;
using System.Collections.Generic;

namespace Depiction
{
    /// <summary>What a rising number tells.</summary>
    public enum FloatNumberKind
    {
        /// <summary>Guard a blow or the wall took off (§5.6 順 2): the shield, the Guard colour, 34 px.</summary>
        GuardLoss,
        /// <summary>HP lost (§5.6 順 3, 出血, a 見切り return): the heart, the HP ink, 48 px.</summary>
        HpLoss,
        /// <summary>HP won back (再生, a heal face): the heart and the HP ink too (§2.1 HP の文字 「回復の数」).</summary>
        HpGain,
    }

    /// <summary>Where each part of a plate sits, in pixels from the plate's left edge.</summary>
    public readonly struct FloatPlateLayout
    {
        public FloatPlateLayout(float width, float height, float iconCentre, float numberCentre, float labelCentre)
        {
            Width = width;
            Height = height;
            IconCentre = iconCentre;
            NumberCentre = numberCentre;
            LabelCentre = labelCentre;
        }

        public float Width { get; }
        public float Height { get; }
        public float IconCentre { get; }
        public float NumberCentre { get; }
        /// <summary>The cause label's centre; 0 when the plate has no label.</summary>
        public float LabelCentre { get; }
    }

    public static class FloatNumberSpec
    {
        /// <summary>§3.1 / §5.5: Guard lost is written in IBM Plex Mono 700 at 34 px.</summary>
        public const int GuardFontPx = 34;

        /// <summary>§3.1 / §5.5: HP lost is written in IBM Plex Mono 700 at 48 px.</summary>
        public const int HpFontPx = 48;

        /// <summary>§5.0 脇ラベル: the cause beside the number is 16 px in the body ink.</summary>
        public const int LabelFontPx = 16;

        /// <summary>§2.1 Guard.</summary>
        public const string GuardColorHex = "#8fc4d8";

        /// <summary>§2.1 HP の文字: 24 px and larger HP numbers, lost or won back.</summary>
        public const string HpInkHex = "#e36a5e";

        /// <summary>§2.1 本文: the cause label (text under 24 px is always the body ink).</summary>
        public const string LabelInkHex = "#ecebf2";

        /// <summary>§5.5 下地は黒 80% の小札.</summary>
        public const float PlateAlpha = 0.8f;

        /// <summary>§3.2: a panel's corners.</summary>
        public const int PlateCorner = 6;

        /// <summary>§3.2 余白の段: 12 px either side of the plate's contents.</summary>
        public const float PadX = 12f;

        /// <summary>§3.2 余白の段: 4 px above and below.</summary>
        public const float PadY = 4f;

        /// <summary>§3.2 余白の段: 8 px between the icon, the number and the label.</summary>
        public const float Gap = 8f;

        /// <summary>§5.6 順 3: the HP number stands 24 px beside the Guard one.</summary>
        public const float PairGap = 24f;

        /// <summary>§5.0: the number's centre starts at the chest, 65% of the figure's height above its feet.</summary>
        public const float RiseFromRatio = 0.65f;

        /// <summary>§5.0: and ends at the head, 84%.</summary>
        public const float RiseToRatio = 0.84f;

        public static int FontPx(FloatNumberKind kind) => kind == FloatNumberKind.GuardLoss ? GuardFontPx : HpFontPx;

        /// <summary>The icon is as tall as the number it leads (§5.5 「盾と Guard の色の 34 px」).</summary>
        public static int IconPx(FloatNumberKind kind) => FontPx(kind);

        public static string ColorHex(FloatNumberKind kind) => kind == FloatNumberKind.GuardLoss ? GuardColorHex : HpInkHex;

        /// <summary>True when the plate leads with the shield, false for the heart.</summary>
        public static bool LeadsWithShield(FloatNumberKind kind) => kind == FloatNumberKind.GuardLoss;

        /// <summary>
        /// The number as written: "−3" for a loss, "+2" for a gain. The amount is the script's and
        /// its sign is dropped, since the kind already says which way it went.
        /// </summary>
        public static string NumberText(FloatNumberKind kind, int amount)
        {
            int magnitude = Math.Abs(amount);
            return (kind == FloatNumberKind.HpGain ? "+" : "−") + magnitude;
        }

        /// <summary>An HpChange cue's kind, from its signed amount (#188).</summary>
        public static FloatNumberKind OfHpChange(int signedAmount) =>
            signedAmount > 0 ? FloatNumberKind.HpGain : FloatNumberKind.HpLoss;

        /// <summary>
        /// The plate's size and where its icon, number and label sit, from the widths the View measured
        /// for the number and the label (0 for no label).
        /// </summary>
        public static FloatPlateLayout Layout(FloatNumberKind kind, float numberWidth, float labelWidth)
        {
            float icon = IconPx(kind);
            float number = Math.Max(0f, numberWidth);
            float label = Math.Max(0f, labelWidth);
            float iconCentre = PadX + icon / 2f;
            float numberCentre = PadX + icon + Gap + number / 2f;
            float right = PadX + icon + Gap + number;
            float labelCentre = 0f;
            if (label > 0f)
            {
                labelCentre = right + Gap + label / 2f;
                right += Gap + label;
            }
            float height = Math.Max(icon, FontPx(kind)) + 2f * PadY;
            return new FloatPlateLayout(right + PadX, height, iconCentre, numberCentre, labelCentre);
        }

        /// <summary>
        /// The plate's centre, across, from the figure's centre line. Alone, a plate stands on the line.
        /// In a pair (§5.6 順 2〜3) the Guard plate stands left of it and the HP plate right, 24 px
        /// apart, so the pair is centred on the figure whichever side it is on.
        /// </summary>
        public static float CentreX(FloatNumberKind kind, bool paired, float plateWidth)
        {
            if (!paired) return 0f;
            float half = PairGap / 2f + Math.Max(0f, plateWidth) / 2f;
            return kind == FloatNumberKind.GuardLoss ? -half : half;
        }

        /// <summary>§5.0: how far above the feet the number's centre starts, for a figure <paramref name="height"/> px tall.</summary>
        public static float StartAbove(float height) => Math.Max(0f, height) * RiseFromRatio;

        /// <summary>§5.0: how far above the feet it ends.</summary>
        public static float EndAbove(float height) => Math.Max(0f, height) * RiseToRatio;

        /// <summary>
        /// §5.6 順 2〜3: the Guard number at <paramref name="index"/> pairs with an HP number when the
        /// script's next cue takes HP off the same unit — the blow Guard took only part of (Slash,
        /// GuardBlock, Hit), the wall, a 見切り return.
        /// </summary>
        public static bool GuardPairsWithHp(IList<Cue> cues, int index)
        {
            if (cues == null || index < 0 || index + 1 >= cues.Count) return false;
            Cue guard = cues[index];
            if (guard == null || guard.Kind != CueKind.GuardBlock) return false;
            return TakesHp(cues[index + 1], guard.Target);
        }

        /// <summary>The HP number at <paramref name="index"/> pairs with the Guard number of the cue just before it.</summary>
        public static bool HpPairsWithGuard(IList<Cue> cues, int index)
        {
            if (cues == null || index < 1 || index >= cues.Count) return false;
            Cue hp = cues[index];
            Cue before = cues[index - 1];
            if (hp == null || before == null || before.Kind != CueKind.GuardBlock) return false;
            return TakesHp(hp, before.Target);
        }

        private static bool TakesHp(Cue cue, UnitSide target)
        {
            if (cue == null || cue.Target != target) return false;
            if (cue.Kind == CueKind.Hit) return cue.Amount > 0;
            return cue.Kind == CueKind.HpChange && cue.Amount < 0;
        }
    }
}
