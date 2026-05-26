# Journal System Implementation Plan

## Overview
手記（Journal）システムをヘッダーUIからアクセス可能なオーバーレイとして実装し、図書館施設を削除する。

## User Decisions
- Settings: 手記に統合（SettingsModal廃止）
- DeckTab: 既存コンポーネント再利用
- 優先順位: 戦術（デッキ）ページ優先
- スタイル: 羊皮紙風

## Session Structure
**各Phaseは独立したセッションで実装する。**
- Phase 1 → Session 1 ✅ **完了**
- Phase 2 → Session 2 ✅ **完了**
- Phase 3 → Session 3 (次回)
- Phase 4 → Session 4
- Phase 5 → Session 5

---

## Phase 1: Foundation + Tactics Page ✅ COMPLETED

**完了日: 2026-02-05**

### 実装済みファイル

| ファイル | 状態 |
|----------|------|
| `src/types/journalTypes.ts` | ✅ 作成済み |
| `src/contexts/JournalContext.tsx` | ✅ 作成済み |
| `src/ui/css/journal/Journal.css` | ✅ 作成済み |
| `src/ui/html/journalHtml/JournalOverlay.tsx` | ✅ 作成済み |
| `src/ui/html/journalHtml/components/PageTabs.tsx` | ✅ 作成済み |
| `src/ui/html/journalHtml/pages/TacticsPage.tsx` | ✅ 作成済み |
| `src/App.tsx` | ✅ 修正済み (JournalProvider, JournalOverlay追加) |
| `src/ui/html/componentsHtml/FacilityHeader.tsx` | ✅ 修正済み (📖ボタン追加) |

### 検証結果
- [x] `npm run build` パス
- [x] Journal関連のlintエラーなし
- [x] Dev server起動可能
- [ ] Journal opens from header on all screens (要手動確認)
- [ ] Page tabs navigate correctly (要手動確認)
- [ ] Tactics shows current deck (要手動確認)
- [ ] Deck editing works outside dungeon (要手動確認)
- [ ] Read-only mode active during dungeon/battle (要手動確認)

---

## Phase 2: Memories (Encyclopedia) Page ✅ COMPLETED

**完了日: 2026-02-06**

### 実装済みファイル

| ファイル | 状態 |
|----------|------|
| `src/constants/data/journal/CardEncyclopediaData.ts` | ✅ 作成済み (re-export from camps) |
| `src/constants/data/journal/EnemyEncyclopediaData.ts` | ✅ 作成済み (re-export from camps) |
| `src/constants/data/journal/GameTipsData.ts` | ✅ 作成済み (re-export from camps) |
| `src/constants/data/journal/EquipmentEncyclopediaData.ts` | ✅ 作成済み (new) |
| `src/constants/data/journal/EventEncyclopediaData.ts` | ✅ 作成済み (new) |
| `src/constants/data/journal/index.ts` | ✅ 作成済み (barrel export) |
| `src/ui/html/journalHtml/pages/MemoriesPage.tsx` | ✅ 作成済み |
| `src/ui/css/journal/Memories.css` | ✅ 作成済み |
| `src/ui/html/journalHtml/JournalOverlay.tsx` | ✅ 修正済み (MemoriesPage統合) |

### 検証結果
- [x] `npm run build` パス
- [x] Journal関連のlintエラーなし
- [ ] Memories displays all categories (要手動確認)
- [ ] Discovery data persists to localStorage (要手動確認)
- [ ] Progress percentage updates correctly (要手動確認)

---

## Phase 3: Settings Integration - 未実装

### 3.1 SettingsPage
**Create**: `src/ui/html/journalHtml/pages/SettingsPage.tsx`
- Import existing: `SoundSettings`, `BrightnessSettings`, `SaveLoadUI`, `AchievementList`
- Sub-tabs for settings categories

### 3.2 SaveLoadUI Modification
**Modify**: `src/ui/html/componentsHtml/SettingsPanels/SaveLoadUI.tsx`
- Add `loadDisabled?: boolean` prop
- Show disabled state with reason during dungeon

### 3.3 Deprecate SettingsModal
**Modify**: `FacilityHeader.tsx`
- Replace settings button action → openJournal('settings')
- Remove SettingsModal import

---

## Phase 4: Notes (Thoughts) + Polish - 未実装

### 4.1 ThoughtsPage
**Create**: `src/ui/html/journalHtml/pages/ThoughtsPage.tsx`
- Note list with timestamps
- Add/Edit/Delete functionality

### 4.2 NoteEditor Component
**Create**: `src/ui/html/journalHtml/components/NoteEditor.tsx`
- Textarea with save/cancel
- Full access always

### 4.3 Notes Persistence
**Update**: `JournalContext.tsx`
- addNote, updateNote, deleteNote (既にPhase 1で実装済み)
- LocalStorage persistence (既にPhase 1で実装済み)

### 4.4 Visual Polish
**Create**: `src/ui/css/journal/JournalAnimations.css`
- Page-turn effect on chapter change
- Subtle paper texture animations

---

## Phase 5: Library Deletion & Cleanup - 未実装

### Files to Delete
```
src/ui/html/campsHtml/Library/Library.tsx
src/ui/html/campsHtml/Library/CardEncyclopediaTab.tsx
src/ui/html/campsHtml/Library/EnemyEncyclopediaTab.tsx
src/ui/html/campsHtml/Library/GameTipsTab.tsx
src/ui/html/campsHtml/Library/CardCategoryRow.tsx
src/ui/html/campsHtml/Library/CardDerivationTree.tsx
src/ui/css/camps/Library.css
```

### References to Remove
- `src/App.tsx`: Remove Library import and route
- `src/constants/campConstants.ts`: Remove library from FACILITY_NAV_ITEMS, FACILITY_ISOMETRIC_POSITIONS
- `src/types/campTypes.ts`: Remove "library" from FacilityType, GameScreen, FacilityUnlockState; Remove LibraryTab type
- `src/types/index.ts`: Remove library-related type exports

---

## Key Reusable Code

| Component | Location | Usage |
|-----------|----------|-------|
| DeckTab | `src/ui/html/dungeonHtml/preparations/DeckTab.tsx` | Tactics page ✅ |
| SoundSettings | `src/ui/html/componentsHtml/SettingsPanels/SoundSettings.tsx` | Settings page |
| BrightnessSettings | `src/ui/html/componentsHtml/SettingsPanels/BrightnessSettings.tsx` | Settings page |
| SaveLoadUI | `src/ui/html/componentsHtml/SettingsPanels/SaveLoadUI.tsx` | Settings page |
| AchievementList | `src/ui/html/componentsHtml/SettingsPanels/AchievementList.tsx` | Settings page |
| SettingsModal pattern | `src/ui/html/componentsHtml/SettingsModal.tsx` | Overlay structure |

---

## Verification Checklist

### Phase 1 Tests ✅
- [x] `npm run build` passes
- [x] No lint errors in Journal files
- [ ] Journal opens from header on all screens
- [ ] Page tabs navigate correctly
- [ ] Tactics shows current deck
- [ ] Deck editing works outside dungeon
- [ ] Read-only mode active during dungeon/battle

### Phase 2 Tests ✅
- [x] `npm run build` passes
- [x] No lint errors in Journal files
- [ ] Memories displays all categories
- [ ] Discovery data persists to localStorage
- [ ] Progress percentage updates correctly

### Phase 3 Tests
- [ ] Settings displays all existing panels
- [ ] Load button disabled during dungeon
- [ ] Settings toggle redirects to Journal

### Phase 4 Tests
- [ ] Notes CRUD operations work
- [ ] Notes persist to localStorage
- [ ] Animations play smoothly

### Final Tests
- [ ] Library removed completely
- [ ] No broken imports
- [ ] `npm run build` passes
- [ ] `npm run lint` passes
