using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// #820: the trial reported Guard 6 taking an attack 8 for 8. The core subtracts the Guard; the 8
    /// that got through was a 14 — the printed 8 plus a fired 重撃 — which the screen did not tell apart.
    /// </summary>
    public class GuardSoakTests
    {
        private static readonly IRng NoRng = new FixedRng(0.9999999);

        private static BattleState Start(CardDef card, int gap, int enemyGuard)
        {
            var deck = Cards.BuildDeck(new[] { card }, copies: 12);
            var state = TurnLoop.Start(new BattleSetup(Fixtures.Enemy(), deck, BattleSetup.SliceFieldCells, StartGap: gap), NoRng).State;
            state = TurnLoop.BeginPlayerTurn(state, NoRng).State;
            // The Guard an enemy carries into the player's turn (its action's and its 構え's), set directly.
            return state.WithEnemy(state.Enemy with { Guard = enemyGuard });
        }

        [Test]
        public void EnemyGuard6_PlainAttack8_TakesTwoHp_AndLeavesGuard0()
        {
            var plain = Fixtures.Card("plain_8", face: new Face(Power: 8, Reach: new Reach(0, 3)));
            var state = Start(plain, gap: 1, enemyGuard: 6);
            int hp = state.Enemy.Hp;

            var play = TurnLoop.PlayCard(state, state.Hand[0].InstanceId, NoRng);

            var blow = play.Events.OfType<DamageDealt>().Single();
            Assert.That(blow.Raw, Is.EqualTo(8));
            Assert.That(blow.Absorbed, Is.EqualTo(6));
            Assert.That(blow.Damage, Is.EqualTo(2));
            Assert.That(play.State.Enemy.Hp, Is.EqualTo(hp - 2));
            Assert.That(play.State.Enemy.Guard, Is.EqualTo(0));
        }

        [Test]
        public void EnemyGuard6_ThornShotWithItsHeavyBlowLit_IsRaw14_SoEightGetsThrough()
        {
            // Gap 1: the dummy's omen is its mid-range hit, an attack, so 棘の一射's 重撃 fires.
            var state = Start(CardCatalog.ThornShot, gap: 1, enemyGuard: 6);
            Assert.That(state.Omen!.Label.Kind, Is.EqualTo(OmenKind.Attack));
            int hp = state.Enemy.Hp;
            string id = state.Hand[0].InstanceId;

            var preview = TurnLoop.Preview(state, id)!;
            Assert.That(preview.TraitHolds, Is.True);
            Assert.That(preview.RawPower, Is.EqualTo(14), "the printed 8 plus 重撃's 6");
            Assert.That(preview.Damage, Is.EqualTo(8));

            var blow = TurnLoop.PlayCard(state, id, NoRng).Events.OfType<DamageDealt>().Single();
            Assert.That(blow.Raw, Is.EqualTo(14));
            Assert.That(blow.Absorbed, Is.EqualTo(6));
            Assert.That(blow.Damage, Is.EqualTo(8));
            Assert.That(blow.TargetHpAfter, Is.EqualTo(hp - 8));
            Assert.That(blow.TargetGuardAfter, Is.EqualTo(0));
        }
    }
}
