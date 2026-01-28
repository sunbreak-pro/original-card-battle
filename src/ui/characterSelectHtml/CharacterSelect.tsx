/**
 * CharacterSelect Component
 *
 * Main character selection screen shown on first game launch.
 * Allows players to choose their character class before starting.
 */

import React, { useState } from "react";
import type { CharacterClass } from "../../domain/characters/type/baseTypes";
import {
  getAllClasses,
  type CharacterClassInfo,
} from "../../domain/characters/player/data/CharacterClassData";
import { CharacterCard } from "./CharacterCard";
import { StarterDeckPreview } from "./StarterDeckPreview";
import { useGameState } from "../../contexts/GameStateContext";
import { usePlayer } from "../../contexts/PlayerContext";
import { saveManager } from "../../domain/save/logic/saveManager";
import "./CharacterSelect.css";

export const CharacterSelect: React.FC = () => {
  const [selectedClass, setSelectedClass] = useState<CharacterClass | null>(
    null,
  );
  const { navigateTo } = useGameState();
  const { initializeWithClass } = usePlayer();

  const allClasses = getAllClasses();

  const selectedClassInfo: CharacterClassInfo | null = selectedClass
    ? allClasses.find((c) => c.class === selectedClass) || null
    : null;

  const handleClassSelect = (classType: CharacterClass) => {
    setSelectedClass(classType);
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

      {/* Main content */}
      <div className="character-select-content">
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
