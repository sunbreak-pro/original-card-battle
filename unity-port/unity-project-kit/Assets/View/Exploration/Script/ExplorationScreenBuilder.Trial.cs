// Trial extension of the screen builder (port of the browser trial's src/bridge/explorationScript/
// ExplorationScreenBuilder.js; docs/EXTENSIONS.md 「探索の持ち物とイベント」 and 「探索の結果と再開」). Build calls these
// only when the session's trial rules are on (or they answer the C#'s way when they are off), so with RunStart.Trial
// off every frame is the C#'s. ExplorationScreenBuilder.cs carries the hooks, each marked 「Trial extension」.
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonContent;
using DungeonCore;

namespace Exploration
{
    public static partial class ExplorationScreenBuilder
    {
        /// <summary>
        /// What this layer's 情報収集 did, once it is done — the enemy it raised to 開示度 2 (「済ませた。瘴甲の竜兵が開示度 2
        /// になった」, 写しの帳's pick marked), or that every enemy behind the map was at 2 already. Null outside trial,
        /// before the survey, and while 写しの帳's choice waits. The node's detail, the journal's 情報収集 row and the
        /// map's notice on arrival all print this one line.
        /// </summary>
        // js: bridge/explorationScript/ExplorationScreenBuilder.js:604
        public static string SurveyLine(ExplorationSession session)
        {
            if (!session.Trial || !session.Surveyed || session.Keeper == null) return null;
            if (session.SurveyTarget != null)
            {
                string name = session.Keeper.EnemyName(session.SurveyTarget);
                return session.SurveyPicked ? ExplorationText.SurveyChosen(name) : ExplorationText.SurveyDone(name);
            }
            if (session.SurveyChoices != null && session.SurveyChoices.Count > 0) return null;
            // the layer was asked (the intel names targets) and had none left below 2; an intel that names none says nothing
            return session.Intel is ISurveyIntel ? ExplorationText.SurveyNothing : null;
        }

        /// <summary>
        /// The 階層間の休憩 card on its own, or null outside the interlude. Build's Card puts a confirmation (この生を終える,
        /// a descent into 瘴気死) in its place; the scene draws the rest under that confirmation from this
        /// (docs/EXTENSIONS.md X-8: the interlude's own card stays under its confirmation, also after a reload).
        /// </summary>
        // js: bridge/explorationScript/ExplorationScreenBuilder.js:760
        public static ScreenCard InterludeCard(ExplorationSession session) =>
            session.State.Phase == RunPhase.Interlude ? InterludeCardOf(session) : null;

        /// <summary>
        /// 「HP 30 → 40」 and the like for every value that differs between two states — the gauge, max stamina, stamina
        /// and HP, the values the screen shows. It is also the one definition of 「changes something now」:
        /// ExplorationSession.CanUseSlot offers a consumable only when this is not empty, so a slot is lit exactly when it
        /// has a forecast (a hidden temporary change the floor of 3 swallows counts as nothing).
        /// </summary>
        // js: bridge/explorationScript/ExplorationScreenBuilder.js:373
        public static IReadOnlyList<string> ChangeLines(ExplorationState before, ExplorationState after)
        {
            var lines = new List<string>();
            if (after.MiasmaPercent != before.MiasmaPercent) lines.Add(ExplorationText.MiasmaChange(before.MiasmaPercent, after.MiasmaPercent));
            if (after.MaxStamina != before.MaxStamina) lines.Add(ExplorationText.MaxStaminaChange(before.MaxStamina, after.MaxStamina));
            if (after.Stamina != before.Stamina) lines.Add(ExplorationText.StaminaChange(before.Stamina, after.Stamina));
            if (after.Hp != before.Hp) lines.Add(ExplorationText.HpChange(before.Hp, after.Hp));
            return lines;
        }

        /// <summary>
        /// What an event node holds, once it is next door or walked (encounters_and_rewards_v4.md §5: 「中身は隣接した
        /// ときに見えます」). A far node never gets here.
        /// </summary>
        // js: bridge/explorationScript/ExplorationScreenBuilder.js:187
        private static IReadOnlyList<string> TrialEventLines(ExplorationSession session, MapNode node)
        {
            var state = session.State;
            var kind = session.EventAt(node.Id);
            if (kind == null) return new[] { ExplorationText.EventHidden };
            int layer = state.Profile.Layer;
            var done = session.Events.FirstOrDefault(e => e.Layer == layer && e.NodeId == node.Id);
            var lines = new List<string>();
            switch (kind.Value)
            {
                case EventKind.Spring:
                    lines.Add(ExplorationText.SpringLine(ExplorationReducer.SpringHeal(state), LayerEvents.SpringPercent(layer),
                        done == null ? System.Math.Max(0, state.MaxHp - state.Hp) : (int?)null));
                    break;
                case EventKind.HeavyChest:
                    lines.Add(ExplorationText.ChestLine(LayerEvents.ChestStaminaCost));
                    lines.Add(ExplorationText.ChestContents(LayerEvents.ChestContents(layer)));
                    break;
                case EventKind.StrangeMeal:
                    lines.Add(ExplorationText.MealLine(LayerEvents.MealStaminaGain, ExplorationReducer.MealCost(state), LayerEvents.MealHpPercent));
                    break;
            }
            if (done != null) lines.Add(ExplorationText.EventDone(done.Accepted, done.Summary));
            else if (LayerEvents.HasChoice(kind.Value)) lines.Add(ExplorationText.EventChoiceNote);
            return lines;
        }

        /// <summary>
        /// The event a step onto this node resolves on arrival without asking — a 泉 — so the forecast can count it
        /// (dungeon_exploration_v4.md §6.4: 「変わるときだけ…HP 30 → 38 を足します」). Null outside trial.
        /// </summary>
        // js: bridge/explorationScript/ExplorationScreenBuilder.js:216
        private static EventKind? ArrivalEvent(ExplorationSession session, MapNode node)
        {
            if (!session.Trial || node.Kind != NodeKind.Event || session.State.HasResolved(node.Id)) return null;
            var kind = session.EventAt(node.Id);
            return kind == EventKind.Spring ? kind : null;
        }

        /// <summary>
        /// 標の石's ways onto a node it can land on, each with its forecast. A far node's contents stay hidden
        /// (encounters_and_rewards_v4.md §5; dungeon_exploration_v4.md §6.3), so the jump's forecast counts its event only
        /// once the node is next door or walked — a far 泉 forecasts as a bare step, like a far 重い宝箱.
        /// </summary>
        // js: bridge/explorationScript/ExplorationScreenBuilder.js:228
        private static IReadOnlyList<StepOption> JumpOptions(ExplorationSession session, MapNode node, int slot, NodeStanding standing)
        {
            var state = session.State;
            if (!ExplorationReducer.CanTeleport(state, node.Id)) return new StepOption[0];
            var arrival = standing == NodeStanding.Far ? null : ArrivalEvent(session, node);
            StepForecast ForecastOf(RestChoice choice) => Forecast(state, node.Id, choice, slot, arrival);
            if (node.Kind == NodeKind.Rest)
            {
                return new[]
                {
                    new StepOption(ExplorationText.JumpRestLabel, RestChoice.Rest, ForecastOf(RestChoice.Rest), true),
                    new StepOption(ExplorationText.JumpTrainLabel, RestChoice.Train, ForecastOf(RestChoice.Train), true),
                };
            }
            return new[] { new StepOption(ExplorationText.JumpLabel, RestChoice.Rest, ForecastOf(RestChoice.Rest), true) };
        }

        /// <summary>
        /// 防瘴の面's note under the gauge, for the layer the gauge's forecast speaks of — the layer being walked
        /// (「次の 1 歩で +N%」), or, once the layer is left, the layer below (「降りると +N%」, X-6), so the note and the
        /// figure beside it are the same layer's (dungeon_exploration_v4.md §6.5 「次の層の予告」).
        /// </summary>
        // js: bridge/explorationScript/ExplorationScreenBuilder.js:287
        private static string ReliefLabel(ExplorationSession session)
        {
            var state = session.State;
            string names = ToolNames(state.Loadout, ReliefKind.Density);
            if (!(state.Phase == RunPhase.Exploring && state.TimeLeft > 0))
            {
                var below = session.TryDescend();
                if (below != null) return ExplorationText.Relief(names, below.Profile.Density, below.EffectiveDensity);
            }
            return ExplorationText.Relief(names, state.Profile.Density, state.EffectiveDensity);
        }

        /// <summary>
        /// Every slot says whether it can be used now, what using it would change (tried on the state, the way a step's
        /// forecast is), or why it cannot.
        /// </summary>
        // js: bridge/explorationScript/ExplorationScreenBuilder.js:338
        private static IReadOnlyList<SlotCard> TrialSlots(ExplorationSession session)
        {
            var state = session.State;
            var carried = state.Loadout.Consumables;
            var slots = new List<SlotCard>();
            for (int i = 0; i < RunLoadout.ConsumableSlots; i++)
            {
                if (i >= carried.Count)
                {
                    slots.Add(new SlotCard(i, ExplorationText.EmptySlot, "", false));
                    continue;
                }
                var item = carried[i];
                bool canUse = session.CanUseSlot(i);
                IReadOnlyList<string> forecast = null;
                string reason = "";
                if (item.Kind == ReliefKind.Teleport)
                {
                    if (!canUse && session.CanUseConsumables) reason = ExplorationText.NoJumpNow;
                }
                else if (session.CanUseConsumables)
                {
                    var after = ExplorationReducer.UseConsumable(state, i);
                    forecast = ChangeLines(state, after);
                    if (forecast.Count == 0)
                    {
                        forecast = null;
                        reason = ExplorationText.NoEffectNow;
                    }
                }
                slots.Add(new SlotCard(i, ExplorationText.ItemName(item.Id), ExplorationText.PlayerNote(item.Id), canUse, forecast, reason,
                    session.AimingSlot == i, item.Id));
            }
            return slots;
        }

        /// <summary>Every carried tool with what it does (tools_and_consumables_v4.md §2). Empty outside trial.</summary>
        // js: bridge/explorationScript/ExplorationScreenBuilder.js:383
        private static IReadOnlyList<ToolCard> ToolCards(ExplorationSession session)
        {
            if (!session.Trial) return new ToolCard[0];
            var state = session.State;
            return state.Loadout.Tools.Select(t =>
            {
                var def = ItemCatalogue.All.FirstOrDefault(d => d.Id == t.Id);
                bool battle = t.Kind == ReliefKind.Battle;
                string effect = ExplorationText.ToolEffect(t.Kind, t.Amount);
                return new ToolCard(t.Id, def != null ? def.Name : t.Id, ExplorationText.PlayerNote(t.Id),
                    battle ? ExplorationText.SideBattle : ExplorationText.SideExploration, effect);
            }).ToList();
        }

        /// <summary>The names of the carried tools of one kind, joined (「刻限の砂・刻限の砂」 → one name).</summary>
        // js: bridge/explorationScript/ExplorationScreenBuilder.js:396
        private static string ToolNames(RunLoadout loadout, ReliefKind kind)
        {
            var names = new List<string>();
            foreach (var t in loadout.Tools)
            {
                if (t.Kind != kind) continue;
                string name = ExplorationText.ItemName(t.Id);
                if (!names.Contains(name)) names.Add(name);
            }
            return string.Join("・", names);
        }

        /// <summary>
        /// The interlude's share with 灯芯の欠片 named. <paramref name="capped"/> (trial): the HP printed beside it is less
        /// than the share because the maximum stopped it, so the share says 「上限まで」. Outside trial the C#'s
        /// 「（最大の 30%）」.
        /// </summary>
        // js: bridge/explorationScript/ExplorationScreenBuilder.js:411
        private static string InterludeShare(ExplorationState state, bool capped = false)
        {
            int percent = ExplorationReducer.InterludePercent(state);
            int bonus = percent - ExplorationReducer.InterludeHpPercent;
            return state.Trial
                ? ExplorationText.InterludeShare(percent, ToolNames(state.Loadout, ReliefKind.Interlude), bonus, capped)
                : $"（最大の {ExplorationReducer.InterludeHpPercent}%）";
        }

        /// <summary>The cards that wait on a choice, before the layer's or the life's end. Null when none waits.</summary>
        // js: bridge/explorationScript/ExplorationScreenBuilder.js:420
        private static ScreenCard TrialCard(ExplorationSession session)
        {
            var state = session.State;
            if (session.HasPendingEvent)
            {
                var waiting = session.PendingEvent;
                string name = ExplorationText.EventName(waiting.Kind);
                var accepted = ExplorationReducer.ResolveEvent(state, waiting.Kind, true);
                var node = state.Map.Node(waiting.NodeId);
                var lines = new List<string> { ExplorationText.EventLead(name) };
                lines.AddRange(TrialEventLines(session, node).Where(l => l != ExplorationText.EventChoiceNote));
                lines.Add(ExplorationText.TempClampNote);
                return new ScreenCard(CardKind.EventChoice, name, lines,
                    new[]
                    {
                        new CardButton(ExplorationText.AcceptLabel, ScreenAction.AcceptEvent, null, string.Join("　", ChangeLines(state, accepted))),
                        new CardButton(ExplorationText.DeclineLabel, ScreenAction.DeclineEvent, null, ExplorationText.DeclineNote),
                    });
            }

            if (session.HasPendingSurvey)
            {
                var keeper = session.Keeper;
                var buttons = session.SurveyChoices.Select(id => new CardButton(
                    ExplorationText.SurveyButton(keeper != null ? keeper.EnemyName(id) : id, keeper != null ? keeper.DisclosureOf(id) : 0),
                    ScreenAction.ChooseSurvey, id)).ToList();
                return new ScreenCard(CardKind.SurveyChoice, ExplorationText.SurveyTitle, new[] { ExplorationText.SurveyLead }, buttons);
            }

            if (session.HasPendingFound)
            {
                string id = session.PendingFound[0];
                string name = ExplorationText.ItemName(id);
                var buttons = state.Loadout.Consumables.Select((item, i) =>
                    new CardButton(ExplorationText.SwapButton(ExplorationText.ItemName(item.Id)), ScreenAction.TakeFound,
                        i.ToString(CultureInfo.InvariantCulture), ExplorationText.PlayerNote(item.Id))).ToList();
                buttons.Add(new CardButton(ExplorationText.LeaveFoundLabel, ScreenAction.LeaveFound, null, ""));
                return new ScreenCard(CardKind.Swap, ExplorationText.SwapTitle,
                    new[] { ExplorationText.SwapLead(name), $"{name}: {ExplorationText.PlayerNote(id)}" }, buttons);
            }
            return null;
        }
    }
}
