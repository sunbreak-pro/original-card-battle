/**
 * DiningTab - Meal options for the Inn
 *
 * Displays available meals and allows purchase.
 * Meals provide buffs that last for a number of battles in the next exploration.
 */

import { useState } from "react";
import { usePlayer } from "@/contexts/PlayerContext";
import { useResources } from "@/contexts/ResourceContext";
import { MEAL_OPTIONS, DEFAULT_INN_BUFFS_STATE } from "@/constants/data/camps/InnData";
import { purchaseMeal, canAffordInnService } from "@/domain/camps/logic/innLogic";
import type { InnBuffsState } from "@/types/campTypes";

const DiningTab = () => {
  const { playerData, updatePlayerData } = usePlayer();
  const { getTotalGold, spendGold } = useResources();
  const [notification, setNotification] = useState<string | null>(null);

  const currentGold = getTotalGold();
  const currentInnBuffs: InnBuffsState = playerData.progression.innBuffsState ?? {
    ...DEFAULT_INN_BUFFS_STATE,
    startingBuffs: [],
  };

  const handlePurchaseMeal = (mealOptionId: string) => {
    const result = purchaseMeal(currentGold, currentInnBuffs, mealOptionId);

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

  // Get buff display name
  const getBuffName = (buffType: string): string => {
    const names: Record<string, string> = {
      defUpMinor: "防御力上昇",
      atkUpMinor: "攻撃力上昇",
      haste: "加速",
      regeneration: "リジェネ",
    };
    return names[buffType] ?? buffType;
  };

  return (
    <div className="dining-tab">
      {notification && <div className="inn-notification">{notification}</div>}

      <div className="dining-section">
        <h3 className="inn-section-title">食事メニュー</h3>
        <p className="inn-section-description">
          食事を取ると、次の探索で一定戦闘数の間バフを得られます。
        </p>

        <div className="meal-options-grid">
          {MEAL_OPTIONS.map((meal) => {
            const canAfford = canAffordInnService(currentGold, meal.cost);

            return (
              <div
                key={meal.id}
                className={`meal-option-card ${!canAfford ? "unaffordable" : ""}`}
              >
                <div className="option-icon">{meal.icon}</div>
                <div className="option-info">
                  <h4 className="option-name">{meal.nameJa}</h4>
                  <p className="option-description">{meal.descriptionJa}</p>
                  <div className="option-effects">
                    {meal.effects.map((effect, idx) => (
                      <span key={idx} className="effect-badge">
                        {effect.type === "buff" && effect.buffType && getBuffName(effect.buffType)}
                        {effect.type === "bonusHp" && `HP +${effect.value}`}
                        {effect.type === "goldBonus" && `金貨 +${effect.value}%`}
                      </span>
                    ))}
                    <span className="duration-badge">{meal.duration}戦闘</span>
                  </div>
                </div>
                <div className="option-action">
                  <span className="option-cost">{meal.cost} G</span>
                  <button
                    className="meal-button"
                    onClick={() => handlePurchaseMeal(meal.id)}
                    disabled={!canAfford}
                  >
                    注文
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

export default DiningTab;
