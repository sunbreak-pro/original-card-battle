// dotnet-only: the scene texts of player.js, byte for byte (the expected strings are the JS functions' own output for the
// same inputs, evaluated with node on scenes/battle/player.js:65-201), the three core numbers BattlePlayer copies pinned to
// BattleCore's Constants, and the labels a scripted battle (DepictionRunner) puts on its numbers and in the pile list's log:
// a fired trait's 「+5 間合い」, a multiplier's 「×1.5 強化」, 重撃's 「+6 重撃」, 「Guard −5」 and the HP beside it.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using Depiction;
using NUnit.Framework;
using Trial.Battle.Flow.Tests.Fakes;

namespace Trial.Battle.Flow.Tests
{
    public class BattlePlayerTextTests
    {
        private List<Exception> _unhandled;

        [SetUp]
        public void SetUp()
        {
            Microtasks.Reset();
            _unhandled = new List<Exception>();
            Microtasks.Unhandled = e => _unhandled.Add(e);
        }

        [TearDown]
        public void TearDown()
        {
            Assert.That(_unhandled, Is.Empty, "no exception may reach Microtasks.Unhandled");
            Microtasks.Reset();
        }

        // ---- the core's numbers ---------------------------------------------------------------------------

        [Test]
        public void TheCopiedCoreNumbers_AreTheCores()
        {
            Assert.That(BattlePlayer.CoreNumbers.FragileMult, Is.EqualTo(Constants.FragileMult));
            Assert.That(BattlePlayer.CoreNumbers.EmpowerMult, Is.EqualTo(Constants.EmpowerMult));
            Assert.That(BattlePlayer.CoreNumbers.HeavyBlowPower, Is.EqualTo(Constants.HeavyBlowPower));
        }

        // ---- scene texts (node on player.js) -----------------------------------------------------------------

        [Test]
        public void TheSceneTexts_AreTheJsStrings()
        {
            Assert.That(BattlePlayer.FreeStepGuide, Is.EqualTo("俊敏で 1 マス動けます。矢印か「留まる」を選んでください（← / → と Enter）"));
            Assert.That(BattlePlayer.FreeStepRefusal, Is.EqualTo("先に、動く先の矢印か「留まる」を選んでください"));
            Assert.That(BattlePlayer.FreeStepStuck, Is.EqualTo("動けず"));
            Assert.That(BattlePlayer.DrawerGuide, Is.EqualTo("一覧を閉じるとプレイできます（Esc）"));
            Assert.That(BattlePlayer.JournalGuide, Is.EqualTo("手記を閉じるとプレイできます（Esc）"));
            Assert.That(BattlePlayer.WrongZoneOnEnemy("突き"), Is.EqualTo("「突き」は敵の上で離すと出せます"));
            Assert.That(BattlePlayer.WallTitle, Is.EqualTo("壁に当たる"));
            Assert.That(BattlePlayer.WallSide, Is.EqualTo("壁"));
            Assert.That(BattlePlayer.PulledWord, Is.EqualTo("引き寄せ"));
            Assert.That(BattlePlayer.Minus, Is.EqualTo("−"));
            Assert.That(BattlePlayer.ContinuePauseMs, Is.EqualTo(80f));
            Assert.That(BattlePlayer.LossWord, Is.EqualTo("力尽きた"));
            Assert.That(BattlePlayer.LogMax, Is.EqualTo(400));
        }

        [Test]
        public void TheLogLines_AreTheJsLines()
        {
            Assert.That(BattlePlayer.LogText.Turn("3"), Is.EqualTo("— ターン 3 —"));
            Assert.That(BattlePlayer.LogText.Turn(""), Is.EqualTo("— ターン  —"));
            Assert.That(BattlePlayer.LogText.EnemyTurn, Is.EqualTo("— 敵の番 —"));
            Assert.That(BattlePlayer.LogText.Mine("突き"), Is.EqualTo("自分: 突き"));
            Assert.That(BattlePlayer.LogText.Foe("錆槍", "払い"), Is.EqualTo("錆槍: 払い"));
            Assert.That(BattlePlayer.LogText.Trait("間合い +5"), Is.EqualTo("　特性: 間合い +5"));
            Assert.That(BattlePlayer.LogText.Status("", "鈍足", 2), Is.EqualTo("　状態: 鈍足 2"));
            Assert.That(BattlePlayer.LogText.Status("灰弩", "強化", 0), Is.EqualTo("　灰弩 状態: 強化 消えた"));
            Assert.That(BattlePlayer.LogText.Stance("岩の構え", 1), Is.EqualTo("　スタンス: 岩の構え"));
            Assert.That(BattlePlayer.LogText.Stance("岩の構え", 2), Is.EqualTo("　スタンス: 岩の構え（2 つ目）"));
            Assert.That(BattlePlayer.LogText.Rebuild, Is.EqualTo("　山札を作り直した"));
            Assert.That(BattlePlayer.LogText.Hp("自分", 12), Is.EqualTo("　自分 HP −12"));
            Assert.That(BattlePlayer.LogText.Guard("敵", 5), Is.EqualTo("　敵 Guard −5"));
            Assert.That(BattlePlayer.LogText.Wall("自分", 3), Is.EqualTo("　自分 壁 −3"));
            Assert.That(BattlePlayer.LogText.Fell("敵"), Is.EqualTo("　敵が倒れた"));
        }

        [Test]
        public void TheResultWords_AreTheJsWords()
        {
            Assert.That(BattlePlayer.ResultText.FellAt(new BattleChain(2, 3), 4), Is.EqualTo("2 戦目で倒れた"));
            Assert.That(BattlePlayer.ResultText.FellAt(null, 4), Is.EqualTo("第 4 層で倒れた"));
            Assert.That(BattlePlayer.ResultText.FellAt(new BattleChain(1, 1), 7), Is.EqualTo("第 7 層で倒れた"));
            Assert.That(BattlePlayer.ResultText.HpLeft(0, 50), Is.EqualTo("残 HP 0 / 50"));
            Assert.That(BattlePlayer.ResultText.PlayedHead, Is.EqualTo("出した札"));
            Assert.That(BattlePlayer.ResultText.Traits(3), Is.EqualTo("特性の発動 3 回"));
            Assert.That(BattlePlayer.ResultText.Turns(5), Is.EqualTo("5 ターン戦った"));
            Assert.That(BattlePlayer.ResultText.ToJournal, Is.EqualTo("手記へ"));
            Assert.That(BattlePlayer.ResultText.Beat("錆槍の竜兵・灰弩"), Is.EqualTo("錆槍の竜兵・灰弩を倒した。"));
            Assert.That(BattlePlayer.ResultText.Beat(""), Is.EqualTo("敵を倒した。"));
            Assert.That(BattlePlayer.ResultText.WonLine(4, 33, 50), Is.EqualTo("ターン 4 ・ 残りの HP 33 / 50"));
            Assert.That(BattlePlayer.ResultText.ChainAt(new BattleChain(2, 3)), Is.EqualTo("2 / 3 戦目"));
            Assert.That(BattlePlayer.ResultText.SwitchTitle(2), Is.EqualTo("2 戦目"));
        }

        [Test]
        public void TheConditionNames_AreTheJsNames()
        {
            string[] words = { "間合い0", "残4", "連動(攻)", "予兆", "相手出血", "自分強化", "初手", "残" };
            Assert.That(string.Join(",", words.Select(BattlePlayer.ConditionName)), Is.EqualTo("間合い,温存,連動,予兆,出血中,強化中,初手,残"));
        }

        /// <summary>JS's `.` stops at a line terminator and its `$` is the input's end only (.NET's `$` also matches before a final \n).</summary>
        [Test]
        public void TheConditionNames_MatchAsJsRegexesDo_AtLineTerminators()
        {
            Assert.That(BattlePlayer.ConditionName("相手出血\n"), Is.EqualTo("相手出血\n"));
            Assert.That(BattlePlayer.ConditionName("自分強\r化"), Is.EqualTo("自分強\r化"));
            Assert.That(BattlePlayer.ConditionName("相手出血"), Is.EqualTo("出血中"));
        }

        // ---- a scripted battle's labels and log ------------------------------------------------------------

        private static DepictionFrame Frame(int playerHp, int enemyHp)
        {
            return new DepictionFrame
            {
                Corner = new CornerFrame { Turn = 1, Floor = 1, ChainIndex = 1, ChainTotal = 1 },
                Player = new UnitFrame { Hp = playerHp, HpMax = 50 },
                Enemy = new UnitFrame { Hp = enemyHp, HpMax = 40, ArtId = Enemies.PolearmWarpedId },
            };
        }

        private static DepictionEvent EnemyAction(int order, string title, DepictionFrame after, params Cue[] cues)
        {
            return new DepictionEvent { Order = order, Kind = DepictionEventKind.EnemyAction, Title = title, Cues = cues.ToList(), After = after };
        }

        private static Cue Slash(int amount, int hpAfter) =>
            new Cue { Kind = CueKind.Slash, Target = UnitSide.Player, Source = UnitSide.Enemy, Amount = amount, HpAfter = hpAfter, System = StrikeSystem.Thrust, Intensity = EffectStrength.Uniform };

        private static Cue Windup() => new Cue { Kind = CueKind.EnemyWindup, Target = UnitSide.Enemy, System = StrikeSystem.Thrust };

        private static Cue Trait(string text) => new Cue { Kind = CueKind.TraitFire, Target = UnitSide.Enemy, Text = text };

        [Test]
        public void AScriptedBattle_LabelsItsNumbers_AndLogsItsLines_AsTheJsDoes()
        {
            var script = new DepictionScript { Opening = Frame(50, 40) };
            script.Events.Add(new DepictionEvent { Order = 1, Kind = DepictionEventKind.TurnStart, Title = "", After = Frame(50, 40) });
            // A fired trait of the enemy's action (間合い0 +5) and the 強化 it spent after its blow (§5.10): 「−12 +5 間合い ×1.5 強化」.
            script.Events.Add(EnemyAction(2, "突き", Frame(38, 40), Trait("間合い0 +5"), Windup(), Slash(12, 38),
                new Cue { Kind = CueKind.StatusChange, Target = UnitSide.Enemy, Text = "強化", Amount = -1, StacksAfter = 0 }));
            // 重撃 is named, not counted: 「−6 +6 重撃」.
            script.Events.Add(EnemyAction(3, "重い一撃", Frame(32, 40), Trait("初手 重撃"), Windup(), Slash(6, 32)));
            // A blow Guard took part of: 「Guard −5」 and the HP number 24 px beside it, on the figure's back.
            script.Events.Add(EnemyAction(4, "二連", Frame(25, 40), Windup(),
                new Cue { Kind = CueKind.Slash, Target = UnitSide.Player, Source = UnitSide.Enemy, Amount = 12, HpAfter = Cue.Unchanged, System = StrikeSystem.Slash, Intensity = 2 },
                new Cue { Kind = CueKind.GuardBlock, Target = UnitSide.Player, Amount = 5, GuardAfter = 0 },
                new Cue { Kind = CueKind.Hit, Target = UnitSide.Player, Amount = 7, HpAfter = 25, Intensity = 2 }));
            var rig = new Rig(new DepictionRunner(script));
            rig.Start();
            Assert.That(rig.RunUntil(() => rig.Finished > 0, 60_000f), Is.True);
            Assert.That(rig.Player.PlanErrors, Is.EqualTo(0), string.Join("\n", rig.Log.Errors));

            var numbers = rig.Field.Floats.Where(f => f.Text.StartsWith(BattlePlayer.Minus, StringComparison.Ordinal)).ToList();
            Assert.That(numbers.Select(f => f.Text), Is.EqualTo(new[] { "−12", "−6", "−7" }));
            Assert.That(numbers[0].O.Side, Is.EqualTo("+5 間合い ×1.5 強化"));
            Assert.That(numbers[1].O.Side, Is.EqualTo("+6 重撃"));
            Assert.That(numbers[2].O.Side, Is.EqualTo(""));
            Assert.That(numbers.All(f => f.Size == BattleLayout.NumberFont(EffectStrength.Uniform)), Is.True);
            // The HP number of the guarded blow stands beside the tag (anchored to its edge, riding its height).
            Assert.That(numbers[2].O.Anchor, Is.Not.EqualTo(FloatAnchor.None));
            Assert.That(numbers[2].O.FollowY, Is.Not.Null);
            var tag = rig.Field.Floats.Single(f => f.Text.StartsWith("Guard", StringComparison.Ordinal));
            Assert.That(tag.Text, Is.EqualTo("Guard −5"));
            Assert.That(tag.Size, Is.EqualTo(L.GuardNumberSize));
            Assert.That(tag.O.Anchor, Is.EqualTo(FloatAnchor.Left), "the blow came from the enemy's side: the tag's left edge on the chest's right");
            // The enemy's condition word swelled in its badge.
            Assert.That(rig.Hud.BadgeOf(0).TraitWords, Is.EqualTo(new[] { "間合い", "初手" }));

            Assert.That(rig.Player.LogLines(), Is.EqualTo(new[]
            {
                "— ターン 1 —",
                "敵: 突き", "　特性: 間合い +5", "　自分 HP −12", "　敵 状態: 強化 消えた",
                "敵: 重い一撃", "　特性: 初手 重撃", "　自分 HP −6",
                "敵: 二連", "　自分 Guard −5", "　自分 HP −7",
            }));
            Assert.That(rig.Hud.Log, Is.EqualTo(rig.Player.LogLines()), "the hud's ログ全文 tab shows the same lines");
        }

        private static CardFace Card(string id, string name, CardAim aim, CardValueKind value, CardKind kind) => new CardFace
        {
            Id = id, Name = name, Cost = 1, Kind = kind, Aim = aim,
            Values = new List<CardValue> { new CardValue { Kind = value, Number = "1", Word = "" } },
        };

        /// <summary>
        /// §3.14 / §5.7 順 5 (a move that went nowhere leans 40 ms that way, after its stamina) and §5.7 順 7 (the blow on an
        /// enemy after its own push in the card's event is the wall's: the edge lights, the number says 壁, the log 壁 −n),
        /// with 「押し出し」 on the enemy the push moved away.
        /// </summary>
        [Test]
        public void ThePlayersCards_LeanAMoveThatWentNowhere_AndStrikeAWall()
        {
            CardFace step = Card("step", "踏み込み", CardAim.Self, CardValueKind.Close, CardKind.Move);
            CardFace shove = Card("shove", "突き飛ばし", CardAim.Single, CardValueKind.Push, CardKind.Attack);
            DepictionFrame withHand = Frame(50, 40);
            withHand.Hand = new List<CardFace> { step, shove };
            DepictionFrame afterStep = Frame(50, 40);
            afterStep.Hand = new List<CardFace> { shove };
            var script = new DepictionScript { Opening = Frame(50, 40) };
            script.Events.Add(new DepictionEvent { Order = 1, Kind = DepictionEventKind.TurnStart, After = withHand });
            script.Events.Add(new DepictionEvent
            {
                Order = 2, Kind = DepictionEventKind.PlayCard, Title = step.Name, CardId = step.Id, Aim = CardAim.Self, After = afterStep,
                Cues = new List<Cue> { new Cue { Kind = CueKind.StaminaChange, Target = UnitSide.Player, Amount = -1, StaminaAfter = 4, StaminaMax = 5 } },
            });
            script.Events.Add(new DepictionEvent
            {
                Order = 3, Kind = DepictionEventKind.PlayCard, Title = shove.Name, CardId = shove.Id, Aim = CardAim.Single, After = Frame(50, 37),
                Cues = new List<Cue>
                {
                    new Cue { Kind = CueKind.StaminaChange, Target = UnitSide.Player, Amount = -1, StaminaAfter = 3, StaminaMax = 5 },
                    new Cue { Kind = CueKind.RangeSwitch, Target = UnitSide.Player, Pushed = true, Unit = 0, UnitCellAfter = 7, PlayerCellAfter = 2, EnemyCellAfter = 7, GapAfter = 4, CellsMoved = 1 },
                    new Cue { Kind = CueKind.Hit, Target = UnitSide.Enemy, Unit = 0, Amount = 3, HpAfter = 37, Intensity = 2 },
                },
            });
            var rig = new Rig(new DepictionRunner(script));
            rig.Start();
            Assert.That(rig.RunUntil(() => rig.Player.State == BattlePlayerState.Idle, 10_000f), Is.True);
            rig.Input(() => rig.Player.HandCallbacks.OnRelease(step.Id, DropZone.AboveThrowLine, -1));
            Assert.That(rig.RunUntil(() => rig.Player.State == BattlePlayerState.Idle, 10_000f), Is.True);
            Assert.That(rig.Field.Bumps, Is.EqualTo(new[] { (UnitSide.Player, 1) }), "詰める with no move: one lean forward");
            rig.Input(() => rig.Player.HandCallbacks.OnRelease(shove.Id, DropZone.Receiver, -1));
            Assert.That(rig.RunUntil(() => rig.Finished > 0, 60_000f), Is.True);
            Assert.That(rig.Player.PlanErrors, Is.EqualTo(0), string.Join("\n", rig.Log.Errors));
            Assert.That(rig.Field.Bumps.Count, Is.EqualTo(1), "the shove moved someone: no lean");
            Assert.That(rig.Field.WallHits, Is.EqualTo(new[] { (UnitSide.Enemy, 1) }), "pushed right into the wall");
            var pushed = rig.Field.Floats.Single(f => f.Text == DepictionText.Pushed);
            Assert.That(pushed.Role, Is.EqualTo(FxRole.Ink2));
            Assert.That(pushed.Size, Is.EqualTo(36f));
            var hit = rig.Field.Floats.Single(f => f.Text == "−3");
            Assert.That(hit.O.Side, Is.EqualTo(BattlePlayer.WallSide));
            Assert.That(rig.Player.LogLines(), Is.EqualTo(new[] { "— ターン 1 —", "自分: 踏み込み", "自分: 突き飛ばし", "　敵 壁 −3" }));
            Assert.That(rig.Hand.Released, Is.EqualTo(new[] { "step", "shove" }));
            Assert.That(rig.Audio.Plays.Count(p => p.Name == "hitBlunt" || p.Name == "hitBody"), Is.EqualTo(0), "a blow on an enemy sounds through the released card");
        }
    }
}
