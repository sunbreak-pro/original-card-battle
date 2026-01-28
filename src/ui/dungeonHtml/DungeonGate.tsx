// DungeonGate - Depth selection screen

import { useState, useCallback } from "react";
import type { Depth } from '@/types/campTypes';
import { useGameState } from "../../contexts/GameStateContext";
import { usePlayer } from "../../contexts/PlayerContext";
import { neutralTheme } from "../../domain/dungeon/depth/deptManager";
import { DEPTH_DISPLAY_INFO } from "../../constants/dungeonConstants";
import FacilityHeader from "../componentsHtml/FacilityHeader";
import "./DungeonGate.css";

/**
 * DungeonGate Component
 * Allows players to select a dungeon depth to explore
 */
export function DungeonGate() {
  const { returnToCamp, navigateTo, setDepth } = useGameState();
  const { resetRuntimeState } = usePlayer();
  const [selectedDepth, setSelectedDepth] = useState<Depth | null>(null);

  // Handle depth card selection
  const handleDepthSelect = useCallback((depth: Depth) => {
    setSelectedDepth(depth);
  }, []);

  // Handle exploration start
  const handleStartExploration = useCallback(() => {
    if (selectedDepth !== null) {
      // Reset runtime state (HP/AP to max) for new exploration
      resetRuntimeState();
      setDepth(selectedDepth);
      navigateTo("dungeon_map");
    }
  }, [selectedDepth, setDepth, navigateTo, resetRuntimeState]);

  // Get theme colors (unified neutral theme for all depths)
  const getThemeColors = (_depth: Depth) => {
    return neutralTheme;
  };

  return (
    <div className="dungeon-gate-screen">
      {/* Header */}
      <FacilityHeader title="ダンジョンゲート" />

      {/* Depth Selection Grid */}
      <div className="depth-selection-container">
        <h2 className="depth-selection-title">Select Depth</h2>

        <div className="depth-cards-grid">
          {([1, 2, 3, 4, 5] as Depth[]).map((depth) => {
            const theme = getThemeColors(depth);
            const info = DEPTH_DISPLAY_INFO[depth];
            const isSelected = selectedDepth === depth;

            return (
              <button
                key={depth}
                className={`depth-card ${isSelected ? "selected" : ""}`}
                onClick={() => handleDepthSelect(depth)}
                style={
                  {
                    "--depth-primary": theme.primary,
                    "--depth-secondary": theme.secondary,
                    "--depth-accent": theme.accent,
                    "--depth-glow": theme.glow,
                    "--depth-hover": theme.hover,
                  } as React.CSSProperties
                }
              >
                <div className="depth-card-inner">
                  <div className="depth-number">{depth}</div>
                  <div className="depth-name-container">
                    <span className="depth-name-jp">{info.japaneseName}</span>
                    <span className="depth-name-en">{info.name}</span>
                  </div>
                  <p className="depth-description">{info.description}</p>
                  <div className="depth-level">
                    Lv. {info.recommendedLevel}+
                  </div>
                </div>

                {isSelected && <div className="depth-selected-indicator" />}
              </button>
            );
          })}
        </div>
      </div>

      {/* Action Buttons */}
      <div className="dungeon-gate-actions">
        <button
          className="start-exploration-button"
          onClick={handleStartExploration}
          disabled={selectedDepth === null}
        >
          {selectedDepth !== null
            ? `Enter ${DEPTH_DISPLAY_INFO[selectedDepth].japaneseName}`
            : "Select a Depth"}
        </button>
      </div>

      {/* Back Button */}
      <button className="dungeon-gate-back-button" onClick={returnToCamp}>
        ← Back to Camp
      </button>
    </div>
  );
}

export default DungeonGate;
