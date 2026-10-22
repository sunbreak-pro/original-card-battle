// The oracle's JSON form (docs/PARITY.md, docs/PARITY_SHAPES.md). One rule set, applied by reflection
// to every value a suite hands in, so the JS side can mirror it with one serializer
// (test/parity/run.mjs → toOracleJson):
//
// - A record / class / struct of the game's namespaces → { "$type": <C# type name>, members... }.
//   Records: the auto-properties (positional + init) and the computed properties of a primitive /
//   enum / string type. Classes and structs: every public instance property and field. Delegates,
//   indexers and getters that throw are left out. StatusSet and LayerMap have custom shapes.
// - Plain enum → member name; [Flags] enum → integer. Dictionary → object, String(key) keys, in
//   enumeration order. Any other IEnumerable → array. Value tuples → objects with the tuple names
//   (from [TupleElementNames]) or Item1.., double → round-trip, float → its exact double value
//   (and its position is marked so the harness compares it through Math.fround).
// - Exceptions → { "$error": <type name>, "Message": <constructor message> }.
// - Per case, objects of the suite's interned types (CardDef, EnemyDef, LayerMap, ...) are written
//   once into the case's "defs" array and referenced as { "$ref": n } (structurally equal → same n).
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Oracle;

public sealed class MemberShape
{
    public string Name = "";
    public Type Type = typeof(object);
    public Func<object, object> Get = _ => null;
    public bool Float;
    public bool Computed;
    public NamedType Names;
}

public sealed class TypeShape
{
    public Type Type = typeof(object);
    public string Name = "";
    public bool Record;
    public string Custom; // "StatusSet" | "LayerMap" | null
    public List<MemberShape> Members = new();
    public bool Used;
}

/// <summary>Tuple element names attached to a declared type, by the pre-order rule of [TupleElementNames].</summary>
public sealed class NamedType
{
    public Type Type = typeof(object);
    public string[] Names; // for a ValueTuple
    public NamedType[] Args = Array.Empty<NamedType>();

    public static NamedType Build(Type t, IList<string> names)
    {
        int i = 0;
        return Build(t, names, ref i);
    }

    private static NamedType Build(Type t, IList<string> names, ref int i)
    {
        var nt = new NamedType { Type = t };
        if (Shapes.IsValueTuple(t))
        {
            int n = t.GetGenericArguments().Length;
            nt.Names = new string[n];
            for (int k = 0; k < n; k++) nt.Names[k] = i + k < names.Count ? names[i + k] : null;
            i += n;
        }
        if (t.IsArray) nt.Args = new[] { Build(t.GetElementType(), names, ref i) };
        else if (t.IsGenericType)
        {
            var args = t.GetGenericArguments();
            nt.Args = new NamedType[args.Length];
            for (int k = 0; k < args.Length; k++) nt.Args[k] = Build(args[k], names, ref i);
        }
        return nt;
    }
}

public static class Shapes
{
    private static readonly Dictionary<Type, TypeShape> Cache = new();
    private static readonly string[] Namespaces = { "BattleCore", "DungeonCore", "DungeonContent", "Depiction", "Exploration", "Journal", "Backdrop", "Oracle" };

    public static IEnumerable<TypeShape> All => Cache.Values;

    public static bool IsDomainType(Type t)
    {
        if (t.IsPrimitive || t.IsEnum || t == typeof(string) || t.IsArray || t.IsPointer || t.IsByRef) return false;
        string ns = t.Namespace ?? "";
        foreach (var n in Namespaces)
        {
            if (ns == n || ns.StartsWith(n + ".", StringComparison.Ordinal)) return true;
        }
        return false;
    }

    public static bool IsValueTuple(Type t) =>
        t.IsGenericType && t.IsValueType && (t.FullName ?? "").StartsWith("System.ValueTuple`", StringComparison.Ordinal);

    public static bool IsSimple(Type t)
    {
        var u = Nullable.GetUnderlyingType(t) ?? t;
        return u.IsPrimitive || u.IsEnum || u == typeof(string) || u == typeof(decimal);
    }

    public static bool ContainsFloat(Type t)
    {
        var u = Nullable.GetUnderlyingType(t) ?? t;
        if (u == typeof(float)) return true;
        if (u.IsArray) return ContainsFloat(u.GetElementType());
        if (u.IsGenericType && !IsDomainType(u)) return u.GetGenericArguments().Any(ContainsFloat);
        return false;
    }

    public static TypeShape Of(Type t)
    {
        if (Cache.TryGetValue(t, out var shape)) return shape;
        shape = new TypeShape { Type = t, Name = t.Name };
        Cache[t] = shape;
        shape.Record = t.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) != null;
        if (t.Name == "StatusSet" && t.Namespace == "BattleCore") shape.Custom = "StatusSet";
        if (t.Name == "LayerMap" && t.Namespace == "DungeonCore") shape.Custom = "LayerMap";

        // Base-first, declaration order.
        var chain = new List<Type>();
        for (var c = t; c != null && c != typeof(object) && c != typeof(ValueType); c = c.BaseType) chain.Insert(0, c);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in chain)
        {
            var members = c.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m is FieldInfo || m is PropertyInfo)
                .OrderBy(m => m.MetadataToken);
            foreach (var m in members)
            {
                if (seen.Contains(m.Name)) continue;
                if (m is FieldInfo f)
                {
                    if (typeof(Delegate).IsAssignableFrom(f.FieldType)) continue;
                    seen.Add(f.Name);
                    shape.Members.Add(new MemberShape
                    {
                        Name = f.Name, Type = f.FieldType, Get = o => f.GetValue(o), Float = ContainsFloat(f.FieldType),
                        Names = TupleNames(f.GetCustomAttribute<TupleElementNamesAttribute>(), f.FieldType),
                    });
                }
                else if (m is PropertyInfo p)
                {
                    if (p.GetIndexParameters().Length > 0 || p.GetMethod == null || !p.GetMethod.IsPublic) continue;
                    if (p.Name == "EqualityContract") continue;
                    if (typeof(Delegate).IsAssignableFrom(p.PropertyType)) continue;
                    bool auto = c.GetField($"<{p.Name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance) != null;
                    if (shape.Record && !auto && !IsSimple(p.PropertyType)) continue;
                    seen.Add(p.Name);
                    var getter = p.GetMethod;
                    shape.Members.Add(new MemberShape
                    {
                        Name = p.Name, Type = p.PropertyType, Get = o => getter.Invoke(o, null), Float = ContainsFloat(p.PropertyType),
                        Computed = !auto,
                        Names = TupleNames(p.GetCustomAttribute<TupleElementNamesAttribute>(), p.PropertyType),
                    });
                }
            }
        }

        if (shape.Custom == "StatusSet")
        {
            // { Stacks: { <kind>: n } } in Kinds order (the set's private dictionary, read through its public API).
            var kinds = t.GetProperty("Kinds");
            var stacks = t.GetMethod("Stacks");
            shape.Members = new List<MemberShape>
            {
                new MemberShape
                {
                    Name = "Stacks", Type = typeof(object), Computed = true,
                    Get = o =>
                    {
                        var d = new OrderedMap();
                        foreach (var k in (IEnumerable)kinds.GetValue(o)) d.Add(k, stacks.Invoke(o, new[] { k }));
                        return d;
                    },
                },
            };
        }
        if (shape.Custom == "LayerMap")
        {
            var nodes = t.GetProperty("Nodes");
            var succ = t.GetMethod("Successors");
            var fp = t.GetMethod("Fingerprint");
            shape.Members.Add(new MemberShape
            {
                Name = "Successors", Type = typeof(object), Computed = true,
                Get = o =>
                {
                    var d = new OrderedMap();
                    foreach (var n in (IEnumerable)nodes.GetValue(o))
                    {
                        int id = (int)n.GetType().GetProperty("Id").GetValue(n);
                        d.Add(id, succ.Invoke(o, new object[] { id }));
                    }
                    return d;
                },
            });
            shape.Members.Add(new MemberShape { Name = "Fingerprint", Type = typeof(string), Computed = true, Get = o => fp.Invoke(o, null) });
        }
        return shape;
    }

    private static NamedType TupleNames(TupleElementNamesAttribute attr, Type t) =>
        attr == null ? null : NamedType.Build(t, attr.TransformNames);
}

/// <summary>An ordered string-keyed map the suites use for outputs (keys kept in insertion order).</summary>
public sealed class OrderedMap : IEnumerable<KeyValuePair<object, object>>
{
    private readonly List<KeyValuePair<object, object>> _items = new();
    public void Add(object key, object value) => _items.Add(new KeyValuePair<object, object>(key, value));
    public object this[string key] { set => Add(key, value); }
    public IEnumerator<KeyValuePair<object, object>> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public int Count => _items.Count;
}

/// <summary>Writes one case: interning table and float positions are per case.</summary>
public sealed class CaseWriter
{
    private readonly HashSet<string> _intern;
    private readonly List<JsonNode> _defs = new();
    private readonly Dictionary<string, int> _defIndex = new(StringComparer.Ordinal);
    private readonly Dictionary<object, int> _byRef = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<string> _floats = new(StringComparer.Ordinal);
    private readonly List<string> _path = new();
    private int _floatMember;
    private int _depth;

    public CaseWriter(IEnumerable<string> internTypes)
    {
        _intern = new HashSet<string>(internTypes ?? Array.Empty<string>(), StringComparer.Ordinal);
    }

    public JsonObject Case(string id, object input, object output)
    {
        var o = new JsonObject { ["id"] = id };
        _path.Clear();
        _path.Add("input");
        o["input"] = Write(input);
        _path.Clear();
        _path.Add("output");
        o["output"] = Write(output);
        _path.Clear();
        if (_defs.Count > 0)
        {
            var defs = new JsonArray();
            foreach (var d in _defs) defs.Add(d);
            o["defs"] = defs;
        }
        if (_floats.Count > 0)
        {
            var fl = new JsonArray();
            foreach (var f in _floats.OrderBy(x => x, StringComparer.Ordinal)) fl.Add(f);
            o["floats"] = fl;
        }
        return o;
    }

    private string PathString() => string.Concat(_path);

    private readonly Dictionary<Type, bool> _internOf = new();

    /// <summary>Interned when the type's name, or a base type's name, is in the suite's list.</summary>
    private bool InternOf(Type t)
    {
        if (_internOf.TryGetValue(t, out bool known)) return known;
        bool yes = false;
        for (var c = t; c != null && c != typeof(object); c = c.BaseType)
        {
            if (_intern.Contains(c.Name)) yes = true;
        }
        _internOf[t] = yes;
        return yes;
    }

    public JsonNode Write(object v, NamedType names = null)
    {
        if (v == null) return null;
        if (++_depth > 80) throw new InvalidOperationException("Parity JSON nested too deep at " + PathString());
        try
        {
            return WriteInner(v, names);
        }
        finally
        {
            _depth--;
        }
    }

    private JsonNode WriteInner(object v, NamedType names)
    {
        var t = v.GetType();
        switch (v)
        {
            case string s: return JsonValue.Create(s);
            case bool b: return JsonValue.Create(b);
            case int i: return JsonValue.Create(i);
            case short sh: return JsonValue.Create((int)sh);
            case byte by: return JsonValue.Create((int)by);
            case sbyte sb: return JsonValue.Create((int)sb);
            case ushort us: return JsonValue.Create((int)us);
            case uint ui: return JsonValue.Create((long)ui);
            case char ch: return JsonValue.Create(ch.ToString());
            case long l: return Math.Abs((double)l) <= 9007199254740992.0 ? JsonValue.Create(l) : JsonValue.Create(l.ToString());
            case ulong ul: return ul <= 9007199254740992UL ? JsonValue.Create((long)ul) : JsonValue.Create(ul.ToString());
            case float f:
                if (_floatMember == 0) _floats.Add(PathString());
                return Number(f);
            case double d: return Number(d);
            case decimal m: return Number((double)m);
            case Enum e: return EnumValue(e, t);
            case Delegate: return null;
            case Exception ex: return Error(ex);
            case OrderedMap om: return Map(om, null);
        }

        if (Shapes.IsValueTuple(t)) return Tuple(v, t, names);

        if (Shapes.IsDomainType(t)) return Typed(v, t);

        if (v is IDictionary dict)
        {
            var o = new JsonObject();
            NamedType valueNames = names != null && names.Args.Length == 2 ? names.Args[1] : null;
            foreach (DictionaryEntry kv in dict) Put(o, KeyString(kv.Key), kv.Value, valueNames);
            return o;
        }
        var kvp = KeyValueInterface(t);
        if (kvp != null)
        {
            var o = new JsonObject();
            var keyProp = kvp.GetProperty("Key");
            var valProp = kvp.GetProperty("Value");
            NamedType valueNames = names != null && names.Args.Length == 2 ? names.Args[1] : null;
            foreach (var item in (IEnumerable)v) Put(o, KeyString(keyProp.GetValue(item)), valProp.GetValue(item), valueNames);
            return o;
        }
        if (v is IEnumerable seq)
        {
            var a = new JsonArray();
            NamedType elem = names != null && names.Args.Length >= 1 ? names.Args[0] : null;
            _path.Add("[*]");
            foreach (var item in seq) a.Add(Write(item, elem));
            _path.RemoveAt(_path.Count - 1);
            return a;
        }
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
        {
            return new JsonObject { ["Key"] = Write(t.GetProperty("Key").GetValue(v)), ["Value"] = Write(t.GetProperty("Value").GetValue(v)) };
        }
        return JsonValue.Create(v.ToString());
    }

    private static Type KeyValueInterface(Type t)
    {
        foreach (var i in t.GetInterfaces())
        {
            if (!i.IsGenericType || i.GetGenericTypeDefinition() != typeof(IEnumerable<>)) continue;
            var arg = i.GetGenericArguments()[0];
            if (arg.IsGenericType && arg.GetGenericTypeDefinition() == typeof(KeyValuePair<,>)) return arg;
        }
        return null;
    }

    private void Put(JsonObject o, string key, object value, NamedType names)
    {
        _path.Add("." + key);
        o[key] = Write(value, names);
        _path.RemoveAt(_path.Count - 1);
    }

    private JsonObject Map(OrderedMap map, NamedType names)
    {
        var o = new JsonObject();
        foreach (var kv in map) Put(o, KeyString(kv.Key), kv.Value, names);
        return o;
    }

    public static string KeyString(object key) => key switch
    {
        null => "",
        string s => s,
        Enum e => Enum.IsDefined(e.GetType(), e) && e.GetType().GetCustomAttribute<FlagsAttribute>() == null
            ? e.ToString()
            : Convert.ToInt64(e).ToString(System.Globalization.CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => key.ToString(),
    };

    private static JsonNode Number(double d)
    {
        if (double.IsNaN(d)) return JsonValue.Create("NaN");
        if (double.IsPositiveInfinity(d)) return JsonValue.Create("Infinity");
        if (double.IsNegativeInfinity(d)) return JsonValue.Create("-Infinity");
        return JsonValue.Create(d);
    }

    private static JsonNode EnumValue(Enum e, Type t)
    {
        if (t.GetCustomAttribute<FlagsAttribute>() != null) return JsonValue.Create(Convert.ToInt64(e));
        string name = Enum.GetName(t, e);
        return name != null ? JsonValue.Create(name) : JsonValue.Create(Convert.ToInt64(e));
    }

    public static JsonObject Error(Exception ex)
    {
        if (ex is TargetInvocationException tie && tie.InnerException != null) ex = tie.InnerException;
        return new JsonObject { ["$error"] = ex.GetType().Name, ["Message"] = BaseMessage(ex) };
    }

    /// <summary>The message the exception was constructed with: .NET's " (Parameter 'x')" and "Actual value was" suffixes are cut.</summary>
    public static string BaseMessage(Exception ex)
    {
        string msg = ex.Message;
        if (ex is ArgumentOutOfRangeException aoore && aoore.ActualValue != null)
        {
            int at = msg.LastIndexOf(Environment.NewLine + "Actual value was ", StringComparison.Ordinal);
            if (at >= 0) msg = msg.Substring(0, at);
        }
        if (ex is ArgumentException ae && !string.IsNullOrEmpty(ae.ParamName))
        {
            string suffix = $" (Parameter '{ae.ParamName}')";
            if (msg.EndsWith(suffix, StringComparison.Ordinal)) msg = msg.Substring(0, msg.Length - suffix.Length);
        }
        return msg;
    }

    private JsonNode Tuple(object v, Type t, NamedType names)
    {
        var o = new JsonObject();
        var args = t.GetGenericArguments();
        for (int k = 0; k < args.Length && k < 7; k++)
        {
            var field = t.GetField("Item" + (k + 1));
            string name = names?.Names != null && names.Names[k] != null ? names.Names[k] : "Item" + (k + 1);
            NamedType sub = names != null && names.Args.Length > k ? names.Args[k] : null;
            Put(o, name, field.GetValue(v), sub);
        }
        return o;
    }

    private JsonNode Typed(object v, Type t)
    {
        var shape = Shapes.Of(t);
        shape.Used = true;
        bool intern = InternOf(t);
        if (intern && _byRef.TryGetValue(v, out int known)) return new JsonObject { ["$ref"] = known };

        var o = new JsonObject { ["$type"] = shape.Name };
        foreach (var m in shape.Members)
        {
            object value;
            try
            {
                value = m.Get(v);
            }
            catch (TargetInvocationException)
            {
                continue; // a getter that throws is left out (the JS side does the same)
            }
            if (m.Float) _floatMember++;
            _path.Add("." + m.Name);
            try
            {
                o[m.Name] = Write(value, m.Names);
            }
            finally
            {
                _path.RemoveAt(_path.Count - 1);
                if (m.Float) _floatMember--;
            }
        }

        if (!intern) return o;
        string canon = o.ToJsonString(Json.Compact);
        if (!_defIndex.TryGetValue(canon, out int index))
        {
            index = _defs.Count;
            _defs.Add(o);
            _defIndex[canon] = index;
        }
        _byRef[v] = index;
        return new JsonObject { ["$ref"] = index };
    }
}

public static class Json
{
    public static readonly JsonSerializerOptions Compact = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };
}

/// <summary>Streams one suite file: a JSON array with one case per line.</summary>
public sealed class SuiteFile : IDisposable
{
    private readonly StreamWriter _w;
    private readonly string[] _intern;
    private bool _first = true;
    public int Count { get; private set; }
    public string Path { get; }

    public SuiteFile(string dir, string suite, params string[] internTypes)
    {
        Path = System.IO.Path.Combine(dir, suite + ".json");
        _w = new StreamWriter(Path, false, new System.Text.UTF8Encoding(false));
        _w.Write("[\n");
        _intern = internTypes;
    }

    public CaseWriter NewCase() => new CaseWriter(_intern);

    public void Add(string id, object input, Func<object> output)
    {
        var cw = NewCase();
        object outValue;
        try
        {
            outValue = output();
        }
        catch (Exception ex)
        {
            outValue = ex;
            if (Environment.GetEnvironmentVariable("ORACLE_TRACE") == "1") Console.Error.WriteLine($"[{id}] {ex}");
        }
        Add(cw.Case(id, input, outValue));
    }

    public void Add(JsonObject caseNode)
    {
        if (!_first) _w.Write(",\n");
        _first = false;
        _w.Write(caseNode.ToJsonString(Json.Compact));
        Count++;
    }

    public void Dispose()
    {
        _w.Write("\n]\n");
        _w.Dispose();
    }
}
