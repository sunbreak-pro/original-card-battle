# Known Issues — INDEX

> 壊れている / 壊れていた箇所の Root Cause + 再発防止知見の索引。**類似バグに遭遇したら、まずこの INDEX を grep してから調査を始める。**

## 運用

- 新規発見時: `NNN-<slug>.md`（連番）を作成し、本 INDEX に 1 行追記。Status は `Open` / `Fixed` / `Mitigated`
- 横断的な教訓は `LESSONS_LEARNED.md` に蓄積（カテゴリ別）
- 体系的に追跡された脆弱性は `../code-explanation/vulnerability-remediation-guide.md` が SSOT（Tier 0-3、106 件追跡 / 約 21 修正済 / 87 残）

## カタログ

| ID                                        | タイトル                                          | カテゴリ               | Status |
| ----------------------------------------- | ------------------------------------------------- | ---------------------- | ------ |
| [001](./001-resonance-debuff-card-lag.md) | Resonance debuff の 1-card-lag 非対称             | Battle / Class Ability | Open   |
| LESSONS_LEARNED #1                        | CSS クラス名衝突（親スコープ必須）                | CSS                    | 知見   |
| LESSONS_LEARNED #2                        | Context Provider スコープ                         | React                  | 知見   |
| LESSONS_LEARNED #3                        | React Hooks ルール違反                            | React                  | 知見   |
| LESSONS_LEARNED #4                        | React 19 render 中 ref.current 参照禁止           | React 19               | 知見   |
| LESSONS_LEARNED #5                        | UI 日本語 / コード英語の言語一貫性                | 規約                   | 知見   |
| LESSONS_LEARNED #6                        | set-state-in-effect vs refs の衝突                | React 19               | 知見   |
| LESSONS_LEARNED #7                        | setState 戻り値の Mutable Object パターン         | React                  | 知見   |
| LESSONS_LEARNED #8                        | リソース state の単一真実源（gold / magicStones） | State                  | 知見   |

## 関連

- `LESSONS_LEARNED.md` — 上記 8 知見の詳細（症状 / 原因 / 解決 / 影響ファイル）
- `../code-explanation/vulnerability-remediation-guide.md` — Tier 別脆弱性トラッカー（Critical / High / Medium / Low）
- `../code-explanation/testing_analysis.md` — テストカバレッジギャップ分析
