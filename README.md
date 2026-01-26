# original-card-battle

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
