# Plan: 全ての画面で共通のヘッダー（最初の PR: 帯の表示と今ある開き先へのつなぎ込み）

- **Status**: IN PROGRESS（土台の単位から実装中。PR で止めて、こうだいさんが Unity で見てから入れる）
- **Created**: 2026-11-28
- **Task**: 部品ごとに見て合格を決める流れを作る（Issue #1074）
- **Project**: `C:/Users/user/orca/original-card-battle`（作業は worktree `main-1074`、ブランチ `feat/main-1074`）
- **計画書**: `.claude/docs/vision/plans/2026-11-28-common-header.md`

## Context

こうだいさんは 2026-11-25 に、全ての画面で共通のヘッダーを作ると決めました（#1074）。2026-11-28 に、Claude Design のキャンバス（https://claude.ai/artifact/HXQzENUH85Ch1byvPZ7yvn ）で形が決まりました。決まった絵の写しは `docs/mockups/2026-11-28-common-header/header.dc.html` です。

決まった形は次のとおりです。

- 高さ 96 px の帯を、拠点・支度の卓・探索・戦闘の上端に置きます。元は探索の今の帯です。
- 左から、現在地・暦（「残り N 日」。数字だけ大きく、棒なし）・HP（「38 / 50」。棒なし）・スタミナ（「8 / 10」）を、左に詰めて並べます。
- 右端は、所持カード（束の枚数の印つき）・手記・設定です。戦闘では、その左に速さのボタンと区切りの線を置きます。
- 「タイトルへ」はヘッダーに置きません（設定の画面に、すでに「保存してタイトルへ」があります）。訓練の刻みはヘッダーに出しません（訓練の仕組みは残します）。
- 拠点の現在地は「拠点」、支度の卓は「拠点」に「支度の卓」の札、戦闘は層の名前にターンの札を添えます。

最初の PR は、帯の表示と、今ある開き先（手記・設定・探索の所持カード）へのつなぎ込みまでです。新しい部品なので、PR で止めて、こうだいさんが Unity で見てから入れます。

### 値（絵の CSS から）

| 項目 | 値 |
| --- | --- |
| 帯 | 幅 1920、高さ 96、左右の余白 24、項目の間 28、地 `rgba(16,20,30,.88)`、下の線 1 px `rgba(157,166,186,.26)`、影 `0 6px 18px rgba(16,20,29,.6)` |
| 現在地 | 絵 28 px、名前は明朝 700 の 28 px（行 36 px、字間 .04em）、絵と名前の間 12 |
| 現在地の札 | 16 px 太字、行 26 px、左右の余白 10、枠 1 px 鋼、角 4 px |
| 区切りの線 | 現在地の右に 1 本。幅 1、高さ 40、`rgba(157,166,186,.26)` |
| 暦 | 砂時計の絵 26 px、「残り」「日」は 20 px 太字、数字は等幅 700 の 32 px（左右 4 px） |
| HP | ハートの絵 22 px（緋）、数字は等幅 700 の 24 px、「 / 50」は 20 px 太字 |
| スタミナ | 琥珀の丸 20 px、数字は等幅 700 の 24 px、「 / 10」は 20 px 太字 |
| ボタン | 高さ 72、最小の幅 76、余白 6 12 4、絵 28 px の下に名前 16 px（行 20 px）、地 `#141925`、枠 1 px 鋼、角 6 px、ボタンの間 8 |
| 点灯したボタン | 枠 2 px 灯の色 `#ffd89a`、地 `#1d2433` |
| 枚数の印 | ボタンの右 −6、上 −8、最小の幅 30、高さ 24、地は琥珀、字は `#10141d` の等幅 700 の 16 px、角 12 px |
| 速さの右の区切り | 幅 1、高さ 48、`rgba(157,166,186,.4)`、左右 8 px |

色は `Geometry/Tokens.cs` の値で当てます（数値を写さず、トークンを読みます）。

### 置いた仮定（PR の本文に書き、Unity で見たときに確かめてもらう）

| 仮定 | 根拠 |
| --- | --- |
| 戦闘では所持カードのボタンを出さない（枚数の印も出さない）。右は速さ・区切りの線・手記・設定を詰めて並べる | こうだいさんの決定（2026-11-28、#1074） |
| 拠点の所持カードは、支度の卓を開く。支度の卓では点灯したままで、押しても何も起きない | 今の札の箱が卓を開く。絵の 2 枚目も点灯 |
| 拠点と支度の卓の HP とスタミナは、満タンの値（最大 HP と、最大スタミナ）を出す | 潜り直しは満タンから始まる |
| スタミナの分母は、実際の最大値（一時の上乗せで 12 にもなる） | 「/ 10」は通常値の例と読んだ。10 に固定すると、12 のときに数字が合わない |
| 数字が転がる動き（`RollLabel`）は入れない | 部品が Explore のアセンブリにあり、共通の部品から読めない |
| 暦が止まっているあいだは、日数を鋼色にし、注記を日数の右に 16 px で出す。注記は拠点・卓・探索が `CalendarText.Note`（戻るまでの日数つき）、戦闘が短い「暦は止まっている」。長い注記で左の並びが右のボタンの左端 − 28 px を越える画面では、短い注記に替える（`HeaderBand.Fits`） | 今の帯が出している。黙って止めると、規則が見えなくなる。戻るまでの日数が分かるほうが役立つが、ボタンの下に入ると読めない |
| 支度の卓から拠点へ戻るボタンは、「支度の卓」の札の右に小さなチップで置く | 今は見出しの「戻る」だけが戻り口で、見出しがヘッダーに替わる |
| 連戦が 2 以上のときだけ、ターンの札に「連戦 n / m」を足す | 今は左上に連戦の点があり、絵に出口が無い |
| 訓練の刻みのボタンは外す。「あとで」で閉じた訓練の札は、次の休息まで開き直せない | こうだいさんの答え（表示を外し、入口は別に作る）。入口は別の Issue |
| 現在地の幅は詰めた形（画面ごとに、暦・HP・スタミナの位置がずれる）。幅を固定する切り替えを、純 C# の値に 1 つ持つ（既定は詰めた形） | こうだいさんの指示は「左に詰める」。Unity で見て、ずれが気になれば固定に替えられるようにする |

## 検討した代替案

| 案 | 採否 | 却下理由 | 復活条件 |
| --- | --- | --- | --- |
| 描く部品を新しい Unity アセンブリ `Trial.Header` に置き、値・並び・文は `Trial.Bridge`（純 C#）に置く | ✓ | — | — |
| 描く部品を `Trial.Meta` に置き、Explore と Battle から参照させる | ✗ | Battle は Meta を参照しておらず、手記もデリゲートで渡している。参照を足すと、画面どうしを分ける今の作りが崩れる | `Trial.Header` の新設が、Unity の取り込みで詰まるとき |
| 各画面が自前で帯を描き、値だけ共有する | ✗ | 画面ごとに上端が違う、という今の問題が残る | なし |
| 描く部品を `Trial.Kit` に置く | ✗ | Kit は Bridge を参照できない | Kit が Bridge を読めるようになったとき |
| 4 画面を 1 つの単位で通す | ✗ | 触るファイルが多く、確かめるのに時間がかかる | 担当が 1 人しか使えないとき |

## Scope (Touchable Paths)

- 触ってよい:
  - `unity-port/unity-project-kit/Assets/View/Trial/Bridge/Header*.cs`（新規）、`Trial/Bridge/DepartGate.cs`、`Trial/Bridge/DepartTable.cs`
  - `unity-port/unity-project-kit/Assets/View/Trial/Header/**`（新規。`Trial.Header.asmdef` と描く部品）
  - `unity-port/unity-project-kit/Assets/View/Trial/Meta/GateScene.cs`、`Meta/DepartureScene.cs`、`Meta/MetaContracts.cs`、`Meta/Trial.Meta.asmdef`
  - `unity-port/unity-project-kit/Assets/View/Trial/Explore/ExploreScene.Band.cs`、`Explore/ExploreContracts.cs`、`Explore/ExploreScene.cs`、`Explore/Trial.Explore.asmdef`
  - `unity-port/unity-project-kit/Assets/View/Trial/Battle/Hud.cs`、`Battle/Chrome.cs`、`Battle/Stances.cs`、`Battle/BattleScene.cs`、`Battle/Trial.Battle.asmdef`
  - `unity-port/unity-project-kit/Assets/View/Trial/Boot/Main.cs`、`Boot/Trial.Boot.asmdef`
  - `unity-port/Trial.Bridge.Tests/**`、新しいアセンブリに要る `unity-port/*.csproj` と `UnityCorePort.slnx`
  - `.claude/docs/art_document/battle-visual-v1.md`、`unity-port/TRIAL-PORT.md`、`.claude/CLAUDE.md` の試遊版の節
- 触らない:
  - 探索の途中で札を組み直す規則（`unity-port/DungeonCore/`）、戦闘中の所持カードの中身、訓練の新しい入口
  - 戦闘のスタミナの板（#1082）、拠点の層の一覧（#1087）、札の一覧と支度の卓の統合（#1088）
  - `Exploration.Script` の `CalendarBar`・`BodyMarks`（`MapStrata.cs`）。使わなくなっても消さない（ほかの担当が触るアセンブリのため）
  - 旧い `View/Depiction` の画面と `CornerLayout`（`Battle.unity` が使う）、世代制の旧い拠点 `HallScene`
  - `Battle/Flow/**` の `IHud` の口（変えず、`Hud.cs` の中でヘッダーへ流す）

## Steps

単位 1 を先に通し、2・3・4 を別々の worktree で並列に回し、最後に 5 でつなぎます。単位 4 は、片付け（#1093）が main に入り、それをこのブランチに取り込んでから始めます。

1. [x] 単位 1: 純 C# の値・並び・文と、描く部品
   - `Trial/Bridge/Header*.cs` に、配置（高さ 96、余白 24、項目の間 28、ボタンの高さ 72 など）と、帯に出す中身の型（画面の種別、現在地、現在地の札、暦の日数と止まっているか、HP、スタミナ、束の枚数、ボタンごとの有効と点灯、速さの文）を置く
   - 4 つの画面の種別が、同じ配置の値を読むことをテストで固定する。帯の高さと、探索の地図の上端（`StrataLayout.StageTop`）が同じであることも固定する
   - 現在地の幅を固定する切り替えを持つ（既定は詰めた形）
   - `Trial/Header/` に、帯を描く部品を作る。開き先はデリゲート（手記・所持カード・設定・速さ・支度の卓から戻る）で受ける。ボタンの下に出す吹き出しは、帯の下端より下に出す
   - 新しいアセンブリを、`dotnet test` と `npm run unity:check` が読む形で登録する（`TRIAL-PORT.md` のアセンブリの表に従う）
2. [x] 単位 2: 拠点と支度の卓への取り付け
   - 拠点: 見出しと説明、手記・設定・タイトルへのチップ、暦を外し、ヘッダーに替える。「タイトルへ」の経路（`LeaveToTitle`、`OnTitle`）は、設定の画面に同じ働きがあることを確かめてから外す
   - 支度の卓: 見出し（戻る・題・説明）、暦、手記と設定を外し、ヘッダーに替える。束の枚数は、卓で増減するたびに更新する
   - `DepartGate`・`DepartTable` の配置から、ヘッダーに移った領域を外し、テストを直す
3. [x] 単位 3: 探索への取り付け
   - 今の帯（層・暦・体・訓練の刻み・最大スタミナ・3 つのボタン）を、ヘッダーに替える。`SyncMarks` の中の、札の持ち上げの同期は残す
   - 所持カードは、`Screen.CanOpenCards` が偽のあいだ押せない見た目にする
4. [x] 単位 4: 戦闘への取り付け（#1093 を取り込んだあと）
   - 左上の表示・速さ・手記・歯車を、ヘッダーに替える。ターン数と連戦は、現在地の札に出す
   - 構えの「上段」の置き場を、帯の下（y 108）へ動かす。大きい敵の予兆の札と重ならないことを確かめる
   - HP とスタミナは、戦闘のフレームの値を流す。左下のスタミナの板と、足元の HP の板は、そのまま残す（#1080・#1082 が扱う）
5. [x] 単位 5: つなぎ込みと文書
   - `Boot/Main.cs` で、束の枚数・暦の日数・開き先を各画面に渡す
   - `battle-visual-v1.md` にヘッダーの配置と値の節を足し、版を上げる。`TRIAL-PORT.md` の「Where this port departs from the JS」と、アセンブリの表を直す。`.claude/CLAUDE.md` の試遊版の節に 1〜2 文を足す
6. [ ] 全体確認 8 本と、別の目での確認
7. [ ] PR を作り、立ち絵のブランチ（Unity に写してあるブランチ）へ取り込んで `npm run unity:sync` で写し、こうだいさんに見てもらう

## Files

| File | Operation | Notes |
| --- | --- | --- |
| `Trial/Bridge/Header*.cs` | create | 配置・中身の型・文 |
| `Trial/Header/**` | create | アセンブリの定義と、帯を描く部品 |
| `unity-port/Trial.Bridge.Tests/Header*Tests.cs` | create | 4 画面が同じ値を読むこと、帯と地図の上端が同じこと |
| `Trial/Meta/GateScene.cs`、`Meta/DepartureScene.cs`、`Meta/MetaContracts.cs` | edit | 見出し・暦・チップを外し、ヘッダーを置く |
| `Trial/Bridge/DepartGate.cs`、`DepartTable.cs` と、そのテスト | edit | ヘッダーに移った領域を外す |
| `Trial/Explore/ExploreScene.Band.cs`、`ExploreContracts.cs`、`ExploreScene.cs` | edit | 帯をヘッダーに替える |
| `Trial/Battle/Hud.cs`、`Chrome.cs`、`Stances.cs`、`BattleScene.cs` | edit | 左上と右上をヘッダーに替え、構えの上段を動かす |
| `Trial/Boot/Main.cs` | edit | 値と開き先を渡す |
| 各 `*.asmdef`、`unity-port/*.csproj`、`UnityCorePort.slnx` | edit / create | 新しいアセンブリへの参照 |
| `battle-visual-v1.md`、`TRIAL-PORT.md`、`.claude/CLAUDE.md` | edit | 配置と値、JS と変えた点 |

## Verification

- [x] `dotnet test unity-port/UnityCorePort.slnx -p:UseSharedCompilation=false --disable-build-servers` が通る
- [ ] `npm run unity:check -- --allow-leftovers --allow-unbuilt` が exit 0
- [x] `npm run build`、`npm run lint`、`npm run test:run -- --maxWorkers=4`、`npm run sources -- --check`、`npm run md:tables -- --check`、`npm run issues:test` が exit 0
- [x] ヘッダーの文字が 16 px 以上で、琥珀はプレイヤーの側（スタミナと束の枚数）だけに使っている
- [x] 4 つの画面のボタンが同じ順に並び、画面ごとの状態が決まりどおりであることを、テスト（`HeaderBandTests.EveryScreen_StandsItsButtonsInOneOrder_WithItsOwnStates`）が確かめている。所持カードの吹き出しが探索だけで C キーを書くことも、テスト（`TheOwnedCardsBubble_NamesTheCKey_OnTheExplorationOnly`）が確かめている
- [x] `battle-visual-v1.md` にヘッダーの配置と値が入り、`TRIAL-PORT.md` に JS と変えた点が入っている
- [ ] 別の目での確認で、直さないと通せない指摘が 0 件
- [ ] こうだいさんが Unity で見る点: 4 つの画面で同じ場所に同じものがあるか / 暦の残り日数がひと目で読めるか / HP とスタミナが戦闘中でも目に入るか / 札・手記・設定を迷わず押せるか / 戦闘でヘッダーが敵の姿や予兆のじゃまになっていないか / 画面ごとに暦・HP・スタミナの位置がずれるのが気になるか

### 危ない点

- 戦闘のヘッダーは HUD の層に置くので、持ち上げた札と結果の札が、ヘッダーの上に重なります。
- 枚数の印は、ボタンの外へ右 6 px・上 8 px はみ出します。
- 敵の上段の構え（y 108、高さ 52）と、大きい敵の予兆の札の上端（y 162）の隙間は 2 px です。
- 拠点は、暦がヘッダーに移ると y 106〜186 が空きます（拠点の作り直しは #1087）。
- Unity の NUnit 3.5 と netstandard2.1 に無い API を使わない（純 C# を足したら `npm run unity:check` を回す）。`ViewRulesBoundaryTests` に当たる名前（`Field`・`Cards`・`Chain`・`Constants` など）の変数とクラスを作らない。
- 新しいアセンブリは、`npm run unity:sync` で Unity に写してから反映されます。写すのはメインのチャットです。

## Worklog

<!-- task-tracker が、終わりに乖離の振り返り 3 行を書きます。 -->
