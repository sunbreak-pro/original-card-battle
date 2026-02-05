/**
 * CharacterSelect Component
 *
 * Main character selection screen shown on first game launch.
 * Allows players to choose their character class before starting.
 */

import React, { useState, useEffect } from "react";
import type { CharacterClass } from "@/types/characterTypes";
import {
  getAllClasses,
  type CharacterClassInfo,
} from "@/constants/data/characters/CharacterClassData";
import { CharacterCard } from "./CharacterCard";
import { StarterDeckPreview } from "./StarterDeckPreview";
import { useGameState } from "@/contexts/GameStateContext";
import { usePlayer } from "@/contexts/PlayerContext";
import { useResources } from "@/contexts/ResourceContext";
import { saveManager, formatSaveTimestamp } from "@/domain/save/logic/saveManager";
import type { SaveMetadata } from "@/types/saveTypes";
import "./CharacterSelect.css";

export const CharacterSelect: React.FC = () => {
  const [selectedClass, setSelectedClass] = useState<CharacterClass | null>(
    null,
  );
  const [saveMetadata, setSaveMetadata] = useState<SaveMetadata | null>(null);
  const { navigateTo } = useGameState();
  const { initializeWithClass, loadFromSaveData } = usePlayer();
  const { loadResourcesFromSave } = useResources();

  // Check for existing save on mount
  useEffect(() => {
    const metadata = saveManager.getMetadata();
    if (metadata.exists) {
      setSaveMetadata(metadata);
    }
  }, []);

  const allClasses = getAllClasses();

  const selectedClassInfo: CharacterClassInfo | null = selectedClass
    ? allClasses.find((c) => c.class === selectedClass) || null
    : null;

  const handleClassSelect = (classType: CharacterClass) => {
    setSelectedClass(classType);
  };

  const handleContinue = () => {
    const loadResult = saveManager.load();
    if (!loadResult.success || !loadResult.data) {
      // Failed to load save, stay on character select
      setSaveMetadata(null);
      return;
    }

    const saveData = loadResult.data;

    // Load player state from save
    loadFromSaveData(saveData);

    // Load resources from save
    loadResourcesFromSave(
      saveData.resources.baseCampGold,
      saveData.resources.baseCampMagicStones,
      saveData.resources.explorationLimit
    );

    // Navigate to camp
    navigateTo("camp");
  };

  const handleDeleteSave = () => {
    saveManager.deleteSave();
    setSaveMetadata(null);
  };

  const handleConfirm = () => {
    if (!selectedClass || !selectedClassInfo?.isAvailable) {
      return;
    }

    // Initialize player with selected class
    initializeWithClass(selectedClass);

    // Create initial save
    saveManager.save({
      player: {
        name: "Adventurer",
        playerClass: selectedClass,
        classGrade: "E",
        level: 1,
        hp: selectedClassInfo.stats.hp,
        maxHp: selectedClassInfo.stats.hp,
        ap: selectedClassInfo.stats.ap,
        maxAp: selectedClassInfo.stats.ap,
        speed: selectedClassInfo.stats.speed,
        deckCardIds: selectedClassInfo.starterDeck.map((card) => card.id),
      },
      resources: {
        baseCampGold: 0,
        baseCampMagicStones: { small: 0, medium: 0, large: 0, huge: 0 },
        explorationGold: 0,
        explorationMagicStones: { small: 0, medium: 0, large: 0, huge: 0 },
        explorationLimit: { current: 0, max: 10 },
      },
      inventory: {
        storageItems: [],
        equipmentSlots: {
          weapon: null,
          armor: null,
          helmet: null,
          boots: null,
          accessory1: null,
          accessory2: null,
        },
        inventoryItems: [],
        equipmentInventoryItems: [],
      },
      progression: {
        sanctuaryProgress: {
          currentRunSouls: 0,
          totalSouls: 0,
          unlockedNodes: [],
          explorationLimitBonus: 0,
        },
        unlockedDepths: [1],
      },
    });

    // Navigate to camp
    navigateTo("camp");
  };

  const canConfirm = selectedClass !== null && selectedClassInfo?.isAvailable;

  return (
    <div className="character-select-screen">
      {/* Header */}
      <header className="character-select-header">
        <h1 className="character-select-title">Choose Your Path</h1>
        <p className="character-select-subtitle">
          Select a class to begin your journey into the abyss
        </p>
      </header>

      {/* Continue Game Section (if save exists) */}
      {saveMetadata && saveMetadata.exists && (
        <div className="continue-game-section">
          <div className="save-info">
            <span className="save-class">
              {saveMetadata.playerClass === "swordsman" ? "Swordsman" : "Mage"}
            </span>
            <span className="save-grade">Grade: {saveMetadata.classGrade}</span>
            {saveMetadata.timestamp && (
              <span className="save-time">
                Last played: {formatSaveTimestamp(saveMetadata.timestamp)}
              </span>
            )}
          </div>
          <div className="continue-buttons">
            <button className="continue-button" onClick={handleContinue}>
              Continue
            </button>
            <button className="delete-save-button" onClick={handleDeleteSave}>
              Delete Save
            </button>
          </div>
        </div>
      )}

      {/* Main content */}
      <div className="character-select-content">
        {/* New Game label when save exists */}
        {saveMetadata && saveMetadata.exists && (
          <h2 className="new-game-label">New Game</h2>
        )}

        {/* Character grid */}
        <div className="character-grid">
          {allClasses.map((classInfo) => (
            <CharacterCard
              key={classInfo.class}
              classInfo={classInfo}
              isSelected={selectedClass === classInfo.class}
              onSelect={() => handleClassSelect(classInfo.class)}
            />
          ))}
        </div>

        {/* Starter Deck Preview */}
        {selectedClassInfo && (
          <StarterDeckPreview cards={selectedClassInfo.starterDeck} />
        )}

        {/* Confirm button */}
        <div className="confirm-button-container">
          <button
            className="confirm-button"
            onClick={handleConfirm}
            disabled={!canConfirm}
          >
            Begin Journey
          </button>
        </div>
      </div>
    </div>
  );
};

export default CharacterSelect;
