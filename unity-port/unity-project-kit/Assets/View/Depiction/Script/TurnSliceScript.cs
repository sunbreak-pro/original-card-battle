// The hand-written one-turn script (plan 2026-09-19-unity-battle-depiction §4).
// Numbers are the §18 draft scale (attack 6 / 13 / 21, guard 4 / 9 / 15, recovery 3).
// Every value below is a settled result written by hand; nothing here is a formula the
// View could re-run.
using System.Collections.Generic;

namespace Depiction
{
    public static class TurnSliceScript
    {
        public const string Kesagiri = "kesagiri";
        public const string Daijodan = "daijodan";
        public const string Ushirotobi = "ushirotobi";
        public const string TetsuNoUke = "tetsu-no-uke";
        public const string Kansatsu = "kansatsu";

        public static DepictionScript Build()
        {
            var script = new DepictionScript();

            // Turn 2 opens: HP 50/50, stamina 6/10, near. Enemy 長柄の歪み兵 HP 60/60, no range side.
            DepictionFrame opening = Snapshot(playerHp: 50, playerGuard: 0, stamina: 6, range: RangeSide.Near,
                enemyHp: 60, hand: new List<CardFace>(), omen: new OmenFrame { Visible = false });
            script.Opening = opening;

            // 1. Turn start: Guard 0, stamina 6 -> 9, draw five, show the omen.
            OmenFrame attackNear = new OmenFrame { Visible = true, KindLabel = "攻撃", SideGlyph = "近", ValueText = "13" };
            var turnStart = new DepictionEvent
            {
                Order = 1, Kind = DepictionEventKind.TurnStart, Title = "ターン開始",
                After = Snapshot(50, 0, 9, RangeSide.Near, 60, FullHand(), attackNear),
            };
            turnStart.Cues.Add(new Cue { Kind = CueKind.GuardReset, Target = UnitSide.Player, GuardAfter = 0 });
            turnStart.Cues.Add(new Cue { Kind = CueKind.StaminaChange, Target = UnitSide.Player, Amount = 3, StaminaAfter = 9, StaminaMax = 10 });
            turnStart.Cues.Add(new Cue { Kind = CueKind.DrawHand, Target = UnitSide.Player, Amount = 5 });
            turnStart.Cues.Add(new Cue { Kind = CueKind.OmenShow, Target = UnitSide.Enemy });
            script.Events.Add(turnStart);

            // 2. 袈裟斬り (cost 1): trait 初手 +3 fires, 9 damage, enemy 60 -> 51, stamina 9 -> 8.
            var kesagiri = new DepictionEvent
            {
                Order = 2, Kind = DepictionEventKind.PlayCard, Title = "袈裟斬り",
                CardId = Kesagiri, Aim = CardAim.Single, PreviewText = "9",
                After = Snapshot(50, 0, 8, RangeSide.Near, 51, HandWithout(Kesagiri), attackNear),
            };
            kesagiri.Cues.Add(new Cue { Kind = CueKind.StaminaChange, Target = UnitSide.Player, Amount = -1, StaminaAfter = 8, StaminaMax = 10 });
            kesagiri.Cues.Add(new Cue { Kind = CueKind.TraitFire, Target = UnitSide.Player, Text = "初手 +3" });
            kesagiri.Cues.Add(new Cue { Kind = CueKind.Slash, Source = UnitSide.Player, Target = UnitSide.Enemy, Amount = 9, Intensity = 1, HpAfter = 51 });
            script.Events.Add(kesagiri);

            // 3. 大上段 (cost 2): 13 damage, enemy 51 -> 38, stamina 8 -> 6.
            var daijodan = new DepictionEvent
            {
                Order = 3, Kind = DepictionEventKind.PlayCard, Title = "大上段",
                CardId = Daijodan, Aim = CardAim.Single, PreviewText = "13",
                After = Snapshot(50, 0, 6, RangeSide.Near, 38, HandWithout(Kesagiri, Daijodan), attackNear),
            };
            daijodan.Cues.Add(new Cue { Kind = CueKind.StaminaChange, Target = UnitSide.Player, Amount = -2, StaminaAfter = 6, StaminaMax = 10 });
            daijodan.Cues.Add(new Cue { Kind = CueKind.Slash, Source = UnitSide.Player, Target = UnitSide.Enemy, Amount = 13, Intensity = 2, HpAfter = 38 });
            script.Events.Add(daijodan);

            // 4. 後ろ跳び (cost 1): switch to far, Guard 4, trait 予兆(攻撃) Guard +3 fires -> Guard 7, stamina 6 -> 5.
            var ushirotobi = new DepictionEvent
            {
                Order = 4, Kind = DepictionEventKind.PlayCard, Title = "後ろ跳び",
                CardId = Ushirotobi, Aim = CardAim.Self, PreviewText = "7",
                After = Snapshot(50, 7, 5, RangeSide.Far, 38, HandWithout(Kesagiri, Daijodan, Ushirotobi), attackNear),
            };
            ushirotobi.Cues.Add(new Cue { Kind = CueKind.StaminaChange, Target = UnitSide.Player, Amount = -1, StaminaAfter = 5, StaminaMax = 10 });
            ushirotobi.Cues.Add(new Cue { Kind = CueKind.RangeSwitch, Target = UnitSide.Player, RangeAfter = RangeSide.Far, RangeGlyphAfter = GlyphOf(RangeSide.Far) });
            ushirotobi.Cues.Add(new Cue { Kind = CueKind.GuardGain, Target = UnitSide.Player, Amount = 4, Intensity = 1, GuardAfter = 4 });
            ushirotobi.Cues.Add(new Cue { Kind = CueKind.TraitFire, Target = UnitSide.Player, Text = "予兆 +3" });
            ushirotobi.Cues.Add(new Cue { Kind = CueKind.GuardGain, Target = UnitSide.Player, Amount = 3, Intensity = 1, GuardAfter = 7 });
            script.Events.Add(ushirotobi);

            // 5. Turn end: stance (5 left >= 3) Guard +3 -> 10, discard the remaining two.
            var turnEnd = new DepictionEvent
            {
                Order = 5, Kind = DepictionEventKind.TurnEnd, Title = "ターン終了",
                After = Snapshot(50, 10, 5, RangeSide.Far, 38, new List<CardFace>(), attackNear, stanceHint: ""),
            };
            turnEnd.Cues.Add(new Cue { Kind = CueKind.StanceCue, Target = UnitSide.Player, Amount = 3, Text = "+3", GuardAfter = 10 });
            turnEnd.Cues.Add(new Cue { Kind = CueKind.DiscardHand, Target = UnitSide.Player, Amount = 2 });
            script.Events.Add(turnEnd);

            // 6. Enemy attacks as foretold: 13. The near +5 misses. 13 - 10 = 3, HP 50 -> 47.
            var enemyAction = new DepictionEvent
            {
                Order = 6, Kind = DepictionEventKind.EnemyAction, Title = "敵の攻撃",
                After = Snapshot(47, 0, 5, RangeSide.Far, 38, new List<CardFace>(), new OmenFrame { Visible = false }, stanceHint: ""),
            };
            enemyAction.Cues.Add(new Cue { Kind = CueKind.EnemyWindup, Target = UnitSide.Enemy });
            enemyAction.Cues.Add(new Cue { Kind = CueKind.SideBonusMiss, Target = UnitSide.Enemy, Amount = 5, Text = "+5" });
            enemyAction.Cues.Add(new Cue { Kind = CueKind.Slash, Source = UnitSide.Enemy, Target = UnitSide.Player, Amount = 13, Intensity = 2 });
            enemyAction.Cues.Add(new Cue { Kind = CueKind.GuardBlock, Target = UnitSide.Player, Amount = 10, GuardAfter = 0 });
            enemyAction.Cues.Add(new Cue { Kind = CueKind.Hit, Target = UnitSide.Player, Amount = 3, Intensity = 1, HpAfter = 47 });
            script.Events.Add(enemyAction);

            // 7. Next omen: 防御.
            var nextOmen = new DepictionEvent
            {
                Order = 7, Kind = DepictionEventKind.NextOmen, Title = "次の予兆",
                After = Snapshot(47, 0, 5, RangeSide.Far, 38, new List<CardFace>(),
                    new OmenFrame { Visible = true, KindLabel = "防御" }, stanceHint: ""),
            };
            nextOmen.Cues.Add(new Cue { Kind = CueKind.OmenShow, Target = UnitSide.Enemy });
            script.Events.Add(nextOmen);

            return script;
        }

        private static DepictionFrame Snapshot(int playerHp, int playerGuard, int stamina, RangeSide range, int enemyHp,
            List<CardFace> hand, OmenFrame omen, string stanceHint = "+3")
        {
            return new DepictionFrame
            {
                Corner = new CornerFrame { Turn = 2, Floor = 1, ChainIndex = 1, ChainTotal = 3, MiasmaPercent = 0 },
                Player = new UnitFrame
                {
                    Hp = playerHp, HpMax = 50, Guard = playerGuard,
                    ShowStamina = true, Stamina = stamina, StaminaMax = 10,
                    HasRange = true, Range = range, RangeGlyph = GlyphOf(range),
                },
                Enemy = new UnitFrame { Hp = enemyHp, HpMax = 60, Guard = 0, ShowStamina = false, HasRange = false },
                Omen = omen,
                Hand = hand,
                StanceHint = stanceHint,
            };
        }

        /// <summary>The script decides how a range side is printed; the View prints the string as given.</summary>
        private static string GlyphOf(RangeSide range)
        {
            return range == RangeSide.Near ? "近" : "遠";
        }

        private static List<CardFace> FullHand()
        {
            return new List<CardFace>
            {
                new CardFace { Id = Kesagiri, Name = "袈裟斬り", Cost = 1, Kind = CardKind.Attack, Aim = CardAim.Single, ValueText = "6", TraitText = "初手 +3", TraitLit = true,
                    TypeLabel = "攻撃・敵単体", Description = "敵に 6 ダメージ。ターン最初なら +3。" },
                new CardFace { Id = Daijodan, Name = "大上段", Cost = 2, Kind = CardKind.Attack, Aim = CardAim.Single, ValueText = "13",
                    TypeLabel = "攻撃・敵単体", Description = "敵に 13 ダメージ。振りかぶる一撃。" },
                new CardFace { Id = Ushirotobi, Name = "後ろ跳び", Cost = 1, Kind = CardKind.Move, Aim = CardAim.Self, Affects = UnitSide.Player, ValueText = "4", TraitText = "予兆 +3", TraitLit = true,
                    TypeLabel = "ムーブ・自分", Description = "遠間へ下がる。Guard 4 を得る。予兆が攻撃なら +3。" },
                new CardFace { Id = TetsuNoUke, Name = "鉄の受け", Cost = 2, Kind = CardKind.Guard, Aim = CardAim.Self, Affects = UnitSide.Player, ValueText = "9",
                    TypeLabel = "防御・自分", Description = "Guard 9 を得る。重い一撃に備える。" },
                // The slice never plays 観察; its text follows swordsman_cards_v4 (draw 1 at the first tier).
                new CardFace { Id = Kansatsu, Name = "観察", Cost = 1, Kind = CardKind.Skill, Aim = CardAim.Single,
                    TypeLabel = "技・敵単体", Description = "カードを 1 枚引く。相手をよく見る。" },
            };
        }

        private static List<CardFace> HandWithout(params string[] played)
        {
            List<CardFace> hand = FullHand();
            hand.RemoveAll(c => System.Array.IndexOf(played, c.Id) >= 0);
            return hand;
        }
    }
}
