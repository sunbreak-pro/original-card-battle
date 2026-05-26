import { useState, useCallback, useMemo } from "react";
import { usePlayer } from "@/contexts/PlayerContext";
import { useResources } from "@/contexts/ResourceContext";
import { useInventory } from "@/contexts/InventoryContext";
import {
  getResolvedPermanentListings,
  getResolvedDailySpecialListings,
  generateDailyEquipmentInventory,
  type ResolvedShopListing,
  type EquipmentListing,
} from "@/constants/data/camps/ShopData";
import {
  canAfford,
  hasInventorySpace,
  purchaseItem,
  useMerchantTicket,
} from "@/domain/camps/logic/shopLogic";
import {
  initializeShopStock,
  decrementConsumableStock,
  markEquipmentSoldOut,
  getConsumableStock,
  isEquipmentAvailable,
  clearNewStockFlag,
} from "@/domain/camps/logic/shopStockLogic";
import { generateEquipmentItem } from "@/domain/item_equipment/logic/generateItem";
import type { ShopStockState } from "@/types/campTypes";

const BuyTab = () => {
  const { playerData, updatePlayerData } = usePlayer();
  const { spendGold } = useResources();
  const { addItemToStorage } = useInventory();
  const storage = playerData.inventory.storage;
  const [notification, setNotification] = useState<string | null>(null);

  // Lazily initialize shop stock if not yet created
  const shopStock = useMemo((): ShopStockState => {
    const existing = playerData.progression.shopStockState;
    if (existing) return existing;
    const seed = Math.floor(Date.now() / 1000);
    const depth = Math.max(...playerData.progression.unlockedDepths) as 1 | 2 | 3 | 4 | 5;
    return initializeShopStock(seed, depth);
  }, [playerData.progression.shopStockState, playerData.progression.unlockedDepths]);

  // Persist stock if it was just initialized
  const [hasInitialized, setHasInitialized] = useState(false);
  if (!playerData.progression.shopStockState && !hasInitialized) {
    setHasInitialized(true);
    const stockToSave = clearNewStockFlag(shopStock);
    updatePlayerData({
      progression: { ...playerData.progression, shopStockState: stockToSave },
    });
  }

  // Clear new stock flag when entering BuyTab
  const [clearedNotification, setClearedNotification] = useState(false);
  if (shopStock.hasNewStock && !clearedNotification) {
    setClearedNotification(true);
    const cleared = clearNewStockFlag(shopStock);
    updatePlayerData({
      progression: { ...playerData.progression, shopStockState: cleared },
    });
  }

  // Resolve listings from stock state
  const permanentListings = useMemo(() => getResolvedPermanentListings(), []);
  const dailySpecialListings = useMemo(
    () => getResolvedDailySpecialListings(shopStock.dailySpecialKeys),
    [shopStock.dailySpecialKeys],
  );
  const dayNumber = playerData.progression.shopRotationDay ?? Math.floor(Date.now() / 86400000);
  const dailyEquipment = useMemo(() => generateDailyEquipmentInventory(dayNumber), [dayNumber]);

  // Check if player has merchant ticket in inventory
  const hasMerchantTicket = useMemo(() => {
    const allItems = [
      ...playerData.inventory.storage.items,
      ...playerData.inventory.inventory.items,
    ];
    return allItems.some(item => item.typeId === "merchant_ticket");
  }, [playerData.inventory.storage.items, playerData.inventory.inventory.items]);

  const updateStock = useCallback((newStock: ShopStockState) => {
    updatePlayerData({
      progression: { ...playerData.progression, shopStockState: newStock },
    });
  }, [updatePlayerData, playerData.progression]);

  const showNotification = useCallback((message: string) => {
    setNotification(message);
    setTimeout(() => setNotification(null), 2000);
  }, []);

  const handleBuyConsumable = useCallback((resolved: ResolvedShopListing) => {
    const totalGold = playerData.resources.baseCampGold + playerData.resources.explorationGold;
    if (!canAfford(totalGold, resolved.price)) {
      showNotification("ゴールドが足りません！");
      return;
    }
    if (!hasInventorySpace(storage.currentCapacity, storage.maxCapacity)) {
      showNotification("倉庫がいっぱいです！");
      return;
    }

    const newStock = decrementConsumableStock(shopStock, resolved.listing.itemTypeId);
    if (!newStock) {
      showNotification("売り切れです！");
      return;
    }

    const item = purchaseItem(resolved.listing);
    if (!item) {
      showNotification("購入に失敗しました");
      return;
    }

    if (spendGold(resolved.price)) {
      addItemToStorage(item);
      updateStock(newStock);
      showNotification(`${resolved.data.nameJa} を購入しました！`);
    }
  }, [playerData.resources, storage, shopStock, spendGold, addItemToStorage, updateStock, showNotification]);

  const handleBuyEquipment = useCallback((listing: EquipmentListing, index: number) => {
    const totalGold = playerData.resources.baseCampGold + playerData.resources.explorationGold;
    if (!canAfford(totalGold, listing.price)) {
      showNotification("ゴールドが足りません！");
      return;
    }
    if (!hasInventorySpace(storage.currentCapacity, storage.maxCapacity)) {
      showNotification("倉庫がいっぱいです！");
      return;
    }

    const newStock = markEquipmentSoldOut(shopStock, index);
    if (!newStock) {
      showNotification("売り切れです！");
      return;
    }

    if (spendGold(listing.price)) {
      const item = generateEquipmentItem(listing.slot, listing.rarity);
      addItemToStorage(item);
      updateStock(newStock);
      showNotification(`${listing.name} を購入しました！`);
    }
  }, [playerData.resources, storage, shopStock, spendGold, addItemToStorage, updateStock, showNotification]);

  const handleUseMerchantTicket = useCallback(() => {
    const depth = Math.max(...playerData.progression.unlockedDepths) as 1 | 2 | 3 | 4 | 5;
    const newStock = useMerchantTicket(depth);

    // Find and consume merchant ticket from storage or inventory
    const storageItems = playerData.inventory.storage.items;
    const ticketIdx = storageItems.findIndex(item => item.typeId === "merchant_ticket");
    if (ticketIdx >= 0) {
      const updatedItems = [...storageItems];
      updatedItems.splice(ticketIdx, 1);
      updatePlayerData({
        progression: { ...playerData.progression, shopStockState: newStock },
        inventory: {
          ...playerData.inventory,
          storage: {
            ...playerData.inventory.storage,
            items: updatedItems,
            currentCapacity: updatedItems.length,
          },
        },
      });
    } else {
      // Try inventory items
      const invItems = playerData.inventory.inventory.items;
      const invIdx = invItems.findIndex(item => item.typeId === "merchant_ticket");
      if (invIdx >= 0) {
        const updatedInvItems = [...invItems];
        updatedInvItems.splice(invIdx, 1);
        updatePlayerData({
          progression: { ...playerData.progression, shopStockState: newStock },
          inventory: {
            ...playerData.inventory,
            inventory: {
              ...playerData.inventory.inventory,
              items: updatedInvItems,
              currentCapacity: updatedInvItems.length,
            },
          },
        });
      }
    }
    showNotification("品揃えが更新されました！");
  }, [playerData, updatePlayerData, showNotification]);

  const totalGold = playerData.resources.baseCampGold + playerData.resources.explorationGold;

  const renderConsumableListing = (resolved: ResolvedShopListing) => {
    const stock = getConsumableStock(shopStock, resolved.listing.itemTypeId);
    const soldOut = stock <= 0;
    const affordable = canAfford(totalGold, resolved.price);
    const hasSpace = hasInventorySpace(storage.currentCapacity, storage.maxCapacity);

    return (
      <div
        key={resolved.data.typeId}
        className={`shop-item ${soldOut ? "sold-out" : ""} ${!affordable && !soldOut ? "unaffordable" : ""}`}
      >
        <div className="item-icon">{resolved.data.icon}</div>
        <div className="item-info">
          <div className="item-name">{resolved.data.nameJa}</div>
          <div className="item-description">{resolved.data.descriptionJa}</div>
        </div>
        <div className="stock-badge-area">
          {soldOut ? (
            <span className="stock-badge out-of-stock">売切</span>
          ) : (
            <span className={`stock-badge ${stock <= 2 ? "low-stock" : ""}`}>
              残り: {stock}
            </span>
          )}
        </div>
        <div className="item-price">{resolved.price} G</div>
        <button
          className="buy-button"
          onClick={() => handleBuyConsumable(resolved)}
          disabled={soldOut || !affordable || !hasSpace}
        >
          購入
        </button>
      </div>
    );
  };

  const renderEquipmentListing = (listing: EquipmentListing, idx: number) => {
    const available = isEquipmentAvailable(shopStock, idx);
    const affordable = canAfford(totalGold, listing.price);
    const hasSpace = hasInventorySpace(storage.currentCapacity, storage.maxCapacity);

    return (
      <div
        key={`${listing.slot}_${listing.rarity}_${idx}`}
        className={`shop-item equipment-item rarity-${listing.rarity} ${!available ? "sold-out" : ""} ${!affordable && available ? "unaffordable" : ""}`}
      >
        <div className="item-icon">{listing.icon}</div>
        <div className="item-info">
          <div className="item-name">{listing.name}</div>
          <div className="item-description">
            {listing.slot} - {listing.rarity}
          </div>
        </div>
        <div className="stock-badge-area">
          {!available ? (
            <span className="stock-badge out-of-stock">売切</span>
          ) : (
            <span className="stock-badge">残り: 1</span>
          )}
        </div>
        <div className="item-price">{listing.price} G</div>
        <button
          className="buy-button"
          onClick={() => handleBuyEquipment(listing, idx)}
          disabled={!available || !affordable || !hasSpace}
        >
          購入
        </button>
      </div>
    );
  };

  return (
    <div className="buy-tab">
      {notification && <div className="shop-notification">{notification}</div>}

      {/* Merchant Ticket Button */}
      {hasMerchantTicket && (
        <button
          className="merchant-ticket-button"
          onClick={handleUseMerchantTicket}
        >
          🎟️ 商人チケットを使う（品揃え更新）
        </button>
      )}

      <section className="shop-section">
        <h2 className="shop-section-title">常設品</h2>
        <div className="shop-items-grid">
          {permanentListings.map(renderConsumableListing)}
        </div>
      </section>

      <section className="shop-section">
        <h2 className="shop-section-title">日替わり特売</h2>
        <div className="shop-items-grid">
          {dailySpecialListings.length > 0 ? (
            dailySpecialListings.map(renderConsumableListing)
          ) : (
            <div className="empty-section">本日の特売品はありません</div>
          )}
        </div>
      </section>

      <section className="shop-section">
        <h2 className="shop-section-title">装備品 (日替わり)</h2>
        <div className="shop-items-grid">
          {dailyEquipment.map(renderEquipmentListing)}
        </div>
      </section>
    </div>
  );
};

export default BuyTab;
