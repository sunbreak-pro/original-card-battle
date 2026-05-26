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

/**
 * Generate a unique ID for game entities
 * Keeps the format similar to existing IDs for compatibility
 * @param prefix - Required prefix identifying the entity type
 * @returns A unique ID string with prefix
 */
export function generateEntityId(prefix: string): string {
  return `${prefix}_${crypto.randomUUID()}`;
}
