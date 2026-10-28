/**
 * ID Generator utility
 * Uses crypto.randomUUID for secure, unique IDs
 */

/**
 * Generate a unique ID with optional prefix
 * @param prefix - Optional prefix for the ID (e.g., 'run', 'player', 'item')
 * @returns A unique ID string
 */
export function generateId(prefix?: string): string {
  const uuid = crypto.randomUUID();
  return prefix ? `${prefix}_${uuid}` : uuid;
}
