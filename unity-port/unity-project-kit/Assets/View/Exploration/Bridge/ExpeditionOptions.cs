// Trial extension (port of the browser trial's src/bridge/exploration/Expedition.js:48 `options`, and of the page
// count src/app/life.js lifeSetup hands the Expedition; docs/EXTENSIONS.md, docs/INHERITANCE.md R-27): what the
// trial turns on in an Expedition. Left out (null), or with every member at its default, the Expedition is the C#
// as it was.
using BattleCore;

namespace Exploration.Bridge
{
    /// <summary>
    /// The browser trial's <c>new Expedition(..., options)</c> (js: bridge/exploration/Expedition.js:55
    /// <c>{ limits, trial, carvingKnown }</c>), plus the inherited page count that src/app/life.js adds by wrapping
    /// the Expedition's BeginLife (C# cannot wrap a method, so the Expedition counts them itself).
    /// </summary>
    public sealed class ExpeditionOptions
    {
        /// <summary>
        /// The life's deck limits (docs/EXTENSIONS.md 「出立の枠（DeckLimits）」: a survivor's heir may hold 45 cards):
        /// the start deck is checked against them, a learned card joins the deck under their DeckMax, and every
        /// battle's launch holds the deck to them. Null: §8's constants, as the C# has them.
        /// </summary>
        public DeckLimits Limits { get; set; }

        /// <summary>
        /// The trial rules (the same as RunStart.Trial): every carried item acts, found consumables enter the slots,
        /// events resolve, the result cards wait until they are closed. False plays the C#.
        /// </summary>
        public bool Trial { get; set; }

        /// <summary>
        /// The lineage read the layer-two carving in an earlier life (docs/INHERITANCE.md R-20): it becomes
        /// RunStart.CarvingKnown. Read in trial mode only; null leaves RunStart.CarvingKnown as it is.
        /// </summary>
        public bool? CarvingKnown { get; set; }

        /// <summary>
        /// The rare pages the heir sets out with (a survivor's whole journal, docs/INHERITANCE.md R-13): they count
        /// among the life's pages from the start and are inherited, not found (Expedition.InheritedPages, R-27).
        /// js: app/life.js:205-211. 0 (or less, as js: app/life.js:74 startPages reads it): none.
        /// </summary>
        public int StartPages { get; set; }
    }
}
