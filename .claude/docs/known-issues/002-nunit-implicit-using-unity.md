# 002 — Unity で EditMode テストが `CS0246: 'Test' could not be found` ×57

- **Status**: Fixed（2026-09-06）
- **カテゴリ**: Unity Port / Build
- **影響ファイル**: `unity-port/BattleCore.Tests/*.cs`（4 本）、`unity-port/BattleCore.Tests/BattleCore.Tests.csproj:21`

## 症状

`npm run unity:sync` 後、Unity Editor が `Assets/Tests/*.cs` を 57 箇所で
`error CS0246: The type or namespace name 'Test' could not be found` として拒否し、
プロジェクト全体がコンパイル不能になった。副作用として Unity CLI のブリッジがポートを
公開せず、`unity-editor-mcp` も接続できなかった。

## Root Cause

テストソースに `using NUnit.Framework;` が 1 行も無かった。headless 側（dotnet test）は
`BattleCore.Tests.csproj` の `<Using Include="NUnit.Framework" />`（暗黙 global using）で
全ファイルに効いていたため成立していたが、Unity の asmdef ビルドにはこの仕組みが無い。

## 解決

4 ファイルの先頭に `using NUnit.Framework;` を明示追加（`unity-port/BattleCore.Tests/` 側で修正し、
`npm run unity:sync` で Unity へ反映）。headless 側は暗黙 using と重複するだけで無害
（`npm run parity:check` 58/58 維持）。

## 再発防止

- **csproj の暗黙 using（`<Using Include>` / `ImplicitUsings`）に依存したコードは Unity の asmdef ビルドで落ちる。**
  Unity へ持ち込む C# は、必要な `using` をファイル内に全部明示する。
- `Assets/Tests/` は生成物なので触らない。直すのは常に `unity-port/` 側。
- 詳細は `unity-port/unity-project-kit/README.md` の Caveats 参照。
