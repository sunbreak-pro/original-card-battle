// PlayerStatusPanel - Displays player stats for invasion preparation

import type { PlayerData } from "@/types/characterTypes";
import type { RuntimeBattleState } from "@/contexts/PlayerContext";
import { CHARACTER_CLASS_DATA } from "@/constants/data/characters/CharacterClassData";

interface PlayerStatusPanelProps {
  playerData: PlayerData;
  runtimeState: RuntimeBattleState;
}

export function PlayerStatusPanel({
  playerData,
  runtimeState,
}: PlayerStatusPanelProps) {
  const classInfo = CHARACTER_CLASS_DATA[playerData.persistent.playerClass];
  const deckCount = playerData.persistent.deckCardIds.length;

  return (
    <div className="player-status-panel">
      <div className="status-header">
        <span className="status-name">{playerData.persistent.name}</span>
        <span className="status-class">{classInfo.japaneseName}</span>
        <span className="status-grade">{playerData.persistent.classGrade}</span>
      </div>

      <div className="status-stats">
        <div className="status-row">
          <span className="status-label">Lv.</span>
          <span className="status-value">{playerData.persistent.level}</span>
        </div>
        <div className="status-row">
          <span className="status-label">HP</span>
          <span className="status-value">
            {runtimeState.currentHp} / {playerData.persistent.baseMaxHp}
          </span>
        </div>
        <div className="status-row">
          <span className="status-label">AP</span>
          <span className="status-value">
            {runtimeState.currentAp} / {playerData.persistent.baseMaxAp}
          </span>
        </div>
        <div className="status-row">
          <span className="status-label">Gold</span>
          <span className="status-value">
            {playerData.resources.baseCampGold}
          </span>
        </div>
        <div className="status-row">
          <span className="status-label">残機</span>
          <span className="status-value">
            {runtimeState.lives.currentLives} / {runtimeState.lives.maxLives}
          </span>
        </div>
        <div className="status-row">
          <span className="status-label">難易度</span>
          <span className="status-value">{runtimeState.difficulty}</span>
        </div>
        <div className="status-row">
          <span className="status-label">デッキ</span>
          <span className="status-value">{deckCount}枚</span>
        </div>
      </div>
    </div>
  );
}
