// DungeonGate - Invasion preparations screen

import { useState, useCallback } from "react";
import type { Depth } from "@/types/campTypes";
import { useGameState } from "../../contexts/GameStateContext";
import { usePlayer } from "../../contexts/PlayerContext";
import { DEPTH_DISPLAY_INFO } from "../../constants/dungeonConstants";
import FacilityHeader from "../componentsHtml/FacilityHeader";
import { DepthSelector } from "./preparations/DepthSelector";
import { PlayerStatusPanel } from "./preparations/PlayerStatusPanel";
import { DeckTab } from "./preparations/DeckTab";
import { InventoryTab } from "./preparations/InventoryTab";
import { EquipmentTab } from "./preparations/EquipmentTab";
import "./DungeonGate.css";

type PreparationTab = "deck" | "inventory" | "equipment";

/**
 * DungeonGate Component
 * Tabbed invasion preparation screen where players review loadout before dungeon entry
 */
export function DungeonGate() {
  const { returnToCamp, navigateTo, setDepth, gameState } = useGameState();
  const { playerData, runtimeState, deckCards, updateDeck, resetRuntimeState } =
    usePlayer();
  const [selectedDepth, setSelectedDepth] = useState<Depth | null>(null);
  const [activeTab, setActiveTab] = useState<PreparationTab>("deck");

  const handleDepthSelect = useCallback((depth: Depth) => {
    setSelectedDepth(depth);
  }, []);

  const handleStartExploration = useCallback(() => {
    if (selectedDepth !== null) {
      resetRuntimeState();
      setDepth(selectedDepth);
      navigateTo("dungeon_map");
    }
  }, [selectedDepth, setDepth, navigateTo, resetRuntimeState]);

  const handleNavigateToStorage = useCallback(() => {
    navigateTo("storage");
  }, [navigateTo]);

  const handleUpdateDeck = useCallback(
    (cardTypeIds: string[]) => {
      updateDeck(cardTypeIds);
    },
    [updateDeck],
  );

  const tabs: { key: PreparationTab; label: string }[] = [
    { key: "deck", label: "デッキ" },
    { key: "inventory", label: "持ち物" },
    { key: "equipment", label: "装備" },
  ];

  return (
    <div className="dungeon-gate-screen">
      <FacilityHeader title="侵攻準備" />

      <div className="invasion-preparations-container">
        {/* Left Panel: Tabs + Content */}
        <div className="preparations-left-panel">
          <div className="preparations-tab-nav">
            {tabs.map((tab) => (
              <button
                key={tab.key}
                className={`preparations-tab-btn ${activeTab === tab.key ? "active" : ""}`}
                onClick={() => setActiveTab(tab.key)}
              >
                {tab.label}
              </button>
            ))}
          </div>

          <div className="preparations-tab-content">
            {activeTab === "deck" && (
              <DeckTab
                deckCards={deckCards}
                playerClass={playerData.persistent.playerClass}
                depth={gameState.depth}
                onUpdateDeck={handleUpdateDeck}
              />
            )}
            {activeTab === "inventory" && (
              <InventoryTab
                inventory={playerData.inventory.inventory}
                onNavigateToStorage={handleNavigateToStorage}
              />
            )}
            {activeTab === "equipment" && (
              <EquipmentTab
                equipmentSlots={playerData.inventory.equipmentSlots}
                onNavigateToStorage={handleNavigateToStorage}
              />
            )}
          </div>
        </div>

        {/* Right Panel: Status + Depth + Start */}
        <div className="preparations-right-panel">
          <PlayerStatusPanel
            playerData={playerData}
            runtimeState={runtimeState}
          />

          <DepthSelector
            selectedDepth={selectedDepth}
            onDepthSelect={handleDepthSelect}
          />

          <button
            className="start-invasion-button"
            onClick={handleStartExploration}
            disabled={selectedDepth === null}
          >
            {selectedDepth !== null
              ? `${DEPTH_DISPLAY_INFO[selectedDepth].japaneseName}へ侵攻`
              : "ダンジョンへ侵攻"}
          </button>
        </div>
      </div>

      <button className="dungeon-gate-back-button" onClick={returnToCamp}>
        ← キャンプに戻る
      </button>
    </div>
  );
}

export default DungeonGate;
