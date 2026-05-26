/**
 * Talent Card Unlock System
 *
 * Handles talent card unlock detection based on mastery levels.
 * Talent cards are special cards unlocked by reaching Lv3/Lv4 mastery on specific cards.
 */

import type { MasteryStore } from "@/domain/cards/state/masteryManager";
import { calculateMasteryLevel } from "@/domain/cards/state/masteryManager";
import { getAllTalentUnlocks } from "@/constants/data/cards/talentCardRegistry";

/**
 * Check which talent cards are unlocked based on current mastery progress
 * @param masteryStore - Current mastery store with use counts
 * @returns Set of unlocked talent card type IDs
 */
export function checkTalentUnlocks(
  masteryStore: MasteryStore
): Set<string> {
  const unlockedTalentIds = new Set<string>();
  const allEntries = getAllTalentUnlocks();

  for (const entry of allEntries) {
    const useCount = masteryStore.get(entry.requiredCardTypeId) ?? 0;
    const masteryLevel = calculateMasteryLevel(useCount);

    if (masteryLevel >= entry.requiredMastery) {
      unlockedTalentIds.add(entry.talentCardTypeId);
    }
  }

  return unlockedTalentIds;
}

/**
 * Get the unlock condition for a specific talent card
 * @param talentCardTypeId - The talent card type ID to check
 * @returns Unlock condition info or undefined if not a talent card
 */
export function getTalentUnlockCondition(
  talentCardTypeId: string
): { requiredCardTypeId: string; requiredMastery: number } | undefined {
  const allEntries = getAllTalentUnlocks();
  const entry = allEntries.find(e => e.talentCardTypeId === talentCardTypeId);

  if (!entry) return undefined;

  return {
    requiredCardTypeId: entry.requiredCardTypeId,
    requiredMastery: entry.requiredMastery,
  };
}

/**
 * Check if a specific talent card is unlocked
 * @param talentCardTypeId - The talent card type ID
 * @param masteryStore - Current mastery store
 * @returns true if unlocked
 */
export function isTalentCardUnlocked(
  talentCardTypeId: string,
  masteryStore: MasteryStore
): boolean {
  const condition = getTalentUnlockCondition(talentCardTypeId);
  if (!condition) return false;

  const useCount = masteryStore.get(condition.requiredCardTypeId) ?? 0;
  const masteryLevel = calculateMasteryLevel(useCount);

  return masteryLevel >= condition.requiredMastery;
}
