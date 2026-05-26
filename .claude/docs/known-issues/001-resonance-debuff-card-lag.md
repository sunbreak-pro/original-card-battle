# 001 — Resonance debuff の 1-card-lag 非対称

- **Status**: Open
- **発見**: 2026-05-17（V-CHAIN-01 修正の role-qa 独立監査で検出）
- **カテゴリ**: Battle / Class Ability（魔術師 元素共鳴）
- **関連**: V-CHAIN-01（damage modifier 側は修正済）

## Root Cause

V-CHAIN-01 修正で「カードの**ダメージ**は現在カードの元素を含めた共鳴レベルで算出」されるよう play-aware 化した（`elementalSystem.getDamageModifierIncludingCard` → `useElementalChain.getDamageModifierForPlay`）。

しかし、共鳴が敵に付与する**デバフ**（burn / freeze / stun 等）を生成する `getResonanceEffects` クロージャは依然プレイ前の `elementalAbilityState`（stale state）を読む。

- 生成: `src/domain/battles/managements/useBattleOrchestrator.ts:377-388`
- 消費: `src/domain/battles/managements/useCardExecution.ts:567`
- 順序: `handleCardPlay`（`useBattleOrchestrator.ts:481-487`）が `executeCard()` → `onCardPlayed()` の順。`executeCard` 内で参照する resonance state はプレイ前のまま

## Impact

挙動の非対称。プレイヤー視点で「3枚目の炎で大共鳴ダメージは出るのに、burn スタックは大共鳴相当にならず2枚目相当」というズレ。データ破壊や進行不能はなし（体験のわかりにくさ＝中程度）。V-CHAIN-01 で片側だけ直したことで非対称が顕在化したため、未修正のまま閉じると技術的負債が不可視化する。

## Fix（提案・未実装）

V-CHAIN-01 と同パターン:

1. `elementalSystem` に `getResonanceEffectsIncludingCard(state, card)`（= `onCardPlay(state, card)` 後の仮想 state から resonance effects を導出）を純粋関数で追加
2. `useElementalChain` に `getResonanceEffectsForPlay(card)` を生やす
3. `useCardExecution` の resonance effects 取得を、同一 `card` 引数で play-aware 経路へ切り替え

純粋関数の追加が主で副作用面積は小さい。swordsman・非カードプレイ経路は不変に保つこと。

## Lessons

- 「1枚遅れ（stale state lag）」バグを片側だけ直すと**非対称**という新しいバグになる。共鳴のように「ダメージ」と「デバフ付与」が同じ state を参照する場合、両経路を同時に play-aware 化するか、両方据え置くかのどちらかにする
- React state は同期更新されない。`onCardPlayed` を先に呼ぶだけでは解決せず、`onCardPlay` 後の仮想 state を純粋関数で同期計算して渡すのが定石
