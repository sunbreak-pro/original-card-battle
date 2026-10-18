using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// §12 の連戦の並び (#52): the default three, roster §9's nine, and an order spelt out for the
    /// layer bench. Moved from ChainRunTests when #518 removed ChainRun; the order stays because the
    /// layer bench (BattleCore.Sim) and the two-enemy battle read it.
    /// </summary>
    public class ChainOrderTests
    {
        [Test]
        public void TheDefaultOrder_IsSection12sThree_AndNineMayBePicked()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ChainOrder.Default.Select(b => b.ToString()), Is.EqualTo(new[] { "polearm_warped@1", "shadow_hound@2", "armored_warden@4" }));
                Assert.That(ChainOrder.Default.Count, Is.EqualTo(Constants.ChainBattlesDefault));
                Assert.That(ChainOrder.Nine.Count, Is.EqualTo(9));
                Assert.That(ChainOrder.Nine[2].EnemyIds, Is.EqualTo(new[] { "polearm_warped", "crossbow_hunter" }), "roster §9's third is the pair");
                Assert.That(ChainOrder.Nine.Select(b => b.Layer), Is.Ordered, "the layer only goes down");
                Assert.That(ChainOrder.Nine.Select(b => b.FieldCells), Is.EqualTo(new[] { 6, 6, 7, 6, 6, 7, 6, 6, 7 }), "7 for a size-2 enemy and for the pair");
                Assert.That(ChainOrder.Nine.SelectMany(b => b.Defs), Has.None.Null, "every id is one the core knows");
            });
        }

        [Test]
        public void AnOrderSpeltOut_IsParsed_AndAWrongOneIsRefused()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ChainOrder.Parse("3"), Is.SameAs(ChainOrder.Default));
                Assert.That(ChainOrder.Parse("9"), Is.SameAs(ChainOrder.Nine));
                Assert.That(ChainOrder.Parse("nine"), Is.SameAs(ChainOrder.Nine));

                var custom = ChainOrder.Parse("shadow_hound, mist_archer@5,polearm_warped+crossbow_hunter");
                Assert.That(custom.Select(b => b.ToString()), Is.EqualTo(new[] { "shadow_hound@1", "mist_archer@5", "polearm_warped+crossbow_hunter@5" }),
                    "a battle with no layer stays on the one before");

                Assert.That(() => ChainOrder.Parse("polearm_warped,nobody"), Throws.InstanceOf<KeyNotFoundException>());
                Assert.That(() => ChainOrder.Parse("polearm_warped@8"), Throws.InstanceOf<ArgumentOutOfRangeException>());
                Assert.That(() => ChainOrder.Parse("polearm_warped,,shadow_hound"), Throws.ArgumentException);
                Assert.That(() => ChainOrder.Parse("polearm_warped+shadow_hound+mist_archer+pack_alpha"),
                    Throws.ArgumentException, "ENEMIES_MAX 3");
                Assert.That(() => ChainOrder.Parse("armored_warden+abyss_angler"), Throws.ArgumentException,
                    "two size-2 enemies need 9 cells, wider than FIELD_CELLS_MAX 8");
            });
        }
    }
}
