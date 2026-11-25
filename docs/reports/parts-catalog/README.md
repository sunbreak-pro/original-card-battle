# 試遊版の画面と部品の一覧

Unity の試遊版（`Assets/Scenes/Trial.unity`）の全画面を撮り、部品に番号を付けて並べた一覧です。こうだいさんが、イメージと違う部品に印と一言を付けます（#1075）。

発行先は Artifact「試遊版 画面と部品の一覧」です。URL は https://claude.ai/artifact/2hazGa45JAjsZn3T3Zh4eM で、更新するときは同じ URL へ発行します。

## ファイル

- `parts-catalog.html` は、一覧のページです。`catalog.json` を読み、印はページのデータベースの `marks` に書きます。
- `build.py` は、画面と部品の定義です。`catalog.json` と、絵の JPEG（`shots/`）を作ります。
- `catalog.json` は、`build.py` が作る生成物です。手で直しません。
- `capture.sh` は、Unity Editor を外から操作して画面を撮る道具です。
- `sheet.py` は、撮った絵を小さく並べて確かめる道具です。

絵（PNG と JPEG）は、リポジトリに入れません。撮り直すたびに数 MB ずつ増えるためです。発行済みの絵は Artifact が持ちます。

## 作り直す手順

1. Unity Editor で `Assets/Scenes/Trial.unity` を開きます。こうだいさんが同じ Editor を使っていないことを、先に確かめます。別のチャットが使っていることもあります。
2. 保存フォルダ（`%USERPROFILE%/AppData/LocalLow/DefaultCompany/RPG-by-card/trial/`）の控えを取ります。
3. `PARTS_SHOTS=<絵を置くフォルダ>` を決めて、`capture.sh` で画面を順に撮ります。名前は `build.py` の `shot(...)` の 2 つ目の値に合わせます。
4. `python -I build.py <PNG のフォルダ> <出力先>` を回します。`catalog.json` と `shots/*.jpg` ができます。
5. `parts-catalog.html` を、`catalog.json` と `shots/*.jpg` を添えて、同じ URL へ発行します。能力は `db` と `user` です。
6. Editor の Play を止め、シーンを保存せずに開き直し、保存フォルダを控えから戻します。

## 撮るときに分かったこと（2026-11-25）

- Editor が前面にないと、Play の進みが遅くなり、入力が遅れて届きます。`capture.sh` は、操作のたびに Editor を前面に出します。
- 座標は、舞台の px（1920×1080、左上が原点）です。Game ビューを 1920×1080 にしておきます。
- 層の最初の部屋は戦闘です。出立すると、地図より先に戦闘が始まります。
- 攻撃の札は、敵の上で離すと出ます。それ以外の札は、場のどこで離しても出ます。手札の位置は、中心が x = 960、間隔が約 168 px、y = 1000 です。
- 戦闘を自動で進めるには、Play の前に `TrialBoot.autobattle` を true にします。シーンは保存しません。戦闘の画面は出ないので、戦闘の画面は手で進める回で撮ります。
- 部屋へ進むには、部屋を押してから Enter を押します。踏んだ部屋より上へは戻れません。
- 手記は J、所持カードは C、設定は Esc で開きます。

## 印を読む

`ArtifactData` の `list` で、`marks` を読みます。文書の ID は、`build.py` の部品の ID です。`verdict` が `ok` なら「イメージどおり」、`off` なら「違う」で、`note` が一言です。
