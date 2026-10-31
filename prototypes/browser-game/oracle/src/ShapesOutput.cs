// shapes.json (machine-readable, read by the harness) and the generated part of docs/PARITY_SHAPES.md.
using System.Text;
using System.Text.Json.Nodes;

namespace Oracle;

public static class ShapesOutput
{
    public const string BeginMark = "<!-- BEGIN GENERATED SHAPES -->";
    public const string EndMark = "<!-- END GENERATED SHAPES -->";

    public static void Write(string outDir, string docPath)
    {
        string path = Path.Combine(outDir, "shapes.json");
        // Merge with what an earlier run (--only) wrote, so the file always covers every suite on disk.
        var root = File.Exists(path) ? (JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? new JsonObject()) : new JsonObject();
        var names = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var shape in Shapes.All.Where(s => s.Used).OrderBy(s => s.Name, StringComparer.Ordinal))
        {
            if (names.TryGetValue(shape.Name, out var other) && other != shape.Type)
            {
                Console.Error.WriteLine($"warning: two serialized types share the name {shape.Name}: {other.FullName} and {shape.Type.FullName}");
            }
            names[shape.Name] = shape.Type;
            var members = new JsonArray();
            var computed = new JsonArray();
            var floats = new JsonArray();
            foreach (var m in shape.Members)
            {
                members.Add(m.Name);
                if (m.Computed) computed.Add(m.Name);
                if (m.Float) floats.Add(m.Name);
            }
            root[shape.Name] = new JsonObject
            {
                ["namespace"] = shape.Type.Namespace,
                ["kind"] = shape.Record ? "record" : shape.Type.IsValueType ? "struct" : "class",
                ["custom"] = shape.Custom,
                ["members"] = members,
                ["computed"] = computed,
                ["floats"] = floats,
            };
        }
        var sorted = new JsonObject();
        foreach (var kv in root.OrderBy(k => k.Key, StringComparer.Ordinal)) sorted[kv.Key] = kv.Value?.DeepClone();
        File.WriteAllText(path, sorted.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
        }) + "\n", new UTF8Encoding(false));

        if (docPath == null || !File.Exists(docPath)) return;
        string doc = File.ReadAllText(docPath);
        int a = doc.IndexOf(BeginMark, StringComparison.Ordinal);
        int b = doc.IndexOf(EndMark, StringComparison.Ordinal);
        if (a < 0 || b < a) return;
        var sb = new StringBuilder();
        sb.Append(BeginMark).Append('\n');
        sb.Append("\n| Type | Kind | Members (in order) | Computed (getters the JS side must expose) | Float members |\n");
        sb.Append("| --- | --- | --- | --- | --- |\n");
        foreach (var kv in sorted)
        {
            var o = (JsonObject)kv.Value;
            string kind = (string)o["kind"] + (o["custom"] != null ? $" (custom: {(string)o["custom"]})" : "");
            string Join(string key) => string.Join(", ", ((JsonArray)o[key]).Select(x => (string)x));
            sb.Append($"| `{kv.Key}` ({(string)o["namespace"]}) | {kind} | {Join("members")} | {Join("computed")} | {Join("floats")} |\n");
        }
        sb.Append('\n');
        doc = doc.Substring(0, a) + sb + doc.Substring(b);
        File.WriteAllText(docPath, doc, new UTF8Encoding(false));
    }
}
