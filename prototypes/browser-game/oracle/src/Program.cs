// dotnet run -c Release --project oracle -- --out oracle/out [--only suite,suite] [--seeds N]
// Writes one JSON file per suite (docs/PARITY.md), plus shapes.json (the per-type members the
// serializer wrote, for the harness) and docs/PARITY_SHAPES.md's generated table.
using System.Diagnostics;
using System.Text;

namespace Oracle;

public static class Program
{
    public static readonly string[] SuiteNames = { "catalog", "battles", "previews", "texts", "scripts", "dungeon", "exploration", "backdrop" };

    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        string outDir = "out";
        string shapesDoc = null;
        var only = new HashSet<string>(StringComparer.Ordinal);
        int seeds = 5;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out": outDir = args[++i]; break;
                case "--only": foreach (var s in args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries)) only.Add(s); break;
                case "--seeds": seeds = int.Parse(args[++i]); break;
                case "--shapes-doc": shapesDoc = args[++i]; break;
                default:
                    Console.Error.WriteLine("usage: Oracle --out <dir> [--only catalog,battles,...] [--seeds N] [--shapes-doc <path>]");
                    return 2;
            }
        }
        Directory.CreateDirectory(outDir);

        var summary = new List<string>();
        foreach (var suite in SuiteNames)
        {
            if (only.Count > 0 && !only.Contains(suite)) continue;
            var clock = Stopwatch.StartNew();
            int count = suite switch
            {
                "catalog" => CatalogSuite.Run(outDir),
                "battles" => BattlesSuite.Run(outDir, seeds),
                "previews" => PreviewsSuite.Run(outDir),
                "texts" => TextsSuite.Run(outDir),
                "scripts" => ScriptsSuite.Run(outDir),
                "dungeon" => DungeonSuite.Run(outDir),
                "exploration" => ExplorationSuite.Run(outDir),
                "backdrop" => BackdropSuite.Run(outDir),
                _ => 0,
            };
            long bytes = new FileInfo(Path.Combine(outDir, suite + ".json")).Length;
            string line = $"{suite}: {count} cases, {bytes / 1024.0 / 1024.0:0.00} MB, {clock.Elapsed.TotalSeconds:0.0} s";
            summary.Add(line);
            Console.Error.WriteLine(line);
        }

        ShapesOutput.Write(outDir, shapesDoc);
        File.WriteAllText(Path.Combine(outDir, "summary.txt"), string.Join("\n", summary) + "\n", new UTF8Encoding(false));
        return 0;
    }
}
