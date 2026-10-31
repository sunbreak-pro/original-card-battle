#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonCore;
using static System.FormattableString;

// The lineage's save (core/inheritance/Inheritance.js:1113-1231, tier1 R1-16: a death, an end and a taken trace are
// saved the moment they happen). The JSON is the JS's byte for byte (Lineage.ToJson writes the JS's key order and
// Json.Stringify writes what JSON.stringify writes). FromJson checks a saved lineage as fromJSON does and throws
// InheritanceRuleException("bad save: …") on one it cannot read.
namespace InheritanceCore
{
    public static partial class Inheritance
    {
        /// <summary>The lineage as a plain JSON value (a fresh copy).</summary>
        // js: core/inheritance/Inheritance.js:1118
        public static JsonObject ToJson(Lineage lineage) => lineage.ToJson();

        // js: core/inheritance/Inheritance.js:1122
        private static Journal? CheckJournalShape(object? j, string what)
        {
            if (j == null) return null;
            if (!(j is JsonObject) && !(j is List<object?>)) throw new InheritanceRuleException($"bad save: {what} journal");
            return LineageJson.JournalFromJson(j);
        }

        // js: core/inheritance/Inheritance.js:1128
        private static Candidate CheckCandidate(object? c, string traceId)
        {
            var id = Js.Get(c, "Id");
            var name = Js.Get(c, "Name");
            CandidateKind kind = default;
            if (!Js.IsObject(c) || !(id is string) || !Js.IsMember(Js.Get(c, "Kind"), out kind) || !(name is string))
                throw new InheritanceRuleException($"bad save: a candidate of {traceId}");
            if (kind == CandidateKind.Card)
            {
                Js.RequireInt(Js.Get(c, "Rank"), $"rank in {traceId}", 0, 3);
                Js.RequireInt(Js.Get(c, "Ticks"), $"ticks in {traceId}", 0);
            }
            if (kind == CandidateKind.EnemyPage) Js.RequireInt(Js.Get(c, "Disclosure"), $"disclosure in {traceId}", 1, InheritanceRules.MaxDisclosure);
            return new Candidate(
                (string)id!,
                kind,
                (string)name!,
                cardId: Js.OptionalString(Js.Get(c, "CardId")),
                rank: Js.OptionalInt(Js.Get(c, "Rank")),
                ticks: Js.OptionalInt(Js.Get(c, "Ticks")),
                enemyId: Js.OptionalString(Js.Get(c, "EnemyId")),
                disclosure: Js.OptionalInt(Js.Get(c, "Disclosure")),
                pageId: Js.OptionalString(Js.Get(c, "PageId")),
                // isFilledPage reads only a string Text: a page whose Text is another value is an empty page.
                text: kind == CandidateKind.DungeonPage ? Js.Get(c, "Text") as string : Js.OptionalString(Js.Get(c, "Text")),
                generation: Js.OptionalInt(Js.Get(c, "Generation")),
                from: Js.OptionalString(Js.Get(c, "From")));
        }

        // js: core/inheritance/Inheritance.js:1139
        private static LifeRecord CheckLifeRecord(object? l, bool open)
        {
            if (!Js.IsObject(l)) throw new InheritanceRuleException("bad save: a life");
            int generation = Js.RequireInt(Js.Get(l, "Generation"), "generation", 1);
            if (!(Js.Get(l, "Name") is string name)) throw new InheritanceRuleException("bad save: a life name");
            int tier = Js.RequireInt(Js.Get(l, "Tier"), "tier", 0, AttireTiers.Count - 1);
            ulong seed = Js.SeedOf(Js.Get(l, "Seed"));
            var endingValue = Js.Get(l, "Ending");
            LifeEnding? ending = null;
            int? layer;
            if (open)
            {
                // `l.Ending !== null`: an absent Ending is not null, so it is refused too.
                if (endingValue != null) throw new InheritanceRuleException("bad save: the current life has an ending");
                layer = Js.OptionalInt(Js.Get(l, "Layer"));
            }
            else
            {
                if (!Js.IsMember(endingValue, out LifeEnding parsed))
                    throw new InheritanceRuleException($"bad save: ending {Js.Display(endingValue)}");
                ending = parsed;
                layer = Js.RequireInt(Js.Get(l, "Layer"), "layer", 1, SevenLayers.Count);
            }
            var rawSeed = Js.Get(l, "Seed");
            return new LifeRecord(
                generation,
                name,
                tier,
                rawSeed is string s ? s : Ulong(seed),
                Js.Truthy(Js.Get(l, "SurvivorBonus")),
                Js.OptionalInt(Js.Get(l, "SurvivorFrom")),
                Js.Truthy(Js.Get(l, "InheritedJournal")),
                Js.OptionalString(Js.Get(l, "TakenTraceId")),
                ending,
                layer,
                Js.OptionalInt(Js.Get(l, "NodeId")),
                Js.OptionalNodeKind(Js.Get(l, "NodeKind")),
                Js.OptionalString(Js.Get(l, "TraceId")),
                Js.OptionalInt(Js.Get(l, "CollectedBy")),
                Js.OptionalInt(Js.Get(l, "ReplacedSurvivor")));
        }

        /// <summary>A saved lineage back, checked. Throws InheritanceRuleException on a save it cannot read.</summary>
        // js: core/inheritance/Inheritance.js:1171
        public static Lineage FromJson(object? data)
        {
            if (!Js.IsObject(data)) throw new InheritanceRuleException("bad save: not an object");
            var version = Js.Get(data, "Version");
            if (!(Json.IsNumber(version) && Json.ToDouble(version) == SaveVersion))
                throw new InheritanceRuleException($"bad save: version {Js.Display(version)}");
            Js.SeedOf(Js.Get(data, "Seed"));
            if (!(Js.Get(data, "Lives") is List<object?> rawLives) || !(Js.Get(data, "Traces") is List<object?> rawTraces))
                throw new InheritanceRuleException("bad save: lists");
            var lives = rawLives.Select(l => CheckLifeRecord(l, false)).ToList();
            for (int i = 0; i < lives.Count; i++)
            {
                if (lives[i].Generation != i + 1) throw new InheritanceRuleException("bad save: generations out of order");
            }
            var rawCurrent = Js.Get(data, "Current");
            var current = Js.Truthy(rawCurrent) ? CheckLifeRecord(rawCurrent, true) : null;
            if (current != null && current.Generation != lives.Count + 1) throw new InheritanceRuleException("bad save: current generation");
            var traces = rawTraces.Select(t =>
            {
                if (!Js.IsObject(t) || !(Js.Get(t, "Id") is string id)) throw new InheritanceRuleException("bad save: a trace");
                int layer = Js.RequireInt(Js.Get(t, "Layer"), "trace layer", 1, SevenLayers.Count);
                int generation = Js.RequireInt(Js.Get(t, "Generation"), "trace generation", 1);
                var endingValue = Js.Get(t, "Ending");
                if (!Js.IsMember(endingValue, out LifeEnding ending))
                    throw new InheritanceRuleException($"bad save: ending {Js.Display(endingValue)} of {id}");
                return new Trace(
                    id,
                    generation,
                    Js.Display(Js.Get(t, "Name")),
                    layer,
                    Js.OptionalInt(Js.Get(t, "NodeId")),
                    Js.OptionalNodeKind(Js.Get(t, "NodeKind")),
                    ending,
                    Js.Truthy(Js.Get(t, "Cleared")),
                    Js.List(Js.Get(t, "Candidates"), $"candidates of {id}").Select(c => CheckCandidate(c, id)),
                    Js.List(Js.Get(t, "Withheld"), $"withheld of {id}").Select(w => new WithheldCard(
                        Js.Display(Js.Get(w, "CardId")),
                        Js.RequireInt(Js.Get(w, "Rank"), "withheld rank", 0, 3),
                        Js.Display(Js.Get(w, "Name")))));
            }).ToList();
            Survivor? survivor = null;
            var rawSurvivor = Js.Get(data, "Survivor");
            if (Js.Truthy(rawSurvivor))
            {
                int generation = Js.RequireInt(Js.Get(rawSurvivor, "Generation"), "survivor generation", 1);
                string name = Js.Display(Js.Get(rawSurvivor, "Name"));
                int left = Js.RequireInt(Js.Get(rawSurvivor, "BonusLivesLeft"), "survivor bonus", 0);
                var rawLives2 = Js.Get(rawSurvivor, "BonusLives");
                int lives2 = Js.RequireInt(Js.IsNullish(rawLives2) ? Js.Get(rawSurvivor, "BonusLivesLeft") : rawLives2, "survivor bonus lives", 0);
                var rawJournal = Js.Get(rawSurvivor, "Journal");
                survivor = new Survivor(generation, name, left, lives2, CheckJournalShape(Js.IsNullish(rawJournal) ? null : rawJournal, "survivor"));
            }
            var player = Js.Get(data, "Player");
            return new Lineage(
                SaveVersion,
                Ulong(Js.SeedOf(Js.Get(data, "Seed"))),
                lives,
                current,
                traces,
                survivor,
                new PlayerKnowledge(Js.Truthy(Js.Get(player, "CarvingTaken")), Js.Truthy(Js.Get(player, "RootDefeated"))));
        }

        /// <summary>JSON text of the lineage (localStorage's / the save file's).</summary>
        // js: core/inheritance/Inheritance.js:1224
        public static string Serialize(Lineage lineage) => Json.Stringify(lineage.ToJson());

        /// <summary>A lineage from JSON text (JsonException on text that does not read, InheritanceRuleException on a bad save).</summary>
        // js: core/inheritance/Inheritance.js:1229
        public static Lineage Deserialize(string text) => FromJson(Json.Parse(text));

        /// <summary>
        /// A journal from its JSON (normalizeJournal on a plain value): 開示度 as an object, pages as a count, ids or
        /// { Id, Title, Text } objects, the memo, the 走り書き; PascalCase or camelCase keys. Null reads as empty.
        /// </summary>
        // js: core/inheritance/Inheritance.js:306
        public static Journal JournalFromJson(object? json) => LineageJson.JournalFromJson(json);
    }

    /// <summary>The JSON halves of the module that are not the lineage itself (journal, take).</summary>
    internal static class LineageJson
    {
        // js: core/inheritance/Inheritance.js:306 (normalizeJournal over a plain JSON value)
        public static Journal JournalFromJson(object? journal)
        {
            if (Js.IsNullish(journal)) return Inheritance.EmptyJournal();
            var disclosure = new List<KeyValuePair<string, int>>();
            var rawDisclosure = Js.Field(journal, "Disclosure");
            foreach (var pair in Js.Entries(rawDisclosure).OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                int level = Js.RequireInt(pair.Value, $"disclosure of {pair.Key}", 0, InheritanceRules.MaxDisclosure);
                if (level > 0) disclosure.Add(new KeyValuePair<string, int>(pair.Key, level));
            }

            var rawPages = Js.Field(journal, "Pages");
            if (Js.IsNullish(rawPages)) rawPages = new List<object?>();
            var pages = new List<JournalPage>();
            if (Json.IsInteger(rawPages))
            {
                int count = Js.RequireInt(rawPages, "page count", 0);
                for (int i = 1; i <= count; i++) pages.Add(new JournalPage("page-" + i, InheritanceText.RarePageTitle(i)));
            }
            else if (rawPages is List<object?> list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var p = list[i];
                    if (p is string id)
                    {
                        pages.Add(new JournalPage(id, InheritanceText.RarePageTitle(i + 1)));
                        continue;
                    }
                    var title = Js.Field(p, "Title");
                    string pageId = Js.Display(Js.Field(p, "Id"));
                    string pageTitle = Js.IsNullish(title) ? InheritanceText.RarePageTitle(i + 1) : Js.Display(title);
                    var text = Js.Field(p, "Text");
                    pages.Add(new JournalPage(pageId, pageTitle, text is string t && !JsText.IsBlank(t) ? t : null));
                }
            }
            else
            {
                throw new InheritanceRuleException("journal pages must be a count or a list");
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in pages)
            {
                if (!seen.Add(p.Id!)) throw new InheritanceRuleException($"page {p.Id} is in the journal twice");
            }

            var memo = Js.Field(journal, "Memo");
            if (Js.IsNullish(memo)) memo = "";
            if (!(memo is string memoText)) throw new InheritanceRuleException("the memo is text");
            var rawScribbles = Js.Field(journal, "Scribbles");
            var scribbles = Js.List(Js.IsNullish(rawScribbles) ? new List<object?>() : rawScribbles, "journal scribbles")
                .Select(s => new Scribble(
                    Js.RequireInt(Js.Field(s, "Generation"), "scribble generation", 1),
                    Js.Display(Js.Field(s, "Name")),
                    Js.Display(Js.Field(s, "Text"))))
                .ToList();
            return Inheritance.NormalizeJournal(new Journal(disclosure, pages, memoText, scribbles));
        }

        /// <summary>A take back from its JSON ({ TraceId, Cards, Journal, Lost }), as the recorder logs it.</summary>
        public static TakenLegacy TakenFromJson(object? json)
        {
            if (!Js.IsObject(json)) throw new InheritanceRuleException("a take is an object");
            var cards = Js.List(Js.IsNullish(Js.Get(json, "Cards")) ? new List<object?>() : Js.Get(json, "Cards"), "taken cards")
                .Select(c =>
                {
                    string cardId = Js.Display(Js.Get(c, "CardId"));
                    var name = Js.Get(c, "Name");
                    return new TakenCard(
                        cardId,
                        Js.OptionalInt(Js.Get(c, "Rank")) ?? 0,
                        Js.OptionalInt(Js.Get(c, "Ticks")) ?? 0,
                        Js.IsNullish(name) ? null : Js.Display(name));
                })
                .ToList();
            return new TakenLegacy(
                Js.OptionalString(Js.Get(json, "TraceId")) ?? "",
                cards,
                JournalFromJson(Js.Get(json, "Journal")),
                Js.OptionalInt(Js.Get(json, "Lost")) ?? 0);
        }
    }

    /// <summary>
    /// JavaScript's reading of a plain JSON value, for the checks that port JS code reading parsed JSON: an absent key
    /// is <see cref="Undefined"/> (not null), String(v), truthiness, Number.isInteger, BigInt seeds.
    /// </summary>
    internal static class Js
    {
        /// <summary>The JS's undefined: what reading an absent key gives.</summary>
        public static readonly object Undefined = new UndefinedValue();

        private sealed class UndefinedValue
        {
            public override string ToString() => "undefined";
        }

        public static bool IsNullish(object? v) => v == null || ReferenceEquals(v, Undefined);

        /// <summary>typeof v === 'object' &amp;&amp; v !== null (arrays included).</summary>
        public static bool IsObject(object? v) => v is JsonObject || v is List<object?>;

        /// <summary>v[key]: Undefined when absent or when v holds no properties.</summary>
        public static object? Get(object? v, string key)
        {
            if (v is JsonObject o && o.TryGet(key, out var value)) return value;
            return Undefined;
        }

        /// <summary>The JS's field(obj, name): the PascalCase key, else the camelCase one.</summary>
        public static object? Field(object? v, string name)
        {
            var value = Get(v, name);
            if (!ReferenceEquals(value, Undefined)) return value;
            return Get(v, char.ToLowerInvariant(name[0]) + name.Substring(1));
        }

        /// <summary>Object.entries(v ?? {}) for an object, an array or a string.</summary>
        public static IEnumerable<KeyValuePair<string, object?>> Entries(object? v)
        {
            switch (v)
            {
                case JsonObject o:
                    return o.ToList();
                case List<object?> list:
                    return list.Select((x, i) => new KeyValuePair<string, object?>(i.ToString(CultureInfo.InvariantCulture), x)).ToList();
                case string s:
                    return s.Select((c, i) => new KeyValuePair<string, object?>(i.ToString(CultureInfo.InvariantCulture), c.ToString())).ToList();
                default:
                    return Array.Empty<KeyValuePair<string, object?>>();
            }
        }

        /// <summary>An array value; anything else is refused (where the JS would fail on .map).</summary>
        public static List<object?> List(object? v, string what)
        {
            if (IsNullish(v)) return new List<object?>();
            if (v is List<object?> list) return list;
            throw new InheritanceRuleException($"bad save: {what} is not a list");
        }

        /// <summary>JS truthiness.</summary>
        public static bool Truthy(object? v)
        {
            switch (v)
            {
                case null: return false;
                case bool b: return b;
                case string s: return s.Length > 0;
                default:
                    if (ReferenceEquals(v, Undefined)) return false;
                    if (Json.IsNumber(v))
                    {
                        double d = Json.ToDouble(v);
                        return d != 0 && !double.IsNaN(d);
                    }
                    return true;
            }
        }

        /// <summary>String(v).</summary>
        public static string Display(object? v)
        {
            switch (v)
            {
                case null: return "null";
                case string s: return s;
                case bool b: return b ? "true" : "false";
                case JsonObject _: return "[object Object]";
                case List<object?> list: return string.Join(",", list.Select(x => IsNullish(x) ? "" : Display(x)));
                default:
                    if (ReferenceEquals(v, Undefined)) return "undefined";
                    if (Json.IsNumber(v)) return Json.NumberToString(Json.ToDouble(v));
                    return v.ToString() ?? "";
            }
        }

        /// <summary>requireInt on a JSON value (Number.isInteger and the range).</summary>
        public static int RequireInt(object? v, string what, double min = double.NegativeInfinity, double max = double.PositiveInfinity)
        {
            if (!Json.IsInteger(v)) throw new InheritanceRuleException(Inheritance.RangeMessage(what, min, max, Display(v)));
            double d = Json.ToDouble(v);
            if (d < min || d > max) throw new InheritanceRuleException(Inheritance.RangeMessage(what, min, max, Display(v)));
            if (d < int.MinValue || d > int.MaxValue)
                throw new InheritanceRuleException($"{what} {Display(v)} is too large for this port");
            return (int)d;
        }

        /// <summary>A kept-as-is integer field (`x ?? null`): null when absent or null; a non-integer is refused.</summary>
        public static int? OptionalInt(object? v)
        {
            if (IsNullish(v)) return null;
            if (!Json.IsInteger(v)) throw new InheritanceRuleException($"bad save: {Display(v)} is not an integer");
            double d = Json.ToDouble(v);
            if (d < int.MinValue || d > int.MaxValue) throw new InheritanceRuleException($"bad save: {Display(v)} is too large");
            return (int)d;
        }

        /// <summary>A kept-as-is text field: null when absent or null; another value as String(v).</summary>
        public static string? OptionalString(object? v) => IsNullish(v) ? null : Display(v);

        /// <summary>A kept-as-is node kind (its NodeKind name): null when absent or null; an unknown name is refused.</summary>
        public static NodeKind? OptionalNodeKind(object? v)
        {
            if (IsNullish(v)) return null;
            if (IsMember(v, out NodeKind kind)) return kind;
            throw new InheritanceRuleException($"bad save: unknown node kind {Display(v)}");
        }

        /// <summary>
        /// isMember(E, v): <paramref name="v"/> is one of the enum's member names, exactly. Enum.TryParse alone is not
        /// enough: it also reads "42" as an undefined value, and (TEnum)42 prints as "42".
        /// </summary>
        // js: core/inheritance/Inheritance.js:207
        public static bool IsMember<TEnum>(object? v, out TEnum value) where TEnum : struct, Enum
        {
            value = default;
            if (!(v is string s)) return false;
            foreach (TEnum member in Enum.GetValues(typeof(TEnum)))
            {
                if (string.Equals(member.ToString(), s, StringComparison.Ordinal))
                {
                    value = member;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// seedOf on a JSON value: decimal digits (or an integer number) wrapped to a ulong, as BigInt.asUintN(64, …).
        /// </summary>
        // js: core/inheritance/Inheritance.js:220
        public static ulong SeedOf(object? v)
        {
            if (IsNullish(v)) throw new InheritanceRuleException("a seed is required");
            if (v is string s) return Inheritance.SeedOf(s);
            if (v is bool b) return b ? 1UL : 0UL;
            if (Json.IsInteger(v)) return WrapToUlong(Json.ToDouble(v));
            // BigInt() throws on anything else (a RangeError or SyntaxError in the JS).
            throw new InheritanceRuleException($"bad seed {Display(v)}");
        }

        /// <summary>An integral double mod 2^64 (exact: the double's bits are shifted, not rounded).</summary>
        private static ulong WrapToUlong(double d)
        {
            if (Math.Abs(d) < 9223372036854775808.0) return unchecked((ulong)(long)d);
            long bits = BitConverter.DoubleToInt64Bits(Math.Abs(d));
            int exponent = (int)((bits >> 52) & 0x7FF) - 1075; // value = mantissa × 2^exponent
            ulong mantissa = (ulong)(bits & 0xFFFFFFFFFFFFFL) | (1UL << 52);
            ulong magnitude = exponent >= 64 ? 0UL : unchecked(mantissa << exponent);
            return d < 0 ? unchecked(0UL - magnitude) : magnitude;
        }
    }
}
