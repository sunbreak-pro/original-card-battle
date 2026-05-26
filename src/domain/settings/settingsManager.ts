/**
 * Settings Manager
 *
 * Handles saving and loading user settings to/from LocalStorage.
 * Manages volume, brightness, and other user preferences.
 */

import { logger } from '@/utils/logger';

// ============================================================================
// Types
// ============================================================================

export interface SettingsData {
  version: string;
  volumeSE: number;      // 0-100
  volumeBGM: number;     // 0-100
  brightness: number;    // 0-100 (0 = normal, 100 = darkest)
  timestamp: number;
}

export interface SettingsResult {
  success: boolean;
  message: string;
}

export interface SettingsLoadResult extends SettingsResult {
  data?: SettingsData;
}

// ============================================================================
// Constants
// ============================================================================

const SETTINGS_KEY = 'roguelike_card_settings';
const SETTINGS_VERSION = '1.0.0';

const DEFAULT_SETTINGS: Omit<SettingsData, 'version' | 'timestamp'> = {
  volumeSE: 70,
  volumeBGM: 50,
  brightness: 0,
};

// ============================================================================
// Settings Manager
// ============================================================================

export const settingsManager = {
  /**
   * Save settings to LocalStorage
   * @param data - Settings data to store (without version/timestamp)
   * @returns SettingsResult indicating success or failure
   */
  save(data: Omit<SettingsData, 'version' | 'timestamp'>): SettingsResult {
    try {
      const settingsData: SettingsData = {
        ...data,
        version: SETTINGS_VERSION,
        timestamp: Date.now(),
      };

      const jsonString = JSON.stringify(settingsData);
      localStorage.setItem(SETTINGS_KEY, jsonString);

      return {
        success: true,
        message: 'Settings saved successfully',
      };
    } catch (error) {
      logger.error('Settings save failed:', error);
      return {
        success: false,
        message: `Settings save failed: ${error instanceof Error ? error.message : 'Unknown error'}`,
      };
    }
  },

  /**
   * Load settings from LocalStorage
   * @returns SettingsLoadResult with data if successful
   */
  load(): SettingsLoadResult {
    try {
      const jsonString = localStorage.getItem(SETTINGS_KEY);

      if (!jsonString) {
        // Return default settings if none exist
        return {
          success: true,
          message: 'No saved settings found, using defaults',
          data: {
            ...DEFAULT_SETTINGS,
            version: SETTINGS_VERSION,
            timestamp: Date.now(),
          },
        };
      }

      const data = JSON.parse(jsonString) as SettingsData;

      // Version check — migrate if needed
      if (data.version !== SETTINGS_VERSION) {
        const migrated = this.migrate(data);
        localStorage.setItem(SETTINGS_KEY, JSON.stringify(migrated));
        return {
          success: true,
          message: `Settings migrated from ${data.version} to ${SETTINGS_VERSION}`,
          data: migrated,
        };
      }

      return {
        success: true,
        message: 'Settings loaded successfully',
        data,
      };
    } catch (error) {
      logger.error('Settings load failed:', error);
      // Return defaults on error
      return {
        success: false,
        message: `Settings load failed: ${error instanceof Error ? error.message : 'Unknown error'}`,
        data: {
          ...DEFAULT_SETTINGS,
          version: SETTINGS_VERSION,
          timestamp: Date.now(),
        },
      };
    }
  },

  /**
   * Reset settings to defaults
   * @returns SettingsResult indicating success or failure
   */
  reset(): SettingsResult {
    try {
      localStorage.removeItem(SETTINGS_KEY);
      return {
        success: true,
        message: 'Settings reset to defaults',
      };
    } catch (error) {
      logger.error('Settings reset failed:', error);
      return {
        success: false,
        message: `Settings reset failed: ${error instanceof Error ? error.message : 'Unknown error'}`,
      };
    }
  },

  /**
   * Check if settings exist in storage
   * @returns true if settings exist
   */
  hasSettings(): boolean {
    return localStorage.getItem(SETTINGS_KEY) !== null;
  },

  /**
   * Get default settings values
   * @returns Default settings object
   */
  getDefaults(): Omit<SettingsData, 'version' | 'timestamp'> {
    return { ...DEFAULT_SETTINGS };
  },

  /**
   * Migrate settings from older versions
   * @param data - Settings data to migrate
   * @returns Migrated settings data
   */
  migrate(data: SettingsData): SettingsData {
    const migrated = { ...data };

    // Future migrations go here
    // Example: if (migrated.version === '1.0.0') { ... migrated.version = '1.1.0'; }

    return {
      ...migrated,
      version: SETTINGS_VERSION,
    };
  },
};

/**
 * Create default settings data structure
 */
export function createDefaultSettings(): Omit<SettingsData, 'version' | 'timestamp'> {
  return { ...DEFAULT_SETTINGS };
}
