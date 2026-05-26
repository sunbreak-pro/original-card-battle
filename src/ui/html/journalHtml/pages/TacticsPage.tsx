/**
 * TacticsPage Component
 *
 * Deck management page within the Journal.
 * Reuses DeckTab component with read-only mode during dungeon/battle.
 */

import { useMemo } from "react";
import { usePlayer } from "@/contexts/PlayerContext";
import { useGameState } from "@/contexts/GameStateContext";
import { useDungeonRun } from "@/contexts/DungeonRunContext";
import { DeckTab } from "@/ui/html/dungeonHtml/preparations/DeckTab";
import type { Depth } from "@/types/cardTypes";
import "@/ui/css/journal/TacticsPage.css";

/**
 * Determines if deck editing should be disabled
 */
function useReadOnlyMode(): { isReadOnly: boolean; reason: string } {
  const { gameState } = useGameState();
  const { dungeonRun } = useDungeonRun();

  return useMemo(() => {
    // During battle
    if (gameState.currentScreen === "battle") {
      return { isReadOnly: true, reason: "戦闘中" };
    }

    // During dungeon exploration
    if (dungeonRun?.isActive) {
      return { isReadOnly: true, reason: "ダンジョン探索中" };
    }

    // Full editing allowed
    return { isReadOnly: false, reason: "" };
  }, [gameState.currentScreen, dungeonRun?.isActive]);
}

export function TacticsPage() {
  const { playerData, deckCards, updateDeck } = usePlayer();
  const { gameState } = useGameState();
  const { isReadOnly, reason } = useReadOnlyMode();

  // Get current depth for card display (use gameState depth or default to 1)
  const currentDepth: Depth = gameState.depth;

  // Handle deck updates (only if not read-only)
  const handleUpdateDeck = (cardTypeIds: string[]) => {
    if (isReadOnly) return;
    updateDeck(cardTypeIds);
  };

  return (
    <div
      className="journal-page"
      id="journal-page-tactics"
      role="tabpanel"
      aria-labelledby="journal-tab-tactics"
    >
      {/* Read-only badge */}
      {isReadOnly && (
        <div className="journal-readonly-badge">
          <span className="journal-readonly-icon">🔒</span>
          <span>{reason} - 閲覧のみ</span>
        </div>
      )}

      {/* Deck description */}
      <section className="journal-section">
        <h3 className="journal-section-title">デッキ構成</h3>
        <p style={{ color: "var(--journal-text-secondary)", fontSize: "1.3vh", marginBottom: "1vh" }}>
          戦闘で使用するカードを編集できます。
          {isReadOnly && " 現在は閲覧のみ可能です。"}
        </p>
      </section>

      {/* Deck Editor - wrapped with read-only state */}
      <div
        className={`tactics-deck-wrapper ${isReadOnly ? "read-only" : ""}`}
        style={{
          pointerEvents: isReadOnly ? "none" : "auto",
          opacity: isReadOnly ? 0.85 : 1,
        }}
      >
        <DeckTab
          deckCards={deckCards}
          playerClass={playerData.persistent.playerClass}
          depth={currentDepth}
          onUpdateDeck={handleUpdateDeck}
        />
      </div>
    </div>
  );
}
