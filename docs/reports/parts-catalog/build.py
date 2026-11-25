"""Builds catalog.json and the JPEG copies of the captures for the parts catalogue.
Run:  python -I build.py <shots dir> <out dir>
Box coordinates are stage px (1920x1080, top-left origin), the same space the trial's layout code uses."""
import json
import sys
from pathlib import Path
from PIL import Image

SRC, OUT = Path(sys.argv[1]), Path(sys.argv[2])
T = "View/Trial/"


def part(pid, name, desc, file, boxes=(), shared=None):
    return {
        "id": pid, "name": name, "desc": desc, "file": file, "shared": shared,
        "boxes": [{"shot": b[0], "x": b[1], "y": b[2], "w": b[3], "h": b[4]} for b in boxes],
    }


def shot(key, name, label):
    return {"key": key, "src": name, "label": label}


SCREENS = [
    {
        "id": "title", "name": "タイトル",
        "route": "Play を押すと最初に出ます。",
        "shots": [shot("a", "title-01", "保存がある状態のタイトル")],
        "parts": [
            part("title-logo", "題字", "ゲームの名前と、その下の線です。", T + "Meta/TitleScene.cs", [("a", 150, 225, 510, 80)]),
            part("title-menu", "メニュー", "はじめから・つづきから・設定の 3 行です。選んでいる行の左に印が付きます。", T + "Meta/TitleScene.cs", [("a", 150, 565, 270, 210)]),
            part("title-copy", "縦書きの文", "右側に縦で出る 2 行の文です。", T + "Meta/TitleScene.cs", [("a", 1570, 95, 170, 800)]),
            part("title-figure", "人影と足元の光", "中央に立つ人影です。", T + "Kit/Ds/Ocb.Figures.cs", [("a", 860, 640, 200, 200)], "拠点・探索と同じ部品"),
            part("title-hint", "操作の案内", "左下の、キーの案内の行です。", T + "Meta/TitleScene.cs", [("a", 150, 1000, 200, 32)]),
            part("title-backdrop", "背景", "星と、浮いている岩の背景です。", T + "Meta/TitleScene.cs"),
        ],
    },
    {
        "id": "confirm", "name": "確認の札",
        "route": "タイトルで、保存があるときに「はじめから」を選ぶと出ます。探索では「この生を終える」を押すと出ます。",
        "shots": [
            shot("a", "confirm-01-new-lineage", "タイトルの「はじめから」の確認"),
            shot("b", "confirm-02-end-life", "探索の「この生を終える」の確認"),
        ],
        "parts": [
            part("confirm-card", "確認の札", "題と説明の文を持つ、中央の札です。うしろの画面は暗くなります。", T + "Kit/Common/Stage.cs", [("a", 580, 424, 760, 232)], "全画面で同じ部品"),
            part("confirm-buttons", "やめる・進めるのボタン", "右下に並ぶ 2 つのボタンです。進めるほうに色の枠が付きます。", T + "Kit/Common/Stage.cs", [("a", 982, 569, 321, 58)], "全画面で同じ部品"),
        ],
    },
    {
        "id": "gate", "name": "拠点（降り口の前）",
        "route": "タイトルで「はじめから」を選ぶと出ます。探索が終わったあとも、ここへ戻ります。",
        "shots": [
            shot("a", "gate-01-first", "最初の探索の前"),
            shot("b", "gate-02-second", "2 回目の探索の前（生きて戻ったあと）"),
            shot("c", "gate-03-hold-0800ms", "「降りる」を押し続けている途中（0.8 秒）"),
            shot("d", "gate-04-after-death", "3 回目の探索の前（倒れたあと）"),
        ],
        "parts": [
            part("gate-head", "見出しと説明", "左上の「拠点」と、その右の 1 行の説明です。", T + "Meta/GateScene.cs", [("a", 48, 28, 510, 56)]),
            part("gate-buttons", "手記・設定・タイトルへ", "右上に並ぶ 3 つのボタンです。", T + "Meta/GateScene.cs", [("a", 1547, 24, 325, 56)], "画面ごとに別の作り"),
            part("gate-calendar", "崩壊までの日数と棒", "降り口の上に出る、残りの日数と 10 区切りの棒です。", T + "Meta/GateScene.cs", [("a", 760, 105, 520, 82)], "画面ごとに別の作り"),
            part("gate-name", "次の探索と名前", "「次の探索 N 回目」と探索者の名前、その下の 1 行です。", T + "Meta/GateScene.cs", [("a", 48, 215, 420, 170)]),
            part("gate-record", "これまでの記録の 3 行", "2 回目からは、これまでの探索・いちばん深く・前の探索の 3 行が出ます。", T + "Bridge/DepartGate.cs", [("b", 48, 350, 420, 150)]),
            part("gate-figure", "探索者の人影", "降り口の左に立つ人影と、足元の光です。", T + "Kit/Ds/Ocb.Figures.cs", [("a", 530, 440, 190, 360)], "タイトル・探索と同じ部品"),
            part("gate-arch", "降り口", "中央の、下へ続く階段のある入口です。線画の仮置きです。", T + "Meta/GateScene.cs", [("a", 744, 198, 552, 592)]),
            part("gate-layers", "層の一覧", "右側に縦に並ぶ 7 つの層です。前の探索で着いた層に印が付きます。", T + "Meta/GateScene.cs", [("a", 1697, 215, 180, 590)]),
            part("gate-deck", "束の箱", "束の枚数と「組み直す」です。押すと支度の卓が開きます。", T + "Meta/GateScene.cs", [("a", 48, 846, 418, 210)]),
            part("gate-tools", "道具の箱", "道具の 3 つの枠です。", T + "Meta/GateScene.cs", [("a", 482, 846, 418, 210)]),
            part("gate-consumables", "消耗品の箱", "消耗品の 3 つの枠です。", T + "Meta/GateScene.cs", [("a", 916, 846, 418, 210)]),
            part("gate-descend", "「降りる」のボタン", "Enter を 1 秒押し続けると出立します。押しているあいだ、下の棒が伸びます。", T + "Meta/GateScene.cs", [("a", 1350, 846, 522, 210), ("c", 1350, 846, 522, 210)]),
        ],
    },
    {
        "id": "table", "name": "支度の卓",
        "route": "拠点で、束・道具・消耗品の箱のどれかを押すと開きます。",
        "shots": [
            shot("a", "table-01", "開いた直後"),
            shot("b", "table-02-packed", "道具と消耗品を 1 つずつ詰めたあと"),
        ],
        "parts": [
            part("table-head", "見出しと「拠点へ」", "左上の戻るボタンと「出立の支度」、その右の説明です。", T + "Meta/DepartureScene.cs", [("a", 48, 22, 720, 58)]),
            part("table-top", "暦・手記・設定", "右上の、残りの日数と 2 つのボタンです。", T + "Meta/DepartureScene.cs", [("a", 1374, 22, 498, 56)], "画面ごとに別の作り"),
            part("table-filters", "絞り込みのボタン", "札の種類とコストで絞るボタンの列です。", T + "Meta/DepartureScene.cs", [("a", 48, 106, 1090, 46)]),
            part("table-grid", "持ち札の格子", "札を 6 列 3 段で並べます。持っていない札は暗く出ます。", T + "Meta/DepartureScene.cs", [("a", 48, 164, 1126, 628)]),
            part("table-card", "小さな札の面", "格子の 1 枚ぶんです。コスト・種類・間合い・名前・数値と、枚数の増減が入ります。", T + "Meta/MetaCardFace.cs", [("a", 48, 164, 178, 200)], "札の面は 3 通りの作り"),
            part("table-pager", "ページ送りと操作の案内", "格子の下の、前後のページと、キーの案内です。", T + "Meta/DepartureScene.cs", [("a", 48, 808, 1126, 40)]),
            part("table-shelf", "品の棚", "道具 8 つと消耗品 5 つの丸いアイコンです。押すと荷に入ります。", T + "Meta/DepartureSuppliesView.cs", [("a", 48, 870, 1126, 182)]),
            part("table-bundle", "束の厚みと枚数", "右上の、重ねた札の絵と枚数、20〜40 枚の棒です。", T + "Meta/DepartureScene.cs", [("a", 1231, 135, 616, 165)]),
            part("table-slots", "道具と消耗品の枠", "荷に詰めた道具と消耗品の、3 つずつの枠です。", T + "Meta/DepartureSuppliesView.cs", [("a", 1231, 322, 616, 202), ("b", 1231, 322, 616, 202)]),
            part("table-readout", "選んだ札の説明", "格子で選んでいる札の、コスト・間合い・効果を文で出します。", T + "Meta/DepartureScene.cs", [("a", 1231, 540, 616, 286)]),
            part("table-presets", "最初期の束・ランダム・空にする", "束をまとめて入れ替える 3 つのボタンです。", T + "Meta/DepartureScene.cs", [("a", 1231, 842, 616, 48)]),
            part("table-depart", "出立できるかの行と「出立する」", "出立できるかを 1 行で知らせ、その下に大きなボタンを置きます。", T + "Meta/DepartureScene.cs", [("a", 1231, 905, 616, 126)]),
        ],
    },
    {
        "id": "map", "name": "探索の地図（第 1 層）",
        "route": "拠点で「降りる」を押し続けるか、支度の卓で「出立する」を押します。最初の部屋は戦闘なので、1 戦のあとに地図が出ます。",
        "note": "第 1 層だけ、部屋を空洞、道を通路として描く形です。第 2 層から先の地図は、まだ撮れていません。",
        "shots": [
            shot("a", "explore-01-layer1-map", "7 行の地図"),
            shot("b", "explore-08-layer1-map-9rows", "9 行の地図（別の探索）"),
            shot("c", "explore-03-node-hover", "部屋にカーソルを置いたとき"),
            shot("d", "explore-06-node-selected", "進む先の部屋を選んだとき"),
            shot("e", "explore-11-boss-hover", "階層ボスの部屋を選んだとき"),
        ],
        "parts": [
            part("map-band-layer", "上の帯: 層の名前", "左上の、層の番号と名前です。", T + "Explore/ExploreScene.Band.cs", [("a", 24, 28, 320, 44)], "画面ごとに別の作り"),
            part("map-band-calendar", "上の帯: 暦の棒と残りの日数", "崩壊までの棒と「残り N 日」です。", T + "Explore/ExploreScene.Band.cs", [("a", 410, 22, 565, 58)], "画面ごとに別の作り"),
            part("map-band-body", "上の帯: HP・スタミナ・訓練の刻み", "HP の数字と棒、スタミナの丸と数字、その下の小さな 2 行です。", T + "Explore/ExploreScene.Band.cs", [("a", 1045, 18, 440, 64)], "画面ごとに別の作り"),
            part("map-band-buttons", "上の帯: 手記・所持カード・設定", "右上の 3 つのボタンです。", T + "Explore/ExploreScene.Band.cs", [("a", 1530, 12, 378, 72)], "画面ごとに別の作り"),
            part("map-depth", "深さの列", "左に縦に並ぶ 7 つの層です。今いる層に色が付きます。", T + "Explore/ExploreScene.Map.cs", [("a", 22, 128, 130, 676)]),
            part("map-chamber", "部屋（空洞）とラベル", "部屋 1 つぶんです。種類のアイコンと、右の名前のラベルを持ちます。行が増えると小さくなります。", T + "Explore/ExploreNodeView.cs", [("a", 590, 172, 200, 74), ("b", 590, 172, 200, 74)]),
            part("map-tunnel", "通路", "部屋をつなぐ道です。進める道は明るく、点線が付きます。", T + "Explore/ExploreEdgeView.cs", [("a", 300, 240, 720, 60)]),
            part("map-walker", "今いる場所の印", "今いる部屋の上の人の印と、四隅の枠です。", T + "Explore/ExploreScene.Map.cs", [("a", 575, 150, 155, 112)]),
            part("map-boss", "階層ボスの部屋", "いちばん下の、二重の枠の部屋です。", T + "Explore/ExploreNodeView.cs", [("a", 566, 838, 240, 100)]),
            part("map-detail", "詳細の窓", "選んだ部屋の絵・種類・脅威度・敵を出す、右の窓です。進めるときは「進む」のボタンが出ます。", T + "Explore/ExploreScene.Detail.cs", [("a", 1305, 113, 590, 602), ("d", 1305, 113, 590, 602)]),
            part("map-carried", "持ち物", "右下の、道具の 1 行と消耗品の 3 つの枠です。", T + "Explore/ExploreScene.Carried.cs", [("a", 1305, 729, 590, 230)]),
            part("map-endlife", "「この生を終える」", "左下のボタンです。", T + "Explore/ExploreScene.cs", [("a", 24, 980, 288, 68)]),
            part("map-hint", "案内の行とキーの案内", "下の 1 行の説明と、右下のキーの案内です。", T + "Explore/ExploreScene.cs", [("a", 336, 1000, 680, 32), ("a", 1800, 1000, 100, 32)]),
            part("map-backdrop", "背景", "層ごとの色の背景です。地図の下に薄く見えます。", T + "Explore/ExploreBackdrop.cs"),
        ],
    },
    {
        "id": "cards", "name": "探索で出る札",
        "route": "イベントの部屋に入る、戦闘に勝つ、探索が終わる、のそれぞれで地図の上に出ます。",
        "shots": [
            shot("a", "explore-04-event-choice", "イベントの選択"),
            shot("b", "explore-05-event-result", "イベントの結果"),
            shot("c", "explore-02-battle-result-card", "戦闘に勝ったあと"),
            shot("d", "explore-07-end-card", "探索の終わり（生きて戻る）"),
            shot("e", "explore-12-end-card-fallen", "探索の終わり（倒れた）"),
        ],
        "parts": [
            part("card-event", "イベントの選択の札", "イベントの名前と説明、受けたときの変化を出します。", T + "Explore/ExploreScene.Plates.cs", [("a", 150, 303, 1005, 480)]),
            part("card-event-buttons", "「受ける」「受けない」", "選ぶと起きる変化を、ボタンの中に書きます。", T + "Explore/ExploreScene.Plates.cs", [("a", 195, 597, 915, 150)]),
            part("card-event-result", "イベントの結果の札", "変わった値と「地図へ戻る」です。", T + "Explore/ExploreScene.Results.cs", [("b", 270, 354, 765, 369)]),
            part("card-battle-result", "戦闘のあとの札", "「戦いに勝った」と、HP とスタミナの今の値です。", T + "Explore/ExploreScene.Results.cs", [("c", 62, 208, 1180, 665)]),
            part("card-result-left", "習熟・手記・拾った物の欄", "左の、上がった習熟・書き足した敵・拾った物の 3 つの欄です。", T + "Explore/ExploreScene.Results.cs", [("c", 107, 335, 290, 310)]),
            part("card-reward", "覚える札の選択", "1 枚を選んで覚える札の並びです。", T + "Explore/ExploreCardFace.cs", [("c", 588, 402, 465, 345)], "札の面は 3 通りの作り"),
            part("card-result-buttons", "「受け取らない」「この札を覚える」", "右下の 2 つのボタンです。", T + "Explore/ExploreScene.Results.cs", [("c", 738, 782, 460, 58)]),
            part("card-end", "探索の終わりの札", "終わり方の名前と、残りの日数・HP、「新しい生で潜り直す」です。", T + "Explore/ExploreScene.Plates.cs", [("d", 165, 315, 975, 453), ("e", 195, 372, 1178, 552)]),
        ],
    },
    {
        "id": "battle", "name": "戦闘",
        "route": "地図で戦闘の部屋へ進むと始まります。",
        "note": "敵が 1 体の通常の戦闘です。敵が複数の戦闘、精鋭、負けたときの画面は、まだ撮れていません。",
        "shots": [
            shot("a", "battle-01-turn-start", "1 ターン目の初め"),
            shot("b", "battle-02-card-hover", "手札にカーソルを置いたとき"),
            shot("c", "battle-06-drag-on-enemy", "攻撃の札を敵に重ねたとき"),
            shot("d", "battle-04-after-attack", "攻撃の札を敵の外で離したとき"),
            shot("e", "battle-07-enemy-turn", "ターン終了を押した直後"),
            shot("f", "battle-03-piles", "山札の一覧を開いたとき"),
            shot("g", "battle-08-result", "勝ったとき"),
        ],
        "parts": [
            part("battle-corner", "左上の表示", "経った日数と層の番号です。", T + "Battle/Chrome.cs", [("a", 24, 20, 162, 44)], "画面ごとに別の作り"),
            part("battle-top", "設定・速さ・手記", "右上の 3 つのボタンです。", T + "Battle/Chrome.cs", [("a", 1640, 20, 256, 56)], "画面ごとに別の作り"),
            part("battle-player", "主人公の人影", "左に立つ影絵です。", T + "Battle/FieldView.cs", [("a", 575, 345, 140, 230)]),
            part("battle-enemy", "敵の人影", "右に立つ影絵です。", T + "Battle/FieldView.cs", [("a", 1405, 340, 175, 235)]),
            part("battle-omen", "予兆の札", "敵の頭の上に出る、次の行動の札です。", T + "Battle/Omen.cs", [("a", 1410, 232, 200, 98)]),
            part("battle-floor", "床のマスと間合い", "横 1 列のマスと、あいだの間合いの数字です。", T + "Battle/Floor.cs", [("a", 304, 560, 1312, 40)]),
            part("battle-status-player", "主人公の HP と Guard", "足元の下の数値の板です。", T + "Battle/Status.cs", [("a", 480, 614, 300, 78)], "画面ごとに別の作り"),
            part("battle-status-enemy", "敵の HP と Guard", "敵の足元の下の数値の板です。", T + "Battle/Status.cs", [("a", 1405, 614, 210, 78)]),
            part("battle-stances", "構えの枠", "左右の端の、点線の枠です。", T + "Battle/Stances.cs", [("a", 24, 470, 250, 52), ("a", 1646, 470, 250, 52)]),
            part("battle-prompt", "案内の行", "中央の 1 行の案内です。出せないときは、理由の行に変わります。", T + "Battle/HandPanels.cs", [("a", 660, 745, 600, 30), ("d", 765, 837, 400, 45)]),
            part("battle-stamina", "スタミナ", "左下の、数字と丸の並びです。", T + "Battle/Stamina.cs", [("a", 32, 846, 236, 84)], "画面ごとに別の作り"),
            part("battle-piles", "山札・捨札・除外", "左下の 3 つの枠です。押すと一覧が左から開きます。", T + "Battle/Piles.cs", [("a", 32, 936, 236, 96), ("f", 0, 0, 575, 560)]),
            part("battle-hand", "手札", "下に扇の形で並ぶ札です。", T + "Battle/HandCard.cs", [("a", 445, 895, 1030, 185)], "札の面は 3 通りの作り"),
            part("battle-card-detail", "札の拡大と説明", "カーソルを置いた札が持ち上がり、使える間合いの説明が横に出ます。", T + "Battle/HandPanels.cs", [("b", 825, 704, 610, 360)]),
            part("battle-target", "敵に重ねたときの枠と数字", "攻撃の札を敵に重ねると、与える数字が札の上に出ます。", T + "Battle/Hand.cs", [("c", 1410, 273, 240, 360)]),
            part("battle-tally", "出した札の種類の数", "右下の 4 つの小さなアイコンです。", T + "Battle/Bottom.cs", [("a", 1718, 897, 178, 40)]),
            part("battle-guard-preview", "ターン終了で得る Guard", "ターン終了の左の、点線の小さな板です。", T + "Battle/Bottom.cs", [("a", 1560, 984, 98, 56)]),
            part("battle-endturn", "「ターン終了」", "右下の大きなボタンです。", T + "Battle/Bottom.cs", [("a", 1672, 976, 224, 72)]),
            part("battle-result", "勝ちの紙", "勝ったときに中央に出る紙です。習熟が上がった札を並べます。", T + "Battle/Chrome.cs", [("g", 720, 205, 720, 670)]),
            part("battle-backdrop", "背景", "層ごとの背景です。第 1 層は森です。", T + "Battle/Backdrop/Script/BackdropScenery.cs"),
        ],
    },
    {
        "id": "journal", "name": "手記",
        "route": "拠点・支度の卓・探索・戦闘のどこでも、J キーか手記のボタンで右から開きます。",
        "shots": [
            shot("a", "journal-01-gate", "拠点で開いたとき（これまでの探索）"),
            shot("b", "journal-07-past-explorations", "2 回目の拠点で開いたとき"),
            shot("c", "journal-04-enemy", "探索で開いたとき（敵のページ）"),
            shot("d", "journal-03-explore-dungeon", "ダンジョンのページ"),
            shot("e", "journal-05-terms", "用語のページ"),
            shot("f", "journal-06-memo", "メモのページ"),
            shot("g", "journal-02-battle", "戦闘で開いたとき"),
        ],
        "parts": [
            part("journal-drawer", "引き出しの紙", "右から出る紙の地です。", T + "Meta/JournalDrawer.cs", [("a", 1344, 0, 576, 1080)], "全画面で同じ部品"),
            part("journal-head", "見出しと「閉じる」", "上の「手記」と、右の閉じるボタンです。", T + "Meta/JournalDrawer.cs", [("a", 1372, 22, 520, 50)]),
            part("journal-tabs", "タブ", "敵・ダンジョン・メモ・用語の 4 つです。使えないタブは薄く出ます。", T + "Meta/JournalDrawer.cs", [("a", 1372, 86, 520, 46)]),
            part("journal-past", "これまでの探索のページ", "探索ごとの終わり方と、持ち越しの決まりの行です。", T + "Script/JournalBookOf.cs", [("a", 1372, 150, 520, 370), ("b", 1372, 150, 520, 400)]),
            part("journal-enemy", "敵のページ", "名前・HP・得意な間合いと、間合いごとの行動です。分かっていない欄は「?」です。", T + "Script/JournalBookOf.cs", [("c", 1372, 150, 520, 870)]),
            part("journal-dungeon", "ダンジョンのページ", "層ごとの領域・日数・部屋の数です。", T + "Script/JournalBookOf.cs", [("d", 1372, 150, 520, 560)]),
            part("journal-terms", "用語のページ", "用語と状態の一覧です。", T + "Script/JournalBookOf.cs", [("e", 1372, 150, 520, 870)]),
            part("journal-memo", "メモのページ", "自分で書く欄です。", T + "Meta/JournalDrawer.cs", [("f", 1372, 150, 520, 870)]),
            part("journal-foot", "下の案内", "ページ送りと閉じ方の案内です。", T + "Meta/JournalDrawer.cs", [("a", 1372, 1036, 520, 36)]),
        ],
    },
    {
        "id": "owned", "name": "所持カード",
        "route": "探索で、C キーか「所持カード」のボタンで開きます。",
        "shots": [shot("a", "owned-01", "12 種を持っているとき")],
        "parts": [
            part("owned-head", "見出しと操作の案内", "上の「所持カード」と種類の数、右の操作の案内と閉じるボタンです。", T + "Meta/OwnedCards.cs", [("a", 60, 55, 1830, 55)]),
            part("owned-grid", "札の格子", "持っている札を 6 列で並べます。札の下に習熟の丸が付きます。", T + "Meta/MetaCardFace.cs", [("a", 70, 125, 1095, 545)], "札の面は 3 通りの作り"),
            part("owned-detail", "選んだ札の説明", "右の、札の拡大・習熟の段・次の段までの刻み・説明の文です。", T + "Meta/OwnedCards.cs", [("a", 1215, 135, 660, 900)]),
        ],
    },
    {
        "id": "settings", "name": "設定・セーブ",
        "route": "タイトルの「設定」、または各画面の設定のボタンか Esc で開きます。",
        "shots": [
            shot("a", "settings-01-title", "タイトルで開いたとき"),
            shot("b", "settings-02-explore", "探索で開いたとき（保存のボタンが付きます）"),
        ],
        "parts": [
            part("settings-panel", "設定の窓", "中央の窓と、右上の閉じるボタンです。", T + "Meta/SettingsOverlay.cs", [("a", 540, 163, 840, 755)], "全画面で同じ部品"),
            part("settings-speed", "戦闘の速さ", "3 段階のボタンです。", T + "Meta/SettingsOverlay.cs", [("a", 577, 254, 766, 64)]),
            part("settings-short", "演出短縮", "入れる・切るのボタンと、下の 1 行の説明です。", T + "Meta/SettingsOverlay.cs", [("a", 577, 330, 766, 90)]),
            part("settings-volume", "音の大きさ", "全体・効果音・環境音のつまみと、消音のボタンです。", T + "Meta/SettingsOverlay.cs", [("a", 577, 445, 766, 320)]),
            part("settings-buttons", "下のボタンと案内", "閉じる、保存して閉じる、キーの案内です。", T + "Meta/SettingsOverlay.cs", [("a", 577, 790, 766, 110)]),
        ],
    },
    {
        "id": "ending", "name": "終わりの画面",
        "route": "探索の終わりの札で Enter を押すと出ます。ここで Enter を押すと拠点へ戻ります。",
        "note": "暦が尽きたときと、踏破したときの画面は、まだ撮れていません。",
        "shots": [
            shot("a", "ending-01-survived", "生きて戻ったとき"),
            shot("b", "ending-02-fallen", "倒れたとき"),
        ],
        "parts": [
            part("ending-layer", "層の名前の帯", "上の中央の、終わった層の名前です。", T + "Meta/EndingScene.cs", [("a", 780, 62, 360, 40)]),
            part("ending-paper", "終わりの紙", "終わり方の名前、何回目の探索か、持ち越すものの行です。", T + "Meta/EndingScene.cs", [("a", 441, 258, 1038, 564)]),
            part("ending-button", "次の探索の行と「拠点へ」", "紙の下の、次の探索の回数と、戻るボタンです。", T + "Meta/EndingScene.cs", [("a", 441, 730, 1038, 80)]),
            part("ending-backdrop", "背景", "終わった層の背景です。", T + "Meta/EndingScene.cs"),
        ],
    },
    {
        "id": "missing", "name": "まだ撮れていない画面",
        "note": "今回の撮影では届かなかった画面です。次の撮影で足します。",
        "shots": [{"key": "a", "src": None, "label": "第 2〜6 層の地図、第 7 層（王の間）、層と層のあいだの休憩、情報収集の選択、休息の訓練、痕跡、精鋭と階層ボスの戦闘、敵が複数の戦闘、負けたとき、暦が尽きたとき"}],
        "parts": [],
    },
]


def main():
    (OUT / "shots").mkdir(parents=True, exist_ok=True)
    total = 0
    for s in SCREENS:
        for sh in s["shots"]:
            src = sh.pop("src")
            if not src:
                sh["file"] = None
                continue
            im = Image.open(SRC / (src + ".png")).convert("RGB").resize((1600, 900), Image.LANCZOS)
            dest = OUT / "shots" / (src + ".jpg")
            im.save(dest, "JPEG", quality=84, optimize=True)
            total += dest.stat().st_size
            sh["file"] = "shots/" + src + ".jpg"
    ids = [p["id"] for s in SCREENS for p in s["parts"]]
    assert len(ids) == len(set(ids)), "duplicate part id"
    catalog = {
        "asOf": "2026-11-25 の 19 時台に、Unity の試遊版（Assets/Scenes/Trial.unity、main の 16f41fca の C#）を動かして撮りました。",
        "screens": SCREENS,
    }
    (OUT / "catalog.json").write_text(json.dumps(catalog, ensure_ascii=False, indent=1), encoding="utf-8")
    print("screens", len(SCREENS), "parts", len(ids), "images", sum(1 for s in SCREENS for sh in s["shots"] if sh["file"]), "bytes", total)


main()
