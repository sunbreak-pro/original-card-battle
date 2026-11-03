// Turns a session into one frame of the exploration screen (dungeon_exploration_v4.md §6). Every
// number comes from the exploration core. The forecast of a step is ExplorationReducer.Step tried on
// the current state — the state is immutable, so trying it changes nothing — which is why the
// forecast and the step taken can never disagree.
using System.Collections.Generic;
using System.Linq;
using Backdrop;
using DungeonCore;
using Journal;

namespace Exploration
{
    public static partial class ExplorationScreenBuilder
    {
        public static ExplorationScreen Build(ExplorationSession session)
        {
            var state = session.State;
            bool mapLive = session.MapIsLive;
            var nodes = NodeCards(session);

            // Trial extension (js: ExplorationScreenBuilder.js:717): 刻限の砂 and 防瘴の面 shown where their numbers are
            // (the band), and the aim's hint. state.TimeLimit is the profile's outside trial.
            string timeBonusLabel = state.TimeBonus > 0
                ? ExplorationText.TimeBonus(ToolNames(state.Loadout, ReliefKind.Time), state.TimeBonus) : "";
            string reliefLabel = session.Trial && state.Loadout.DensityRelief > 0 ? ReliefLabel(session) : "";
            string hint = !mapLive ? "" : session.IsAiming ? ExplorationText.AimHint
                : "明るい枠のノードへ進めます。踏んだノードへは刻限を使わずに移れます。";

            return new ExplorationScreen
            {
                LayerTitle = ExplorationText.LayerTitle(state.Profile.Layer, state.Profile.DisplayName(session.CarvingTaken)),
                Layer = state.Profile.Layer,
                TimeLabel = $"刻限 {state.TimeLeft} / {state.TimeLimit}",
                TimeLeft = state.TimeLeft,
                TimeLimit = state.TimeLimit,
                Miasma = Gauge(session),
                HpLabel = $"HP {state.Hp} / {state.MaxHp}",
                StaminaLabel = $"スタミナ {state.Stamina} / {state.MaxStamina}",
                MaxStaminaLabel = MaxStaminaLabel(state),
                MarksLabel = $"訓練の刻み {state.TrainingMarks}",
                Nodes = nodes,
                Edges = Edges(state, nodes),
                CurrentNodeId = state.CurrentNodeId,
                Hint = hint,
                ToolsLabel = ToolsLabel(state.Loadout),
                Consumables = Slots(session),
                CanEndLife = session.CanEndLife,
                ShowsEndLife = !state.IsOver,
                Card = Card(session),
                Journal = Book(session),
                OpenOverlay = session.OpenOverlay,
                CanOpenCards = session.CanOpen(Overlay.Cards),
                CanOpenSettings = session.CanOpen(Overlay.Settings),
                Cards = CardsPage,
                Settings = SettingsPage,
                ToolCards = ToolCards(session),
                TimeBonusLabel = timeBonusLabel,
                ReliefLabel = reliefLabel,
                Aiming = session.IsAiming,
                Events = session.Events,
            };
        }

        private static List<NodeCard> NodeCards(ExplorationSession session)
        {
            var state = session.State;
            var map = state.Map;
            bool mapLive = session.MapIsLive;

            var nodes = new List<NodeCard>();
            foreach (var node in map.Nodes.OrderBy(n => n.Id))
            {
                var standing = StandingOf(state, node.Id);
                int rowWidth = map.Row(node.Row).Count;

                var lines = standing == NodeStanding.Far ? new string[0] : ContentLines(session, node);
                // Trial extension (js: ExplorationScreenBuilder.js:692): while a 標の石 is aimed, the nodes it can land
                // on take its jump instead.
                var jumps = mapLive && session.IsAiming ? JumpOptions(session, node, session.AimingSlot, standing) : null;
                var options = jumps ?? (mapLive ? Options(state, node, standing, ArrivalEvent(session, node)) : new StepOption[0]);
                string note = jumps != null && jumps.Count > 0 ? ExplorationText.JumpNote : NoteOf(standing);
                nodes.Add(new NodeCard(node)
                {
                    X = (node.Column + 0.5f) / rowWidth,
                    Y = map.RowCount == 1 ? 0.5f : 1f - (float)node.Row / (map.RowCount - 1),
                    Glyph = ExplorationText.Glyph(node.Kind),
                    KindLabel = ExplorationText.KindLabel(node.Kind),
                    Standing = standing,
                    Lines = lines,
                    Options = options,
                    Note = note,
                    Title = $"{ExplorationText.Glyph(node.Kind)}　{ExplorationText.KindLabel(node.Kind)}",
                    Detail = Detail(note, lines, options),
                });
            }
            return nodes;
        }

        private static List<EdgeLine> Edges(ExplorationState state, IReadOnlyList<NodeCard> nodes)
        {
            var edges = new List<EdgeLine>();
            foreach (var node in state.Map.Nodes.OrderBy(n => n.Id))
            {
                foreach (int to in state.Map.Successors(node.Id).OrderBy(id => id))
                {
                    edges.Add(new EdgeLine(node.Id, to, Lit(state, nodes, node.Id, to)));
                }
            }
            return edges;
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
                        lines.Add($"敵: {intel.EnemyName}（{JournalPage.DisclosureText(intel.Disclosure)}）");
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
                    if (session.ResolvesEvents) lines.AddRange(TrialEventLines(session, node));
                    else lines.Add("泉 / 重い宝箱 / 特殊な食事 のどれかが起きる");
                    break;

                case NodeKind.Trace:
                    if (!session.Trial)
                    {
                        lines.AddRange(TraceLines(session));
                        break;
                    }
                    // Trial extension (js: ExplorationScreenBuilder.js:169): once this life collected the layer's trace
                    // (the Expedition's Inherit sets InheritedTraceId / InheritedLayer), the node says so.
                    lines.Add(session.Keeper != null && session.Keeper.InheritedTraceId != null &&
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

        /// <summary>
        /// What a 死亡地点の痕跡 promises, read from the ledger (dungeon_exploration_v4.md §6.3, §7.5).
        /// </summary>
        private static IEnumerable<string> TraceLines(ExplorationSession session)
        {
            TraceLedger ledger = session.Traces;
            if (ledger.HasReceived)
            {
                yield return "この生は既に遺産を受け取った";
                yield break;
            }
            if (ledger.Pending != null)
            {
                yield return ledger.Pending.Place.Layer == session.State.Profile.Layer
                    ? "この生が受け取る痕跡はここ"
                    : "この生が受け取る痕跡は既に決まっている";
                yield break;
            }
            yield return "前の生の遺産が残っている";
            yield return "最初に踏んだ痕跡だけを受け取れる（1 つの生で 1 件）";
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
            if (arrivalEvent != null && !after.IsOver) after = ExplorationReducer.ApplyEvent(after, arrivalEvent.Value, true);

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
                warnings.Add($"ここで瘴気が {Miasma.DeathAt}% に達し、この生が終わります");
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
            // battle-visual-v1 §2.5: a step whose share ahead is drawn in 注意 (a 20% line crossed, or
            // death reached) says so with △ too. Only this forecast of the next step: a 休息 that crosses
            // a line may still lift max stamina, which StepForecast judges on the maximum itself.
            if (forecast.Length > 0) forecast = MiasmaLook.ForecastMark(percent, next) + forecast;

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

            // #107: a journal that keeps more than the enemies adds the layer's dungeon entries and the memo.
            var more = session.Intel as IJournalPages;
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
            rows.AddRange(more?.DungeonRows(layer) ?? System.Array.Empty<JournalRow>());
            pages.Add(new JournalPage($"dungeon-{layer}", JournalTab.Dungeon,
                ExplorationText.LayerTitle(layer, profile.DisplayName(session.CarvingTaken)), rows));
            // Trial extension (js: ExplorationScreenBuilder.js:661): reading the layer-two carving adds a page
            // (seven_layers_v4.md §5 「手記に頁が 1 つ増える」); a life that knows the name from an earlier life has it too.
            if (session.Trial && session.CarvingTaken)
            {
                pages.Add(new JournalPage("carving", JournalTab.Dungeon, ExplorationText.CarvingPageTitle,
                    ExplorationText.CarvingPageRows.Select(row => new JournalRow(row.Label, row.Text)).ToList()));
            }

            pages.Add(more?.MemoPage() ?? new JournalPage("memo", JournalTab.Memo, "メモ",
                new[] { new JournalRow("", "メモはまだありません。") }));

            pages.Add(JournalTerms.Page());

            return new JournalBook(pages, JournalContext.Exploration);
        }
    }
}
