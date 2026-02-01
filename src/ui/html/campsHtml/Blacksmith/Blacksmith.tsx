import { useState } from "react";
import { useGameState } from "@/contexts/GameStateContext";
import type { BlacksmithTab } from "@/types/campTypes";
import UpgradeTab from "./UpgradeTab";
import RepairTab from "./RepairTab";
import DismantleTab from "./DismantleTab";
import FacilityHeader from "../../componentsHtml/FacilityHeader";
import "../../../css/camps/Blacksmith.css";

export const Blacksmith = () => {
  const [selectedTab, setSelectedTab] = useState<BlacksmithTab>("upgrade");
  const { returnToCamp } = useGameState();

  return (
    <>
      <img
        className="blacksmith-background"
        alt="Blacksmith Background"
        src="/assets/images/Blacksmith-background.png"
      ></img>
      <div className="blacksmith-screen">
        {/* Header */}
        <FacilityHeader title="鍛冶屋" />

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
