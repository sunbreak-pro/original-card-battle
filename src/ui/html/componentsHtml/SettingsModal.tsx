/**
 * SettingsModal
 *
 * Main settings modal with tabs for Settings and Achievements.
 */

import React, { useState } from 'react';
import { SoundSettings } from './SettingsPanels/SoundSettings';
import { BrightnessSettings } from './SettingsPanels/BrightnessSettings';
import { SaveLoadUI } from './SettingsPanels/SaveLoadUI';
import { AchievementList } from './AchievementList';
import { useSettings } from '@/contexts/SettingsContext';
import '@/ui/css/components/SettingsModal.css';

// ============================================================================
// Types
// ============================================================================

type TabType = 'settings' | 'achievements';

interface SettingsModalProps {
  isOpen: boolean;
  onClose: () => void;
}

// ============================================================================
// Component
// ============================================================================

export const SettingsModal: React.FC<SettingsModalProps> = ({
  isOpen,
  onClose,
}) => {
  const [activeTab, setActiveTab] = useState<TabType>('settings');
  const { resetSettings } = useSettings();

  if (!isOpen) return null;

  const handleBackdropClick = (e: React.MouseEvent) => {
    if (e.target === e.currentTarget) {
      onClose();
    }
  };

  const handleResetSettings = () => {
    if (window.confirm('設定を初期値にリセットしますか？')) {
      resetSettings();
    }
  };

  return (
    <div className="settings-modal-backdrop" onClick={handleBackdropClick}>
      <div className="settings-modal">
        {/* Header */}
        <div className="settings-modal-header">
          <h2 className="settings-modal-title">⚙️ 設定</h2>
          <button
            className="settings-modal-close"
            onClick={onClose}
            aria-label="閉じる"
          >
            ✕
          </button>
        </div>

        {/* Tab Navigation */}
        <div className="settings-tabs">
          <button
            className={`settings-tab ${activeTab === 'settings' ? 'active' : ''}`}
            onClick={() => setActiveTab('settings')}
          >
            ⚙️ 設定
          </button>
          <button
            className={`settings-tab ${activeTab === 'achievements' ? 'active' : ''}`}
            onClick={() => setActiveTab('achievements')}
          >
            🏆 実績
          </button>
        </div>

        {/* Content */}
        <div className="settings-modal-content">
          {activeTab === 'settings' && (
            <div className="settings-content">
              <SoundSettings />
              <BrightnessSettings />
              <SaveLoadUI />

              {/* Reset Button */}
              <div className="settings-reset">
                <button
                  className="settings-reset-btn"
                  onClick={handleResetSettings}
                >
                  🔄 設定をリセット
                </button>
              </div>
            </div>
          )}

          {activeTab === 'achievements' && (
            <div className="achievements-content">
              <AchievementList />
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default SettingsModal;
