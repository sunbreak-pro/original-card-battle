using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// #59: battle_core_v4 §3.2 / §17.8 / §19.3 習熟 — the four contexts a play meets, 才能 doubling
    /// one card, the steps at 3 / 8 / 15 (型替え 2 / 6 / 12) judged only once the battle is over, the
    /// three kinds of step, step 3 for one card of the life only, and 習熟訓練.
    /// </summary>
    public class MasteryTests
    {
        private const int Cells = 8;

        private static readonly IRng NoRng = new FixedRng(0.9999999);

        // ---- Fixtures ----

        /// <summary>An attack reaching 0〜2 whose trait wants N 2 or more: every context can be met by one play.</summary>
        private static readonly CardDef Lance = Fixtures.Card(
            "lance", 1, new Face(Power: 6, Reach: new Reach(0, 2)),
            trait: new Trait(TraitCondition.GapAtLeast, TraitEffect.PowerBonus, 2, Threshold: 2));

        private static readonly CardDef Block = Fixtures.Card(
            "block", 1, new Face(Guard: 4), BattleAttribute.Guard, targets: TargetKind.Self);

        private static TraitContext Board(int gap = 0, OmenKind? omen = null, int staminaBefore = 4) =>
            new TraitContext(Gap: gap, StaminaBefore: staminaBefore, OpponentOmen: omen);

        private static MasteryContext Contexts(CardDef def, TraitContext board, int paid = 1) =>
            Mastery.ContextsOf(def, board, Traits.EvaluateAll(def.AllTraits, board).Triggered, paid);

        private static MasteryProfile Profile(string id, MasteryKind kind) => new MasteryProfile(id, kind);

        private static MasteryTally Tally(params (string Id, int Ticks)[] entries)
        {
            var ticks = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (id, n) in entries) ticks[id] = n;
            return new MasteryTally(ticks);
        }

        // ---- The four contexts: 0〜4 ticks a play ----

        [Test]
        public void APlay_MeetingNoContext_GivesNoTick()
        {
            // N 0: the trait (N 2 以上) fails and 0 is not the far end of 0〜2. An attack omen wants
            // 防御. 4 before and 2 paid leaves 2: not 構え圏, and 4 before is not 死力圏.
            var met = Contexts(Lance, Board(gap: 0, omen: OmenKind.Attack, staminaBefore: 4), paid: 2);

            Assert.That(met, Is.EqualTo(MasteryContext.None));
            Assert.That(new MasteryPlay(Lance.Id, met).Ticks, Is.EqualTo(0));
        }

        [Test]
        public void APlay_MeetingAllFour_GivesFourTicks()
        {
            var met = Contexts(Lance, Board(gap: 2, omen: OmenKind.Move, staminaBefore: 10));

            Assert.That(met, Is.EqualTo(MasteryContext.Gap | MasteryContext.Omen | MasteryContext.Trait | MasteryContext.Zone));
            Assert.That(new MasteryPlay(Lance.Id, met).Ticks, Is.EqualTo(4));
        }

        [Test]
        public void GapContext_IsTheGapTraitsSide_OrTheFarEndOfTheReach()
        {
            var plain = Fixtures.Card("plain", 1, new Face(Power: 6, Reach: new Reach(0, 2)));
            var narrow = Fixtures.Card("narrow", 1, new Face(Power: 8, Reach: Reach.Only(0)));

            Assert.Multiple(() =>
            {
                // The trait's side: N 2 以上.
                Assert.That(Mastery.GapHolds(Lance, 2), Is.True);
                Assert.That(Mastery.GapHolds(Lance, 1), Is.False);

                // No gap trait: the far end of a reach spanning 2 N or more.
                Assert.That(Mastery.GapHolds(plain, 2), Is.True);
                Assert.That(Mastery.GapHolds(plain, 1), Is.False);
                Assert.That(Mastery.GapHolds(narrow, 0), Is.False, "a reach of one N has no edge to play at");
                Assert.That(Mastery.GapHolds(Block, 3), Is.False, "a self card waits for the card table to give it a range");

                // A profile's own range wins.
                Assert.That(Mastery.GapHolds(Block, 3, new MasteryProfile("block", MasteryKind.Lighten, GapContext: new Reach(2, 9))), Is.True);

                Assert.That(Contexts(Lance, Board(gap: 2, omen: OmenKind.Attack), paid: 2) & MasteryContext.Gap, Is.EqualTo(MasteryContext.Gap));
            });
        }

        [Test]
        public void OmenContext_IsTheAttributeThatAnswersTheOmen()
        {
            var skill = Fixtures.Card("hex", 1, new Face(Statuses: new[] { new StatusGrant(StatusKind.Fragile, 2) }), BattleAttribute.Skill);

            Assert.Multiple(() =>
            {
                Assert.That(Mastery.Answer(OmenKind.Attack), Is.EqualTo(BattleAttribute.Guard));
                Assert.That(Mastery.Answer(OmenKind.Guard), Is.EqualTo(BattleAttribute.Skill));
                foreach (var other in new[] { OmenKind.Move, OmenKind.Skill, OmenKind.Stance, OmenKind.Rest })
                {
                    Assert.That(Mastery.Answer(other), Is.EqualTo(BattleAttribute.Attack), other.ToString());
                }

                Assert.That(Contexts(Block, Board(omen: OmenKind.Attack), paid: 2), Is.EqualTo(MasteryContext.Omen));
                Assert.That(Contexts(Block, Board(omen: OmenKind.Move), paid: 2), Is.EqualTo(MasteryContext.None));
                Assert.That(Contexts(skill, Board(omen: OmenKind.Guard), paid: 2), Is.EqualTo(MasteryContext.Omen));
                Assert.That(Contexts(Block, Board(omen: null), paid: 2), Is.EqualTo(MasteryContext.None), "no omen, nothing to answer");
            });
        }

        [Test]
        public void TraitContext_IsAnyTraitHolding()
        {
            var first = Fixtures.Card("first", 1, new Face(Guard: 4), BattleAttribute.Guard,
                trait: new Trait(TraitCondition.FirstPlay, TraitEffect.GuardBonus, 2), targets: TargetKind.Self);
            var played = new TraitContext(StaminaBefore: 4, Played: new[] { BattleAttribute.Attack });

            Assert.Multiple(() =>
            {
                Assert.That(Contexts(first, Board(), paid: 2), Is.EqualTo(MasteryContext.Trait), "初手 holds");
                Assert.That(Contexts(first, played, paid: 2), Is.EqualTo(MasteryContext.None), "初手 does not");
            });
        }

        [Test]
        public void ZoneContext_IsReserveLeftOrDesperatePaid()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Contexts(Block, Board(staminaBefore: 4), paid: 1), Is.EqualTo(MasteryContext.Zone), "3 left: 構え圏");
                Assert.That(Contexts(Block, Board(staminaBefore: 4), paid: 2), Is.EqualTo(MasteryContext.None), "2 left and 4 before: neither");
                Assert.That(Contexts(Block, Board(staminaBefore: 2), paid: 1), Is.EqualTo(MasteryContext.Zone), "2 before: 死力圏");
                Assert.That(Contexts(Block, Board(staminaBefore: 3), paid: 1), Is.EqualTo(MasteryContext.None), "3 before, 2 left");
            });
        }

        [Test]
        public void PlayCard_WritesTheContextsDown_AndTheTallyCountsThem()
        {
            // Turn 1 on an 8-cell line, start gap 2: the lance plays at N 2 with 10 stamina, the
            // enemy's omen at 1〜2 is an attack (mid_hit). Gap + trait + zone = 3; the block answers
            // the attack omen and leaves 8: omen + zone = 2.
            var state = TurnLoop.Start(new BattleSetup(Fixtures.Enemy(), Deck(Lance, Block), Cells, StartGap: 2), NoRng).State;
            state = TurnLoop.BeginPlayerTurn(state, NoRng).State;
            state = TurnLoop.PlayCard(state, InHand(state, "lance"), NoRng).State;
            state = TurnLoop.PlayCard(state, InHand(state, "block"), NoRng).State;

            var plays = state.History.MasteryPlays;
            var tally = MasteryTally.Of(state);
            Assert.Multiple(() =>
            {
                Assert.That(plays.Select(p => p.CardId), Is.EqualTo(new[] { "lance", "block" }));
                Assert.That(plays[0].Contexts, Is.EqualTo(MasteryContext.Gap | MasteryContext.Trait | MasteryContext.Zone));
                Assert.That(plays[1].Contexts, Is.EqualTo(MasteryContext.Omen | MasteryContext.Zone));
                Assert.That(tally.TicksOf("lance"), Is.EqualTo(3));
                Assert.That(tally.TicksOf("block"), Is.EqualTo(2));
            });
        }

        // ---- 才能 ----

        [Test]
        public void Talent_DoublesThatCardsTicks_AndNoOtherCards()
        {
            var book = MasteryBook.Begin("lance", new[] { Profile("lance", MasteryKind.Extend), Profile("block", MasteryKind.Lighten) });

            var after = book.AfterBattle(Tally(("lance", 3), ("block", 3))).Book;

            Assert.Multiple(() =>
            {
                Assert.That(after.TicksOf("lance"), Is.EqualTo(6));
                Assert.That(after.TicksOf("block"), Is.EqualTo(3));
                Assert.That(after.Train("lance").Book.TicksOf("lance"), Is.EqualTo(7), "training gives a fixed 1, talent or not");
            });
        }

        // ---- Thresholds, and only at the battle's end ----

        [Test]
        public void Steps_GoUpAt3_8_15()
        {
            var book = MasteryBook.Begin(null, new[] { Profile("lance", MasteryKind.Extend) });
            var ranks = new List<int>();
            var ups = new List<RankUp>();
            foreach (int ticks in new[] { 2, 1, 4, 1, 6, 1 })
            {
                var result = book.AfterBattle(Tally(("lance", ticks)));
                book = result.Book;
                ranks.Add(book.RankOf("lance"));
                ups.AddRange(result.RankUps);
            }

            Assert.Multiple(() =>
            {
                // 2 → 3 → 7 → 8 → 14 → 15.
                Assert.That(ranks, Is.EqualTo(new[] { 0, 1, 1, 2, 2, 3 }));
                Assert.That(ups, Is.EqualTo(new[] { new RankUp("lance", 0, 1), new RankUp("lance", 1, 2), new RankUp("lance", 2, 3) }));
                Assert.That(Constants.MasteryThresholds, Is.EqualTo(new[] { 3, 8, 15 }));
            });
        }

        [Test]
        public void ReformCards_StepUpAt2_6_12()
        {
            var book = MasteryBook.Begin(null, new[] { Profile("seer", MasteryKind.Reform) });

            Assert.Multiple(() =>
            {
                Assert.That(book.AfterBattle(Tally(("seer", 2))).Book.RankOf("seer"), Is.EqualTo(1));
                Assert.That(book.AfterBattle(Tally(("seer", 6))).Book.RankOf("seer"), Is.EqualTo(2));
                Assert.That(book.AfterBattle(Tally(("seer", 12))).Book.RankOf("seer"), Is.EqualTo(3));
            });
        }

        [Test]
        public void Steps_GoUpOnlyOnceTheBattleIsOver()
        {
            var lance = Lance;
            var book = MasteryBook.Begin("lance", new[] { Profile("lance", MasteryKind.Extend) });
            var deck = book.MasteredDeck(Deck(lance, lance, lance));
            var state = TurnLoop.Start(new BattleSetup(Fixtures.Enemy(), deck, Cells, StartGap: 2), NoRng).State;
            state = TurnLoop.BeginPlayerTurn(state, NoRng).State;

            // Three lances at N 2: 3 ticks each, 18 with the talent — past 8 already.
            for (int i = 0; i < 3; i++) state = TurnLoop.PlayCard(state, InHand(state, "lance"), NoRng).State;

            Assert.Multiple(() =>
            {
                Assert.That(state.Result, Is.EqualTo(GameResult.Ongoing));
                Assert.That(MasteryTally.Of(state).TicksOf("lance"), Is.EqualTo(9));
                Assert.Throws<InvalidOperationException>(() => book.AfterBattle(state), "not while the battle goes on");
                Assert.That(state.DiscardPile.Select(c => c.Def), Is.All.EqualTo(lance), "the cards in play keep the face they started with");
                Assert.That(book.RankOf("lance"), Is.EqualTo(0));
            });

            // The battle ends (won here): now the steps are judged.
            var won = state with { Result = GameResult.Won };
            var result = book.AfterBattle(won);
            Assert.Multiple(() =>
            {
                Assert.That(result.Book.TicksOf("lance"), Is.EqualTo(18));
                Assert.That(result.Book.RankOf("lance"), Is.EqualTo(3));
                Assert.That(result.RankUps, Is.EqualTo(new[] { new RankUp("lance", 0, 3) }));
                Assert.That(result.Book.Mastered(lance).Column, Is.EqualTo(4), "the next battle fights with the grown card");
            });
        }

        [Test]
        public void ACardWithNoProfile_GathersTicks_ButDoesNotStepUp()
        {
            var book = MasteryBook.Begin(null, Array.Empty<MasteryProfile>());

            var after = book.AfterBattle(Tally(("lance", 20))).Book;

            Assert.Multiple(() =>
            {
                Assert.That(after.TicksOf("lance"), Is.EqualTo(20));
                Assert.That(after.RankOf("lance"), Is.EqualTo(0));
                Assert.That(after.Mastered(Lance), Is.EqualTo(Lance));
            });
        }

        // ---- The three kinds ----

        [Test]
        public void Extend_OnACostThreeCard_GivesTheColumnFourValue_AtCostThree()
        {
            // 突き (#1): cost 3, 0〜2 (width 3), 23 = 21 + 素直 2. Column 4 of width 3 is 30, so 32.
            var thrust = CardCatalog.Thrust;
            var extended = Mastery.Master(thrust, 1, Profile(thrust.Id, MasteryKind.Extend));

            // 柄当て (#36): two faces, cost 3, 18 / Guard 10. Column 4 of a two-face card is +6 / +4.
            var guardThrust = CardCatalog.GuardThrust;
            var both = Mastery.Master(guardThrust, 1, Profile(guardThrust.Id, MasteryKind.Extend));

            Assert.Multiple(() =>
            {
                Assert.That(thrust.Cost, Is.EqualTo(3));
                Assert.That(extended.Column, Is.EqualTo(4));
                Assert.That(extended.Face.Power, Is.EqualTo(Columns.Value(Columns.SingleAttackPower, 4) + Constants.PlainCardBonus));
                Assert.That(extended.Face.Power, Is.EqualTo(32));
                Assert.That(extended.Cost, Is.EqualTo(3), "伸び keeps the cost");
                Assert.That(extended.Face.Reach, Is.EqualTo(thrust.Face.Reach));

                Assert.That(both.Face.Power, Is.EqualTo(24));
                Assert.That(both.Face.Guard, Is.EqualTo(14));
                Assert.That(both.Cost, Is.EqualTo(3));

                // Column 4 is the last: a further step adds nothing, and 集中 on it adds nothing either.
                Assert.That(Mastery.Master(thrust, 2, Profile(thrust.Id, MasteryKind.Extend)).Face.Power, Is.EqualTo(32));
                Assert.That(FocusStep.Of(extended.Attributes, extended.Column, extended.Face), Is.EqualTo(FocusStep.None));
            });
        }

        [Test]
        public void Extend_OnACostOneCard_ClimbsAColumnAStep_AndKeepsCostOne()
        {
            var cut = Fixtures.Card("cut", 1, new Face(Power: 6, Statuses: new[] { new StatusGrant(StatusKind.Bleed, 1) }));
            var profile = Profile("cut", MasteryKind.Extend);

            var one = Mastery.Master(cut, 1, profile);
            var three = Mastery.Master(cut, 3, profile);

            Assert.Multiple(() =>
            {
                Assert.That(one.Face.Power, Is.EqualTo(13));
                Assert.That(one.Face.StatusList[0].Stacks, Is.EqualTo(2), "出血 climbs the stack scale too");
                Assert.That(one.Cost, Is.EqualTo(1));
                Assert.That(three.Face.Power, Is.EqualTo(30));
                Assert.That(three.Column, Is.EqualTo(4));
                Assert.That(three.Cost, Is.EqualTo(1));
            });
        }

        [Test]
        public void Lighten_TakesTheCostOneDownAStep_FloorZero()
        {
            var heavy = Fixtures.Card("heavy", 2, new Face(Power: 13));
            var profile = Profile("heavy", MasteryKind.Lighten);

            Assert.Multiple(() =>
            {
                Assert.That(Mastery.Master(heavy, 1, profile).Cost, Is.EqualTo(1));
                Assert.That(Mastery.Master(heavy, 2, profile).Cost, Is.EqualTo(0), "§19.3 S5: column 2 at step 2 is cost 0");
                Assert.That(Mastery.Master(heavy, 3, profile).Cost, Is.EqualTo(0), "floor 0");
                Assert.That(Mastery.Master(heavy, 2, profile).Face, Is.EqualTo(heavy.Face), "the numbers stay");
            });
        }

        [Test]
        public void ALightenedCard_AtCostZero_IsPlayedWithNoStamina()
        {
            var heavy = Fixtures.Card("heavy", 2, new Face(Power: 13));
            var light = Mastery.Master(heavy, 2, Profile("heavy", MasteryKind.Lighten));
            var state = TurnLoop.Start(new BattleSetup(Fixtures.Enemy(), Deck(light), Cells, StartGap: 0), NoRng).State;
            state = TurnLoop.BeginPlayerTurn(state, NoRng).State;
            state = state with { Player = state.Player with { Stamina = 0 } };

            var played = TurnLoop.PlayCard(state, InHand(state, "heavy"), NoRng);

            Assert.Multiple(() =>
            {
                Assert.That(TurnLoop.CostNow(state, light), Is.EqualTo(0));
                Assert.That(played.Events.OfType<StaminaSpent>().Single().Amount, Is.EqualTo(0));
            });
        }

        [Test]
        public void Reform_SwapsTheTrait_AndTheName_AndKeepsTheNumbers()
        {
            // §19.3: 観察 → 精査 (締め) → 洞察 (連動(Sk)) → 看破 (初手), ドロー +1 throughout.
            var observe = CardCatalog.Observe;
            var profile = MasteryProfiles.Find("observe")!;

            var steps = Enumerable.Range(1, 3).Select(rank => Mastery.Master(observe, rank, profile)).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(profile.Kind, Is.EqualTo(MasteryKind.Reform));
                Assert.That(steps.Select(c => c.Name), Is.EqualTo(new[] { "精査", "洞察", "看破" }));
                Assert.That(steps.Select(c => c.Trait!.Condition), Is.EqualTo(new[] { TraitCondition.Finisher, TraitCondition.Combo, TraitCondition.FirstPlay }));
                Assert.That(steps.Select(c => c.Trait!.Effect), Is.All.EqualTo(TraitEffect.Draw));
                Assert.That(steps.Select(c => c.Trait!.Amount), Is.All.EqualTo(1));
                Assert.That(steps[1].Trait!.Attribute, Is.EqualTo(BattleAttribute.Skill));
                foreach (var step in steps)
                {
                    Assert.That(step.Face, Is.EqualTo(observe.Face));
                    Assert.That(step.Cost, Is.EqualTo(observe.Cost));
                    Assert.That(step.Id, Is.EqualTo(observe.Id));
                }
            });
        }

        // ---- Step 3 for one card of the life ----

        [Test]
        public void Step3_CannotGoToASecondCard()
        {
            var profiles = new[] { Profile("lance", MasteryKind.Extend), Profile("block", MasteryKind.Lighten) };
            var book = MasteryBook.Begin(null, profiles).AfterBattle(Tally(("lance", 15))).Book;

            var after = book.AfterBattle(Tally(("block", 40))).Book;

            Assert.Multiple(() =>
            {
                Assert.That(book.TopRankHolder, Is.EqualTo("lance"));
                Assert.That(after.RankOf("lance"), Is.EqualTo(3));
                Assert.That(after.RankOf("block"), Is.EqualTo(2), "the others stop at step 2");
                Assert.That(after.TicksOf("block"), Is.EqualTo(40), "the ticks still count");
                Assert.That(after.Train("block").Book.RankOf("block"), Is.EqualTo(2));
            });
        }

        [Test]
        public void Step3_ReachedByTwoAtOnce_GoesToTheMoreTicks_ThenTheTalent()
        {
            var profiles = new[] { Profile("lance", MasteryKind.Extend), Profile("block", MasteryKind.Lighten), Profile("cut", MasteryKind.Extend) };

            var byTicks = MasteryBook.Begin(null, profiles).AfterBattle(Tally(("lance", 15), ("block", 16)));
            var byTalent = MasteryBook.Begin("lance", profiles).AfterBattle(Tally(("cut", 16), ("lance", 8)));

            Assert.Multiple(() =>
            {
                Assert.That(byTicks.Book.TopRankHolder, Is.EqualTo("block"));
                Assert.That(byTicks.Book.RankOf("lance"), Is.EqualTo(2));
                Assert.That(byTicks.RankUps, Is.EquivalentTo(new[] { new RankUp("block", 0, 3), new RankUp("lance", 0, 2) }));

                // lance 8 × 2 = 16 ties cut's 16: the talent card takes it.
                Assert.That(byTalent.Book.TopRankHolder, Is.EqualTo("lance"));
                Assert.That(byTalent.Book.RankOf("cut"), Is.EqualTo(2));
            });
        }

        // ---- 習熟訓練 ----

        [Test]
        public void Training_GivesTheNamedCardOneTick_AndIsJudgedAtOnce()
        {
            var book = MasteryBook.Begin(null, new[] { Profile("lance", MasteryKind.Extend) }).AfterBattle(Tally(("lance", 2))).Book;

            var trained = book.Train("lance");

            Assert.Multiple(() =>
            {
                Assert.That(Constants.TrainingTicks, Is.EqualTo(1));
                Assert.That(trained.Book.TicksOf("lance"), Is.EqualTo(3));
                Assert.That(trained.Book.RankOf("lance"), Is.EqualTo(1));
                Assert.That(trained.RankUps, Is.EqualTo(new[] { new RankUp("lance", 0, 1) }));
                Assert.That(trained.Book.TicksOf("block"), Is.EqualTo(0), "only the named card");
            });
        }

        // ---- Helpers ----

        private static List<CardInstance> Deck(params CardDef[] cards)
        {
            var filler = Fixtures.Card("filler", 1, new Face(Guard: 1), BattleAttribute.Guard, targets: TargetKind.Self);
            var deck = new List<CardInstance>();
            for (int i = 0; i < cards.Length; i++) deck.Add(new CardInstance(cards[i].Id + "-" + i, cards[i]));
            for (int i = deck.Count; i < Constants.HandDraw; i++) deck.Add(new CardInstance(filler.Id + "-" + i, filler));
            return deck;
        }

        private static string InHand(BattleState s, string cardId) => s.Hand.First(c => c.Def.Id == cardId).InstanceId;
    }
}
