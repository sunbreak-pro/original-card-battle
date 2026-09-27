// Turns a session into one frame of the exploration screen (dungeon_exploration_v4.md §6). Every
// number comes from the exploration core. The forecast of a step is ExplorationReducer.Step tried on
// the current state — the state is immutable, so trying it changes nothing — which is why the
// forecast and the step taken can never disagree.
using System.Collections.Generic;
using System.Linq;
using DungeonCore;
using Journal;

namespace Exploration
{
    public static class ExplorationScreenBuilder
    {
        public static ExplorationScreen Build(ExplorationSession session)
        {
            var state = session.State;
            var map = state.Map;
            var neighbours = new HashSet<int>(map.Neighbours(state.CurrentNodeId));
            bool canStep = state.Phase == RunPhase.Exploring;
            bool mapLive = session.MapIsLive;

            var nodes = new List<NodeCard>();
            foreach (var node in map.Nodes.OrderBy(n => n.Id))
            {
                var standing = StandingOf(state, node.Id, neighbours, canStep);
                int rowWidth = map.Row(node.Row).Count;
                float x = (node.Column + 0.5f) / rowWidth;
                float y = map.RowCount == 1 ? 0.5f : 1f - (float)node.Row / (map.RowCount - 1);

                var lines = standing == NodeStanding.Far ? new string[0] : ContentLines(session, node);
                var options = mapLive ? Options(state, node, standing) : new StepOption[0];
                string note = NoteOf(standing);
                nodes.Add(new NodeCard(
                    node,
                    x,
                    y,
                    ExplorationText.Glyph(node.Kind),
                    ExplorationText.KindLabel(node.Kind),
                    standing,
                    lines,
                    options,
                    note,
                    $"{ExplorationText.Glyph(node.Kind)}　{ExplorationText.KindLabel(node.Kind)}",
                    Detail(note, lines, options)));
            }

            var edges = new List<EdgeLine>();
            foreach (var node in map.Nodes.OrderBy(n => n.Id))
            {
                foreach (int to in map.Successors(node.Id).OrderBy(id => id))
                {
                    edges.Add(new EdgeLine(node.Id, to, Lit(state, nodes, node.Id, to)));
                }
            }

            return new ExplorationScreen(
                ExplorationText.LayerTitle(state.Profile.Layer, state.Profile.DisplayName(session.CarvingTaken)),
                $"刻限 {state.TimeLeft} / {state.Profile.TimeLimit}",
                state.TimeLeft,
                state.Profile.TimeLimit,
                Gauge(session),
                $"HP {state.Hp} / {state.MaxHp}",
                $"スタミナ {state.Stamina} / {state.MaxStamina}",
                MaxStaminaLabel(state),
                $"訓練の刻み {state.TrainingMarks}",
                nodes,
                edges,
                state.CurrentNodeId,
                mapLive ? "明るい枠のノードへ進めます。踏んだノードへは刻限を使わずに移れます。" : "",
                ToolsLabel(state.Loadout),
                Slots(session),
                session.CanEndLife,
                !state.IsOver,
                Card(session),
                Book(session));
        }

        private static NodeStanding StandingOf(ExplorationState state, int id, HashSet<int> neighbours, bool canStep)
        {
            if (id == state.CurrentNodeId) return NodeStanding.Current;
            bool resolved = state.HasResolved(id);
            if (canStep && neighbours.Contains(id))
            {
                if (resolved) return NodeStanding.Revisit;
                if (state.TimeLeft > 0) return NodeStanding.Reachable;
            }
            return resolved ? NodeStanding.Resolved : NodeStanding.Far;
        }

        private static string NoteOf(NodeStanding standing) => standing switch
        {
            NodeStanding.Current => "今いるノード",
            NodeStanding.Reachable => "入ると刻限を 1 使う",
            NodeStanding.Revisit => "踏んだノード。刻限を使わずに移れる",
            NodeStanding.Resolved => "踏んだノード",
            _ => ExplorationText.FarNode,
        };

        /// <summary>The detail panel's lines: the note, the contents, then each way in with its forecast.</summary>
        private static IReadOnlyList<string> Detail(string note, IReadOnlyList<string> contents, IReadOnlyList<StepOption> options)
        {
            var detail = new List<string> { note };
            detail.AddRange(contents);
            foreach (var option in options)
            {
                if (option.Forecast == null) continue;
                detail.Add("");
                detail.Add($"【{option.Label}】" + string.Join("　", option.Forecast.Lines));
                detail.AddRange(option.Forecast.Warnings.Select(w => "！ " + w));
            }
            return detail;
        }

        private static bool Lit(ExplorationState state, IReadOnlyList<NodeCard> nodes, int from, int to)
        {
            int other;
            if (from == state.CurrentNodeId) other = to;
            else if (to == state.CurrentNodeId) other = from;
            else return false;

            var standing = nodes.First(n => n.Id == other).Standing;
            return standing == NodeStanding.Reachable || standing == NodeStanding.Revisit;
        }

        /// <summary>What a node holds, by kind (dungeon_exploration_v4.md §6.3).</summary>
        private static IReadOnlyList<string> ContentLines(ExplorationSession session, MapNode node)
        {
            var state = session.State;
            var lines = new List<string>();
            string threat = ExplorationText.Threat(node.Kind);
            if (threat.Length > 0) lines.Add($"脅威度: {threat}");

            switch (node.Kind)
            {
                case NodeKind.Battle:
                case NodeKind.Elite:
                case NodeKind.Boss:
                    if (node.Kind == NodeKind.Elite) lines.Add("避けて通れる寄り道");
                    if (node.Kind == NodeKind.Boss)
                    {
                        lines.Add(state.Profile.IsLast
                            ? "倒すと、この生で潜り切ったことになる"
                            : "倒すと階層間の休憩へ進む");
                    }
                    var intel = session.Intel.ForNode(node, state.Profile.Layer, session.Surveyed);
                    if (intel == null || intel.EnemyName.Length == 0)
                    {
                        lines.Add($"敵: {ExplorationText.UnknownEnemy}");
                    }
                    else
                    {
                        lines.Add($"敵: {intel.EnemyName}（開示度 {intel.Disclosure} / {JournalPage.MaxDisclosure}）");
                        lines.AddRange(intel.Lines);
                    }
                    break;

                case NodeKind.Rest:
                    lines.Add($"休む: HP +{ExplorationReducer.RestHeal(state.MaxHp)}（最大の {ExplorationReducer.RestHpPercent}%）・" +
                              $"スタミナ全回復・最大スタミナ +{ExplorationReducer.RestMaxStaminaBonus}");
                    lines.Add("訓練する: 選んだ 1 枚に習熟の刻み 1");
                    lines.Add("どちらか 1 つだけを選ぶ");
                    break;

                case NodeKind.Survey:
                    lines.Add("この層の敵 1 種の開示度が 2 段目へ上がる");
                    if (session.Surveyed) lines.Add("この層では済ませた");
                    break;

                case NodeKind.Event:
                    lines.Add("泉 / 重い宝箱 / 特殊な食事 のどれかが起きる");
                    break;

                case NodeKind.Trace:
                    lines.Add("前の生の遺産を受け取れる");
                    lines.Add("受け取れるのは 1 つの生で 1 件");
                    break;

                case NodeKind.Carving:
                    lines.Add(session.CarvingTaken ? "読んだ。この層の名前が分かった" : "この層の名前が分かる");
                    lines.Add("戦闘には効かない");
                    break;
            }
            return lines;
        }

        private static IReadOnlyList<StepOption> Options(ExplorationState state, MapNode node, NodeStanding standing)
        {
            if (standing == NodeStanding.Revisit)
                return new[] { new StepOption("移る", RestChoice.Rest, null) };
            if (standing != NodeStanding.Reachable)
                return new StepOption[0];

            if (node.Kind == NodeKind.Rest)
            {
                return new[]
                {
                    new StepOption("休む", RestChoice.Rest, Forecast(state, node.Id, RestChoice.Rest)),
                    new StepOption("訓練する", RestChoice.Train, Forecast(state, node.Id, RestChoice.Train)),
                };
            }
            return new[] { new StepOption("進む", RestChoice.Rest, Forecast(state, node.Id, RestChoice.Rest)) };
        }

        /// <summary>The step tried on the current state, told as before → after (dungeon_exploration_v4.md §6.4).</summary>
        public static StepForecast Forecast(ExplorationState before, int nodeId, RestChoice choice)
        {
            var after = ExplorationReducer.Step(before, nodeId, choice);

            var lines = new List<string>
            {
                $"刻限 {ExplorationText.Change(before.TimeLeft, after.TimeLeft)}",
                $"瘴気 {before.MiasmaPercent}% → {after.MiasmaPercent}%",
            };
            if (after.MaxStamina != before.MaxStamina)
                lines.Add($"最大スタミナ {ExplorationText.Change(before.MaxStamina, after.MaxStamina)}");
            if (after.Stamina != before.Stamina)
                lines.Add($"スタミナ {ExplorationText.Change(before.Stamina, after.Stamina)}");
            if (after.Hp != before.Hp)
                lines.Add($"HP {ExplorationText.Change(before.Hp, after.Hp)}");

            var warnings = new List<string>();
            if (after.Phase == RunPhase.MiasmaDeath)
            {
                warnings.Add("ここで瘴気が 100% に達し、この生が終わります");
            }
            else
            {
                // Judged on the maximum itself: a 休息 that crosses a 20% line still lifts it by 2.
                if (after.MaxStamina < before.MaxStamina)
                {
                    int line = Miasma.MaxStaminaPenalty(after.MiasmaPercent) * Miasma.StepPercent;
                    warnings.Add($"瘴気が {line}% を越え、最大スタミナが {before.MaxStamina} から {after.MaxStamina} に下がります");
                }
                if (after.Phase == RunPhase.PushedOut)
                    warnings.Add("刻限が尽き、この層の行動が終わります");
            }
            return new StepForecast(before, after, lines, warnings);
        }

        private static MiasmaGauge Gauge(ExplorationSession session)
        {
            var state = session.State;
            int percent = state.MiasmaPercent;
            int next = percent;
            string forecast = "";

            if (state.Phase == RunPhase.Exploring && state.TimeLeft > 0)
            {
                next = Miasma.Accumulate(percent, state.EffectiveDensity);
                forecast = $"次の 1 歩で +{state.EffectiveDensity}%";
            }
            else if (session.TryDescend() is ExplorationState below)
            {
                // The next layer's entry node is resolved on arrival, so descending costs one step at once.
                next = below.MiasmaPercent;
                forecast = $"降りると +{below.MiasmaPercent - percent}%";
            }

            var stepLines = new List<int>();
            for (int line = Miasma.StepPercent; line < Miasma.DeathAt; line += Miasma.StepPercent) stepLines.Add(line);

            return new MiasmaGauge(percent, next, $"瘴気 {percent}%", forecast, stepLines);
        }

        /// <summary>「最大 9 = 10 − 瘴気 1」, plus the temporary modifier when there is one.</summary>
        private static string MaxStaminaLabel(ExplorationState state)
        {
            int penalty = Miasma.MaxStaminaPenalty(state.MiasmaPercent);
            int temp = state.TempMaxStaminaMod;
            int max = state.MaxStamina;

            // The 3..14 clamp can break the sum; then only the maximum itself is honest to print.
            if (Miasma.BaseMaxStamina - penalty + temp != max) return $"最大 {max}";

            string label = $"最大 {max} = {Miasma.BaseMaxStamina} − 瘴気 {penalty}";
            if (temp > 0) label += $" + 一時 {temp}";
            else if (temp < 0) label += $" − 一時 {-temp}";
            return label;
        }

        private static string ToolsLabel(RunLoadout loadout)
        {
            string names = loadout.Tools.Count == 0
                ? "なし"
                : string.Join("・", loadout.Tools.Select(t => ExplorationText.ItemName(t.Id)));
            return $"ツール: {names}（{loadout.Tools.Count} / {loadout.ToolSlotCount}）";
        }

        private static IReadOnlyList<SlotCard> Slots(ExplorationSession session)
        {
            var carried = session.State.Loadout.Consumables;
            var slots = new List<SlotCard>();
            for (int i = 0; i < RunLoadout.ConsumableSlots; i++)
            {
                if (i < carried.Count)
                {
                    string id = carried[i].Id;
                    slots.Add(new SlotCard(i, ExplorationText.ItemName(id), ExplorationText.ItemNote(id), session.CanUseConsumables));
                }
                else
                {
                    slots.Add(new SlotCard(i, ExplorationText.EmptySlot, "", false));
                }
            }
            return slots;
        }

        /// <summary>The card over the map. Waiting inputs come first, then the layer's or the life's end.</summary>
        private static ScreenCard Card(ExplorationSession session)
        {
            var state = session.State;
            int layer = state.Profile.Layer;

            if (session.HasPendingBattle)
            {
                var kind = state.Map.Node(session.PendingBattleNode).Kind;
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

            if (session.HasPendingStep)
            {
                return new ScreenCard(CardKind.ConfirmStep, "瘴気が 100% に達します",
                    new[] { "このノードへ入ると、瘴気死でこの生が終わります。" },
                    new[]
                    {
                        new CardButton("それでも進む", ScreenAction.ConfirmStep),
                        new CardButton("やめる", ScreenAction.Cancel),
                    });
            }

            if (session.ConfirmingDescend)
            {
                return new ScreenCard(CardKind.ConfirmDescend, "瘴気が 100% に達します",
                    new[] { "次の層へ降りると、入口で瘴気が 100% に達し、この生が終わります。" },
                    new[]
                    {
                        new CardButton("それでも降りる", ScreenAction.ConfirmDescend),
                        new CardButton("やめる", ScreenAction.Cancel),
                    });
            }

            if (session.ConfirmingEndLife)
            {
                return new ScreenCard(CardKind.ConfirmEndLife, "この生を終えますか",
                    new[]
                    {
                        "生きたまま次の生へ移ります。手記は丸ごと次の生へ渡ります。",
                        $"いまの到達: 第 {layer} 層、瘴気 {state.MiasmaPercent}%",
                    },
                    new[]
                    {
                        new CardButton("この生を終える", ScreenAction.ConfirmEndLife),
                        new CardButton("やめる", ScreenAction.Cancel),
                    });
            }

            // What the interlude will give, tried on the state the way a step's forecast is.
            string interludeLine = state.Phase == RunPhase.LayerCleared || state.Phase == RunPhase.PushedOut
                ? $"階層間の休憩: HP +{ExplorationReducer.Interlude(state).Hp - state.Hp}" +
                  $"（最大の {ExplorationReducer.InterludeHpPercent}%）・スタミナ全回復・瘴気はたまらない"
                : "";
            var toInterlude = new[] { new CardButton("階層間の休憩へ", ScreenAction.GoToInterlude) };

            switch (state.Phase)
            {
                case RunPhase.LayerCleared:
                    return new ScreenCard(CardKind.LayerCleared, "階層ボスを倒した",
                        new[] { $"第 {layer} 層を抜けます。", interludeLine }, toInterlude);

                case RunPhase.PushedOut:
                    return new ScreenCard(CardKind.PushedOut, "刻限が尽きた",
                        new[]
                        {
                            state.Profile.IsLast
                                ? "この層の行動は終わりです。これより下の層はありません。"
                                : "この層の行動は終わりです。階層間の休憩を経て、次の層へ押し出されます。",
                            interludeLine,
                        },
                        toInterlude);

                case RunPhase.Interlude:
                    return InterludeCard(session);

                case RunPhase.MiasmaDeath:
                    return EndCard(CardKind.MiasmaDeath, "瘴気死", $"瘴気が 100% に達し、第 {layer} 層でこの生が終わった。", state);

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

        private static ScreenCard InterludeCard(ExplorationSession session)
        {
            var state = session.State;
            var lines = new List<string>
            {
                $"HP +{session.InterludeHealed}（最大の {ExplorationReducer.InterludeHpPercent}%）で HP {state.Hp} / {state.MaxHp}",
                $"スタミナが全回復した（{state.Stamina} / {state.MaxStamina}）",
                $"瘴気はたまらない（{state.MiasmaPercent}%）",
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
                lines.Add($"次は第 {next.Layer} 層　{next.Name}: 1 歩ごとに瘴気 +{density}%、刻限 {next.TimeLimit}");
                if (session.TryDescend().Phase == RunPhase.MiasmaDeath)
                    lines.Add("！ 降りると入口で瘴気が 100% に達し、この生が終わります");
                buttons.Add(new CardButton("次の層へ降りる", ScreenAction.Descend));
            }
            buttons.Add(new CardButton("この生を終える", ScreenAction.AskEndLife));
            return new ScreenCard(CardKind.Interlude, "階層間の休憩", lines, buttons);
        }

        private static ScreenCard EndCard(CardKind kind, string title, string line, ExplorationState state) =>
            new ScreenCard(kind, title,
                new[] { line, $"瘴気 {state.MiasmaPercent}% ／ HP {state.Hp} / {state.MaxHp}" },
                new[] { new CardButton("新しい生で潜り直す", ScreenAction.NewLife) });

        /// <summary>The journal the drawer shows while exploring (dungeon_exploration_v4.md §6.7).</summary>
        private static JournalBook Book(ExplorationSession session)
        {
            var state = session.State;
            int layer = state.Profile.Layer;
            var pages = new List<JournalPage>();

            var enemies = session.Intel.Pages(layer, session.Surveyed);
            if (enemies.Count > 0)
            {
                pages.AddRange(enemies);
            }
            else
            {
                pages.Add(new JournalPage("enemy-none", JournalTab.Enemy, "敵の頁",
                    new[] { new JournalRow("", "敵の頁はまだありません。遭遇すると開示度 1 の頁ができます。") },
                    disclosure: 0));
            }

            var profile = state.Profile;
            int relief = state.Loadout.DensityRelief;
            string density = $"1 刻限ごとに +{state.EffectiveDensity}%";
            if (relief > 0) density += $"（層の濃さ {profile.Density} − ツール {relief}）";

            var counts = System.Enum.GetValues(typeof(NodeKind)).Cast<NodeKind>()
                .Where(k => state.Map.CountOf(k) > 0)
                .Select(k => $"{ExplorationText.KindLabel(k)} {state.Map.CountOf(k)}");

            pages.Add(new JournalPage($"dungeon-{layer}", JournalTab.Dungeon,
                ExplorationText.LayerTitle(layer, profile.DisplayName(session.CarvingTaken)),
                new[]
                {
                    new JournalRow("瘴気の濃さ", density),
                    new JournalRow("刻限", $"{profile.TimeLimit}（寄り道なしの踏破は {state.Map.ShortestPathCost}）"),
                    new JournalRow("ノード", string.Join("・", counts)),
                    new JournalRow("情報収集", session.Surveyed ? "済ませた。この層の敵 1 種が開示度 2 になった" : "まだ"),
                }));

            pages.Add(new JournalPage("memo", JournalTab.Memo, "メモ",
                new[] { new JournalRow("", "メモはまだありません。") }));

            return new JournalBook(pages, JournalContext.Exploration);
        }
    }
}
