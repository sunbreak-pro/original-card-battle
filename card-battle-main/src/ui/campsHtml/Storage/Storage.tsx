import React, { useState } from "react";
import { useGameState } from "../../../domain/camps/contexts/GameStateContext";
import { usePlayer } from "../../../domain/camps/contexts/PlayerContext";
import { useInventory } from "../../../domain/camps/contexts/InventoryContext";
import { type Item } from "../../../domain/item_equipment/type/ItemTypes";
import ItemCard from "./ItemCard";
import { ItemDetailPanel, EquipmentDetailPanel } from "./ItemDetailPanel";
import DeleteModal from "../modal/DeleteModal";
import FacilityHeader from "../../commonHtml/FacilityHeader";
import "../../css/camps/Storage.css";

type TabType = "items" | "equipment";
type ItemSource = "storage" | "inventory" | "equipment" | "equipmentInventory";

/**
 * Storage Component
 * Manages item storage, inventory, and equipment
 */
export const Storage: React.FC = () => {
  const { returnToCamp } = useGameState();
  const { playerData } = usePlayer();
  const {
    moveItem,
    equipItem,
    unequipItem,
    removeItemFromInventory,
    removeItemFromStorage,
    removeItemFromEquipmentInventory,
  } = useInventory();

  // Component state
  const [activeTab, setActiveTab] = useState<TabType>("items");
  const [selectedItem, setSelectedItem] = useState<Item | null>(null);
  const [selectedSource, setSelectedSource] = useState<ItemSource>("storage");
  const [message, setMessage] = useState<string>("");
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState<boolean>(false);

  // Handle item selection
  const handleSelectItem = (item: Item, source: ItemSource) => {
    setSelectedItem(item);
    setSelectedSource(source);
  };

  // Show temporary message
  const showMessage = (msg: string, duration: number = 2000) => {
    setMessage(msg);
    setTimeout(() => setMessage(""), duration);
  };

  // Handle move to storage
  const handleMoveToStorage = () => {
    if (!selectedItem) return;
    // if()return;
    const result = moveItem(selectedItem.id, "inventory_to_storage");
    showMessage(result.message);
    if (result.success) setSelectedItem(null);
  };

  // Handle move to inventory
  const handleMoveToInventory = () => {
    if (!selectedItem) return;
    const result = moveItem(selectedItem.id, "storage_to_inventory");
    showMessage(result.message);
    if (result.success) setSelectedItem(null);
  };

  // Handle equip
  const handleEquip = () => {
    if (!selectedItem || !selectedItem.equipmentSlot) return;
    const result = equipItem(selectedItem.id, selectedItem.equipmentSlot);
    showMessage(result.message);
    if (result.success) setSelectedItem(null);
  };

  // Handle unequip
  const handleUnequip = () => {
    if (!selectedItem || !selectedItem.equipmentSlot) return;
    const result = unequipItem(selectedItem.equipmentSlot);
    showMessage(result.message);
    if (result.success) setSelectedItem(null);
  };

  // Handle delete button click - opens modal
  const handleDeleteClick = () => {
    if (!selectedItem) return;
    setIsDeleteModalOpen(true);
  };

  // Handle delete confirmation
  const handleDeleteConfirm = () => {
    if (!selectedItem) return;

    let success = false;
    if (selectedSource === "storage") {
      success = removeItemFromStorage(selectedItem.id);
    } else if (selectedSource === "inventory") {
      success = removeItemFromInventory(selectedItem.id);
    } else if (selectedSource === "equipmentInventory") {
      success = removeItemFromEquipmentInventory(selectedItem.id);
    }

    if (success) {
      showMessage(`Deleted ${selectedItem.name}`);
      setSelectedItem(null);
    } else {
      showMessage("Failed to delete item");
    }

    setIsDeleteModalOpen(false);
  };

  // Handle move to equipment inventory
  const handleMoveToEquipmentInventory = () => {
    if (!selectedItem) return;
    const result = moveItem(selectedItem.id, "storage_to_equipment_inventory");
    showMessage(result.message);
    if (result.success) setSelectedItem(null);
  };

  // Handle move from equipment inventory to storage
  const handleMoveToStorageFromEquipmentInventory = () => {
    if (!selectedItem) return;
    const result = moveItem(selectedItem.id, "equipment_inventory_to_storage");
    showMessage(result.message);
    if (result.success) setSelectedItem(null);
  };

  // Handle delete cancellation
  const handleDeleteCancel = () => {
    setIsDeleteModalOpen(false);
  };

  // Render compact item grid (4x6 = 24 slots)
  const renderCompactGrid = (
    items: Item[],
    source: ItemSource,
    maxSlots: number,
  ) => {
    const cells = [];
    for (let i = 0; i < maxSlots; i++) {
      if (i < items.length) {
        const item = items[i];
        cells.push(
          <ItemCard
            key={item.id}
            item={item}
            isSelected={selectedItem?.id === item.id}
            onClick={() => handleSelectItem(item, source)}
            compact
          />,
        );
      } else {
        cells.push(
          <div
            key={`empty-${source}-${i}`}
            className="item-card-compact item-card-empty"
          >
            <span className="empty-placeholder">empty</span>
          </div>,
        );
      }
    }
    return cells;
  };

  // Render equipment slots for Equipment tab (compact version)
  const renderEquipmentSlotsCompact = () => {
    const slots = [
      {
        key: "weapon",
        label: "Weapon",
        item: playerData.inventory.equipmentSlots.weapon,
      },
      {
        key: "armor",
        label: "Armor",
        item: playerData.inventory.equipmentSlots.armor,
      },
      {
        key: "helmet",
        label: "Helmet",
        item: playerData.inventory.equipmentSlots.helmet,
      },
      {
        key: "boots",
        label: "Boots",
        item: playerData.inventory.equipmentSlots.boots,
      },
      {
        key: "accessory1",
        label: "Acc 1",
        item: playerData.inventory.equipmentSlots.accessory1,
      },
      {
        key: "accessory2",
        label: "Acc 2",
        item: playerData.inventory.equipmentSlots.accessory2,
      },
    ];

    return slots.map((slot) => (
      <div
        key={slot.key}
        className={`equip-slot-compact ${
          selectedItem?.id === slot.item?.id ? "equip-slot-selected" : ""
        } ${slot.item ? "equip-slot-filled" : ""}`}
        onClick={() => slot.item && handleSelectItem(slot.item, "equipment")}
      >
        <div className="equip-slot-label-compact">{slot.label}</div>
        {slot.item ? (
          <>
            <div className="equip-slot-icon-compact">{slot.item.type}</div>
            <div className="equip-slot-name-compact">{slot.item.name}</div>
          </>
        ) : (
          <div className="equip-slot-empty-compact">-</div>
        )}
      </div>
    ));
  };

  // Render equipment inventory grid (3 slots max)
  const renderEquipmentInventoryGrid = () => {
    const maxSlots = playerData.inventory.equipmentInventory.maxCapacity;
    const cells = [];

    for (let i = 0; i < maxSlots; i++) {
      if (i < playerData.inventory.equipmentInventory.items.length) {
        const item = playerData.inventory.equipmentInventory.items[i];
        cells.push(
          <ItemCard
            key={item.id}
            item={item}
            isSelected={selectedItem?.id === item.id}
            onClick={() => handleSelectItem(item, "equipmentInventory")}
            compact
          />,
        );
      } else {
        cells.push(
          <div
            key={`empty-equip-inv-${i}`}
            className="item-card-compact item-card-empty"
          >
            <span className="empty-placeholder">empty</span>
          </div>,
        );
      }
    }
    return cells;
  };

  // Get equipment items from storage
  const storageEquipmentItems = playerData.inventory.storage.items.filter(
    (item) => item.itemType === "equipment",
  );

  // Get non-equipment items from inventory (for Items tab)
  const inventoryNonEquipmentItems =
    playerData.inventory.inventory.items.filter(
      (item) => item.itemType !== "equipment",
    );

  return (
    <div className="storage-container">
      {/* Header */}
      <FacilityHeader title="倉庫" />

      {/* Tab Navigation */}
      <nav className="nav">
        <div className="storage-tabs">
          <button
            className={`storage-tab ${
              activeTab === "items" ? "storage-tab-active" : ""
            }`}
            onClick={() => {
              setActiveTab("items");
              setSelectedItem(null);
            }}
          >
            Items
          </button>
          <button
            className={`storage-tab ${
              activeTab === "equipment" ? "storage-tab-active" : ""
            }`}
            onClick={() => {
              setActiveTab("equipment");
              setSelectedItem(null);
            }}
          >
            Equipment
          </button>
        </div>
        <div className="back-content">
          <button className="storage-back-button" onClick={returnToCamp}>
            🏠 baseCamp Back
          </button>
        </div>
      </nav>

      {/* Main Content */}
      <div className="storage-content">
        {activeTab === "items" ? (
          /* Items Tab: Storage + Inventory side by side */
          <div className="items-tab-content">
            {/* Left: Dual Grid Panel */}
            <div className="dual-grid-panel">
              {/* Storage Grid */}
              <div className="storage-grid-section">
                <div className="storage-grid-header">
                  <span className="storage-grid-title">Storage</span>
                  <span className="storage-grid-count">
                    {playerData.inventory.storage.currentCapacity}/
                    {playerData.inventory.storage.maxCapacity}
                  </span>
                </div>
                <div className="compact-grid storage-grid">
                  {renderCompactGrid(
                    playerData.inventory.storage.items,
                    "storage",
                    playerData.inventory.storage.maxCapacity,
                  )}
                </div>
              </div>

              {/* Transfer Buttons */}
              <div className="transfer-buttons">
                <button
                  className="transfer-btn"
                  onClick={handleMoveToInventory}
                  disabled={!selectedItem || selectedSource !== "storage"}
                  title="Move to Inventory"
                >
                  →
                </button>
                <button
                  className="transfer-btn"
                  onClick={handleMoveToStorage}
                  disabled={!selectedItem || selectedSource !== "inventory"}
                  title="Move to Storage"
                >
                  ←
                </button>
              </div>

              {/* Inventory Grid (non-equipment items only) */}
              <div className="storage-grid-section">
                <div className="storage-grid-header">
                  <span className="storage-grid-title">Inventory</span>
                  <span className="storage-grid-count">
                    {inventoryNonEquipmentItems.length}/
                    {playerData.inventory.inventory.maxCapacity}
                  </span>
                </div>
                <div className="compact-grid inventory-grid">
                  {renderCompactGrid(
                    inventoryNonEquipmentItems,
                    "inventory",
                    playerData.inventory.inventory.maxCapacity,
                  )}
                </div>
              </div>
            </div>

            {/* Right: Item Detail Panel */}
            <div className="storage-detail-panel">
              <ItemDetailPanel
                item={selectedItem}
                source={selectedSource}
                onMoveToStorage={handleMoveToStorage}
                onMoveToInventory={handleMoveToInventory}
                onMoveToEquipmentInventory={handleMoveToEquipmentInventory}
                onEquip={handleEquip}
                onUnequip={handleUnequip}
                onDelete={handleDeleteClick}
              />
            </div>
          </div>
        ) : (
          /* Equipment Tab: Equipment List + Slots + Equipment Inventory */
          <div className="equipment-tab-content">
            {/* Left: Equipment List from Storage */}
            <div className="equipment-list-panel">
              <div className="storage-grid-header">
                <span className="storage-grid-title">Equipment List</span>
                <span className="storage-grid-count">
                  {storageEquipmentItems.length} items
                </span>
              </div>
              <div className="compact-grid equipment-list-grid">
                {storageEquipmentItems.length > 0 ? (
                  storageEquipmentItems.map((item) => (
                    <ItemCard
                      key={item.id}
                      item={item}
                      isSelected={selectedItem?.id === item.id}
                      onClick={() => handleSelectItem(item, "storage")}
                      compact
                    />
                  ))
                ) : (
                  <div className="empty-list-message">
                    No equipment in storage
                  </div>
                )}
              </div>
            </div>
            <div className="transfer-buttons">
              <button
                className="transfer-btn"
                onClick={handleMoveToEquipmentInventory}
                disabled={!selectedItem || selectedSource !== "storage"}
                title="Move to Inventory"
              >
                →
              </button>
              <button
                className="transfer-btn"
                onClick={handleMoveToStorageFromEquipmentInventory}
                disabled={
                  !selectedItem || selectedSource !== "equipmentInventory"
                }
                title="Move to Storage"
              >
                ←
              </button>
            </div>

            {/* Right: Equipment Slots + Equipment Inventory */}
            <div className="equipment-right-panel">
              {/* Equipment Slots */}
              <div className="equipment-slots-section">
                <div className="storage-section-header">
                  <span className="storage-section-title">Equipped Item</span>
                </div>
                <div className="equip-slots-grid-compact">
                  {renderEquipmentSlotsCompact()}
                </div>
              </div>
              <div className="transfer-buttons-equip">
                <button
                  className="transfer-btn"
                  onClick={() =>
                    moveItem(
                      selectedItem!.id,
                      "equipment_inventory_to_equipment",
                    )
                  }
                  disabled={
                    !selectedItem || selectedSource !== "equipmentInventory"
                  }
                  title="Move to Equipment Slots"
                >
                  ↑
                </button>
                <button
                  className="transfer-btn"
                  onClick={() =>
                    moveItem(
                      selectedItem!.id,
                      "equipment_to_equipment_inventory",
                    )
                  }
                  disabled={!selectedItem || selectedSource !== "equipment"}
                  title="Move to Equipment Inventory"
                >
                  ↓
                </button>
              </div>

              {/* Equipment Inventory */}
              <div className="equipment-inventory-section">
                <div className="storage-section-header">
                  <span className="storage-section-title">
                    Equipment's Inventory
                  </span>
                  <span className="storage-section-count">
                    {playerData.inventory.equipmentInventory.currentCapacity}/
                    {playerData.inventory.equipmentInventory.maxCapacity}
                  </span>
                </div>
                <div className="equipment-inventory-grid">
                  {renderEquipmentInventoryGrid()}
                </div>
              </div>
            </div>
            <div className="equip-detail-panel">
              <EquipmentDetailPanel
                item={selectedItem}
                source={selectedSource}
                onMoveToStorage={handleMoveToStorage}
                onMoveToEquipmentInventory={handleMoveToEquipmentInventory}
                onEquip={handleEquip}
                onUnequipToEquipmentInventory={handleUnequip}
                onDelete={handleDeleteClick}
              />
            </div>
          </div>
        )}
      </div>

      {/* Delete Confirmation Modal */}
      {selectedItem && (
        <DeleteModal
          item={selectedItem}
          isOpen={isDeleteModalOpen}
          onConfirm={handleDeleteConfirm}
          onCancel={handleDeleteCancel}
        />
      )}

      {/* Message Display */}
      {message && <div className="storage-message">{message}</div>}

      {/* Footer */}
      <div className="storage-footer"> </div>
    </div>
  );
};

export default Storage;
