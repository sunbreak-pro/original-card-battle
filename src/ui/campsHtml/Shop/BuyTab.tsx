import { useState } from "react";
import { usePlayer } from "../../../contexts/PlayerContext";
import { useInventory } from "../../../contexts/InventoryContext";
import {
  getResolvedConsumableListings,
  getResolvedTeleportListings,
  EQUIPMENT_PACKS,
  type ResolvedShopListing,
} from "../../../domain/camps/data/ShopData";
import type { EquipmentPackConfig } from '@/types/campTypes';
import {
  canAfford,
  hasInventorySpace,
  purchaseItem,
  openEquipmentPack,
} from "../../../domain/camps/logic/shopLogic";

const BuyTab = () => {
  const { playerData, useGold } = usePlayer();
  const { addItemToInventory } = useInventory();
  const inventory = playerData.inventory.inventory;
  const [notification, setNotification] = useState<string | null>(null);
  const [purchasedPack, setPurchasedPack] = useState<string[] | null>(null);

  const consumableListings = getResolvedConsumableListings();
  const teleportListings = getResolvedTeleportListings();

  const showNotification = (message: string) => {
    setNotification(message);
    setTimeout(() => setNotification(null), 2000);
  };

  const handleBuyItem = (resolved: ResolvedShopListing) => {
    const totalGold =
      playerData.resources.baseCampGold + playerData.resources.explorationGold;
    if (!canAfford(totalGold, resolved.price)) {
      showNotification("Not enough gold!");
      return;
    }

    if (!hasInventorySpace(inventory.currentCapacity, inventory.maxCapacity)) {
      showNotification("Inventory full!");
      return;
    }

    const item = purchaseItem(resolved.listing);
    if (!item) {
      showNotification("Purchase failed!");
      return;
    }

    if (useGold(resolved.price)) {
      addItemToInventory(item);
      showNotification(`Purchased ${resolved.data.name}!`);
    }
  };

  const handleBuyPack = (pack: EquipmentPackConfig) => {
    const totalGold =
      playerData.resources.baseCampGold + playerData.resources.explorationGold;
    if (!canAfford(totalGold, pack.price)) {
      showNotification("Not enough gold!");
      return;
    }

    if (
      !hasInventorySpace(inventory.currentCapacity, inventory.maxCapacity, 6)
    ) {
      showNotification("Need 6 inventory slots!");
      return;
    }

    if (useGold(pack.price)) {
      const items = openEquipmentPack(pack.id);
      items.forEach((item) => addItemToInventory(item));
      setPurchasedPack(items.map((i) => `${i.type} ${i.name} (${i.rarity})`));
      setTimeout(() => setPurchasedPack(null), 4000);
    }
  };

  const renderListing = (resolved: ResolvedShopListing) => {
    const totalGold =
      playerData.resources.baseCampGold + playerData.resources.explorationGold;
    const affordable = canAfford(totalGold, resolved.price);
    const hasSpace = hasInventorySpace(
      inventory.currentCapacity,
      inventory.maxCapacity,
    );

    return (
      <div
        key={resolved.data.typeId}
        className={`shop-item ${!affordable ? "unaffordable" : ""}`}
      >
        <div className="item-icon">{resolved.data.icon}</div>
        <div className="item-info">
          <div className="item-name">{resolved.data.name}</div>
          <div className="item-description">{resolved.data.description}</div>
        </div>
        <div className="item-price">{resolved.price} G</div>
        <button
          className="buy-button"
          onClick={() => handleBuyItem(resolved)}
          disabled={!affordable || !hasSpace}
        >
          Buy
        </button>
      </div>
    );
  };

  const renderPackItem = (pack: EquipmentPackConfig) => {
    const totalGold =
      playerData.resources.baseCampGold + playerData.resources.explorationGold;
    const affordable = canAfford(totalGold, pack.price);
    const hasSpace = hasInventorySpace(
      inventory.currentCapacity,
      inventory.maxCapacity,
      6,
    );

    return (
      <div
        key={pack.id}
        className={`shop-item pack ${!affordable ? "unaffordable" : ""}`}
      >
        <div className="item-icon">{pack.icon}</div>
        <div className="item-info">
          <div className="item-name">{pack.name}</div>
          <div className="item-description">{pack.description}</div>
          <div className="pack-badge">6 Items</div>
        </div>
        <div className="item-price">{pack.price} G</div>
        <button
          className="buy-button pack-buy"
          onClick={() => handleBuyPack(pack)}
          disabled={!affordable || !hasSpace}
        >
          Open
        </button>
      </div>
    );
  };

  return (
    <div className="buy-tab">
      {notification && <div className="shop-notification">{notification}</div>}

      {purchasedPack && (
        <div className="pack-result">
          <h3>Pack Opened!</h3>
          <div className="pack-items">
            {purchasedPack.map((item, idx) => (
              <div key={idx} className="pack-item-result">
                {item}
              </div>
            ))}
          </div>
        </div>
      )}

      <section className="shop-section">
        <h2 className="section-title">Consumables</h2>
        <div className="shop-items-grid">
          {consumableListings.map(renderListing)}
        </div>
      </section>

      <section className="shop-section">
        <h2 className="section-title">Teleport Stones</h2>
        <div className="shop-items-grid">
          {teleportListings.map(renderListing)}
        </div>
      </section>

      <section className="shop-section">
        <h2 className="section-title">Equipment Packs</h2>
        <div className="shop-items-grid">
          {EQUIPMENT_PACKS.map(renderPackItem)}
        </div>
      </section>
    </div>
  );
};

export default BuyTab;
