// Save by replay (port of src/app/recorder.js, the log half): a life is saved as its start plus the ordered
// list of state-changing calls made on the Expedition (tag E), its ExplorationSession (S), the
// EncounterBattle (B) and each CoreBattleSource (C). Loading rebuilds the Expedition and replays the calls;
// a fight in progress comes back where it stood (tier1 R1-16, #462).
//
// - LogEntry / LifeLog: the entries, [tag, method, args] or [tag, method, [], n] in JSON (a run of n ≥ 2
//   identical calls without arguments is one entry), the JSON form, TrimUnfinished.
// - ReplayTable: the method table the replay dispatches through ("E.StartEncounter" → a call on the raw
//   Expedition). JS replays by name (target[m](...args)); C# has no such call, so every logged method has
//   an entry. Standard() holds the methods today's C# has (the trial's actions, the battle's target and free
//   step among them); the units that add methods to the cores (the Expedition's inheritance and receipt calls)
//   add their entries with Add (it replaces an entry of the same name), on their own table or on
//   ReplayTable.Shared.
// - LifeReplay.ReplayLife: the replay itself (recorder.js replayLife).
//
// The recording side (the facades that push to the log after each call returns, settleWhenDecided) is
// Recorder's. Arguments are JSON values (null, bool, numbers, string, List<object>, JSON objects); enums are
// logged by their name strings, as the JS enums are; a source passed back as an argument
// (EncounterBattle.Finish(source)) is logged as "$source".
//
// Engine-free (no UnityEngine).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Depiction;
using Depiction.Bridge;
using DungeonCore;
using Exploration;
using Exploration.Bridge;

namespace Trial.Bridge
{
    /// <summary>One logged call: [tag, method, args] or, for a run of calls without arguments, [tag, method, [], n].</summary>
    public sealed class LogEntry
    {
        private readonly List<object> _args;

        public LogEntry(string tag, string method, IEnumerable<object> args = null, int count = 1)
        {
            if (string.IsNullOrEmpty(tag)) throw new ArgumentException("LogEntry: a tag is needed.", nameof(tag));
            if (string.IsNullOrEmpty(method)) throw new ArgumentException("LogEntry: a method is needed.", nameof(method));
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), count, "LogEntry: a call happens at least once.");
            Tag = tag;
            Method = method;
            _args = args == null ? new List<object>() : new List<object>(args);
            Count = count;
        }

        /// <summary>E Expedition, S its ExplorationSession, B the EncounterBattle, C the current CoreBattleSource.</summary>
        public string Tag { get; }

        public string Method { get; }

        /// <summary>The arguments as JSON values.</summary>
        public IReadOnlyList<object> Args => _args;

        /// <summary>How many times the call was made in a row (only calls without arguments run together).</summary>
        public int Count { get; internal set; }

        /// <summary>JS e.slice(): the same call, a count of its own.</summary>
        public LogEntry Copy() => new LogEntry(Tag, Method, _args, Count);

        /// <summary>[tag, method, args] or [tag, method, [], n] (the args list is the entry's own: do not change it).</summary>
        public List<object> ToJson()
        {
            var e = new List<object>(4) { Tag, Method, _args };
            if (Count > 1) e.Add((long)Count);
            return e;
        }

        public override string ToString() => Tag + "." + Method + (Count > 1 ? " ×" + Count.ToString(CultureInfo.InvariantCulture) : "");
    }

    /// <summary>The ordered calls of one life.</summary>
    public sealed class LifeLog : IReadOnlyList<LogEntry>
    {
        /// <summary>A recorded source passed back as an argument (EncounterBattle.Finish(source)) is logged as this.</summary>
        public const string SourceMarker = "$source";

        private readonly List<LogEntry> _entries;

        public LifeLog() { _entries = new List<LogEntry>(); }

        public LifeLog(IEnumerable<LogEntry> entries)
        {
            _entries = new List<LogEntry>();
            if (entries != null) foreach (LogEntry e in entries) _entries.Add(e ?? throw new ArgumentException("LifeLog: a null entry."));
        }

        public int Count => _entries.Count;
        public LogEntry this[int index] => _entries[index];
        public IEnumerator<LogEntry> GetEnumerator() => _entries.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _entries.GetEnumerator();

        /// <summary>The last entry, or null.</summary>
        public LogEntry Last => _entries.Count == 0 ? null : _entries[_entries.Count - 1];

        /// <summary>Every call, a run counted as its length.</summary>
        public int Calls
        {
            get
            {
                int n = 0;
                foreach (LogEntry e in _entries) n += e.Count;
                return n;
            }
        }

        // js: app/recorder.js:47
        /// <summary>
        /// Appends a call. A call without arguments that repeats the last entry (same tag and method, no arguments
        /// either) counts that entry up instead. Arguments are turned into JSON values (<see cref="ToJsonArg"/>); a
        /// single list passed here is one argument (use <see cref="PushArgs"/> to pass the argument list itself).
        /// </summary>
        public void Push(string tag, string method, params object[] args)
        {
            PushArgs(tag, method, args ?? Array.Empty<object>());
        }

        /// <summary>Like <see cref="Push(string, string, object[])"/>, with the call's arguments as a list.</summary>
        public void PushArgs(string tag, string method, IReadOnlyList<object> args)
        {
            int n = args == null ? 0 : args.Count;
            LogEntry last = Last;
            if (n == 0 && last != null && last.Tag == tag && last.Method == method && last.Args.Count == 0)
            {
                last.Count = last.Count + 1;
                return;
            }
            var json = new List<object>(n);
            for (int i = 0; i < n; i++) json.Add(ToJsonArg(args[i]));
            _entries.Add(new LogEntry(tag, method, json));
        }

        /// <summary>Appends an entry as it is (a log read back, a test's hand-made one).</summary>
        public void Add(LogEntry entry) => _entries.Add(entry ?? throw new ArgumentNullException(nameof(entry)));

        /// <summary>JS log.splice(0, log.length, ...entries): the log becomes <paramref name="entries"/> in place (main.js:476).</summary>
        public void ReplaceAll(IEnumerable<LogEntry> entries)
        {
            var next = entries == null ? new List<LogEntry>() : new List<LogEntry>(entries);
            _entries.Clear();
            _entries.AddRange(next);
        }

        /// <summary>JS log.map(e => e.slice()): a log of its own, each entry copied.</summary>
        public LifeLog Copy()
        {
            var copy = new LifeLog();
            foreach (LogEntry e in _entries) copy._entries.Add(e.Copy());
            return copy;
        }

        /// <summary>
        /// An argument as a JSON value: null, bool, string as they are; an enum as its name (the JS enums are name
        /// strings); integers as long; float and double as double; lists and JSON objects as they are.
        /// </summary>
        public static object ToJsonArg(object a)
        {
            switch (a)
            {
                case null: return null;
                case string s: return s;
                case bool b: return b;
                case Enum e: return e.ToString();
                case int i: return (long)i;
                case long l: return l;
                case short sh: return (long)sh;
                case byte by: return (long)by;
                case sbyte sb: return (long)sb;
                case ushort us: return (long)us;
                case uint ui: return (long)ui;
                case double d: return d;
                case float f: return (double)f;
                case decimal m: return (double)m;
                default: return a;
            }
        }

        /// <summary>The log as JSON: a list of [tag, method, args] / [tag, method, [], n].</summary>
        public List<object> ToJson()
        {
            var list = new List<object>(_entries.Count);
            foreach (LogEntry e in _entries) list.Add(e.ToJson());
            return list;
        }

        /// <summary>
        /// A log read back from its JSON form. Null reads as an empty log (resumeLife: l.log || []); anything that
        /// is not a list of [tag, method, args(, n)] throws <see cref="SaveFormatException"/>.
        /// </summary>
        public static LifeLog FromJson(object json)
        {
            var log = new LifeLog();
            if (!JsonNumber.Truthy(json)) return log;
            if (!(json is IList list) || json is string) throw new SaveFormatException("bad save: the log is not a list");
            for (int i = 0; i < list.Count; i++)
            {
                if (!(list[i] is IList e) || list[i] is string || e.Count < 3 || e.Count > 4)
                    throw new SaveFormatException("bad save: log entry " + i.ToString(CultureInfo.InvariantCulture) + " is not [tag, method, args]");
                if (!(e[0] is string tag) || tag.Length == 0 || !(e[1] is string method) || method.Length == 0)
                    throw new SaveFormatException("bad save: log entry " + i.ToString(CultureInfo.InvariantCulture) + " names no call");
                if (!(e[2] is IList args) || e[2] is string)
                    throw new SaveFormatException("bad save: log entry " + i.ToString(CultureInfo.InvariantCulture) + " has no argument list");
                int count = 1;
                if (e.Count == 4 && e[3] != null)
                {
                    // JS (n || 1): 0 is one call. A negative n would make the JS skip the call; the game never writes
                    // one, so it is refused here rather than replayed differently.
                    if (!JsonNumber.TryInt(e[3], out count) || count < 0)
                        throw new SaveFormatException("bad save: log entry " + i.ToString(CultureInfo.InvariantCulture) + " has a bad count");
                    if (count == 0) count = 1;
                }
                var items = new List<object>(args.Count);
                foreach (object a in args) items.Add(a);
                log._entries.Add(new LogEntry(tag, method, items, count));
            }
            return log;
        }

        // js: app/recorder.js:161
        /// <summary>
        /// Drops everything from the last B.StartNext that has no B.Finish after it: the fight left unfinished starts
        /// over. Only for a fight that cannot go on (its calls no longer replay, or the battle scene failed on it); a
        /// reload keeps the fight (ReplayLife). Returns a log of its own (each entry copied).
        /// </summary>
        public static LifeLog TrimUnfinished(IReadOnlyList<LogEntry> log)
        {
            int lastStart = -1;
            bool finishedAfter = true;
            for (int i = 0; i < log.Count; i++)
            {
                LogEntry e = log[i];
                if (e.Tag == "B" && e.Method == "StartNext") { lastStart = i; finishedAfter = false; }
                if (e.Tag == "B" && e.Method == "Finish") finishedAfter = true;
            }
            int keep = lastStart >= 0 && !finishedAfter ? lastStart : log.Count;
            var kept = new LifeLog();
            for (int i = 0; i < keep; i++) kept._entries.Add(log[i].Copy());
            return kept;
        }
    }

    /// <summary>A replay that cannot go on: "replay: cannot call T.M" (recorder.js:96), or an argument it cannot read.</summary>
    public sealed class ReplayException : Exception
    {
        public ReplayException(string message) : base(message) { }
    }

    /// <summary>What a replayed call acts on: the Expedition being rebuilt, and the encounter and source the log has opened.</summary>
    public sealed class ReplayContext
    {
        internal ReplayContext(Expedition expedition) { Expedition = expedition; }

        public Expedition Expedition { get; }

        /// <summary>The Expedition's session (the S target).</summary>
        public ExplorationSession Session => Expedition.Session;

        /// <summary>The encounter the last E.StartEncounter returned (the B target), or null.</summary>
        public EncounterBattle Battle { get; internal set; }

        /// <summary>The source the last B.StartNext returned (the C target), or null.</summary>
        public CoreBattleSource Source { get; internal set; }

        /// <summary>A fight was started and is not finished yet.</summary>
        public bool Fighting { get; internal set; }

        /// <summary>The entry being replayed.</summary>
        public LogEntry Entry { get; internal set; }
    }

    /// <summary>The arguments of one replayed call, with readers that turn the JSON values back into the method's.</summary>
    public sealed class ReplayArgs
    {
        private readonly IReadOnlyList<object> _args;
        private readonly ReplayContext _ctx;

        internal ReplayArgs(IReadOnlyList<object> args, ReplayContext ctx)
        {
            _args = args ?? Array.Empty<object>();
            _ctx = ctx;
        }

        public int Count => _args.Count;

        /// <summary>The JSON value as logged.</summary>
        public object this[int index] => _args[index];

        public bool Has(int index) => index < _args.Count;

        public int Int(int index)
        {
            object v = Need(index);
            if (JsonNumber.TryInt(v, out int i)) return i;
            throw Bad(index, "an integer");
        }

        public long Long(int index)
        {
            object v = Need(index);
            if (JsonNumber.TryLong(v, out long l)) return l;
            throw Bad(index, "an integer");
        }

        public double Double(int index)
        {
            object v = Need(index);
            if (JsonNumber.TryDouble(v, out double d)) return d;
            throw Bad(index, "a number");
        }

        /// <summary>A string, or null when the logged value is null.</summary>
        public string String(int index)
        {
            object v = Need(index);
            if (v == null || v is string) return (string)v;
            throw Bad(index, "a string");
        }

        public bool Bool(int index)
        {
            object v = Need(index);
            if (v is bool b) return b;
            throw Bad(index, "true or false");
        }

        /// <summary>The argument at <paramref name="index"/>, or <paramref name="fallback"/> when the call was logged without it.</summary>
        public bool Bool(int index, bool fallback) => Has(index) && _args[index] != null ? Bool(index) : fallback;

        /// <summary>An enum logged by its name.</summary>
        public T Enum<T>(int index) where T : struct, Enum
        {
            object v = Need(index);
            if (TryEnum(v, out T value)) return value;
            throw Bad(index, "a " + typeof(T).Name + " name");
        }

        /// <summary>Whether the argument is the name of a <typeparamref name="T"/> member.</summary>
        public bool IsEnum<T>(int index) where T : struct, Enum => Has(index) && TryEnum(_args[index], out T _);

        /// <summary>
        /// A string as it is, an integer as its invariant-culture text, null as null: an argument the C# takes as text
        /// that the browser logs as a number (ExplorationSession.Act's arg: the slot of TakeFound, the enemy id of
        /// ChooseSurvey).
        /// </summary>
        public string Text(int index)
        {
            object v = Need(index);
            if (v == null || v is string) return (string)v;
            if (JsonNumber.TryLong(v, out long l)) return l.ToString(CultureInfo.InvariantCulture);
            throw Bad(index, "a string or an integer");
        }

        /// <summary>"$source": the source the log has open.</summary>
        public CoreBattleSource Source(int index)
        {
            object v = Need(index);
            if (v is string s && s == LifeLog.SourceMarker) return _ctx.Source;
            throw Bad(index, "\"" + LifeLog.SourceMarker + "\"");
        }

        // A member's name exactly as the JS enums are (defineEnum: the name string). Enum.TryParse would also take
        // " Rest", "Rest, Train" or a number; none of them is a name the JS logs.
        private static bool TryEnum<T>(object v, out T value) where T : struct, Enum
        {
            value = default;
            if (!(v is string name) || Array.IndexOf(System.Enum.GetNames(typeof(T)), name) < 0) return false;
            value = (T)System.Enum.Parse(typeof(T), name);
            return true;
        }

        private object Need(int index)
        {
            if (index < _args.Count) return _args[index];
            throw new ReplayException("replay: " + Where() + " has no argument " + index.ToString(CultureInfo.InvariantCulture));
        }

        private ReplayException Bad(int index, string what) =>
            new ReplayException("replay: " + Where() + " argument " + index.ToString(CultureInfo.InvariantCulture) + " is not " + what);

        private string Where() => _ctx.Entry == null ? "a call" : _ctx.Entry.Tag + "." + _ctx.Entry.Method;
    }

    /// <summary>One replayed call: runs it on the raw object and returns what it returned.</summary>
    public delegate object ReplayCall(ReplayContext ctx, ReplayArgs args);

    /// <summary>The logged methods by "Tag.Method", and how each is called again.</summary>
    public sealed class ReplayTable
    {
        private readonly Dictionary<string, ReplayCall> _calls = new Dictionary<string, ReplayCall>(StringComparer.Ordinal);

        /// <summary>
        /// The table <see cref="LifeReplay.ReplayLife"/> uses when it is given none: <see cref="Standard"/> plus what
        /// other units added. Reset by <see cref="ResetShared"/> (tests; Unity runs with domain reload off, and Add
        /// replaces an entry of the same name, so adding twice is harmless).
        /// </summary>
        public static ReplayTable Shared { get; private set; } = Standard();

        /// <summary>Shared goes back to <see cref="Standard"/>.</summary>
        public static void ResetShared() { Shared = Standard(); }

        public static string KeyOf(string tag, string method) => tag + "." + method;

        /// <summary>Adds or replaces the call for Tag.Method. Returns this table.</summary>
        public ReplayTable Add(string tag, string method, ReplayCall call)
        {
            if (string.IsNullOrEmpty(tag)) throw new ArgumentException("ReplayTable: a tag is needed.", nameof(tag));
            if (string.IsNullOrEmpty(method)) throw new ArgumentException("ReplayTable: a method is needed.", nameof(method));
            _calls[KeyOf(tag, method)] = call ?? throw new ArgumentNullException(nameof(call));
            return this;
        }

        public bool Remove(string tag, string method) => _calls.Remove(KeyOf(tag, method));

        public bool Has(string tag, string method) => _calls.ContainsKey(KeyOf(tag, method));

        public bool TryGet(string tag, string method, out ReplayCall call) => _calls.TryGetValue(KeyOf(tag, method), out call);

        /// <summary>Every "Tag.Method" the table knows, sorted (ordinal).</summary>
        public IReadOnlyList<string> Keys
        {
            get
            {
                var keys = new List<string>(_calls.Keys);
                keys.Sort(StringComparer.Ordinal);
                return keys;
            }
        }

        /// <summary>A table of its own with the same calls.</summary>
        public ReplayTable Copy()
        {
            var t = new ReplayTable();
            foreach (var pair in _calls) t._calls[pair.Key] = pair.Value;
            return t;
        }

        /// <summary>
        /// The calls today's C# cores have (app.md §2.4; the JS-only ones are added by the units that port them):
        /// E StartEncounter / Return / Abandon / Learn / Train; S Step / Jump / UseConsumable / Act (any ScreenAction,
        /// the trial's included, with its argument) / OpenTraining / PutOffTraining / Escape / Toggle; B StartNext /
        /// Finish; C AdvanceAuto / Next (replayed as AdvanceAuto: JS logs Next only while a script event is pending,
        /// where the two are the same call) / EndTurn / TryPlay (card, zone[, target]) / TakeFreeStep.
        /// Still to come from the units that add them to the Expedition: E Inherit / CloseResult / ShowTaken /
        /// CloseTaken / Take.
        /// </summary>
        public static ReplayTable Standard()
        {
            var t = new ReplayTable();
            t.Add("E", "StartEncounter", (c, a) => c.Expedition.StartEncounter());
            t.Add("E", "Return", (c, a) => c.Expedition.Return());
            t.Add("E", "Abandon", (c, a) => { c.Expedition.Abandon(); return null; });
            t.Add("E", "Learn", (c, a) => c.Expedition.Learn(a.String(0)));
            t.Add("E", "Train", (c, a) => c.Expedition.Train(a.String(0)));

            // A choice the screen left undefined is logged as null (JSON.stringify): the default, Rest.
            t.Add("S", "Step", (c, a) => c.Session.Step(a.Int(0), a.Has(1) && a[1] != null ? a.Enum<RestChoice>(1) : RestChoice.Rest));
            t.Add("S", "Jump", (c, a) => c.Session.Jump(a.Int(0), a.Has(1) && a[1] != null ? a.Enum<RestChoice>(1) : RestChoice.Rest));
            t.Add("S", "UseConsumable", (c, a) => c.Session.UseConsumable(a.Int(0)));
            t.Add("S", "Act", (c, a) =>
            {
                // Act(action) or Act(action, arg) (ExploreScene.js:1170 logs both, arg null when there is none). The
                // browser's TrialAction names are ScreenAction members here; the arg is TakeFound's slot (a number in
                // the browser's log, invariant text for the C#) or ChooseSurvey's enemy id.
                if (a.Count < 1 || a.Count > 2)
                    throw new ReplayException("replay: cannot call S.Act with " + a.Count.ToString(CultureInfo.InvariantCulture) + " arguments");
                return c.Session.Act(a.Enum<ScreenAction>(0), a.Has(1) ? a.Text(1) : null);
            });
            t.Add("S", "OpenTraining", (c, a) => c.Session.OpenTraining());
            t.Add("S", "PutOffTraining", (c, a) => c.Session.PutOffTraining());
            t.Add("S", "Escape", (c, a) => c.Session.Escape());
            t.Add("S", "Toggle", (c, a) => c.Session.Toggle(a.Enum<Overlay>(0)));

            t.Add("B", "StartNext", (c, a) => c.Battle.StartNext(a.Bool(0, false)));
            t.Add("B", "Finish", (c, a) => { c.Battle.Finish(a.Source(0)); return null; });

            t.Add("C", "AdvanceAuto", (c, a) => c.Source.AdvanceAuto());
            t.Add("C", "Next", (c, a) => c.Source.AdvanceAuto());
            t.Add("C", "EndTurn", (c, a) => c.Source.EndTurn());
            t.Add("C", "TryPlay", (c, a) =>
            {
                // TryPlay(card, zone) or TryPlay(card, zone, target): the enemy the card was released on, by its index
                // (player.js:1677). A null target is the JS's `target ?? TargetFor`: the C#'s -1.
                if (a.Count < 2 || a.Count > 3)
                    throw new ReplayException("replay: cannot call C.TryPlay with " + a.Count.ToString(CultureInfo.InvariantCulture) + " arguments");
                int target = a.Has(2) && a[2] != null ? a.Int(2) : -1;
                return c.Source.TryPlay(a.String(0), a.Enum<DropZone>(1), target, out DepictionEvent _);
            });
            t.Add("C", "TakeFreeStep", (c, a) => c.Source.TakeFreeStep(a.Int(0)));
            return t;
        }
    }

    /// <summary>The fight a log left in progress: the raw encounter and source, standing where the last saved call left them.</summary>
    public sealed class OpenFight
    {
        public OpenFight(EncounterBattle battle, CoreBattleSource source)
        {
            Battle = battle;
            Source = source;
        }

        public EncounterBattle Battle { get; }
        public CoreBattleSource Source { get; }
    }

    /// <summary>What <see cref="LifeReplay.ReplayLife"/> returns: the log as replayed (a copy) and the fight left open, or null.</summary>
    public sealed class ReplayResult
    {
        public ReplayResult(LifeLog kept, OpenFight open)
        {
            Kept = kept;
            Open = open;
        }

        public LifeLog Kept { get; }
        public OpenFight Open { get; }
    }

    public static class LifeReplay
    {
        // js: app/recorder.js:88
        /// <summary>
        /// Replays a log onto a fresh Expedition (the raw one: nothing is logged), fights in progress included.
        /// Returns the log as replayed (a copy) and the fight left in progress, or null. A call the table does not
        /// know, or one whose target is not open, throws "replay: cannot call T.M"; a call that throws in the core
        /// throws as it is.
        /// </summary>
        public static ReplayResult ReplayLife(Expedition expedition, IReadOnlyList<LogEntry> log, ReplayTable table = null)
        {
            if (expedition == null) throw new ArgumentNullException(nameof(expedition));
            if (log == null) throw new ArgumentNullException(nameof(log));
            table = table ?? ReplayTable.Shared;
            var kept = new LifeLog();
            foreach (LogEntry e in log) kept.Add(e.Copy());
            var ctx = new ReplayContext(expedition);
            foreach (LogEntry e in kept)
            {
                ctx.Entry = e;
                var args = new ReplayArgs(e.Args, ctx);
                for (int k = 0; k < e.Count; k++)
                {
                    object target = e.Tag == "E" ? expedition : e.Tag == "S" ? expedition.Session : e.Tag == "B" ? ctx.Battle : (object)ctx.Source;
                    if (target == null || !table.TryGet(e.Tag, e.Method, out ReplayCall call))
                        throw new ReplayException("replay: cannot call " + e.Tag + "." + e.Method);
                    object r = call(ctx, args);
                    if (e.Tag == "E" && e.Method == "StartEncounter") { ctx.Battle = r as EncounterBattle; ctx.Fighting = false; }
                    if (e.Tag == "E" && (e.Method == "Abandon" || e.Method == "Return")) ctx.Fighting = false;
                    if (e.Tag == "B" && e.Method == "StartNext") { ctx.Source = r as CoreBattleSource; ctx.Fighting = true; }
                    if (e.Tag == "B" && e.Method == "Finish") ctx.Fighting = false;
                }
            }
            ctx.Entry = null;
            OpenFight open = ctx.Fighting && ctx.Battle != null && ctx.Source != null && ReferenceEquals(expedition.Battle, ctx.Battle)
                ? new OpenFight(ctx.Battle, ctx.Source)
                : null;
            return new ReplayResult(kept, open);
        }

        // js: app/recorder.js:109
        /// <summary>Replays a log onto a fresh Expedition; returns the log as replayed (ReplayLife's Kept).</summary>
        public static LifeLog Replay(Expedition expedition, IReadOnlyList<LogEntry> log, ReplayTable table = null) =>
            ReplayLife(expedition, log, table).Kept;
    }

    /// <summary>Reading JSON numbers whatever CLR type the parser gave them (long, int, double, …).</summary>
    public static class JsonNumber
    {
        /// <summary>JS truthiness of a JSON value: null, false, 0, NaN and "" are false; everything else is true.</summary>
        public static bool Truthy(object v)
        {
            switch (v)
            {
                case null: return false;
                case bool b: return b;
                case string s: return s.Length > 0;
            }
            if (TryDouble(v, out double d)) return d != 0 && !double.IsNaN(d);
            return true;
        }

        public static bool TryDouble(object v, out double d)
        {
            switch (v)
            {
                case double x: d = x; return true;
                case long x: d = x; return true;
                case int x: d = x; return true;
                case float x: d = x; return true;
                case decimal x: d = (double)x; return true;
                case short x: d = x; return true;
                case uint x: d = x; return true;
                case ulong x: d = x; return true;
                case byte x: d = x; return true;
                case sbyte x: d = x; return true;
                case ushort x: d = x; return true;
                default: d = 0; return false;
            }
        }

        /// <summary>An integral number within long.</summary>
        public static bool TryLong(object v, out long l)
        {
            switch (v)
            {
                case long x: l = x; return true;
                case int x: l = x; return true;
                case short x: l = x; return true;
                case byte x: l = x; return true;
                case sbyte x: l = x; return true;
                case ushort x: l = x; return true;
                case uint x: l = x; return true;
                case ulong x when x <= long.MaxValue: l = (long)x; return true;
            }
            if (TryDouble(v, out double d) && !double.IsNaN(d) && Math.Floor(d) == d && d >= -9.2233720368547758E18 && d < 9.2233720368547758E18)
            {
                l = (long)d;
                return true;
            }
            l = 0;
            return false;
        }

        /// <summary>An integral number within int.</summary>
        public static bool TryInt(object v, out int i)
        {
            if (TryLong(v, out long l) && l >= int.MinValue && l <= int.MaxValue)
            {
                i = (int)l;
                return true;
            }
            i = 0;
            return false;
        }
    }
}
