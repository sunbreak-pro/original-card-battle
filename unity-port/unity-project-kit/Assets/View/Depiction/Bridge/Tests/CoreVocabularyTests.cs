// The demo vocabulary (#188) on screen: what the new core events write, how the eighty cards read,
// and a whole fight with decks drawn from the eighty in which the screen never drifts from the core.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class CoreVocabularyTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        // ---- the new events, written ----

        [Test]
        public void HpMovedWithoutABlow_IsAnHpChangeCue_WithTheCauseAndTheSign()
        {
            BattleState state = TurnLoop.Start(BattleSetup.Slice(), NoShuffle).State;
            var writer = new CoreScriptWriter(state.EnemyDef);
            writer.Opening(state);
            var events = new List<BattleEvent>
            {
                new TurnStarted(Actor.Player, 1),
                new StatusHpChanged(Actor.Player, StatusKind.Bleed, -4, 46),
                new StatusHpChanged(Actor.Player, StatusKind.Regen, 2, 48),
                new Healed(Actor.Player, 2, 50),
            };
            List<Cue> cues = writer.Write(events, state)[0].Cues.Where(c => c.Kind == CueKind.HpChange).ToList();

            Assert.That(cues.Select(c => (c.Target, c.Text, c.Amount, c.HpAfter)), Is.EqualTo(new[]
            {
                (UnitSide.Player, "出血", -4, 46), (UnitSide.Player, "再生", 2, 48), (UnitSide.Player, "回復", 2, 50),
            }));
            Assert.That(EffectPlan.StepsOf(new DepictionEvent { Kind = DepictionEventKind.EnemyAction, Cues = cues }).Count(s => s.Id == EffectId.HpDrain), Is.EqualTo(3));
        }

        [Test]
        public void AParryReturn_ShieldsThenWounds_TheAttacker()
        {
            BattleState state = TurnLoop.Start(BattleSetup.Slice(), NoShuffle).State;
            var writer = new CoreScriptWriter(state.EnemyDef);
            writer.Opening(state);
            var events = new List<BattleEvent>
            {
                new CardPlayed(Actor.Player, new CardInstance("thrust-0", CardCatalog.Thrust), 0),
                new Reflected(Actor.Enemy, Actor.Player, 5, 2, 3, 0, 47),
            };
            List<Cue> cues = writer.Write(events, state)[0].Cues;

            Assert.That(cues.Select(c => c.Kind), Is.EqualTo(new[] { CueKind.GuardBlock, CueKind.HpChange }));
            Assert.That(cues[1].Text, Is.EqualTo("見切り"));
            Assert.That(cues[1].Amount, Is.EqualTo(-3));
            Assert.That(cues[1].HpAfter, Is.EqualTo(47));
        }

        [Test]
        public void AStance_FiresItsNameOnce_AndLeadsTheChips()
        {
            BattleState state = TurnLoop.Start(BattleSetup.Slice(), NoShuffle).State;
            var writer = new CoreScriptWriter(state.EnemyDef);
            writer.Opening(state);
            StanceDef rock = CardCatalog.RockStance.Face.Stance;
            var events = new List<BattleEvent>
            {
                new CardPlayed(Actor.Player, new CardInstance("rock_stance-0", CardCatalog.RockStance), 3),
                new StanceSet(Actor.Player, "rock_stance", "岩の構え", rock, null),
                new StatusApplied(Actor.Player, Actor.Player, StatusKind.Empower, 2, 2, false),
                new StatusConsumed(Actor.Player, StatusKind.Empower, 1),
            };
            DepictionEvent ev = writer.Write(events, state)[0];

            Assert.That(ev.Cues.Where(c => c.Kind == CueKind.TraitFire).Select(c => c.Text), Is.EqualTo(new[] { "構え・岩の構え" }));
            Assert.That(ev.Cues.Where(c => c.Kind == CueKind.StatusChange).Select(c => (c.Amount, c.StacksAfter)),
                Is.EqualTo(new[] { (2, 2), (-1, 1) }));
            Assert.That(ev.After.Player.Statuses.Select(c => c.Label + c.Stacks), Is.EqualTo(new[] { "構え・岩の構え0", "強化1" }));
        }

        [Test]
        public void ATurnEndStance_PlaysInsideTheTurnEnd_BeforeReserve()
        {
            // 根渡り at gap 2 with 4 left: Guard 0 → 5 (the stance) → 8 (構え), all in one turn-end event.
            BattleState state = TurnLoop.Start(BattleSetup.Slice(), NoShuffle).State;
            var writer = new CoreScriptWriter(state.EnemyDef);
            writer.Opening(state);
            var events = new List<BattleEvent>
            {
                new StanceFired(Actor.Player, "root_stride", StanceHook.TurnEnd),
                new GuardGained(Actor.Player, 5, 5),
                new ReserveChecked(Actor.Player, 4, 3, 8),
                new HandDiscarded(Actor.Player, 2),
                new TurnEnded(Actor.Player, 1),
            };
            List<DepictionEvent> written = writer.Write(events, state);

            Assert.That(written, Has.Count.EqualTo(1));
            Assert.That(written[0].Kind, Is.EqualTo(DepictionEventKind.TurnEnd));
            Assert.That(written[0].Cues.Select(c => (c.Kind, c.GuardAfter)), Is.EqualTo(new[]
            {
                (CueKind.GuardGain, 5), (CueKind.StanceCue, 8), (CueKind.DiscardHand, Cue.Unchanged),
            }));
        }

        [Test]
        public void ABreak_DrainsThePlayersPips_AndSettlesTheEnemysStaminaQuietly()
        {
            BattleState state = TurnLoop.Start(BattleSetup.Slice(), NoShuffle).State;
            var writer = new CoreScriptWriter(state.EnemyDef);
            writer.Opening(state);
            var events = new List<BattleEvent>
            {
                new CardPlayed(Actor.Player, new CardInstance("flat_strike-0", CardCatalog.FlatStrike), 0),
                new StaminaBroken(Actor.Player, Actor.Enemy, 1, 9),
                new StaminaBroken(Actor.Enemy, Actor.Player, 2, 8),
            };
            DepictionEvent ev = writer.Write(events, state)[0];

            Assert.That(ev.Cues.Where(c => c.Kind == CueKind.StaminaChange).Select(c => (c.Amount, c.StaminaAfter)),
                Is.EqualTo(new[] { (-2, 8) }), "only the player's pips are on screen");
            Assert.That(ev.After.Enemy.Stamina, Is.EqualTo(9));
        }

        // ---- the eighty, as printed ----

        [Test]
        public void TheCardText_ReadsTheNewWords()
        {
            Assert.Multiple(() =>
            {
                Assert.That(CoreText.TraitLines(CardCatalog.LastStand), Is.EqualTo("間合い2以上 +5 ／ 死力 +3"));
                Assert.That(CoreText.TraitLines(CardCatalog.WaterStance), Is.EqualTo("連動(動) コスト-1"));
                Assert.That(CoreText.TraitLines(CardCatalog.SpiritRoar), Is.EqualTo("相手出血 脆化+2"));
                Assert.That(CoreText.TraitLines(CardCatalog.Resolve), Is.EqualTo("死力 自分再生+1"));
                Assert.That(CoreText.TraitLines(CardCatalog.PurgeFlash), Is.EqualTo("予兆攻撃 追撃"));
                Assert.That(CoreText.TraitLines(CardCatalog.Thrust), Is.EqualTo(""), "素直");
                Assert.That(CoreText.Describe(CardCatalog.Focus.Face, CardCatalog.Focus.Attributes),
                    Is.EqualTo("自分に集中を 2 付与する。自分に強化を 2 付与する。カードを 1 枚引く。"));
                Assert.That(CoreText.Describe(CardCatalog.FlatStrike.Face, CardCatalog.FlatStrike.Attributes), Is.EqualTo("敵に 6 ダメージ。崩し 1。"));
                Assert.That(CoreText.Describe(CardCatalog.FirstAid.Face, CardCatalog.FirstAid.Attributes), Is.EqualTo("HP を 15 回復する。"));
                Assert.That(CoreText.Describe(CardCatalog.RockStance.Face, CardCatalog.RockStance.Attributes), Is.EqualTo("構え: 毎ターン開始に Guard +3。"));
                Assert.That(CoreText.StanceText(CardCatalog.WaterStance.Face.Stance), Is.EqualTo("間合い 2 以上で始まるターンに回復 +2"));
                Assert.That(CoreText.StanceText(CardCatalog.MistStep.Face.Stance), Is.EqualTo("間合い 2 以上で終えたターンに Guard +3、次の回復 +2"));
                Assert.That(CoreText.StanceText(CardCatalog.PriestPrayer.Face.Stance), Is.EqualTo("被弾のたび（ターン 1 回）スタミナ +1、Guard +3"));
                Assert.That(CoreText.ValueOf(CardCatalog.FirstAid.Face, CardCatalog.FirstAid.Attributes), Is.EqualTo("15"));
            });
        }

        [Test]
        public void EveryCard_HasTextForEveryFace_AndATraitLine()
        {
            foreach (CardDef def in CardCatalog.All)
            {
                string text = CoreText.Describe(def.Face, def.Attributes);
                Assert.That(text, Is.Not.Empty, def.Id);
                if (def.Face.Stance != null) Assert.That(text, Does.Contain("構え: "), def.Id);
                foreach (Trait trait in def.AllTraits)
                {
                    string line = CoreText.TraitLine(trait);
                    Assert.That(line, Is.Not.Empty, def.Id);
                    Assert.That(line.Split(' ').Length, Is.LessThanOrEqualTo(2), def.Id + ": 「条件 効果」");
                }
            }
        }

        [Test]
        public void AHandCard_PrintsTheCostTheCoreWouldTake()
        {
            // 水の構え (cost 2, 連動(M): コスト −1) after a move card: the card prints 1.
            var deck = new List<CardInstance>
            {
                new CardInstance("footwork-0", CardCatalog.Footwork),
                new CardInstance("water_stance-0", CardCatalog.WaterStance),
            };
            for (int i = 0; i < 18; i++) deck.Add(new CardInstance("filler-" + i, CardCatalog.Brace));
            BattleState state = TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, deck, BattleSetup.SliceFieldCells), NoShuffle).State;
            state = TurnLoop.BeginPlayerTurn(state, NoShuffle).State;
            CardInstance water = state.Hand.First(c => c.Def == CardCatalog.WaterStance);

            Assert.That(CoreText.Face(water, TurnLoop.Preview(state, water.InstanceId)).Cost, Is.EqualTo(2));
            state = TurnLoop.PlayCard(state, "footwork-0", NoShuffle).State;
            Assert.That(CoreText.Face(water, TurnLoop.Preview(state, water.InstanceId)).Cost, Is.EqualTo(1));
        }

        // ---- a whole fight with the eighty ----

        [Test]
        public void EveryEventsFrame_AgreesWithTheCore_ForDecksDrawnFromTheEighty()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var deck = RandomDeck(new SeededRng(seed * 7919), 20 + (seed % 21));
                var source = new CoreBattleSource(new BattleSetup(Enemies.PolearmWarped, deck, BattleSetup.SliceFieldCells), seed, suggestCards: true);
                int guard = 0;
                while (!source.Finished && guard++ < 2000)
                {
                    if (source.WaitingForPlayer)
                    {
                        string id = source.SuggestedCardId;
                        if (id.Length > 0)
                        {
                            CardFace face = DepictionText.Find(source.Frame.Hand, id);
                            DepictionEvent played;
                            Assert.That(source.TryPlay(id, DepictionText.RequiredZone(face.Aim), out played), Is.EqualTo(PlayVerdict.Accepted), "seed " + seed + " " + id);
                        }
                        else
                        {
                            source.EndTurn();
                        }
                    }
                    else
                    {
                        source.AdvanceAuto();
                    }

                    if (!source.WaitingForPlayer && !source.Finished) continue;
                    BattleState state = source.State;
                    DepictionFrame frame = source.Frame;
                    string at = "seed " + seed + " turn " + state.Turn;
                    Assert.That(frame.Player.Hp, Is.EqualTo(state.Player.Hp), at);
                    Assert.That(frame.Player.Guard, Is.EqualTo(state.Player.Guard), at);
                    Assert.That(frame.Player.Stamina, Is.EqualTo(state.Player.Stamina), at);
                    Assert.That(frame.Enemy.Hp, Is.EqualTo(state.Enemy.Hp), at);
                    Assert.That(frame.Enemy.Guard, Is.EqualTo(state.Enemy.Guard), at);
                    Assert.That(frame.Enemy.Stamina, Is.EqualTo(state.Enemy.Stamina), at);
                    Assert.That(frame.Player.RangeGlyph, Is.EqualTo(state.Gap.ToString()), at);
                    Assert.That(frame.Player.Statuses.Select(c => c.Label + c.Stacks),
                        Is.EqualTo(CoreText.Chips(state.Player.Statuses, NameOf(state.Player.StanceSource)).Select(c => c.Label + c.Stacks)), at);
                    Assert.That(frame.Enemy.Statuses.Select(c => c.Label + c.Stacks),
                        Is.EqualTo(CoreText.Chips(state.Enemy.Statuses).Select(c => c.Label + c.Stacks)), at);
                    Assert.That(frame.Hand.Select(c => c.Id), Is.EqualTo(state.Hand.Select(c => c.InstanceId)), at);
                    foreach (CardFace face in frame.Hand)
                    {
                        CardInstance card = state.Hand.First(c => c.InstanceId == face.Id);
                        Assert.That(face.Cost, Is.EqualTo(TurnLoop.CostNow(state, card.Def)), at + " " + face.Id);
                    }
                }
                Assert.That(source.Finished, Is.True, "seed " + seed + " ends one way or the other");
            }
        }

        private static string NameOf(string cardId)
        {
            return string.IsNullOrEmpty(cardId) ? "" : CardCatalog.ById(cardId).Name;
        }

        /// <summary>A legal deck (§8): at most three of a kind, drawn from the eighty.</summary>
        private static List<CardInstance> RandomDeck(IRng rng, int size)
        {
            var counts = new Dictionary<string, int>();
            var deck = new List<CardInstance>();
            while (deck.Count < size)
            {
                CardDef def = CardCatalog.All[(int)(rng.NextDouble() * CardCatalog.All.Count) % CardCatalog.All.Count];
                int held;
                counts.TryGetValue(def.Id, out held);
                if (held >= Constants.CopiesMax) continue;
                counts[def.Id] = held + 1;
                deck.Add(new CardInstance(def.Id + "-" + held, def));
            }
            return deck;
        }
    }
}
