// The recorded calls the long walks of LifeReplayEndToEndTests never make (習熟訓練's card, the band's overlays, Esc,
// 「この生を終える」 taken back, 標の石's aim put away, a battle dropped unfought, 俊敏's free cell), each made through the
// recorded facades with the save written after every call (the app's PersistSoon), and every saved point loaded again
// (Life.BeginLife, or the replay itself for the free cell's custom deck): the life, the screen and the fight in progress
// stand where they stood when the point was written.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using Depiction;
using Depiction.Bridge;
using DungeonCore;
using Exploration;
using Exploration.Bridge;
using InheritanceCore;
using NUnit.Framework;
using static Trial.Bridge.Tests.LifeTestKit;

namespace Trial.Bridge.Tests
{
    public sealed class RecordedCallsReplayTests
    {
        /// <summary>One saved point: the log as stored, and what the life and the fight stood on.</summary>
        private sealed class Point
        {
            public string Label;
            public LifeLog Log;
            public string State;
            public string Fight;
        }

        /// <summary>The life being played, its saved points and the calls it made.</summary>
        private sealed class Play
        {
            public LiveLife Live;
            public RecordedSource Fighting;
            public Point Started;
            public readonly List<Point> Points = new List<Point>();
            public readonly SortedSet<string> Seen = new SortedSet<string>(StringComparer.Ordinal);

            /// <summary>The recorder's onCall: the save is written after every logged call.</summary>
            public void OnCall(string tag, string method)
            {
                LogEntry last = Live.Log.Last;
                string what = tag + "." + method;
                if (method == "Act") what += ":" + last.Args[0];
                if (method == "Step") what += ":" + last.Args[1];
                Seen.Add(what);
                if ((tag == "B" && method == "Finish") || (tag == "E" && method == "Abandon")) Fighting = null;
                var p = new Point
                {
                    Label = what,
                    Log = Saved(Live.Log),
                    State = Snap(Live.Raw),
                    Fight = Fighting == null ? null : BattleSnap(Fighting),
                };
                Points.Add(p);
                // B.StartNext is saved inside the call: its source is the caller's once it returns (Begin fills it).
                if (what == "B.StartNext") Started = p;
            }

            /// <summary>A fight's source is in hand: the point its B.StartNext saved stands on it.</summary>
            public void Begin(RecordedSource src)
            {
                Fighting = src;
                Started.Fight = BattleSnap(src);
            }
        }

        /// <summary>Every fight of the waiting encounter, settled at its decision; false when the heir fell.</summary>
        private static bool FightThrough(Play play)
        {
            RecordedExpedition ex = play.Live.Expedition;
            RecordedBattle battle = ex.Battle ?? ex.StartEncounter();
            while (!battle.Over)
            {
                RecordedSource src = battle.StartNext(false);
                play.Begin(src);
                SettlingFight fight = Recorder.SettleWhenDecided(battle, src);
                for (int g = 0; g < 20000 && !fight.Source.Finished; g++) PlayerStep(fight.Source);
                fight.Finish();
            }
            ex.Return();
            if (ex.Offer.Count > 0) ex.Learn(ex.Offer[0]);
            else if (ex.PendingResult != null) ex.CloseResult();
            return !play.Live.Raw.Session.State.IsOver;
        }

        /// <summary>The steps from one node to another (bosses passed by), or int.MaxValue when there is no way.</summary>
        private static int Distance(LayerMap map, int from, int to)
        {
            try
            {
                return RouteTo(map, from, to).Count;
            }
            catch (KeyNotFoundException)
            {
                return int.MaxValue;
            }
        }

        /// <summary>What the trial's cards ask after a step: an event declined, a find left, the survey's first enemy.</summary>
        private static void AnswerCards(RecordedSession s)
        {
            if (s.HasPendingEvent) s.Act(ScreenAction.DeclineEvent);
            if (s.HasPendingFound) s.Act(ScreenAction.LeaveFound);
            if (s.HasPendingSurvey) s.Act(ScreenAction.ChooseSurvey, s.SurveyChoices[0]);
        }

        /// <summary>
        /// One life of lineage <paramref name="seed"/>: the entry battle dropped unfought once and then won, the walk to a
        /// rest taken as 訓練する, the training card put off, opened again from the band and spent, the overlays and Esc,
        /// 「この生を終える」 taken back, 標の石 aimed and put away. False when the heir falls before the story is told.
        /// </summary>
        private static Play Story(ulong seed, out LifeStartData start, out Lineage lineage)
        {
            LifeStart ls = Inheritance.StartLife(Inheritance.NewLineage(seed));
            lineage = ls.Lineage;
            start = Clone(Life.MakeStart(ls, Departure(ls, 2)));   // 標の石 among the consumables
            var play = new Play();
            play.Live = Life.BeginLife(start, new LifeLog(), "", null, ls.Lineage, play.OnCall);
            RecordedExpedition ex = play.Live.Expedition;
            RecordedSession s = ex.Session;

            // the entry battle: begun, then dropped unfought (the View found no battle scene), then fought
            RecordedBattle first = ex.StartEncounter();
            RecordedSource dropped = first.StartNext(false);
            play.Begin(dropped);
            for (int i = 0; i < 3; i++) PlayerStep(dropped);
            ex.Abandon();
            Assert.That(ex.Battle, Is.Null);
            Assert.That(s.HasPendingBattle, Is.True, "the node still waits on its battle");
            if (!FightThrough(play)) return null;

            // to the nearest rest of the layer, taken as 訓練する
            ExplorationState st = s.State;
            MapNode rest = st.Map.Nodes.Where(n => n.Kind == NodeKind.Rest && !st.HasResolved(n.Id))
                .OrderBy(n => Distance(st.Map, st.CurrentNodeId, n.Id)).FirstOrDefault();
            if (rest == null || Distance(st.Map, st.CurrentNodeId, rest.Id) == int.MaxValue) return null;
            for (int guard = 0; guard < 40 && s.State.CurrentNodeId != rest.Id; guard++)
            {
                int next = RouteTo(s.State.Map, s.State.CurrentNodeId, rest.Id).First();
                if (!s.Step(next, next == rest.Id ? RestChoice.Train : RestChoice.Rest)) return null;
                if (s.HasPendingStep) return null;   // the step would end the life
                AnswerCards(s);
                if (s.HasPendingBattle && !FightThrough(play)) return null;
                AnswerCards(s);
                if (s.State.IsOver) return null;
            }
            if (s.State.CurrentNodeId != rest.Id || !s.TrainingWaiting) return null;

            // the training card: 「あとで」, opened again from the band, the journal opened and closed over it, spent
            Assert.That(s.PutOffTraining(), Is.True);
            Assert.That(s.TrainingWaiting, Is.False);
            Assert.That(s.OpenTraining(), Is.True, "the mark is still on the band");
            Assert.That(s.Toggle(Overlay.Journal), Is.True);
            Assert.That(s.Toggle(Overlay.Journal), Is.True);
            Assert.That(ex.Train(ex.Deck[0].Def.Id), Is.True);
            Assert.That(s.TrainingWaiting, Is.False, "the last mark is spent");
            Assert.That(s.OpenTraining(), Is.False, "no mark left: refused, and logged all the same");

            // the band: 所持カード opened and closed with Esc; Esc again opens 設定・セーブ, the gear closes it
            Assert.That(s.Toggle(Overlay.Cards), Is.True);
            Assert.That(s.Escape(), Is.True);
            Assert.That(s.OpenOverlay, Is.EqualTo(Overlay.None));
            Assert.That(s.Escape(), Is.True);
            Assert.That(s.OpenOverlay, Is.EqualTo(Overlay.Settings));
            Assert.That(s.Toggle(Overlay.Settings), Is.True);

            // 「この生を終える」 asked, then taken back with Esc
            Assert.That(s.Act(ScreenAction.AskEndLife), Is.True);
            Assert.That(s.ConfirmingEndLife, Is.True);
            Assert.That(s.Escape(), Is.True);
            Assert.That(s.ConfirmingEndLife, Is.False);

            // 標の石 aimed and put away with Esc
            int slot = Enumerable.Range(0, s.State.Loadout.Consumables.Count)
                .FirstOrDefault(i => s.State.Loadout.Consumables[i].Kind == ReliefKind.Teleport && s.CanUseSlot(i), -1);
            if (slot < 0) return null;
            Assert.That(s.UseConsumable(slot), Is.True);
            Assert.That(s.IsAiming, Is.True);
            Assert.That(s.Escape(), Is.True);
            Assert.That(s.IsAiming, Is.False);
            return play;
        }

        [Test]
        public void TrainingTheBandEscAndADroppedBattle_AreSavedAtEveryCall_AndEveryPointLoadsBackWhereItStood()
        {
            Play play = null;
            LifeStartData start = null;
            Lineage lineage = null;
            ulong seed = 1;
            for (; seed <= 40 && play == null; seed++) play = Story(seed, out start, out lineage);
            Assert.That(play, Is.Not.Null, "some lineage in 1..40 tells the whole story");
            TestContext.Progress.WriteLine($"lineage {seed - 1}: {play.Points.Count} saved points; calls: {string.Join(" ", play.Seen)}");
            foreach (string call in new[]
            {
                "E.Abandon", "E.Train", "S.OpenTraining", "S.PutOffTraining", "S.Toggle", "S.Escape", "S.UseConsumable",
                "S.Step:Train", "S.Act:AskEndLife",
            })
            {
                Assert.That(play.Seen, Does.Contain(call), call + " was made");
            }

            int fights = 0;
            for (int i = 0; i < play.Points.Count; i++)
            {
                Point p = play.Points[i];
                string at = $"point {i} ({p.Label})";
                LiveLife back = Life.BeginLife(start, p.Log, "", null, lineage);
                Assert.That(Snap(back.Raw), Is.EqualTo(p.State), at + ": the life and its screen");
                Assert.That(back.OpenSource == null ? null : BattleSnap(back.OpenSource), Is.EqualTo(p.Fight), at + ": the fight in progress");
                Assert.That(back.Log.Count, Is.EqualTo(p.Log.Count), at + ": the log goes on from where it stood");
                if (p.Fight != null) fights++;
            }
            Assert.That(fights, Is.GreaterThan(3), "points inside a fight");
        }

        // ------------------------------------------------------------------ 俊敏's free cell

        /// <summary>
        /// A card of the test's own (as Depiction.Bridge.Tests' FreeStepTests): 俊敏 2 on the player. No card of the catalogue
        /// puts 俊敏 on the player yet, so the free cell is never open in a life the catalogue's decks play.
        /// </summary>
        private static readonly CardDef Quicken = new CardDef("quicken", "身軽", BattleAttribute.Skill, 1,
            new Face(Statuses: new[] { new StatusGrant(StatusKind.Swift, 2, true) }), null, TargetKind.Self, "");

        private static List<CardInstance> DeckWithQuicken()
        {
            List<CardInstance> deck = PrototypeDeck.Build().Take(17).ToList();
            for (int i = 0; i < 3; i++) deck.Add(new CardInstance("quicken-" + i, Quicken));
            return deck;
        }

        private static void ToPlayer(ITrialSource src)
        {
            for (int g = 0; g < 200 && !src.WaitingForPlayer && !src.Finished; g++) src.AdvanceAuto();
        }

        [Test]
        public void TheFreeCell_IsLoggedWithItsDirection_AndAFightSavedAroundItComesBackWhereItStood()
        {
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var log = new LifeLog();
                var points = new List<Point>();
                RecordedSource src = null;
                RecordedExpedition ex = Recorder.RecordExpedition(new Expedition(new RunStart { Seed = seed }, DeckWithQuicken()), log, (t, m) =>
                {
                    if (t == "B" && m == "Finish") src = null;
                    points.Add(new Point { Label = t + "." + m, Log = Saved(log), Fight = src == null ? null : BattleSnap(src) });
                });
                RecordedBattle battle = ex.StartEncounter();
                src = battle.StartNext(false);
                points[points.Count - 1].Fight = BattleSnap(src);   // B.StartNext was saved inside the call
                SettlingFight fight = Recorder.SettleWhenDecided(battle, src);
                ITrialSource view = fight.Source;
                ToPlayer(view);
                CardFace quicken = view.WaitingForPlayer ? view.Frame.Hand.FirstOrDefault(f => f.Id.StartsWith("quicken", StringComparison.Ordinal)) : null;
                if (quicken == null || view.Inspect(quicken.Id) != PlayVerdict.Accepted) continue;
                Assert.That(view.TryPlay(quicken.Id, DepictionText.RequiredZone(quicken.Aim), out DepictionEvent _), Is.EqualTo(PlayVerdict.Accepted));
                if (!view.CanEndTurn) continue;
                view.EndTurn();
                ToPlayer(view);
                if (!view.WaitingForPlayer) continue;
                int dir = view.CanTakeFreeStep(1) ? 1 : view.CanTakeFreeStep(-1) ? -1 : 0;
                if (dir == 0) continue;

                int before = log.Count;
                view.TakeFreeStep(dir);
                Assert.That(Json.Stringify(log.Last.ToJson()), Is.EqualTo("[\"C\",\"TakeFreeStep\",[" + dir + "]]"), "logged with its direction");
                Assert.That(log.Count, Is.EqualTo(before + 1));
                Assert.That(view.CanTakeFreeStep(), Is.False, "the cell is taken");
                for (int i = 0; i < 6 && !fight.Settled; i++) PlayerStep(view);

                // every point, the free cell's among them, replays onto the same fight
                Assert.That(points.Count(p => p.Log.Any(e => e.Method == "TakeFreeStep")), Is.GreaterThan(0));
                for (int i = 0; i < points.Count; i++)
                {
                    ReplayResult r = Recorder.ReplayLife(new Expedition(new RunStart { Seed = seed }, DeckWithQuicken()), points[i].Log);
                    Assert.That(r.Open == null ? null : BattleSnap(r.Open.Source), Is.EqualTo(points[i].Fight), $"seed {seed}, point {i} ({points[i].Label})");
                }
                TestContext.Progress.WriteLine($"seed {seed}: the free cell {dir}, {points.Count} saved points");
                return;
            }
            Assert.Fail("no seed in 1..60 opens the free cell with 身軽 in the first hand");
        }
    }
}
