import { useState } from "react";
import { useGameState } from "../../../domain/camps/contexts/GameStateContext";
import { usePlayer } from "../../../domain/camps/contexts/PlayerContext";
import { calculateMagicStoneValue } from "../../../domain/item_equipment/type/ItemTypes";
import BuyTab from "./BuyTab";
import SellTab from "./SellTab";
import ExchangeTab from "./ExchangeTab";
import "../../css/camps/Shop.css";

type ShopTab = "buy" | "sell" | "exchange";

export const Shop = () => {
  const [selectedTab, setSelectedTab] = useState<ShopTab>("buy");
  const { returnToCamp } = useGameState();
  const { player } = usePlayer();

  const totalMagicStoneValue = calculateMagicStoneValue(
    player.baseCampMagicStones
  );

  return (
    <div className="shop-screen">
      {/* Header */}
      <header className="shop-header">
        <div className="shop-title-row">
          <h1 className="shop-title">Merchant's Exchange</h1>
        </div>
        <div className="shop-resources">
          <div className="resource-display gold">
            <span className="resource-icon">💰</span>
            <span className="resource-value">{player.baseCampGold} G</span>
          </div>
          <div className="resource-display stones">
            <span className="resource-icon">💎</span>
            <span className="resource-value">
              {totalMagicStoneValue} G worth
            </span>
          </div>
        </div>
      </header>

      {/* Tab Navigation */}
      <nav className="shop-tabs">
        <button
          className={`shop-tab ${selectedTab === "buy" ? "active" : ""}`}
          onClick={() => setSelectedTab("buy")}
        >
          <span className="tab-icon">🛒</span>
          <span className="tab-label">Buy</span>
        </button>
        <button
          className={`shop-tab ${selectedTab === "sell" ? "active" : ""}`}
          onClick={() => setSelectedTab("sell")}
        >
          <span className="tab-icon">💵</span>
          <span className="tab-label">Sell</span>
        </button>
        <button
          className={`shop-tab ${selectedTab === "exchange" ? "active" : ""}`}
          onClick={() => setSelectedTab("exchange")}
        >
          <span className="tab-icon">🔄</span>
          <span className="tab-label">Exchange</span>
        </button>
      </nav>

      {/* Tab Content */}
      <div className="shop-content">
        {selectedTab === "buy" && <BuyTab />}
        {selectedTab === "sell" && <SellTab />}
        {selectedTab === "exchange" && <ExchangeTab />}
      </div>

      {/* Back Button */}
      <button className="shop-back-button" onClick={returnToCamp}>
        ← Back to Camp
      </button>
    </div>
  );
};

export default Shop;
