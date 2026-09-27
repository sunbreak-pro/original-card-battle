// Every word the exploration screen prints about a node kind, an item or a number. UI text is
// Japanese (CLAUDE.md); the numbers are passed in, never worked out here.
using System;
using System.Linq;
using System.Text.RegularExpressions;
using DungeonContent;
using DungeonCore;

namespace Exploration
{
    public static class ExplorationText
    {
        /// <summary>The one-character stand-in for a node's icon until #90's art arrives.</summary>
        public static string Glyph(NodeKind kind) => kind switch
        {
            NodeKind.Battle => "戦",
            NodeKind.Elite => "精",
            NodeKind.Boss => "主",
            NodeKind.Rest => "休",
            NodeKind.Survey => "調",
            NodeKind.Event => "事",
            NodeKind.Trace => "痕",
            NodeKind.Carving => "彫",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        public static string KindLabel(NodeKind kind) => kind switch
        {
            NodeKind.Battle => "通常戦闘",
            NodeKind.Elite => "精鋭",
            NodeKind.Boss => "階層ボス",
            NodeKind.Rest => "休息",
            NodeKind.Survey => "情報収集",
            NodeKind.Event => "イベント",
            NodeKind.Trace => "死亡地点の痕跡",
            NodeKind.Carving => "秘跡の彫り",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        /// <summary>The threat word a combat node shows (dungeon_exploration_v4.md §6.3). Empty for the rest.</summary>
        public static string Threat(NodeKind kind) => kind switch
        {
            NodeKind.Battle => "通常",
            NodeKind.Elite => "精鋭",
            NodeKind.Boss => "階層の主",
            _ => "",
        };

        public static bool IsCombat(NodeKind kind) =>
            kind == NodeKind.Battle || kind == NodeKind.Elite || kind == NodeKind.Boss;

        public static string LayerTitle(int layer, string name) => $"第 {layer} 層　{name}";

        public const string UnknownEnemy = "？";

        public const string EmptySlot = "（空き）";

        public const string JournalButton = "手記（J）";

        public const string EndLifeButton = "この生を終える";

        public const string FarNode = "遠いノードは種別しか分かりません";

        /// <summary>An item's name from the catalogue, or its id when the catalogue does not know it.</summary>
        public static string ItemName(string id) =>
            ItemCatalogue.All.FirstOrDefault(i => i.Id == id)?.Name ?? id;

        /// <summary>The catalogue's note without the design-document pointers it carries for the team.</summary>
        public static string ItemNote(string id) =>
            DocPointer.Replace(ItemCatalogue.All.FirstOrDefault(i => i.Id == id)?.Note ?? "", "").Trim();

        private static readonly Regex DocPointer = new Regex(@"（[^（）]*\.md[^（）]*）");

        /// <summary>「7 → 6」.</summary>
        public static string Change(int before, int after) => $"{before} → {after}";
    }
}
