#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using DungeonCore;

// The view-models of core/inheritance/Inheritance.js (:900-1111): what the hall (継承の間), the 痕跡 card and the
// ending card print. Data only; each has ToJson() in the JS's key order, so a screen's input can be compared with
// the JS's byte for byte.
namespace InheritanceCore
{
    /// <summary>One part of the 装束 (面の縁飾り / マントの裏地 / 留め具), its words and colour (null: none).</summary>
    public sealed class AttirePart
    {
        public AttirePart(string part, string text, string? color)
        {
            Part = part;
            Text = text;
            Color = color;
        }

        public string Part { get; }
        public string Text { get; }
        public string? Color { get; }

        public JsonObject ToJson() => new JsonObject { ["Part"] = Part, ["Text"] = Text, ["Color"] = Color };
    }

    /// <summary>The heir block: generation, name, title and line, and the 装束 tier with why it is that tier.</summary>
    public sealed class HeirView
    {
        public HeirView(int generation, string generationLabel, string name, string title, string line, int tier,
            string tierName, string tierReason, IEnumerable<AttirePart> attire)
        {
            Generation = generation;
            GenerationLabel = generationLabel;
            Name = name;
            Title = title;
            Line = line;
            Tier = tier;
            TierName = tierName;
            TierReason = tierReason;
            Attire = Frozen.List(attire);
        }

        public int Generation { get; }
        public string GenerationLabel { get; }
        public string Name { get; }
        public string Title { get; }
        public string Line { get; }
        public int Tier { get; }
        public string TierName { get; }
        public string TierReason { get; }
        public IReadOnlyList<AttirePart> Attire { get; }

        public JsonObject ToJson() => new JsonObject
        {
            ["Generation"] = Generation,
            ["GenerationLabel"] = GenerationLabel,
            ["Name"] = Name,
            ["Title"] = Title,
            ["Line"] = Line,
            ["Tier"] = Tier,
            ["TierName"] = TierName,
            ["TierReason"] = TierReason,
            ["Attire"] = Attire.Select(a => (object?)a.ToJson()).ToList(),
        };
    }

    /// <summary>
    /// A candidate row as the hall and the 痕跡 card print it, plus what it holds as data (the card and its 段, the enemy
    /// and its 開示度, the page, the 走り書き), so a screen can say what the row gives a given heir (the game's TraceGain)
    /// without reading the printed words back. The kind's fields are set, the others null.
    /// </summary>
    public sealed class CandidateView
    {
        public CandidateView(string id, CandidateKind kind, string kindLabel, string name, string detail,
            string? cardId = null, int? rank = null, string? enemyId = null, int? disclosure = null, string? pageId = null,
            int? generation = null, string? from = null, string? text = null)
        {
            Id = id;
            Kind = kind;
            KindLabel = kindLabel;
            Name = name;
            Detail = detail;
            CardId = cardId;
            Rank = rank;
            EnemyId = enemyId;
            Disclosure = disclosure;
            PageId = pageId;
            Generation = generation;
            From = from;
            Text = text;
        }

        public string Id { get; }
        public CandidateKind Kind { get; }
        public string KindLabel { get; }
        public string Name { get; }
        public string Detail { get; }
        public string? CardId { get; }
        public int? Rank { get; }
        public string? EnemyId { get; }
        public int? Disclosure { get; }
        public string? PageId { get; }
        public int? Generation { get; }
        public string? From { get; }
        public string? Text { get; }

        public JsonObject ToJson()
        {
            var o = new JsonObject { ["Id"] = Id, ["Kind"] = JsonName.Of(Kind), ["KindLabel"] = KindLabel, ["Name"] = Name, ["Detail"] = Detail };
            switch (Kind)
            {
                case CandidateKind.Card:
                    Put(o, "CardId", CardId);
                    Put(o, "Rank", Rank);
                    break;
                case CandidateKind.EnemyPage:
                    Put(o, "EnemyId", EnemyId);
                    Put(o, "Disclosure", Disclosure);
                    break;
                case CandidateKind.DungeonPage:
                    Put(o, "PageId", PageId);
                    break;
                case CandidateKind.Scribble:
                    Put(o, "Generation", Generation);
                    Put(o, "From", From);
                    Put(o, "Text", Text);
                    break;
            }
            return o;
        }

        private static void Put(JsonObject o, string key, object? value)
        {
            if (value != null) o[key] = value;
        }
    }

    /// <summary>A card the shallow rule kept out, with the reason.</summary>
    public sealed class WithheldView
    {
        public WithheldView(string name, string detail)
        {
            Name = name;
            Detail = detail;
        }

        public string Name { get; }
        public string Detail { get; }

        public JsonObject ToJson() => new JsonObject { ["Name"] = Name, ["Detail"] = Detail };
    }

    /// <summary>An untaken trace in the hall: who, where, how, whether it shows this life, and its candidates.</summary>
    public sealed class TraceView
    {
        public TraceView(string id, int generation, string heirLine, int layer, int? nodeId, string placeLine, string endingLabel,
            bool cleared, string note, bool visible, string appearsLine, string countLine, IEnumerable<CandidateView> candidates,
            IEnumerable<WithheldView> withheld)
        {
            Id = id;
            Generation = generation;
            HeirLine = heirLine;
            Layer = layer;
            NodeId = nodeId;
            PlaceLine = placeLine;
            EndingLabel = endingLabel;
            Cleared = cleared;
            Note = note;
            Visible = visible;
            AppearsLine = appearsLine;
            CountLine = countLine;
            Candidates = Frozen.List(candidates);
            Withheld = Frozen.List(withheld);
        }

        public string Id { get; }
        public int Generation { get; }
        public string HeirLine { get; }
        public int Layer { get; }
        public int? NodeId { get; }
        public string PlaceLine { get; }
        public string EndingLabel { get; }
        public bool Cleared { get; }
        public string Note { get; }
        /// <summary>The trace shows on its layer (the oldest there); otherwise it waits (試作で決めたこと 4).</summary>
        public bool Visible { get; }
        public string AppearsLine { get; }
        public string CountLine { get; }
        public IReadOnlyList<CandidateView> Candidates { get; }
        public IReadOnlyList<WithheldView> Withheld { get; }

        public JsonObject ToJson() => new JsonObject
        {
            ["Id"] = Id,
            ["Generation"] = Generation,
            ["HeirLine"] = HeirLine,
            ["Layer"] = Layer,
            ["NodeId"] = NodeId,
            ["PlaceLine"] = PlaceLine,
            ["EndingLabel"] = EndingLabel,
            ["Cleared"] = Cleared,
            ["Note"] = Note,
            ["Visible"] = Visible,
            ["AppearsLine"] = AppearsLine,
            ["CountLine"] = CountLine,
            ["Candidates"] = Candidates.Select(c => (object?)c.ToJson()).ToList(),
            ["Withheld"] = Withheld.Select(w => (object?)w.ToJson()).ToList(),
        };
    }

    /// <summary>The survivor block: who, what passes (journal, slots, deck), and for how long.</summary>
    public sealed class SurvivorView
    {
        public SurvivorView(int generation, string line, IEnumerable<string> lines, string durationLine, int bonusLivesLeft)
        {
            Generation = generation;
            Line = line;
            Lines = Frozen.List(lines);
            DurationLine = durationLine;
            BonusLivesLeft = bonusLivesLeft;
        }

        public int Generation { get; }
        public string Line { get; }
        public IReadOnlyList<string> Lines { get; }
        public string DurationLine { get; }
        public int BonusLivesLeft { get; }

        public JsonObject ToJson() => new JsonObject
        {
            ["Generation"] = Generation,
            ["Line"] = Line,
            ["Lines"] = Lines.Cast<object?>().ToList(),
            ["DurationLine"] = DurationLine,
            ["BonusLivesLeft"] = BonusLivesLeft,
        };
    }

    /// <summary>Where the survivor stood, once the life that used the bonus has closed with no new survivor (R-14).</summary>
    public sealed class SurvivorSpentView
    {
        public SurvivorSpentView(int generation, string line, IEnumerable<string> lines)
        {
            Generation = generation;
            Line = line;
            Lines = Frozen.List(lines);
        }

        public int Generation { get; }
        public string Line { get; }
        public IReadOnlyList<string> Lines { get; }

        public JsonObject ToJson() => new JsonObject
        {
            ["Generation"] = Generation,
            ["Line"] = Line,
            ["Lines"] = Lines.Cast<object?>().ToList(),
        };
    }

    /// <summary>A closed life in the hall's list.</summary>
    public sealed class PastLifeView
    {
        public PastLifeView(int generation, string generationLabel, string name, string line, string tierName, LifeEnding ending,
            string endingLabel, string endingLine, string placeLine, string traceLine)
        {
            Generation = generation;
            GenerationLabel = generationLabel;
            Name = name;
            Line = line;
            TierName = tierName;
            Ending = ending;
            EndingLabel = endingLabel;
            EndingLine = endingLine;
            PlaceLine = placeLine;
            TraceLine = traceLine;
        }

        public int Generation { get; }
        public string GenerationLabel { get; }
        public string Name { get; }
        public string Line { get; }
        public string TierName { get; }
        public LifeEnding Ending { get; }
        public string EndingLabel { get; }
        public string EndingLine { get; }
        public string PlaceLine { get; }
        public string TraceLine { get; }

        public JsonObject ToJson() => new JsonObject
        {
            ["Generation"] = Generation,
            ["GenerationLabel"] = GenerationLabel,
            ["Name"] = Name,
            ["Line"] = Line,
            ["TierName"] = TierName,
            ["Ending"] = JsonName.Of(Ending),
            ["EndingLabel"] = EndingLabel,
            ["EndingLine"] = EndingLine,
            ["PlaceLine"] = PlaceLine,
            ["TraceLine"] = TraceLine,
        };
    }

    /// <summary>継承の間 (look only, R-12).</summary>
    public sealed class HallView
    {
        public HallView(string title, string lead, string empty, string nextHeirHeading, HeirView nextHeir, string pastLivesHeading,
            IEnumerable<PastLifeView> pastLives, string traceHeading, string noTraces, IEnumerable<TraceView> traces,
            IEnumerable<string> traceRules, string survivorHeading, SurvivorView? survivor, SurvivorSpentView? survivorSpent,
            string startButton)
        {
            Title = title;
            Lead = lead;
            Empty = empty;
            NextHeirHeading = nextHeirHeading;
            NextHeir = nextHeir;
            PastLivesHeading = pastLivesHeading;
            PastLives = Frozen.List(pastLives);
            TraceHeading = traceHeading;
            NoTraces = noTraces;
            Traces = Frozen.List(traces);
            TraceRules = Frozen.List(traceRules);
            SurvivorHeading = survivorHeading;
            Survivor = survivor;
            SurvivorSpent = survivorSpent;
            StartButton = startButton;
        }

        public string Title { get; }
        public string Lead { get; }
        /// <summary>The first visit's line, or "".</summary>
        public string Empty { get; }
        public string NextHeirHeading { get; }
        public HeirView NextHeir { get; }
        public string PastLivesHeading { get; }
        /// <summary>Newest first.</summary>
        public IReadOnlyList<PastLifeView> PastLives { get; }
        public string TraceHeading { get; }
        /// <summary>「未回収の痕跡は無い。」 or "".</summary>
        public string NoTraces { get; }
        public IReadOnlyList<TraceView> Traces { get; }
        public IReadOnlyList<string> TraceRules { get; }
        public string SurvivorHeading { get; }
        public SurvivorView? Survivor { get; }
        public SurvivorSpentView? SurvivorSpent { get; }
        public string StartButton { get; }

        public JsonObject ToJson() => new JsonObject
        {
            ["Title"] = Title,
            ["Lead"] = Lead,
            ["Empty"] = Empty,
            ["NextHeirHeading"] = NextHeirHeading,
            ["NextHeir"] = NextHeir.ToJson(),
            ["PastLivesHeading"] = PastLivesHeading,
            ["PastLives"] = PastLives.Select(p => (object?)p.ToJson()).ToList(),
            ["TraceHeading"] = TraceHeading,
            ["NoTraces"] = NoTraces,
            ["Traces"] = Traces.Select(t => (object?)t.ToJson()).ToList(),
            ["TraceRules"] = TraceRules.Cast<object?>().ToList(),
            ["SurvivorHeading"] = SurvivorHeading,
            ["Survivor"] = Survivor?.ToJson(),
            ["SurvivorSpent"] = SurvivorSpent?.ToJson(),
            ["StartButton"] = StartButton,
        };
    }

    /// <summary>The card the 痕跡 node opens: the trace on the layer, its candidates, and whether this life may take it.</summary>
    public sealed class TraceNodeView
    {
        public TraceNodeView(string title, string traceId, string heirLine, string placeLine, string lead, string note,
            IEnumerable<CandidateView> candidates, string withheldHeading, IEnumerable<WithheldView> withheld, int maxPicks,
            bool canTake, string refusal, string takeButton, string leaveButton)
        {
            Title = title;
            TraceId = traceId;
            HeirLine = heirLine;
            PlaceLine = placeLine;
            Lead = lead;
            Note = note;
            Candidates = Frozen.List(candidates);
            WithheldHeading = withheldHeading;
            Withheld = Frozen.List(withheld);
            MaxPicks = maxPicks;
            CanTake = canTake;
            Refusal = refusal;
            TakeButton = takeButton;
            LeaveButton = leaveButton;
        }

        public string Title { get; }
        public string TraceId { get; }
        public string HeirLine { get; }
        public string PlaceLine { get; }
        public string Lead { get; }
        public string Note { get; }
        public IReadOnlyList<CandidateView> Candidates { get; }
        public string WithheldHeading { get; }
        public IReadOnlyList<WithheldView> Withheld { get; }
        public int MaxPicks { get; }
        public bool CanTake { get; }
        public string Refusal { get; }
        public string TakeButton { get; }
        public string LeaveButton { get; }

        public JsonObject ToJson() => new JsonObject
        {
            ["Title"] = Title,
            ["TraceId"] = TraceId,
            ["HeirLine"] = HeirLine,
            ["PlaceLine"] = PlaceLine,
            ["Lead"] = Lead,
            ["Note"] = Note,
            ["Candidates"] = Candidates.Select(c => (object?)c.ToJson()).ToList(),
            ["WithheldHeading"] = WithheldHeading,
            ["Withheld"] = Withheld.Select(w => (object?)w.ToJson()).ToList(),
            ["MaxPicks"] = MaxPicks,
            ["CanTake"] = CanTake,
            ["Refusal"] = Refusal,
            ["TakeButton"] = TakeButton,
            ["LeaveButton"] = LeaveButton,
        };
    }

    /// <summary>The 「n / 3」 under the 痕跡 card's list, and whether one more may be picked.</summary>
    public sealed class PickState
    {
        public PickState(string line, bool canPickMore)
        {
            Line = line;
            CanPickMore = canPickMore;
        }

        public string Line { get; }
        public bool CanPickMore { get; }

        public JsonObject ToJson() => new JsonObject { ["Line"] = Line, ["CanPickMore"] = CanPickMore };
    }

    /// <summary>The ending card of the life that closed last.</summary>
    public sealed class EndingView
    {
        public EndingView(LifeEnding ending, string title, string sentence, string heirLine, IEnumerable<string> lines, string nextLine, string button)
        {
            Ending = ending;
            Title = title;
            Sentence = sentence;
            HeirLine = heirLine;
            Lines = Frozen.List(lines);
            NextLine = nextLine;
            Button = button;
        }

        public LifeEnding Ending { get; }
        public string Title { get; }
        public string Sentence { get; }
        public string HeirLine { get; }
        public IReadOnlyList<string> Lines { get; }
        public string NextLine { get; }
        public string Button { get; }

        public JsonObject ToJson() => new JsonObject
        {
            ["Ending"] = JsonName.Of(Ending),
            ["Title"] = Title,
            ["Sentence"] = Sentence,
            ["HeirLine"] = HeirLine,
            ["Lines"] = Lines.Cast<object?>().ToList(),
            ["NextLine"] = NextLine,
            ["Button"] = Button,
        };
    }

    public static partial class Inheritance
    {
        // -----------------------------------------------------------------------------------------
        // View-models
        // -----------------------------------------------------------------------------------------

        // js: core/inheritance/Inheritance.js:522
        private static CandidateView CandidateViewOf(Candidate c)
        {
            string kindLabel = InheritanceText.KindLabel(c.Kind);
            string detail = CandidateDetail(c);
            switch (c.Kind)
            {
                case CandidateKind.Card:
                    return new CandidateView(c.Id, c.Kind, kindLabel, c.Name, detail, cardId: c.CardId, rank: c.Rank);
                case CandidateKind.EnemyPage:
                    return new CandidateView(c.Id, c.Kind, kindLabel, c.Name, detail, enemyId: c.EnemyId, disclosure: c.Disclosure);
                case CandidateKind.DungeonPage:
                    return new CandidateView(c.Id, c.Kind, kindLabel, c.Name, detail, pageId: c.PageId);
                default:
                    return new CandidateView(c.Id, c.Kind, kindLabel, c.Name, detail, generation: c.Generation, from: c.From, text: c.Text);
            }
        }

        // js: core/inheritance/Inheritance.js:904
        private static HeirView HeirViewOf(Heir heir, string reason)
        {
            var attire = AttireOf(heir.Tier);
            return new HeirView(
                heir.Generation,
                InheritanceText.GenerationLabel(heir.Generation),
                heir.Name,
                InheritanceText.HeirTitle(heir.Name),
                InheritanceText.HeirLine(heir.Generation, heir.Name),
                heir.Tier,
                attire.Name,
                reason,
                new[]
                {
                    new AttirePart("面の縁飾り", attire.Mask, attire.MaskColor),
                    new AttirePart("マントの裏地", attire.Lining, attire.LiningColor),
                    new AttirePart("留め具", attire.Clasp, attire.ClaspColor),
                });
        }

        // js: core/inheritance/Inheritance.js:923
        private static TraceView TraceViewOf(Lineage lineage, Trace trace)
        {
            bool visible = TraceForLayer(lineage.WithCurrent(null), trace.Layer)?.Id == trace.Id;
            var counts = CountCandidates(trace);
            return new TraceView(
                trace.Id,
                trace.Generation,
                InheritanceText.HeirLine(trace.Generation, trace.Name),
                trace.Layer,
                trace.NodeId,
                PlaceTitle(lineage, trace.Layer, trace.NodeKind),
                InheritanceText.EndingTitle(trace.Ending),
                trace.Cleared,
                trace.Cleared ? InheritanceText.ClearedTraceNote : "",
                visible,
                visible ? InheritanceText.Appears : InheritanceText.Waits,
                InheritanceText.CandidateCount(counts.Cards, counts.Pages, counts.Scribbles),
                OfferedCandidates(trace).Select(CandidateViewOf),
                trace.Withheld.Select(w => new WithheldView(w.Name, InheritanceText.WithheldReason(w.Rank))));
        }

        // js: core/inheritance/Inheritance.js:944
        private static SurvivorView? SurvivorViewOf(Lineage lineage)
        {
            var s = lineage.Survivor;
            if (s == null) return null;
            int nextGen = NextGeneration(lineage);
            bool journalPending = s.Journal != null && s.Generation == nextGen - 1;
            var lines = new List<string>();
            if (journalPending) lines.Add(InheritanceText.SurvivorJournal);
            if (s.BonusLivesLeft > 0)
            {
                lines.Add(InheritanceText.SurvivorTools(RunLoadout.ToolSlots, InheritanceRules.SurvivorToolSlots));
                lines.Add(InheritanceText.SurvivorDeck(Constants.DeckMax, Constants.DeckMax + InheritanceRules.SurvivorDeckBonus));
            }
            return new SurvivorView(
                s.Generation,
                InheritanceText.HeirLine(s.Generation, s.Name),
                lines,
                s.BonusLivesLeft > 0 ? InheritanceText.SurvivorDuration(s.BonusLivesLeft) : "",
                s.BonusLivesLeft);
        }

        /// <summary>
        /// The survivor whose bonus the last closed life used up, when no survivor stands now (R-14: the bonus is for
        /// the next life only; R-15): the hall says so where the survivor stood, so the block does not vanish without a
        /// word and the next heir's 3 tool slots and 40 cards read as the bonus having ended. Null otherwise.
        /// </summary>
        // js: core/inheritance/Inheritance.js:969
        private static SurvivorSpentView? SurvivorSpentViewOf(Lineage lineage)
        {
            if (lineage.Survivor != null && lineage.Survivor.BonusLivesLeft > 0) return null;
            var last = LastLife(lineage);
            if (last == null || !last.SurvivorBonus || last.SurvivorFrom == null) return null;
            if (lineage.Survivor != null && lineage.Survivor.Generation == last.Generation) return null;
            var from = lineage.Lives.FirstOrDefault(l => l.Generation == last.SurvivorFrom);
            return new SurvivorSpentView(
                last.SurvivorFrom.Value,
                InheritanceText.HeirLine(last.SurvivorFrom.Value, from != null ? from.Name : ""),
                new[]
                {
                    InheritanceText.SurvivorSpent(last.Generation, last.Name),
                    InheritanceText.SurvivorSpentLimits(RunLoadout.ToolSlots, Constants.DeckMax),
                });
        }

        // js: core/inheritance/Inheritance.js:985
        private static PastLifeView PastLifeViewOf(Lineage lineage, LifeRecord life)
        {
            string traceLine;
            if (life.TraceId == null) traceLine = InheritanceText.TraceStatus.None;
            else if (life.CollectedBy == null) traceLine = InheritanceText.TraceStatus.Untaken;
            else
            {
                var by = lineage.Lives.FirstOrDefault(l => l.Generation == life.CollectedBy) ?? lineage.Current;
                traceLine = InheritanceText.TraceStatus.Taken(life.CollectedBy.Value, by != null ? by.Name : "");
            }
            var ending = life.Ending ?? throw new InheritanceRuleException(
                System.FormattableString.Invariant($"generation {life.Generation} has no ending"));
            int layer = life.Layer.GetValueOrDefault();
            return new PastLifeView(
                life.Generation,
                InheritanceText.GenerationLabel(life.Generation),
                life.Name,
                InheritanceText.HeirLine(life.Generation, life.Name),
                AttireOf(life.Tier).Name,
                ending,
                InheritanceText.EndingTitle(ending),
                InheritanceText.EndingSentence(ending, life.Layer),
                PlaceTitle(lineage, layer, life.NodeKind),
                traceLine);
        }

        /// <summary>
        /// 継承の間 (CAMP_FACILITIES_DESIGN §3.1): the past lives (newest first), every untaken trace with its
        /// candidates (look only: the choice is made at the place of death, concept-v3 §8.3), the survivor's bonus,
        /// and the next heir with name and 装束. Nothing in it takes a trace.
        /// </summary>
        // js: core/inheritance/Inheritance.js:1012
        public static InheritanceCore.HallView HallView(Lineage lineage)
        {
            var heir = NextHeir(lineage);
            var survivor = SurvivorViewOf(lineage);
            return new InheritanceCore.HallView(
                InheritanceText.HallTitle,
                InheritanceText.HallLead,
                lineage.Lives.Count == 0 ? InheritanceText.HallEmpty : "",
                InheritanceText.NextHeirHeading,
                HeirViewOf(heir, TierReason(LastLife(lineage))),
                InheritanceText.PastLivesHeading,
                lineage.Lives.Reverse().Select(l => PastLifeViewOf(lineage, l)),
                InheritanceText.TraceHeading,
                lineage.Traces.Count == 0 ? InheritanceText.NoTraces : "",
                lineage.Traces.Select(t => TraceViewOf(lineage, t)),
                InheritanceText.TraceRules,
                InheritanceText.SurvivorHeading,
                survivor,
                survivor != null ? null : SurvivorSpentViewOf(lineage),
                InheritanceText.StartButton);
        }

        /// <summary>
        /// The card the 痕跡 node opens (dungeon_exploration_v4 §6.3: 「前の生の遺産を受け取れること（1 つの生で 1 件）」):
        /// the trace on <paramref name="layer"/>, its candidates to pick from and how many may be picked. Null when no
        /// trace stands there. CanTake is false (with the refusal line) once this life took its one trace.
        /// </summary>
        // js: core/inheritance/Inheritance.js:1038
        public static InheritanceCore.TraceNodeView? TraceNodeView(Lineage lineage, int layer)
        {
            var trace = lineage.Traces.FirstOrDefault(t => t.Layer == layer);
            if (trace == null) return null;
            var current = lineage.Current;
            bool canTake = current != null && current.TakenTraceId == null;
            var view = TraceViewOf(lineage, trace);
            return new InheritanceCore.TraceNodeView(
                InheritanceText.TraceNodeTitle,
                trace.Id,
                InheritanceText.TraceOf(trace.Generation, trace.Name),
                view.PlaceLine,
                InheritanceText.PickLead(InheritanceRules.LegacyPicks),
                view.Note,
                view.Candidates,
                trace.Withheld.Count > 0 ? InheritanceText.WithheldHeading : "",
                view.Withheld,
                InheritanceRules.LegacyPicks,
                canTake,
                canTake ? "" : InheritanceText.AlreadyTaken,
                InheritanceText.TakeButton,
                InheritanceText.LeaveButton);
        }

        /// <summary>The 「n / 3」 the 痕跡 card prints under its list, and whether one more may be picked.</summary>
        // js: core/inheritance/Inheritance.js:1063
        public static InheritanceCore.PickState PickState(IEnumerable<string>? selected)
        {
            int n = selected?.Count() ?? 0;
            return new InheritanceCore.PickState(InheritanceText.PickCount(n, InheritanceRules.LegacyPicks), n < InheritanceRules.LegacyPicks);
        }

        /// <summary>
        /// The ending card of the life that closed last (dungeon_exploration_v4 §6.9): the end sentence, then what stays
        /// and what passes on — the trace and its candidates, the cards the shallow rule kept out, or the whole journal
        /// and the survivor's bonus — and the next heir.
        /// </summary>
        // js: core/inheritance/Inheritance.js:1076
        public static InheritanceCore.EndingView EndingView(Lineage lineage)
        {
            var life = LastLife(lineage);
            if (life == null || lineage.Current != null) throw new InheritanceRuleException("no closed life to show");
            var ending = life.Ending ?? throw new InheritanceRuleException("no closed life to show");
            int layer = life.Layer.GetValueOrDefault();
            var lines = new List<string>();
            string layerTitle = InheritanceText.LayerTitle(layer, LayerName(lineage, layer));
            var trace = life.TraceId != null ? lineage.Traces.FirstOrDefault(t => t.Id == life.TraceId) : null;
            if (Array.IndexOf(Deaths, ending) >= 0)
            {
                lines.Add(InheritanceText.TraceLeft(layerTitle));
                if (trace != null && CountCandidates(trace).Total > 0)
                {
                    var c = CountCandidates(trace);
                    lines.Add(InheritanceText.CandidateCount(c.Cards, c.Pages, c.Scribbles));
                    lines.Add(InheritanceText.TraceVisit(layer, InheritanceRules.LegacyPicks));
                }
                else
                {
                    lines.Add(InheritanceText.NothingLeft);
                }
                if (trace != null && trace.Withheld.Count > 0) lines.Add(InheritanceText.WithheldLine(trace.Withheld.Select(w => w.Name)));
            }
            else
            {
                lines.Add(InheritanceText.JournalPasses);
                lines.Add(InheritanceText.SurvivorBonusLine(lineage.Survivor != null ? lineage.Survivor.BonusLives : InheritanceRules.SurvivorBonusLives));
                if (ending == LifeEnding.Completed && trace != null) lines.Add(InheritanceText.ClearedTraceLeft(layerTitle));
                if (life.ReplacedSurvivor != null)
                {
                    var old = lineage.Lives.FirstOrDefault(l => l.Generation == life.ReplacedSurvivor);
                    lines.Add(InheritanceText.SurvivorReplaced(life.ReplacedSurvivor.Value, old != null ? old.Name : ""));
                }
            }
            var heir = NextHeir(lineage);
            return new InheritanceCore.EndingView(
                ending,
                InheritanceText.EndingTitle(ending),
                InheritanceText.EndingSentence(ending, life.Layer),
                InheritanceText.HeirLine(life.Generation, life.Name),
                lines,
                InheritanceText.NextHeirLine(heir.Generation, heir.Name),
                InheritanceText.ToHallButton);
        }
    }
}
