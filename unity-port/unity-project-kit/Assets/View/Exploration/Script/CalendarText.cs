// Every word the exploration screen prints about the collapse calendar (seven_layers_v4.md §2,
// dungeon_exploration_v4.md §6.2 / §6.4 / §6.9) and about the layers and regions it is walked through
// (seven_layers_v4.md §3). Like ExplorationText, the numbers are passed in or read off the core's
// CalendarState and LayerProfile; nothing here counts a day.
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonCore;

namespace Exploration
{
    public static class CalendarText
    {
        /// <summary>The band's calendar: 「残り 312 日」.</summary>
        public static string DaysLabel(int daysLeft) => $"残り {daysLeft} 日";

        /// <summary>A step's calendar line: 「暦 残り 312 → 311 日」.</summary>
        public static string Change(int before, int after) => $"暦 残り {before} → {after} 日";

        /// <summary>A king is down, so no day of the calendar passes (seven_layers_v4.md §2.2).</summary>
        public const string Frozen = "暦は止まっている";

        /// <summary>The next new node costs no day: 早駆けの丸薬, or 日読みの砂 at a layer's entry.</summary>
        public const string DayFree = "次の 1 日は進まない";

        /// <summary>dungeon_exploration_v4.md §6.4: the one warning a step can carry.</summary>
        public const string ExpiryWarning = "ここで暦が尽き、龍神が目覚めます。世界が終わり、最初からやり直します";

        /// <summary>dungeon_exploration_v4.md §6.9: the end card of an expired calendar.</summary>
        public const string ExpiredLine = "暦が尽き、龍神が目覚めた。世界が終わり、最初からやり直す。";

        /// <summary>The king a region's king room holds (seven_layers_v4.md §3.1).</summary>
        public static string KingName(Region region) => region switch
        {
            Region.Dragon => "竜王",
            Region.Parasite => "爛れた世界樹",
            Region.Ghost => "死霊王",
            _ => throw new ArgumentOutOfRangeException(nameof(region), region, "only the three regions have a king"),
        };

        /// <summary>「竜の領域」: what a region is called on screen.</summary>
        public static string RegionName(Region region) => region.Label() + "の領域";

        /// <summary>
        /// "" while the calendar runs. While a king is down: 「竜王が戻るまで 41 日。暦は止まっている」, every downed king
        /// in the order of Lair.Regions.
        /// </summary>
        public static string Note(CalendarState calendar)
        {
            if (calendar == null) throw new ArgumentNullException(nameof(calendar));
            if (!calendar.IsFrozen) return "";
            IEnumerable<string> kings = Lair.Regions
                .Where(r => calendar.KingReturns.ContainsKey(r))
                .Select(r => $"{KingName(r)}が戻るまで {calendar.KingReturns[r]} 日");
            return string.Join("、", kings) + "。" + Frozen;
        }

        /// <summary>
        /// A step's calendar line, read off the two states the core gave (§6.4): 「暦は止まっている」 while a king is
        /// down, 「次の 1 日は進まない」 when the node costs no day, else 「暦 残り 312 → 311 日」.
        /// </summary>
        public static string ForecastLine(ExplorationState before, ExplorationState after)
        {
            if (before.Calendar.IsFrozen) return Frozen;
            if (after.DaysLeft == before.DaysLeft) return DayFree;
            return Change(before.DaysLeft, after.DaysLeft);
        }

        /// <summary>The band's line on what the next new node costs: 「次の 1 歩で 1 日進む」.</summary>
        public static string StepCosts(int days) => $"次の 1 歩で {days} 日進む";

        /// <summary>The band's line at the interlude: 「降りると 1 日進む」.</summary>
        public static string DescendCosts(int days) => $"降りると {days} 日進む";

        /// <summary>The interlude's line when the next layer's entry costs no day (日読みの砂).</summary>
        public const string DescendFree = "降りても入口では日が進まない";

        /// <summary>seven_layers_v4.md §2.2: shown on a king's node before the fight.</summary>
        public static string Extension(int days) => $"暦が {days} 日延びる見込み";

        /// <summary>The 「この生を終える」 card: what the hand-over to the next life costs (seven_layers_v4.md §2.1).</summary>
        public static string Handover(int days) => $"次の代の出立までに暦が {days} 日進む";

        /// <summary>The 「この生を終える」 card while a king is down: the hand-over costs no day.</summary>
        public const string HandoverFrozen = "王が倒れているあいだは、次の代の出立まで暦が進まない";

        /// <summary>
        /// The layer's title in the band and on its journal page (dungeon_exploration_v4.md §6.2): 「第 1 層　燦光の樹海」,
        /// a named region layer with its region 「第 3 層　大黒蛇の住処（竜の領域）」, and an unnamed one by the name the core
        /// gives it, 「竜の領域 第 4 層」.
        /// </summary>
        public static string LayerTitle(LayerProfile profile, bool clueTaken)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            string name = profile.DisplayName(clueTaken);
            if (profile.Region == Region.Common) return $"第 {profile.Layer} 層　{name}";
            string region = RegionName(profile.Region);
            return name.StartsWith(region, StringComparison.Ordinal) ? name : $"第 {profile.Layer} 層　{name}（{region}）";
        }

        /// <summary>
        /// The next layer as the interlude previews it (§6.5): its title, its node count and the days a straight run
        /// through it takes. The numbers are the map's: layers 1〜6 draw 7〜9 rows per map, not one size per layer (#1049).
        /// </summary>
        public static string LayerPreview(LayerProfile profile, LayerMap map, bool clueTaken)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            return $"{LayerTitle(profile, clueTaken)}: ノード {map.NodeCount}、真っ直ぐ抜けると {map.ShortestPathCost} 日";
        }

        /// <summary>The layer-two interlude's lead over the three ways down (§6.5).</summary>
        public const string RegionChoiceLead = "ここで 3 つの降り口に分かれる。選んだ領域は、この生のあいだ変えられない。";

        /// <summary>After the choice: 「竜の領域へ降りる」.</summary>
        public static string RegionChosen(Region region) => $"{RegionName(region)}へ降りる";

        /// <summary>The king layer's card once its king is down: layer 8 stays shut for now (seven_layers_v4.md §9).</summary>
        public const string KingLayerEnd = "これより下の層は、まだ開いていない。この生を終えて、次の代へ渡す。";
    }
}
