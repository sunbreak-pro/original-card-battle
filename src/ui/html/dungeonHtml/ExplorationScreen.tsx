// ExplorationScreen - Main container for dungeon exploration with 2-panel layout

import { useState, useRef, useEffect } from "react";
import { NodeMap } from "./NodeMap";
import { ExplorationStatusTab } from "./exploration/ExplorationStatusTab";
import { ExplorationInventoryTab } from "./exploration/ExplorationInventoryTab";
import { ExplorationJournalTab } from "./exploration/ExplorationJournalTab";
import { ReturnConfirmModal } from "./exploration/ReturnConfirmModal";
import "./ExplorationScreen.css";

type ExplorationTab = "status" | "items" | "journal";

const TAB_LABELS: Record<ExplorationTab, string> = {
  status: "ステータス",
  items: "アイテム",
  journal: "ジャーナル",
};

export function ExplorationScreen() {
  const [activeTab, setActiveTab] = useState<ExplorationTab>("status");
  const [showReturnModal, setShowReturnModal] = useState(false);

  // Exploration timer
  const startTimeRef = useRef(0);
  const [elapsedSeconds, setElapsedSeconds] = useState(0);

  useEffect(() => {
    startTimeRef.current = Date.now();
    const interval = setInterval(() => {
      setElapsedSeconds(Math.floor((Date.now() - startTimeRef.current) / 1000));
    }, 1000);
    return () => clearInterval(interval);
  }, []);

  const formatTime = (totalSeconds: number): string => {
    const minutes = Math.floor(totalSeconds / 60);
    const seconds = totalSeconds % 60;
    return `${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
  };

  // Placeholder for teleport stone count - will be connected to inventory later
  const teleportStoneCount = 0;

  const handleReturnConfirm = () => {
    // TODO: Implement teleport stone usage logic
    setShowReturnModal(false);
  };

  return (
    <div className="exploration-screen">
      {/* Left Panel */}
      <div className="exploration-left-panel">
        {/* Timer and Return */}
        <div className="exploration-left-header">
          <div className="exploration-timer">
            <span className="exploration-timer-icon">⏱️</span>
            <span className="exploration-timer-value">
              {formatTime(elapsedSeconds)}
            </span>
          </div>
          <button
            className="exploration-return-btn"
            onClick={() => setShowReturnModal(true)}
          >
            帰還
          </button>
        </div>

        {/* Tab Navigation */}
        <div className="exploration-tab-nav">
          {(["status", "items", "journal"] as ExplorationTab[]).map((tab) => (
            <button
              key={tab}
              className={`exploration-tab-btn${activeTab === tab ? " active" : ""}`}
              onClick={() => setActiveTab(tab)}
            >
              {TAB_LABELS[tab]}
            </button>
          ))}
        </div>

        {/* Tab Content */}
        <div className="exploration-tab-content">
          {activeTab === "status" && <ExplorationStatusTab />}
          {activeTab === "items" && <ExplorationInventoryTab />}
          {activeTab === "journal" && <ExplorationJournalTab />}
        </div>
      </div>

      {/* Right Panel - Map */}
      <div className="exploration-right-panel">
        <NodeMap />
      </div>

      {/* Return Confirm Modal */}
      <ReturnConfirmModal
        isOpen={showReturnModal}
        teleportStoneCount={teleportStoneCount}
        onConfirm={handleReturnConfirm}
        onCancel={() => setShowReturnModal(false)}
      />
    </div>
  );
}
