using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// #248: a held card and an enemy's omen each preview as one number for the whole move — every
    /// blow of a face with hits, the 脆化 its first blow leaves, and the wall damage of a push. The
    /// numbers are held against what <see cref="TurnLoop.PlayCard"/> and <see cref="TurnLoop.EndTurn"/>
    /// actually deal. The deck is dealt in order on an 8-cell line with the player on cell 2.
    /// </summary>
    public class PreviewTests
    {
        private const int Cells = 8;

        private static readonly IRng NoRng = new FixedRng(0.9999999);

        private static readonly EnemyActionDef Wait =
            Fixtures.EnemyAction("wait", face: new Face(), attributes: BattleAttribute.Guard, targets: TargetKind.Self);

        /// <summary>An enemy that never touches the player.</summary>
        private static readonly EnemyDef Idle = Fixtures.Enemy("idle", atZero: Wait, atOneToTwo: Wait, atThreePlus: Wait);

        private static readonly CardDef Filler =
            Fixtures.Card("filler", 1, new Face(Guard: 1), BattleAttribute.Guard, targets: TargetKind.Self);

        /// <summary>A player's card of two blows (none in the catalogue has hits yet).</summary>
        private static readonly CardDef TwoHits = Fixtures.Card("two_hits", 1, new Face(Power: 5, Hits: 2));

        /// <summary>二段斬り's shape on a player's card: 脆化 on the first blow, for the second.</summary>
        private static readonly CardDef TwinCut = Fixtures.Card(
            "twin_cut", 1, new Face(Power: 6, Hits: 2, Statuses: new[] { new StatusGrant(StatusKind.Fragile, 1) }));

        // ---- Helpers ----

        private static BattleState Opened(EnemyDef enemy, int gap, params CardDef[] cards)
        {
            var deck = new List<CardInstance>();
            for (int i = 0; i < cards.Length; i++) deck.Add(new CardInstance(cards[i].Id + "-" + i, cards[i]));
            for (int i = deck.Count; i < Constants.HandDraw; i++) deck.Add(new CardInstance(Filler.Id + "-" + i, Filler));
            var start = TurnLoop.Start(new BattleSetup(enemy, deck, Cells, StartGap: gap), NoRng).State;
            return TurnLoop.BeginPlayerTurn(start, NoRng).State;
        }

        private static string InHand(BattleState s, string cardId) => s.Hand.First(c => c.Def.Id == cardId).InstanceId;

        /// <summary>What the events put on the enemy <paramref name="unit"/>: the player's blows and its wall, before and after Guard.</summary>
        private static (int Raw, int Damage) OnEnemy(IEnumerable<BattleEvent> events, int unit = 0)
        {
            int raw = 0, damage = 0;
            foreach (var e in events)
            {
                if (e is DamageDealt d && d.Actor == Actor.Player && d.Unit == unit) { raw += d.Raw; damage += d.Damage; }
                if (e is WallHit w && w.Actor == Actor.Enemy && w.Unit == unit) { raw += w.Raw; damage += w.Damage; }
            }
            return (raw, damage);
        }

        /// <summary>What the events put on the player: the enemy's blows and the wall.</summary>
        private static (int Raw, int Damage) OnPlayer(IEnumerable<BattleEvent> events)
        {
            int raw = 0, damage = 0;
            foreach (var e in events)
            {
                if (e is DamageDealt d && d.Target == Actor.Player) { raw += d.Raw; damage += d.Damage; }
                if (e is WallHit w && w.Actor == Actor.Player) { raw += w.Raw; damage += w.Damage; }
            }
            return (raw, damage);
        }

        private static void AssertPreviewIsWhatPlayDeals(BattleState s, string cardId, int raw, int damage)
        {
            var preview = TurnLoop.Preview(s, InHand(s, cardId))!;
            var dealt = OnEnemy(TurnLoop.PlayCard(s, InHand(s, cardId), NoRng).Events);

            Assert.That((preview.RawPower, preview.Damage), Is.EqualTo(dealt), "the preview is what the card deals");
            Assert.That(dealt, Is.EqualTo((raw, damage)));
        }

        // ---- A player's card ----

        [Test]
        public void TwoBlows_AddUp_AndEachMeetsGuardOnItsOwn()
        {
            // 5 into Guard 4 → 1 through, then 5 into nothing → 5: power 10, HP 6.
            var s = Opened(Idle, 1, TwoHits);
            s = s.WithEnemy(s.Enemy with { Guard = 4 });

            AssertPreviewIsWhatPlayDeals(s, "two_hits", raw: 10, damage: 6);
        }

        [Test]
        public void TheFragileTheFirstBlowLeaves_CountsOnTheSecond()
        {
            // 6, then 脆化 lands, then 6 × 1.5 = 9.
            var s = Opened(Idle, 1, TwinCut);

            AssertPreviewIsWhatPlayDeals(s, "twin_cut", raw: 15, damage: 15);
        }

        [Test]
        public void FragileIsSpentPerBlow_AndEmpowerRidesTheBlowsItDoesNotTake()
        {
            // §19.5 S13: blow 1 takes 脆化 (5 × 1.5 → 8), blow 2 takes 強化 (→ 8).
            var s = Opened(Idle, 1, TwoHits);
            s = s with { Player = s.Player with { Statuses = StatusSet.Of((StatusKind.Empower, 1)) } };
            s = s.WithEnemy(s.Enemy with { Statuses = StatusSet.Of((StatusKind.Fragile, 1)) });

            AssertPreviewIsWhatPlayDeals(s, "two_hits", raw: 16, damage: 16);
        }

        [Test]
        public void AWaitingFollowUp_RidesTheFirstBlowOnly()
        {
            // §17.6 F6: 5 + 5, then 5.
            var s = Opened(Idle, 1, TwoHits);
            s = s with { Player = s.Player with { FollowUp = 5 } };

            AssertPreviewIsWhatPlayDeals(s, "two_hits", raw: 15, damage: 15);
        }

        [Test]
        public void APushIntoTheWall_AddsTheWallDamage()
        {
            // §7.3: 盾押し on an enemy already at the end of the line — the cell it cannot take is 3
            // of wall, and its Guard 2 takes 2 of it. 盾押し has no attack face; the wall is its power.
            var s = Opened(Idle, 0, CardCatalog.ShieldPush);
            s = s with { Player = s.Player with { Cell = Cells - 1 } };
            s = s.WithEnemy(s.Enemy with { Cell = Cells, Guard = 2 });

            AssertPreviewIsWhatPlayDeals(s, "shield_push", raw: 3, damage: 1);
            Assert.That(TurnLoop.Preview(s, InHand(s, "shield_push"))!.Attacks, Is.False);
        }

        [Test]
        public void ACardThePlayerCannotPayFor_StillPreviewsItsNumber()
        {
            var s = Opened(Idle, 1, TwinCut);
            var poor = s with { Player = s.Player with { Stamina = 0 } };

            var preview = TurnLoop.Preview(poor, InHand(poor, "twin_cut"))!;

            Assert.That(TurnLoop.CanPlay(poor, InHand(poor, "twin_cut")), Is.EqualTo(PlayRefusal.NotEnoughStamina));
            Assert.That((preview.RawPower, preview.Damage), Is.EqualTo((15, 15)));
        }

        [Test]
        public void EveryBlowIsCounted_EvenWhenTheFirstWouldFellTheEnemy()
        {
            // The number is the card's power; a blow that would end the fight does not hide the rest.
            var s = Opened(Idle, 1, TwoHits);
            s = s.WithEnemy(s.Enemy with { Hp = 4 });

            var preview = TurnLoop.Preview(s, InHand(s, "two_hits"))!;

            Assert.That((preview.RawPower, preview.Damage), Is.EqualTo((10, 10)));
            Assert.That(s.Enemy.Hp, Is.EqualTo(4), "the board is not touched");
        }

        [Test]
        public void OutOfReach_TheCardShowsOneBlowWorkedOutWithoutResolving()
        {
            var s = Opened(Idle, 3, TwoHits);

            var preview = TurnLoop.Preview(s, InHand(s, "two_hits"))!;

            Assert.That(preview.InReach, Is.False);
            Assert.That(preview.RawPower, Is.EqualTo(5));
        }

        // ---- #253: a stance that gives a status on every attack ----

        /// <summary>脆化 1 on the foe after each blow that lands (#253).</summary>
        private static readonly StanceDef FragileOnAttack =
            new StanceDef(StanceHook.StatusOnAttack, Status: StatusKind.Fragile, StatusStacks: 1);

        [Test]
        public void TheFragileAStanceGivesOnTheFirstBlow_CountsOnTheSecond()
        {
            // 6, then the stance's 脆化 on the enemy, then 6 × 1.5 = 9: the preview is the 15 PlayCard deals.
            var stanceCard = Fixtures.Card("sting_stance", 1, new Face(Stance: FragileOnAttack), BattleAttribute.Stance, targets: TargetKind.Self);
            var twoSix = Fixtures.Card("two_six", 1, new Face(Power: 6, Hits: 2));
            var s = Opened(Idle, 1, stanceCard, twoSix);
            s = TurnLoop.PlayCard(s, InHand(s, "sting_stance"), NoRng).State;

            AssertPreviewIsWhatPlayDeals(s, "two_six", 15, 15);
            Assert.That(s.Enemy.Statuses.Has(StatusKind.Fragile), Is.False, "the board is not touched");
        }

        // ---- An enemy's omen ----

        [Test]
        public void ATwoBlowOmen_CountsTheFragileTheEnemysStanceGivesOnTheFirstBlow()
        {
            // #253: a plain 6 × 2 from an enemy holding the stance is 6, then 9, as the enemy phase deals it.
            var twin = Fixtures.EnemyAction("twin", column: 3, face: new Face(Power: 6, Hits: 2, Reach: Reach.Only(0)));
            var s = Opened(Fixtures.Enemy("twin_fixture", atZero: twin), 0);
            s = s.WithEnemy(s.Enemy with { Stance = FragileOnAttack, StanceSource = "enemy_stance" });
            s = s with { Player = s.Player with { Stamina = 0 } };
            Assert.That(s.Omen!.ActionId, Is.EqualTo("twin"));

            var omen = TurnLoop.PreviewOmen(s, 0)!;
            var dealt = OnPlayer(TurnLoop.EndTurn(s, NoRng).Events);

            Assert.That(omen, Is.EqualTo(new OmenPreview(15, 15, Lands: true, Rests: false)));
            Assert.That(dealt, Is.EqualTo((15, 15)), "what the enemy phase deals");
        }

        [Test]
        public void ATwoBlowOmen_IsOneSum_WithTheFragileItsFirstBlowLeaves()
        {
            // roster §2.6 二段斬り: 6, then 脆化 on the player, then 9.
            var s = Opened(Enemies.TwinBladeWarped, 0);
            s = s with { Player = s.Player with { Stamina = 0 } };
            Assert.That(s.Omen!.ActionId, Is.EqualTo("twin_slash"));

            var omen = TurnLoop.PreviewOmen(s, 0)!;
            var dealt = OnPlayer(TurnLoop.EndTurn(s, NoRng).Events);

            Assert.That(omen, Is.EqualTo(new OmenPreview(15, 15, Lands: true, Rests: false)));
            Assert.That(dealt, Is.EqualTo((15, 15)), "what the enemy phase deals");
        }

        [Test]
        public void ATwoBlowOmen_MeetsThePlayersGuardBlowByBlow()
        {
            // Guard 10: 6 is all absorbed (4 left), then 9 into 4 → 5 through.
            var s = Opened(Enemies.TwinBladeWarped, 0);
            s = s with { Player = s.Player with { Stamina = 0, Guard = 10 } };

            var omen = TurnLoop.PreviewOmen(s, 0)!;
            var dealt = OnPlayer(TurnLoop.EndTurn(s, NoRng).Events);

            Assert.That((omen.RawPower, omen.Damage), Is.EqualTo((15, 5)));
            Assert.That(dealt, Is.EqualTo((15, 5)));
        }

        [Test]
        public void AnOmenThatDoesNotReach_SaysSo_AndStillShowsWhatItWouldDoIfItLanded()
        {
            // 二段斬り reaches 0 only; the player has stepped back to gap 1.
            var s = Opened(Enemies.TwinBladeWarped, 0);
            s = s with { Player = s.Player with { Cell = s.Player.Cell - 1 } };
            Assert.That(s.Gap, Is.EqualTo(1));

            var omen = TurnLoop.PreviewOmen(s, 0)!;

            Assert.That(omen.Lands, Is.False);
            Assert.That(omen.RawPower, Is.EqualTo(15));
            Assert.That(s.Player.Cell, Is.EqualTo(1), "the board is not touched");
        }

        [Test]
        public void AnOmenTheEnemyCannotPayFor_IsARest_WorthNothing()
        {
            // 二段斬り costs 3; 0 now plus a recovery of 2 cannot pay it.
            var s = Opened(Enemies.TwinBladeWarped, 0);
            s = s.WithEnemy(s.Enemy with { Stamina = 0 });

            Assert.That(TurnLoop.PreviewOmen(s, 0), Is.EqualTo(new OmenPreview(0, 0, Lands: false, Rests: true)));
        }

        [Test]
        public void AShoveOmen_CountsItsTraitAndTheWall()
        {
            // 石突きの押し込み at gap 0: 5 + 3 (無防備), then the push finds one of its two cells → 3 of wall.
            var s = Opened(Enemies.PolearmWarped, 0);
            s = s with { Player = s.Player with { Stamina = 0 } };
            Assert.That(s.Omen!.ActionId, Is.EqualTo("shove"));

            var omen = TurnLoop.PreviewOmen(s, 0)!;
            var dealt = OnPlayer(TurnLoop.EndTurn(s, NoRng).Events);

            Assert.That((omen.RawPower, omen.Damage), Is.EqualTo((11, 11)));
            Assert.That(dealt, Is.EqualTo((11, 11)));
        }

        [Test]
        public void TheReserveGuardTheTurnEndWouldGive_IsCountedBeforeTheOmen()
        {
            // §9 step 7: with 3 or more stamina left, 構え puts Guard 3 on the player before the enemy
            // acts, so 無防備 no longer holds: the shove is 5, 3 of it absorbed, then 3 of wall.
            var s = Opened(Enemies.PolearmWarped, 0);
            Assert.That(s.Player.Stamina, Is.GreaterThanOrEqualTo(Constants.ReserveThreshold));
            Assert.That(s.Player.Guard, Is.EqualTo(0));

            var omen = TurnLoop.PreviewOmen(s, 0)!;
            var dealt = OnPlayer(TurnLoop.EndTurn(s, NoRng).Events);

            Assert.That((omen.RawPower, omen.Damage), Is.EqualTo((8, 5)));
            Assert.That(dealt, Is.EqualTo((8, 5)), "what the enemy phase deals");
            Assert.That(s.Player.Guard, Is.EqualTo(0), "the board is not touched");
        }
    }
}
