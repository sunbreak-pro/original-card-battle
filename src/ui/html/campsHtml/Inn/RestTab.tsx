/**
 * RestTab - Rest options for the Inn
 *
 * Displays available rest options and allows purchase.
 * Rest provides HP and energy bonuses for the next exploration.
 */

import { useState } from "react";
import { usePlayer } from "@/contexts/PlayerContext";
import { useResources } from "@/contexts/ResourceContext";
import { REST_OPTIONS, DEFAULT_INN_BUFFS_STATE } from "@/constants/data/camps/InnData";
import { purchaseRest, canAffordInnService } from "@/domain/camps/logic/innLogic";
import type { InnBuffsState } from "@/types/campTypes";

const RestTab = () => {
  const { playerData, updatePlayerData } = usePlayer();
  const { getTotalGold, spendGold } = useResources();
  const [notification, setNotification] = useState<string | null>(null);

  const currentGold = getTotalGold();
  const currentInnBuffs: InnBuffsState = playerData.progression.innBuffsState ?? {
    ...DEFAULT_INN_BUFFS_STATE,
    startingBuffs: [],
  };

  const handlePurchaseRest = (restOptionId: string) => {
    const result = purchaseRest(currentGold, currentInnBuffs, restOptionId);

    if (result.success && result.newInnBuffs !== undefined && result.newGold !== undefined) {
      // Spend gold
      const goldCost = currentGold - result.newGold;
      if (goldCost > 0) {
        spendGold(goldCost);
      }

      // Update inn buffs state
      updatePlayerData({
        progression: {
          ...playerData.progression,
          innBuffsState: result.newInnBuffs,
        },
      });

      setNotification(result.message);
      setTimeout(() => setNotification(null), 2000);
    } else {
      setNotification(result.message);
      setTimeout(() => setNotification(null), 2000);
    }
  };

  return (
    <div className="rest-tab">
      {notification && <div className="inn-notification">{notification}</div>}

      <div className="rest-section">
        <h3 className="inn-section-title">休息オプション</h3>
        <p className="inn-section-description">
          休息を取ると、次の探索でボーナスを得られます。
        </p>

        <div className="rest-options-grid">
          {REST_OPTIONS.map((option) => {
            const canAfford = canAffordInnService(currentGold, option.cost);

            return (
              <div
                key={option.id}
                className={`rest-option-card ${!canAfford && option.cost > 0 ? "unaffordable" : ""}`}
              >
                <div className="option-icon">{option.icon}</div>
                <div className="option-info">
                  <h4 className="option-name">{option.nameJa}</h4>
                  <p className="option-description">{option.descriptionJa}</p>
                  {option.effects.length > 0 && (
                    <div className="option-effects">
                      {option.effects.map((effect, idx) => (
                        <span key={idx} className="effect-badge">
                          {effect.type === "bonusHp" && `HP +${effect.value}`}
                          {effect.type === "energyBonus" && `エネルギー +${effect.value}`}
                        </span>
                      ))}
                    </div>
                  )}
                </div>
                <div className="option-action">
                  <span className="option-cost">
                    {option.cost > 0 ? `${option.cost} G` : "無料"}
                  </span>
                  <button
                    className="rest-button"
                    onClick={() => handlePurchaseRest(option.id)}
                    disabled={!canAfford && option.cost > 0}
                  >
                    {option.cost > 0 ? "予約" : "休憩"}
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};

export default RestTab;
