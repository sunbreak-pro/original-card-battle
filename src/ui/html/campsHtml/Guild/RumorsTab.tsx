/**
 * RumorsTab: Rumor purchase system
 *
 * Players can purchase temporary exploration buffs with gold.
 * Each rumor provides a different effect for a limited number of runs.
 */

import { useState } from "react";
import type { Rumor } from "@/types/campTypes";
import { RUMORS } from "@/constants/data/camps/RumorData";
import { useResources } from "@/contexts/ResourceContext";

const RARITY_COLORS: Record<string, string> = {
  common: "#9ca3af",
  rare: "#3b82f6",
  epic: "#a855f7",
};

const RumorsTab = () => {
  const { useGold } = useResources();
  const [purchasedIds, setPurchasedIds] = useState<Set<string>>(new Set());
  const [activeRumors, setActiveRumors] = useState<
    { rumor: Rumor; runsRemaining: number }[]
  >([]);

  const purchaseRumor = (rumor: Rumor) => {
    if (purchasedIds.has(rumor.id)) return;

    const success = useGold(rumor.cost);
    if (!success) return;

    setPurchasedIds((prev) => new Set([...prev, rumor.id]));
    setActiveRumors((prev) => [
      ...prev,
      { rumor, runsRemaining: rumor.duration ?? 1 },
    ]);
  };

  const getEffectDescription = (rumor: Rumor): string => {
    switch (rumor.effect.type) {
      case "elite_rate":
        return `Elite encounter rate +${Math.round(rumor.effect.value * 100)}%`;
      case "shop_discount":
        return `Shop discount ${Math.round(rumor.effect.value * 100)}%`;
      case "treasure_rate":
        return `Treasure rate +${Math.round(rumor.effect.value * 100)}%`;
      case "start_bonus":
        return `Starting bonus: ${rumor.effect.bonus}`;
      default:
        return "Unknown effect";
    }
  };

  return (
    <div className="rumors-tab">
      {/* Active Rumors */}
      {activeRumors.length > 0 && (
        <div className="guild-active-rumors">
          <h3 className="guild-active-rumors-title">Active Rumors</h3>
          <div className="guild-active-rumor-list">
            {activeRumors.map(({ rumor, runsRemaining }) => (
              <div key={rumor.id} className="guild-active-rumor-item">
                <span className="guild-active-rumor-icon">{rumor.icon}</span>
                <span className="guild-active-rumor-name">{rumor.name}</span>
                <span className="guild-active-rumor-runs">
                  {runsRemaining} runs left
                </span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Available Rumors */}
      <div className="guild-rumor-grid">
        {RUMORS.map((rumor) => {
          const isPurchased = purchasedIds.has(rumor.id);
          const rarityColor = RARITY_COLORS[rumor.rarity];

          return (
            <div
              key={rumor.id}
              className={`guild-rumor-card ${isPurchased ? "purchased" : ""} ${rumor.rarity}`}
              style={{ borderColor: rarityColor }}
            >
              <div className="guild-rumor-card-header">
                <span className="guild-rumor-icon">{rumor.icon}</span>
                <div>
                  <h3 className="guild-rumor-name">{rumor.name}</h3>
                  <span
                    className="guild-rumor-rarity"
                    style={{ color: rarityColor }}
                  >
                    {rumor.rarity.toUpperCase()}
                  </span>
                </div>
              </div>

              <p className="guild-rumor-description">{rumor.description}</p>

              <div className="guild-rumor-effect">
                <span className="guild-rumor-effect-label">Effect:</span>
                <span className="guild-rumor-effect-value">
                  {getEffectDescription(rumor)}
                </span>
              </div>

              {rumor.duration && (
                <div className="guild-rumor-duration">
                  Duration: {rumor.duration} runs
                </div>
              )}

              <div className="guild-rumor-footer">
                <span className="guild-rumor-cost">{rumor.cost}G</span>
                {isPurchased ? (
                  <span className="guild-rumor-purchased-badge">Purchased</span>
                ) : (
                  <button
                    className="guild-rumor-buy-btn"
                    onClick={() => purchaseRumor(rumor)}
                  >
                    Purchase
                  </button>
                )}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
};

export default RumorsTab;
