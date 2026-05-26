// ExplorationJournalTab - Journal sub-tabs during dungeon exploration

import { useState } from "react";
import { usePlayer } from "@/contexts/PlayerContext";
import { useGameState } from "@/contexts/GameStateContext";
import { DeckReadOnlyView } from "../preparations/DeckReadOnlyView";
import { MemoriesPage } from "@/ui/html/journalHtml/pages/MemoriesPage";
import { ThoughtsPage } from "@/ui/html/journalHtml/pages/ThoughtsPage";
import type { Depth } from "@/types/cardTypes";

type JournalSubTab = "deck" | "memories" | "thoughts";

const SUB_TAB_LABELS: Record<JournalSubTab, string> = {
  deck: "デッキ",
  memories: "記憶",
  thoughts: "思考",
};

export function ExplorationJournalTab() {
  const [activeSubTab, setActiveSubTab] = useState<JournalSubTab>("deck");
  const { deckCards } = usePlayer();
  const { gameState } = useGameState();
  const depth = (gameState.depth || 1) as Depth;

  return (
    <div className="exploration-journal-tab">
      {/* Sub-tab Navigation */}
      <div className="exploration-journal-sub-tabs">
        {(["deck", "memories", "thoughts"] as JournalSubTab[]).map((tab) => (
          <button
            key={tab}
            className={`exploration-journal-sub-btn${activeSubTab === tab ? " active" : ""}`}
            onClick={() => setActiveSubTab(tab)}
          >
            {SUB_TAB_LABELS[tab]}
          </button>
        ))}
      </div>

      {/* Sub-tab Content */}
      <div className="exploration-journal-content">
        {activeSubTab === "deck" && (
          <div className="exploration-journal-deck-wrapper">
            <DeckReadOnlyView
              deckCards={deckCards}
              depth={depth}
              showEditButton={false}
            />
          </div>
        )}
        {activeSubTab === "memories" && (
          <div className="exploration-journal-page-wrapper">
            <MemoriesPage />
          </div>
        )}
        {activeSubTab === "thoughts" && (
          <div className="exploration-journal-page-wrapper">
            <ThoughtsPage />
          </div>
        )}
      </div>
    </div>
  );
}
