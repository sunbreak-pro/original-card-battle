/**
 * SettingsContext
 *
 * Manages user settings: volume (SE/BGM), brightness, etc.
 * Persists settings to localStorage via settingsManager.
 */

import React, {
  createContext,
  useContext,
  useState,
  useCallback,
  useEffect,
  type ReactNode,
} from 'react';
import { settingsManager } from '@/domain/settings/settingsManager';

// ============================================================================
// Types
// ============================================================================

export interface SettingsState {
  volumeSE: number;      // 0-100
  volumeBGM: number;     // 0-100
  brightness: number;    // 0-100 (0 = normal, 100 = darkest)
}

interface SettingsContextValue {
  settings: SettingsState;
  setVolumeSE: (value: number) => void;
  setVolumeBGM: (value: number) => void;
  setBrightness: (value: number) => void;
  resetSettings: () => void;
}

// ============================================================================
// Context
// ============================================================================

const SettingsContext = createContext<SettingsContextValue | undefined>(
  undefined,
);

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Load initial settings from localStorage or use defaults
 */
function loadInitialSettings(): SettingsState {
  const result = settingsManager.load();
  if (result.success && result.data) {
    return {
      volumeSE: result.data.volumeSE,
      volumeBGM: result.data.volumeBGM,
      brightness: result.data.brightness,
    };
  }
  const defaults = settingsManager.getDefaults();
  return {
    volumeSE: defaults.volumeSE,
    volumeBGM: defaults.volumeBGM,
    brightness: defaults.brightness,
  };
}

/**
 * Clamp value between 0 and 100
 */
function clampVolume(value: number): number {
  return Math.max(0, Math.min(100, Math.round(value)));
}

// ============================================================================
// Provider Component
// ============================================================================

export const SettingsProvider: React.FC<{ children: ReactNode }> = ({
  children,
}) => {
  const [settings, setSettings] = useState<SettingsState>(loadInitialSettings);

  /**
   * Save settings to localStorage whenever they change
   */
  useEffect(() => {
    settingsManager.save({
      volumeSE: settings.volumeSE,
      volumeBGM: settings.volumeBGM,
      brightness: settings.brightness,
    });
  }, [settings]);

  /**
   * Set SE volume (0-100)
   */
  const setVolumeSE = useCallback((value: number) => {
    setSettings((prev) => ({
      ...prev,
      volumeSE: clampVolume(value),
    }));
  }, []);

  /**
   * Set BGM volume (0-100)
   */
  const setVolumeBGM = useCallback((value: number) => {
    setSettings((prev) => ({
      ...prev,
      volumeBGM: clampVolume(value),
    }));
  }, []);

  /**
   * Set brightness (0-100, 0 = normal, 100 = darkest)
   */
  const setBrightness = useCallback((value: number) => {
    setSettings((prev) => ({
      ...prev,
      brightness: clampVolume(value),
    }));
  }, []);

  /**
   * Reset all settings to defaults
   */
  const resetSettings = useCallback(() => {
    const defaults = settingsManager.getDefaults();
    setSettings({
      volumeSE: defaults.volumeSE,
      volumeBGM: defaults.volumeBGM,
      brightness: defaults.brightness,
    });
    settingsManager.reset();
  }, []);

  return (
    <SettingsContext.Provider
      value={{
        settings,
        setVolumeSE,
        setVolumeBGM,
        setBrightness,
        resetSettings,
      }}
    >
      {children}
    </SettingsContext.Provider>
  );
};

// ============================================================================
// Hook
// ============================================================================

/**
 * Hook to use Settings context
 */
// eslint-disable-next-line react-refresh/only-export-components
export const useSettings = () => {
  const context = useContext(SettingsContext);
  if (!context) {
    throw new Error('useSettings must be used within SettingsProvider');
  }
  return context;
};
