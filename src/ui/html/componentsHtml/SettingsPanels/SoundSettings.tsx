/**
 * SoundSettings Panel
 *
 * Volume controls for SE and BGM.
 */

import React from 'react';
import { useSettings } from '@/contexts/SettingsContext';

export const SoundSettings: React.FC = () => {
  const { settings, setVolumeSE, setVolumeBGM } = useSettings();

  return (
    <div className="settings-panel sound-settings">
      <h3 className="settings-panel-title">🔊 サウンド設定</h3>

      <div className="settings-item">
        <label className="settings-label">
          <span className="settings-label-text">効果音 (SE)</span>
          <span className="settings-value">{settings.volumeSE}%</span>
        </label>
        <input
          type="range"
          min="0"
          max="100"
          value={settings.volumeSE}
          onChange={(e) => setVolumeSE(Number(e.target.value))}
          className="settings-slider"
        />
      </div>

      <div className="settings-item">
        <label className="settings-label">
          <span className="settings-label-text">BGM</span>
          <span className="settings-value">{settings.volumeBGM}%</span>
        </label>
        <input
          type="range"
          min="0"
          max="100"
          value={settings.volumeBGM}
          onChange={(e) => setVolumeBGM(Number(e.target.value))}
          className="settings-slider"
        />
      </div>

      <p className="settings-note">
        ※ 音声機能は今後実装予定です
      </p>
    </div>
  );
};

export default SoundSettings;
