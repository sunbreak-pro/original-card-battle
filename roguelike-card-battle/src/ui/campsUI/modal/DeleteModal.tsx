// DeleteModal Component - Confirmation modal for item deletion

import React from "react";
import type { Item } from "../../../domain/camps/types/ItemTypes";
import "./DeleteModal.css";

interface DeleteModalProps {
  item: Item;
  isOpen: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

/**
 * DeleteModal Component
 * Displays a confirmation dialog before deleting an item
 */
export const DeleteModal: React.FC<DeleteModalProps> = ({
  item,
  isOpen,
  onConfirm,
  onCancel,
}) => {
  if (!isOpen) return null;

  // Get rarity color for display
  const getRarityColor = () => {
    switch (item.rarity) {
      case "common":
        return "#9e9e9e";
      case "uncommon":
        return "#4caf50";
      case "rare":
        return "#2196f3";
      case "epic":
        return "#9c27b0";
      case "legendary":
        return "#ffc107";
      default:
        return "#ffffff";
    }
  };

  // Handle backdrop click to close modal
  const handleBackdropClick = (e: React.MouseEvent<HTMLDivElement>) => {
    if (e.target === e.currentTarget) {
      onCancel();
    }
  };

  return (
    <div className="delete-modal-backdrop" onClick={handleBackdropClick}>
      <div className="delete-modal">
        <div className="delete-modal-header">
          <h2 className="delete-modal-title">Delete Item</h2>
        </div>

        <div className="delete-modal-content">
          <p className="delete-modal-message">
            Are you sure you want to delete this item?
          </p>

          <div className="delete-modal-item-info">
            <span className="delete-modal-item-icon">{item.type}</span>
            <span
              className="delete-modal-item-name"
              style={{ color: getRarityColor() }}
            >
              {item.name}
            </span>
          </div>

          <p className="delete-modal-warning">
            This action cannot be undone.
          </p>
        </div>

        <div className="delete-modal-actions">
          <button
            className="delete-modal-button delete-modal-cancel"
            onClick={onCancel}
          >
            Cancel
          </button>
          <button
            className="delete-modal-button delete-modal-confirm"
            onClick={onConfirm}
          >
            Delete
          </button>
        </div>
      </div>
    </div>
  );
};

export default DeleteModal;
