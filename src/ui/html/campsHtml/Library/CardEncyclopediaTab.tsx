/**
 * CardEncyclopediaTab Component
 *
 * Displays all cards in a three-row layout:
 * - Base Cards: Always unlocked, no derivedFrom or isTalentCard
 * - Derived Cards: Unlocked when parent card reaches required mastery
 * - Talent Cards: Unlocked via high mastery on specific cards
 */

import React, { useState, useMemo } from "react";
import { getAllCards } from "@/constants/data/camps/CardEncyclopediaData";
import { classifyCards, sortCardsByCostAndName } from "@/domain/cards/logic/cardClassification";
import { checkTalentUnlocks } from "@/domain/cards/logic/talentCardUnlock";
import { calculateMasteryLevel, type MasteryStore } from "@/domain/cards/state/masteryManager";
import { getAllDerivations } from "@/constants/data/cards/cardDerivationRegistry";
import type { Card } from "@/types/cardTypes";
import { CardCategoryRow } from "./CardCategoryRow";
import { CardDerivationTree } from "./CardDerivationTree";

export const CardEncyclopediaTab: React.FC = () => {
  const [selectedCard, setSelectedCard] = useState<Card | null>(null);

  // Mock mastery store for now (in real implementation, get from PlayerContext)
  // TODO: Replace with actual mastery store from context
  const masteryStore: MasteryStore = useMemo(() => new Map(), []);

  // Get all cards and classify them
  const allCards = useMemo(() => getAllCards(), []);

  const { baseCards, derivedCards, talentCards } = useMemo(
    () => classifyCards(allCards),
    [allCards],
  );

  // Sort each category
  const sortedBaseCards = useMemo(
    () => sortCardsByCostAndName(baseCards),
    [baseCards],
  );
  const sortedDerivedCards = useMemo(
    () => sortCardsByCostAndName(derivedCards),
    [derivedCards],
  );
  const sortedTalentCards = useMemo(
    () => sortCardsByCostAndName(talentCards),
    [talentCards],
  );

  // Calculate unlocked IDs for each category
  const unlockedBaseIds = useMemo(() => {
    // All base cards are always unlocked
    return new Set(baseCards.map(c => c.cardTypeId));
  }, [baseCards]);

  const unlockedDerivedIds = useMemo(() => {
    // Check which derived cards are unlocked based on parent card mastery
    const unlockedIds = new Set<string>();
    const derivations = getAllDerivations();

    for (const entry of derivations) {
      const useCount = masteryStore.get(entry.parentCardTypeId) ?? 0;
      const masteryLevel = calculateMasteryLevel(useCount);
      if (masteryLevel >= entry.requiredMastery) {
        unlockedIds.add(entry.derivedCardTypeId);
      }
    }

    return unlockedIds;
  }, [masteryStore]);

  const unlockedTalentIds = useMemo(
    () => checkTalentUnlocks(masteryStore),
    [masteryStore],
  );

  // Stats calculation
  const stats = useMemo(() => {
    const totalBase = baseCards.length;
    const totalDerived = derivedCards.length;
    const totalTalent = talentCards.length;
    const total = totalBase + totalDerived + totalTalent;

    const unlockedBase = unlockedBaseIds.size;
    const unlockedDerived = unlockedDerivedIds.size;
    const unlockedTalent = unlockedTalentIds.size;
    const unlocked = unlockedBase + unlockedDerived + unlockedTalent;

    return { total, unlocked, totalBase, totalDerived, totalTalent };
  }, [
    baseCards.length,
    derivedCards.length,
    talentCards.length,
    unlockedBaseIds.size,
    unlockedDerivedIds.size,
    unlockedTalentIds.size,
  ]);

  return (
    <div className="library-content card-encyclopedia-three-rows">
      {/* Stats Bar */}
      <div className="library-stats">
        <div className="stat-item">
          <span className="stat-value">
            {stats.unlocked}/{stats.total}
          </span>
          <span className="stat-label">解放済み</span>
        </div>
        <div className="stat-item">
          <span className="stat-value">{stats.totalBase}</span>
          <span className="stat-label">基本</span>
        </div>
        <div className="stat-item">
          <span className="stat-value">{stats.totalDerived}</span>
          <span className="stat-label">派生</span>
        </div>
        <div className="stat-item">
          <span className="stat-value">{stats.totalTalent}</span>
          <span className="stat-label">才能</span>
        </div>
      </div>

      {/* Derivation Tree (shown when card selected) */}
      {selectedCard && (
        <CardDerivationTree
          card={selectedCard}
          onClose={() => setSelectedCard(null)}
          unlockedCardTypeIds={new Set([
            ...unlockedBaseIds,
            ...unlockedDerivedIds,
            ...unlockedTalentIds,
          ])}
        />
      )}

      {/* Three-row layout */}
      <div className="card-categories-wrapper">
        <CardCategoryRow
          title="基本カード"
          cards={sortedBaseCards}
          unlockedIds={unlockedBaseIds}
          onCardClick={setSelectedCard}
        />

        <CardCategoryRow
          title="派生カード"
          cards={sortedDerivedCards}
          unlockedIds={unlockedDerivedIds}
          onCardClick={setSelectedCard}
        />

        <CardCategoryRow
          title="才能カード"
          cards={sortedTalentCards}
          unlockedIds={unlockedTalentIds}
          onCardClick={setSelectedCard}
        />
      </div>
    </div>
  );
};
