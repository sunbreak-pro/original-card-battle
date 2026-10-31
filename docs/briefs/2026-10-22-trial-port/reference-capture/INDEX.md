# Browser game reference captures (1920×1080)

445 images in `shots/`.

Reference screenshots of the browser prototype (`proto/prototypes/browser-game/dist/dev.html`) for screen-by-screen comparison with the Unity port.

- **Viewport:** Chromium headless shell 1243, viewport 1920×1080, deviceScaleFactor 1 (the `.ocbg-stage` is 1:1). Fonts: Google Fonts over the network; every still waits for `document.fonts.ready`.
- **Build used:** a copy of `dist/` in `ref/game/`, identical except one added statement in `game.js` (`window.__scene = <mounted scene>`), used only to read battle geometry and the scene state (where a card is, whether the scene waits for input). All input is real `page.mouse` / `page.keyboard`. `window.__ocb` (`?debug`) is read for node ids, event kinds and the fight state.
- **Frame sequences** (`…-NNNNms.png`) come from the DevTools screencast (~40 fps PNG). Each file is the frame on screen at the target time; the index line gives the target and the real time of the frame. "time from X" says what 0 ms is.
- **Lineage:** seed pinned to 789591033 (heirs ケイト, ロタ, ラウル, スヴェン). Deck: 試作デッキ + 水の構え + 岩の構え (22); the third heir adds 集中 and 石礫. Battle speed setting: default ×1.25 (the speed toggle shots cycle it back to ×1.25).
- **Scripts:** `cap-meta.mjs`, `cap-battle1.mjs`, `cap-explore1.mjs`, `cap-explore2.mjs`, `cap-life2.mjs`, `cap-life3.mjs`, `cap-life3b.mjs` (helpers `lib.mjs`, `battlelib.mjs`, `explorelib.mjs`; this file `build-index.mjs`). Each section starts from the checkpoint (`ckpt-*.json`, a localStorage dump) the previous one wrote.

## Title, 継承の間 (first heir), デッキを組む, 出立の支度, set out (layer-1 map, encounter banner, first battle opening)

Script: `cap-meta.mjs`

| file | screen | state | how it was reached |
| --- | --- | --- | --- |
| 101-title-intro-0000ms.png | title | intro animation, fresh storage (target 0 ms, frame shown at -12 ms) | load dev.html?debug; time from the title root attaching (after fonts.ready) |
| 101-title-intro-0300ms.png | title | intro animation, fresh storage (target 300 ms, frame shown at 294 ms) | load dev.html?debug; time from the title root attaching (after fonts.ready) |
| 101-title-intro-0800ms.png | title | intro animation, fresh storage (target 800 ms, frame shown at 799 ms) | load dev.html?debug; time from the title root attaching (after fonts.ready) |
| 101-title-intro-1500ms.png | title | intro animation, fresh storage (target 1500 ms, frame shown at 1493 ms) | load dev.html?debug; time from the title root attaching (after fonts.ready) |
| 101-title-intro-2500ms.png | title | intro animation, fresh storage (target 2500 ms, frame shown at 2471 ms) | load dev.html?debug; time from the title root attaching (after fonts.ready) |
| 101-title-intro-4000ms.png | title | intro animation, fresh storage (target 4000 ms, frame shown at 3975 ms) | load dev.html?debug; time from the title root attaching (after fonts.ready) |
| 102-title-idle.png | title | idle, はじめから focused (no save) | fresh load, 5 s |
| 103-title-hover-new.png | title | pointer over はじめから | mouse.move to item 0 |
| 104-title-hover-continue-disabled.png | title | pointer over つづきから (disabled, 保存された生は無い) | mouse.move to item 1 |
| 105-title-hover-settings.png | title | pointer over 設定 (focus moved) | mouse.move to item 2 |
| 106-title-key-focus-new.png | title | keyboard focus back on はじめから (↑ from 設定) | ArrowUp |
| 107-title-settings.png | settings | settings overlay over the title | Esc on the title |
| 108-title-leave-0000ms.png | title | leaving (はじめから clicked: menu slides out, fade to black) (target 0 ms, frame shown at -15 ms) | click はじめから; time from the click |
| 108-title-leave-0300ms.png | title | leaving (はじめから clicked: menu slides out, fade to black) (target 300 ms, frame shown at 265 ms) | click はじめから; time from the click |
| 108-title-leave-0700ms.png | title | leaving (はじめから clicked: menu slides out, fade to black) (target 700 ms, frame shown at 697 ms) | click はじめから; time from the click |
| 108-title-leave-1000ms.png | title | leaving (はじめから clicked: menu slides out, fade to black) (target 1000 ms, frame shown at 995 ms) | click はじめから; time from the click |
| 201-hall-first-enter-0000ms.png | hall (継承の間) | first heir, entering (lamp up, rows rise, name written, seal stamped) (target 0 ms, frame shown at -35 ms) | title → はじめから; time from the hall root attaching |
| 201-hall-first-enter-0300ms.png | hall (継承の間) | first heir, entering (lamp up, rows rise, name written, seal stamped) (target 300 ms, frame shown at 303 ms) | title → はじめから; time from the hall root attaching |
| 201-hall-first-enter-1000ms.png | hall (継承の間) | first heir, entering (lamp up, rows rise, name written, seal stamped) (target 1000 ms, frame shown at 1006 ms) | title → はじめから; time from the hall root attaching |
| 201-hall-first-enter-2000ms.png | hall (継承の間) | first heir, entering (lamp up, rows rise, name written, seal stamped) (target 2000 ms, frame shown at 1997 ms) | title → はじめから; time from the hall root attaching |
| 201-hall-first-enter-4000ms.png | hall (継承の間) | first heir, entering (lamp up, rows rise, name written, seal stamped) (target 4000 ms, frame shown at 3964 ms) | title → はじめから; time from the hall root attaching |
| 202-hall-first-idle.png | hall (継承の間) | first heir, settled (no past lives, no traces) | after the entrance, ~5 s |
| 203-hall-first-hover-stratum3.png | hall (継承の間) | pointer over layer card 3 (no trace there) | mouse.move to the 3rd stratum |
| 204-hall-first-hover-start.png | hall (継承の間) | pointer over 出立の支度へ | mouse.move to the start button |
| 205-hall-settings.png | settings | settings over the hall (lineage save: 保存してタイトルへ) | Esc in the hall |
| 301-deck-enter-0000ms.png | deck (デッキを組む) | entering (cards rise in) (target 0 ms, frame shown at -58 ms) | hall → 出立の支度へ; time from the departure root attaching |
| 301-deck-enter-0150ms.png | deck (デッキを組む) | entering (cards rise in) (target 150 ms, frame shown at 151 ms) | hall → 出立の支度へ; time from the departure root attaching |
| 301-deck-enter-0300ms.png | deck (デッキを組む) | entering (cards rise in) (target 300 ms, frame shown at 293 ms) | hall → 出立の支度へ; time from the departure root attaching |
| 301-deck-enter-0600ms.png | deck (デッキを組む) | entering (cards rise in) (target 600 ms, frame shown at 587 ms) | hall → 出立の支度へ; time from the departure root attaching |
| 301-deck-enter-1000ms.png | deck (デッキを組む) | entering (cards rise in) (target 1000 ms, frame shown at 1000 ms) | hall → 出立の支度へ; time from the departure root attaching |
| 302-deck-default.png | deck (デッキを組む) | default: 試作デッキ (20), first card selected, page 1, 全て / 全コスト | just entered |
| 303-deck-filter-attr.png | deck (デッキを組む) | filter tab 「攻撃」 selected | click the 2nd filter tab |
| 304-deck-filter-attr-cost.png | deck (デッキを組む) | filter 「攻撃」 + 「1コスト 1」 | click a cost filter too |
| 305-deck-hover-card.png | deck (デッキを組む) | pointer over card 9 (hover look), detail still shows the selected card | mouse.move onto the 9th card |
| 306-deck-card-selected-detail.png | deck (デッキを組む) | card 9 selected: detail panel (face, lines, terms), deck list chip marked | click the 9th card |
| 307-deck-key-cursor.png | deck (デッキを組む) | keyboard cursor moved to card 10 (→) | ArrowRight |
| 308-deck-page2-turn-0000ms.png | deck (デッキを組む) | turning to page 2 (cards slide in from the right) (target 0 ms, frame shown at -2 ms) | click 次のページ |
| 308-deck-page2-turn-0100ms.png | deck (デッキを組む) | turning to page 2 (cards slide in from the right) (target 100 ms, frame shown at 101 ms) | click 次のページ |
| 308-deck-page2-turn-0200ms.png | deck (デッキを組む) | turning to page 2 (cards slide in from the right) (target 200 ms, frame shown at 206 ms) | click 次のページ |
| 308-deck-page2-turn-0400ms.png | deck (デッキを組む) | turning to page 2 (cards slide in from the right) (target 400 ms, frame shown at 398 ms) | click 次のページ |
| 309-deck-page2.png | deck (デッキを組む) | page 2 | after 次のページ |
| 310-deck-cleared-0.png | deck (デッキを組む) | deck cleared: 0 cards (クリア), 準備へ disabled | click クリア |
| 311-deck-under20.png | deck (デッキを組む) | deck under 20 (8 cards): gauge short of the 20 mark, status says how many more | clear, then ＋ on cards 1–6 and again on 1–2 |
| 312-deck-full.png | deck (デッキを組む) | deck at the cap (40): every ＋ disabled, gauge full | clicked ＋ until every ＋ was disabled |
| 313-deck-full-refuse-plus-0000ms.png | deck (デッキを組む) | refused ＋ on a full deck (the ＋ shakes) (target 0 ms, frame shown at -1 ms) | re-enabled the disabled ＋ in the DOM and clicked it (the builder refuses) |
| 313-deck-full-refuse-plus-0060ms.png | deck (デッキを組む) | refused ＋ on a full deck (the ＋ shakes) (target 60 ms, frame shown at 54 ms) | re-enabled the disabled ＋ in the DOM and clicked it (the builder refuses) |
| 313-deck-full-refuse-plus-0120ms.png | deck (デッキを組む) | refused ＋ on a full deck (the ＋ shakes) (target 120 ms, frame shown at 113 ms) | re-enabled the disabled ＋ in the DOM and clicked it (the builder refuses) |
| 314-deck-prototype-valid.png | deck (デッキを組む) | 試作デッキ again (valid), 準備へ enabled | click 試作デッキ |
| 315-deck-list-chip.png | deck (デッキを組む) | a deck-list chip clicked (its card picked and marked) | click the 4th chip of the deck list |
| 316-deck-for-this-life.png | deck (デッキを組む) | the deck this life sets out with: 試作デッキ + 水の構え + 岩の構え (22) | ＋ on 水の構え and 岩の構え |
| 401-supplies-enter-0000ms.png | supplies (出立の支度) | step change deck → supplies (slide) (target 0 ms, frame shown at -3 ms) | click 準備へ |
| 401-supplies-enter-0150ms.png | supplies (出立の支度) | step change deck → supplies (slide) (target 150 ms, frame shown at 147 ms) | click 準備へ |
| 401-supplies-enter-0300ms.png | supplies (出立の支度) | step change deck → supplies (slide) (target 300 ms, frame shown at 291 ms) | click 準備へ |
| 401-supplies-enter-0500ms.png | supplies (出立の支度) | step change deck → supplies (slide) (target 500 ms, frame shown at 484 ms) | click 準備へ |
| 402-supplies-empty.png | supplies (出立の支度) | empty: no talent, no tool, no consumable | after 準備へ |
| 403-supplies-start-diagram-default.png | supplies (出立の支度) | start-position diagram (crop of the info panel), no tool | clip x1236 y576 w636 h324 |
| 404-supplies-talent-tool-consumable.png | supplies (出立の支度) | talent 「突き」, tool 「防瘴の面」, consumable 「浄化の香」 chosen; 出立する enabled | click talent 1, ＋ on tool 1, ＋ on consumable 1 |
| 405-supplies-start-diagram-chosen.png | supplies (出立の支度) | start-position diagram after the choice (crop) | clip x1236 y576 w636 h324 |
| 406-supplies-start-diagram-moved.png | supplies (出立の支度) | start-position diagram with 間合いの履 (start cell 1, gap 4) (crop) | click ＋ on 間合いの履; clip of the info panel |
| 406b-supplies-with-boots.png | supplies (出立の支度) | two tools (防瘴の面, 間合いの履), the start diagram moved | full screen of the same moment |
| 407-supplies-final.png | supplies (出立の支度) | this life's supplies: talent 突き, tools 防瘴の面 + 写しの帳, consumables 浄化の香 + 薬草 | state used for this life |
| 407b-supplies-hover-slot.png | supplies (出立の支度) | pointer over a filled tool slot (× to remove) | mouse.move onto the first filled slot |
| 408-supplies-hover-setout.png | supplies (出立の支度) | pointer over 出立する | mouse.move to 出立する |
| 501-explore-layer1-enter-0000ms.png | exploration | layer 1 right after departure (map assembling under the layer banner) (target 0 ms, frame shown at -22 ms) | click 出立する; time from the explore root attaching |
| 501-explore-layer1-enter-0300ms.png | exploration | layer 1 right after departure (map assembling under the layer banner) (target 300 ms, frame shown at 296 ms) | click 出立する; time from the explore root attaching |
| 501-explore-layer1-enter-0700ms.png | exploration | layer 1 right after departure (map assembling under the layer banner) (target 700 ms, frame shown at 694 ms) | click 出立する; time from the explore root attaching |
| 501-explore-layer1-enter-1000ms.png | exploration | layer 1 right after departure (map assembling under the layer banner) (target 1000 ms, frame shown at 989 ms) | click 出立する; time from the explore root attaching |
| 501-explore-layer1-enter-1500ms.png | exploration | layer 1 right after departure (map assembling under the layer banner) (target 1500 ms, frame shown at 1495 ms) | click 出立する; time from the explore root attaching |
| 501-explore-layer1-enter-2000ms.png | exploration | layer 1 right after departure (map assembling under the layer banner) (target 2000 ms, frame shown at 1976 ms) | click 出立する; time from the explore root attaching |
| 502-explore-encounter-banner-0000ms.png | exploration | encounter banner before the first battle (「通常戦闘」) (target 0 ms, frame shown at 8 ms) | the start node is a battle; time from the banner switching on |
| 502-explore-encounter-banner-0150ms.png | exploration | encounter banner before the first battle (「通常戦闘」) (target 150 ms, frame shown at 152 ms) | the start node is a battle; time from the banner switching on |
| 502-explore-encounter-banner-0300ms.png | exploration | encounter banner before the first battle (「通常戦闘」) (target 300 ms, frame shown at 290 ms) | the start node is a battle; time from the banner switching on |
| 502-explore-encounter-banner-0600ms.png | exploration | encounter banner before the first battle (「通常戦闘」) (target 600 ms, frame shown at 582 ms) | the start node is a battle; time from the banner switching on |
| 502-explore-encounter-banner-1000ms.png | exploration | encounter banner before the first battle (「通常戦闘」) (target 1000 ms, frame shown at 1006 ms) | the start node is a battle; time from the banner switching on |
| 601-battle-open-0000ms.png | battle | battle opening (backdrop first, then HUD / field fade in, then あなたの番) (target 0 ms, frame shown at -7 ms) | first battle of layer 1 after setting out; time from the battle root attaching |
| 601-battle-open-0200ms.png | battle | battle opening (backdrop first, then HUD / field fade in, then あなたの番) (target 200 ms, frame shown at 181 ms) | first battle of layer 1 after setting out; time from the battle root attaching |
| 601-battle-open-0400ms.png | battle | battle opening (backdrop first, then HUD / field fade in, then あなたの番) (target 400 ms, frame shown at 406 ms) | first battle of layer 1 after setting out; time from the battle root attaching |
| 601-battle-open-0700ms.png | battle | battle opening (backdrop first, then HUD / field fade in, then あなたの番) (target 700 ms, frame shown at 678 ms) | first battle of layer 1 after setting out; time from the battle root attaching |
| 601-battle-open-1000ms.png | battle | battle opening (backdrop first, then HUD / field fade in, then あなたの番) (target 1000 ms, frame shown at 998 ms) | first battle of layer 1 after setting out; time from the battle root attaching |
| 601-battle-open-1500ms.png | battle | battle opening (backdrop first, then HUD / field fade in, then あなたの番) (target 1500 ms, frame shown at 1494 ms) | first battle of layer 1 after setting out; time from the battle root attaching |
| 601-battle-open-2500ms.png | battle | battle opening (backdrop first, then HUD / field fade in, then あなたの番) (target 2500 ms, frame shown at 2501 ms) | first battle of layer 1 after setting out; time from the battle root attaching |
| 602-battle-turn1-banner-0000ms.png | battle | turn start banner 「あなたの番」, turn 1 (target 0 ms, frame shown at -3 ms) | time from the banner element attaching |
| 602-battle-turn1-banner-0150ms.png | battle | turn start banner 「あなたの番」, turn 1 (target 150 ms, frame shown at 155 ms) | time from the banner element attaching |
| 602-battle-turn1-banner-0300ms.png | battle | turn start banner 「あなたの番」, turn 1 (target 300 ms, frame shown at 274 ms) | time from the banner element attaching |
| 602-battle-turn1-banner-0600ms.png | battle | turn start banner 「あなたの番」, turn 1 (target 600 ms, frame shown at 571 ms) | time from the banner element attaching |
| 602-battle-turn1-banner-1000ms.png | battle | turn start banner 「あなたの番」, turn 1 (target 1000 ms, frame shown at 992 ms) | time from the banner element attaching |

## First battle of layer 1, played by hand (no autobattle): interactive states, enemy turn, attack, win

Script: `cap-battle1.mjs`

| file | screen | state | how it was reached |
| --- | --- | --- | --- |
| 109-title-with-save.png | title | title with a life in progress (つづきから enabled and focused) | reload with the save of a life just set out |
| 603-battle-idle-hand.png | battle | turn 1, idle: hand fanned, guide line, enemy omen (移動) | つづきから into the waiting fight; no input |
| 604-battle-hover-card.png | battle | pointer over 「呼吸を整える」: lifted, hover line (trait) above it | mouse.move onto the card |
| 605-battle-hover-out-of-reach.png | battle | pointer over 「袈裟斬り」 (range 0〜1, gap 3): reason panel 「相手との間合いが 0〜1 のとき使用可能」 | mouse.move onto an attack that does not reach |
| 606-battle-card-detail.png | battle | card detail of 「袈裟斬り」 (right click) | right click on the card |
| 607-battle-held-out-of-reach-over-enemy.png | battle | held 「袈裟斬り」 over the enemy while out of reach | mouse down on the card, carried onto the enemy receiver |
| 608-battle-refusal-0000ms.png | battle | refused release: refusal line, card slides home (target 0 ms, frame shown at -9 ms) | mouse up over the enemy with the out-of-reach card |
| 608-battle-refusal-0080ms.png | battle | refused release: refusal line, card slides home (target 80 ms, frame shown at 86 ms) | mouse up over the enemy with the out-of-reach card |
| 608-battle-refusal-0160ms.png | battle | refused release: refusal line, card slides home (target 160 ms, frame shown at 156 ms) | mouse up over the enemy with the out-of-reach card |
| 608-battle-refusal-0300ms.png | battle | refused release: refusal line, card slides home (target 300 ms, frame shown at 307 ms) | mouse up over the enemy with the out-of-reach card |
| 608-battle-refusal-0600ms.png | battle | refused release: refusal line, card slides home (target 600 ms, frame shown at 599 ms) | mouse up over the enemy with the out-of-reach card |
| 608-battle-refusal-1000ms.png | battle | refused release: refusal line, card slides home (target 1000 ms, frame shown at 998 ms) | mouse up over the enemy with the out-of-reach card |
| 609-battle-refusal-line.png | battle | refusal line standing (3.5 s) | right after the refused release |
| 610-battle-hover-draw-pile.png | battle | pointer over the draw pile | mouse.move onto the draw pile |
| 611-battle-pile-drawer-draw.png | battle | pile drawer open: draw pile list (figures shift right) | click on the draw pile |
| 612-battle-speed-1.png | battle | speed switch clicked → ×1.5 (pointer on it) | click the speed switch |
| 612-battle-speed-2.png | battle | speed switch clicked → ×1 (pointer on it) | click the speed switch |
| 612-battle-speed-3.png | battle | speed switch clicked → ×1.25 (pointer on it) | click the speed switch |
| 613-battle-journal.png | battle | journal drawer over the battle (right 30 %, figures shift left) | J |
| 614-battle-settings.png | settings | settings over the battle (保存して閉じる) | Esc (released) in the fight with nothing open |
| 615-battle-stance-slot-empty-hover.png | battle | pointer over the empty stance slot (player) | mouse.move onto the stance slot |
| 616-battle-held-self-below-line.png | battle | held 「岩の構え」 (self) still below the throw line | mouse down on the card, carried to y 720 |
| 617-battle-held-self-over-line.png | battle | held 「岩の構え」 (self) above the throw line (zone lit) | carried on to y 470 |
| 618-battle-release-stance-0000ms.png | battle | release of 「岩の構え」 above the throw line (card flies, effect lands) (target 0 ms, frame shown at -9 ms) | mouse up above the throw line |
| 618-battle-release-stance-0080ms.png | battle | release of 「岩の構え」 above the throw line (card flies, effect lands) (target 80 ms, frame shown at 76 ms) | mouse up above the throw line |
| 618-battle-release-stance-0160ms.png | battle | release of 「岩の構え」 above the throw line (card flies, effect lands) (target 160 ms, frame shown at 163 ms) | mouse up above the throw line |
| 618-battle-release-stance-0300ms.png | battle | release of 「岩の構え」 above the throw line (card flies, effect lands) (target 300 ms, frame shown at 296 ms) | mouse up above the throw line |
| 618-battle-release-stance-0500ms.png | battle | release of 「岩の構え」 above the throw line (card flies, effect lands) (target 500 ms, frame shown at 504 ms) | mouse up above the throw line |
| 618-battle-release-stance-0800ms.png | battle | release of 「岩の構え」 above the throw line (card flies, effect lands) (target 800 ms, frame shown at 806 ms) | mouse up above the throw line |
| 618-battle-release-stance-1200ms.png | battle | release of 「岩の構え」 above the throw line (card flies, effect lands) (target 1200 ms, frame shown at 1201 ms) | mouse up above the throw line |
| 618-battle-release-stance-1800ms.png | battle | release of 「岩の構え」 above the throw line (card flies, effect lands) (target 1800 ms, frame shown at 1802 ms) | mouse up above the throw line |
| 619-battle-after-stance.png | battle | after the stance card: stance list shows it | idle after the release |
| 620-battle-stance-tooltip.png | battle | pointer over the player's stance: tooltip | mouse.move onto the stance frame |
| 622-battle-key-selected.png | battle | card 3 「呼吸を整える」 selected with its digit key | key 3 |
| 623-battle-turn1-before-end.png | battle | turn 1 before ending the turn (Guard up, stance set) | idle |
| 624-battle-turn1-end-from-x-0000ms.png | battle | turn 1: end turn → enemy turn → next turn (target 0 ms, frame shown at -7 ms) | X; time from the key press |
| 624-battle-turn1-end-from-x-0300ms.png | battle | turn 1: end turn → enemy turn → next turn (target 300 ms, frame shown at 307 ms) | X; time from the key press |
| 624-battle-turn1-end-from-x-0600ms.png | battle | turn 1: end turn → enemy turn → next turn (target 600 ms, frame shown at 591 ms) | X; time from the key press |
| 624-battle-turn1-end-from-x-1000ms.png | battle | turn 1: end turn → enemy turn → next turn (target 1000 ms, frame shown at 1007 ms) | X; time from the key press |
| 624-battle-turn1-end-from-x-1500ms.png | battle | turn 1: end turn → enemy turn → next turn (target 1500 ms, frame shown at 1506 ms) | X; time from the key press |
| 624-battle-turn1-end-from-x-2000ms.png | battle | turn 1: end turn → enemy turn → next turn (target 2000 ms, frame shown at 1992 ms) | X; time from the key press |
| 624-battle-turn1-end-from-x-2500ms.png | battle | turn 1: end turn → enemy turn → next turn (target 2500 ms, frame shown at 2502 ms) | X; time from the key press |
| 624-battle-turn1-end-from-x-3000ms.png | battle | turn 1: end turn → enemy turn → next turn (target 3000 ms, frame shown at 3006 ms) | X; time from the key press |
| 624-battle-turn1-end-from-x-3500ms.png | battle | turn 1: end turn → enemy turn → next turn (target 3500 ms, frame shown at 3505 ms) | X; time from the key press |
| 624-battle-turn1-end-from-x-4000ms.png | battle | turn 1: end turn → enemy turn → next turn (target 4000 ms, frame shown at 3994 ms) | X; time from the key press |
| 624-battle-turn1-end-from-x-5000ms.png | battle | turn 1: end turn → enemy turn → next turn (target 5000 ms, frame shown at 4990 ms) | X; time from the key press |
| 624-battle-turn1-end-from-x-6000ms.png | battle | turn 1: end turn → enemy turn → next turn (target 6000 ms, frame shown at 6001 ms) | X; time from the key press |
| 624-battle-turn1-end-enemy-banner-0000ms.png | battle | enemy turn shade + banner 「敵の番」, then the enemy's action (target 0 ms, frame shown at -3 ms) | time from the 敵の番 banner attaching |
| 624-battle-turn1-end-enemy-banner-0150ms.png | battle | enemy turn shade + banner 「敵の番」, then the enemy's action (target 150 ms, frame shown at 143 ms) | time from the 敵の番 banner attaching |
| 624-battle-turn1-end-enemy-banner-0300ms.png | battle | enemy turn shade + banner 「敵の番」, then the enemy's action (target 300 ms, frame shown at 301 ms) | time from the 敵の番 banner attaching |
| 624-battle-turn1-end-enemy-banner-0600ms.png | battle | enemy turn shade + banner 「敵の番」, then the enemy's action (target 600 ms, frame shown at 597 ms) | time from the 敵の番 banner attaching |
| 624-battle-turn1-end-enemy-banner-1000ms.png | battle | enemy turn shade + banner 「敵の番」, then the enemy's action (target 1000 ms, frame shown at 999 ms) | time from the 敵の番 banner attaching |
| 624-battle-turn1-end-enemy-banner-1400ms.png | battle | enemy turn shade + banner 「敵の番」, then the enemy's action (target 1400 ms, frame shown at 1402 ms) | time from the 敵の番 banner attaching |
| 624-battle-turn1-end-enemy-banner-2000ms.png | battle | enemy turn shade + banner 「敵の番」, then the enemy's action (target 2000 ms, frame shown at 1998 ms) | time from the 敵の番 banner attaching |
| 624-battle-turn1-end-turn-banner-0000ms.png | battle | turn start banner 「あなたの番」 (next turn) (target 0 ms, frame shown at -6 ms) | time from the あなたの番 banner attaching |
| 624-battle-turn1-end-turn-banner-0150ms.png | battle | turn start banner 「あなたの番」 (next turn) (target 150 ms, frame shown at 150 ms) | time from the あなたの番 banner attaching |
| 624-battle-turn1-end-turn-banner-0300ms.png | battle | turn start banner 「あなたの番」 (next turn) (target 300 ms, frame shown at 297 ms) | time from the あなたの番 banner attaching |
| 624-battle-turn1-end-turn-banner-0600ms.png | battle | turn start banner 「あなたの番」 (next turn) (target 600 ms, frame shown at 598 ms) | time from the あなたの番 banner attaching |
| 624-battle-turn1-end-turn-banner-1000ms.png | battle | turn start banner 「あなたの番」 (next turn) (target 1000 ms, frame shown at 995 ms) | time from the あなたの番 banner attaching |
| 625-battle-turn2-idle.png | battle | turn 2 idle (new omen, the enemy has moved) | after the enemy turn |
| 626-battle-hover-attack-in-reach.png | battle | pointer over 「伸び突き」 in reach (hover line, reach cells lit on the floor) | mouse.move onto the card |
| 627-battle-held-over-enemy.png | battle | held 「伸び突き」 over the enemy: target hooks, prediction tag, reach light on the floor | mouse down on the card, carried onto the enemy receiver |
| 627b-battle-held-over-enemy-low.png | battle | held 「伸び突き」 low on the enemy receiver (figure, hooks and floor visible) | carried to the lower left part of the receiver box |
| 628-battle-release-attack-0000ms.png | battle | release of 「伸び突き」 on the enemy (card flies, strike, hit) (target 0 ms, frame shown at 4 ms) | mouse up over the enemy receiver |
| 628-battle-release-attack-0080ms.png | battle | release of 「伸び突き」 on the enemy (card flies, strike, hit) (target 80 ms, frame shown at 74 ms) | mouse up over the enemy receiver |
| 628-battle-release-attack-0160ms.png | battle | release of 「伸び突き」 on the enemy (card flies, strike, hit) (target 160 ms, frame shown at 160 ms) | mouse up over the enemy receiver |
| 628-battle-release-attack-0300ms.png | battle | release of 「伸び突き」 on the enemy (card flies, strike, hit) (target 300 ms, frame shown at 298 ms) | mouse up over the enemy receiver |
| 628-battle-release-attack-0500ms.png | battle | release of 「伸び突き」 on the enemy (card flies, strike, hit) (target 500 ms, frame shown at 494 ms) | mouse up over the enemy receiver |
| 628-battle-release-attack-0700ms.png | battle | release of 「伸び突き」 on the enemy (card flies, strike, hit) (target 700 ms, frame shown at 703 ms) | mouse up over the enemy receiver |
| 628-battle-release-attack-1000ms.png | battle | release of 「伸び突き」 on the enemy (card flies, strike, hit) (target 1000 ms, frame shown at 997 ms) | mouse up over the enemy receiver |
| 628-battle-release-attack-1500ms.png | battle | release of 「伸び突き」 on the enemy (card flies, strike, hit) (target 1500 ms, frame shown at 1498 ms) | mouse up over the enemy receiver |
| 628-battle-release-attack-2000ms.png | battle | release of 「伸び突き」 on the enemy (card flies, strike, hit) (target 2000 ms, frame shown at 2003 ms) | mouse up over the enemy receiver |
| 629-battle-hit-number-0000ms.png | battle | hit: floating number 1 (「Guard−5」) (target 0 ms, frame shown at -4 ms) | time from that floating number attaching |
| 629-battle-hit-number-0100ms.png | battle | hit: floating number 1 (「Guard−5」) (target 100 ms, frame shown at 95 ms) | time from that floating number attaching |
| 629-battle-hit-number-0200ms.png | battle | hit: floating number 1 (「Guard−5」) (target 200 ms, frame shown at 205 ms) | time from that floating number attaching |
| 629-battle-hit-number-0400ms.png | battle | hit: floating number 1 (「Guard−5」) (target 400 ms, frame shown at 408 ms) | time from that floating number attaching |
| 629-battle-hit-number-0700ms.png | battle | hit: floating number 1 (「Guard−5」) (target 700 ms, frame shown at 692 ms) | time from that floating number attaching |
| 629-battle-hit-number-1000ms.png | battle | hit: floating number 1 (「Guard−5」) (target 1000 ms, frame shown at 999 ms) | time from that floating number attaching |
| 629b-battle-hit-number-0000ms.png | battle | hit: floating number 2 (「−15+5 間合い」) (target 0 ms, frame shown at -4 ms) | time from that floating number attaching |
| 629b-battle-hit-number-0100ms.png | battle | hit: floating number 2 (「−15+5 間合い」) (target 100 ms, frame shown at 97 ms) | time from that floating number attaching |
| 629b-battle-hit-number-0200ms.png | battle | hit: floating number 2 (「−15+5 間合い」) (target 200 ms, frame shown at 204 ms) | time from that floating number attaching |
| 629b-battle-hit-number-0400ms.png | battle | hit: floating number 2 (「−15+5 間合い」) (target 400 ms, frame shown at 396 ms) | time from that floating number attaching |
| 629b-battle-hit-number-0700ms.png | battle | hit: floating number 2 (「−15+5 間合い」) (target 700 ms, frame shown at 688 ms) | time from that floating number attaching |
| 629b-battle-hit-number-1000ms.png | battle | hit: floating number 2 (「−15+5 間合い」) (target 1000 ms, frame shown at 993 ms) | time from that floating number attaching |
| 631-battle-omen-hover.png | battle | pointer over the enemy omen badge (its aimed cells kept lit) | mouse.move onto the omen badge |
| 633-battle-result-won-0000ms.png | battle | win: result card (勝ち) coming in (target 0 ms, frame shown at 55 ms) | the winning card released; time from the result element attaching |
| 633-battle-result-won-0150ms.png | battle | win: result card (勝ち) coming in (target 150 ms, frame shown at 146 ms) | the winning card released; time from the result element attaching |
| 633-battle-result-won-0300ms.png | battle | win: result card (勝ち) coming in (target 300 ms, frame shown at 291 ms) | the winning card released; time from the result element attaching |
| 633-battle-result-won-0600ms.png | battle | win: result card (勝ち) coming in (target 600 ms, frame shown at 596 ms) | the winning card released; time from the result element attaching |
| 633-battle-result-won-1000ms.png | battle | win: result card (勝ち) coming in (target 1000 ms, frame shown at 997 ms) | the winning card released; time from the result element attaching |
| 633-battle-result-won-1500ms.png | battle | win: result card (勝ち) coming in (target 1500 ms, frame shown at 1502 ms) | the winning card released; time from the result element attaching |
| 633-battle-result-won-2500ms.png | battle | win: result card (勝ち) coming in (target 2500 ms, frame shown at 2499 ms) | the winning card released; time from the result element attaching |
| 701-explore-battle-result-reward.png | exploration | battle result / reward card after the first fight (勝利, learned, journal, finds) | the fight won; the map comes back with the result card |

## Layer 1 with ?autobattle: result card, node hover / pin / preview, overlays, walk, 情報収集, イベント, 休息→訓練, boss, layer break, interlude, descent

Script: `cap-explore1.mjs`

| file | screen | state | how it was reached |
| --- | --- | --- | --- |
| 702-explore-result-hover-pick.png | exploration | battle result card: pointer over reward card 2 | mouse.move onto the 2nd card |
| 703-explore-result-card-chosen.png | exploration | battle result card: card 2 chosen (frame, この札を覚える enabled) | click the 2nd card |
| 704-explore-result-close-0000ms.png | exploration | result card closing after この札を覚える (target 0 ms, frame shown at -20 ms) | Enter with a card chosen |
| 704-explore-result-close-0100ms.png | exploration | result card closing after この札を覚える (target 100 ms, frame shown at 108 ms) | Enter with a card chosen |
| 704-explore-result-close-0200ms.png | exploration | result card closing after この札を覚える (target 200 ms, frame shown at 181 ms) | Enter with a card chosen |
| 704-explore-result-close-0350ms.png | exploration | result card closing after この札を覚える (target 350 ms, frame shown at 342 ms) | Enter with a card chosen |
| 704-explore-result-close-0600ms.png | exploration | result card closing after この札を覚える (target 600 ms, frame shown at 588 ms) | Enter with a card chosen |
| 705-explore-map-idle-start.png | exploration | layer 1 map idle on the start node (after the first fight) | after closing the result card |
| 706-explore-hover-node.png | exploration | pointer over a reachable node (Elite) without pinning: the detail follows the hover, no buttons | mouse.move onto the node |
| 707-explore-hover-far-node.png | exploration | pointer over a far node (Battle) | mouse.move onto a node not reachable yet |
| 708-explore-pinned-survey.png | exploration | node pinned (Survey): selection frame and the preview panel with its way in and forecast | click the node |
| 708-explore-pinned-elite.png | exploration | node pinned (Elite): selection frame and the preview panel with its way in and forecast | click the node |
| 708-explore-pinned-battle.png | exploration | node pinned (Battle): selection frame and the preview panel with its way in and forecast | click the node |
| 709-explore-key-select.png | exploration | selection moved with ← (keyboard) | ArrowLeft |
| 710-explore-journal.png | journal (手記) | journal drawer over the map (J) | J on the map |
| 710b-explore-journal-tab2.png | journal (手記) | journal tab 2 | click the tab |
| 711-explore-owned-cards.png | owned cards (所持カード) | owned cards overlay (C) | C on the map |
| 711b-explore-owned-cards-hover.png | owned cards (所持カード) | owned cards: pointer over a card | mouse.move onto a card |
| 711c-explore-owned-cards-key.png | owned cards (所持カード) | owned cards: cursor moved with → | ArrowRight |
| 712-explore-settings.png | settings | settings over the map (保存して閉じる) | Esc on the map |
| 713-explore-hover-endlife.png | exploration | pointer over この生を終える | mouse.move onto the button |
| 714-explore-confirm-endlife.png | exploration | confirmation: この生を終える? (やめる / 終える) | click この生を終える |
| 715-explore-hover-consumable.png | exploration | pointer over consumable slot 1 | mouse.move onto the slot |
| 719-explore-preview-survey.png | exploration | Survey node pinned: preview panel (進む) | click the node before stepping |
| 718-explore-walk-0000ms.png | exploration | walking animation to Survey#1 (walker steps along the lit edge) (target 0 ms, frame shown at 0 ms) | click the way-in button; time from the click |
| 718-explore-walk-0100ms.png | exploration | walking animation to Survey#1 (walker steps along the lit edge) (target 100 ms, frame shown at 100 ms) | click the way-in button; time from the click |
| 718-explore-walk-0200ms.png | exploration | walking animation to Survey#1 (walker steps along the lit edge) (target 200 ms, frame shown at 182 ms) | click the way-in button; time from the click |
| 718-explore-walk-0300ms.png | exploration | walking animation to Survey#1 (walker steps along the lit edge) (target 300 ms, frame shown at 289 ms) | click the way-in button; time from the click |
| 718-explore-walk-0450ms.png | exploration | walking animation to Survey#1 (walker steps along the lit edge) (target 450 ms, frame shown at 436 ms) | click the way-in button; time from the click |
| 718-explore-walk-0600ms.png | exploration | walking animation to Survey#1 (walker steps along the lit edge) (target 600 ms, frame shown at 602 ms) | click the way-in button; time from the click |
| 718-explore-walk-0800ms.png | exploration | walking animation to Survey#1 (walker steps along the lit edge) (target 800 ms, frame shown at 794 ms) | click the way-in button; time from the click |
| 718-explore-walk-1000ms.png | exploration | walking animation to Survey#1 (walker steps along the lit edge) (target 1000 ms, frame shown at 991 ms) | click the way-in button; time from the click |
| 718-explore-walk-1300ms.png | exploration | walking animation to Survey#1 (walker steps along the lit edge) (target 1300 ms, frame shown at 1291 ms) | click the way-in button; time from the click |
| 718-explore-walk-1700ms.png | exploration | walking animation to Survey#1 (walker steps along the lit edge) (target 1700 ms, frame shown at 1707 ms) | click the way-in button; time from the click |
| 718b-explore-walk-from-start-0000ms.png | exploration | walking frames from the walker starting (target 0 ms, frame shown at -12 ms) | time from the walker's is-walking class |
| 718b-explore-walk-from-start-0100ms.png | exploration | walking frames from the walker starting (target 100 ms, frame shown at 94 ms) | time from the walker's is-walking class |
| 718b-explore-walk-from-start-0200ms.png | exploration | walking frames from the walker starting (target 200 ms, frame shown at 202 ms) | time from the walker's is-walking class |
| 718b-explore-walk-from-start-0300ms.png | exploration | walking frames from the walker starting (target 300 ms, frame shown at 289 ms) | time from the walker's is-walking class |
| 718b-explore-walk-from-start-0400ms.png | exploration | walking frames from the walker starting (target 400 ms, frame shown at 405 ms) | time from the walker's is-walking class |
| 718b-explore-walk-from-start-0600ms.png | exploration | walking frames from the walker starting (target 600 ms, frame shown at 595 ms) | time from the walker's is-walking class |
| 718b-explore-walk-from-start-0800ms.png | exploration | walking frames from the walker starting (target 800 ms, frame shown at 785 ms) | time from the walker's is-walking class |
| 720-explore-survey-choice.png | exploration | 情報収集 with 写しの帳: which enemy to read up on (choice plate) | arrived on the 情報収集 node |
| 721-explore-survey-choice-hover.png | exploration | 情報収集 choice: pointer over option 1 | mouse.move onto the first row |
| 722-explore-survey-banner-0000ms.png | exploration | 情報収集 chosen: the note banner says what was raised (target 0 ms, frame shown at -14 ms) | key 1 on the choice |
| 722-explore-survey-banner-0200ms.png | exploration | 情報収集 chosen: the note banner says what was raised (target 200 ms, frame shown at 203 ms) | key 1 on the choice |
| 722-explore-survey-banner-0500ms.png | exploration | 情報収集 chosen: the note banner says what was raised (target 500 ms, frame shown at 493 ms) | key 1 on the choice |
| 722-explore-survey-banner-1000ms.png | exploration | 情報収集 chosen: the note banner says what was raised (target 1000 ms, frame shown at 983 ms) | key 1 on the choice |
| 722-explore-survey-banner-2000ms.png | exploration | 情報収集 chosen: the note banner says what was raised (target 2000 ms, frame shown at 1995 ms) | key 1 on the choice |
| 722-explore-survey-banner-3000ms.png | exploration | 情報収集 chosen: the note banner says what was raised (target 3000 ms, frame shown at 2986 ms) | key 1 on the choice |
| 723-explore-preview-event.png | exploration | Event node pinned: preview panel (進む) | click the node before stepping |
| 726-explore-event-result.png | exploration | イベント result card (what the event did) | arrived; the result card |
| 727-explore-preview-rest.png | exploration | Rest node pinned: preview panel (休む / 訓練する) | click the node before stepping |
| 732-explore-training.png | exploration | training pick card (訓練: one mark to spend) | reached during the walk |
| 733-explore-training-chosen.png | exploration | training card: a card chosen (preview on the right) | ↓ twice |
| 734-explore-trained-banner-0000ms.png | exploration | trained: the note banner says what the mark did (target 0 ms, frame shown at -21 ms) | Enter on the training card |
| 734-explore-trained-banner-0200ms.png | exploration | trained: the note banner says what the mark did (target 200 ms, frame shown at 205 ms) | Enter on the training card |
| 734-explore-trained-banner-0500ms.png | exploration | trained: the note banner says what the mark did (target 500 ms, frame shown at 491 ms) | Enter on the training card |
| 734-explore-trained-banner-1000ms.png | exploration | trained: the note banner says what the mark did (target 1000 ms, frame shown at 980 ms) | Enter on the training card |
| 734-explore-trained-banner-2000ms.png | exploration | trained: the note banner says what the mark did (target 2000 ms, frame shown at 1988 ms) | Enter on the training card |
| 736-explore-preview-boss.png | exploration | Boss node pinned: preview panel (進む) | click the node before stepping |
| 737-explore-to-boss-0000ms.png | exploration | stepping onto the boss node: walk, encounter banner (autobattle fights it unattended) (target 0 ms, frame shown at -19 ms) | click the way-in button; time from the click |
| 737-explore-to-boss-0300ms.png | exploration | stepping onto the boss node: walk, encounter banner (autobattle fights it unattended) (target 300 ms, frame shown at 284 ms) | click the way-in button; time from the click |
| 737-explore-to-boss-0600ms.png | exploration | stepping onto the boss node: walk, encounter banner (autobattle fights it unattended) (target 600 ms, frame shown at 588 ms) | click the way-in button; time from the click |
| 737-explore-to-boss-0900ms.png | exploration | stepping onto the boss node: walk, encounter banner (autobattle fights it unattended) (target 900 ms, frame shown at 887 ms) | click the way-in button; time from the click |
| 737-explore-to-boss-1200ms.png | exploration | stepping onto the boss node: walk, encounter banner (autobattle fights it unattended) (target 1200 ms, frame shown at 1194 ms) | click the way-in button; time from the click |
| 737-explore-to-boss-1500ms.png | exploration | stepping onto the boss node: walk, encounter banner (autobattle fights it unattended) (target 1500 ms, frame shown at 1483 ms) | click the way-in button; time from the click |
| 737-explore-to-boss-2000ms.png | exploration | stepping onto the boss node: walk, encounter banner (autobattle fights it unattended) (target 2000 ms, frame shown at 1980 ms) | click the way-in button; time from the click |
| 737-explore-to-boss-2500ms.png | exploration | stepping onto the boss node: walk, encounter banner (autobattle fights it unattended) (target 2500 ms, frame shown at 2499 ms) | click the way-in button; time from the click |
| 737-explore-to-boss-3000ms.png | exploration | stepping onto the boss node: walk, encounter banner (autobattle fights it unattended) (target 3000 ms, frame shown at 2976 ms) | click the way-in button; time from the click |
| 737-explore-to-boss-4000ms.png | exploration | stepping onto the boss node: walk, encounter banner (autobattle fights it unattended) (target 4000 ms, frame shown at 3978 ms) | click the way-in button; time from the click |
| 740-explore-boss-result.png | exploration | boss fight result card (autobattle) | arrived; the result card |
| 741-explore-layer-break.png | exploration | layer-break card (階層を抜けた) | after the boss result |
| 741b-explore-layer-break-hover.png | exploration | layer-break card: pointer over its button | mouse.move onto the first button |
| 742-explore-to-interlude-0000ms.png | exploration | layer-break card → interlude (rest on washi, the next layer on the right) (target 0 ms, frame shown at 6 ms) | Enter on the layer-break card |
| 742-explore-to-interlude-0200ms.png | exploration | layer-break card → interlude (rest on washi, the next layer on the right) (target 200 ms, frame shown at 204 ms) | Enter on the layer-break card |
| 742-explore-to-interlude-0400ms.png | exploration | layer-break card → interlude (rest on washi, the next layer on the right) (target 400 ms, frame shown at 397 ms) | Enter on the layer-break card |
| 742-explore-to-interlude-0700ms.png | exploration | layer-break card → interlude (rest on washi, the next layer on the right) (target 700 ms, frame shown at 697 ms) | Enter on the layer-break card |
| 742-explore-to-interlude-1000ms.png | exploration | layer-break card → interlude (rest on washi, the next layer on the right) (target 1000 ms, frame shown at 1006 ms) | Enter on the layer-break card |
| 742-explore-to-interlude-1500ms.png | exploration | layer-break card → interlude (rest on washi, the next layer on the right) (target 1500 ms, frame shown at 1478 ms) | Enter on the layer-break card |
| 742-explore-to-interlude-2400ms.png | exploration | layer-break card → interlude (rest on washi, the next layer on the right) (target 2400 ms, frame shown at 2394 ms) | Enter on the layer-break card |
| 743-explore-interlude.png | exploration | interlude (階層間の休憩): rest plate, next layer window | after the layer-break card |
| 744-explore-interlude-hover-descend.png | exploration | interlude: pointer over 次の層へ降りる | mouse.move onto the first button |
| 747-explore-descend-0000ms.png | exploration | descent: interlude → layer 2 (descend banner, map assembling) (target 0 ms, frame shown at -10 ms) | Enter at the interlude; time from the key |
| 747-explore-descend-0300ms.png | exploration | descent: interlude → layer 2 (descend banner, map assembling) (target 300 ms, frame shown at 281 ms) | Enter at the interlude; time from the key |
| 747-explore-descend-0600ms.png | exploration | descent: interlude → layer 2 (descend banner, map assembling) (target 600 ms, frame shown at 607 ms) | Enter at the interlude; time from the key |
| 747-explore-descend-1000ms.png | exploration | descent: interlude → layer 2 (descend banner, map assembling) (target 1000 ms, frame shown at 985 ms) | Enter at the interlude; time from the key |
| 747-explore-descend-1500ms.png | exploration | descent: interlude → layer 2 (descend banner, map assembling) (target 1500 ms, frame shown at 1484 ms) | Enter at the interlude; time from the key |
| 747-explore-descend-2000ms.png | exploration | descent: interlude → layer 2 (descend banner, map assembling) (target 2000 ms, frame shown at 2000 ms) | Enter at the interlude; time from the key |
| 747-explore-descend-3000ms.png | exploration | descent: interlude → layer 2 (descend banner, map assembling) (target 3000 ms, frame shown at 2983 ms) | Enter at the interlude; time from the key |
| 747-explore-descend-4000ms.png | exploration | descent: interlude → layer 2 (descend banner, map assembling) (target 4000 ms, frame shown at 3996 ms) | Enter at the interlude; time from the key |
| 747-explore-descend-5000ms.png | exploration | descent: interlude → layer 2 (descend banner, map assembling) (target 5000 ms, frame shown at 5003 ms) | Enter at the interlude; time from the key |
| 748-explore-layer2-banner-0000ms.png | exploration | descend banner 「第 2 層■■の秘跡」 (target 0 ms, frame shown at -25 ms) | time from the layer banner switching on |
| 748-explore-layer2-banner-0150ms.png | exploration | descend banner 「第 2 層■■の秘跡」 (target 150 ms, frame shown at 130 ms) | time from the layer banner switching on |
| 748-explore-layer2-banner-0300ms.png | exploration | descend banner 「第 2 層■■の秘跡」 (target 300 ms, frame shown at 266 ms) | time from the layer banner switching on |
| 748-explore-layer2-banner-0600ms.png | exploration | descend banner 「第 2 層■■の秘跡」 (target 600 ms, frame shown at 592 ms) | time from the layer banner switching on |
| 748-explore-layer2-banner-1000ms.png | exploration | descend banner 「第 2 層■■の秘跡」 (target 1000 ms, frame shown at 997 ms) | time from the layer banner switching on |
| 748-explore-layer2-banner-1500ms.png | exploration | descend banner 「第 2 層■■の秘跡」 (target 1500 ms, frame shown at 1500 ms) | time from the layer banner switching on |
| 730-explore-battle-result-node0.png | exploration | fight result card (autobattle) on node 0 | arrived; the result card |
| 749-explore-layer2-map.png | exploration | layer 2 map, idle after the descent | after the descent settles |

## Layer 2 of the first life: consumable use, 秘跡の彫り, boss fought by hand and lost, end card, ending (death), hall of the second heir

Script: `cap-explore2.mjs`

| file | screen | state | how it was reached |
| --- | --- | --- | --- |
| 750-explore-hover-consumable-herb.png | exploration | pointer over 薬草 (HP 26 → 36 forecast) | mouse.move onto the slot |
| 751-explore-use-consumable-0000ms.png | exploration | consumable used on the map (薬草: HP rolls up, slot empties) (target 0 ms, frame shown at 7 ms) | click the slot |
| 751-explore-use-consumable-0150ms.png | exploration | consumable used on the map (薬草: HP rolls up, slot empties) (target 150 ms, frame shown at 154 ms) | click the slot |
| 751-explore-use-consumable-0300ms.png | exploration | consumable used on the map (薬草: HP rolls up, slot empties) (target 300 ms, frame shown at 304 ms) | click the slot |
| 751-explore-use-consumable-0600ms.png | exploration | consumable used on the map (薬草: HP rolls up, slot empties) (target 600 ms, frame shown at 603 ms) | click the slot |
| 751-explore-use-consumable-1000ms.png | exploration | consumable used on the map (薬草: HP rolls up, slot empties) (target 1000 ms, frame shown at 996 ms) | click the slot |
| 751-explore-use-consumable-1500ms.png | exploration | consumable used on the map (薬草: HP rolls up, slot empties) (target 1500 ms, frame shown at 1489 ms) | click the slot |
| 754-explore-preview-carving.png | exploration | 秘跡の彫り pinned: preview (the hidden name ■■の秘跡) | click the carving node |
| 755-explore-carving-0000ms.png | exploration | stepping onto 秘跡の彫り: walk, then the layer banner with the real name, the band renamed (target 0 ms, frame shown at 7 ms) | click 進む on the carving; time from the click |
| 755-explore-carving-0400ms.png | exploration | stepping onto 秘跡の彫り: walk, then the layer banner with the real name, the band renamed (target 400 ms, frame shown at 385 ms) | click 進む on the carving; time from the click |
| 755-explore-carving-0800ms.png | exploration | stepping onto 秘跡の彫り: walk, then the layer banner with the real name, the band renamed (target 800 ms, frame shown at 784 ms) | click 進む on the carving; time from the click |
| 755-explore-carving-1200ms.png | exploration | stepping onto 秘跡の彫り: walk, then the layer banner with the real name, the band renamed (target 1200 ms, frame shown at 1187 ms) | click 進む on the carving; time from the click |
| 755-explore-carving-1600ms.png | exploration | stepping onto 秘跡の彫り: walk, then the layer banner with the real name, the band renamed (target 1600 ms, frame shown at 1605 ms) | click 進む on the carving; time from the click |
| 755-explore-carving-2000ms.png | exploration | stepping onto 秘跡の彫り: walk, then the layer banner with the real name, the band renamed (target 2000 ms, frame shown at 1993 ms) | click 進む on the carving; time from the click |
| 755-explore-carving-2500ms.png | exploration | stepping onto 秘跡の彫り: walk, then the layer banner with the real name, the band renamed (target 2500 ms, frame shown at 2506 ms) | click 進む on the carving; time from the click |
| 755-explore-carving-3000ms.png | exploration | stepping onto 秘跡の彫り: walk, then the layer banner with the real name, the band renamed (target 3000 ms, frame shown at 3002 ms) | click 進む on the carving; time from the click |
| 755-explore-carving-4000ms.png | exploration | stepping onto 秘跡の彫り: walk, then the layer banner with the real name, the band renamed (target 4000 ms, frame shown at 3984 ms) | click 進む on the carving; time from the click |
| 755-explore-carving-5000ms.png | exploration | stepping onto 秘跡の彫り: walk, then the layer banner with the real name, the band renamed (target 5000 ms, frame shown at 5002 ms) | click 進む on the carving; time from the click |
| 756-explore-carving-banner-0000ms.png | exploration | carving banner 「第 2 層竜神の秘跡」 (target 0 ms, frame shown at 7 ms) | time from the banner switching on |
| 756-explore-carving-banner-0150ms.png | exploration | carving banner 「第 2 層竜神の秘跡」 (target 150 ms, frame shown at 139 ms) | time from the banner switching on |
| 756-explore-carving-banner-0300ms.png | exploration | carving banner 「第 2 層竜神の秘跡」 (target 300 ms, frame shown at 302 ms) | time from the banner switching on |
| 756-explore-carving-banner-0600ms.png | exploration | carving banner 「第 2 層竜神の秘跡」 (target 600 ms, frame shown at 588 ms) | time from the banner switching on |
| 756-explore-carving-banner-1000ms.png | exploration | carving banner 「第 2 層竜神の秘跡」 (target 1000 ms, frame shown at 1002 ms) | time from the banner switching on |
| 756-explore-carving-banner-1500ms.png | exploration | carving banner 「第 2 層竜神の秘跡」 (target 1500 ms, frame shown at 1487 ms) | time from the banner switching on |
| 757-explore-carving-read.png | exploration | after the carving: the layer's real name in the band and on the node | settled on the carving node |
| 660-battle-boss-idle.png | battle | boss fight (layer 2), turn 1 idle | stepped onto the boss node without autobattle |
| 663-battle-boss-enemy-turn-0000ms.png | battle | end turn → the boss acts (enemy action, hit on the player, omen change) (target 0 ms, frame shown at 2 ms) | X with nothing played; time from the key |
| 663-battle-boss-enemy-turn-0500ms.png | battle | end turn → the boss acts (enemy action, hit on the player, omen change) (target 500 ms, frame shown at 499 ms) | X with nothing played; time from the key |
| 663-battle-boss-enemy-turn-1000ms.png | battle | end turn → the boss acts (enemy action, hit on the player, omen change) (target 1000 ms, frame shown at 1006 ms) | X with nothing played; time from the key |
| 663-battle-boss-enemy-turn-1500ms.png | battle | end turn → the boss acts (enemy action, hit on the player, omen change) (target 1500 ms, frame shown at 1499 ms) | X with nothing played; time from the key |
| 663-battle-boss-enemy-turn-2000ms.png | battle | end turn → the boss acts (enemy action, hit on the player, omen change) (target 2000 ms, frame shown at 1995 ms) | X with nothing played; time from the key |
| 663-battle-boss-enemy-turn-2500ms.png | battle | end turn → the boss acts (enemy action, hit on the player, omen change) (target 2500 ms, frame shown at 2504 ms) | X with nothing played; time from the key |
| 663-battle-boss-enemy-turn-3000ms.png | battle | end turn → the boss acts (enemy action, hit on the player, omen change) (target 3000 ms, frame shown at 3003 ms) | X with nothing played; time from the key |
| 663-battle-boss-enemy-turn-3500ms.png | battle | end turn → the boss acts (enemy action, hit on the player, omen change) (target 3500 ms, frame shown at 3497 ms) | X with nothing played; time from the key |
| 663-battle-boss-enemy-turn-4000ms.png | battle | end turn → the boss acts (enemy action, hit on the player, omen change) (target 4000 ms, frame shown at 3994 ms) | X with nothing played; time from the key |
| 663-battle-boss-enemy-turn-5000ms.png | battle | end turn → the boss acts (enemy action, hit on the player, omen change) (target 5000 ms, frame shown at 5001 ms) | X with nothing played; time from the key |
| 663-battle-boss-enemy-turn-6000ms.png | battle | end turn → the boss acts (enemy action, hit on the player, omen change) (target 6000 ms, frame shown at 5588 ms) | X with nothing played; time from the key |
| 663-battle-boss-enemy-turn-7000ms.png | battle | end turn → the boss acts (enemy action, hit on the player, omen change) (target 7000 ms, frame shown at 5588 ms) | X with nothing played; time from the key |
| 664-battle-player-hit-0000ms.png | battle | hit on the player: floating number 「+3」 (target 0 ms, frame shown at -4 ms) | time from the floating number attaching |
| 664-battle-player-hit-0100ms.png | battle | hit on the player: floating number 「+3」 (target 100 ms, frame shown at 99 ms) | time from the floating number attaching |
| 664-battle-player-hit-0200ms.png | battle | hit on the player: floating number 「+3」 (target 200 ms, frame shown at 200 ms) | time from the floating number attaching |
| 664-battle-player-hit-0400ms.png | battle | hit on the player: floating number 「+3」 (target 400 ms, frame shown at 404 ms) | time from the floating number attaching |
| 664-battle-player-hit-0700ms.png | battle | hit on the player: floating number 「+3」 (target 700 ms, frame shown at 699 ms) | time from the floating number attaching |
| 664-battle-player-hit-1000ms.png | battle | hit on the player: floating number 「+3」 (target 1000 ms, frame shown at 999 ms) | time from the floating number attaching |
| 665-battle-boss-turn2.png | battle | boss fight turn 2 (after the boss's action: new omen, HP lost) | after the enemy turn |
| 666-battle-result-lost-0000ms.png | battle | loss: ink, 力尽きた, the result screen rising (where, HP, cards by attribute, 手記へ) (target 0 ms, frame shown at -4 ms) | turns ended until the heir fell; time from the result element attaching |
| 666-battle-result-lost-0200ms.png | battle | loss: ink, 力尽きた, the result screen rising (where, HP, cards by attribute, 手記へ) (target 200 ms, frame shown at 201 ms) | turns ended until the heir fell; time from the result element attaching |
| 666-battle-result-lost-0400ms.png | battle | loss: ink, 力尽きた, the result screen rising (where, HP, cards by attribute, 手記へ) (target 400 ms, frame shown at 392 ms) | turns ended until the heir fell; time from the result element attaching |
| 666-battle-result-lost-0700ms.png | battle | loss: ink, 力尽きた, the result screen rising (where, HP, cards by attribute, 手記へ) (target 700 ms, frame shown at 693 ms) | turns ended until the heir fell; time from the result element attaching |
| 666-battle-result-lost-1000ms.png | battle | loss: ink, 力尽きた, the result screen rising (where, HP, cards by attribute, 手記へ) (target 1000 ms, frame shown at 993 ms) | turns ended until the heir fell; time from the result element attaching |
| 666-battle-result-lost-1500ms.png | battle | loss: ink, 力尽きた, the result screen rising (where, HP, cards by attribute, 手記へ) (target 1500 ms, frame shown at 1486 ms) | turns ended until the heir fell; time from the result element attaching |
| 666-battle-result-lost-2500ms.png | battle | loss: ink, 力尽きた, the result screen rising (where, HP, cards by attribute, 手記へ) (target 2500 ms, frame shown at 2496 ms) | turns ended until the heir fell; time from the result element attaching |
| 666-battle-result-lost-3500ms.png | battle | loss: ink, 力尽きた, the result screen rising (where, HP, cards by attribute, 手記へ) (target 3500 ms, frame shown at 3500 ms) | turns ended until the heir fell; time from the result element attaching |
| 667-battle-result-lost.png | battle | loss result screen settled (手記へ) | after the rise |
| 668-battle-result-lost-hover.png | battle | loss result: pointer over 手記へ | mouse.move onto the button |
| 758-explore-end-card-fallen-0000ms.png | exploration | end-of-life card: 力尽きた (Fallen) (target 0 ms, frame shown at 81 ms) | after 手記へ; time from the card attaching |
| 758-explore-end-card-fallen-0150ms.png | exploration | end-of-life card: 力尽きた (Fallen) (target 150 ms, frame shown at 149 ms) | after 手記へ; time from the card attaching |
| 758-explore-end-card-fallen-0300ms.png | exploration | end-of-life card: 力尽きた (Fallen) (target 300 ms, frame shown at 304 ms) | after 手記へ; time from the card attaching |
| 758-explore-end-card-fallen-0600ms.png | exploration | end-of-life card: 力尽きた (Fallen) (target 600 ms, frame shown at 607 ms) | after 手記へ; time from the card attaching |
| 758-explore-end-card-fallen-1000ms.png | exploration | end-of-life card: 力尽きた (Fallen) (target 1000 ms, frame shown at 982 ms) | after 手記へ; time from the card attaching |
| 759-explore-end-card-fallen.png | exploration | end-of-life card (Fallen) settled | after the card comes in |
| 801-ending-death-0000ms.png | ending | ending screen after a death (sheet laid down, words written, what stays behind) (target 0 ms, frame shown at -31 ms) | Enter on the end-of-life card; time from the ending root attaching |
| 801-ending-death-0300ms.png | ending | ending screen after a death (sheet laid down, words written, what stays behind) (target 300 ms, frame shown at 261 ms) | Enter on the end-of-life card; time from the ending root attaching |
| 801-ending-death-0700ms.png | ending | ending screen after a death (sheet laid down, words written, what stays behind) (target 700 ms, frame shown at 697 ms) | Enter on the end-of-life card; time from the ending root attaching |
| 801-ending-death-1200ms.png | ending | ending screen after a death (sheet laid down, words written, what stays behind) (target 1200 ms, frame shown at 1204 ms) | Enter on the end-of-life card; time from the ending root attaching |
| 801-ending-death-2000ms.png | ending | ending screen after a death (sheet laid down, words written, what stays behind) (target 2000 ms, frame shown at 2004 ms) | Enter on the end-of-life card; time from the ending root attaching |
| 801-ending-death-3000ms.png | ending | ending screen after a death (sheet laid down, words written, what stays behind) (target 3000 ms, frame shown at 2978 ms) | Enter on the end-of-life card; time from the ending root attaching |
| 801-ending-death-4000ms.png | ending | ending screen after a death (sheet laid down, words written, what stays behind) (target 4000 ms, frame shown at 3961 ms) | Enter on the end-of-life card; time from the ending root attaching |
| 801-ending-death-5500ms.png | ending | ending screen after a death (sheet laid down, words written, what stays behind) (target 5500 ms, frame shown at 5470 ms) | Enter on the end-of-life card; time from the ending root attaching |
| 801-ending-death-7000ms.png | ending | ending screen after a death (sheet laid down, words written, what stays behind) (target 7000 ms, frame shown at 6936 ms) | Enter on the end-of-life card; time from the ending root attaching |
| 801-ending-death-8500ms.png | ending | ending screen after a death (sheet laid down, words written, what stays behind) (target 8500 ms, frame shown at 8474 ms) | Enter on the end-of-life card; time from the ending root attaching |
| 802-ending-death-settled.png | ending | ending screen (death) settled: 継承の間へ | after ~9 s |
| 803-ending-death-hover-button.png | ending | pointer over 継承の間へ | mouse.move onto the button |
| 804-hall-gen2-enter-0000ms.png | hall (継承の間) | second heir: entering (past life, the trace on layer 2) (target 0 ms, frame shown at -42 ms) | ending → 継承の間へ; time from the hall root attaching |
| 804-hall-gen2-enter-0300ms.png | hall (継承の間) | second heir: entering (past life, the trace on layer 2) (target 300 ms, frame shown at 307 ms) | ending → 継承の間へ; time from the hall root attaching |
| 804-hall-gen2-enter-1000ms.png | hall (継承の間) | second heir: entering (past life, the trace on layer 2) (target 1000 ms, frame shown at 964 ms) | ending → 継承の間へ; time from the hall root attaching |
| 804-hall-gen2-enter-2000ms.png | hall (継承の間) | second heir: entering (past life, the trace on layer 2) (target 2000 ms, frame shown at 2004 ms) | ending → 継承の間へ; time from the hall root attaching |
| 804-hall-gen2-enter-4000ms.png | hall (継承の間) | second heir: entering (past life, the trace on layer 2) (target 4000 ms, frame shown at 3993 ms) | ending → 継承の間へ; time from the hall root attaching |
| 805-hall-gen2-idle.png | hall (継承の間) | second heir settled: past life card, untaken trace (flame) on layer 2, trace detail | after the entrance |
| 806-hall-gen2-hover-trace-stratum.png | hall (継承の間) | pointer over the layer card holding the trace | mouse.move onto the live stratum |
| 807-hall-gen2-hover-mark.png | hall (継承の間) | pointer over the life mark on the layer card (native title tooltip is not drawn in headless shots) | mouse.move onto the mark |
| 808-hall-gen2-hover-life.png | hall (継承の間) | pointer over the past life card | mouse.move onto the life card |

## Second heir: hall via つづきから, departure on the saved deck, swap plate (this heir then fell to the layer-1 boss under autobattle)

Script: `cap-life2.mjs`

| file | screen | state | how it was reached |
| --- | --- | --- | --- |
| 809-hall-gen2-via-continue.png | hall (継承の間) | second heir via つづきから (settled) | reload, つづきから |
| 810-deck-gen2-saved-deck.png | deck (デッキを組む) | second heir: the deck opens on the saved deck of the last departure | 継承の間 → 出立の支度へ |
| 811-supplies-gen2.png | supplies (出立の支度) | second heir supplies (talent, 防瘴の面 + 写しの帳, 薬草 + 浄化の香) | chosen with clicks |
| 823-explore-swap.png | exploration | found consumable with full slots: swap plate | reached during the walk |

## Second heir's death on layer 1 → ending → hall of the third heir (two traces); third heir's departure and first fight by hand (status chips)

Script: `cap-life3.mjs`

| file | screen | state | how it was reached |
| --- | --- | --- | --- |
| 860-explore-end-card-fallen-layer1.png | exploration | end-of-life card of the second heir (fell to the layer-1 boss under autobattle) | つづきから brings the card back |
| 861-ending-death-layer1-0000ms.png | ending | ending of a death on layer 1 (first frames) (target 0 ms, frame shown at 3 ms) | Enter on the end card; time from the ending root attaching |
| 861-ending-death-layer1-0500ms.png | ending | ending of a death on layer 1 (first frames) (target 500 ms, frame shown at 499 ms) | Enter on the end card; time from the ending root attaching |
| 861-ending-death-layer1-1000ms.png | ending | ending of a death on layer 1 (first frames) (target 1000 ms, frame shown at 1000 ms) | Enter on the end card; time from the ending root attaching |
| 861-ending-death-layer1-2000ms.png | ending | ending of a death on layer 1 (first frames) (target 2000 ms, frame shown at 1975 ms) | Enter on the end card; time from the ending root attaching |
| 862-ending-death-layer1-skipped.png | ending | ending (death on layer 1) after a click: the whole sheet at once (shallow death: high-rank cards stay out) | click on the backdrop while the words were being written |
| 863-hall-gen3-enter-0000ms.png | hall (継承の間) | third heir entering: two past lives, two traces (target 0 ms, frame shown at -25 ms) | ending → 継承の間へ; time from the hall root attaching |
| 863-hall-gen3-enter-1000ms.png | hall (継承の間) | third heir entering: two past lives, two traces (target 1000 ms, frame shown at 994 ms) | ending → 継承の間へ; time from the hall root attaching |
| 863-hall-gen3-enter-2000ms.png | hall (継承の間) | third heir entering: two past lives, two traces (target 2000 ms, frame shown at 2006 ms) | ending → 継承の間へ; time from the hall root attaching |
| 863-hall-gen3-enter-4000ms.png | hall (継承の間) | third heir entering: two past lives, two traces (target 4000 ms, frame shown at 4001 ms) | ending → 継承の間へ; time from the hall root attaching |
| 864-hall-gen3-two-traces.png | hall (継承の間) | third heir: two past lives (both fallen), traces on layers 1 and 2 (1 / 2 pager) | settled |
| 865-hall-gen3-trace2.png | hall (継承の間) | the other trace selected (→): 2 / 2 | ArrowRight |
| 866-hall-gen3-hover-stratum.png | hall (継承の間) | pointer over a layer card holding a trace | mouse.move onto the first live stratum |
| 867-deck-gen3.png | deck (デッキを組む) | third heir: saved deck + 集中 + 石礫 (the last one added is selected) | ＋ on 集中 and 石礫 |
| 868-supplies-gen3.png | supplies (出立の支度) | third heir supplies | chosen with clicks |
| 670-battle-status-chips.png | battle | status chips on the panels (威圧2 / 脆化2) | after 石礫 / 集中 |
| 671a-battle-status-chip-tooltip.png | battle | pointer over status chip 1 (威圧2): tooltip | mouse.move onto the chip |
| 671b-battle-status-chip-tooltip.png | battle | pointer over status chip 2 (脆化2): tooltip | mouse.move onto the chip |
| 672-battle-status-chip-pinned.png | battle | status chip detail pinned (right click) | right click on the chip |
| 828-explore-gen3-layer1-map.png | exploration | third heir, layer 1 map with the trace node of the second heir | after the first fight |

## Third heir again from the same hall (same map): イベント choice, 標の石 jump, the second heir's trace, この生を終える, ending (alive), hall and departure of the fourth heir

Script: `cap-life3b.mjs`

| file | screen | state | how it was reached |
| --- | --- | --- | --- |
| 869-supplies-gen3-jumpstone.png | supplies (出立の支度) | third heir (second try): consumables 薬草 + 標の石 + 浄化の香 | chosen with clicks |
| 820-explore-event-choice.png | exploration | イベント choice plate: 特殊な食事 (受ける / 受けない tiles) | arrived on the 特殊な食事 node |
| 821-explore-event-choice-hover.png | exploration | イベント choice: pointer over tile 1 (受ける) | mouse.move onto the first tile |
| 822-explore-event-choice-result.png | exploration | イベント result after 受ける (max stamina +1 for HP) | key 1 on the choice |
| 824-explore-jumpstone-aiming.png | exploration | 標の石 being aimed: the nodes it can land on are marked | click the 標の石 slot |
| 825-explore-jumpstone-target.png | exploration | 標の石: the trace node pinned with its jump option | click the trace node while aiming |
| 826-explore-jump-leap-0000ms.png | exploration | the 標の石 leap (walker jumps instead of walking) (target 0 ms, frame shown at -6 ms) | click the jump option; time from the click |
| 826-explore-jump-leap-0100ms.png | exploration | the 標の石 leap (walker jumps instead of walking) (target 100 ms, frame shown at 94 ms) | click the jump option; time from the click |
| 826-explore-jump-leap-0200ms.png | exploration | the 標の石 leap (walker jumps instead of walking) (target 200 ms, frame shown at 194 ms) | click the jump option; time from the click |
| 826-explore-jump-leap-0350ms.png | exploration | the 標の石 leap (walker jumps instead of walking) (target 350 ms, frame shown at 348 ms) | click the jump option; time from the click |
| 826-explore-jump-leap-0500ms.png | exploration | the 標の石 leap (walker jumps instead of walking) (target 500 ms, frame shown at 492 ms) | click the jump option; time from the click |
| 826-explore-jump-leap-0700ms.png | exploration | the 標の石 leap (walker jumps instead of walking) (target 700 ms, frame shown at 706 ms) | click the jump option; time from the click |
| 826-explore-jump-leap-1000ms.png | exploration | the 標の石 leap (walker jumps instead of walking) (target 1000 ms, frame shown at 993 ms) | click the jump option; time from the click |
| 826-explore-jump-leap-1500ms.png | exploration | the 標の石 leap (walker jumps instead of walking) (target 1500 ms, frame shown at 1489 ms) | click the jump option; time from the click |
| 827-explore-jump-leap-from-start-0000ms.png | exploration | leap frames from the walker leaving (target 0 ms, frame shown at -13 ms) | time from the walker's is-leaping class |
| 827-explore-jump-leap-from-start-0100ms.png | exploration | leap frames from the walker leaving (target 100 ms, frame shown at 88 ms) | time from the walker's is-leaping class |
| 827-explore-jump-leap-from-start-0200ms.png | exploration | leap frames from the walker leaving (target 200 ms, frame shown at 191 ms) | time from the walker's is-leaping class |
| 827-explore-jump-leap-from-start-0300ms.png | exploration | leap frames from the walker leaving (target 300 ms, frame shown at 291 ms) | time from the walker's is-leaping class |
| 827-explore-jump-leap-from-start-0450ms.png | exploration | leap frames from the walker leaving (target 450 ms, frame shown at 456 ms) | time from the walker's is-leaping class |
| 827-explore-jump-leap-from-start-0600ms.png | exploration | leap frames from the walker leaving (target 600 ms, frame shown at 599 ms) | time from the walker's is-leaping class |
| 830-explore-trace-card.png | exploration | the trace of the second heir (死亡地点の痕跡): candidates, nothing picked | landed on the trace node |
| 831-explore-trace-cursor.png | exploration | trace card: cursor moved onto a candidate row (↓) | ArrowDown |
| 832-explore-trace-picked.png | exploration | trace card: two candidates picked (Space) | Space, ↓↓, Space |
| 833-explore-trace-hover-row.png | exploration | trace card: pointer over a candidate row | mouse.move onto row 4 |
| 834-explore-trace-leave-confirm.png | exploration | trace card: 「何も受け取らない」 asks first (the trace is collected either way) | Esc on the trace card |
| 835-explore-trace-take-0000ms.png | exploration | taking the picks: the trace card goes, 受け取った遺産 comes (target 0 ms, frame shown at -2 ms) | Enter on the trace card |
| 835-explore-trace-take-0200ms.png | exploration | taking the picks: the trace card goes, 受け取った遺産 comes (target 200 ms, frame shown at 197 ms) | Enter on the trace card |
| 835-explore-trace-take-0400ms.png | exploration | taking the picks: the trace card goes, 受け取った遺産 comes (target 400 ms, frame shown at 384 ms) | Enter on the trace card |
| 835-explore-trace-take-0700ms.png | exploration | taking the picks: the trace card goes, 受け取った遺産 comes (target 700 ms, frame shown at 691 ms) | Enter on the trace card |
| 835-explore-trace-take-1000ms.png | exploration | taking the picks: the trace card goes, 受け取った遺産 comes (target 1000 ms, frame shown at 993 ms) | Enter on the trace card |
| 835-explore-trace-take-1500ms.png | exploration | taking the picks: the trace card goes, 受け取った遺産 comes (target 1500 ms, frame shown at 1507 ms) | Enter on the trace card |
| 835-explore-trace-take-2000ms.png | exploration | taking the picks: the trace card goes, 受け取った遺産 comes (target 2000 ms, frame shown at 2002 ms) | Enter on the trace card |
| 836-explore-trace-taken.png | exploration | 受け取った遺産 card (what joined this heir) | after the take |
| 840-explore-confirm-endlife.png | exploration | この生を終えますか (confirm, after the trace) | click この生を終える |
| 841-explore-end-card-survived-0000ms.png | exploration | end-of-life card: closed alive (Survived) (target 0 ms, frame shown at -10 ms) | Enter on the confirmation |
| 841-explore-end-card-survived-0150ms.png | exploration | end-of-life card: closed alive (Survived) (target 150 ms, frame shown at 137 ms) | Enter on the confirmation |
| 841-explore-end-card-survived-0300ms.png | exploration | end-of-life card: closed alive (Survived) (target 300 ms, frame shown at 306 ms) | Enter on the confirmation |
| 841-explore-end-card-survived-0600ms.png | exploration | end-of-life card: closed alive (Survived) (target 600 ms, frame shown at 593 ms) | Enter on the confirmation |
| 841-explore-end-card-survived-1000ms.png | exploration | end-of-life card: closed alive (Survived) (target 1000 ms, frame shown at 999 ms) | Enter on the confirmation |
| 841-explore-end-card-survived-1600ms.png | exploration | end-of-life card: closed alive (Survived) (target 1600 ms, frame shown at 1596 ms) | Enter on the confirmation |
| 842-explore-end-card-survived.png | exploration | end-of-life card (Survived) settled | after the card comes in |
| 850-ending-alive-0000ms.png | ending | ending screen after a life closed alive (warm light, what passes on) (target 0 ms, frame shown at 3 ms) | Enter on the survived card; time from the ending root attaching |
| 850-ending-alive-0300ms.png | ending | ending screen after a life closed alive (warm light, what passes on) (target 300 ms, frame shown at 298 ms) | Enter on the survived card; time from the ending root attaching |
| 850-ending-alive-0700ms.png | ending | ending screen after a life closed alive (warm light, what passes on) (target 700 ms, frame shown at 685 ms) | Enter on the survived card; time from the ending root attaching |
| 850-ending-alive-1200ms.png | ending | ending screen after a life closed alive (warm light, what passes on) (target 1200 ms, frame shown at 1199 ms) | Enter on the survived card; time from the ending root attaching |
| 850-ending-alive-2000ms.png | ending | ending screen after a life closed alive (warm light, what passes on) (target 2000 ms, frame shown at 1984 ms) | Enter on the survived card; time from the ending root attaching |
| 850-ending-alive-3000ms.png | ending | ending screen after a life closed alive (warm light, what passes on) (target 3000 ms, frame shown at 2982 ms) | Enter on the survived card; time from the ending root attaching |
| 850-ending-alive-4000ms.png | ending | ending screen after a life closed alive (warm light, what passes on) (target 4000 ms, frame shown at 3978 ms) | Enter on the survived card; time from the ending root attaching |
| 850-ending-alive-5500ms.png | ending | ending screen after a life closed alive (warm light, what passes on) (target 5500 ms, frame shown at 5498 ms) | Enter on the survived card; time from the ending root attaching |
| 850-ending-alive-7000ms.png | ending | ending screen after a life closed alive (warm light, what passes on) (target 7000 ms, frame shown at 7003 ms) | Enter on the survived card; time from the ending root attaching |
| 850-ending-alive-8500ms.png | ending | ending screen after a life closed alive (warm light, what passes on) (target 8500 ms, frame shown at 8485 ms) | Enter on the survived card; time from the ending root attaching |
| 851-ending-alive-settled.png | ending | ending screen (alive) settled | after ~9 s |
| 852-hall-gen4-enter-0000ms.png | hall (継承の間) | fourth heir entering: three past lives, the survivor's bonus (target 0 ms, frame shown at -24 ms) | ending → 継承の間へ; time from the hall root attaching |
| 852-hall-gen4-enter-0300ms.png | hall (継承の間) | fourth heir entering: three past lives, the survivor's bonus (target 300 ms, frame shown at 295 ms) | ending → 継承の間へ; time from the hall root attaching |
| 852-hall-gen4-enter-1000ms.png | hall (継承の間) | fourth heir entering: three past lives, the survivor's bonus (target 1000 ms, frame shown at 1000 ms) | ending → 継承の間へ; time from the hall root attaching |
| 852-hall-gen4-enter-2000ms.png | hall (継承の間) | fourth heir entering: three past lives, the survivor's bonus (target 2000 ms, frame shown at 1986 ms) | ending → 継承の間へ; time from the hall root attaching |
| 852-hall-gen4-enter-4000ms.png | hall (継承の間) | fourth heir entering: three past lives, the survivor's bonus (target 4000 ms, frame shown at 3995 ms) | ending → 継承の間へ; time from the hall root attaching |
| 853-hall-gen4-idle.png | hall (継承の間) | fourth heir settled: three past lives (fallen, fallen, survived), survivor bonus block, the remaining trace | after the entrance |
| 854-hall-gen4-hover-life.png | hall (継承の間) | pointer over the newest past life | mouse.move onto the first life card |
| 855-deck-gen4-survivor-bonus.png | deck (デッキを組む) | fourth heir: deck limits raised by the survivor bonus (bonus chip by the title) | 継承の間 → 出立の支度へ |
| 856-supplies-gen4-survivor-bonus.png | supplies (出立の支度) | fourth heir supplies: the survivor bonus slot | 準備へ |

## Not captured, or captured differently

- Rest choice: the rest node has no choice plate in this build. Its two ways in (休む / 訓練する) are the buttons of the node's preview panel (727); 訓練する then opens the training card (732–734).
- 情報収集 choice plate exists only when the life carries 写しの帳, so the first heir took it (720–722). Without it the survey only shows a note banner.
- Event choice plate: the events on the first heir's layer-1 path were 泉 (no choice; 726 is its result card). The choice plate (特殊な食事) is the third heir's (820–822).
- Status chip tooltip: no status appeared in the first heir's fights (the opening fight was won on turn 2; the layer-2 boss put none on the heir). The chips are from the third heir's first fight (670–672), after 石礫 (威圧 2 / 脆化 2 on the enemy); 集中 and 石礫 were added to that deck for this.
- Battle loss: forced on the first heir's layer-2 boss by ending every turn with nothing played (660–668). The loss result card waits for 手記へ.
- Trace flow: the first heir's trace on layer 2 was never reached. The second heir fell to the layer-1 boss under autobattle (860–862 are its end card and ending), and the third heir's first try (cap-life3.mjs) fell to an elite under autobattle after the chip shots. The second try (cap-life3b.mjs) started again from the same hall checkpoint (same life seed, same map), carried 標の石 and jumped over the elite onto the second heir's trace on layer 1 (824–836). The first heir's trace stays uncollected (853).
- Ending screens: a death on layer 2 (801–803, played through), a death on layer 1 (861–862, the second shot after the click that lays the whole sheet down), a life closed alive (850–851).
- Native title tooltips (the hall's life marks, 807) are browser tooltips and are not drawn in headless screenshots; the shot shows the hover state only.
- Not reached: the training card before a descent (is-descend; the only mark of layer 1 was spent at the rest), the confirmation before a step into 瘴気死 (miasma never passed 10 %), layers 3–7, a fight with two or three enemies, 俊敏's free step, and a cleared run (Completed).
- The enemy's action and the omen change are inside the end-turn sequences (624-…-from-x, 624-…-enemy-banner, 663-…): the enemy moves and gains Guard, and the omen badge changes 移動 → 攻撃 by the next turn (compare 603 / 625 / 627).
- Title intro times are measured from the title root attaching (the game boots after document.fonts.ready), not from navigation start.
- Settings in battle open on the Esc key's release (keyup), as the game does (614).
- Each life's map and fights depend on the lineage seed, which the game draws from Date.now() and Math.random() when はじめから is pressed. cap-meta.mjs pins both for that one click (seed 789591033); every later section starts from a localStorage checkpoint, so re-running a section reproduces it.
