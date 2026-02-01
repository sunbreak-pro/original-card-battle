import { useState } from "react";
import type { ShopTab } from "@/types/campTypes";
import { SHOP_TABS } from "@/constants/campConstants";
import BuyTab from "./BuyTab";
import SellTab from "./SellTab";
import ExchangeTab from "./ExchangeTab";
import FacilityHeader from "../../componentsHtml/FacilityHeader";
import BackToCampButton from "../../componentsHtml/BackToCampButton";
import FacilityTabNav from "../../componentsHtml/FacilityTabNav";
import "../../../css/camps/Shop.css";

export const Shop = () => {
  const [selectedTab, setSelectedTab] = useState<ShopTab>("buy");

  return (
    <div className="shop-screen">
      {/* Header */}
      <FacilityHeader title="取引所" />

      {/* Tab Navigation */}
      <FacilityTabNav
        tabs={SHOP_TABS}
        activeTab={selectedTab}
        onTabChange={setSelectedTab}
        facility="shop"
      />

      {/* Tab Content */}
      <div className="shop-content">
        {selectedTab === "buy" && <BuyTab />}
        {selectedTab === "sell" && <SellTab />}
        {selectedTab === "exchange" && <ExchangeTab />}
      </div>

      {/* Back Button */}
      <BackToCampButton />
    </div>
  );
};

export default Shop;
