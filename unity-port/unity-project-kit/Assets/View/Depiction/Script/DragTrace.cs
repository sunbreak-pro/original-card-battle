// One frame of a card drag written as one JSON line (#643). DragTraceRecorder reads the scene each
// frame (the held card, the lit receiver, the lit floor cells, the stamina "▸ n") and hands the values
// here; this file only formats them, so `dotnet test` can hold the line's shape without an Editor.
// The fields are the issue's list, in its order; numbers are written with the invariant culture so a
// Japanese locale's decimal mark never reaches the file.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Depiction
{
    /// <summary>Which drop target lights while a card is held: the enemy, the player's own figure, or none.</summary>
    public enum DragReceiver
    {
        None,
        Enemy,
        Self,
    }

    /// <summary>The held (or last held) card's transform and opacity in one frame.</summary>
    public sealed class DragTraceCard
    {
        public string Id;
        /// <summary>True between the drag's start and its end; after the release the card is still traced while it returns.</summary>
        public bool Held;
        public float X;
        public float Y;
        /// <summary>localEulerAngles, degrees.</summary>
        public float RotX;
        public float RotY;
        public float RotZ;
        public float ScaleX;
        public float ScaleY;
        public float ScaleZ;
        /// <summary>The card's CanvasGroup alpha, 0〜1.</summary>
        public float Alpha;
    }

    public sealed class DragTraceFrame
    {
        public int Frame;
        /// <summary>Time.unscaledTime, seconds.</summary>
        public double Time;
        /// <summary>The pointer in screen pixels, from the drag events; null before the first event.</summary>
        public float? PointerX;
        public float? PointerY;
        /// <summary>The card being traced; null when no card has been picked up since the trace began.</summary>
        public DragTraceCard Card;
        public DragReceiver Receiver;
        /// <summary>The lit floor cells, 1-based, as FloorView names them (Cell1, Cell2, …).</summary>
        public IList<int> ReachCells;
        /// <summary>The stamina the held card would pay ("▸ n" on the stamina tag); null when the tag shows none.</summary>
        public int? StaminaPay;

        /// <summary>The frame as one JSON object on one line, no trailing newline.</summary>
        public string ToJson()
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"frame\":").Append(Frame.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"time\":").Append(Time.ToString("0.000000", CultureInfo.InvariantCulture));
            sb.Append(",\"pointer\":");
            if (PointerX.HasValue && PointerY.HasValue) sb.Append('[').Append(Num(PointerX.Value)).Append(',').Append(Num(PointerY.Value)).Append(']');
            else sb.Append("null");
            sb.Append(",\"card\":");
            if (Card == null) sb.Append("null");
            else
            {
                sb.Append("{\"id\":");
                AppendString(sb, Card.Id);
                sb.Append(",\"held\":").Append(Card.Held ? "true" : "false");
                sb.Append(",\"pos\":[").Append(Num(Card.X)).Append(',').Append(Num(Card.Y)).Append(']');
                sb.Append(",\"rot\":[").Append(Num(Card.RotX)).Append(',').Append(Num(Card.RotY)).Append(',').Append(Num(Card.RotZ)).Append(']');
                sb.Append(",\"scale\":[").Append(Num(Card.ScaleX)).Append(',').Append(Num(Card.ScaleY)).Append(',').Append(Num(Card.ScaleZ)).Append(']');
                sb.Append(",\"alpha\":").Append(Num(Card.Alpha));
                sb.Append('}');
            }
            sb.Append(",\"receiver\":\"").Append(ReceiverName(Receiver)).Append('"');
            sb.Append(",\"reach\":[");
            if (ReachCells != null)
            {
                for (int i = 0; i < ReachCells.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(ReachCells[i].ToString(CultureInfo.InvariantCulture));
                }
            }
            sb.Append(']');
            sb.Append(",\"pay\":").Append(StaminaPay.HasValue ? StaminaPay.Value.ToString(CultureInfo.InvariantCulture) : "null");
            sb.Append('}');
            return sb.ToString();
        }

        public static string ReceiverName(DragReceiver receiver)
        {
            switch (receiver)
            {
                case DragReceiver.Enemy: return "enemy";
                case DragReceiver.Self: return "self";
                default: return "none";
            }
        }

        // Three decimals: sub-pixel positions and small angle changes stay readable, the line stays short.
        private static string Num(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return "null";
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static void AppendString(StringBuilder sb, string value)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }

    public static class DragTrace
    {
        /// <summary>
        /// The number on the stamina tag's "▸ n" (StaminaTagView), or null when the label is hidden or
        /// carries no number. The tag hides the label at 0, so a shown label always means a held card.
        /// </summary>
        public static int? ParsePay(string text, bool shown)
        {
            if (!shown || string.IsNullOrEmpty(text)) return null;
            int i = text.Length;
            while (i > 0 && char.IsDigit(text[i - 1])) i--;
            if (i == text.Length) return null;
            return int.TryParse(text.Substring(i), NumberStyles.None, CultureInfo.InvariantCulture, out int pay) ? pay : (int?)null;
        }

        /// <summary>The cell number in a FloorView cell's name ("Cell3" → 3), or 0 when the name is not a cell's.</summary>
        public static int CellNumber(string name)
        {
            const string Prefix = "Cell";
            if (name == null || !name.StartsWith(Prefix, StringComparison.Ordinal) || name.Length == Prefix.Length) return 0;
            return int.TryParse(name.Substring(Prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int n) ? n : 0;
        }
    }
}
