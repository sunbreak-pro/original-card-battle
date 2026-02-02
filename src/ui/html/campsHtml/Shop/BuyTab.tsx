import { useState } from "react";
import { usePlayer } from "@/contexts/PlayerContext";
import { useResources } from "@/contexts/ResourceContext";
import { useInventory } from "@/contexts/InventoryContext";
import {
  getResolvedConsumableListings,
  getResolvedTeleportListings,
  generateDailyEquipmentInventory,
  type ResolvedShopListing,
  type EquipmentListing,
} from "@/constants/data/camps/ShopData";
import {
  canAfford,
  hasInventorySpace,
  purchaseItem,
} from "@/domain/camps/logic/shopLogic";
import { generateEquipmentItem } from "@/domain/item_equipment/logic/generateItem";

const BuyTab = () => {
  const { playerData, updatePlayerData } = usePlayer();
  const { spendGold } = useResources();
  const { addItemToStorage } = useInventory();
  const storage = playerData.inventory.storage;
  const [notification, setNotification] = useState<string | null>(null);

  const consumableListings = getResolvedConsumableListings();
  const teleportListings = getResolvedTeleportListings();
  const [dayNumber] = useState(() => {
    const saved = playerData.progression.shopRotationDay;
    if (saved != null) return saved;
    const computed = Math.floor(Date.now() / 86400000);
    // Persist to progression so save/load preserves the same lineup
    updatePlayerData({
      progression: { ...playerData.progression, shopRotationDay: computed },
    });
    return computed;
  });
  const dailyEquipment = generateDailyEquipmentInventory(dayNumber);

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

    if (!hasInventorySpace(storage.currentCapacity, storage.maxCapacity)) {
      showNotification("倉庫がいっぱいです！");
      return;
    }

    const item = purchaseItem(resolved.listing);
    if (!item) {
      showNotification("Purchase failed!");
      return;
    }

    if (spendGold(resolved.price)) {
      addItemToStorage(item);
      showNotification(`Purchased ${resolved.data.name}!`);
    }
  };

  const handleBuyEquipment = (listing: EquipmentListing) => {
    const totalGold =
      playerData.resources.baseCampGold + playerData.resources.explorationGold;
    if (!canAfford(totalGold, listing.price)) {
      showNotification("ゴールドが足りません！");
      return;
    }

    if (!hasInventorySpace(storage.currentCapacity, storage.maxCapacity)) {
      showNotification("倉庫がいっぱいです！");
      return;
    }

    if (spendGold(listing.price)) {
      const item = generateEquipmentItem(listing.slot, listing.rarity);
      addItemToStorage(item);
      showNotification(`${listing.name} を購入しました！`);
    }
  };

  const renderListing = (resolved: ResolvedShopListing) => {
    const totalGold =
      playerData.resources.baseCampGold + playerData.resources.explorationGold;
    const affordable = canAfford(totalGold, resolved.price);
    const hasSpace = hasInventorySpace(
      storage.currentCapacity,
      storage.maxCapacity,
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

  const renderEquipmentListing = (listing: EquipmentListing, idx: number) => {
    const totalGold =
      playerData.resources.baseCampGold + playerData.resources.explorationGold;
    const affordable = canAfford(totalGold, listing.price);
    const hasSpace = hasInventorySpace(
      storage.currentCapacity,
      storage.maxCapacity,
    );

    return (
      <div
        key={`${listing.slot}_${listing.rarity}_${idx}`}
        className={`shop-item equipment-item rarity-${listing.rarity} ${!affordable ? "unaffordable" : ""}`}
      >
        <div className="item-icon">{listing.icon}</div>
        <div className="item-info">
          <div className="item-name">{listing.name}</div>
          <div className="item-description">
            {listing.slot} - {listing.rarity}
          </div>
        </div>
        <div className="item-price">{listing.price} G</div>
        <button
          className="buy-button"
          onClick={() => handleBuyEquipment(listing)}
          disabled={!affordable || !hasSpace}
        >
          Buy
        </button>
      </div>
    );
  };

  return (
    <div className="buy-tab">
      {notification && <div className="shop-notification">{notification}</div>}

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
        <h2 className="section-title">装備品 (日替わり)</h2>
        <div className="shop-items-grid">
          {dailyEquipment.map(renderEquipmentListing)}
        </div>
      </section>
    </div>
  );
};

export default BuyTab;
