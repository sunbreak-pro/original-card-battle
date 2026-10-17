// #92: the list of drawn UI parts (battle-visual-v1 §11). Every Appendix A icon is put on one side —
// replaced by art or kept as code — every status of §7.3 has its art, and every part has its own file.
using System.Collections.Generic;
using NUnit.Framework;

namespace UiArt.Tests
{
    public class UiArtCatalogTests
    {
        // battle-visual-v1 付録 A, in its order.
        private static readonly string[] AppendixA =
        {
            "威力（剣）", "Guard（盾）", "HP（ハート）", "回復", "前へ", "後ろへ", "間合い", "永続（∞）", "スタミナの粒",
            "ドロー / 山札", "捨札", "除外", "スタンス（旗）", "スキル", "ターン（砂時計）", "階層", "瘴気", "手記",
            "自分（人型）", "狙うマス（斜線）", "状態付与（印）", "出血", "脆化", "鈍足", "威圧", "疲労", "強化", "集中",
            "見切り", "俊敏", "再生", "「+n」", "休み（横線）", "速さ ×1.0（山形 1 本）", "速さ ×1.25（山形 2 本）",
            "速さ ×1.5（山形 3 本）",
        };

        [Test]
        public void EveryAppendixAIcon_IsEitherDrawnOrKeptAsCode()
        {
            var drawn = new HashSet<string>();
            foreach (UiArtPart part in UiArtCatalog.Parts)
            {
                if (part.Kind != UiArtKind.EffectMark && part.Fallback != "") drawn.Add(part.Fallback);
            }
            var code = new HashSet<string>(UiArtCatalog.StaysCode);
            foreach (string icon in AppendixA)
            {
                Assert.That(drawn.Contains(icon) ^ code.Contains(icon), Is.True, icon + " must be on exactly one side");
            }
            Assert.That(drawn.Count + code.Count, Is.EqualTo(AppendixA.Length), "no name outside Appendix A");
        }

        [Test]
        public void EveryStatusOfSection73_HasItsArt()
        {
            string[] statuses = { "出血", "脆化", "鈍足", "威圧", "疲労", "強化", "集中", "見切り", "俊敏", "再生", "瘴気纏い", "呪縛" };
            foreach (string name in statuses)
            {
                UiArtPart part = System.Array.Find(UiArtCatalog.Parts, p => p.Name == name);
                Assert.That(part, Is.Not.Null, name);
                Assert.That(part.Kind, Is.EqualTo(UiArtKind.Status).Or.EqualTo(UiArtKind.BossStatus), name);
            }
        }

        [Test]
        public void TheFourAttributes_AreDrawn()
        {
            int attributes = System.Array.FindAll(UiArtCatalog.Parts, p => p.Kind == UiArtKind.Attribute).Length;
            Assert.That(attributes, Is.EqualTo(4), "battle_core_v4 §2.1: attack, defense, skill, stance");
        }

        [Test]
        public void EveryPart_HasItsOwnFile()
        {
            var paths = new HashSet<string>();
            foreach (UiArtPart part in UiArtCatalog.Parts)
            {
                Assert.That(part.Id, Does.Match("^[a-z0-9-]+$"), "file names are lower-case kebab: " + part.Id);
                Assert.That(paths.Add(part.ResourcePath), Is.True, "two parts load " + part.ResourcePath);
                Assert.That(part.ResourcePath, Does.StartWith("UiArt/icon-"));
            }
        }

        [Test]
        public void TheSource_IsLargeEnoughForTheLargestUse()
        {
            foreach (UiArtPart part in UiArtCatalog.Parts)
            {
                Assert.That(UiArtCatalog.SourcePx, Is.GreaterThanOrEqualTo(part.ShownPx * 3), part.Id);
            }
        }

        [Test]
        public void Find_ReturnsThePartOrNull()
        {
            Assert.That(UiArtCatalog.Find("bleed").Name, Is.EqualTo("出血"));
            Assert.That(UiArtCatalog.Find("receiver"), Is.Null, "the receiver is gone (battle-visual-v1 decision 10)");
        }
    }
}
