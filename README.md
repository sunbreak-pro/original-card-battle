# roguelike-card

オリジナルのターン性カードバトル。オクトパストラベラー、slay the spireからインスピレーションをもらい
typeScriptやReactを学びながらclaude codeと共に作成しようと思った。

## 11 月 27 日

git hub に連携した。

### 連携のやり方

まず最初に github からリポジトリを作成(READ.me 作成にチェック)　 1.
その後 Code メニューから中央の「Add file」をクリック。 2.
アップロードかファイル作成を選択 3.
desktop に作成したリポジトリ名と同じフォルダを作成する
VSCode を開く 4.
READ.me があれば OK！ 5.
なければターミナルから [git@github.com:sunbreak-pro/roguelike-card.git] 6.
もしそれでも無理なら AI に聞く 7.

## 1月20日〜23日の間(1月25日執筆)
### claude Maxプランに加入
加入した理由として、制限が早く使いずらい。計算したら五倍の値段で五倍の使用制限＋追加の出力向上＋より真剣に作成に取り組めるようになると仮定
以上の三つが理由。

## 1月25日
README.mdを描く習慣が全くなくいまさらになって放棄していたことを忘れた。
手書きで書くのではなく、AIにプログラミング限定の日記をつけてもらうことを徹底するようにする。

## 1月26日
### デッキシステム統合を実装
PlayerContextで設定されたデッキがバトルで正しく使用されるようにデータフローを統合した。

**変更点:**
- `initialDeckConfig.ts`: クラス別デッキ設定(`INITIAL_DECK_BY_CLASS`)を追加。剣士は20枚から15枚に調整。
- `useBattleState.ts`: `InitialPlayerState`に`deckConfig`フィールドを追加。
- `BattleScreen.tsx`: ハードコードされた`Swordman_Status`を`playerData.persistent`参照に変更。
- `useBattleOrchestrator.ts`: `getCardDataByClass()`ヘルパーを追加し、クラス別デッキ生成に対応。
- `CharacterClassData.ts`: `getStarterDeckStacks()`関数を追加（キャラ選択画面用）。

**新しい剣士デッキ構成（15枚）:**
- sw_001 x3 (迅雷斬)
- sw_003 x2 (連撃)
- sw_007 x2 (斬りつける)
- sw_013 x2 (剣気集中)
- sw_037 x2 (剣気円盾)
- sw_039 x2 (不屈の精神)
- sw_014 x2 (瞑想)

### キャラクター選択画面のカード表示改善
スターターデッキのプレビューで、各カードタイプを1枚ずつのみ表示するように変更。
- 変更前: 15枚全て表示（同じカードが複数表示される）
- 変更後: 7種類のユニークカードを1枚ずつ表示
- ヘッダーには「15 cards」と総枚数を維持

### 魔術師クラスを選択可能に
魔術師(Mage)がキャラクター選択画面で選択可能になった。

**変更点:**
- `CharacterClassData.ts`: `MAGE_CARDS`インポート追加、`createMageStarterDeck()`関数作成
- `getCardDataByClass()`: mageケースで`MAGE_CARDS`を返すよう更新
- 魔術師エントリ: `isAvailable: true`、固有メカニクス「Elemental Resonance」に更新

**魔術師スターターデッキ構成（15枚）:**
- mg_001 x3 (火球) - 火属性基本攻撃
- mg_008 x2 (炎の矢) - 0コスト火属性
- mg_009 x2 (氷結) - 氷属性基本攻撃
- mg_017 x2 (雷撃) - 雷属性基本攻撃
- mg_033 x2 (光の槍) - 光属性基本攻撃
- mg_007 x2 (炎の壁) - 火属性ガード
- mg_037 x2 (癒しの光) - 光属性回復

### Phase 1: Buff/Debuff Ownership System 実装
バフ・デバフの持続時間減少タイミングの不具合を修正。

**問題点:**
- 敵がプレイヤーに「毒（持続3ターン）」を付与
- プレイヤーフェーズ開始時に持続時間が2に減少（間違い！）
- 正しくは敵フェーズ開始時に減少すべき

**修正内容:**
- `BuffOwner`型を追加（`'player' | 'enemy' | 'environment'`）
- `BuffDebuffState`に`appliedBy`フィールドを追加
- 新関数`decreaseBuffDebuffDurationForPhase(map, currentActor)`を実装
- 旧関数`decreaseBuffDebuffDuration()`を非推奨化
- `enemyPhaseExecution.ts`のパラメータ順序バグも修正

**変更ファイル:**
- `baffType.ts` - 型定義追加
- `buffLogic.ts` - 新しい持続時間減少ロジック
- `playerPhaseExecution.ts` - プレイヤーフェーズで'player'を指定
- `enemyPhaseExecution.ts` - 敵フェーズで'enemy'を指定、パラメータ順序修正
- `useCardExecution.ts` - カード効果に'player'を指定

### MEMORY.mdのCritical Lessons Learnedセクションを整理
コンテキストサイズ削減のため、詳細説明を別ファイルに分離。

**変更内容:**
- `.claude/LESSONS_LEARNED.md`を新規作成 - 詳細な説明、コード例、エラーメッセージを含む
- `.claude/MEMORY.md`の「Critical Lessons Learned」セクションをコンパクトなテーブル形式に変更（約30行→約10行）

**テーブル形式に含まれる5つの教訓:**
| 問題 | ルール |
|------|--------|
| CSS Class Collision | 親要素でスコープ: `.battle-screen .card {}` |
| Context Provider Scope | 画面間で状態を維持 → providerをツリー上位に配置 |
| React Hooks | トップレベルで呼び出し、条件付きreturnの前に |
| React 19 Refs | render中に`ref.current`使用不可 → `useState`を使用 |
| Language | UI: 日本語 / コード: 英語 |

### Claude開発スキルの作成
ゲーム開発を効率化するためのClaude Skills（9個）を作成。

**作成したスキル:**
| スキル名 | 用途 |
|---------|------|
| `card-creator` | 新カードの追加 |
| `enemy-creator` | 新敵キャラの追加 |
| `character-class-creator` | 新クラスの実装 |
| `battle-system` | バトルシステムの修正・追加 |
| `camp-facility` | キャンプ施設の機能追加 |
| `dungeon-system` | ダンジョン探索システムの修正 |
| `design-research` | 設計ドキュメントの検索 |
| `ui-ux-creator` | UI/UXデザインガイドライン |
| `debugging-error-prevention` | デバッグとエラー防止 |

**スキルの内容:**
- 説明文は英語、ゲーム内テキスト例は日本語を維持
- 各スキルにワークフロー、コード例、参照ファイル一覧を含む
- `debugging-error-prevention`には React 19 のベストプラクティスを反映

**配置場所:** `.claude/skill/`
