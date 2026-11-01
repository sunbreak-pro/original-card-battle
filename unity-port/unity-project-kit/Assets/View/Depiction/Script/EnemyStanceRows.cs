// The rows of the enemy side's stance list when a battle has several enemies (battle-visual-v1 §4.6,
// battle_ui_ux_v2 §3.7, #589). §4.6 gives the enemy side one place: 24 px from the right edge, at most
// three frames stacked up from y 470, the older ones folded into "+n", and a swatch at the left end of
// each frame "when there are two or more enemies". A second column would stand on the figures (the line
// runs to x 1620 and the first column starts at x 1646), so every standing enemy's stances share the one
// list, as the trial's hud does. Each stance keeps its own frame, also when two enemies hold the same name.
//
// The script gives each enemy's list in the order put down, but no order across enemies, so this keeps
// one: a stance new to the list, or put down again (its count rose), goes to the newest place (§3.7), and
// the others keep theirs. Pure C#, so the EditMode tests hold it.
using System;
using System.Collections.Generic;

namespace Depiction
{
    public sealed class EnemyStanceRows
    {
        private sealed class Stamp
        {
            public int Order;
            public int Count;
        }

        private readonly Dictionary<string, Stamp> _stamps = new Dictionary<string, Stamp>();
        private int _next;

        /// <summary>A new battle: nothing has been put down yet.</summary>
        public void Reset()
        {
            _stamps.Clear();
            _next = 0;
        }

        /// <summary>
        /// The list of <paramref name="enemies"/>, the newest last: every standing enemy's stances (§2.2 の
        /// 6: a fallen enemy's go with it). Once none stands, the list stays as they stood.
        /// </summary>
        public List<StanceRow> Of(IReadOnlyList<EnemyFrame> enemies)
        {
            if (enemies == null) throw new ArgumentNullException(nameof(enemies));
            bool anyStands = false;
            foreach (EnemyFrame enemy in enemies)
            {
                if (!enemy.Unit.Down) anyStands = true;
            }
            var seen = new HashSet<string>();
            var listed = new List<(int Order, int Listed, StanceRow Row)>();
            for (int k = 0; k < enemies.Count; k++)
            {
                UnitFrame unit = enemies[k].Unit;
                if (anyStands && unit.Down) continue;
                foreach (StanceRow row in unit.Stances)
                {
                    string id = k + ":" + row.Name;
                    seen.Add(id);
                    if (!_stamps.TryGetValue(id, out Stamp stamp))
                    {
                        stamp = new Stamp { Order = _next++, Count = row.Count };
                        _stamps[id] = stamp;
                    }
                    else if (row.Count > stamp.Count)
                    {
                        stamp.Order = _next++;
                        stamp.Count = row.Count;
                    }
                    listed.Add((stamp.Order, listed.Count, row));
                }
            }
            var gone = new List<string>();
            foreach (string id in _stamps.Keys)
            {
                if (!seen.Contains(id)) gone.Add(id);
            }
            foreach (string id in gone) _stamps.Remove(id);
            listed.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Listed.CompareTo(b.Listed));
            var rows = new List<StanceRow>(listed.Count);
            foreach ((int Order, int Listed, StanceRow Row) entry in listed) rows.Add(entry.Row);
            return rows;
        }
    }
}
