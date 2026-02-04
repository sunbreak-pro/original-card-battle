# Code Quality Improvement Plan

## Overview
コード品質向上のための実装計画書。テストフレームワーク導入、ロギング改善、エラーハンドリング強化、コンポーネント分割を段階的に実施。

---

## Priority: HIGH

### 1. テストフレームワーク導入

**インストール:**
```bash
npm install -D vitest @testing-library/react @testing-library/jest-dom jsdom
```

**設定ファイル:** `vitest.config.ts`
```typescript
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
  },
  resolve: {
    alias: { '@': '/src' },
  },
});
```

**テスト対象（優先順）:**

| ファイル | 関数 | テストケース数（目安） |
|----------|------|----------------------|
| `src/domain/battles/calculators/damageCalculation.ts` | resolveDamageType, calculateDamage, applyDamageAllocation | 15-20 |
| `src/domain/cards/decks/deck.ts` | drawCards, shuffleDiscardIntoDraw, createInitialDeck | 10-15 |

**重点テストケース:**
- `applyDamageAllocation`: ガード→AP→HPのブリードスルー配分（6パターン）
- `drawCards`: 山札枯渇時の捨て札リサイクル
- `createInitialDeck`: インスタンスID生成の一意性

**テストファイル配置:** `src/domain/**/__tests__/*.test.ts`

---

### 2. console.log 削除 & Logger導入

**現状:**
| ファイル | 箇所 | 種類 |
|----------|------|------|
| `src/ui/battleHtml/BattleScreen.tsx` | 243, 258, 319行 | console.log（アイテム使用） |
| `src/domain/save/logic/saveManager.ts` | 42, 86, 157, 165行 | console.error（エラー処理） |

**Logger実装:** `src/utils/logger.ts`
```typescript
const isDev = import.meta.env.DEV;

export const logger = {
  debug: (...args: unknown[]) => isDev && console.log('[DEBUG]', ...args),
  info: (...args: unknown[]) => isDev && console.info('[INFO]', ...args),
  warn: (...args: unknown[]) => console.warn('[WARN]', ...args),
  error: (...args: unknown[]) => console.error('[ERROR]', ...args),
};
```

**対応方針:**
- BattleScreen.tsx: `console.log` → `logger.debug` に置換
- saveManager.ts: `console.error` → `logger.error` に置換（本番でも出力維持）

---

### 3. ErrorBoundary 追加

**実装:** `src/ui/components/ErrorBoundary.tsx`

```typescript
interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

class ErrorBoundary extends Component<Props, State> {
  // componentDidCatch でエラーログ
  // render で fallback または children
}
```

**ErrorScreen:** `src/ui/components/ErrorScreen.tsx`
- 「エラーが発生しました」メッセージ
- 「ホームに戻る」ボタン（window.location.reload）
- エラー詳細（DEV環境のみ表示）

**適用箇所:** `App.tsx` のルート

```tsx
<ErrorBoundary fallback={<ErrorScreen />}>
  <GameStateProvider>
    ...
  </GameStateProvider>
</ErrorBoundary>
```

---

## Priority: MEDIUM

### 4. BattleScreen.tsx コンポーネント分割

**現状:** 725行（ロジック335行 + JSX390行）

**分割計画:**

| 抽出先 | 元の行番号 | 責務 |
|--------|-----------|------|
| `useEscapeLogic.ts` | 335-385 | 逃走確率計算、成功/失敗処理 |
| `useItemUsage.ts` | 238-333 | アイテム効果適用、在庫更新 |
| `useVictoryRewards.ts` | 390-452 | マスタリー収集、ソウル獲得 |
| `useDefeatHandling.ts` | 193-210, 454-467 | 死亡報酬、ゲームオーバー処理 |

**配置:** `src/domain/battles/hooks/`

**分割後のBattleScreen.tsx:** 約300-350行（JSX中心）

---

### 5. ID生成の改善

**現状のID生成箇所:**
| ファイル | 行 | 現在のパターン |
|----------|-----|---------------|
| `dungeonLogic.ts` | 26 | `run-${Date.now()}-${Math.random()...}` |
| `enemyUtils.ts` | 13 | `${id}_${Date.now()}_${Math.random()...}` |
| `generateItem.ts` | 16 | `${typeId}_${Date.now()}_${Math.random()...}` |
| `PlayerContext.tsx` | 274 | `player_${Date.now()}` |
| `summonSystem.ts` | 199 | `${summonId}_${Date.now()}` |

**改善案:** `src/utils/idGenerator.ts`
```typescript
export const generateId = (prefix?: string): string => {
  const uuid = crypto.randomUUID();
  return prefix ? `${prefix}_${uuid}` : uuid;
};
```

**移行:** 各ファイルで `generateId()` を使用

---

### 6. parseInt radix追加

**対象:** `src/ui/html/campsHtml/Shop/ExchangeTab.tsx:156`

```typescript
// Before
parseInt(e.target.value)

// After
parseInt(e.target.value, 10)
```

---

## Priority: LOW

### 7. ドキュメント整備

**作成ファイル:**
- `.claude/docs/INDEX.md` - ドキュメント一覧と概要
- `.claude/skills/README.md` - スキル一覧と使用方法

---

### 8. CSS方針検討（要調査）

**調査項目:**
- 現在のCSS構成（グローバルCSS、コンポーネント単位CSS）
- CSS Modulesへの移行コスト
- styled-components / emotion の検討

**結論:** 調査後に別途計画書を作成

---

## Implementation Order

```
Phase 1: Logger導入 → console.log置換 → ErrorBoundary追加
Phase 2: Vitest導入 → damageCalculation テスト → deck テスト
Phase 3: BattleScreen分割（useEscapeLogic, useItemUsage優先）
Phase 4: ID生成改善 → parseInt修正
Phase 5: ドキュメント整備
```

---

## Verification

### Phase 1 検証
```bash
npm run build  # ビルドエラーなし
npm run dev    # DEV環境でログ出力確認
# 本番ビルドでdebugログが出力されないこと確認
```

### Phase 2 検証
```bash
npm run test   # 全テストパス
npm run test -- --coverage  # カバレッジ確認
```

### Phase 3 検証
- BattleScreenでのアイテム使用が正常動作
- 逃走処理が正常動作
- 勝利/敗北フローが正常動作

### Phase 4 検証
- 各ID生成箇所でUUID形式のIDが生成されること
- 既存セーブデータとの互換性確認

---

## Files to Modify

**新規作成:**
- `vitest.config.ts`
- `src/test/setup.ts`
- `src/utils/logger.ts`
- `src/utils/idGenerator.ts`
- `src/ui/components/ErrorBoundary.tsx`
- `src/ui/components/ErrorScreen.tsx`
- `src/domain/battles/calculators/__tests__/damageCalculation.test.ts`
- `src/domain/cards/decks/__tests__/deck.test.ts`
- `src/domain/battles/hooks/useEscapeLogic.ts`
- `src/domain/battles/hooks/useItemUsage.ts`
- `.claude/docs/INDEX.md`
- `.claude/skills/README.md`

**修正:**
- `package.json` - test script追加
- `App.tsx` - ErrorBoundary適用
- `src/ui/battleHtml/BattleScreen.tsx` - logger使用、hooks抽出
- `src/domain/save/logic/saveManager.ts` - logger使用
- `src/ui/html/campsHtml/Shop/ExchangeTab.tsx` - parseInt radix
- `src/domain/dungeon/logic/dungeonLogic.ts` - generateId使用
- `src/domain/characters/enemy/logic/enemyUtils.ts` - generateId使用
- `src/domain/item_equipment/logic/generateItem.ts` - generateId使用
- `src/contexts/PlayerContext.tsx` - generateId使用
- `src/domain/characters/player/logic/summonSystem.ts` - generateId使用
