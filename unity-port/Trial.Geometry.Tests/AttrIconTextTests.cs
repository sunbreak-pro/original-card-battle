using NUnit.Framework;

namespace Trial.Geometry.Tests
{
    public class AttrIconTextTests
    {
        private const string S = "〓";
        private const string Hidden = "<color=#00000000>" + S + "</color>";

        [TestCase("連動 攻撃", "連動 " + S, new[] { "attack" })]
        [TestCase("連動 防御", "連動 " + S, new[] { "defense" })]
        [TestCase("連動 スキル", "連動 " + S, new[] { "skill" })]
        [TestCase("連動 スタンス", "連動 " + S, new[] { "stance" })]
        [TestCase("置く時 予兆が攻撃", "置く時 予兆が" + S, new[] { "attack" })]
        [TestCase("このターンに防御の札を出していれば、この札の威力 13 → 18", "このターンに" + S + "の札を出していれば、この札の威力 13 → 18", new[] { "defense" })]
        [TestCase("連動防御→威力+5", "連動" + S + "→威力+5", new[] { "defense" })]
        [TestCase("攻撃スタンス防御", S + S + S, new[] { "attack", "stance", "defense" })]
        public void Of_ReplacesEachAttributeWordWithOneSlot(string text, string display, string[] attrs)
        {
            AttrIconText t = AttrIconText.Of(text);
            Assert.That(t.Original, Is.EqualTo(text));
            Assert.That(t.Display, Is.EqualTo(display));
            Assert.That(t.Attrs, Is.EqualTo(attrs));
            Assert.That(t.HasIcons, Is.True);
        }

        [TestCase("素直な札")]
        [TestCase("特性なし（+2 込み）")]
        [TestCase("→ スタミナ +1")]
        [TestCase("移動後")]
        [TestCase("→ 追撃")]
        [TestCase("")]
        public void Of_LeavesTextWithoutAttributeWords(string text)
        {
            AttrIconText t = AttrIconText.Of(text);
            Assert.That(t.Display, Is.EqualTo(text));
            Assert.That(t.HasIcons, Is.False);
            Assert.That(t.Printed, Is.EqualTo(text));
        }

        [Test]
        public void Of_ReadsNullAsEmpty()
        {
            AttrIconText t = AttrIconText.Of(null);
            Assert.That((t.Original, t.Display, t.Printed), Is.EqualTo(("", "", "")));
        }

        [Test]
        public void MapRange_MovesARangeAfterAWordOntoTheDisplay()
        {
            // 「スタンス 連動」: 連動 sits at 5..7 of the text and at 2..4 of the display
            AttrIconText t = AttrIconText.Of("スタンス 連動");
            t.MapRange(5, 2, out int start, out int length);
            Assert.That((start, length), Is.EqualTo((2, 2)));
            Assert.That(t.Display.Substring(start, length), Is.EqualTo("連動"));
        }

        [Test]
        public void MapRange_KeepsARangeBeforeAWord()
        {
            AttrIconText t = AttrIconText.Of("連動 スタンス");
            t.MapRange(0, 2, out int start, out int length);
            Assert.That((start, length), Is.EqualTo((0, 2)));
        }

        [Test]
        public void MapRange_ARangeEndingInsideAWordTakesItsSlot()
        {
            AttrIconText t = AttrIconText.Of("予兆が攻撃");
            t.MapRange(2, 2, out int start, out int length);
            Assert.That((start, length), Is.EqualTo((2, 2)));
            Assert.That(t.Display.Substring(start, length), Is.EqualTo("が" + S));
        }

        [Test]
        public void MapRange_AnEmptyRangeIsEmpty()
        {
            AttrIconText t = AttrIconText.Of("連動 攻撃");
            t.MapRange(4, 0, out _, out int length);
            Assert.That(length, Is.EqualTo(0));
        }

        [Test]
        public void Rich_HidesEachSlotAndKeepsTheRest()
        {
            Assert.That(AttrIconText.Of("連動 攻撃").Printed, Is.EqualTo("連動 " + Hidden));
            Assert.That(AttrIconText.Rich("a" + S + "\nb" + S), Is.EqualTo("a" + Hidden + "\nb" + Hidden));
        }

        [Test]
        public void Rich_ShowsTheTextAfterASlotAgain()
        {
            // TMP has no </alpha>: the hiding must close with a tag TMP pops, or everything after the first icon vanishes.
            string printed = AttrIconText.Of("このターンに防御の札を出していれば、この札の威力 13 → 18").Printed;
            Assert.That(printed, Is.EqualTo("このターンに" + Hidden + "の札を出していれば、この札の威力 13 → 18"));
            Assert.That(printed, Does.Not.Contain("alpha"));
        }

        [Test]
        public void Rich_KeepsALessThanSignAsText()
        {
            Assert.That(AttrIconText.Rich("a<b" + S), Is.EqualTo("<noparse>a<b</noparse>" + Hidden));
        }

        [Test]
        public void Words_NameTheFourAttributesOnce()
        {
            Assert.That(AttrIconText.Words.Count, Is.EqualTo(4));
            Assert.That(AttrIconText.Words[0].Key + AttrIconText.Words[1].Key + AttrIconText.Words[2].Key + AttrIconText.Words[3].Key,
                Is.EqualTo("攻撃防御スキルスタンス"));
        }
    }
}
