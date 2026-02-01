import { useState } from "react";
import { useGameState } from "@/contexts/GameStateContext";
import BuyTab from "./BuyTab";
import SellTab from "./SellTab";
import ExchangeTab from "./ExchangeTab";
import FacilityHeader from "../../componentsHtml/FacilityHeader";
import "../../../css/camps/Shop.css";

type ShopTab = "buy" | "sell" | "exchange";

export const Shop = () => {
  const [selectedTab, setSelectedTab] = useState<ShopTab>("buy");
  const { returnToCamp } = useGameState();

  return (
    <div className="shop-screen">
      {/* Header */}
      <FacilityHeader title="取引所" />

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
