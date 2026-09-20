---
name: loop-verify
description: 検証ゲートを通し、落ちた原因をコード起因か環境起因かに切り分けるループ。PR 前や merge 後の main が緑か確かめたい時に明示起動する。
disable-model-invocation: true
---

# /loop-verify — ゲートを通し、落ちた原因を切り分ける

## 目標

対象の変更（作業ブランチ、または merge 後の main）に検証ゲートを通し、緑にする。緑にできないものは**コード起因か環境起因かを言い切って**手を離す。

## 完了条件（機械検証可能）

- 下の検証コマンドがすべて exit 0、**または**落ちた各件に「コード起因 / 環境起因」の結論と根拠 1 行がついている
- 環境起因と結論したものが `.claude/docs/known-issues/INDEX.md` の既知パターンに載っているか、載っていなければ候補として書き出されている
- コード起因で未修正のものが 1 件も残っていない（残るなら停止条件に当たっている）

## 予算

- 反復上限: **3 周**（1 周 = ゲート一巡 → 修正）。`session-verifier` が各ゲート内で最大 2 回リトライを持つので、本ループはその**外側の輪**。二重に数えない
- 時間上限: **30 分**。開始直後に `START_TS=$(date +%s)` を取る
- 超えたら、緑にできた範囲と落ちたままの一覧を出して停止する

## 停止条件（人間に返す）

- **環境起因と判断できた時点**（そこから先はこのループの担当ではない。深追いしない）
- 落ちているのが自分の変更していないファイルで、直すと Scope の外に出る
- 同じゲートが 3 周とも同じ理由で落ちる（周回を増やしても状況が変わっていない = 前提が違う）
- 修正すると別のゲートが落ちる往復に入った（2 往復で停止）
- **Unity Editor がつながらず、EditMode / PlayMode が回せない**（`dotnet test` までで切って明記する）

## 検証コマンド（この repo の実物）

**CI はまだ無い。** `.github/workflows/` が存在しないので、ゲートの正本は `.claude/CLAUDE.md` の Development Commands。

| 順  | コマンド                          | 見るもの                    |
| --- | --------------------------------- | --------------------------- |
| 1   | `npm run build`                   | `tsc -b` の型 + vite build  |
| 2   | `npm run lint`                    | eslint                      |
| 3   | `npm run test:run`                | vitest 単発                 |
| 4   | `cd unity-port && dotnet test`    | BattleCore 54 件            |
| 5   | `npm run parity:check`            | TS / C# のパリティ          |
| 6   | `npm run unity:sync`              | Unity リポへの写し漏れ 0 件 |
| 7   | `unity-editor-mcp` の `run_tests` | EditMode / PlayMode         |

**`npm run build` はテストを見ない。** build が通っても `test:run` が落ちることがあるので、build 緑を「通った」と読まない。

## 使ってよい道具

- `session-verifier` スキル — ゲートの正本（Gate 0 Scope → 1 型 → 2 lint → 3 test → 4 coverage → 5 プロジェクト規約）
- `debugging-active` スキル — 原因の切り分けが噛み合わないとき
- `debugging-error-prevention` スキル — 同じ型のバグを繰り返しているとき
- `.claude/docs/known-issues/INDEX.md` — 既知の環境起因パターンの照合先。**類似バグはまずここを grep**

## 環境の事実（推論では埋まらないので明記する）

- **`unity:sync` は 2 回目が 0 件になるのが正常**。1 回目で差分が出るのは写していないだけで、コード起因ではない
- **Unity の実機の手触り（ドラッグ・演出の長さ・重なり）は機械では測れない**。落ちていなくても「確かめた」と読まない。`type:human` の Issue へ回す
- **ツール実行直後にハングしたら ESC で復帰する**。原因は Claude Code 本体側で、ローカル調査は無駄（`~/.claude/docs/bash-tool-stability.md`）
- **`session-start-check.sh` が Windows で落ちるのは既知**（#40、暫定回避済み）。これを「環境起因の新発見」として書かない
- **固定台本の再生が壊れていないか**は、戦闘描写に触る変更では毎回見る。View は計算せず値を台本から取る規約が崩れると、テストは緑のまま画面だけ壊れる

---

- 「たぶん環境」で止めない。環境起因と言い切るなら根拠を 1 行つける（既知パターンとの一致 / 別マシンとの差 / 再現条件）
- 実測した周回数・所要時間は、区切りで `history/chat-<self>.md` に 1 行足す

## 未導入

CI（`.github/workflows/ci.yml` の `verify` + `docs-lint`）。入ったらゲートの正本をそちらへ移し、この表は消して参照に置き換える。
