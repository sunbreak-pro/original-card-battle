// #242: the card face of battle-visual-v1 as the script carries it — the lamp for each enemy (§6.3),
// the values and rows (§6.1), the trait box, the hover line (§7.2), the detail (§7.4), and the status
// chips' panel text (§7.3). Runs under `dotnet test` (Depiction.Bridge.Tests.csproj) and in Unity's
// EditMode runner from the same file.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class CardFaceVisualTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        private static readonly CardDef[] Hand =
        {
            CardCatalog.KesaCut, CardCatalog.ReachThrust, CardCatalog.Brace, CardCatalog.BackLeap, CardCatalog.RockStance,
        };

        /// <summary>Two 瘴牙の走竜 side by side past turn 1's start, the first one <paramref name="gap"/> away.</summary>
        private static BattleState TwoHounds(int gap)
        {
            var setup = new BattleSetup(Enemies.ShadowHound, Cards.BuildDeck(Hand, 1), 8,
                StartGap: gap, MoreEnemies: new[] { Enemies.ShadowHound });
            BattleState state = TurnLoop.Start(setup, NoShuffle).State;
            return TurnLoop.BeginPlayerTurn(state, NoShuffle).State;
        }

        /// <summary>The polearm alone at the given gap, past turn 1's start.</summary>
        private static BattleState Polearm(int gap)
        {
            var setup = new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck(Hand, 1), BattleSetup.SliceFieldCells, StartGap: gap);
            BattleState state = TurnLoop.Start(setup, NoShuffle).State;
            return TurnLoop.BeginPlayerTurn(state, NoShuffle).State;
        }

        private static string Id(BattleState state, CardDef def)
        {
            return state.Hand.First(c => c.Def == def).InstanceId;
        }

        private static bool Lit(BattleState state, CardDef def, out List<bool> perEnemy)
        {
            return CardFaceText.Lamps(state, Id(state, def), out perEnemy);
        }

        // ---- §6.3 the lamp --------------------------------------------------------------------

        [Test]
        public void ACardAimedAtOne_Lights_WhenAnyEnemyInReachSatisfiesIt_AndKeepsEachEnemysVerdict()
        {
            BattleState state = TwoHounds(0);
            Assert.That(new[] { state.GapTo(0), state.GapTo(1) }, Is.EqualTo(new[] { 0, 1 }));

            // 袈裟斬り (0〜1, 間合い 0 → +5): both hounds are in reach, only the first stands adjacent.
            bool lit = Lit(state, CardCatalog.KesaCut, out List<bool> perEnemy);

            Assert.That(lit, Is.True);
            Assert.That(perEnemy, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public void TheFramedEnemy_ReLightsTheLamp_FromTheVerdictTheScriptCarries()
        {
            BattleState state = TwoHounds(1);
            Assert.That(new[] { state.GapTo(0), state.GapTo(1) }, Is.EqualTo(new[] { 1, 2 }));
            CardInstance card = state.Hand.First(c => c.Def == CardCatalog.ReachThrust);
            CardFace face = CoreText.Face(card, TurnLoop.Preview(state, card.InstanceId), false);
            face.TraitLit = CardFaceText.Lamps(state, card.InstanceId, out face.LitOn);

            // 伸び突き (1〜2, 間合い 2 以上 → +5): lit in the hand because the second hound stands at 2.
            Assert.That(face.TraitLit, Is.True);
            Assert.That(face.LitFor(0), Is.False, "framing the hound at 1 puts the lamp out");
            Assert.That(face.LitFor(1), Is.True);
        }

        [Test]
        public void ACardThatHoldsOnlyOutOfReach_StaysDark()
        {
            // 伸び突き at a gap of 3: 間合い 2 以上 holds, but 1〜2 does not reach the polearm.
            BattleState state = Polearm(3);
            CardInstance card = state.Hand.First(c => c.Def == CardCatalog.ReachThrust);
            Assert.That(TurnLoop.Preview(state, card.InstanceId)!.TraitHolds, Is.True, "the condition itself holds");

            Assert.That(Lit(state, CardCatalog.ReachThrust, out List<bool> perEnemy), Is.False);
            Assert.That(perEnemy, Is.EqualTo(new[] { false }));
        }

        [Test]
        public void ASelfCard_ReadsTheNearestEnemy_AndCarriesNoVerdictPerEnemy()
        {
            BattleState state = TwoHounds(0);
            var attack = new Omen("bite", new OmenLabel(OmenKind.Attack));
            var guard = new Omen("crouch", new OmenLabel(OmenKind.Guard));

            // 後ろ跳び (予兆が攻撃 → Guard +3) reads the nearest hound, the first one.
            BattleState nearAttacks = state.WithOmen(0, attack).WithOmen(1, guard);
            BattleState farAttacks = state.WithOmen(0, guard).WithOmen(1, attack);

            Assert.That(Lit(nearAttacks, CardCatalog.BackLeap, out List<bool> perEnemy), Is.True);
            Assert.That(perEnemy, Is.Empty, "a card aimed at nobody has one lamp everywhere");
            Assert.That(Lit(farAttacks, CardCatalog.BackLeap, out _), Is.False, "the far hound's attack is not read");
        }

        [Test]
        public void ACardWithNoTrait_IsNeverLit()
        {
            var setup = new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck(new[] { CardCatalog.Thrust, CardCatalog.KesaCut }, 3),
                BattleSetup.SliceFieldCells, StartGap: 0);
            BattleState state = TurnLoop.BeginPlayerTurn(TurnLoop.Start(setup, NoShuffle).State, NoShuffle).State;

            Assert.That(Lit(state, CardCatalog.Thrust, out List<bool> perEnemy), Is.False);
            Assert.That(perEnemy, Is.Empty);
        }

        [Test]
        public void TheWritersHand_CarriesTheAggregatedLamp()
        {
            var setup = new BattleSetup(Enemies.ShadowHound, Cards.BuildDeck(Hand, 1), 8,
                StartGap: 1, MoreEnemies: new[] { Enemies.ShadowHound });
            BattleState state = TurnLoop.Start(setup, NoShuffle).State;
            var writer = new CoreScriptWriter(setup.Enemy);
            writer.Opening(state);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            DepictionFrame frame = writer.Write(begin.Events, begin.State)[0].After;

            CardFace thrust = frame.Hand.First(c => c.Name == "伸び突き");
            Assert.That(thrust.TraitLit, Is.True, "the hound at 2 satisfies it");
            Assert.That(thrust.LitOn, Is.EqualTo(new[] { false, true }));
        }

        // ---- §6.1 / §9 the face -----------------------------------------------------------------

        [Test]
        public void APlainCard_PrintsThePlainBox_AndSaysWhereItsTwoWent()
        {
            CardFace face = CoreText.FaceOf(CardCatalog.Thrust);

            Assert.That(face.Plain, Is.True);
            Assert.That(new[] { face.TraitTop, face.TraitBottom }, Is.EqualTo(new[] { "素直な札", "特性なし（+2 込み）" }));
            Assert.That(face.HoverLine, Is.EqualTo("特性なし。そのぶん威力が 2 高い（23 に込み）"));
            Assert.That(face.Values.Select(v => v.Word + " " + v.Number), Is.EqualTo(new[] { "威力 23" }));
        }

        [TestCase("kesa_cut", "間合い 0", "→ 威力 +5", "隣り合っていれば、この札の威力 15 → 20")]
        [TestCase("reach_thrust", "間合い 2 以上", "→ 威力 +5", "間合い 2 以上なら、この札の威力 15 → 20")]
        [TestCase("brace", "温存 残り 6 以上", "→ 次ターン回復 +1", "出した後スタミナが 6 以上残れば、次ターンの回復 3 → 4")]
        [TestCase("rock_stance", "置く時 予兆が攻撃", "→ Guard +3（1 回）", "置くときに敵の予兆が攻撃なら、その場で Guard +3（1 回だけ）")]
        [TestCase("war_cry", "連打", "→ 敵に脆化 2", "このターン直前に出した札もスキルなら、敵に脆化 2 も付ける")]
        [TestCase("back_leap", "予兆が攻撃", "→ Guard +3", "いちばん近い敵の予兆が攻撃なら、この札の Guard 4 → 7")]
        [TestCase("body_check", "間合い 0", "→ 重撃", "隣り合っていれば、威力 5 → 11。ただし次ターンの回復 3 → 2")]
        [TestCase("stone_throw", "間合い 2 以上", "→ コスト −1", "間合い 2 以上なら、この札のコスト 2 → 1")]
        [TestCase("observe", "手薄", "→ ドロー +1", "出した直後の手札が 2 枚以下なら、さらに 1 枚引く")]
        public void TheSampleCards_ReadAsSection9Writes(string id, string top, string bottom, string hover)
        {
            CardFace face = CoreText.FaceOf(CardCatalog.ById(id));

            Assert.That(face.TraitTop, Is.EqualTo(top));
            Assert.That(face.TraitBottom, Is.EqualTo(bottom));
            Assert.That(face.HoverLine, Is.EqualTo(hover));
            Assert.That(face.Plain, Is.False);
        }

        [Test]
        public void TheCardWithTwoTraits_PutsOneTraitOnEachRow()
        {
            CardFace face = CoreText.FaceOf(CardCatalog.ById("last_stand"));

            Assert.That(new[] { face.TraitTop, face.TraitBottom }, Is.EqualTo(new[] { "間合い2以上→威力+5", "死力→威力+3" }));
            Assert.That(face.HoverLine, Does.Contain("。"), "two sentences, one per trait");
        }

        [Test]
        public void StatusesLeadTheRows_AndAreNeverAValueColumn()
        {
            CardFace body = CoreText.FaceOf(CardCatalog.ById("body_check"));
            CardFace stone = CoreText.FaceOf(CardCatalog.ById("stone_throw"));
            CardFace cry = CoreText.FaceOf(CardCatalog.ById("war_cry"));

            Assert.That(body.TextLines.First(), Is.EqualTo("敵に 鈍足 2"));
            Assert.That(stone.TextLines.First(), Is.EqualTo("敵に 脆化 2・威圧 2"));
            Assert.That(cry.TextLines, Is.EqualTo(new[] { "敵に 威圧 2", "自分に 強化 2" }));
            Assert.That(body.Values.Select(v => v.Kind), Is.EqualTo(new[] { CardValueKind.Power }));
        }

        [Test]
        public void AMoveIsAValueColumn_WithItsWord()
        {
            CardFace leap = CoreText.FaceOf(CardCatalog.BackLeap);

            Assert.That(leap.Values.Select(v => v.Kind), Is.EqualTo(new[] { CardValueKind.Back, CardValueKind.Guard }));
            Assert.That(leap.Values.Select(v => v.Word + " " + v.Number), Is.EqualTo(new[] { "離れる 2", "Guard 4" }));
        }

        [Test]
        public void AStanceCard_HasNoValues_AndItsRowsAreLasting()
        {
            CardFace face = CoreText.FaceOf(CardCatalog.RockStance);

            Assert.That(face.Values, Is.Empty);
            Assert.That(face.Lasting, Is.True);
            Assert.That(face.TextLines, Is.EqualTo(new[] { "毎ターン開始に Guard +3" }));
        }

        [Test]
        public void EveryCatalogCard_KeepsToTheFacesLimits()
        {
            var misfits = new List<string>();
            foreach (CardDef def in CardCatalog.All)
            {
                CardFace face = CoreText.FaceOf(def);
                if (face.Values.Count > CardFaceText.MaxValues) misfits.Add(def.Name + ": " + face.Values.Count + " values");
                int rows = face.Lasting ? CardFaceText.MaxLastingLines : CardFaceText.MaxTextLines;
                if (face.TextLines.Count > rows) misfits.Add(def.Name + ": " + face.TextLines.Count + " rows");
                if (face.TraitTop.Length == 0 || face.TraitBottom.Length == 0) misfits.Add(def.Name + ": an empty trait row");
                if (face.HoverLine.Length == 0) misfits.Add(def.Name + ": no hover line");
                if (face.PrintedCost != def.Cost) misfits.Add(def.Name + ": printed cost " + face.PrintedCost);
                if (face.Detail == null || face.Detail.Body.Length == 0) misfits.Add(def.Name + ": no detail");
            }
            Assert.That(misfits, Is.Empty);
        }

        /// <summary>
        /// §6.1: each trait row inside the box's 16 px text width (216 − 12 − 7 = 197 px of box, less
        /// the 16 px lamp, its 8 px gaps and the 4 px right margin: 165 px). The stand-in measure of
        /// CardFaceFitTests (a Japanese letter one em, a space 0.3 em, any other letter 0.6 em) is
        /// wider than the font, so a row that fits here fits on screen.
        /// </summary>
        [Test]
        public void EveryTraitRow_FitsTheTraitBoxAt16Px()
        {
            const float width = 165f;
            const int size = 16;
            var misfits = new List<string>();
            foreach (CardDef def in CardCatalog.All)
            {
                CardFace face = CoreText.FaceOf(def);
                foreach (string row in new[] { face.TraitTop, face.TraitBottom })
                {
                    float ems = row.Sum(c => c >= '⺀' ? 1f : c == ' ' ? 0.3f : 0.6f);
                    if (ems * size > width) misfits.Add(def.Name + " 「" + row + "」 " + ems * size + " px");
                }
            }
            Assert.That(misfits, Is.Empty);
        }

        [Test]
        public void TheDiscountedCost_IsMarked()
        {
            BattleState state = Polearm(2);
            var setup = new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck(new[] { CardCatalog.ById("stone_throw") }, 6),
                BattleSetup.SliceFieldCells, StartGap: 2);
            state = TurnLoop.BeginPlayerTurn(TurnLoop.Start(setup, NoShuffle).State, NoShuffle).State;
            CardInstance card = state.Hand.First();
            CardFace face = CoreText.Face(card, TurnLoop.Preview(state, card.InstanceId), false);

            Assert.That(face.PrintedCost, Is.EqualTo(2));
            Assert.That(face.Cost, Is.EqualTo(1), "石礫 costs one less from a gap of 2");
            Assert.That(face.CostDrop, Is.EqualTo(1));
        }

        // ---- §7.4 the detail ---------------------------------------------------------------------

        [Test]
        public void TheDetail_NamesOneAttribute_TheTargetAndTheReach()
        {
            CardDetail kesa = CoreText.FaceOf(CardCatalog.KesaCut).Detail;
            CardDetail brace = CoreText.FaceOf(CardCatalog.Brace).Detail;

            Assert.That(kesa.Name, Is.EqualTo("袈裟斬り"));
            Assert.That(kesa.KindLine, Is.EqualTo("攻撃 ・ 敵 1 体 ・ 間合い 0〜1"));
            Assert.That(kesa.Body, Does.Contain("隣り合っていれば、この札の威力 15 → 20"));
            Assert.That(brace.KindLine, Is.EqualTo("防御 ・ 自分"));
            Assert.That(brace.Terms.Select(t => t.Word), Does.Contain("温存"));
            Assert.That(brace.Body, Does.StartWith("Guard 9 を得る。"));
        }

        [Test]
        public void TheDetail_ListsTheTermsTheCardUses_WithTheirMeaning()
        {
            CardDetail body = CoreText.FaceOf(CardCatalog.ById("body_check")).Detail;

            Assert.That(body.Terms.Select(t => t.Word), Is.EqualTo(new[] { "間合い", "重撃" }));
            Assert.That(body.Terms[1].Meaning, Is.EqualTo("威力 +6。そのかわり次のターン開始のスタミナ回復が 1 減る"));
        }

        // ---- §7.3 the status chips ----------------------------------------------------------------

        [Test]
        public void AWordPutOnTheOpponent_IsASquareChip_WithItsEffectAndDecay()
        {
            StatusChip bleed = StatusChipText.Chip(StatusKind.Bleed, 2);

            Assert.That(bleed.Kind, Is.EqualTo(ChipKind.OnFoe));
            Assert.That(bleed.Title, Is.EqualTo("出血 2"));
            Assert.That(bleed.Group, Is.EqualTo("相手に付ける系"));
            Assert.That(bleed.Effect, Is.EqualTo("ターン開始に HP −2×スタック（いまは −4）"));
            Assert.That(bleed.Decay, Is.EqualTo("ターンごとに 1 減る"));
        }

        [Test]
        public void TheStatusTable_WritesEachPanel()
        {
            Assert.Multiple(() =>
            {
                Assert.That(StatusChipText.Chip(StatusKind.Fragile, 1).Effect, Is.EqualTo("次に受ける攻撃の威力が 1.5 倍（Guard で引く前の値）"));
                Assert.That(StatusChipText.Chip(StatusKind.Fragile, 1).Decay, Is.EqualTo("効くと 1 減る"));
                Assert.That(StatusChipText.Chip(StatusKind.Intimidate, 2).Effect, Is.EqualTo("次の行動の威力と Guard が −3"));
                Assert.That(StatusChipText.Chip(StatusKind.Fatigue, 2).Effect, Is.EqualTo("次のターン開始のスタミナ回復が −1"));
                Assert.That(StatusChipText.Chip(StatusKind.Empower, 2).Effect, Is.EqualTo("次の攻撃の威力が 1.5 倍"));
                Assert.That(StatusChipText.Chip(StatusKind.Regen, 3).Effect, Is.EqualTo("ターン開始に HP +2×スタック（いまは +6）"));
                Assert.That(StatusChipText.Chip(StatusKind.Parry, 1).Decay, Is.EqualTo("効くと 1 減る（Guard 0 で受けたときは減らない）"));
                Assert.That(StatusChipText.Chip(StatusKind.Regen, 3).Kind, Is.EqualTo(ChipKind.OnSelf));
                Assert.That(StatusChipText.Chip(StatusKind.Regen, 3).Group, Is.EqualTo("自分に付ける系"));
                Assert.That(StatusChipText.Chip(StatusKind.Binding, 1).Kind, Is.EqualTo(ChipKind.Boss));
            });
        }

        [Test]
        public void EveryStatusWord_HasAPanel()
        {
            foreach (StatusKind kind in Enum.GetValues(typeof(StatusKind)))
            {
                StatusChip chip = StatusChipText.Chip(kind, 1);
                Assert.That(chip.Effect, Is.Not.Empty, kind.ToString());
                Assert.That(chip.Decay, Is.Not.Empty, kind.ToString());
                Assert.That(chip.Group, Is.Not.Empty, kind.ToString());
            }
        }

        [Test]
        public void TheSameStanceTwice_IsOneLastingChip_ThatSaysItWorksTwice()
        {
            List<StatusChip> chips = StatusChipText.StanceChips(new[] { "岩の構え", "岩の構え" });

            Assert.That(chips, Has.Count.EqualTo(1));
            Assert.That(chips[0].Kind, Is.EqualTo(ChipKind.Lasting));
            Assert.That(chips[0].Title, Is.EqualTo("岩の構え ×2"));
            Assert.That(chips[0].Effect, Does.StartWith("毎ターン開始に Guard +3"));
            Assert.That(chips[0].Decay, Is.EqualTo("減らない（永続）"));
            Assert.That(StatusChipText.StanceChips(new[] { "岩の構え" })[0].Title, Is.EqualTo("岩の構え"), "once prints no ×1");
        }

        [Test]
        public void TheWritersUnits_CarryThePanelText()
        {
            BattleState state = Polearm(1);
            state = state with { Player = state.Player with { Statuses = StatusSet.Of((StatusKind.Regen, 2)) } };
            var writer = new CoreScriptWriter(state.EnemyDef);

            DepictionFrame frame = writer.Opening(state);

            StatusChip regen = frame.Player.Statuses.Single(c => c.Label == "再生");
            Assert.That(regen.Effect, Is.EqualTo("ターン開始に HP +2×スタック（いまは +4）"));
            Assert.That(regen.Kind, Is.EqualTo(ChipKind.OnSelf));
        }
    }
}
