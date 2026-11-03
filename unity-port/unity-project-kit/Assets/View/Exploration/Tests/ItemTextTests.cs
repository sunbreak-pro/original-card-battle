// #598: the exploration screen names an item only from ItemCatalogue. An id the catalogue does not know
// stops the screen, as ItemCatalogue.Tool and Consumable do, instead of printing the raw id.
using System;
using DungeonContent;
using Exploration;
using NUnit.Framework;

namespace Exploration.Tests
{
    public class ItemTextTests
    {
        [Test]
        public void EveryCatalogueItem_HasItsNameAndNote()
        {
            foreach (ItemDef item in ItemCatalogue.All)
            {
                Assert.That(ExplorationText.ItemName(item.Id), Is.EqualTo(item.Name));
                Assert.That(ExplorationText.ItemNote(item.Id), Does.Not.Contain(".md"), "the note drops the design-document pointers");
            }
        }

        [TestCase("no_such_item")]
        [TestCase("")]
        [TestCase(null)]
        public void AnIdTheCatalogueDoesNotKnow_Throws(string id)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ExplorationText.ItemName(id));
            Assert.Throws<ArgumentOutOfRangeException>(() => ExplorationText.ItemNote(id));
        }
    }
}
