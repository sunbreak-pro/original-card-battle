# MEMORY (chat-cards)

> 進行中 / 直近の完了 / 予定の正本。**手動編集せず task-tracker スキル経由で更新**。完了タスクの詳細は `history/chat-cards.md`、要約は `README.md` の Development History。
> レーン `cards`（worktree: `C:\Users\user\orca\workspaces\original-card-battle\cards`）。担当は カード設計・デッキ・習熟・敵ロースター（既定ラベル `area:cards` `area:enemy`）。

## 進行中

なし。

## 直近の完了

### ✅ ラスボス・第 2 ボス・エクストラボス枠（Issue #38、完了日: 2026-09-21）

**対象**: `.claude/docs/enemy_document/enemy_roster_v4.md`（§5 §6 §7 新設、旧 §5〜§7 が §8〜§10 へ繰り下がり）/ `.claude/docs/card_document/swordsman_cards_v4.md`（#44〜49）/ `.claude/docs/vision/concept-v3.md`（§15）/ `.claude/docs/battle_document/battle_ui_ux_v2.md`（参照の付け替えのみ）
**PR**: [#64](https://github.com/sunbreak-pro/original-card-battle/pull/64)（open。merge はこうだいさんの手番）
**ブランチ**: `docs/cards-38`（2 commit。1 = メインチェックアウトのドラフト `docs/boss-roster-and-extra-boss` をそのまま、2 = 整合の修正）

- 深淵の釣り人（遠、HP 180、第 2 ボス）と歪みの根（中、HP 200、3 段階）を §4 瘴気の司祭と同じ章立てで記述
- エクストラボスは「生が閉じる直前の根の裂け目」に置き、敗北を死亡扱いにしない。報酬は既存の仕組み（遺産の候補 / 生存者ボーナスの期間）だけを伸ばす
- 3 段階は段階ごとに違う問いを出す形に決めた（同じ問いを重くする案は不採用）

## 予定

- 🔜 **エクストラボス第 1 号の竜（Issue #39）** — #38 で強さの目安（HP 240〜260、出力 16〜24、狙い 22〜28 ターン）と枠を置いたので着手可能
- 🔜 **100% 用の残り 8 体**（`enemy_roster_v4.md` §8）— 1 行ずつしか決まっていない
