# 試遊版を Unity へ写す作業の資料（2026-10-22）

ブラウザの試遊版（Artifact「Original Card Battle 試遊版」、元のコードは `origin/claude/nifty-dirac-a99nv7` の `prototypes/browser-game/`）を、Unity の `View/Trial` へ写したときの下調べと見本の撮り方です。写し方の決まりそのものは `unity-port/TRIAL-PORT.md` にあります。

- `analysis/`: 写す前に、試遊版のコードと Unity の今のコードを領域ごとに読み比べた報告です。部品の見た目（kit）、規則の差（rules）、画面の切り替えと保存（app）、音（audio）、戦闘の流れ（battle-flow）と部品（battle-views）、探索（explore）、タイトル・継承の間・終わり（meta-a）、出立・所持カード・設定・手記（meta-b）の 9 本です。
- `reference-capture/`: 試遊版を 1920×1080 で撮った見本の画面の一覧（`INDEX.md`）と、撮り直すためのスクリプトです。画像は 445 枚・約 276 MB あるので、リポジトリには入れていません。試遊版を `node build.mjs --dev` で組み立ててから、`cap-meta.mjs` から順に実行すると同じ画面を撮り直せます。スクリプトは Playwright MCP に同梱の playwright を使います。
