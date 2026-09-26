# SOURCES — 正本の台帳

> **役割**: 「どの主題は、どのファイルが正本で、どのレーンが書くか」を 1 か所に集めた台帳です。図書館の蔵書目録にあたります。本の中身は持たず、棚の場所と貸出係だけを持ちます。
> **この台帳が正本であるもの**: 正本の場所、持ち主のレーン、食い違ったときの手順。他の文書とスキルは同じ表を持たず、ここを指します。
> **引き方**: `npm run sources -- --lane <slug>` が、そのレーンの書くものと読むものを出します。`npm run sources -- --check` が台帳と実ファイルのずれを検出します。
> **更新**: 正本を足す・版を上げる・退役させるコミットで、同じコミットに本書の行を含めます。書き手は chat-main です。レーンは自分の PR で自分の行だけを直します。

## 1. 読む前に main を取り込む

worktree ごとに設計書のコピーを持つので、main を取り込んでいないレーンは古い正本を読みます。2026-09-21 の実測では、レーンが最大 37 コミット、メインのチェックアウトも 16 コミット遅れていました。着手前に `git fetch origin && git merge origin/main --no-edit`（メインは `git pull --ff-only`）を済ませてから本書を引きます。

## 2. 現行の正本

持ち主の `main` は chat-main、`人` はこうだいさんです。パスはリポジトリのルートからの相対です。

| 主題 | 正本 | 持ち主 | 状態 | 読むレーン |
| --- | --- | --- | --- | --- |
| 構想（生と継承） | `.claude/docs/vision/concept-v3.md` | main | APPROVED | all |
| 設計原則 | `.claude/docs/vision/core.md` | main | APPROVED | all |
| 世界設定・用語・固有名詞 | `.claude/docs/vision/world-v1.md` | main | 正典 v4 | cards, design, dungeon |
| ゲーム全体像 | `.claude/docs/Overall_document/game_design_master.md` | main | V4.0 | audit |
| 要件（何をどの順で） | `.claude/docs/requirements/tier1-core.md` | main | v6 | all |
| 要件（支援） | `.claude/docs/requirements/tier2-support.md` | main | v3 | all |
| 要件（実験・凍結） | `.claude/docs/requirements/tier3-experimental.md` | main | v2 | audit |
| 戦闘のルールと数値 | `.claude/docs/battle_document/battle_core_v4.md` | battle | v4.3（間合い N、#159）+ §19 の習熟（S5〜S8）未反映 | cards, design |
| 戦闘の画面と操作 | `.claude/docs/battle_document/battle_ui_ux_v2.md` | battle | v2.2（間合い N、#163。本文が正。§10 / §11 / §13 は記録） | design |
| カード（剣士 80 種） | `.claude/docs/card_document/swordsman_cards_v4.md` | cards | v4.2 | battle, design |
| 敵の数値とロースター | `.claude/docs/enemy_document/enemy_roster_v4.md` | cards | v4.4（試験台で測った HP、#204） | battle, design |
| 探索（刻限・瘴気・ノード） | `.claude/docs/danjeon_document/dungeon_exploration_v4.md` | dungeon | PROPOSED | design |
| concept-v3 の未確定の棚卸し | `.claude/docs/danjeon_document/concept_v3_open_items.md` | dungeon | PROPOSED（決定ではない） | design |
| 七層の瘴気と刻限（層ごとの数値） | `.claude/docs/danjeon_document/seven_layers_v4.md` | dungeon | PROPOSED | battle, cards |
| 探索の瘴気と戦闘の数値の突き合わせ | `.claude/docs/danjeon_document/miasma_and_battle_v4.md` | dungeon | PROPOSED | battle |
| ツールと消耗品（出立の枠の中身） | `.claude/docs/danjeon_document/tools_and_consumables_v4.md` | dungeon | PROPOSED | battle |
| 継承の間の画面と導線 | `.claude/docs/camp_document/CAMP_FACILITIES_DESIGN.md` | design | V5.0 | dungeon |
| カードの絵の方針 | `.claude/docs/art_document/card-art-policy.md` | design | DRAFT v1 | cards |
| 絵柄の規約（原本の大きさ・指示文・加筆） | `.claude/docs/art_document/style-guide.md` | design | DRAFT v1（指示文は未実測） | — |
| 素材の取り込み規約（置き場・命名・Unity の設定） | `.claude/docs/art_document/asset-intake.md` | design | DRAFT v1 | battle |
| アートの道具と前提 | `.claude/skills/visual-production-pipeline/references/tools-and-prerequisites.md` | design | 現行 | — |
| 戦闘コアの実装 | `unity-port/BattleCore/` | battle | C# が正 | cards |
| 戦闘描写の台本と View | `unity-port/unity-project-kit/Assets/View/Depiction/` | battle | C# はこのリポが正 | design |
| 実装規約・凍結範囲 | `.claude/CLAUDE.md` | main | 現行 | all |
| レーンの定義と worktree の手順 | `.claude/skills/worktree-policy/SKILL.md` | main | 現行 | all |
| 課題の状態・担当・優先順位 | GitHub Issues（`npm run issues:next`） | 人 + main | — | all |
| ラベルの一覧 | GitHub のラベル（`gh label list -R sunbreak-pro/original-card-battle`） | main | — | all |
| タスクの進捗（チャットごと） | `.claude/memory/` | 各チャット | task-tracker 経由 | — |

**状態の欄に但し書きがある行は、そのまま読むと間違えます。** `battle_core_v4.md` は §19（2026-09-21 の決定）が本文に入っていません。同じ文書は 2026-09-22 の v4.3（#159）で近間 / 遠間を間合い N（マスの距離）へ改めました。`swordsman_cards_v4.md`・`enemy_roster_v4.md`・`battle_ui_ux_v2.md` は近間 / 遠間のままで、追従は #160 と #163 です。間合いの規則が食い違ったら `battle_core_v4.md` が勝ちます。`battle_ui_ux_v2.md` は 2026-09-21 の v2.1 改訂（#46）で本文が正本に戻りました。

**`battle_ui_ux_v2.md` と `View/Depiction/` の持ち主は仮です。** レーンの定義では UI / UX 設計は design ですが、縦切り（#67）の間は #46 / #56 / #57 が `lane:battle` なので battle に置いています。縦切りの振り返り（#80）で見直します。

## 3. 旧版と記録（現行仕様として読まない）

| ファイル | 状態 | 代わりに読むもの |
| --- | --- | --- |
| `.claude/docs/vision/concept-v2.md` | SUPERSEDED | `concept-v3.md` |
| `.claude/docs/vision/2026-06-11-gap-analysis.md` | SUPERSEDED | `requirements/tier1-core.md` |
| `.claude/docs/vision/2026-06-11-realism-concept-kickoff.md` | SUPERSEDED | `concept-v3.md` |
| `.claude/docs/battle_document/battle_core_v3.md` | SUPERSEDED | `battle_core_v4.md` |
| `.claude/docs/battle_document/battle_logic.md` | PARTIALLY SUPERSEDED | `battle_core_v4.md` |
| `.claude/docs/battle_document/battle_ui_ux_v1.md` | 旧正本（View v1.1 の実装記録） | `battle_ui_ux_v2.md` |
| `.claude/docs/battle_document/buff_debuff_system.md` | WEB VERSION RECORD | `battle_core_v4.md` §5 |
| `.claude/docs/battle_document/element_system_spec.md` | WEB VERSION RECORD | `battle_core_v4.md` |
| `.claude/docs/card_document/SWORDSMAN_CARDS_40.md` | Web 版の記録（バナーなし） | `swordsman_cards_v4.md` |
| `.claude/docs/card_document/MAGE_CARDS_40.md` | Web 版の記録（バナーなし） | なし（魔術師は未設計） |
| `.claude/docs/card_document/NEW_CHARACTER_SYSTEM_DESIGN.md` | Web 版の記録（バナーなし） | `concept-v3.md` |
| `.claude/docs/enemy_document/boss_system_redesign.md` | WEB VERSION RECORD | `enemy_roster_v4.md` |
| `.claude/docs/enemy_document/depth1_enemy_database.md` | WEB VERSION RECORD | `enemy_roster_v4.md` |
| `.claude/docs/enemy_document/depth2_enemy_database.md` | WEB VERSION RECORD | `enemy_roster_v4.md` |
| `.claude/docs/enemy_document/depth3_enemy_database.md` | WEB VERSION RECORD | `enemy_roster_v4.md` |
| `.claude/docs/enemy_document/depth4_enemy_database.md` | WEB VERSION RECORD | `enemy_roster_v4.md` |
| `.claude/docs/enemy_document/depth5_boss_database.md` | WEB VERSION RECORD | `enemy_roster_v4.md` |
| `.claude/docs/danjeon_document/dungeon_exploration_ui_design_v3.0.md` | SUPERSEDED | `dungeon_exploration_v4.md` |
| `.claude/docs/danjeon_document/return_system_design.md` | SUPERSEDED | `dungeon_exploration_v4.md` |
| `.claude/docs/journal_document/journal_system_implementation_plan.md` | PARTIALLY SUPERSEDED | `concept-v3.md` §5 |
| `.claude/docs/Overall_document/DESIGN_CHANGE_PLAN_lives_system.md` | SUPERSEDED | `concept-v3.md` §8 |
| `.claude/docs/Overall_document/PROJECT_OVERVIEW.md` | スナップショット（2026-06-07） | `game_design_master.md` |
| `.claude/docs/ap-equipment-system.md` | FROZEN | なし（防御は Guard のみ） |
| `.claude/docs/item_document/EQUIPMENT_AND_ITEMS_DESIGN.md` | FROZEN | なし |
| `.claude/docs/util_doument/inventory_design.md` | FROZEN | なし |
| `.claude/docs/ui_ux_design_guide.md` | WEB VERSION RECORD | `battle_ui_ux_v2.md` |
| `src/` | 旧ループの Web 版 | `unity-port/` |
| `src/ui/battle-lab/core/` | 凍結 | `unity-port/BattleCore/` |

台帳の外に置くもの: `vision/plans/`（計画書）、`art_document/briefs/`（制作指示）、`handover/`、`known-issues/`、`code-explanation/`、リポジトリ直下の `docs/`（レポート・ブリーフ・モック・プロンプト）。これらは正本を引用する側で、食い違えば正本が勝ちます。

## 4. 食い違ったときに勝つ側

上の段が語っている点は、上の段が正です。上の段が黙っている点は、下の段が決めます。

1. こうだいさんの決定（Issue のコメント、`.claude/comm/decisions/ANSWERS.md`）
2. `concept-v3.md` と `core.md`
3. `world-v1.md`（呼び名と設定）、`battle_core_v4.md`（ルールと数値）、`dungeon_exploration_v4.md`（探索）
4. `swordsman_cards_v4.md` と `enemy_roster_v4.md`（3 のルールを使う側のデータ）
5. `battle_ui_ux_v2.md`（見せ方）
6. 実装（`unity-port/`）

**同じ段どうし、または直し方で遊びの結果が変わる食い違いは、順位で片づけません。** #116（鈍足と予兆の一字）がその例で、コアを直すかロースターを直すかで効く敵の数が変わります。こうだいさんの判断に回します。

## 5. 食い違いを見つけたときの手順

1. **他のレーンの正本を直さない。** 直してよいのは自分が持ち主の行だけです（one writer per artifact）
2. **Issue にする。** 題を「正本の食い違い: <何と何が>」で始め、両方の `file:§` と、直し方ごとに変わる結果を書きます。ラベルは `type:task` と両方の `area:`、`lane:` は書く側が決まるまで付けません。重複は `gh issue list -R sunbreak-pro/original-card-battle --search "正本の食い違い in:title"` で確かめます
3. **止まらずに進む。** どちらに従って進めたかを Issue と PR 本文に 1 行で書きます。既定は「自分の Issue の Scope が名指しする文書の字義どおり」です
4. **決着したら、持ち主のレーンが自分の正本を直します。** 同じコミットで本書の状態の欄を直します

## 6. 正本を腐らせない書き方

- **決定は本文へ入れ込む。** 「§N 20XX-XX-XX の決定」を末尾に積むのは記録としてだけです。積んだ時点で §2 の状態の欄に「§N 未反映」と書き、入れ込んだら消します。`battle_core_v4.md` §16〜§18 と `battle_ui_ux_v2.md` §11 で同じ形の食い違いが 2 回起きました
- **他の文書の節番号と数値を写さない。** 「戦闘の正本は §16〜§18」のような案内は、相手が改訂されると嘘になります。ファイル名までを書き、節は相手の目次に任せます
- **版を上げたら旧版にバナーを付け、§3 へ行を移す。** バナーの 1 行目に SUPERSEDED / FROZEN / WEB VERSION RECORD のいずれかと、代わりに読むファイルを書きます
- **新しい設計書は §2 に行を足してから PR にする。** `npm run sources -- --check` が、台帳に無い設計書を検出します
