/**
 * Environment-aware logger utility
 * - debug/info: Only outputs in development environment
 * - warn/error: Always outputs (production safe)
 */

const isDev = import.meta.env.DEV;

export const logger = {
  /**
   * Debug level logging - DEV only
   */
  debug: (...args: unknown[]): void => {
    if (isDev) {
      console.log('[DEBUG]', ...args);
    }
  },

  /**
   * Info level logging - DEV only
   */
  info: (...args: unknown[]): void => {
    if (isDev) {
      console.info('[INFO]', ...args);
    }
  },

  /**
   * Warning level logging - Always outputs
   */
  warn: (...args: unknown[]): void => {
    console.warn('[WARN]', ...args);
  },

  /**
   * Error level logging - Always outputs
   */
  error: (...args: unknown[]): void => {
    console.error('[ERROR]', ...args);
  },
};
