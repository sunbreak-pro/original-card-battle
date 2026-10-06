// The judgement for every effect (#78): what it carries, and whether the screen needs it.
// Pure C#, next to EffectCatalog, so the table the report prints is built from the code and an effect
// added later cannot ship without a row here (EffectAuditTests fails until it has one).
//
// The judgements below were made from the script data alone (Cue fields, the settled frames, the
// lengths in EffectCatalog and the waits in EffectPlan). Nothing was captured on screen. Where the
// call rests on how an effect looks or feels, Basis is HandsOn and the effect goes to #79.
using System;
using System.Collections.Generic;

namespace Depiction
{
    /// <summary>What the player would stop being able to read if the effect were switched off.</summary>
    [Flags]
    public enum Carries
    {
        None = 0,
        /// <summary>How much damage (or Guard absorbed) a blow did. The settled frame shows only the new HP.</summary>
        Amount = 1 << 0,
        /// <summary>Who hit whom, and with what shape of blow.</summary>
        WhoHitWhom = 1 << 1,
        /// <summary>A status appeared, grew, ticked down or vanished.</summary>
        Status = 1 << 2,
        /// <summary>The player's place on the row changed (or was pushed).</summary>
        Position = 1 << 3,
        /// <summary>What the enemy is about to do or is doing (omen, its move).</summary>
        Intent = 1 << 4,
        /// <summary>Which cards are in hand, which was played, whether one can be paid for or was refused.</summary>
        Hand = 1 << 5,
        /// <summary>Whose turn it is, or how the fight ended.</summary>
        TurnOrResult = 1 << 6,
        /// <summary>Feedback that the pointer or a drag was understood.</summary>
        Input = 1 << 7,
    }

    public enum Verdict
    {
        /// <summary>要る: it carries information the settled frame does not.</summary>
        Needed,
        /// <summary>短くする: the information is needed, the time is not.</summary>
        Shorten,
        /// <summary>要らない: nothing is lost without it (or another effect says the same).</summary>
        Drop,
    }

    public enum Basis
    {
        /// <summary>Decided from the script data: where the value lives and what the frame keeps.</summary>
        Script,
        /// <summary>Rests on how it looks or feels. Decided provisionally here; #79's hands-on check settles it.</summary>
        HandsOn,
    }

    public sealed class EffectAuditEntry
    {
        public EffectAuditEntry(EffectId id, string family, Carries carries, Verdict verdict, Basis basis, string reason)
        {
            Id = id;
            Family = family;
            Carries = carries;
            Verdict = verdict;
            Basis = basis;
            Reason = reason;
        }

        public EffectId Id { get; }
        public string Family { get; }
        public Carries Carries { get; }
        public Verdict Verdict { get; }
        public Basis Basis { get; }
        public string Reason { get; }
    }

    /// <summary>
    /// Timing the flow adds between events, outside any effect. DepictionPlayer waits exactly these,
    /// and the measurement counts the time input is closed with them. Like EffectCatalog, the lengths
    /// are written at 1.25 times: DepictionPlayer hands the constants to UiTween.Wait and the battle
    /// speed (#348) is applied by UiTween.Speed. The *At forms are for the measurement.
    /// </summary>
    public static class EffectFlow
    {
        /// <summary>After an event the player asked for (a played card, the end-turn plate).</summary>
        public const float AfterPlayerEventMs = 250f;

        /// <summary>After each event the script plays by itself (the enemy's turn, the next turn start).</summary>
        public const float AfterAutoEventMs = 350f;

        /// <summary>One frame at 60 fps: the most a tween can run past its length (UiTween ends on the first frame after it). A frame stays a frame at every speed.</summary>
        public const float FrameMs = 1000f / 60f;

        /// <summary><see cref="AfterPlayerEventMs"/> as the screen really waits it at this battle speed.</summary>
        public static float AfterPlayerEventMsAt(BattleSpeedStep speed) => BattleSpeed.WallMs(AfterPlayerEventMs, speed);

        /// <summary><see cref="AfterAutoEventMs"/> as the screen really waits it at this battle speed.</summary>
        public static float AfterAutoEventMsAt(BattleSpeedStep speed) => BattleSpeed.WallMs(AfterAutoEventMs, speed);
    }

    public static class EffectAudit
    {
        private const string Cards = "札の動き";
        private const string Blow = "攻撃と防御";
        private const string Beats = "そのほかの拍";
        private const string StatusTurn = "状態とターン";

        private static readonly Dictionary<EffectId, EffectAuditEntry> Entries = Build();

        public static IReadOnlyCollection<EffectAuditEntry> All => Entries.Values;

        public static bool Has(EffectId id) => Entries.ContainsKey(id);

        public static EffectAuditEntry Of(EffectId id)
        {
            if (!Entries.TryGetValue(id, out EffectAuditEntry entry))
            {
                throw new KeyNotFoundException("No judgement is written for effect " + id + ".");
            }
            return entry;
        }

        private static Dictionary<EffectId, EffectAuditEntry> Build()
        {
            const Verdict N = Verdict.Needed, S = Verdict.Shorten, D = Verdict.Drop;
            const Basis Sc = Basis.Script, H = Basis.HandsOn;
            var list = new[]
            {
                // ---- Card motion ----
                new EffectAuditEntry(EffectId.CardDraw, Cards, Carries.Hand, S, H,
                    "新しい手札はターン開始の枠が先に持っているので、飛ぶ動きが運ぶのは「どれが来たか」だけです。5 枚で 500 ms 入力を止めるため、間隔を詰めます。"),
                new EffectAuditEntry(EffectId.HandFan, Cards, Carries.Hand, N, Sc,
                    "並行して走り、待ち時間はありません。切ると残りの札が一瞬で飛び、どの札が抜けたか追えなくなります。"),
                new EffectAuditEntry(EffectId.CardHover, Cards, Carries.Input, N, Sc,
                    "どの札を掴もうとしているかの唯一の返事です。並行で待ち時間はありません。"),
                new EffectAuditEntry(EffectId.CardGrab, Cards, Carries.Input, N, Sc,
                    "掴めたことの返事です。並行で待ち時間はありません。"),
                new EffectAuditEntry(EffectId.ReceiverShow, Cards, Carries.Input | Carries.WhoHitWhom, N, Sc,
                    "単体の札を離す場所と、狙う相手を示します。切ると離す場所が分かりません。"),
                new EffectAuditEntry(EffectId.ThrowLineShow, Cards, Carries.Input, N, Sc,
                    "自分に使う札を離す線を示します。切ると離す場所が分かりません。"),
                new EffectAuditEntry(EffectId.ReceiverSnap, Cards, Carries.Input, D, H,
                    "皿の上にいることは、皿が出ていることで分かります。吸い付きは手触りの演出で、情報を足しません。"),
                new EffectAuditEntry(EffectId.CardRelease, Cards, Carries.Hand, S, H,
                    "札が受け入れられたことを示しますが、160 ms は毎回の出札で入力を止めます。どこまで詰められるかは触って確かめます。"),
                new EffectAuditEntry(EffectId.CardToDiscard, Cards, Carries.Hand, D, H,
                    "捨て札の枚数は角の表示が持っています。並行で待ち時間はなく、切っても読み取れなくなる情報がありません。"),
                new EffectAuditEntry(EffectId.CardReturn, Cards, Carries.Hand | Carries.Input, N, Sc,
                    "出せなかった札が手札に戻ることを示します。カタログでは待たない扱いですが、画面は入力を 200 ms 止めます。"),
                new EffectAuditEntry(EffectId.RefusalShake, Cards, Carries.Hand, S, H,
                    "断られた理由は案内の文が言っています。揺れは「断られた」の合図で、入力をさらに 160 ms 止めるため短くします。"),
                new EffectAuditEntry(EffectId.HandDiscard, Cards, Carries.Hand, S, H,
                    "ターン終了で手札が全部捨て札へ行くだけで、捨てた中身はもう決まっています。5 枚で 480 ms かかるため、間隔を詰めます。"),
                new EffectAuditEntry(EffectId.UnpayableDim, Cards, Carries.Hand, N, Sc,
                    "スタミナが足りない札を、掴む前に見分ける唯一の印です。"),

                // ---- Attack and defence ----
                new EffectAuditEntry(EffectId.AttackLunge, Blow, Carries.WhoHitWhom, N, Sc,
                    "誰が殴りに行くかを示します。すでに 100 ms まで詰めてあります。"),
                new EffectAuditEntry(EffectId.EnemyLunge, Blow, Carries.WhoHitWhom, N, H,
                    "敵が殴りに来たことを、絵 1 枚のままで示します。構えの動きだけでは攻撃と守りの見分けが弱いため足しました。100 ms で足りるかは #79 で確かめます。"),
                new EffectAuditEntry(EffectId.StrikeShape, Blow, Carries.WhoHitWhom, N, Sc,
                    "斬 / 突 / 打 などの系統は、この形でしか見えません。"),
                new EffectAuditEntry(EffectId.HitStop, Blow, Carries.None, D, H,
                    "30 ms の停止が運ぶ情報はありません。手応えの好みなので、確定は #79 です。"),
                new EffectAuditEntry(EffectId.HitStopStrong, Blow, Carries.None, D, H,
                    "強い一撃であることは数字の大きさと画面の揺れも言っています。手応えの好みなので、確定は #79 です。"),
                new EffectAuditEntry(EffectId.HitFlash, Blow, Carries.WhoHitWhom, N, Sc,
                    "誰に当たったかを、数字が出る前に示します。並行で待ち時間はありません。"),
                new EffectAuditEntry(EffectId.TargetRecoil, Blow, Carries.None, D, H,
                    "誰に当たったかは点滅と数字が言っています。のけぞりは重さの演出なので、確定は #79 です。"),
                new EffectAuditEntry(EffectId.DamageNumber, Blow, Carries.Amount, N, Sc,
                    "ダメージの量が書かれる唯一の場所です。枠は新しい HP だけを持ちます。"),
                new EffectAuditEntry(EffectId.HpDrain, Blow, Carries.Amount, N, Sc,
                    "HP の減りを目で追える唯一の動きです。300 ms を待ちますが、切ると HP が一瞬で書き換わります。"),
                new EffectAuditEntry(EffectId.HpTrail, Blow, Carries.Amount, D, H,
                    "減った量は数字と HP バーの動きが言っています。灰色の残像は同じことの繰り返しで、待ち時間はありません。確定は #79 です。"),
                new EffectAuditEntry(EffectId.ScreenShake, Blow, Carries.None, D, H,
                    "強い一撃の重さの演出で、情報を足しません。確定は #79 です。"),
                new EffectAuditEntry(EffectId.AttackReturn, Blow, Carries.None, D, H,
                    "元の位置に戻るだけで、読み取れる情報がありません。攻撃のたびに 120 ms 入力を止めます。切ると人物が瞬間移動して見えるため、確定は #79 です。"),
                new EffectAuditEntry(EffectId.EnemyMotion, Blow, Carries.WhoHitWhom | Carries.Intent, N, Sc,
                    "敵が何をするかを、系統ごとの形で示します。切ると敵の動きが読めません。"),
                new EffectAuditEntry(EffectId.OmenSpend, Blow, Carries.Intent, N, Sc,
                    "予兆と実際の一撃を結びつけます。切ると、どの予兆が当たったのか分かりません。"),
                new EffectAuditEntry(EffectId.EnemyReturn, Blow, Carries.None, D, H,
                    "元の位置に戻るだけです。敵の行動のたびに 140 ms 入力を止めます。切ると敵が瞬間移動して見えるため、確定は #79 です。"),
                new EffectAuditEntry(EffectId.GuardBlock, Blow, Carries.Amount | Carries.WhoHitWhom, S, Sc,
                    "Guard が受けた量を示すので必要ですが、320 ms は 1 つの行動で最も長い拍です。#78 の 1990 ms の出来事を作っています。"),
                new EffectAuditEntry(EffectId.GuardNumber, Blow, Carries.Amount, N, Sc,
                    "Guard が吸った量が書かれる場所です。並行で待ち時間はありません。"),
                new EffectAuditEntry(EffectId.GuardBreak, Blow, Carries.Status, N, Sc,
                    "Guard が 0 になったことを示します。バッジの数字だけでは、割れた瞬間が分かりません。"),
                new EffectAuditEntry(EffectId.GuardGain, Blow, Carries.Amount, S, Sc,
                    "得た Guard の量を示しますが、260 ms 待ちます。40 戦の計測で、待ちの合計が最も大きい効果です。数字は同じ拍の中で出せるので、待ちを詰めます。"),

                // ---- The other beats ----
                new EffectAuditEntry(EffectId.OmenShow, Beats, Carries.Intent, N, Sc,
                    "次の敵の行動を知らせる唯一の拍です。"),
                new EffectAuditEntry(EffectId.TraitFire, Beats, Carries.Status, N, Sc,
                    "特性の条件が成り立ったことを示します。切ると、なぜ効果が乗ったのか分かりません。"),
                new EffectAuditEntry(EffectId.StaminaChange, Beats, Carries.Hand, N, Sc,
                    "スタミナ表示が一瞬弾みます。View は待たずに並行で走らせますが、カタログは待つ拍として登録しています。このずれは報告に書きます。"),
                new EffectAuditEntry(EffectId.StanceCue, Beats, Carries.Amount, S, Sc,
                    "ターン終了時の構えによる Guard を示します。460 ms は毎ターンの長い拍で、盾と +3 は同時に出せます。"),
                new EffectAuditEntry(EffectId.RangeSwitch, Beats, Carries.Position, S, Sc,
                    "位置が変わることを示す唯一の拍なので、情報は要ります。ただし 470 ms は移動 320 と一字札の反転 150 を続けており、重ねれば詰められます。"),
                new EffectAuditEntry(EffectId.SideBonusMiss, Beats, Carries.Intent, N, Sc,
                    "敵の予兆のボーナスが外れたことを示します。切ると、外れたのに何も起きなかったように見えます。"),

                // ---- Status and turn ----
                new EffectAuditEntry(EffectId.StatusApply, StatusTurn, Carries.Status, N, Sc,
                    "状態が付いたことを示します。枠は付いた後のチップだけを持ちます。"),
                new EffectAuditEntry(EffectId.StatusStack, StatusTurn, Carries.Status, N, Sc,
                    "スタックが増えたことを示します。"),
                new EffectAuditEntry(EffectId.StatusTick, StatusTurn, Carries.Status, N, Sc,
                    "スタックが減ったことを示します。120 ms まで詰めてあります。"),
                new EffectAuditEntry(EffectId.StatusVanish, StatusTurn, Carries.Status, N, Sc,
                    "状態が消えたことを示します。切るとチップが黙って消えます。"),
                new EffectAuditEntry(EffectId.OmenBlink, StatusTurn, Carries.Intent, D, H,
                    "予兆はターン開始の時点ですでに表示されています。明滅は目を向けさせる演出です。確定は #79 です。"),
                new EffectAuditEntry(EffectId.OmenExecute, StatusTurn, Carries.Intent, D, H,
                    "予兆が実行されたことは、敵の動きと予兆の消え方が言っています。確定は #79 です。"),
                new EffectAuditEntry(EffectId.PushMark, StatusTurn, Carries.Position, N, Sc,
                    "相手に動かされたことを示す唯一の印です。自分の移動と見分けがつかなくなります。"),
                new EffectAuditEntry(EffectId.TurnBanner, StatusTurn, Carries.TurnOrResult, S, H,
                    "自分の番が来たことは、手札が配られることでも分かります。300 ms 毎ターン入力を止めるので、詰めます。"),
                new EffectAuditEntry(EffectId.EnemyTurnBanner, StatusTurn, Carries.TurnOrResult, S, H,
                    "番が渡ったことは、終了ボタンの後に敵が動くことで分かります。400 ms 毎ターン入力を止めるので、詰めます。"),
                new EffectAuditEntry(EffectId.StaminaRecover, StatusTurn, Carries.Hand, S, Sc,
                    "回復した量は最終の値が持っています。1 つずつ点ける方式は 5 個で 400 ms かかり、毎ターン平均で約 200 ms を使うため、間隔を詰めます。"),
                new EffectAuditEntry(EffectId.ResultCard, StatusTurn, Carries.TurnOrResult, N, Sc,
                    "勝ち負けを知らせる唯一の札です。戦闘の最後に 1 度だけ出て、並行で待ち時間はありません。"),
                new EffectAuditEntry(EffectId.Defeat, StatusTurn, Carries.TurnOrResult, N, Sc,
                    "敵が倒れてマスが空いたことを示します。切ると人型が一瞬で消え、何が起きたか読めません。勝利の札はこの後に出ます（§2.2 の 6）。"),
            };
            var map = new Dictionary<EffectId, EffectAuditEntry>();
            foreach (EffectAuditEntry entry in list) map.Add(entry.Id, entry);
            return map;
        }
    }
}
