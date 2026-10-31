// Helpers of the life, intel and recorder tests (dotnet only; not copied into Unity): the browser trial's test helpers
// (test/app/life.test.mjs walk / fight / departure, test/exploration/_explore.mjs Explorable / RouteTo, test/ext/
// journal.test.mjs stepTo / downTo) and a state dump that stands in for the JS's toParityJson.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using BattleCore;
using Depiction;
using Depiction.Bridge;
using DungeonContent;
using DungeonCore;
using Exploration;
using Exploration.Bridge;
using InheritanceCore;
using NUnit.Framework;

namespace Trial.Bridge.Tests
{
    internal sealed class WalkOptions
    {
        /// <summary>From this layer on, the life ends alive at the interlude (life.test.mjs surviveAt).</summary>
        public int? SurviveAt { get; set; }

        /// <summary>Called when the walk steps onto a trace node (life.test.mjs onTrace).</summary>
        public Action<int> OnTrace { get; set; }

        /// <summary>Fights each fight through Recorder.SettleWhenDecided with this onSettled (settle.test.mjs walkSettled).</summary>
        public Action OnSettled { get; set; }

        /// <summary>Settle each fight at its decision (walkSettled) instead of finishing it after the source has played out.</summary>
        public bool Settle { get; set; }

        /// <summary>Answer the explore scene's own cards too (result cards closed, a chest's pick learned, training put off, the receipt closed).</summary>
        public bool Screens { get; set; }
    }

    internal static class LifeTestKit
    {
        // ------------------------------------------------------------------ fights

        /// <summary>life.test.mjs fight: the suggested card, else the turn's end; the next event while not waiting.</summary>
        public static void Fight(ITrialSource source)
        {
            int guard = 0;
            while (!source.Finished)
            {
                if (++guard > 20000) throw new AssertionException("fight did not end");
                SuggestedStep(source, assertAccepted: true);
            }
        }

        /// <summary>One screen step of an unattended fight (settle.test.mjs step(src, true)).</summary>
        public static void SuggestedStep(ITrialSource source, bool assertAccepted = false)
        {
            if (source.WaitingForPlayer)
            {
                string id = source.SuggestedCardId;
                CardFace face = string.IsNullOrEmpty(id) ? null : DepictionText.Find(source.Frame.Hand, id);
                if (face != null)
                {
                    PlayVerdict r = source.TryPlay(id, DepictionText.RequiredZone(face.Aim), out DepictionEvent _);
                    if (assertAccepted) Assert.That(r, Is.EqualTo(PlayVerdict.Accepted));
                }
                else source.EndTurn();
            }
            else source.AdvanceAuto();
        }

        /// <summary>settle.test.mjs step(src, play): the suggested card when play, else the turn's end; the next event while not waiting.</summary>
        public static void Step(ITrialSource src, bool play)
        {
            if (src.WaitingForPlayer)
            {
                string id = play ? src.SuggestedCardId : null;
                CardFace face = string.IsNullOrEmpty(id) ? null : DepictionText.Find(src.Frame.Hand, id);
                if (face != null) src.TryPlay(id, DepictionText.RequiredZone(face.Aim), out DepictionEvent _);
                else src.EndTurn();
            }
            else src.AdvanceAuto();
        }

        /// <summary>settle.test.mjs playerStep: the leftmost card the source would take, else the turn's end.</summary>
        public static void PlayerStep(ITrialSource src)
        {
            if (src.WaitingForPlayer)
            {
                CardFace card = src.Frame.Hand.FirstOrDefault(f => src.Inspect(f.Id) == PlayVerdict.Accepted);
                if (card != null) src.TryPlay(card.Id, DepictionText.RequiredZone(card.Aim), out DepictionEvent _);
                else src.EndTurn();
            }
            else src.AdvanceAuto();
        }

        public static bool Decided(ITrialSource src) => src.State.Result != GameResult.Ongoing;

        // ------------------------------------------------------------------ a life

        /// <summary>
        /// Walks one life through the recorded facades (life.test.mjs walk): pick(open, state) picks the next node among
        /// the open ones. Fights are fought unattended; trial choices are answered (events taken, found consumables swapped
        /// into slot 0 or left, the survey's first choice). Returns the end state.
        /// </summary>
        public static ExplorationState Walk(RecordedExpedition ex, Func<IReadOnlyList<MapNode>, ExplorationState, MapNode> pick, WalkOptions opts = null)
        {
            opts = opts ?? new WalkOptions();
            for (int guard = 0; ; guard++)
            {
                RecordedSession s = ex.Session;
                ExplorationState st = s.State;
                if (guard > 600)
                    throw new AssertionException($"walk did not end: phase {st.Phase} layer {st.Profile.Layer} node {st.CurrentNodeId}");
                if (s.HasPendingBattle)
                {
                    RecordedBattle battle = ex.Battle ?? ex.StartEncounter();
                    while (!battle.Over)
                    {
                        if (opts.Settle)
                        {
                            SettlingFight fight = Recorder.SettleWhenDecided(battle, battle.StartNext(true), opts.OnSettled);
                            for (int g = 0; g < 20000 && !fight.Source.Finished; g++) Step(fight.Source, true);
                            Assert.That(fight.Settled, Is.True, "every fight is finished at its decision");
                            fight.Finish();
                        }
                        else
                        {
                            RecordedSource src = battle.StartNext(true);
                            Fight(src);
                            battle.Finish(src);
                        }
                    }
                    ex.Return();
                    if (ex.Offer.Count > 0) ex.Learn(ex.Offer[0]);
                    else if (opts.Screens && ex.PendingResult != null) ex.CloseResult();
                    continue;
                }
                if (s.HasPendingStep) { s.Act(ScreenAction.ConfirmStep); continue; }
                if (s.HasPendingEvent) { Assert.That(s.Act(ScreenAction.AcceptEvent), Is.True); continue; }
                if (s.HasPendingFound) { Assert.That(s.Act(ScreenAction.TakeFound, "0") || s.Act(ScreenAction.LeaveFound), Is.True); continue; }
                if (s.HasPendingSurvey) { Assert.That(s.Act(ScreenAction.ChooseSurvey, s.SurveyChoices[0]), Is.True); continue; }
                if (opts.Screens)
                {
                    // The explore scene's own cards: a chest's pick, a result card, the training card.
                    if (ex.Offer.Count > 0) { ex.Learn(ex.Offer[0]); continue; }
                    if (ex.PendingResult != null) { ex.CloseResult(); continue; }
                    if (ex.PendingTaken != null) { ex.CloseTaken(); continue; }
                    if (s.TrainingWaiting) { s.PutOffTraining(); continue; }
                }
                if (s.ConfirmingDescend) { s.Act(ScreenAction.ConfirmDescend); continue; }
                if (st.IsOver) return st;
                if (st.Phase == RunPhase.Interlude)
                {
                    if (opts.SurviveAt.HasValue && st.Profile.Layer >= opts.SurviveAt.Value)
                    {
                        s.Act(ScreenAction.AskEndLife);
                        s.Act(ScreenAction.ConfirmEndLife);
                        continue;
                    }
                    Assert.That(s.Act(ScreenAction.Descend), Is.True);
                    continue;
                }
                if (ExplorationReducer.CanInterlude(st)) { Assert.That(s.Act(ScreenAction.GoToInterlude), Is.True); continue; }
                List<MapNode> open = st.Map.Nodes.Where(n => ExplorationReducer.CanStep(st, n.Id)).ToList();
                Assert.That(open.Count, Is.GreaterThan(0), "no open node");
                List<MapNode> fresh = open.Where(n => !st.HasResolved(n.Id)).ToList();
                MapNode node = pick(fresh.Count > 0 ? fresh : open, st);
                if (node.Kind == NodeKind.Trace) opts.OnTrace?.Invoke(st.Profile.Layer);
                Assert.That(s.Step(node.Id), Is.True, "step refused");
            }
        }

        /// <summary>life.test.mjs preferTrace: a trace node when one is open, else the first.</summary>
        public static MapNode PreferTrace(IReadOnlyList<MapNode> open, ExplorationState st) =>
            open.FirstOrDefault(n => n.Kind == NodeKind.Trace) ?? open[0];

        // ------------------------------------------------------------------ the departure

        /// <summary>
        /// The departure's payload as the meta scene hands it (life.test.mjs departure): the prototype deck, the tools
        /// that act in battle, and a DungeonContent Loadout with the life's slots.
        /// </summary>
        public static DepartureChoice Departure(LifeStart lifeStart, int g = 1)
        {
            string[] tools = g % 2 == 1 ? new[] { "bosho_no_men", "kokugen_no_suna", "ikitsugi_no_fue" } : new[] { "toshin_no_kakera", "utsushi_no_tobari" };
            string[] consumables = g % 2 == 1 ? new[] { "yakuso", "joka_no_ko" } : new[] { "koi_hoshiniku", "shirube_no_ishi", "wasuremizu" };
            return new DepartureChoice
            {
                Deck = PrototypeDeck.Build(),
                BattleToolIds = tools.Where(id => ItemCatalogue.Tool(id).Side == EffectSide.Battle).ToList(),
                Loadout = Loadout.Of(tools, consumables, lifeStart.ToolSlots),
                Talent = null,
            };
        }

        /// <summary>survivor.test.mjs departure: the prototype deck, nothing carried.</summary>
        public static DepartureChoice Plain(LifeStart lifeStart) => new DepartureChoice
        {
            Deck = PrototypeDeck.Build(),
            BattleToolIds = Array.Empty<string>(),
            Loadout = Loadout.Of(Array.Empty<string>(), Array.Empty<string>(), lifeStart.ToolSlots),
        };

        /// <summary>JSON.parse(JSON.stringify(start)): the start as a save holds it.</summary>
        public static LifeStartData Clone(LifeStartData start) =>
            LifeStartData.FromJson(Json.Parse(Json.Stringify(start.ToJson())));

        /// <summary>JSON.parse(JSON.stringify(log)): the log as a save holds it.</summary>
        public static LifeLog Saved(LifeLog log) => LifeLog.FromJson(Json.Parse(Json.Stringify(log.ToJson())));

        // ------------------------------------------------------------------ the exploration (test/exploration/_explore.mjs)

        /// <summary>Reports the entry battle won, so the map takes steps.</summary>
        public static ExplorationSession Explorable(ExplorationSession session)
        {
            if (session.HasPendingBattle) session.Act(ScreenAction.WinBattle);
            Assert.That(session.MapIsLive, Is.True);
            return session;
        }

        /// <summary>A breadth-first route over the map's links, excluding the start (ExplorationSessionTests.RouteTo).</summary>
        public static List<int> RouteTo(LayerMap map, int from, int to)
        {
            var previous = new Dictionary<int, int>();
            var queue = new Queue<int>();
            queue.Enqueue(from);
            var seen = new HashSet<int> { from };
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (current == to) break;
                foreach (int next in map.Neighbours(current))
                {
                    if (map.Node(next).Kind == NodeKind.Boss && next != to) continue;
                    if (!seen.Add(next)) continue;
                    previous[next] = current;
                    queue.Enqueue(next);
                }
            }
            var route = new List<int>();
            for (int at = to; at != from; at = previous[at]) route.Add(at);
            route.Reverse();
            return route;
        }

        /// <summary>
        /// journal.test.mjs stepTo: steps along a route, the stand-in winning each fight on the way but not the last
        /// node's; with the trial rules on, an event waiting on 受ける / 受けない is declined and a find left.
        /// </summary>
        public static void StepTo(ExplorationSession session, int nodeId)
        {
            foreach (int id in RouteTo(session.State.Map, session.State.CurrentNodeId, nodeId))
            {
                Assert.That(session.Step(id), Is.True, $"step to {id}");
                if (session.HasPendingEvent) session.Act(ScreenAction.DeclineEvent);
                if (session.HasPendingFound) session.Act(ScreenAction.LeaveFound);
                if (session.HasPendingBattle && id != nodeId) session.Act(ScreenAction.WinBattle);
            }
        }

        /// <summary>journal.test.mjs downTo: clears layers straight down (the stand-in wins every fight) to <paramref name="layer"/>'s entry.</summary>
        public static void DownTo(ExplorationSession session, int layer, Action<int> onInterlude = null)
        {
            while (session.State.Profile.Layer < layer)
            {
                Explorable(session);
                while (session.State.Phase == RunPhase.Exploring)
                {
                    LayerMap map = session.State.Map;
                    int row = map.Node(session.State.CurrentNodeId).Row;
                    int next = map.Successors(session.State.CurrentNodeId).First(id => map.Node(id).Row == row + 1);
                    Assert.That(session.Step(next), Is.True);
                    if (session.HasPendingEvent) session.Act(ScreenAction.DeclineEvent);
                    if (session.HasPendingFound) session.Act(ScreenAction.LeaveFound);
                    if (session.HasPendingBattle) session.Act(ScreenAction.WinBattle);
                }
                Assert.That(session.Act(ScreenAction.GoToInterlude), Is.True);
                onInterlude?.Invoke(session.State.Profile.Layer);
                Assert.That(session.Act(ScreenAction.Descend), Is.True);
                Assert.That(session.ConfirmingDescend, Is.False, "the walk would end the life by 瘴気");
            }
        }

        /// <summary>The Expedition's internal Mastery setter (the JS tests assign raw.Mastery; Exploration.Bridge shows its internals to its own tests only).</summary>
        public static void SetMastery(Expedition ex, MasteryBook book) =>
            typeof(Expedition).GetProperty(nameof(Expedition.Mastery))!.SetValue(ex, book);

        // ------------------------------------------------------------------ snapshots

        /// <summary>
        /// life.test.mjs snap: the session's state, the mastery, the deck, the cards owned (sorted), the journal (in its
        /// order) and the pages — here the whole Expedition as <see cref="Dump"/> writes it (its session and its encounter
        /// with them), and the screen.
        /// </summary>
        public static string Snap(Expedition ex) => Dump.Of(ex) + "\n" + Dump.Of(ex.Session.Screen());

        /// <summary>settle.test.mjs battleJson: the core state, the frame, Finished and WaitingForPlayer.</summary>
        public static string BattleSnap(ITrialSource src) =>
            Dump.Of(src.State) + "\n" + Dump.Of(src.Frame) + "\n" + src.Finished + "|" + src.WaitingForPlayer;
    }

    /// <summary>
    /// A deterministic text of an object graph (the JS's toParityJson): public properties and fields by name; lists and
    /// maps in their order; cards, card and enemy definitions, mastery profiles and maps by their ids; the Expedition, the
    /// intel and the keeper below the root as a mark (they point back up).
    /// </summary>
    internal static class Dump
    {
        public static string Of(object o)
        {
            var sb = new StringBuilder();
            Write(sb, o, 0);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object o, int depth)
        {
            if (depth > 16) { sb.Append('…'); return; }
            switch (o)
            {
                case null: sb.Append("null"); return;
                case string s: sb.Append('"').Append(s).Append('"'); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case Enum e: sb.Append(e.ToString()); return;
                case CardInstance c: sb.Append(c.InstanceId).Append('/').Append(c.Def.Id); return;
                case CardDef d: sb.Append("card:").Append(d.Id); return;
                case EnemyDef ed: sb.Append("enemy:").Append(ed.Id); return;
                case EnemyActionDef ad: sb.Append("action:").Append(ad.Id); return;
                case MasteryProfile mp: sb.Append("profile:").Append(mp.CardId); return;
                case LayerMap m: sb.Append("map:").Append(m.Fingerprint()); return;
                case Delegate _: sb.Append("fn"); return;
                case Type t: sb.Append("type:").Append(t.Name); return;
            }
            if (o.GetType().IsPrimitive || o is decimal)
            {
                sb.Append(((IFormattable)o).ToString(null, CultureInfo.InvariantCulture));
                return;
            }
            if (depth > 0 && (o is Expedition || o is IEnemyIntel || o is IExplorationKeeper))
            {
                sb.Append("ref:").Append(o.GetType().Name);
                return;
            }
            if (o is IEnumerable list && !(o is JsonObject))
            {
                sb.Append('[');
                foreach (object item in list)
                {
                    Write(sb, item, depth + 1);
                    sb.Append(',');
                }
                sb.Append(']');
                return;
            }
            Type type = o.GetType();
            if (o is JsonObject json)
            {
                sb.Append(Json.Stringify(json));
                return;
            }
            sb.Append(type.Name).Append('{');
            foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m is FieldInfo || (m is PropertyInfo p && p.GetIndexParameters().Length == 0 && p.CanRead))
                .OrderBy(m => m.Name, StringComparer.Ordinal))
            {
                object value;
                try
                {
                    value = member is FieldInfo f ? f.GetValue(o) : ((PropertyInfo)member).GetValue(o);
                }
                catch (TargetInvocationException e)
                {
                    value = "!" + e.InnerException?.GetType().Name;
                }
                sb.Append(member.Name).Append('=');
                Write(sb, value, depth + 1);
                sb.Append(';');
            }
            sb.Append('}');
        }
    }
}
