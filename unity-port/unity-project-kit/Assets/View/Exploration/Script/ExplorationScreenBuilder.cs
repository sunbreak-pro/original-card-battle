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
    public static partial class ExplorationScreenBuilder
    {
        public static ExplorationScreen Build(ExplorationSession session)
        {
            var state = session.State;
            var map = state.Map;
            bool mapLive = session.MapIsLive;

            var nodes = new List<NodeCard>();
            foreach (var node in map.Nodes.OrderBy(n => n.Id))
            {
                var standing = StandingOf(state, node.Id);
                int rowWidth = map.Row(node.Row).Count;
                float x = (node.Column + 0.5f) / rowWidth;
                float y = map.RowCount == 1 ? 0.5f : 1f - (float)node.Row / (map.RowCount - 1);

                var lines = standing == NodeStanding.Far ? new string[0] : ContentLines(session, node);
                // Trial extension (js: ExplorationScreenBuilder.js:692): while a 標の石 is aimed, the nodes it can land
                // on take its jump instead.
                var jumps = mapLive && session.IsAiming ? JumpOptions(session, node, session.AimingSlot, standing) : null;
                var options = jumps ?? (mapLive ? Options(state, node, standing, ArrivalEvent(session, node)) : new StepOption[0]);
                string note = jumps != null && jumps.Count > 0 ? ExplorationText.JumpNote : NoteOf(standing);
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

            // Trial extension (js: ExplorationScreenBuilder.js:717): 刻限の砂 and 防瘴の面 shown where their numbers are
            // (the band), and the aim's hint. state.TimeLimit is the profile's outside trial.
            string timeBonusLabel = state.TimeBonus > 0
                ? ExplorationText.TimeBonus(ToolNames(state.Loadout, ReliefKind.Time), state.TimeBonus) : "";
            string reliefLabel = session.Trial && state.Loadout.DensityRelief > 0 ? ReliefLabel(session) : "";
            string hint = !mapLive ? "" : session.IsAiming ? ExplorationText.AimHint
                : "明るい枠のノードへ進めます。踏んだノードへは刻限を使わずに移れます。";

            return new ExplorationScreen(
                ExplorationText.LayerTitle(state.Profile.Layer, state.Profile.DisplayName(session.CarvingTaken)),
                $"刻限 {state.TimeLeft} / {state.TimeLimit}",
                state.TimeLeft,
                state.TimeLimit,
                Gauge(session),
                $"HP {state.Hp} / {state.MaxHp}",
                $"スタミナ {state.Stamina} / {state.MaxStamina}",
                MaxStaminaLabel(state),
                $"訓練の刻み {state.TrainingMarks}",
                nodes,
                edges,
                state.CurrentNodeId,
                hint,
                ToolsLabel(state.Loadout),
                Slots(session),
                session.CanEndLife,
                !state.IsOver,
                Card(session),
                Book(session),
                session.OpenOverlay,
                session.CanOpen(Overlay.Cards),
                session.CanOpen(Overlay.Settings),
                CardsPage,
                SettingsPage,
                ToolCards(session),
                timeBonusLabel,
                reliefLabel,
                session.IsAiming,
                session.Events);
        }

        /// <summary>
        /// 所持カード (CAMP_FACILITIES_DESIGN.md §3.4): a stand-in until #244's deck screen lends its
        /// parts and the run carries the character's cards.
        /// </summary>
        private static readonly OverlayPage CardsPage = new OverlayPage("所持カード（仮）",
            new[]
            {
                "今のキャラクターが持つカードの、現在の名称・習熟度・永続能力を読む画面です。",
                "読むだけで、戦闘デッキは組み替えられません。",
                "",
                "カードの一覧はまだつながっていません。",
            },
            "閉じる（C）");

        /// <summary>
        /// 設定・セーブ (CAMP_FACILITIES_DESIGN.md §3.5): the items it will hold, none of them wired yet.
        /// 「保存してゲームを閉じる」 saves without ending the life; 「この生を終える」 is a different button.
        /// </summary>
        private static readonly OverlayPage SettingsPage = new OverlayPage("設定・セーブ（仮）",
            new[]
            {
                "戦闘の速さ: ×1.0 / ×1.25 / ×1.5（既定は ×1.25）",
                "演出短縮: 入れる / 切る",
                "音の大きさ",
                "保存してゲームを閉じる: 今の状態を保存して閉じる。生は終わらない",
                "",
                "設定と保存はまだつながっていません。いまは開け閉めだけができます。",
            },
            "閉じる（Esc）");

        private static NodeStanding StandingOf(ExplorationState state, int id)
        {
            if (id == state.CurrentNodeId) return NodeStanding.Current;
            bool resolved = state.HasResolved(id);
            if (ExplorationReducer.CanStep(state, id)) return resolved ? NodeStanding.Revisit : NodeStanding.Reachable;
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
                case NodeKind kind when kind.IsCombat():
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
                {
                    // Trial extension (js: ExplorationScreenBuilder.js:141): the HP the rest would give now, marked when the
                    // maximum stops it (「HP +4（最大の 15%・上限まで）」); the C# prints the share.
                    int share = ExplorationReducer.RestHeal(state.MaxHp);
                    int gain = session.Trial ? System.Math.Min(share, System.Math.Max(0, state.MaxHp - state.Hp)) : share;
                    lines.Add($"休む: HP +{gain}（最大の {ExplorationReducer.RestHpPercent}%{(gain < share ? ExplorationText.CappedMark : "")}）・" +
                              $"スタミナ全回復・最大スタミナ +{ExplorationReducer.RestMaxStaminaBonus}");
                    lines.Add("訓練する: 選んだ 1 枚に習熟の刻み 1");
                    lines.Add("どちらか 1 つだけを選ぶ");
                    break;
                }

                case NodeKind.Survey:
                    lines.Add("この層の敵 1 種の開示度が 2 段目へ上がる");
                    // Trial extension (js: ExplorationScreenBuilder.js:156): once done, the node says which enemy it
                    // raised (SurveyLine), not only that it was done.
                    if (session.Surveyed) lines.Add(SurveyLine(session) ?? "この層では済ませた");
                    // Trial extension: 写しの帳 (tools_and_consumables_v4.md §2.2).
                    if (session.Trial && state.Loadout.ChoosesSurvey && !session.Surveyed) lines.Add(ExplorationText.SurveyChooseLine);
                    break;

                case NodeKind.Event:
                    if (session.Trial) lines.AddRange(TrialEventLines(session, node));
                    else lines.Add("泉 / 重い宝箱 / 特殊な食事 のどれかが起きる");
                    break;

                case NodeKind.Trace:
                    // Trial extension (js: ExplorationScreenBuilder.js:169): once this life collected the layer's trace
                    // (the Expedition's Inherit sets InheritedTraceId / InheritedLayer), the node says so.
                    lines.Add(session.Trial && session.Keeper != null && session.Keeper.InheritedTraceId != null &&
                        session.Keeper.InheritedLayer == state.Profile.Layer ? ExplorationText.TraceCollected : "前の生の遺産を受け取れる");
                    lines.Add("受け取れるのは 1 つの生で 1 件");
                    break;

                case NodeKind.Carving:
                    lines.Add(session.CarvingTaken ? "読んだ。この層の名前が分かった" : "この層の名前が分かる");
                    lines.Add("戦闘には効かない");
                    break;
            }
            return lines;
        }

        // js: bridge/explorationScript/ExplorationScreenBuilder.js:242
        private static IReadOnlyList<StepOption> Options(ExplorationState state, MapNode node, NodeStanding standing, EventKind? arrivalEvent = null)
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
            return new[] { new StepOption("進む", RestChoice.Rest, Forecast(state, node.Id, RestChoice.Rest, -1, arrivalEvent)) };
        }

        /// <summary>
        /// The step tried on the current state, told as before → after (dungeon_exploration_v4.md §6.4).
        /// Trial extension (js: ExplorationScreenBuilder.js:769): with <paramref name="jumpSlot"/> (≥ 0) it is 標の石's
        /// jump from that slot instead of a step; with <paramref name="arrivalEvent"/> the event the node resolves on
        /// arrival is counted too.
        /// </summary>
        public static StepForecast Forecast(ExplorationState before, int nodeId, RestChoice choice, int jumpSlot = -1, EventKind? arrivalEvent = null)
        {
            var after = jumpSlot >= 0
                ? ExplorationReducer.Teleport(before, jumpSlot, nodeId, choice)
                : ExplorationReducer.Step(before, nodeId, choice);
            // Trial extension: a 泉 heals on arrival, so its step's forecast counts the HP.
            if (arrivalEvent != null && !after.IsOver) after = ExplorationReducer.ResolveEvent(after, arrivalEvent.Value, true);

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
                // Trial extension (js: ExplorationScreenBuilder.js:273): the label says what a step of the next layer adds
                // — its density, as the map's 「次の 1 歩で +N%」 does — not the gap to the 100% cap.
                forecast = $"降りると +{(session.Trial ? below.EffectiveDensity : below.MiasmaPercent - percent)}%";
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
            if (session.Trial) return TrialSlots(session); // Trial extension
            var carried = session.State.Loadout.Consumables;
            var slots = new List<SlotCard>();
            for (int i = 0; i < RunLoadout.ConsumableSlots; i++)
            {
                if (i < carried.Count)
                {
                    string id = carried[i].Id;
                    slots.Add(new SlotCard(i, ExplorationText.ItemName(id), ExplorationText.ItemNote(id), session.CanUseConsumable(i)));
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

            // Trial extension (js: ExplorationScreenBuilder.js:510): an event, a survey pick or a swap waits before the
            // layer's end.
            if (session.Trial)
            {
                var trial = TrialCard(session);
                if (trial != null) return trial;
            }

            // What the interlude will give, tried on the state the way a step's forecast is.
            int interludeGain = ExplorationReducer.CanInterlude(state) ? ExplorationReducer.Interlude(state).Hp - state.Hp : 0;
            string interludeLine = ExplorationReducer.CanInterlude(state)
                ? $"階層間の休憩: HP +{interludeGain}" +
                  $"{InterludeShare(state, interludeGain < ExplorationReducer.InterludeHealOf(state))}・スタミナ全回復・瘴気はたまらない"
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
                    return InterludeCardOf(session);

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
                lines.Add($"次は第 {next.Layer} 層　{next.DisplayName(session.CarvingTaken)}: 1 歩ごとに瘴気 +{density}%、刻限 {time}");
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
            // Trial extension (js: ExplorationScreenBuilder.js:634): the density stops at its floor (seven_layers_v4.md
            // §3.2 「濃度は 1 より下がりません」), so a subtraction that would go below it says so.
            if (relief > 0 && session.Trial && profile.Density - relief < state.EffectiveDensity)
                density += $"（{ExplorationText.ReliefFloor(profile.Density, relief, state.EffectiveDensity)}）";
            else if (relief > 0) density += $"（層の濃さ {profile.Density} − ツール {relief}）";

            var counts = System.Enum.GetValues(typeof(NodeKind)).Cast<NodeKind>()
                .Where(k => state.Map.CountOf(k) > 0)
                .Select(k => $"{ExplorationText.KindLabel(k)} {state.Map.CountOf(k)}");

            var rows = new List<JournalRow>
            {
                new JournalRow("瘴気の濃さ", density),
                new JournalRow("刻限", $"{state.TimeLimit}（寄り道なしの踏破は {state.Map.ShortestPathCost}）"),
                new JournalRow("ノード", string.Join("・", counts)),
                new JournalRow("情報収集", session.Surveyed ? SurveyLine(session) ?? "済ませた。この層の敵 1 種が開示度 2 になった" : "まだ"),
            };
            // Trial extension (js: ExplorationScreenBuilder.js:650): the events of this layer (encounters_and_rewards_v4.md
            // §5: 「起きたイベントは、手記のダンジョンの頁に…残します」).
            if (session.Trial && state.Map.CountOf(NodeKind.Event) > 0)
            {
                var here = session.Events.Where(e => e.Layer == layer).ToList();
                rows.Add(new JournalRow(ExplorationText.EventsRow, here.Count == 0
                    ? ExplorationText.NoEvents
                    : string.Join(" ／ ", here.Select(e => ExplorationText.EventRecord(e.Name, e.Accepted, e.Summary)))));
            }
            pages.Add(new JournalPage($"dungeon-{layer}", JournalTab.Dungeon,
                ExplorationText.LayerTitle(layer, profile.DisplayName(session.CarvingTaken)), rows));
            // Trial extension (js: ExplorationScreenBuilder.js:661): reading the layer-two carving adds a page
            // (seven_layers_v4.md §5 「手記に頁が 1 つ増える」); a life that knows the name from an earlier life has it too.
            if (session.Trial && session.CarvingTaken)
            {
                pages.Add(new JournalPage("carving", JournalTab.Dungeon, ExplorationText.CarvingPageTitle,
                    ExplorationText.CarvingPageRows.Select(row => new JournalRow(row.Label, row.Text)).ToList()));
            }

            pages.Add(new JournalPage("memo", JournalTab.Memo, "メモ",
                new[] { new JournalRow("", "メモはまだありません。") }));

            pages.Add(JournalTerms.Page());

            return new JournalBook(pages, JournalContext.Exploration);
        }
    }
}
