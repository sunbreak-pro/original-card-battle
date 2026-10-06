using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace BattleCore.Sim;

/// <summary>
/// #53: dotnet run --project BattleCore.Sim -- [--seed N] [--runs N] [--json path] [--only first3] [--threads N] [--max-plays N].
/// Prints the §13 table to stdout; the run time goes to stderr so that stdout and the JSON depend on
/// the seed alone.
/// </summary>
public static class Program
{
    public const string Usage =
        "usage: BattleCore.Sim [--seed N] [--runs N] [--json <path>] [--only first3] [--threads N] [--max-plays N] [--trace deck:enemy[:policy]] [-h]";

    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (!TryParse(args, out var options, out string? jsonPath, out string? trace, out bool help, out string? error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(Usage);
            return 2;
        }
        if (help)
        {
            Console.WriteLine(Usage);
            return 0;
        }

        if (trace != null) return Trace(trace, options);

        // The JSON's folder is made before the battles, so a path that cannot be written stops in a
        // moment rather than after the whole run.
        if (jsonPath != null && !TryPrepareJsonPath(jsonPath, out jsonPath, out error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(Usage);
            return 2;
        }

        var clock = Stopwatch.StartNew();
        var results = Bench.Run(options);
        var rows = Criteria.Compute(results);
        var first = Criteria.ComputeFirstThree(results.Main, Decks.Archetypes, Decks.CostOneOnly().Id, CardCatalog.All, Enemies.All);
        clock.Stop();

        Console.Write(Report.Markdown(results, rows, first));
        if (jsonPath != null)
        {
            try
            {
                File.WriteAllText(jsonPath, Report.Json(results, rows, first), new UTF8Encoding(false));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"cannot write --json \"{jsonPath}\": {e.Message}");
                return 1;
            }
        }
        int battles = results.Main.Count + results.Policies.Values.Sum(p => p.Count) + results.LowStamina.Count + results.Pairs.Count;
        Console.Error.WriteLine($"{battles} battles (pairs counted once) in {clock.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s");
        return 0;
    }

    /// <summary>
    /// Resolves --json's path and makes its folder. False (with a one-line error) for a path that is
    /// not a path, names a folder, or sits where no folder can be made.
    /// </summary>
    public static bool TryPrepareJsonPath(string path, out string fullPath, out string? error)
    {
        fullPath = path;
        error = null;
        try
        {
            fullPath = Path.GetFullPath(path);
            if (Directory.Exists(fullPath))
            {
                error = $"--json \"{path}\" is a folder; give a file path";
                return false;
            }
            string? dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            error = $"cannot write --json \"{path}\": {e.Message}";
            return false;
        }
    }

    /// <summary>
    /// --trace deck:enemy[:policy]: one battle (run 0 of the main seeds) written turn by turn — the
    /// hand, the omen, each play and its value. For reading what the greedy player does.
    /// </summary>
    private static int Trace(string spec, BenchOptions options)
    {
        if (!TryParseTrace(spec, out var deck, out var enemy, out var policy, out string? error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(Usage);
            return 2;
        }
        var (record, _) = Runner.Fight(deck!, Runner.Setup(deck!, enemy!), new GreedyPlayer(policy, options.MaxPlays),
            Runner.SeedFor(options.Seed, "main", enemy!.Id, 0), Console.WriteLine);
        Console.WriteLine($"{record.Outcome} in {record.Turns} turns, plays {string.Join(",", record.PlaysPerTurn)}, max blow {record.MaxBlow}, hp left {record.HpLeft}");
        return 0;
    }

    /// <summary>
    /// Reads --trace's deck:enemy[:policy]. A deck, enemy or policy that is not known is an error
    /// (exit 2 with the usage, like any other bad argument); the policy is one whole
    /// <see cref="Positioning"/> name, any case (not a number, a padded name or a comma list).
    /// </summary>
    public static bool TryParseTrace(string spec, out SimDeck? deck, out EnemyDef? enemy, out Positioning policy, out string? error)
    {
        deck = null;
        enemy = null;
        policy = Positioning.Neutral;
        error = null;
        string[] parts = spec.Split(':');
        var decks = Bench.MainDecks();
        if (parts.Length < 2 || parts.Length > 3)
        {
            error = "--trace needs deck:enemy[:policy]";
            return false;
        }
        deck = decks.FirstOrDefault(d => d.Id == parts[0]);
        if (deck == null)
        {
            error = $"unknown deck \"{parts[0]}\"; decks are " + string.Join(", ", decks.Select(d => d.Id));
            return false;
        }
        enemy = Enemies.All.FirstOrDefault(e => e.Id == parts[1]);
        if (enemy == null)
        {
            error = $"unknown enemy \"{parts[1]}\"; enemies are " + string.Join(", ", Enemies.All.Select(e => e.Id));
            return false;
        }
        if (parts.Length == 3)
        {
            // Whole names only: Enum.TryParse would also take a number, padding, or a comma list
            // ("close,away" is Close | Away = InOut).
            string? name = Enum.GetNames<Positioning>().FirstOrDefault(n => string.Equals(n, parts[2], StringComparison.OrdinalIgnoreCase));
            if (name == null)
            {
                error = $"unknown policy \"{parts[2]}\"; policies are " + string.Join(", ", Enum.GetNames<Positioning>());
                return false;
            }
            policy = Enum.Parse<Positioning>(name);
        }
        return true;
    }

    /// <summary>
    /// Reads the command line. -h / --help is not an error: it returns true with <paramref name="help"/>
    /// set, and Main prints the usage to stdout and exits 0.
    /// </summary>
    public static bool TryParse(IReadOnlyList<string> args, out BenchOptions options, out string? jsonPath, out string? trace, out bool help, out string? error)
    {
        int seed = BenchOptions.DefaultSeed, runs = BenchOptions.DefaultRuns, threads = 0, maxPlays = 0;
        bool onlyFirstThree = false;
        jsonPath = null;
        trace = null;
        help = false;
        error = null;
        options = new BenchOptions();
        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            string? Next() => i + 1 < args.Count ? args[++i] : null;
            switch (arg)
            {
                case "--seed":
                    if (!int.TryParse(Next(), NumberStyles.Integer, CultureInfo.InvariantCulture, out seed)) { error = "--seed needs an integer"; return false; }
                    break;
                case "--runs":
                    if (!int.TryParse(Next(), NumberStyles.Integer, CultureInfo.InvariantCulture, out runs) || runs < 1) { error = "--runs needs a positive integer"; return false; }
                    break;
                case "--threads":
                    if (!int.TryParse(Next(), NumberStyles.Integer, CultureInfo.InvariantCulture, out threads) || threads < 0) { error = "--threads needs 0 or more"; return false; }
                    break;
                case "--max-plays":
                    if (!int.TryParse(Next(), NumberStyles.Integer, CultureInfo.InvariantCulture, out maxPlays) || maxPlays < 1) { error = "--max-plays needs a positive integer"; return false; }
                    break;
                case "--json":
                    jsonPath = Next();
                    if (jsonPath == null) { error = "--json needs a path"; return false; }
                    break;
                case "--only":
                    string? what = Next();
                    if (what != "first3") { error = "--only takes first3"; return false; }
                    onlyFirstThree = true;
                    break;
                case "--trace":
                    trace = Next();
                    if (trace == null) { error = "--trace needs deck:enemy[:policy]"; return false; }
                    break;
                case "-h":
                case "--help":
                    help = true;
                    return true;
                default:
                    error = $"unknown argument \"{arg}\"";
                    return false;
            }
        }
        options = new BenchOptions(seed, runs, onlyFirstThree, threads, maxPlays);
        return true;
    }
}
