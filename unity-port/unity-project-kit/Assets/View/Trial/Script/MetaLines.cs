// The meta screens' own trace lines over Inheritance's views: the two small rules the hall and the ending
// card add in the JS (scenes/meta/hall.js pastTraceLine, scenes/meta/ending.js withWaitingTrace). Pure: they
// read the lineage and a view and return a line or a view; nothing is written (R-12).
using System.Collections.Generic;
using System.Linq;
using DungeonCore;
using InheritanceCore;

namespace Trial.Script
{
    public static class MetaLines
    {
        /// <summary>
        /// A past life's trace line on its card. HallView says 「痕跡：第 n 代 …が受け取った」 once a trace is
        /// collected, but visiting collects a trace even with 「何も受け取らない」 (INHERITANCE.md R-10, 試作で決めたこと
        /// 5) and the lineage keeps who collected it, not what was picked: the hall says 「…が回収した」. Any other
        /// line (untaken, none, or one the inheritance module words differently) stands as HallView gives it.
        /// </summary>
        // js: scenes/meta/hall.js:62
        public static string PastTraceLine(Lineage lineage, PastLifeView life)
        {
            LifeRecord rec = lineage.Lives.FirstOrDefault(l => l.Generation == life.Generation);
            int? gen = rec != null ? rec.CollectedBy : null;
            if (gen == null) return life.TraceLine;
            LifeRecord by = lineage.Lives.FirstOrDefault(l => l.Generation == gen.Value) ?? lineage.Current;
            string name = by != null ? by.Name : "";
            return life.TraceLine == InheritanceText.TraceStatus.Taken(gen.Value, name)
                ? MetaText.TraceCollected(gen.Value, name)
                : life.TraceLine;
        }

        /// <summary>
        /// A layer shows one trace, the oldest untaken one (INHERITANCE.md 試作で決めたこと 4, NodeKind.Trace),
        /// so a life that ends on a layer where an older trace still lies leaves a trace that waits: the next
        /// heir to visit gets the older one. EndingView says 「次の踏破者が第 n 層を訪れると…受け取れる」 for every
        /// death; here that line becomes the wait, as the hall says it (TraceView: Visible / Waits), naming the
        /// trace that stands first (Inheritance.TraceForLayer). A cleared life's card trace gets the same line.
        /// Nothing changes when the trace is the one that shows, or once EndingView prints the wait itself:
        /// then the very view handed in comes back (the JS returns the same object).
        /// </summary>
        // js: scenes/meta/ending.js:23
        public static EndingView WithWaitingTrace(EndingView view, Lineage lineage)
        {
            LifeRecord life = lineage != null && lineage.Lives.Count > 0 ? lineage.Lives[lineage.Lives.Count - 1] : null;
            if (life == null || string.IsNullOrEmpty(life.TraceId) || lineage.Current != null
                || !lineage.Traces.Any(t => t.Id == life.TraceId)) return view;
            int layer = life.Layer.GetValueOrDefault();
            Trace first = Inheritance.TraceForLayer(lineage, layer);
            if (first == null || first.Id == life.TraceId) return view;
            List<Trace> ahead = lineage.Traces.Where(t => t.Layer == layer && t.Id != life.TraceId).ToList();
            string wait = ahead.Count > 1
                ? MetaText.TraceWaitsMany(layer, ahead.Count)
                : MetaText.TraceWaits(layer, first.Generation, first.Name);
            string visit = InheritanceText.TraceVisit(layer, InheritanceRules.LegacyPicks);
            List<string> lines;
            if (view.Lines.Contains(visit))
            {
                lines = view.Lines.Select(l => l == visit ? wait : l).ToList();
            }
            else if (view.Ending == LifeEnding.Completed)
            {
                string left = InheritanceText.ClearedTraceLeft(
                    InheritanceText.LayerTitle(layer, SevenLayers.Of(layer).DisplayName(lineage.Player.CarvingTaken)));
                int at = IndexOf(view.Lines, left);
                if (at < 0) return view;
                lines = view.Lines.Take(at + 1).Concat(new[] { wait }).Concat(view.Lines.Skip(at + 1)).ToList();
            }
            else
            {
                return view;
            }
            return new EndingView(view.Ending, view.Title, view.Sentence, view.HeirLine, lines, view.NextLine, view.Button);
        }

        private static int IndexOf(IReadOnlyList<string> lines, string line)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i] == line) return i;
            }
            return -1;
        }
    }
}
