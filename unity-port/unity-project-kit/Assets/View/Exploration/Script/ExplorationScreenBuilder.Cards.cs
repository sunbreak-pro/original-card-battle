// The card over the exploration map (dungeon_exploration_v4.md §6.3〜§6.6): one small function per
// thing the card can wait on, so Card itself reads as the order they are asked in (#597).
using System.Collections.Generic;
using System.Globalization;
using DungeonCore;

namespace Exploration
{
    public static partial class ExplorationScreenBuilder
    {
        /// <summary>The card over the map. Waiting inputs come first, then the layer's or the life's end.</summary>
        private static ScreenCard Card(ExplorationSession session)
        {
            var state = session.State;

            if (session.HasPendingBattle) return PendingBattleCard(state, session.PendingBattleNode);
            if (session.HasPendingStep) return ConfirmStepCard();
            if (session.ConfirmingDescend) return ConfirmDescendCard();
            if (session.ConfirmingEndLife) return ConfirmEndLifeCard(state);

            // Trial extension (js: ExplorationScreenBuilder.js:510): an event, a survey pick or a swap waits before the
            // layer's end. Outside trial only the event's own card can wait here (#718).
            if (session.ResolvesEvents)
            {
                var trial = TrialCard(session);
                if (trial != null) return trial;
            }

            return LayerEndCard(session) ?? LifeEndCard(state);
        }

        private static ScreenCard PendingBattleCard(ExplorationState state, int nodeId)
        {
            var kind = state.Map.Node(nodeId).Kind;
            string title = kind == NodeKind.Boss ? "階層ボスとの戦闘（仮）"
                : kind == NodeKind.Elite ? "精鋭との戦闘（仮）"
                : "戦闘（仮）";
            return new ScreenCard(CardKind.Battle, title,
                new[]
                {
                    "探索と戦闘はまだつながっていません。結果を選んで探索へ戻ります。",
                    "勝ったことにする: HP とスタミナはそのまま",
                    "倒れたことにする: HP 0 でこの生が終わる",
                },
                new[]
                {
                    new CardButton("勝ったことにする", ScreenAction.WinBattle),
                    new CardButton("倒れたことにする", ScreenAction.FallInBattle),
                });
        }

        private static ScreenCard ConfirmStepCard() =>
            new ScreenCard(CardKind.ConfirmStep, "ここで暦が尽きます",
                new[] { CalendarText.ExpiryWarning + "。" },
                new[]
                {
                    new CardButton("それでも進む", ScreenAction.ConfirmStep),
                    new CardButton("やめる", ScreenAction.Cancel),
                });

        private static ScreenCard ConfirmDescendCard() =>
            new ScreenCard(CardKind.ConfirmDescend, "降りると暦が尽きます",
                new[] { "次の層の入口で暦が尽き、龍神が目覚めます。世界が終わり、最初からやり直します。" },
                new[]
                {
                    new CardButton("それでも降りる", ScreenAction.ConfirmDescend),
                    new CardButton("やめる", ScreenAction.Cancel),
                });

        private static ScreenCard ConfirmEndLifeCard(ExplorationState state)
        {
            // The days the hand-over takes are the core's (CalendarState.Handover), tried on the calendar as it stands:
            // 3, 1 with 帰り道の糸, none while a king is down (seven_layers_v4.md §2.1 / §2.2).
            int days = state.DaysLeft - state.Calendar.Handover(RunPhase.Survived, state.Loadout.ShortHandover).DaysLeft;
            return new ScreenCard(CardKind.ConfirmEndLife, "この生を終えますか",
                new[]
                {
                    "生きたまま次の生へ移ります。手記は丸ごと次の生へ渡ります。",
                    $"いまの到達: 第 {state.Profile.Layer} 層、暦 {CalendarText.DaysLabel(state.DaysLeft)}",
                    days > 0 ? CalendarText.Handover(days) : CalendarText.HandoverFrozen,
                },
                new[]
                {
                    new CardButton(ExplorationText.EndLifeButton, ScreenAction.ConfirmEndLife),
                    new CardButton("やめる", ScreenAction.Cancel),
                });
        }

        /// <summary>The layer's end: its boss beaten, or the interlude. Null in any other phase.</summary>
        private static ScreenCard LayerEndCard(ExplorationSession session)
        {
            var state = session.State;
            int layer = state.Profile.Layer;

            switch (state.Phase)
            {
                case RunPhase.LayerCleared when state.Profile.IsKingLayer:
                    // A king is down but the other two are not: layer 8 stays shut, so the life can only end here
                    // (dungeon_exploration_v4.md §6.5, seven_layers_v4.md §9). No interlude is taken on the king layer.
                    return new ScreenCard(CardKind.LayerCleared, $"{CalendarText.KingName(state.Region)}を倒した",
                        new[]
                        {
                            CalendarText.Extension(state.Calendar.ExtensionIfKingDown(state.Region)),
                            CalendarText.Note(state.Calendar),
                            CalendarText.KingLayerEnd,
                        },
                        new[] { new CardButton(ExplorationText.EndLifeButton, ScreenAction.AskEndLife) });

                case RunPhase.LayerCleared:
                    return new ScreenCard(CardKind.LayerCleared, "階層ボスを倒した",
                        new[] { $"第 {layer} 層を抜けます。", InterludeLine(state) }, ToInterlude());

                case RunPhase.Interlude:
                    return InterludeCardOf(session);

                default:
                    return null;
            }
        }

        /// <summary>What the interlude will give, tried on the state the way a step's forecast is. "" when it cannot be taken.</summary>
        private static string InterludeLine(ExplorationState state)
        {
            if (!ExplorationReducer.CanInterlude(state)) return "";
            int gain = ExplorationReducer.Interlude(state).Hp - state.Hp;
            return $"階層間の休憩: HP +{gain}" +
                   $"{InterludeShare(state, gain < ExplorationReducer.InterludeHealOf(state))}・スタミナ全回復・日数を使わない";
        }

        private static CardButton[] ToInterlude() => new[] { new CardButton("階層間の休憩へ", ScreenAction.GoToInterlude) };

        /// <summary>The life's end, or no card at all while the life goes on.</summary>
        private static ScreenCard LifeEndCard(ExplorationState state)
        {
            int layer = state.Profile.Layer;
            switch (state.Phase)
            {
                case RunPhase.Expired:
                    return EndCard(CardKind.Expired, "期限切れ", CalendarText.ExpiredLine, state);

                case RunPhase.Fallen:
                    return EndCard(CardKind.Fallen, "倒れた", $"第 {layer} 層の戦いで倒れた。", state);

                case RunPhase.Completed:
                    return EndCard(CardKind.Completed, "踏破", "三体の王を倒した。", state);

                case RunPhase.Survived:
                    return EndCard(CardKind.Survived, "生存ルート", $"生きたまま、第 {layer} 層でこの生を閉じた。", state);

                default:
                    return ScreenCard.None;
            }
        }

        // js: bridge/explorationScript/ExplorationScreenBuilder.js:558
        private static ScreenCard InterludeCardOf(ExplorationSession session)
        {
            var state = session.State;
            // Trial extension: the rest's own lines read the state the rest left (session.InterludeState), so a
            // consumable used at the interlude (§6.5 「休憩の間も消耗品は使える」) changes the band and the slots, not what
            // the rest is said to have given. The C# reads the state itself.
            var rest = session.Trial && session.InterludeState != null ? session.InterludeState : state;
            var lines = new List<string>
            {
                $"HP +{session.InterludeHealed}{InterludeShare(rest, session.Trial && session.InterludeHealed < ExplorationReducer.InterludeHealOf(rest))}で HP {rest.Hp} / {rest.MaxHp}",
                $"スタミナが全回復した（{rest.Stamina} / {rest.MaxStamina}）",
                $"日数を使わない（暦 {CalendarText.DaysLabel(rest.DaysLeft)}）",
            };
            var buttons = new List<CardButton>();

            if (session.NeedsRegion)
            {
                // §6.5: the layer-two interlude lays out the three ways down, each with its region's layer 3, in place of
                // 「次の層へ降りる」. The region holds for the life (seven_layers_v4.md §3.1).
                lines.Add(CalendarText.RegionChoiceLead);
                foreach (Region region in Lair.Regions)
                {
                    string preview = CalendarText.LayerPreview(Lair.Of(Lair.SacramentLayer + 1, region), session.CarvingTaken);
                    lines.Add(preview);
                    buttons.Add(new CardButton(CalendarText.RegionName(region), ScreenAction.ChooseRegion,
                        ((int)region).ToString(CultureInfo.InvariantCulture), preview));
                }
            }
            else if (session.NextLayer() is LayerProfile next)
            {
                // The name the life knows (DisplayName) — the same as Name in the C#, where the layer-two carving is
                // never read before its own layer; a lineage that read it in an earlier life knows it from the start (R-20).
                lines.Add($"次は{CalendarText.LayerPreview(next, session.CarvingTaken)}");
                var below = session.TryDescend();
                if (below != null && below.Phase == RunPhase.Expired)
                    lines.Add("！ 降りると入口で暦が尽き、世界が終わります");
                else if (below != null && below.DaysLeft == state.DaysLeft && !state.Calendar.IsFrozen)
                    lines.Add(CalendarText.DescendFree);
                buttons.Add(new CardButton(session.Region is Region chosen ? CalendarText.RegionChosen(chosen) : "次の層へ降りる",
                    ScreenAction.Descend));
            }
            else
            {
                lines.Add("これより下の層はありません。");
            }
            buttons.Add(new CardButton(ExplorationText.EndLifeButton, ScreenAction.AskEndLife));
            return new ScreenCard(CardKind.Interlude, "階層間の休憩", lines, buttons);
        }

        /// <summary>
        /// A life's end card. 期限切れ ends the world rather than the life, so it offers no new life here: starting over
        /// from the first day is the game's (§6.9).
        /// </summary>
        private static ScreenCard EndCard(CardKind kind, string title, string line, ExplorationState state) =>
            new ScreenCard(kind, title,
                new[] { line, $"暦 {CalendarText.DaysLabel(state.DaysLeft)} ／ HP {state.Hp} / {state.MaxHp}" },
                kind == CardKind.Expired
                    ? new CardButton[0]
                    : new[] { new CardButton("新しい生で潜り直す", ScreenAction.NewLife) });
    }
}
