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
            bool mapLive = session.MapIsLive;
            var nodes = NodePanels(session);

            // Trial extension (js: ExplorationScreenBuilder.js:717): the aim's hint.
            string hint = !mapLive ? "" : session.IsAiming ? ExplorationText.AimHint
                : "明るい枠のノードへ進めます。踏んだノードへは日数を使わずに移れます。";
            var (nextDays, daysForecast) = DaysAhead(session);

            return new ExplorationScreen
            {
                LayerTitle = CalendarText.LayerTitle(state.Profile, session.CarvingTaken),
                Layer = state.Profile.Layer,
                BackdropLayer = state.Profile.BackdropLayer,
                DaysLeft = state.DaysLeft,
                DaysLabel = CalendarText.DaysLabel(state.DaysLeft),
                CalendarFrozen = state.Calendar.IsFrozen,
                CalendarNote = CalendarText.Note(state.Calendar),
                NextDaysLeft = nextDays,
                DaysForecastLabel = daysForecast,
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
                Aiming = session.IsAiming,
                Events = session.Events,
            };
        }

        private static List<NodePanel> NodePanels(ExplorationSession session)
        {
            var state = session.State;
            var map = state.Map;
            bool mapLive = session.MapIsLive;

            var nodes = new List<NodePanel>();
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
                nodes.Add(new NodePanel(node)
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

        private static List<EdgeLine> Edges(ExplorationState state, IReadOnlyList<NodePanel> nodes)
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
            NodeStanding.Reachable => "入ると暦が 1 日進む",
            NodeStanding.Revisit => "踏んだノード。日数を使わずに移れる",
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

        private static bool Lit(ExplorationState state, IReadOnlyList<NodePanel> nodes, int from, int to)
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
                    if (node.Kind == NodeKind.Boss && state.Profile.IsKingLayer)
                    {
                        // seven_layers_v4.md §2.2: the days the king's fall adds, net of the kings already down.
                        lines.Add("倒すと王が倒れ、戻るまで暦が止まる");
                        lines.Add(CalendarText.Extension(state.Calendar.ExtensionIfKingDown(state.Region)));
                    }
                    else if (node.Kind == NodeKind.Boss)
                    {
                        lines.Add("倒すと階層間の休憩へ進む");
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

            var lines = new List<string> { CalendarText.ForecastLine(before, after) };
            if (after.MaxStamina != before.MaxStamina)
                lines.Add($"最大スタミナ {ExplorationText.Change(before.MaxStamina, after.MaxStamina)}");
            if (after.Stamina != before.Stamina)
                lines.Add($"スタミナ {ExplorationText.Change(before.Stamina, after.Stamina)}");
            if (after.Hp != before.Hp)
                lines.Add($"HP {ExplorationText.Change(before.Hp, after.Hp)}");

            // dungeon_exploration_v4.md §6.4: the one warning left is the calendar running out on this node.
            var warnings = new List<string>();
            if (after.Phase == RunPhase.Expired) warnings.Add(CalendarText.ExpiryWarning);
            return new StepForecast(before, after, lines, warnings);
        }

        /// <summary>
        /// The days left after the next day the run can spend, tried on the core the way a step's forecast is: the
        /// first new node next door while the layer is explored, the descent at the interlude. The band draws that day
        /// ahead of time (§6.4) and says what it costs.
        /// </summary>
        private static (int NextDays, string Label) DaysAhead(ExplorationSession session)
        {
            var state = session.State;
            int now = state.DaysLeft;
            if (state.Phase == RunPhase.Exploring)
            {
                var ahead = state.ReachableUnresolved();
                if (ahead.Count == 0) return (now, "");
                int after = ExplorationReducer.Step(state, ahead[0]).DaysLeft;
                if (after < now) return (after, CalendarText.StepCosts(now - after));
                return (now, state.Calendar.IsFrozen ? "" : CalendarText.DayFree);
            }
            if (session.TryDescend() is ExplorationState below)
            {
                // The next layer's entry node is resolved on arrival, so descending costs its day at once (§6.5).
                if (below.DaysLeft < now) return (below.DaysLeft, CalendarText.DescendCosts(now - below.DaysLeft));
                return (now, state.Calendar.IsFrozen ? "" : CalendarText.DescendFree);
            }
            return (now, "");
        }

        /// <summary>「最大 12 = 10 + 一時 2」: the base and the temporary changes, or the maximum alone.</summary>
        private static string MaxStaminaLabel(ExplorationState state)
        {
            int temp = state.TempMaxStaminaMod;
            int max = state.MaxStamina;

            // The 3..14 clamp can break the sum; then only the maximum itself is honest to print.
            if (temp == 0 || StaminaRules.BaseMaxStamina + temp != max) return $"最大 {max}";
            return temp > 0
                ? $"最大 {max} = {StaminaRules.BaseMaxStamina} + 一時 {temp}"
                : $"最大 {max} = {StaminaRules.BaseMaxStamina} − 一時 {-temp}";
        }

        private static string ToolsLabel(RunLoadout loadout)
        {
            string names = loadout.Tools.Count == 0
                ? "なし"
                : string.Join("・", loadout.Tools.Select(t => ExplorationText.ItemName(t.Id)));
            return $"ツール: {names}（{loadout.Tools.Count} / {loadout.ToolSlotCount}）";
        }

        private static IReadOnlyList<SlotPanel> Slots(ExplorationSession session)
        {
            if (session.Trial) return TrialSlots(session); // Trial extension
            var carried = session.State.Loadout.Consumables;
            var slots = new List<SlotPanel>();
            for (int i = 0; i < RunLoadout.ConsumableSlots; i++)
            {
                if (i < carried.Count)
                {
                    string id = carried[i].Id;
                    slots.Add(new SlotPanel(i, ExplorationText.ItemName(id), ExplorationText.ItemNote(id), session.CanUseConsumable(i)));
                }
                else
                {
                    slots.Add(new SlotPanel(i, ExplorationText.EmptySlot, "", false));
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

            var counts = System.Enum.GetValues(typeof(NodeKind)).Cast<NodeKind>()
                .Where(k => state.Map.CountOf(k) > 0)
                .Select(k => $"{ExplorationText.KindLabel(k)} {state.Map.CountOf(k)}");

            // #107: a journal that keeps more than the enemies adds the layer's dungeon entries and the memo.
            var more = session.Intel as IJournalPages;
            var rows = new List<JournalRow>
            {
                new JournalRow("領域", profile.Region == Region.Common ? Region.Common.Label() : CalendarText.RegionName(profile.Region)),
                new JournalRow("日数", $"寄り道なしで抜けると {state.Map.ShortestPathCost} 日"),
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
                CalendarText.LayerTitle(profile, session.CarvingTaken), rows));
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
