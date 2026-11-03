// The card over the exploration map (dungeon_exploration_v4.md §6.3〜§6.6): one small function per
// thing the card can wait on, so Card itself reads as the order they are asked in (#597).
using System.Collections.Generic;
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
            new ScreenCard(CardKind.ConfirmStep, $"瘴気が {Miasma.DeathAt}% に達します",
                new[] { "このノードへ入ると、瘴気死でこの生が終わります。" },
                new[]
                {
                    new CardButton("それでも進む", ScreenAction.ConfirmStep),
                    new CardButton("やめる", ScreenAction.Cancel),
                });

        private static ScreenCard ConfirmDescendCard() =>
            new ScreenCard(CardKind.ConfirmDescend, $"瘴気が {Miasma.DeathAt}% に達します",
                new[] { $"次の層へ降りると、入口で瘴気が {Miasma.DeathAt}% に達し、この生が終わります。" },
                new[]
                {
                    new CardButton("それでも降りる", ScreenAction.ConfirmDescend),
                    new CardButton("やめる", ScreenAction.Cancel),
                });

        private static ScreenCard ConfirmEndLifeCard(ExplorationState state) =>
            new ScreenCard(CardKind.ConfirmEndLife, "この生を終えますか",
                new[]
                {
                    "生きたまま次の生へ移ります。手記は丸ごと次の生へ渡ります。",
                    $"いまの到達: 第 {state.Profile.Layer} 層、瘴気 {state.MiasmaPercent}%",
                },
                new[]
                {
                    new CardButton(ExplorationText.EndLifeButton, ScreenAction.ConfirmEndLife),
                    new CardButton("やめる", ScreenAction.Cancel),
                });

        /// <summary>The layer's end: its boss beaten, its 刻限 run out, or the interlude. Null in any other phase.</summary>
        private static ScreenCard LayerEndCard(ExplorationSession session)
        {
            var state = session.State;
            int layer = state.Profile.Layer;

            switch (state.Phase)
            {
                case RunPhase.LayerCleared:
                    return new ScreenCard(CardKind.LayerCleared, "階層ボスを倒した",
                        new[] { $"第 {layer} 層を抜けます。", InterludeLine(state) }, ToInterlude());

                case RunPhase.PushedOut:
                    return new ScreenCard(CardKind.PushedOut, "刻限が尽きた",
                        new[]
                        {
                            state.Profile.IsLast
                                ? "この層の行動は終わりです。これより下の層はありません。"
                                : "この層の行動は終わりです。階層間の休憩を経て、次の層へ押し出されます。",
                            InterludeLine(state),
                        },
                        ToInterlude());

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
                   $"{InterludeShare(state, gain < ExplorationReducer.InterludeHealOf(state))}・スタミナ全回復・瘴気はたまらない";
        }

        private static CardButton[] ToInterlude() => new[] { new CardButton("階層間の休憩へ", ScreenAction.GoToInterlude) };

        /// <summary>The life's end, or no card at all while the life goes on.</summary>
        private static ScreenCard LifeEndCard(ExplorationState state)
        {
            int layer = state.Profile.Layer;
            switch (state.Phase)
            {
                case RunPhase.MiasmaDeath:
                    return EndCard(CardKind.MiasmaDeath, "瘴気死", $"瘴気が {Miasma.DeathAt}% に達し、第 {layer} 層でこの生が終わった。", state);

                case RunPhase.Fallen:
                    return EndCard(CardKind.Fallen, "倒れた", $"第 {layer} 層の戦いで倒れた。", state);

                case RunPhase.Completed:
                    return EndCard(CardKind.Completed, "踏破", "歪みの根を倒した。", state);

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
                $"瘴気はたまらない（{rest.MiasmaPercent}%）",
            };
            var buttons = new List<CardButton>();

            if (state.Profile.IsLast)
            {
                lines.Add("これより下の層はありません。");
            }
            else
            {
                var next = SevenLayers.Of(state.Profile.Layer + 1);
                int density = Miasma.EffectiveDensity(next.Density, state.Loadout.DensityRelief);
                // A hidden name stays hidden until its own carving is read, so the next layer shows its first name.
                // Trial extension: 刻限の砂 adds to the next layer's 刻限 too.
                string time = state.TimeBonus > 0
                    ? $"{next.TimeLimit + state.TimeBonus}（{ExplorationText.TimeBonus(ToolNames(state.Loadout, ReliefKind.Time), state.TimeBonus)}）"
                    : $"{next.TimeLimit}";
                // The name the life knows (DisplayName) — the same as Name in the C#, where the layer-two carving is
                // never read before its own layer; a lineage that read it in an earlier life knows it from the start (R-20).
                lines.Add($"次は第 {next.Layer} 層　{next.DisplayName(session.CarvingTaken)}: 1 刻限ごとに増える瘴気 +{density}%、刻限 {time}");
                if (session.TryDescend().Phase == RunPhase.MiasmaDeath)
                    lines.Add($"！ 降りると入口で瘴気が {Miasma.DeathAt}% に達し、この生が終わります");
                buttons.Add(new CardButton("次の層へ降りる", ScreenAction.Descend));
            }
            buttons.Add(new CardButton(ExplorationText.EndLifeButton, ScreenAction.AskEndLife));
            return new ScreenCard(CardKind.Interlude, "階層間の休憩", lines, buttons);
        }

        private static ScreenCard EndCard(CardKind kind, string title, string line, ExplorationState state) =>
            new ScreenCard(kind, title,
                new[] { line, $"瘴気 {state.MiasmaPercent}% ／ HP {state.Hp} / {state.MaxHp}" },
                new[] { new CardButton("新しい生で潜り直す", ScreenAction.NewLife) });
    }
}
