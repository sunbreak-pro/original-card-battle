import { useState, useCallback, useMemo } from "react";
import { usePlayer } from "@/contexts/PlayerContext";
import { useResources } from "@/contexts/ResourceContext";
import { useInventory } from "@/contexts/InventoryContext";
import { resolveConsumableByKey, type ResolvedShopListing } from "@/constants/data/camps/ShopData";
import { EQUIPMENT_TEMPLATES } from "@/constants/data/items/EquipmentData";
import { EQUIPMENT_SLOTS, EQUIPMENT_BUY_PRICES } from "@/constants/itemConstants";
import {
  canAfford,
  hasInventorySpace,
  purchaseItem,
} from "@/domain/camps/logic/shopLogic";
import {
  initializeShopStock,
  initializeDarkMarketStock,
  decrementDarkMarketConsumableStock,
  markDarkMarketEquipmentSoldOut,
  getDarkMarketConsumableStock,
  isDarkMarketEquipmentAvailable,
  clearDarkMarketNewStockFlag,
} from "@/domain/camps/logic/shopStockLogic";
import { generateEquipmentItem } from "@/domain/item_equipment/logic/generateItem";
import {
  DARK_MARKET_PRICE_MULTIPLIER,
  DARK_MARKET_MIN_EQUIPMENT_RARITY,
  DARK_MARKET_EQUIPMENT_SLOTS,
  DARK_MARKET_LEGENDARY_RATE,
} from "@/constants/data/camps/DarkMarketConstants";
import type { ShopStockState, Depth } from "@/types/campTypes";
import type { ItemRarity, EquipmentSlot } from "@/types/itemTypes";

interface DarkMarketEquipmentListing {
  slot: EquipmentSlot;
  rarity: ItemRarity;
  name: string;
  icon: string;
  price: number;
}

/**
 * Seeded RNG for deterministic equipment generation
 */
function seededRandom(seed: number): () => number {
  let s = seed;
  return () => {
    s = (s * 1664525 + 1013904223) & 0x7fffffff;
    return s / 0x7fffffff;
  };
}

/**
 * Generate dark market equipment inventory (rare+ only)
 */
function generateDarkMarketEquipment(seed: number, depth: Depth): DarkMarketEquipmentListing[] {
  const rng = seededRandom(seed * 8831 + 17);
  const listings: DarkMarketEquipmentListing[] = [];
  const usedSlots = new Set<string>();
  const legendaryRate = DARK_MARKET_LEGENDARY_RATE[depth];

  for (let i = 0; i < DARK_MARKET_EQUIPMENT_SLOTS; i++) {
    // Pick a slot (avoid duplicates)
    let slotIdx = Math.floor(rng() * EQUIPMENT_SLOTS.length);
    let attempts = 0;
    while (usedSlots.has(EQUIPMENT_SLOTS[slotIdx]) && attempts < 10) {
      slotIdx = (slotIdx + 1) % EQUIPMENT_SLOTS.length;
      attempts++;
    }
    const slot = EQUIPMENT_SLOTS[slotIdx];
    usedSlots.add(slot);

    // Pick rarity (rare, epic, legendary only)
    const rarityRoll = rng();
    let rarity: ItemRarity;
    if (rarityRoll < legendaryRate) {
      rarity = "legendary";
    } else if (rarityRoll < legendaryRate + 0.3) {
      rarity = "epic";
    } else {
      rarity = "rare";
    }

    // Ensure minimum rarity
    const minRarityOrder = ["common", "uncommon", "rare", "epic", "legendary"];
    const minIdx = minRarityOrder.indexOf(DARK_MARKET_MIN_EQUIPMENT_RARITY);
    const currentIdx = minRarityOrder.indexOf(rarity);
    if (currentIdx < minIdx) {
      rarity = DARK_MARKET_MIN_EQUIPMENT_RARITY;
    }

    const template = EQUIPMENT_TEMPLATES[slot][rarity];
    const basePrice = EQUIPMENT_BUY_PRICES[rarity];
    const darkMarketPrice = Math.floor(basePrice * DARK_MARKET_PRICE_MULTIPLIER);

    listings.push({
      slot,
      rarity,
      name: template.name,
      icon: template.icon,
      price: darkMarketPrice,
    });
  }

  return listings;
}

const DarkMarketTab = () => {
  const { playerData, updatePlayerData } = usePlayer();
  const { spendGold } = useResources();
  const { addItemToStorage } = useInventory();
  const storage = playerData.inventory.storage;
  const [notification, setNotification] = useState<string | null>(null);

  // Stable seed for initialization (generated once on mount)
  const [initSeed] = useState(() => Math.floor(Date.now() / 1000));

  // Get current depth for equipment generation
  const currentDepth = useMemo(() => {
    return Math.max(...playerData.progression.unlockedDepths) as Depth;
  }, [playerData.progression.unlockedDepths]);

  // Lazily initialize shop stock if not yet created
  const shopStock = useMemo((): ShopStockState => {
    const existing = playerData.progression.shopStockState;
    if (existing) {
      // Check if dark market fields exist (migration)
      if (existing.darkMarketConsumableStock === undefined) {
        const seed = existing.rotationSeed ?? initSeed;
        const darkMarketFields = initializeDarkMarketStock(seed, currentDepth);
        return { ...existing, ...darkMarketFields };
      }
      return existing;
    }
    return initializeShopStock(initSeed, currentDepth);
  }, [playerData.progression.shopStockState, currentDepth, initSeed]);

  // Persist stock if it was just initialized or migrated
  const [hasInitialized, setHasInitialized] = useState(false);
  if (
    (!playerData.progression.shopStockState ||
      playerData.progression.shopStockState.darkMarketConsumableStock === undefined) &&
    !hasInitialized
  ) {
    setHasInitialized(true);
    updatePlayerData({
      progression: { ...playerData.progression, shopStockState: shopStock },
    });
  }

  // Clear new stock flag when entering DarkMarketTab
  const [clearedNotification, setClearedNotification] = useState(false);
  if (shopStock.darkMarketHasNewStock && !clearedNotification) {
    setClearedNotification(true);
    const cleared = clearDarkMarketNewStockFlag(shopStock);
    updatePlayerData({
      progression: { ...playerData.progression, shopStockState: cleared },
    });
  }

  // Get dark market consumables from stock
  const darkMarketConsumables = useMemo((): ResolvedShopListing[] => {
    const itemKeys = Object.keys(shopStock.darkMarketConsumableStock);
    return itemKeys
      .map((key) => resolveConsumableByKey(key))
      .filter((r): r is ResolvedShopListing => r !== null)
      .map((r) => ({
        ...r,
        price: Math.floor(r.price * DARK_MARKET_PRICE_MULTIPLIER),
      }));
  }, [shopStock.darkMarketConsumableStock]);

  // Generate dark market equipment
  const darkMarketEquipment = useMemo(
    () => generateDarkMarketEquipment(shopStock.darkMarketSeed, currentDepth),
    [shopStock.darkMarketSeed, currentDepth]
  );

  const updateStock = useCallback(
    (newStock: ShopStockState) => {
      updatePlayerData({
        progression: { ...playerData.progression, shopStockState: newStock },
      });
    },
    [updatePlayerData, playerData.progression]
  );

  const showNotification = useCallback((message: string) => {
    setNotification(message);
    setTimeout(() => setNotification(null), 2000);
  }, []);

  const handleBuyConsumable = useCallback(
    (resolved: ResolvedShopListing) => {
      const totalGold = playerData.resources.baseCampGold + playerData.resources.explorationGold;
      const darkMarketPrice = Math.floor(resolved.data.shopPrice! * DARK_MARKET_PRICE_MULTIPLIER);

      if (!canAfford(totalGold, darkMarketPrice)) {
        showNotification("ゴールドが足りません！");
        return;
      }
      if (!hasInventorySpace(storage.currentCapacity, storage.maxCapacity)) {
        showNotification("倉庫がいっぱいです！");
        return;
      }

      const newStock = decrementDarkMarketConsumableStock(shopStock, resolved.listing.itemTypeId);
      if (!newStock) {
        showNotification("売り切れです！");
        return;
      }

      const item = purchaseItem(resolved.listing);
      if (!item) {
        showNotification("購入に失敗しました");
        return;
      }

      if (spendGold(darkMarketPrice)) {
        addItemToStorage(item);
        updateStock(newStock);
        showNotification(`${resolved.data.nameJa} を購入しました！`);
      }
    },
    [playerData.resources, storage, shopStock, spendGold, addItemToStorage, updateStock, showNotification]
  );

  const handleBuyEquipment = useCallback(
    (listing: DarkMarketEquipmentListing, index: number) => {
      const totalGold = playerData.resources.baseCampGold + playerData.resources.explorationGold;
      if (!canAfford(totalGold, listing.price)) {
        showNotification("ゴールドが足りません！");
        return;
      }
      if (!hasInventorySpace(storage.currentCapacity, storage.maxCapacity)) {
        showNotification("倉庫がいっぱいです！");
        return;
      }

      const newStock = markDarkMarketEquipmentSoldOut(shopStock, index);
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
    },
    [playerData.resources, storage, shopStock, spendGold, addItemToStorage, updateStock, showNotification]
  );

  const totalGold = playerData.resources.baseCampGold + playerData.resources.explorationGold;

  const renderConsumableListing = (resolved: ResolvedShopListing) => {
    const stock = getDarkMarketConsumableStock(shopStock, resolved.listing.itemTypeId);
    const soldOut = stock <= 0;
    const darkMarketPrice = Math.floor(resolved.data.shopPrice! * DARK_MARKET_PRICE_MULTIPLIER);
    const affordable = canAfford(totalGold, darkMarketPrice);
    const hasSpace = hasInventorySpace(storage.currentCapacity, storage.maxCapacity);

    return (
      <div
        key={resolved.data.typeId}
        className={`shop-item dark-market-item ${soldOut ? "sold-out" : ""} ${!affordable && !soldOut ? "unaffordable" : ""}`}
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
            <span className={`stock-badge ${stock <= 1 ? "low-stock" : ""}`}>
              残り: {stock}
            </span>
          )}
        </div>
        <div className="item-price dark-market-price">{darkMarketPrice} G</div>
        <button
          className="buy-button dark-market-buy"
          onClick={() => handleBuyConsumable(resolved)}
          disabled={soldOut || !affordable || !hasSpace}
        >
          購入
        </button>
      </div>
    );
  };

  const renderEquipmentListing = (listing: DarkMarketEquipmentListing, idx: number) => {
    const available = isDarkMarketEquipmentAvailable(shopStock, idx);
    const affordable = canAfford(totalGold, listing.price);
    const hasSpace = hasInventorySpace(storage.currentCapacity, storage.maxCapacity);

    return (
      <div
        key={`dark_${listing.slot}_${listing.rarity}_${idx}`}
        className={`shop-item dark-market-item equipment-item rarity-${listing.rarity} ${!available ? "sold-out" : ""} ${!affordable && available ? "unaffordable" : ""}`}
      >
        <div className="item-icon">{listing.icon}</div>
        <div className="item-info">
          <div className="item-name">{listing.name}</div>
          <div className="item-description">
            {listing.slot === "accessory1" || listing.slot === "accessory2"
              ? "アクセサリー"
              : listing.slot === "weapon"
              ? "武器"
              : listing.slot === "armor"
              ? "鎧"
              : listing.slot === "helmet"
              ? "兜"
              : "靴"}{" "}
            - <span className={`rarity-label rarity-${listing.rarity}`}>{listing.rarity}</span>
          </div>
        </div>
        <div className="stock-badge-area">
          {!available ? (
            <span className="stock-badge out-of-stock">売切</span>
          ) : (
            <span className="stock-badge">残り: 1</span>
          )}
        </div>
        <div className="item-price dark-market-price">{listing.price} G</div>
        <button
          className="buy-button dark-market-buy"
          onClick={() => handleBuyEquipment(listing, idx)}
          disabled={!available || !affordable || !hasSpace}
        >
          購入
        </button>
      </div>
    );
  };

  return (
    <div className="dark-market-tab">
      {notification && <div className="shop-notification">{notification}</div>}

      {/* Warning Banner */}
      <div className="dark-market-warning">
        <span className="warning-icon">&#x26A0;</span>
        闇市場の商品は通常より高額です（{Math.round(DARK_MARKET_PRICE_MULTIPLIER * 100 - 100)}%増）
      </div>

      {/* Consumables Section */}
      <section className="shop-section dark-market-section">
        <h2 className="shop-section-title dark-market-title">&#x1F9EA; 希少消耗品</h2>
        <div className="shop-items-grid">
          {darkMarketConsumables.length > 0 ? (
            darkMarketConsumables.map(renderConsumableListing)
          ) : (
            <div className="empty-section">現在の在庫はありません</div>
          )}
        </div>
      </section>

      {/* Equipment Section */}
      <section className="shop-section dark-market-section">
        <h2 className="shop-section-title dark-market-title">&#x2694;&#xFE0F; 希少装備</h2>
        <div className="shop-items-grid">
          {darkMarketEquipment.map(renderEquipmentListing)}
        </div>
      </section>

      {/* Info Note */}
      <div className="dark-market-info">
        <span className="info-icon">&#x1F30D;</span>
        ボス討伐後に在庫が更新されます
      </div>
    </div>
  );
};

export default DarkMarketTab;
