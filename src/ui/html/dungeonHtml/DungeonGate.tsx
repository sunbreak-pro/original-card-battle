// DungeonGate - Invasion preparations screen

import { useState, useCallback, useMemo } from "react";
import type { Depth, PreparationTab } from "@/types/campTypes";
import { PREPARATION_TABS } from "@/constants/campConstants";
import { useGameState } from "@/contexts/GameStateContext";
import { usePlayer } from "@/contexts/PlayerContext";
import { DEPTH_DISPLAY_INFO } from "@/constants/dungeonConstants";
import { MIN_DECK_SIZE } from "@/constants/uiConstants";
import { calculateEquipmentAP } from "@/domain/item_equipment/logic/equipmentStats";
import FacilityHeader from "../componentsHtml/FacilityHeader";
import BackToCampButton from "../componentsHtml/BackToCampButton";
import FacilityTabNav from "../componentsHtml/FacilityTabNav";
import { DepthSelector } from "./preparations/DepthSelector";
import { PlayerStatusPanel } from "./preparations/PlayerStatusPanel";
import { DeckTab } from "./preparations/DeckTab";
import { InventoryTab } from "./preparations/InventoryTab";
import { EquipmentTab } from "./preparations/EquipmentTab";
import "./DungeonGate.css";

/** AP warning threshold (20%) */
const AP_WARNING_THRESHOLD = 0.2;

/**
 * DungeonGate Component
 * Tabbed invasion preparation screen where players review loadout before dungeon entry
 */
export function DungeonGate() {
  const { navigateTo, setDepth, gameState } = useGameState();
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

  // Calculate equipment AP and check for low AP warning
  const apWarning = useMemo(() => {
    const apResult = calculateEquipmentAP(playerData.inventory.equipmentSlots);
    if (apResult.maxAP === 0) return null; // No defense equipment
    const ratio = apResult.totalAP / apResult.maxAP;
    if (ratio <= AP_WARNING_THRESHOLD) {
      return {
        currentAP: apResult.totalAP,
        maxAP: apResult.maxAP,
        percentage: Math.round(ratio * 100),
      };
    }
    return null;
  }, [playerData.inventory.equipmentSlots]);

  return (
    <div className="dungeon-gate-screen">
      <FacilityHeader title="侵攻準備" />

      <div className="invasion-preparations-container">
        {/* Left Panel: Tabs + Content */}
        <div className="preparations-left-panel">
          <FacilityTabNav
            tabs={PREPARATION_TABS}
            activeTab={activeTab}
            onTabChange={setActiveTab}
            facility="preparations"
          />

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

          {deckCards.length < MIN_DECK_SIZE && (
            <div className="deck-size-warning">
              デッキが{MIN_DECK_SIZE}枚未満です（現在{deckCards.length}枚）
            </div>
          )}
          {apWarning && (
            <div className="ap-warning">
              ⚠️ 装備APが低下しています（{apWarning.currentAP}/{apWarning.maxAP} - {apWarning.percentage}%）
              <br />
              <span className="ap-warning-hint">鍛冶屋で装備を修理してください</span>
            </div>
          )}
          <button
            className="start-invasion-button"
            onClick={handleStartExploration}
            disabled={
              selectedDepth === null || deckCards.length < MIN_DECK_SIZE
            }
          >
            {selectedDepth !== null
              ? `${DEPTH_DISPLAY_INFO[selectedDepth].japaneseName}へ侵攻`
              : "ダンジョンへ侵攻"}
          </button>
        </div>
      </div>

      <BackToCampButton />
    </div>
  );
}

export default DungeonGate;
