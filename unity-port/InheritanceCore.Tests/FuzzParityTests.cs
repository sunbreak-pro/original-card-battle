using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BattleCore;
using DungeonCore;
using InheritanceCore;
using NUnit.Framework;

namespace InheritanceCore.Tests
{
    /// <summary>
    /// Random op sequences (start, death, survival, end, take, carving, reload, heir journal) that the trial's
    /// Inheritance.js ran (Fixtures/js-fuzz.mjs writes Fixtures/js-fuzz.json): the C# replays every op and must give
    /// the same lineage, hall, ending, trace nodes and take byte for byte, or refuse with the same message. Also
    /// mergeJournal / normalizeJournal / heirJournal / mergeMastery on random journals and records.
    /// INHERITANCE_FUZZ=&lt;path&gt; replays a bigger file made with the same script.
    /// </summary>
    public class FuzzParityTests
    {
        private static JsonObject Load()
        {
            string? env = Environment.GetEnvironmentVariable("INHERITANCE_FUZZ");
            string path = !string.IsNullOrEmpty(env)
                ? env!
                : Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "js-fuzz.json");
            return (JsonObject)Json.Parse(File.ReadAllText(path, Encoding.UTF8))!;
        }

        private static string S(object? v) => Json.Stringify(v);

        /// <summary>js-fuzz.mjs's big(): "sha:" and the first 16 hex digits of the text's SHA-256 (UTF-8).</summary>
        private static string Sha(string text)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
            return "sha:" + string.Concat(hash.Take(8).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
        }

        // ---- the JS inputs as C# inputs ------------------------------------------------------------------

        private static Journal? JournalOf(object? raw)
        {
            if (raw == null) return null;
            var o = (JsonObject)raw;
            var disclosure = new List<KeyValuePair<string, int>>();
            if (o["Disclosure"] is JsonObject d)
                foreach (var pair in d) disclosure.Add(new KeyValuePair<string, int>(pair.Key, (int)pair.Value!));
            string? memo = o.TryGet("Memo", out var m) ? (string?)m : null;
            var scribbles = o["Scribbles"] is List<object?> sl
                ? sl.Cast<JsonObject>().Select(s => new Scribble((int)s["Generation"]!, (string?)s["Name"], (string?)s["Text"])).ToList()
                : null;
            bool carving = o["CarvingTaken"] is bool b && b;
            var pages = o["Pages"];
            if (pages is int count) return Journal.WithPageCount(count, disclosure, memo, scribbles, carving);
            var list = pages is List<object?> pl
                ? pl.Select(p => p is string id
                    ? new JournalPage(id)
                    : new JournalPage((string?)((JsonObject)p!)["Id"], (string?)((JsonObject)p!)["Title"], (string?)((JsonObject)p!)["Text"])).ToList()
                : null;
            return new Journal(disclosure, list, memo, scribbles, carving);
        }

        private static LifeDeck? DeckOf(object? raw)
        {
            if (raw == null) return null;
            return ((List<object?>)raw).Cast<JsonObject>().Select(e => new CardEntry(
                (string?)e["CardId"],
                (int)e["Rank"]!,
                (string?)e["Name"],
                e["Thresholds"] is List<object?> t ? t.Select(x => (int)x!).ToList() : null)).ToList();
        }

        private static EndOptions OptsOf(JsonObject o)
        {
            var opts = new EndOptions();
            if ((string?)o["cardName"] == "fmt") opts.CardName = (id, rank) => FormattableString.Invariant($"{id}@{rank}");
            if ((string?)o["cardName"] == "null") opts.CardName = (_, _) => null;
            if ((string?)o["enemyName"] == "fmt") opts.EnemyName = id => $"敵 {id}";
            if ((string?)o["enemyName"] == "null") opts.EnemyName = _ => null;
            if (o["RootCards"] is List<object?> roots)
                opts.RootCards = roots.Select(r => r is string id ? new CardEntry(id) : new CardEntry((string?)((JsonObject)r!)["CardId"],
                    (int)((JsonObject)r!)["Rank"]!, (string?)((JsonObject)r!)["Name"])).ToList();
            if (o["ExtraBossWon"] is bool won) opts.ExtraBossWon = won;
            return opts;
        }

        private static LifePlace PlaceOf(JsonObject p) => new LifePlace(
            (int)p["Layer"]!,
            p["NodeId"] as int?,
            p["NodeKind"] is string k ? (NodeKind?)Enum.Parse<NodeKind>(k) : null,
            p["Ending"] is string e ? (RunPhase?)Enum.Parse<RunPhase>(e) : null);

        // ---- what the JS printed, the C# way ---------------------------------------------------------------

        private static JsonObject Snapshot(Lineage l)
        {
            var heir = Inheritance.NextHeir(l);
            var o = new JsonObject
            {
                ["lineage"] = Inheritance.Serialize(l),
                ["hall"] = S(Inheritance.HallView(l).ToJson()),
                ["layers"] = S(Inheritance.TraceLayers(l)),
                ["heir"] = S(new JsonObject { ["Generation"] = heir.Generation, ["Name"] = heir.Name, ["Tier"] = heir.Tier }),
            };
            try
            {
                o["ending"] = S(Inheritance.EndingView(l).ToJson());
            }
            catch (InheritanceRuleException e)
            {
                o["ending"] = "ERR " + e.Message;
            }
            o["nodes"] = S(Enumerable.Range(1, 7).Select(layer =>
            {
                var node = Inheritance.TraceNodeView(l, layer);
                return node == null ? null : (object?)S(node.ToJson());
            }).ToList());
            return o;
        }

        private static string StartJson(LifeStart s) => S(new JsonObject
        {
            ["Life"] = s.Life.ToJson(),
            ["Seed"] = s.Seed.ToString(CultureInfo.InvariantCulture),
            ["ToolSlots"] = s.ToolSlots,
            ["ConsumableSlots"] = s.ConsumableSlots,
            ["DeckMin"] = s.DeckMin,
            ["DeckMax"] = s.DeckMax,
            ["Slots"] = s.Loadout.ToolSlotCount,
            ["Journal"] = s.Journal.ToJson(),
            ["JournalFrom"] = s.JournalFrom?.ToJson(),
            ["SurvivorBonus"] = s.SurvivorBonus,
            ["TraceLayers"] = s.TraceLayers.Cast<object?>().ToList(),
            ["CarvingKnown"] = s.CarvingKnown,
            ["Attire"] = s.Attire.Tier,
        });

        private static string ErrorOf(Exception e) => $"{e.GetType().Name}: {e.Message}";

        [Test]
        public void RandomLineages_GiveTheJsLineageViewsAndRefusals()
        {
            var root = Load();
            var failures = new List<string>();
            int steps = 0;
            var scenarios = (List<object?>)root["scenarios"]!;
            for (int n = 0; n < scenarios.Count; n++)
            {
                var scenario = (JsonObject)scenarios[n]!;
                var lineage = Inheritance.NewLineage((string)scenario["seed"]!);
                LifeStart? lastStart = null;
                var list = (List<object?>)scenario["steps"]!;
                for (int i = 0; i < list.Count; i++)
                {
                    steps++;
                    var step = (JsonObject)list[i]!;
                    string op = (string)step["op"]!;
                    string where = $"scenario {n} step {i} ({op})";
                    var got = new JsonObject();
                    try
                    {
                        switch (op)
                        {
                            case "start":
                            {
                                var s = step["seed"] is string seed
                                    ? Inheritance.StartLife(lineage, ulong.Parse(seed, CultureInfo.InvariantCulture))
                                    : Inheritance.StartLife(lineage);
                                lastStart = s;
                                lineage = s.Lineage;
                                got["start"] = StartJson(s);
                                break;
                            }
                            case "reload":
                                lineage = Inheritance.Deserialize(Inheritance.Serialize(lineage));
                                break;
                            case "carving":
                                lineage = Inheritance.NoteCarving(lineage);
                                break;
                            case "heirJournal":
                                got["result"] = S(Inheritance.HeirJournal(lastStart!.Journal, lastStart.JournalFrom).ToJson());
                                break;
                            case "take":
                            {
                                var picks = step["picks"] is List<object?> p ? p.Cast<string>().ToList() : null;
                                var t = Inheritance.TakeTrace(lineage, (string)step["traceId"]!, picks, step["layer"] as int?);
                                lineage = t.Lineage;
                                got["result"] = S(t.Taken.ToJson());
                                break;
                            }
                            case "end":
                            {
                                var place = PlaceOf((JsonObject)step["place"]!);
                                var deck = DeckOf(step["deck"]);
                                var journal = JournalOf(step["journal"]);
                                var opts = OptsOf((JsonObject)step["opts"]!);
                                string kind = (string)step["kind"]!;
                                var life = step["life"];
                                if (life is int generation)
                                {
                                    lineage = kind == "death" ? Inheritance.RecordDeath(lineage, generation, place, deck, journal, opts)
                                        : kind == "survival" ? Inheritance.RecordSurvival(lineage, generation, place, deck, journal, opts)
                                        : Inheritance.RecordEnd(lineage, generation, place, deck, journal, opts);
                                }
                                else
                                {
                                    var record = (string?)life == "record" ? lineage.Current : null;
                                    lineage = kind == "death" ? Inheritance.RecordDeath(lineage, record, place, deck, journal, opts)
                                        : kind == "survival" ? Inheritance.RecordSurvival(lineage, record, place, deck, journal, opts)
                                        : Inheritance.RecordEnd(lineage, record, place, deck, journal, opts);
                                }
                                break;
                            }
                            default:
                                throw new InvalidOperationException(op);
                        }
                        foreach (var pair in Snapshot(lineage)) got[pair.Key] = pair.Value;
                    }
                    catch (Exception e) when (!(e is AssertionException))
                    {
                        got = new JsonObject { ["error"] = ErrorOf(e) };
                    }

                    foreach (var key in new[] { "error", "start", "result", "lineage", "hall", "ending", "nodes", "layers", "heir" })
                    {
                        if (!step.TryGet(key, out var expected)) continue;
                        string want = expected is string w ? w : S(expected);
                        string have = got[key] as string ?? "(none)";
                        if (want.StartsWith("sha:", StringComparison.Ordinal) && got.ContainsKey(key)) have = Sha(have);
                        if (want != have) failures.Add($"{where} {key}\n  js: {want}\n  c#: {got[key] as string ?? "(none)"}");
                    }
                    if (got.ContainsKey("error") && !step.ContainsKey("error"))
                        failures.Add($"{where}: the C# refused ({got["error"]}), the JS did not");
                }
            }
            Assert.That(steps, Is.GreaterThan(0));
            Assert.That(failures, Is.Empty, string.Join("\n", failures.Take(12)) + $"\n({failures.Count} differences in {steps} steps)");
        }

        [Test]
        public void RandomJournalsAndRecords_MergeAsTheJsMerges()
        {
            var root = Load();
            var failures = new List<string>();
            var merges = (List<object?>)root["merges"]!;
            for (int n = 0; n < merges.Count; n++)
            {
                var m = (JsonObject)merges[n]!;
                void Check(string key, string errorKey, Func<string> run)
                {
                    string have;
                    try
                    {
                        have = run();
                    }
                    catch (InheritanceRuleException e)
                    {
                        have = ErrorOf(e);
                    }
                    string want = m.TryGet(key, out var v) ? (string)v! : (string)m[errorKey]!;
                    if (want != have) failures.Add($"merge {n} {key}\n  js: {want}\n  c#: {have}");
                }
                Check("result", "error", () => S(Inheritance.MergeJournal(JournalOf(m["a"]), JournalOf(m["b"])).ToJson()));
                Check("normalized", "normalizedError", () => S(Inheritance.NormalizeJournal(JournalOf(m["a"])).ToJson()));
                var from = m["from"] is JsonObject f ? new HeirRef((int)f["Generation"]!, (string?)f["Name"]) : null;
                Check("heir", "heirError", () => S(Inheritance.HeirJournal(JournalOf(m["a"]), from).ToJson()));

                var records = new Dictionary<string, CardMastery>(StringComparer.Ordinal);
                foreach (var r in ((List<object?>)m["records"]!).Cast<JsonObject>())
                    records[(string)r["CardId"]!] = new CardMastery((string)r["CardId"]!, (int)r["Ticks"]!, (int)r["Rank"]!);
                var cards = ((List<object?>)m["cards"]!).Cast<JsonObject>()
                    .Select(c => new CardMastery((string)c["CardId"]!, (int)c["Ticks"]!, (int)c["Rank"]!)).ToList();
                string mastery = S(Inheritance.MergeMastery(records, cards).Values
                    .Select(r => (object?)new JsonObject { ["CardId"] = r.CardId, ["Ticks"] = r.Ticks, ["Rank"] = r.Rank }).ToList());
                if (mastery != (string)m["mastery"]!) failures.Add($"merge {n} mastery\n  js: {m["mastery"]}\n  c#: {mastery}");
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures.Take(12)) + $"\n({failures.Count} differences)");
        }
    }
}
