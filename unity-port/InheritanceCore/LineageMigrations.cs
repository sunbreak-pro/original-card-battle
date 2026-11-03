#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

// The lineage save's version policy (#109, decided 2026-11-03; dungeon_exploration_v4.md §7.9). The lineage is the
// record of every generation: the traces, the survivor, the lives and their names, the player's knowledge. A save that
// stops loading loses all of it, and no later life can earn it back, so a change of its shape converts the old save
// instead of setting it aside:
//
// - A save of an older version goes through one step per version, in order, up to Inheritance.SaveVersion.
// - A save of a newer version (written by a later build) is refused. Nothing converts it backwards; the store keeps the
//   text aside under its own key until the player starts anew (Trial.Bridge.Save.OpenSave), so the later build reads it
//   again.
// - A version with no step to the next one is refused the same way: a step that was never written is a bug, and
//   guessing would write a wrong lineage over the right one.
//
// The life in progress (its start and its log of operations) is not converted: a replay is only the same life on the
// rules of the build that recorded it. Trial.Bridge refuses a start of another version and goes the way of a life that
// does not replay (the player gives it up and the lineage goes back to how it stood before that departure, read
// through these steps).
//
// To change the shape: raise Inheritance.SaveVersion to n + 1, add Step(n, …) to Steps that turns a version-n save into
// version n + 1, and keep a version-n save in the tests that must still read.
namespace InheritanceCore
{
    /// <summary>One step of the lineage save: a save of version <see cref="From"/> becomes version From + 1.</summary>
    public sealed class LineageMigration
    {
        public LineageMigration(int from, Func<JsonObject, JsonObject> upgrade)
        {
            if (from < 1) throw new ArgumentOutOfRangeException(nameof(from), from, "a save version starts at 1");
            From = from;
            Upgrade = upgrade ?? throw new ArgumentNullException(nameof(upgrade));
        }

        /// <summary>The version this step reads.</summary>
        public int From { get; }

        /// <summary>Turns a version-From save (a copy it may change) into the save of version From + 1.</summary>
        public Func<JsonObject, JsonObject> Upgrade { get; }
    }

    /// <summary>The steps that bring an older lineage save up to <see cref="Inheritance.SaveVersion"/>.</summary>
    public static class LineageMigrations
    {
        /// <summary>The steps this build knows, one per version below Inheritance.SaveVersion. None while it is 1.</summary>
        public static IReadOnlyList<LineageMigration> Steps { get; } = Array.Empty<LineageMigration>();

        /// <summary>
        /// The saved lineage <paramref name="data"/> as a save of version <paramref name="target"/>: the same object when
        /// it is that version already, else a converted copy (the input is never changed). Throws
        /// InheritanceRuleException("bad save: …") on a version that is not an integer of 1 or more, one newer than
        /// <paramref name="target"/>, or one with no step to the next.
        /// </summary>
        public static JsonObject Upgrade(JsonObject data, int target, IReadOnlyList<LineageMigration> steps)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (steps == null) throw new ArgumentNullException(nameof(steps));
            int version = VersionOf(data);
            if (version > target)
                throw new InheritanceRuleException(Text($"bad save: version {version} is newer than this build reads ({target})"));
            if (version == target) return data;

            var byFrom = new Dictionary<int, LineageMigration>();
            foreach (var step in steps)
            {
                if (byFrom.ContainsKey(step.From)) throw new ArgumentException(Text($"two steps read version {step.From}"), nameof(steps));
                byFrom[step.From] = step;
            }
            var save = Copy(data);
            while (version < target)
            {
                if (!byFrom.TryGetValue(version, out var step))
                    throw new InheritanceRuleException(Text($"bad save: version {version} has no step to version {version + 1}"));
                save = step.Upgrade(save) ?? throw new InheritanceRuleException(Text($"bad save: the step from version {version} gave nothing"));
                int next = VersionOf(save);
                if (next != version + 1)
                    throw new InheritanceRuleException(Text($"bad save: the step from version {version} gave version {next}"));
                version = next;
            }
            return save;
        }

        /// <summary><see cref="Upgrade(JsonObject,int,IReadOnlyList{LineageMigration})"/> with this build's version and steps.</summary>
        public static JsonObject Upgrade(JsonObject data) => Upgrade(data, Inheritance.SaveVersion, Steps);

        /// <summary>A save's Version: an integer of 1 or more, or InheritanceRuleException("bad save: version …").</summary>
        private static int VersionOf(JsonObject data)
        {
            var version = Js.Get(data, "Version");
            if (!Json.IsInteger(version) || Json.ToDouble(version) < 1 || Json.ToDouble(version) > int.MaxValue)
                throw new InheritanceRuleException($"bad save: version {Js.Display(version)}");
            return (int)Json.ToDouble(version);
        }

        private static JsonObject Copy(JsonObject data) => (JsonObject)Json.Parse(Json.Stringify(data))!;

        private static string Text(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
    }
}
