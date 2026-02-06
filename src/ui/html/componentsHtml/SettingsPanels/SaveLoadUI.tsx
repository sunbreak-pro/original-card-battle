/**
 * SaveLoadUI Panel
 *
 * Save and load game data controls.
 */

import React, { useState, useCallback } from 'react';
import { usePlayer } from '@/contexts/PlayerContext';
import { saveManager, formatSaveTimestamp } from '@/domain/save/logic/saveManager';
import { useResources } from '@/contexts/ResourceContext';
import { useToast } from '@/contexts/ToastContext';

interface SaveLoadUIProps {
  onNewGame?: () => void;
}

export const SaveLoadUI: React.FC<SaveLoadUIProps> = ({
  onNewGame,
}) => {
  const { playerData } = usePlayer();
  const { resources } = useResources();
  const { addToast } = useToast();

  const [metadata, setMetadata] = useState(() => saveManager.getMetadata());
  const [isLoading, setIsLoading] = useState(false);

  /**
   * Save game data
   */
  const handleSave = useCallback(() => {
    setIsLoading(true);

    try {
      const result = saveManager.save({
        player: {
          name: playerData.persistent.name,
          playerClass: playerData.persistent.playerClass,
          classGrade: playerData.persistent.classGrade,
          level: playerData.persistent.level,
          hp: playerData.persistent.baseMaxHp,
          maxHp: playerData.persistent.baseMaxHp,
          ap: playerData.persistent.baseMaxAp,
          maxAp: playerData.persistent.baseMaxAp,
          speed: playerData.persistent.baseSpeed,
          deckCardIds: playerData.persistent.deckCardIds,
        },
        resources: {
          baseCampGold: resources.gold.baseCamp,
          baseCampMagicStones: resources.magicStones.baseCamp,
          explorationGold: resources.gold.exploration,
          explorationMagicStones: resources.magicStones.exploration,
          explorationLimit: resources.explorationLimit,
        },
        inventory: {
          storageItems: playerData.inventory.storage.items,
          equipmentSlots: playerData.inventory.equipmentSlots,
          inventoryItems: playerData.inventory.inventory.items,
          equipmentInventoryItems: playerData.inventory.equipmentInventory.items,
        },
        progression: {
          sanctuaryProgress: playerData.progression.sanctuaryProgress,
          unlockedDepths: playerData.progression.unlockedDepths,
        },
      });

      if (result.success) {
        setMetadata(saveManager.getMetadata());
        addToast({
          type: 'success',
          message: 'ゲームをセーブしました',
          icon: '💾',
          duration: 2000,
        });
      } else {
        addToast({
          type: 'error',
          message: `セーブに失敗しました: ${result.message}`,
          icon: '❌',
          duration: 3000,
        });
      }
    } finally {
      setIsLoading(false);
    }
  }, [playerData, resources, addToast]);

  /**
   * Delete save data
   */
  const handleDelete = useCallback(() => {
    if (!metadata.exists) {
      return;
    }

    if (window.confirm('セーブデータを削除しますか？\nこの操作は取り消せません。')) {
      saveManager.deleteSave();
      setMetadata(saveManager.getMetadata());
      addToast({
        type: 'success',
        message: 'セーブデータを削除しました',
        icon: '🗑️',
        duration: 2000,
      });
    }
  }, [metadata.exists, addToast]);

  // Helper to format class name
  const formatClassName = (playerClass: string): string => {
    switch (playerClass) {
      case 'swordsman': return '剣士';
      case 'mage': return '魔術師';
      default: return playerClass;
    }
  };

  return (
    <div className="settings-panel saveload-settings">
      <h3 className="settings-panel-title">💾 セーブ / データ管理</h3>

      {/* Save Info */}
      {metadata.exists && (
        <div className="save-info">
          <div className="save-info-item">
            <span className="save-info-label">キャラクター:</span>
            <span className="save-info-value">{metadata.playerName}</span>
          </div>
          <div className="save-info-item">
            <span className="save-info-label">クラス:</span>
            <span className="save-info-value">
              {formatClassName(metadata.playerClass || '')} ({metadata.classGrade}ランク)
            </span>
          </div>
          <div className="save-info-item">
            <span className="save-info-label">最終セーブ:</span>
            <span className="save-info-value">
              {metadata.timestamp ? formatSaveTimestamp(metadata.timestamp) : '-'}
            </span>
          </div>
        </div>
      )}

      {!metadata.exists && (
        <div className="save-info empty">
          <p>セーブデータがありません</p>
        </div>
      )}

      {/* Action Buttons */}
      <div className="saveload-buttons">
        <button
          className="saveload-btn save-btn"
          onClick={handleSave}
          disabled={isLoading}
        >
          {isLoading ? '保存中...' : '💾 セーブ'}
        </button>
        {onNewGame && (
          <button
            className="saveload-btn newgame-btn"
            onClick={onNewGame}
          >
            🎮 ニューゲーム
          </button>
        )}
        <button
          className="saveload-btn delete-btn"
          onClick={handleDelete}
          disabled={!metadata.exists || isLoading}
        >
          🗑️ 削除
        </button>
      </div>

      <p className="settings-note">
        ※ ゲームは拠点に戻った時に自動でセーブされます
      </p>
    </div>
  );
};

export default SaveLoadUI;
