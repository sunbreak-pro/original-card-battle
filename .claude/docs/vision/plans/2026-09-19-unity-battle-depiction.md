# Unity 戦闘描写セッション — 前準備と冒頭プロンプト

> **Status**: PLANNED（2026-09-19）。こうだいさんの回答 4 件（固定台本 / 配置はプレハブ・動きはコード / 1 ターンの縦切り / 影絵のまま）で方針を確定。
> **目標**: Unity Editor で、v4.2 の形の 1 ターンを通しで再生できる戦闘画面を作る。ルールの計算はしない。
> **関連**: 規則 `battle_core_v4.md` §16〜§18、画面 `battle_ui_ux_v2.md` §10〜§11、分析 `docs/reports/2026-09-19-card-balance-and-unity-prep.html`。

## 1. 方針（2026-09-19 決定）

| 項目   | 決定                                                                                                                                        |
| ------ | ------------------------------------------------------------------------------------------------------------------------------------------- |
| 土台   | 固定台本。v4.2 の形の表示データとイベント列（1 ターン分）を手書きし、画面はそれを再生する。コア（v3）は触らない。後でコアの出力に差し替える |
| 作り方 | 配置はプレハブ、動きはコード。カード・人型・HUD をプレハブにして Editor で位置と見た目を調整する。補間と演出は `UiTween` を続ける           |
| 範囲   | 1 ターンの縦切り。ドロー → 受け皿へドラッグ → 攻撃 → 切り替え + Guard → ターン終了（構え）→ 敵の予兆どおりの攻撃 → 被弾 → 次の予兆          |
| 立ち絵 | 影絵（`ProceduralArt`）のまま。差し替え口 `FigureView.SetSprite` だけ用意する                                                               |

`battle_ui_ux_v2.md` §6 方針 1「Canvas はコードで組む。プレハブとシーン YAML は作りません」は、この決定で置き換わる。v2.1 の書き直しで §6 に反映する。DOTween と Animator を入れない方針（§6 方針 3）は変えない。

## 2. 正本の置き場

| 種類                                   | 正本                                            | 写し方                                                       |
| -------------------------------------- | ----------------------------------------------- | ------------------------------------------------------------ |
| C#（コア、View、台本）                 | `unity-port/`（このリポ）                       | `npm run unity:sync` で Unity 側へ。Unity 側で直接編集しない |
| プレハブ / シーン / `.meta` / フォント | `C:/Users/user/Unity/RPG-by-card`（Unity リポ） | 写さない。Unity リポにコミットする                           |

同期スクリプトは追加と上書きだけで、Unity 側のファイルを消さない（`sync-unity-project.mjs`）。プレハブが参照する MonoBehaviour の `.cs.meta`（GUID）は Unity 側にしか無いので、Unity リポへ必ずコミットする。

## 3. セッション前に済ませること

1. **Unity リポの初回コミット**（2026-09-16 決定、未実施）。いまは全ファイルがステージされただけで、`Assets/Editor/HubForceResolve.cs` は「追加後に削除」の状態。プレハブの差分を追えるよう、描写の前にコミットを入れる。
2. **URP の版を揃える**（2026-09-16 決定、未実施）。`Packages/manifest.json` は 17.6.0、`packages-lock.json` は 17.5.0。manifest を 17.5.0 に直す。
3. **Unity MCP をつなぐ**（2026-09-19 に接続を確認済み。`editor_status` = ready）。以下は切れたときの手順。`~/.claude.json` に `unity-editor-mcp`（`unity mcp --project-path C:/Users/user/Unity/RPG-by-card`）は登録済みだが、2026-09-19 のセッションでは道具として見えなかった。手順は、残っている `unity.exe`（CLI、約 20 MB が 10 個）を終了 → Unity Editor で RPG-by-card を開く → その後に Claude Code を起動 → `/mcp` で `unity-editor-mcp` が connected か見る。
4. **Editor を前面に置く**。再生の確認とプレハブの微調整は Editor の Game ビューで行う。

1 と 2 はセッションの冒頭で Claude に任せてもよい（プロンプトの「準備」に入れてある）。3 は Claude Code の起動前に、こうだいさんの手で要る。

## 4. 台本（1 ターン分、叩き台）

数値は `battle_core_v4.md` §18 の叩き台（アタック 6 / 13 / 21、ガード 4 / 9 / 15、回復 3）。

**開始の状態**: ターン 2。プレイヤー HP 50 / 50、スタミナ 6 / 10、近間。敵は長柄の歪み兵 HP 60 / 60（通常敵、位置なし）。予兆は「攻撃・近 13」（近間にいる相手へ +5）。

| 順  | 出来事               | 表示の変化                                                                                       |
| --- | -------------------- | ------------------------------------------------------------------------------------------------ |
| 1   | ターン開始           | Guard 0、スタミナ 6 → 9、5 枚引く（袈裟斬り / 大上段 / 後ろ跳び / 鉄の受け / 観察）、予兆を出す  |
| 2   | 袈裟斬り（コスト 1） | 受け皿へドラッグ。特性「初手: 威力 +3」が発火。6 + 3 = 9。敵 HP 60 → 51。スタミナ 9 → 8          |
| 3   | 大上段（コスト 2）   | 受け皿へドラッグ。13。敵 HP 51 → 38。スタミナ 8 → 6                                              |
| 4   | 後ろ跳び（コスト 1） | 投げ上げ線。遠間へ切り替え、Guard 4。特性「予兆(攻撃): Guard +3」が発火。Guard 7。スタミナ 6 → 5 |
| 5   | ターン終了           | 構え（残 5 ≥ 3）で Guard +3 → 10。残り 2 枚を捨てる                                              |
| 6   | 敵の攻撃             | 予兆どおり 13。近間の +5 は外れる（外した合図）。13 − 10 = 3。HP 50 → 47                         |
| 7   | 次の予兆             | 「防御」を出して終わる                                                                           |

演出の強弱 4 段（`battle_ui_ux_v2.md` §11.4）のうち、順 2 が弱、順 3 が中、順 6 が Guard で大半を止めた被弾になる。

## 5. 完了条件

1. Unity Editor で `BattleDepiction` シーンを再生すると、§4 の 7 つの出来事が通しで 1 回流れる。順 2〜4 は実際のドラッグ操作で進み、順 1 / 5〜7 は自動で進む。
2. カード / 人型 / HUD（HP・Guard・スタミナ・状態）/ 予兆 / 受け皿 / 投げ上げ線がプレハブで、Editor で位置を動かしても再生が壊れない。
3. 画面は台本の値を表示するだけで、威力や Guard を計算しない。
4. 既存の View v1.1（`test1.unity`、v3 コア）と `dotnet test` 54 件が壊れない。
5. Game ビューのスクリーンショットを出来事ごとに撮り、`visual-inspect` で座標のずれと文字の欠けを確かめた。
6. 手触りの確認（ドラッグの気持ちよさ、演出の長さ）は人手。報告に確認の手順を書く。

## 6. 触らないもの

- `unity-port/BattleCore/`（v3 コア）と `BattleCore.Tests/`。
- 既存の View 11 ファイルの挙動。部品（`UiKit` / `UiTween` / `ProceduralArt` / `BattleTheme`）は読んで再利用し、変更が要るなら新しいファイルへ写す。
- `src/`（Web 版、凍結）、設計書の数値。
- 立ち絵の制作（`visual-production-pipeline` の別セッション）、2 体戦、精鋭の 2 行動、連戦、結果画面。

## 7. 冒頭に貼るプロンプト

```
Unity Editor で戦闘の描写（1 ターンの縦切り）を作ってください。ルールの計算は作りません。固定の台本を再生する画面を作ります。

最初に読むもの（この順）:
1. .claude/docs/vision/plans/2026-09-19-unity-battle-depiction.md（このプラン。方針 / 正本の置き場 / 台本 / 完了条件 / 触らないもの）
2. .claude/docs/battle_document/battle_ui_ux_v2.md の §10 と §11（上帯の廃止、語の予算、受け皿と投げ上げ線、近間 / 遠間の一字札、演出 4 段）。§1〜§7 は旧版なので、§10 / §11 と食い違う所は §10 / §11 が正
3. .claude/docs/battle_document/battle_core_v4.md の §17 と §18（列 = コスト、スタック制、目盛り 6 / 13 / 21、回復 3）
4. unity-port/README.md と unity-port/unity-project-kit/Assets/View/ の 11 ファイル（再利用する部品: UiKit / UiTween / ProceduralArt / BattleTheme）
5. docs/mockups/2026-09-14-drag-select-demo.html（受け皿と投げ上げ線の動き）

準備（最初に確かめて、欠けていたら直す）:
- unity-editor-mcp はつながっている（2026-09-19 に editor_status = ready、RPG-by-card、6000.5.5f1 を確認済み）。最初に ToolSearch で要る道具をまとめて読み込み、editor_status で ready を確かめる。使う道具の目安: editor_status / recompile / recompile_status / get_console_logs / create_scene / open_scene / save_scene / create_prefab / instantiate_prefab / save_prefab_contents / get_scene_hierarchy / set_serialized_field / menu / editor_play / editor_stop / capture_game_view / run_tests / test_status。途中で切れたら unity CLI（unity test / unity build）と C# の Editor スクリプトで代用し、報告に書く
- C:/Users/user/Unity/RPG-by-card に初回コミットがあるか。無ければ、ステージ済みの内容を確かめてから初回コミットを入れる（Assets/Editor/HubForceResolve.cs は削除済みなので含めない）
- Packages/manifest.json の URP を 17.5.0 に揃える（lock は 17.5.0）

作るもの:
- 台本: v4.2 の形の表示データ（DepictionFrame）と出来事の列（DepictionEvent）を純 C# で定義し、プランの §4 の 7 つの出来事を手書きする。置き場は unity-port/unity-project-kit/Assets/View/Depiction/。BattleCore の型には依存させない（後でコアの出力から同じ型を作れる形にする）
- プレハブ: Card / Figure（プレイヤーと敵で共用、足元に「近」「遠」の一字札）/ StatusBar（HP・Guard・スタミナのピップ・状態チップ）/ OmenBadge（種別 + 咎める側の一字 + 値）/ Receiver（受け皿、予測値 1 つ）/ ThrowLine（投げ上げ線）/ CornerInfo（左上: ターン・階層・連戦・瘴気）。プレハブは Editor スクリプト（メニュー 1 つで再生成できる形）で最初の版を作り、以後は Editor で手直しできるようにする。再生成が手直しを上書きしないよう、既存のプレハブがあれば作り直さない
- シーン: Assets/Scenes/BattleDepiction.unity。参照解像度 1920×1080、ScreenSpace-Overlay、matchWidthOrHeight 0.5 は既存と同じ
- 再生: DepictionPlayer（MonoBehaviour）が台本を順に流す。順 2〜4 はプレイヤーのドラッグを待つ。単体向きの札は受け皿の上で離したときだけ確定し、皿の外で離すと手札に戻る。自分向きの札は投げ上げ線より上で離すと確定する。台本と違う札を出そうとしたら手札に戻す
- 演出: 補間は UiTween。斬撃、Guard で止める、数字の浮き、被弾の揺れ、特性の発火の合図、切り替え（立ち位置の前後移動と一字札の入れ替え）、構えの合図。1 行動 2.0 秒以内。強弱は確定値で 4 段
- 立ち絵は ProceduralArt の影絵。FigureView.SetSprite(Sprite) の差し替え口だけ用意する

決まりごと:
- C# の正本は unity-port/。編集はこのリポで行い、npm run unity:sync で Unity 側へ写す。プレハブ / シーン / .meta は Unity リポが正本で、Unity リポへコミットする
- 画面は計算しない。数値・文言・条件の成否は全て台本から取る
- 静止時の固定ラベルは 10 字まで。上帯は作らない。刻限・手番・敵名は出さない
- DOTween と Animator は入れない。文字の部品は既存の UiKit に合わせる
- 触らないもの: unity-port/BattleCore/、BattleCore.Tests/、既存 View 11 ファイルの挙動、test1.unity、src/、設計書の数値

進め方:
- 範囲（触るファイル / 完了条件 / 触らないもの）を宣言してから、台本の型 → プレハブ生成 → シーン → 再生（自動の順 1 / 5〜7）→ ドラッグ（順 2〜4）→ 演出 → 確認、の順
- 確認は 4 つ。(1) npm run unity:sync の後に recompile → get_console_logs でエラー 0。(2) run_tests（EditMode）で、台本の 7 つの出来事が順に流れ切るテストが緑。(3) dotnet test unity-port/UnityCorePort.slnx が 54 件緑のまま。(4) editor_play で再生し、capture_game_view で出来事ごとに撮って visual-inspect で拡大して確かめる。ドラッグ待ちの順 2〜4 は、確認用に DepictionPlayer へ自動で進めるデバッグの切り替え（既定は切）を持たせて撮る
- ブランチは feat/unity-battle-depiction（main から）。Unity リポも同名のブランチ
- 途中で質問して止まらない。置いた仮定は報告に書く。人手の確認（ドラッグの手触り、演出の長さ）は手順を報告に書く
- 報告は html-report スキルで、出来事ごとのスクリーンショット、プレハブの一覧、コアへ差し替えるときの接続点（DepictionFrame を BattleViewModel から作る写像）を載せる
```

## 8. この後の予定

| 順  | 内容                                                                                      |
| --- | ----------------------------------------------------------------------------------------- |
| 1   | 描写セッション（本プラン）                                                                |
| 2   | 戦闘 v4.2 の本文改訂（`battle_core_v4.md` §16〜§18 を §0〜§14 と 80 種・9 体へ反映）      |
| 3   | v4.2 コアの C# 実装 + 試験台（`2026-09-13-battle-v4-implementation.md` を書き直して解除） |
| 4   | 描写をコアへつなぐ（`DepictionFrame` をコアの出力から作る）                               |

順 1 と順 2 は独立しているので、どちらを先にしてもよい。
