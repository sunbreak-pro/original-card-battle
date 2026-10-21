# ブラウザ試遊版（Original Card Battle 試遊版）

継承の間から出立し、七つの層を降りて戦い、死ぬか生き延びるかして、次の踏破者へ遺産を渡す。この一続きの生を、ブラウザでそのまま遊べるようにしたものです。公開版は Artifact「Original Card Battle 試遊版」（https://claude.ai/artifact/SMRswFnM5LEGuFjNmcEfo8 、非公開）です。

- **戦闘と探索の規則は C# をそのまま移しています。** `unity-port/` の BattleCore・DungeonCore・DungeonContent と、View の `Bridge/`・`Script/`（Depiction・Exploration・Journal・Backdrop）を、名前もそのままに JS へ書き写しました。C# を実行して出した結果と、1,506 件で突き合わせています。
- **見た目はデザインシステム「Original Card Battle 戦闘画面」です。** `vendor/ds/` にその組み上がった部品を置いています。
- **C# にまだ無い規則は、文書から作った試作です。** 継承（遺産・痕跡・生存者ボーナス）、探索の持ち物とイベント、手記の敵の頁、2〜3 体の戦闘、俊敏の自由な一歩がそれにあたります。どれも、どの文書のどの節から作ったかを `docs/EXTENSIONS.md` と `docs/INHERITANCE.md` に書いています。C# の側が入ったら、そちらに置き換えます。

## 遊ぶ

`dist/index.html` をブラウザで開くと、そのまま遊べます（`npm install` と `node build.mjs` の後）。進み具合はブラウザの保存領域に残り、戦闘の途中で閉じても、その場面から再開します（要件 R1-16）。

| 場面 | 操作 |
| --- | --- |
| 戦闘 | 札をつかんで敵か自分の受け皿で離す。数字キー 1〜9 で札を選んで Enter。X でターン終了。Space で早送り（押し続けると続く）。俊敏の一歩は ← / → |
| 探索 | ノードを選んで Enter。J 手記、C 所持カード、Esc 設定 |

## 開発

```bash
npm install
node build.mjs            # dist/ を作る（--dev で dist/dev.html も）
npm test                  # 単体テスト（約 2,000 件）
dotnet run -c Release --project oracle -- --out oracle/out   # C# の結果を書き出す（数分）
node test/parity/run.mjs  # JS と C# の突き合わせ（1504 件一致 + 意図した差 2 件）
```

テスト用のスイッチ: `dist/dev.html?autobattle&debug` は戦闘を自動で戦い、`window.__ocb` で状態を読めます。

## 置き場

| パス | 中身 |
| --- | --- |
| `src/core/` | 戦闘・探索・持ち物の規則（C# の写し）と、継承の試作（`inheritance/`） |
| `src/bridge/` | 画面に出す値と文（C# の View の `Bridge/`・`Script/` の写し） |
| `src/scenes/` | 画面（battle・explore・meta）。値は計算せず、bridge の値を描くだけ |
| `src/app/` | 画面の切り替え、セーブ（呼び出しの記録を再生する形）、設定 |
| `oracle/` | C# を実行して比較用の結果を書き出す小さなプログラム |
| `docs/` | 設計（ARCH）、移し方の決まり（CONVENTIONS）、試作の規則（EXTENSIONS・INHERITANCE）、戦闘画面（BATTLE_SCENE） |

## C# の側で見つかったこと

移す途中で、C# の側にも同じ形で残っている問題が見つかりました。試遊版では JS の側で直し、`docs/port-notes/depiction-multi-enemy.md` に書いています。

- 2 体の戦闘（錆槍の竜兵 + 灰弩の竜兵、第 2〜3 層）で、`CoreScriptWriter` が 2 体目の予兆を 1 体目の行動表で引き、例外で止まります。
- `CoreBattleSource` が札の相手を渡さないため、1 体目が倒れた後は単体向けの札がすべて断られ、2 体の戦闘に勝てません。
- 予兆の札で、精鋭とボスの 2 手目の届くマスと当たり外れの印、ボスの第 2 段階の印、移動札の残像、威圧などで変わった札の数値は、台本に値が無いため出せません（Unity も同じです）。
