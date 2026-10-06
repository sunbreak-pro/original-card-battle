---
from: <自分の .session-name>
date: YYYY-MM-DD
kind: <bug | feature | task | conflict | human>
area: <area ラベルの案。複数可>
lane: <宛先レーンの案。決められなければ空>
prio: <1〜4 の案。main が決め直す>
related: <関連する Issue / PR の番号。無ければ空>
---

# <Issue の題の案。conflict なら「正本の食い違い: 〜」で始める>

## 何が起きているか

<1〜3 行。根拠を file:line か file:§ で添える。推測なら「未確認」と書く>

## どちらに従って進めたか

<conflict のときだけ書く。自分のレーンで採った側と、その理由を 1 行>

## 直し方の案

<案が複数あれば並べ、直し方ごとに変わる結果を書く。どのレーンが書くかの案も添える>

## DoD の案

<機械で確かめられる形。npm run build exit 0 / dotnet test 緑 / 実測値 など>
