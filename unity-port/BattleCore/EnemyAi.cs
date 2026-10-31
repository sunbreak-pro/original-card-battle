using System;
using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>
    /// §6 / §6.1 and roster §1.2: how an enemy picks its next action and what it shows of it.
    ///
    /// There is no randomness here. The tree is walked top-down and the first action that can be
    /// paid for wins, so the same board always gives the same omen.
    /// </summary>
    public static class EnemyAi
    {
        /// <summary>The action id an omen carries when nothing in the branch can be paid for.</summary>
        public const string RestActionId = "rest";

        public static readonly OmenLabel RestLabel = new OmenLabel(OmenKind.Rest);

        /// <summary>
        /// §6.1: every enemy branches three ways on the gap band at the moment the omen is decided.
        /// <paramref name="active"/> is the boss's swapped tree (#50): a branch it names replaces the
        /// base one, a band it leaves null keeps the base branch.
        /// </summary>
        public static IReadOnlyList<string> BranchFor(EnemyDef def, int gap, TreeSwitch? active = null)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var band = gap.ToBand();
            return active?.Branch(band) ?? def.Branch(band);
        }

        /// <summary>
        /// #50 the hook: the switch that holds now, or null for the base tree. Of the switches that
        /// hold the last in the enemy's list wins (a later stage overrides an earlier one), and one
        /// that stops holding lets the base tree back, so this is asked at every omen decision.
        /// </summary>
        public static TreeSwitch? ActiveSwitch(EnemyDef def, AdaptationView view)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (view == null) throw new ArgumentNullException(nameof(view));
            TreeSwitch? found = null;
            if (def.Switches == null) return null;
            foreach (var each in def.Switches)
            {
                if (each.Holds(view)) found = each;
            }
            return found;
        }

        /// <summary>The switch of this id, or null (also for a null id).</summary>
        public static TreeSwitch? SwitchById(EnemyDef def, string? id)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (id == null || def.Switches == null) return null;
            foreach (var each in def.Switches)
            {
                if (string.Equals(each.Id, id, StringComparison.Ordinal)) return each;
            }
            return null;
        }

        /// <summary>
        /// roster §1.2: walk the branch from the top and take the first action whose cost fits in
        /// <paramref name="stamina"/>. Null when none does — the enemy rests.
        ///
        /// The caller decides which stamina to pass. The turn loop passes what the enemy will hold
        /// when the omen is carried out (after its recovery), so an omen only turns into a rest when
        /// something drained the enemy in between.
        ///
        /// <paramref name="skip"/> are actions it may not take now (#189): stances it has used —
        /// a stance action is used once a battle and then leaves the tree (roster §1.2) — and, for
        /// the second action of an elite or a boss, the first one (roster §1.3).
        /// </summary>
        public static EnemyActionDef? ChooseAction(
            EnemyDef def, int gap, int stamina, IReadOnlyCollection<string>? skip = null, TreeSwitch? active = null)
        {
            foreach (var actionId in BranchFor(def, gap, active))
            {
                if (skip != null && Contains(skip, actionId)) continue;
                var action = ActionOf(def, actionId, active);
                if (Combat.CanPay(action.Cost, stamina)) return action;
            }
            return null;
        }

        /// <summary>
        /// §9 step 12: the stamina an enemy will hold when its omen is carried out — now, plus its
        /// next recovery. 疲労 it already holds will take its 1 off that recovery too (§9 step 9); one
        /// put on it later is what "drained in between" means. The turn loop passes this to
        /// <see cref="DecideOmen"/> and <see cref="DecidePlan"/>; it is public so a test that walks
        /// the trees (#395) reads the omen's stamina the same way.
        /// </summary>
        public static int StaminaAtAction(EnemyDef def, CombatantState body)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (body == null) throw new ArgumentNullException(nameof(body));
            int fatigue = body.Statuses.Has(StatusKind.Fatigue) ? Constants.FatiguePenalty : 0;
            return Combat.RecoverStamina(body.Stamina, body.MaxStamina, def.Recovery - fatigue, body.NextTurnRecoveryBonus);
        }

        /// <summary>
        /// #51: the enemy's action of this id as it stands under the active switch — the switch's
        /// override when it carries one (roster §4.4 breaker: 錫杖 with 崩し 2), else the enemy's own.
        /// </summary>
        public static EnemyActionDef ActionOf(EnemyDef def, string actionId, TreeSwitch? active = null)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            return active?.Override(actionId) ?? def.Actions[actionId];
        }

        /// <summary>
        /// #51 (roster §4.3): the actions the tree passes over because the player holds the word the
        /// action waits out (<see cref="EnemyActionDef.NotWhileFoeHas"/>: 縛りの言葉 while 呪縛 remains).
        /// <paramref name="beforeFoeTurn"/> is true for an omen decided at step 12, which is taken
        /// after the player's next turn start: a ターンで減る型 word loses a stack there, so it counts
        /// as remaining only if a stack is left after that tick. False for the second action of a
        /// phase, read with nothing in between.
        ///
        /// #497 (roster v4.18 §4.3): also the actions that would put nothing more on the player
        /// (<see cref="EnemyActionDef.NotAtFoeCap"/>: 瘴気の儀 once 瘴気纏い is at its cap), read
        /// off the action as it stands under <paramref name="active"/> (the second stage's cap of 2).
        /// <paramref name="warded"/> is what 竜語の符 has kept off (§9.1, #452): the word counts as it
        /// would sit without the ward, so the ward lowers the word and leaves the choice as it is.
        /// </summary>
        public static IReadOnlyList<string> Barred(
            EnemyDef def, StatusSet foe, bool beforeFoeTurn, TreeSwitch? active = null, StatusSet? warded = null)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (foe == null) throw new ArgumentNullException(nameof(foe));
            var barred = new List<string>();
            foreach (var own in def.Actions.Values)
            {
                var action = ActionOf(def, own.Id, active);
                if (action.NotWhileFoeHas.HasValue && Left(foe, action.NotWhileFoeHas.Value, beforeFoeTurn) > 0)
                {
                    barred.Add(action.Id);
                }
                else if (action.NotAtFoeCap && AtFoeCap(action, foe, beforeFoeTurn, warded ?? StatusSet.Empty))
                {
                    barred.Add(action.Id);
                }
            }
            return barred;
        }

        /// <summary>The stacks of <paramref name="word"/> the player holds when the action is taken (see <see cref="Barred"/>).</summary>
        private static int Left(StatusSet foe, StatusKind word, bool beforeFoeTurn) =>
            foe.Stacks(word) - (beforeFoeTurn && Statuses.DecayOf(word) == StatusDecay.OnTurn ? 1 : 0);

        /// <summary>
        /// #497: every capped word the action puts on the player is held at the cap. An action with
        /// no capped grant on the player is never at it.
        /// </summary>
        private static bool AtFoeCap(EnemyActionDef action, StatusSet foe, bool beforeFoeTurn, StatusSet warded)
        {
            bool capped = false;
            foreach (var grant in action.Face.StatusList)
            {
                if (grant.OnSelf || grant.Cap <= 0) continue;
                capped = true;
                if (Left(foe, grant.Kind, beforeFoeTurn) + warded.Stacks(grant.Kind) < grant.Cap) return false;
            }
            return capped;
        }

        /// <summary>
        /// #51 (roster §6.3 伸びる根): the face an action resolves with at gap <paramref name="gap"/>.
        /// An action with <see cref="EnemyActionDef.PullBeyond"/> pushes |Push| cells at that gap or
        /// nearer and pulls |Push| cells beyond it; every other action's face is its own.
        /// </summary>
        public static Face FaceAt(EnemyActionDef action, int gap)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (!action.PullBeyond.HasValue || action.Face.Push == 0) return action.Face;
            int cells = Math.Abs(action.Face.Push);
            return action.Face with { Push = gap <= action.PullBeyond.Value ? cells : -cells };
        }

        private static bool Contains(IReadOnlyCollection<string> ids, string id)
        {
            foreach (var each in ids)
            {
                if (string.Equals(each, id, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>
        /// §6: the omen for the next action — 種別 + 狙うマス. An elite or a boss shows a second step
        /// beside it, <see cref="DecidePlan"/>.
        /// </summary>
        public static Omen DecideOmen(
            EnemyDef def, int gap, int stamina, IReadOnlyCollection<string>? skip = null, TreeSwitch? active = null)
        {
            var action = ChooseAction(def, gap, stamina, skip, active);
            return OmenOf(action);
        }

        /// <summary>The omen an action carries, or the rest omen for none.</summary>
        public static Omen OmenOf(EnemyActionDef? action) =>
            action == null ? new Omen(RestActionId, RestLabel) : new Omen(action.Id, LabelOf(action));

        /// <summary>
        /// §9 step 12 / §17.6 F11 (#50): the 予定 of an elite or a boss — the action it would take
        /// second, chosen from the tree as it stands now with the first action's cost already paid
        /// and the first action out of the running (roster §1.3). Null when it takes only one action
        /// a phase, or when the first omen is a rest (nothing follows a rest). It is not a commitment:
        /// the tree is read again after the first action (<see cref="TurnLoop"/>), and a different
        /// answer is a 予定変更.
        /// </summary>
        public static Omen? DecidePlan(
            EnemyDef def, int gap, int stamina, Omen first, IReadOnlyCollection<string>? skip = null, TreeSwitch? active = null)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (first == null) throw new ArgumentNullException(nameof(first));
            if (def.ActionsPerPhase < 2 || first.ActionId == RestActionId) return null;

            var firstAction = ActionOf(def, first.ActionId, active);
            var second = new List<string>();
            if (skip != null) second.AddRange(skip);
            second.Add(first.ActionId);
            return OmenOf(ChooseAction(def, gap, stamina - firstAction.Cost, second, active));
        }

        /// <summary>
        /// §6: the omen is a commitment. The declared action is carried out as shown, and when it can
        /// no longer be paid for the enemy rests instead of picking something cheaper.
        /// </summary>
        public static EnemyActionDef? ActionToExecute(EnemyDef def, Omen omen, int stamina, TreeSwitch? active = null)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (omen == null) throw new ArgumentNullException(nameof(omen));
            if (omen.ActionId == RestActionId) return null;

            var action = ActionOf(def, omen.ActionId, active);
            return Combat.CanPay(action.Cost, stamina) ? action : null;
        }

        /// <summary>
        /// §5 俊敏 (#48): the free cell an enemy holding 俊敏 takes at its turn start (§9 step 9), as
        /// +1 (forward, toward the player), −1 (back) or 0 (it stays). It serves the omen it is about
        /// to carry out: an action aimed at the player whose reach the gap falls outside steps one
        /// cell toward that reach. An action already in reach, one aimed at nobody, and a rest
        /// (<paramref name="action"/> null) stay. Nothing random: the player can read it off the omen.
        /// </summary>
        public static int FreeStepDirection(EnemyActionDef? action, int gap)
        {
            if (action == null) return 0;
            if (!IsOpponentDirected(action.Attributes, action.Face, action.Targets)) return 0;
            var reach = action.Face.ReachOrDefault;
            if (gap > reach.Max) return Statuses.SwiftFreeCells;
            if (gap < reach.Min) return -Statuses.SwiftFreeCells;
            return 0;
        }

        /// <summary>
        /// §2.4 / §6: whether a face is aimed at the opponent, which is what a reach applies to — an
        /// attack, a status put on the opponent, a push / pull, or 崩し. A Self-targeted action reads
        /// no reach even when it carries those rows. One and All (§7.4) both aim at the opponent.
        /// </summary>
        public static bool IsOpponentDirected(BattleAttribute attributes, Face face, TargetKind targets)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));
            if (targets == TargetKind.Self) return false;
            return attributes.HasFlag(BattleAttribute.Attack) || face.GivesFoeStatus || face.Push != 0 || face.Break > 0;
        }

        /// <summary>
        /// §6: the omen label, read off the action. The kind is the first attribute in the order
        /// Attack, Move, Guard, Skill, Stance — the movement effect (not an attribute, §2.1) is put before a guard here (unlike the card's
        /// colour role, CoreText.KindOf) because where the enemy will stand is what the player has to
        /// read. The reach is the action's own when it has an opponent-directed face, else null.
        /// </summary>
        public static OmenLabel LabelOf(EnemyActionDef action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            // The roster's 予兆 column wins where the attribute order would read it otherwise (#189:
            // 盾を掲げて前進 is 防御, 追い立てる is 技 — the same faces as moves that read 移動).
            OmenKind kind =
                action.Omen.HasValue ? action.Omen.Value
                : action.Attributes.HasFlag(BattleAttribute.Attack) ? OmenKind.Attack
                : AttributeRule.HasMovement(action.Face) ? OmenKind.Move
                : action.Attributes.HasFlag(BattleAttribute.Guard) ? OmenKind.Guard
                : action.Attributes.HasFlag(BattleAttribute.Skill) ? OmenKind.Skill
                : OmenKind.Stance;
            Reach? reach = IsOpponentDirected(action.Attributes, action.Face, action.Targets)
                ? action.Face.ReachOrDefault
                : null;
            return new OmenLabel(kind, reach);
        }

        /// <summary>
        /// §6: the cells an omen aims at, counted from the enemy's near edge right now (the player
        /// side of the line). Empty for an action that aims at nobody, or when nothing it reaches is
        /// on the line. Lowest cell first.
        /// </summary>
        public static IReadOnlyList<int> TargetCells(OmenLabel label, CombatantState enemy)
        {
            if (label == null) throw new ArgumentNullException(nameof(label));
            if (enemy == null) throw new ArgumentNullException(nameof(enemy));
            var cells = new List<int>();
            if (label.Reach == null) return cells;
            for (int gap = label.Reach.Max; gap >= label.Reach.Min; gap--)
            {
                int cell = Field.FrontCellAt(gap, enemy.Cell);
                if (cell >= 1) cells.Add(cell);
            }
            return cells;
        }

        /// <summary>
        /// §6 「行動が間合いの境目を読むとき」: the gap boundary <paramref name="action"/>'s trait reads
        /// (間合い n 以上 / 間合い n 以下), or null for an action whose trait reads none.
        /// </summary>
        public static GapBoundary? BoundaryOf(EnemyActionDef action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            var trait = action.Trait;
            if (trait == null) return null;
            return trait.Condition switch
            {
                TraitCondition.GapAtLeast => new GapBoundary(AtLeast: true, trait.Threshold),
                TraitCondition.GapAtMost => new GapBoundary(AtLeast: false, trait.Threshold),
                _ => null,
            };
        }

        /// <summary>
        /// §6: 境目を満たすマス — of the cells <paramref name="action"/> aims at from where
        /// <paramref name="enemy"/> stands (<see cref="TargetCells"/>), the ones whose gap meets the
        /// boundary its trait reads. A player standing on one would meet the trait when the action is
        /// carried out from there. Empty when the action reads no boundary or aims at nobody. Lowest
        /// cell first.
        /// </summary>
        public static IReadOnlyList<int> BoundaryCells(EnemyActionDef action, CombatantState enemy)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (enemy == null) throw new ArgumentNullException(nameof(enemy));
            var cells = new List<int>();
            var boundary = BoundaryOf(action);
            if (boundary == null) return cells;
            foreach (int cell in TargetCells(LabelOf(action), enemy))
            {
                // TargetCells counts a cell as Field.FrontCellAt(gap, …); read the gap back off it.
                if (boundary.Holds(Field.GapFrom(cell, enemy.Cell))) cells.Add(cell);
            }
            return cells;
        }

        /// <summary>§6: the omen as the screen words it — "攻撃・1〜2", "防御", "休み" (battle-visual-v1 4.4, #357).</summary>
        public static string ToText(this OmenLabel label)
        {
            if (label == null) throw new ArgumentNullException(nameof(label));
            string kind = label.Kind switch
            {
                OmenKind.Attack => "攻撃",
                OmenKind.Guard => "防御",
                OmenKind.Move => "移動",
                OmenKind.Skill => "技",
                OmenKind.Stance => "構え",
                OmenKind.Rest => "休み",
                _ => throw new ArgumentOutOfRangeException(nameof(label), label.Kind, null),
            };
            return label.Reach != null ? $"{kind}・{label.Reach.ToText()}" : kind;
        }
    }
}
