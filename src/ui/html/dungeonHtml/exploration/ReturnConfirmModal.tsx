// ReturnConfirmModal - Confirmation modal for returning from dungeon

interface ReturnConfirmModalProps {
  isOpen: boolean;
  teleportStoneCount: number;
  onConfirm: () => void;
  onCancel: () => void;
}

export function ReturnConfirmModal({
  isOpen,
  teleportStoneCount,
  onConfirm,
  onCancel,
}: ReturnConfirmModalProps) {
  if (!isOpen) return null;

  return (
    <div className="return-confirm-overlay" onClick={onCancel}>
      <div
        className="return-confirm-modal"
        onClick={(e) => e.stopPropagation()}
      >
        <h2 className="return-confirm-title">帰還確認</h2>
        <p className="return-confirm-description">
          テレポートストーンを使用して拠点に帰還しますか？
        </p>
        <div className="return-confirm-stone-info">
          <span className="return-confirm-stone-icon">🔮</span>
          <span className="return-confirm-stone-count">
            テレポートストーン: {teleportStoneCount}個
          </span>
        </div>
        <div className="return-confirm-actions">
          <button
            className="return-confirm-btn return-confirm-use"
            onClick={onConfirm}
            disabled={teleportStoneCount <= 0}
          >
            {teleportStoneCount > 0 ? "使用して帰還" : "ストーンがありません"}
          </button>
          <button
            className="return-confirm-btn return-confirm-cancel"
            onClick={onCancel}
          >
            キャンセル
          </button>
        </div>
      </div>
    </div>
  );
}
