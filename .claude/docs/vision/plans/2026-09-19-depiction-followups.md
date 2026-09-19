# 戦闘描写の後片付け — 次セッションのプランと冒頭プロンプト

> **Status**: PLANNED（2026-09-19）。
> **Task**: 戦闘描写の後片付け（Issue #26 / #27 / #28 / #29 の前半）
> **関連**: 完了プラン `.claude/archive/2026-09-19-unity-battle-depiction.md`、レポート `docs/reports/2026-09-19-unity-battle-depiction.html`。

## 1. Context

2026-09-19 の描写セッションで、固定台本の 1 ターンが Unity Editor で流れるようになった（Web リポ PR #24、Unity リポ PR #1）。そのとき範囲の外に出した点と、QA レビューで見送った点を Issue にした。このプランは、人の判断を待たずに Claude だけで進められる 4 件をまとめて片付ける。

| Issue | 内容                                                       | このプランでの扱い                   |
| ----- | ---------------------------------------------------------- | ------------------------------------ |
| #25   | [人手] ドラッグの手触りと演出の長さ                        | 対象外。こうだいさんの確認           |
| #26   | `BattleScreenView` の自動起動にシーンの条件を付ける        | 対象                                 |
| #27   | `unity:sync` が改行コードだけの差分を作らないようにする    | 対象                                 |
| #28   | 1 行動 2.0 秒の上限を PlayMode テストにする                | 対象                                 |
| #29   | 台本の型の詰め（攻撃者・一字札の文字）と v4.2 コアの変換器 | 前半だけ対象。変換器は v4.2 コアの後 |
| #30   | [設計] 強弱 4 段のしきい値と投げ上げ線の予測値の形         | 対象外。v4.2 / v2.1 の改訂で決める   |

## 2. 検討した代替案

- **#26 を `test1.unity` に View を置く形で直す**: 自動起動そのものを消せて素直。ただし Unity リポのシーンを触り、コマンドライン引数（`-replayTrace` / `-captureDir`）の受け口を作り直すことになる。今回は採らず、自動起動にシーン名の条件を足す最小の変更にする。
- **#27 を `.gitattributes` 側で直す**: Unity リポの設定を変えると、Unity のテンプレートからずれる。同期スクリプトが改行をそろえて比べるほうが、影響がこのリポの 1 ファイルで済む。
- **#28 を EditMode で済ませる**: 時間は Play しないと測れないので不可。PlayMode テストを 1 本だけ足す。

## 3. Scope

- **触るファイル**: `unity-port/unity-project-kit/Assets/View/BattleScreenView.cs`（自動起動の条件だけ）、`unity-port/unity-project-kit/Assets/View/Depiction/**`、`unity-port/tools/sync-unity-project.mjs`、`package.json`（`engines` だけ）、Unity リポの `Assets/View/Depiction/Tests/` の `.meta`。
- **完了条件**: §6 の確認が全て緑。Issue #26 / #27 / #28 をクローズし、#29 に前半の完了をコメントする。
- **触らないもの**: `unity-port/BattleCore/`、`BattleCore.Tests/`、`BattleScreenView.cs` の自動起動以外の挙動とほかの既存 View 10 ファイル、`test1.unity`、プレハブとシーンの配置、`src/`、設計書の数値。

## 4. Steps

1. 前提の確認: PR #24 と RPG-by-card#1 がマージ済みか見る。未マージなら、`feat/unity-battle-depiction` から枝を切る。
2. #27: `sync-unity-project.mjs` の比較を、改行を LF にそろえてから行う。`package.json` に `engines.node >= 20.12` を足す。
3. #26: `BattleScreenView.Bootstrap` に条件を足す。`DepictionPlayer` がシーンにあるときは生成しない、が最小。`DepictionPlayer.Start` の `Destroy` の回避を消す。
4. #29 前半: `Cue.Source`（`UnitSide`）と、`UnitFrame.RangeGlyph`（文字列）を足す。`TurnSliceScript` に値を書き、`DepictionPlayer.Attack` と `FigureView` が台本から取るようにする。EditMode テストで必須にする。
5. #28: PlayMode テスト用の asmdef を `Assets/View/Depiction/Tests/PlayMode/` に足し、通しで流して `EventSeconds` の全要素が 2.0 未満、`Finished`、最後のフレームの値を確かめる。
6. 確認 → 撮り直し（見た目が変わっていないこと）→ コミット → PR → Issue の整理。

## 5. Files

| ファイル                                                             | 変更                                 |
| -------------------------------------------------------------------- | ------------------------------------ |
| `unity-port/tools/sync-unity-project.mjs`                            | 改行をそろえて比較                   |
| `package.json`                                                       | `engines.node`                       |
| `unity-port/unity-project-kit/Assets/View/BattleScreenView.cs`       | `Bootstrap` の条件 1 つ              |
| `.../View/Depiction/DepictionPlayer.cs`                              | 回避の削除、`Cue.Source` を読む      |
| `.../View/Depiction/FigureView.cs`                                   | 一字札の文字を引数で受ける           |
| `.../View/Depiction/Script/DepictionTypes.cs` / `TurnSliceScript.cs` | `Cue.Source`、`UnitFrame.RangeGlyph` |
| `.../View/Depiction/Tests/DepictionScriptTests.cs`                   | 新しい 2 項目を必須に                |
| `.../View/Depiction/Tests/PlayMode/*`（新規）                        | PlayMode テストと asmdef             |

## 6. Verification

1. `npm run unity:sync` を 2 回続けて回し、2 回目が「0 file(s) written」。直後に Unity リポの `git status` が空。
2. recompile → `get_console_logs` でエラー 0。
3. `run_tests`: EditMode が緑（60 件 + 追加分）、PlayMode の新しい 1 本が緑。
4. `dotnet test unity-port/UnityCorePort.slnx` が 54 件緑。
5. `BattleDepiction` を再生して `BattleScreenView` が生成されないこと、`test1.unity` を再生して View v1.1 が今までどおり出ることを、`capture_game_view` で 1 枚ずつ撮って確かめる。
6. `debugCaptureDir` で撮り直し、2026-09-19 の 15 枚と見た目が変わっていないことを `visual-inspect` で確かめる。

## 7. 冒頭に貼るプロンプト

```
戦闘描写の後片付けをしてください。Issue #26 / #27 / #28 と #29 の前半を 1 セッションで片付けます。

最初に読むもの（この順）:
1. .claude/docs/vision/plans/2026-09-19-depiction-followups.md（このプラン。Scope / Steps / Files / Verification）
2. gh issue view 26 / 27 / 28 / 29（完了条件はここが正）
3. docs/reports/2026-09-19-unity-battle-depiction.html（前回の結果、QA の指摘、コアへの接続点）
4. unity-port/unity-project-kit/Assets/View/Depiction/ の全ファイルと、BattleScreenView.cs の Bootstrap（77 行目付近）

準備（最初に確かめる）:
- unity-editor-mcp がつながっているか。ToolSearch で editor_status / recompile / get_console_logs / eval / editor_play / editor_stop / capture_game_view / run_tests をまとめて読み込み、editor_status で ready を確かめる
- PR #24（original-card-battle）と RPG-by-card#1 がマージ済みか。済みなら両リポで main を pull してから fix/depiction-followups を切る。未マージなら feat/unity-battle-depiction から切り、報告に書く
- Unity Editor は前面でなくてよい（描写シーンは runInBackground を入れてある）。test1.unity を再生して確かめるときだけ、時計が止まったら Application.runInBackground を eval で入れる

決まりごと:
- C# の正本は unity-port/。編集はこのリポで行い、npm run unity:sync で Unity 側へ写す。.meta と PlayMode テスト用の asmdef の .meta は Unity リポへコミットする
- 画面は計算しない。足す値（攻撃者、一字札の文字）も台本に持たせ、テストで必須にする
- BattleScreenView.cs で触ってよいのは Bootstrap の条件だけ。ほかの既存 View 10 ファイル、BattleCore/、BattleCore.Tests/、test1.unity、プレハブとシーンの配置、src/、設計書の数値は触らない
- 削除コマンド（rm -rf）は権限で拒否される。アセットを消す必要が出たら、消さずに済む方法を選び、報告に書く
- プレハブとシーンは作り直さない。手直しが要るなら eval で PrefabUtility.LoadPrefabContents → SaveAsPrefabAsset

進め方:
- 範囲（触るファイル / 完了条件 / 触らないもの）を宣言してから、#27 → #26 → #29 前半 → #28 の順。#27 を先にするのは、以降の同期で改行だけの差分が出なくなるため
- 確認はプラン §6 の 6 つ。通しの再生は eval で DepictionPlayer の autoPlayDrags と debugCaptureDir を入れてから editor_play。切り替えは保存されないので、再生のたびに入れ直す
- 実装の後に role-qa を別コンテキストで 1 回。Blocking が 0 になってからコミット
- 途中で質問して止まらない。置いた仮定は報告に書く
- 完了したら task-tracker（END）、両リポへコミットと push、PR を 1 本ずつ。Issue #26 / #27 / #28 は PR の本文に Closes を書き、#29 には前半の完了をコメントする
- 報告は文章で足りる（判断材料が 3 つ以上並ぶなら html-report）

対象外（触らずに報告へ回す）:
- #25 手触りの確認（こうだいさんの手番）
- #30 強弱のしきい値と投げ上げ線の予測値の形（v4.2 / v2.1 の改訂で決める）
- #29 の後半（v4.2 コアからの変換器。コアの C# 実装の後）
```

## 8. この後の予定

| 順  | 内容                                                                   | Issue / プラン                           |
| --- | ---------------------------------------------------------------------- | ---------------------------------------- |
| 1   | 手触りの確認                                                           | #25（人手）                              |
| 2   | 描写の後片付け（本プラン）                                             | #26 / #27 / #28 / #29 前半               |
| 3   | 戦闘 v4.2 の本文改訂。強弱のしきい値と投げ上げ線の予測値もここで決める | `MEMORY.md` の予定、#30                  |
| 4   | v4.2 コアの C# 実装 + 試験台                                           | `2026-09-13-battle-v4-implementation.md` |
| 5   | 描写をコアへつなぐ                                                     | #29 後半                                 |

順 1 と順 2 は独立している。順 2 と順 3 も独立している。
