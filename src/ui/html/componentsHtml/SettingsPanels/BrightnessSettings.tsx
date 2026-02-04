/**
 * BrightnessSettings Panel
 *
 * Screen brightness control via dark overlay.
 */

import React from 'react';
import { useSettings } from '@/contexts/SettingsContext';

export const BrightnessSettings: React.FC = () => {
  const { settings, setBrightness } = useSettings();

  return (
    <div className="settings-panel brightness-settings">
      <h3 className="settings-panel-title">🌙 画面の明るさ</h3>

      <div className="settings-item">
        <label className="settings-label">
          <span className="settings-label-text">暗さ調整</span>
          <span className="settings-value">{settings.brightness}%</span>
        </label>
        <input
          type="range"
          min="0"
          max="100"
          value={settings.brightness}
          onChange={(e) => setBrightness(Number(e.target.value))}
          className="settings-slider"
        />
        <div className="settings-slider-labels">
          <span>明るい</span>
          <span>暗い</span>
        </div>
      </div>

      <p className="settings-note">
        画面に暗いフィルターを適用します。目の負担を軽減できます。
      </p>
    </div>
  );
};

export default BrightnessSettings;
