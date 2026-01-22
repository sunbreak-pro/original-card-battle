import { useState } from "react";
import { useGameState } from "../../../domain/camps/contexts/GameStateContext";
import { usePlayer } from "../../../domain/camps/contexts/PlayerContext";
import { calculateMagicStoneValue } from "../../../domain/item_equipment/type/ItemTypes";
import type { BlacksmithTab } from "../../../domain/camps/types/BlacksmithTypes";
import UpgradeTab from "./UpgradeTab";
import RepairTab from "./RepairTab";
import DismantleTab from "./DismantleTab";
import "../../css/camps/Blacksmith.css";

export const Blacksmith = () => {
  const [selectedTab, setSelectedTab] = useState<BlacksmithTab>("upgrade");
  const { returnToCamp } = useGameState();
  const { player } = usePlayer();

  const totalMagicStoneValue = calculateMagicStoneValue(
    player.baseCampMagicStones,
  );

  return (
    <>
      <img
        className="blacksmith-background"
        alt="Blacksmith Background"
        src="/assets/images/Blacksmith-background.png"
      ></img>
      <div className="blacksmith-screen">
        {/* Header */}
        <header className="blacksmith-header">
          <div className="blacksmith-title-row">
            <h1 className="blacksmith-title">Blacksmith's Forge</h1>
          </div>
          <div className="blacksmith-resources">
            <div className="resource-display gold">
              <span className="resource-icon">💰</span>
              <span className="resource-value">{player.baseCampGold} G</span>
            </div>
            <div className="resource-display stones">
              <span className="resource-icon">💎</span>
              <span className="resource-value">
                {totalMagicStoneValue} G worth
              </span>
            </div>
          </div>
        </header>

        {/* Tab Navigation */}
        <nav className="blacksmith-tabs">
          <button
            className={`blacksmith-tab ${selectedTab === "upgrade" ? "active" : ""}`}
            onClick={() => setSelectedTab("upgrade")}
          >
            <span className="tab-icon">⚒️</span>
            <span className="tab-label">Upgrade</span>
          </button>
          <button
            className={`blacksmith-tab ${selectedTab === "repair" ? "active" : ""}`}
            onClick={() => setSelectedTab("repair")}
          >
            <span className="tab-icon">🔧</span>
            <span className="tab-label">Repair</span>
          </button>
          <button
            className={`blacksmith-tab ${selectedTab === "dismantle" ? "active" : ""}`}
            onClick={() => setSelectedTab("dismantle")}
          >
            <span className="tab-icon">💥</span>
            <span className="tab-label">Dismantle</span>
          </button>
        </nav>

        {/* Tab Content */}
        <div className="blacksmith-content">
          {selectedTab === "upgrade" && <UpgradeTab />}
          {selectedTab === "repair" && <RepairTab />}
          {selectedTab === "dismantle" && <DismantleTab />}
        </div>

        {/* Back Button */}
        <button className="blacksmith-back-button" onClick={returnToCamp}>
          ← Back to Camp
        </button>
      </div>
    </>
  );
};

export default Blacksmith;
