import { useState } from "react";
import { usePlayer } from "@/contexts/PlayerContext";
import { useInventory } from "@/contexts/InventoryContext";
import type { Item } from "@/types/itemTypes";
import { calculateSellPrice } from "@/domain/camps/logic/shopLogic";

const SellTab = () => {
  const { playerData, addGold } = usePlayer();
  const { removeItemFromInventory, getEquippedItem } = useInventory();
  const inventory = playerData.inventory.inventory;
  const [selectedItem, setSelectedItem] = useState<Item | null>(null);
  const [notification, setNotification] = useState<string | null>(null);

  const showNotification = (message: string) => {
    setNotification(message);
    setTimeout(() => setNotification(null), 2000);
  };

  // Filter out items that cannot be sold and equipped items
  const sellableItems = inventory.items.filter((item) => {
    if (!item.canSell) return false;

    // Check if item is equipped
    if (item.itemType === "equipment" && item.equipmentSlot) {
      const equippedItem = getEquippedItem(item.equipmentSlot);
      if (equippedItem && equippedItem.id === item.id) return false;
    }

    return true;
  });

  const handleSell = (item: Item) => {
    const price = calculateSellPrice(item);

    if (removeItemFromInventory(item.id)) {
      addGold(price, true); // Add to baseCampGold
      showNotification(`Sold ${item.name} for ${price} G!`);
      setSelectedItem(null);
    } else {
      showNotification("Failed to sell item!");
    }
  };

  const getRarityClass = (rarity: string) => {
    return `rarity-${rarity}`;
  };

  return (
    <div className="sell-tab">
      {notification && <div className="shop-notification">{notification}</div>}

      <div className="sell-layout">
        {/* Item Grid */}
        <div className="sell-items-section">
          <h2 className="section-title">
            Inventory ({sellableItems.length} sellable items)
          </h2>

          {sellableItems.length === 0 ? (
            <div className="empty-inventory">
              <p>No items to sell</p>
            </div>
          ) : (
            <div className="sell-items-grid">
              {sellableItems.map((item) => (
                <div
                  key={item.id}
                  className={`sell-item ${getRarityClass(item.rarity)} ${
                    selectedItem?.id === item.id ? "selected" : ""
                  }`}
                  onClick={() => setSelectedItem(item)}
                >
                  <div className="item-icon">{item.type}</div>
                  <div className="item-name">{item.name}</div>
                  <div className="item-sell-price">
                    {calculateSellPrice(item)} G
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Detail Panel */}
        <div className="sell-detail-panel">
          {selectedItem ? (
            <div className="item-detail">
              <div className="detail-header">
                <span className="detail-icon">{selectedItem.type}</span>
                <h3
                  className={`detail-name ${getRarityClass(
                    selectedItem.rarity,
                  )}`}
                >
                  {selectedItem.name}
                </h3>
              </div>
              <p className="detail-description">{selectedItem.description}</p>
              <div className="detail-info">
                <div className="info-row">
                  <span>Type:</span>
                  <span>{selectedItem.itemType}</span>
                </div>
                <div className="info-row">
                  <span>Rarity:</span>
                  <span className={getRarityClass(selectedItem.rarity)}>
                    {selectedItem.rarity}
                  </span>
                </div>
                {selectedItem.equipmentSlot && (
                  <div className="info-row">
                    <span>Slot:</span>
                    <span>{selectedItem.equipmentSlot}</span>
                  </div>
                )}
              </div>
              <div className="sell-action">
                <div className="sell-price">
                  Sell Price:{" "}
                  <strong>{calculateSellPrice(selectedItem)} G</strong>
                </div>
                <button
                  className="sell-button"
                  onClick={() => handleSell(selectedItem)}
                >
                  Sell Item
                </button>
              </div>
            </div>
          ) : (
            <div className="no-selection">
              <p>Select an item to sell</p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default SellTab;
