# original-card-battle

死者の技を継ぎ、失われた知識を自らの記憶で補いながら、ダンジョンの深奥を目指す、高難度の探索とカード戦略のゲームです。
個人開発で、実装先は Unity です。ブラウザ版（React）も同じリポジトリにあります。

## 構成

| 場所 | 中身 |
| --- | --- |
| `src/` | ブラウザ版です。React 19・TypeScript 5.9・Vite 7 で書き、描画に PixiJS（`pixi.js`）を使います。 |
| `src/ui/battle-lab/core/` | ブラウザ版の戦闘の試作です。凍結していて、戦闘の規則は Unity 側の C# が持ちます。 |
| `unity-port/` | Unity への移植です。Unity のエディタを使わない C# のコア（`BattleCore/`・`DungeonCore/` など）と、そのテストが入っています。 |
| `unity-port/unity-project-kit/` | Unity プロジェクトへ渡す部品（asmdef・画面の雛形・`.gitignore`）です。 |
| `unity-port/tools/` | コアを Unity プロジェクトへ同期する、C# がコンパイルできるかを検査するなどのスクリプトです。 |

Unity のバージョンは 6000.5.5f1 です（`unity-port/unity-project-kit/README.md`）。Unity プロジェクトは別のリポジトリ（`sunbreak-pro/RPG-by-card`）にあります。

## コマンド

Node.js 20.12 以上が必要です。最初に `npm install` を実行してください。

```bash
npm run dev            # ブラウザ版の開発サーバーを起動する
npm run build          # 型を検査してから本番用にビルドする
npm run preview        # ビルドしたものを確認する
npm run lint           # ESLint を実行する
npm run test           # Vitest を監視モードで実行する
npm run test:run       # Vitest を 1 回だけ実行する
npm run test:coverage  # カバレッジつきで Vitest を実行する
```

Unity 側のコマンドは次のとおりです。

```bash
npm run unity:sync     # C# のコアと画面の部品を Unity プロジェクトへ同期する
npm run unity:check    # Unity のエディタを開かずに、C# がコンパイルできるかを検査する
```

C# のテストは `dotnet test unity-port/UnityCorePort.slnx` で実行します。前提と手順は `unity-port/README.md` にあります。

## 文書の入口

- `.claude/CLAUDE.md`: 実装の規約・構成・ブランチの運用です。
- `.claude/docs/INDEX.md`: 設計書の一覧です。
- `.claude/docs/SOURCES.md`: 主題ごとに、どの文書が元になるかを示します。
- `.claude/docs/vision/core.md`: ゲームの狙いと設計原則です。
- `unity-port/README.md`: C# コアの構造と、Unity 移植の経緯です。
