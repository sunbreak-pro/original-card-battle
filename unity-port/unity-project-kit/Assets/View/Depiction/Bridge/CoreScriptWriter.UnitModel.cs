// CoreScriptWriter (see CoreScriptWriter.cs): the numbers kept of each unit, and what is kept of each enemy.
using System.Collections.Generic;
using BattleCore;

namespace Depiction.Bridge
{
    public sealed partial class CoreScriptWriter
    {
        /// <summary>The numbers one unit shows, copied forward from events as they are read.</summary>
        private sealed class UnitModel
        {
            public int Hp;
            public int HpMax;
            public int Guard;
            public int Stamina;
            public int StaminaMax;
            public int Cell;
            public int Size;
            /// <summary>The unit's fall has been written (#288); the frames from its Defeat beat on show it gone.</summary>
            public bool Down;
            public readonly SortedDictionary<StatusKind, int> Statuses = new SortedDictionary<StatusKind, int>();

            /// <summary>§4 (v4.4): the name of each stance on the list of permanent effects, in the order they were put on; a repeat is listed again.</summary>
            public readonly List<string> Stances = new List<string>();

            // js: CoreScriptWriter.js:108
            public void CopyFrom(CombatantState unit, EnemyDef enemy)
            {
                Hp = unit.Hp;
                HpMax = unit.MaxHp;
                Guard = unit.Guard;
                Stamina = unit.Stamina;
                StaminaMax = unit.MaxStamina;
                Cell = unit.Cell;
                Size = unit.Size;
                Down = false;
                Statuses.Clear();
                foreach (StatusKind kind in unit.Statuses.Kinds) Statuses[kind] = unit.Statuses.Stacks(kind);
                Stances.Clear();
                foreach (StanceEntry entry in unit.StanceList) Stances.Add(StanceName(entry.Source, enemy));
            }
        }

        /// <summary>
        /// js: CoreScriptWriter.js:130 — what the writer keeps of one enemy. The one-enemy writer kept these
        /// once, as private fields, for the first enemy (_enemyDef, _enemy, _omen, _omenVisible,
        /// _omenPreview, _omenPreviewFor, _plan, _planVisible, _planChanged, _enemyReserveHeld, and
        /// _enemyFalls as FallPending); track 0 is exactly those.
        /// </summary>
        private sealed class EnemyTrack
        {
            public readonly EnemyDef Def;
            public readonly UnitModel Model = new UnitModel();
            public Omen Omen;
            public bool OmenVisible;

            /// <summary>
            /// The core's reading of the omen on screen (#248, TurnLoop.PreviewOmen), and the omen it was
            /// taken for. It is only taken on a state from the player's turn: a frame is read against the
            /// state a whole move left, and after an enemy phase that state is past the turn end the
            /// number already counts — even when the enemy chose the same action again. Until the omen
            /// shown is spent it keeps the number the player last saw; a newly decided omen starts over.
            /// </summary>
            public OmenPreview OmenPreview;
            public Omen OmenPreviewFor;

            /// <summary>#50: the 予定 (the second action an elite or a boss plans), whether it is on screen, and whether a 予定変更 has replaced it.</summary>
            public Omen Plan;
            public bool PlanVisible;
            public bool PlanChanged;

            /// <summary>The enemy's 構え, held back so it plays with its next omen and not inside the attack (see <see cref="Write"/>).</summary>
            public ReserveChecked ReserveHeld;

            /// <summary>The core said this enemy fell in the move being written; its fall is written as a beat of its own (#288).</summary>
            public bool FallPending;

            public EnemyTrack(EnemyDef def)
            {
                Def = def;
            }
        }
    }
}
