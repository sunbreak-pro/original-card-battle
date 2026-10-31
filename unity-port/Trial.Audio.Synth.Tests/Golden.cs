// The golden data written by unity-port/tools/audio-golden.mjs from the JS sources (Audio.js + the design system's
// bundle.js under a recording WebAudio mock at 48 kHz), and the helpers that compare a C# call log with it.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using Trial.Audio.Synth;

namespace Trial.Audio.Synth.Tests
{
    internal static class Golden
    {
        private static JsonDocument doc;
        private static readonly object Lock = new object();

        public static JsonElement Root
        {
            get
            {
                lock (Lock)
                {
                    if (doc == null) doc = JsonDocument.Parse(File.ReadAllText(FindPath()));
                    return doc.RootElement;
                }
            }
        }

        private static string FindPath()
        {
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8 && dir != null; i++)
            {
                string p = Path.Combine(dir, "golden", "audio-golden.json");
                if (File.Exists(p)) return p;
                p = Path.Combine(dir, "Trial.Audio.Synth.Tests", "golden", "audio-golden.json");
                if (File.Exists(p)) return p;
                dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
            }
            throw new FileNotFoundException("golden/audio-golden.json not found above " + AppContext.BaseDirectory);
        }

        public static IEnumerable<JsonElement> Array(string name) => Root.GetProperty(name).EnumerateArray();

        public static JsonElement Preset(string name) => Array("presets").First(p => p.GetProperty("name").GetString() == name);

        // ---- shared fixtures (48 kHz) --------------------------------------------------------------------------------

        private static NoiseBank bank;
        private static AudioBuffer dsNoise;

        public static NoiseBank Bank
        {
            get { lock (Lock) return bank ??= new NoiseBank(48000); }
        }

        public static AudioBuffer DsNoise
        {
            get { lock (Lock) return dsNoise ??= AudioBuffers.MakeDsNoise(48000); }
        }

        // ---- log comparison -------------------------------------------------------------------------------------------

        public static string Show(object[] e) => "[" + string.Join(",", e.Select(ShowValue)) + "]";

        private static string ShowValue(object x) => x switch
        {
            null => "null",
            string s => "\"" + s + "\"",
            bool b => b ? "true" : "false",
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            _ => x.ToString(),
        };

        public static string Show(JsonElement e) => e.GetRawText();

        private static bool Same(JsonElement g, object a)
        {
            switch (g.ValueKind)
            {
                case JsonValueKind.Number:
                    if (!(a is double d)) return false;
                    double gv = g.GetDouble();
                    return Math.Abs(gv - d) <= 1.01e-6 + 1e-12 * Math.Abs(gv);
                case JsonValueKind.String: return a is string s && s == g.GetString();
                case JsonValueKind.True: return a is bool b1 && b1;
                case JsonValueKind.False: return a is bool b2 && !b2;
                case JsonValueKind.Null: return a == null;
                default: return false;
            }
        }

        /// <summary>Asserts the C# log equals the golden one entry by entry (numbers within one rounding unit, 1e-6).</summary>
        public static void AssertLog(JsonElement golden, IReadOnlyList<object[]> actual, string label)
        {
            JsonElement[] g = golden.EnumerateArray().ToArray();
            int n = Math.Min(g.Length, actual.Count);
            for (int i = 0; i < n; i++)
            {
                JsonElement[] ge = g[i].EnumerateArray().ToArray();
                object[] ae = actual[i];
                bool ok = ge.Length == ae.Length;
                for (int k = 0; ok && k < ge.Length; k++) ok = Same(ge[k], ae[k]);
                if (!ok) Assert.Fail(Context(label, i, g, actual));
            }
            if (g.Length != actual.Count) Assert.Fail(label + $": golden has {g.Length} entries, C# {actual.Count}\n" + Context(label, n, g, actual));
        }

        private static string Context(string label, int at, JsonElement[] g, IReadOnlyList<object[]> a)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{label}: first difference at entry {at}");
            for (int i = Math.Max(0, at - 3); i < Math.Min(Math.Max(g.Length, a.Count), at + 4); i++)
            {
                string gs = i < g.Length ? Show(g[i]) : "—";
                string cs = i < a.Count ? Show(a[i]) : "—";
                sb.AppendLine($"  {(i == at ? ">" : " ")}{i,4}  js {gs}");
                sb.AppendLine($"        c# {cs}");
            }
            return sb.ToString();
        }

        /// <summary>Renames node ids in order of their "new" entries (n0, n1, …, with the kind kept) so two setup logs compare.</summary>
        public static List<object[]> Canonical(IEnumerable<object[]> log)
        {
            var map = new Dictionary<string, string>();
            var output = new List<object[]>();
            string Name(string id)
            {
                if (map.TryGetValue(id, out string n)) return n;
                return id;
            }
            foreach (object[] e in log)
            {
                var c = (object[])e.Clone();
                string id = (string)c[0];
                if (c.Length == 2 && (string)c[1] == "new" && !map.ContainsKey(id)) map[id] = "n" + map.Count;
                c[0] = Name(id);
                if (c.Length >= 3 && (string)c[1] == "connect")
                {
                    string t = (string)c[2];
                    int dot = t.IndexOf('.');
                    c[2] = dot < 0 ? Name(t) : Name(t.Substring(0, dot)) + t.Substring(dot);
                }
                output.Add(c);
            }
            return output;
        }

        public static List<object[]> ToObjects(JsonElement log) =>
            log.EnumerateArray().Select(e => e.EnumerateArray().Select(ToObject).ToArray()).ToList();

        public static object ToObject(JsonElement v) => v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDouble(),
            JsonValueKind.String => v.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }
}
