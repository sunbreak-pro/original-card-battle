import { useState } from "react";
import { usePlayer } from "../../../domain/camps/contexts/PlayerContext";
import type { MagicStones } from "../../../domain/item_equipment/type/ItemTypes";
import {
  calculateMagicStoneTotal,
  calculateStonesToConsume,
} from "../../../domain/camps/logic/shopLogic";

const ExchangeTab = () => {
  const { player, addGold, updatePlayer } = usePlayer();
  const [exchangeAmount, setExchangeAmount] = useState<number>(0);
  const [notification, setNotification] = useState<string | null>(null);

  const stones = player.baseCampMagicStones;
  const totalValue = calculateMagicStoneTotal(stones);

  const showNotification = (message: string) => {
    setNotification(message);
    setTimeout(() => setNotification(null), 2000);
  };

  const handleExchange = () => {
    if (exchangeAmount <= 0) {
      showNotification("Enter an amount to exchange!");
      return;
    }

    if (exchangeAmount > totalValue) {
      showNotification("Not enough magic stones!");
      return;
    }

    const result = calculateStonesToConsume(stones, exchangeAmount);
    if (!result) {
      showNotification("Exchange failed!");
      return;
    }

    // Update player's magic stones
    updatePlayer({
      baseCampMagicStones: result.newStones,
    });

    // Add gold (actual value consumed, may be slightly more than requested)
    addGold(result.actualValue, true);

    showNotification(`Exchanged for ${result.actualValue} G!`);
    setExchangeAmount(0);
  };

  const handleQuickExchange = (amount: number) => {
    if (amount > totalValue) {
      showNotification("Not enough magic stones!");
      return;
    }

    const result = calculateStonesToConsume(stones, amount);
    if (!result) {
      showNotification("Exchange failed!");
      return;
    }

    updatePlayer({
      baseCampMagicStones: result.newStones,
    });
    addGold(result.actualValue, true);

    showNotification(`Exchanged for ${result.actualValue} G!`);
  };

  // Exchange a single stone of a specific type
  const handleExchangeSingleStone = (
    type: keyof MagicStones,
    value: number
  ) => {
    if (stones[type] <= 0) {
      showNotification("No stones of this type!");
      return;
    }

    // Deduct one stone of the specified type
    const newStones = {
      ...stones,
      [type]: stones[type] - 1,
    };

    updatePlayer({
      baseCampMagicStones: newStones,
    });
    addGold(value, true);

    showNotification(`Exchanged for ${value} G!`);
  };

  const renderStoneRow = (
    type: keyof MagicStones,
    label: string,
    value: number,
    icon: string
  ) => {
    const count = stones[type];
    return (
      <>
        {count > 0 && (
          <div className="stone-row" key={type}>
            <div className="stone-icon">{icon}</div>
            <div className="stone-label">{label}</div>
            <div className="stone-count">x {count}</div>
            <div className="stone-value">{value} G</div>
            <button
              className="stone-exchange-btn"
              onClick={() => handleExchangeSingleStone(type, value)}
              disabled={count <= 0}
            >
              Exchange
            </button>
          </div>
        )}
      </>
    );
  };

  return (
    <div className="exchange-tab">
      {notification && <div className="shop-notification">{notification}</div>}

      <section className="exchange-section">
        <h2 className="section-title">Magic Stone Inventory</h2>

        <div className="stone-list">
          {renderStoneRow("small", "Small Magic Stone", 30, "💎")}
          {renderStoneRow("medium", "Medium Magic Stone", 100, "💠")}
          {renderStoneRow("large", "Large Magic Stone", 350, "🔷")}
          {renderStoneRow("huge", "Huge Magic Stone", 1000, "🔶")}
          <div className="quick-exchange">
            <span className="quick-exchange-tittle">Quick Exchange </span>
            <span className="quick-exchange-value">({totalValue}G)</span>
            <button
              className="stone-exchange-btn"
              onClick={() => handleQuickExchange(totalValue)}
              disabled={totalValue <= 0}
            >
              All exchange
            </button>
          </div>
        </div>
      </section>

      <section className="exchange-section">
        <h2 className="section-title">Exchange Stones for Gold</h2>

        <div className="exchange-controls">
          <div className="exchange-input-group">
            <label>Amount to exchange:</label>
            <input
              type="number"
              className="exchange-input"
              value={exchangeAmount}
              onChange={(e) =>
                setExchangeAmount(Math.max(0, parseInt(e.target.value) || 0))
              }
              min={0}
              max={totalValue}
            />
            <span className="input-suffix">G</span>
          </div>

          <button
            className="exchange-button"
            onClick={handleExchange}
            disabled={exchangeAmount <= 0 || exchangeAmount > totalValue}
          >
            Exchange
          </button>
        </div>

        <p className="exchange-note">
          Note: Smallest stones are consumed first. Due to stone denominations,
          actual gold received may be slightly more than requested.
        </p>
      </section>
    </div>
  );
};

export default ExchangeTab;
