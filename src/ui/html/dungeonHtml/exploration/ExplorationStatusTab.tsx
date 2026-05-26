// ExplorationStatusTab - Player status display for dungeon exploration

import { usePlayer } from "@/contexts/PlayerContext";
import { useResources } from "@/contexts/ResourceContext";

export function ExplorationStatusTab() {
  const { playerData, runtimeState, deckCards } = usePlayer();
  const { resources } = useResources();

  const equipSlots = playerData.inventory.equipmentSlots;

  return (
    <div className="exploration-status-tab">
      <div className="exploration-status-grid">
        <div className="exploration-stat-row">
          <span className="exploration-stat-icon">❤️</span>
          <span className="exploration-stat-label">HP</span>
          <span className="exploration-stat-value">
            {runtimeState.currentHp}/{playerData.persistent.baseMaxHp}
          </span>
        </div>
        <div className="exploration-stat-row">
          <span className="exploration-stat-icon">⚡</span>
          <span className="exploration-stat-label">AP</span>
          <span className="exploration-stat-value">
            {runtimeState.currentAp}/{playerData.persistent.baseMaxAp}
          </span>
        </div>
        <div className="exploration-stat-row">
          <span className="exploration-stat-icon">💖</span>
          <span className="exploration-stat-label">ライフ</span>
          <span className="exploration-stat-value">
            {runtimeState.lives.currentLives}
          </span>
        </div>
        <div className="exploration-stat-row">
          <span className="exploration-stat-icon">💰</span>
          <span className="exploration-stat-label">ゴールド</span>
          <span className="exploration-stat-value">
            {resources.gold.baseCamp + resources.gold.exploration}
          </span>
        </div>
        <div className="exploration-stat-row">
          <span className="exploration-stat-icon">🃏</span>
          <span className="exploration-stat-label">デッキ</span>
          <span className="exploration-stat-value">{deckCards.length}枚</span>
        </div>
        <div className="exploration-stat-row">
          <span className="exploration-stat-icon">🎒</span>
          <span className="exploration-stat-label">インベントリ</span>
          <span className="exploration-stat-value">
            {playerData.inventory.inventory.currentCapacity}/
            {playerData.inventory.inventory.maxCapacity}
          </span>
        </div>
      </div>

      {/* Equipment */}
      <div className="exploration-equipment-section">
        <h4 className="exploration-section-title">装備</h4>
        <div className="exploration-equipment-list">
          {equipSlots.weapon ? (
            <div className="exploration-equip-slot">
              <span className="exploration-equip-icon">⚔️</span>
              <span className="exploration-equip-name">{equipSlots.weapon.name}</span>
            </div>
          ) : (
            <div className="exploration-equip-slot exploration-equip-empty">
              <span className="exploration-equip-icon">⚔️</span>
              <span className="exploration-equip-name">なし</span>
            </div>
          )}
          {equipSlots.armor ? (
            <div className="exploration-equip-slot">
              <span className="exploration-equip-icon">🛡️</span>
              <span className="exploration-equip-name">{equipSlots.armor.name}</span>
            </div>
          ) : (
            <div className="exploration-equip-slot exploration-equip-empty">
              <span className="exploration-equip-icon">🛡️</span>
              <span className="exploration-equip-name">なし</span>
            </div>
          )}
          {equipSlots.accessory1 ? (
            <div className="exploration-equip-slot">
              <span className="exploration-equip-icon">💍</span>
              <span className="exploration-equip-name">{equipSlots.accessory1.name}</span>
            </div>
          ) : (
            <div className="exploration-equip-slot exploration-equip-empty">
              <span className="exploration-equip-icon">💍</span>
              <span className="exploration-equip-name">なし</span>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
