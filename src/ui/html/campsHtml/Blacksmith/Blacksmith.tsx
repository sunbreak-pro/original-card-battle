import { useState } from "react";
import type { BlacksmithTab } from "@/types/campTypes";
import { BLACKSMITH_TABS } from "@/constants/campConstants";
import UpgradeTab from "./UpgradeTab";
import RepairTab from "./RepairTab";
import DismantleTab from "./DismantleTab";
import FacilityHeader from "../../componentsHtml/FacilityHeader";
import BackToCampButton from "../../componentsHtml/BackToCampButton";
import FacilityTabNav from "../../componentsHtml/FacilityTabNav";
import "../../../css/camps/Blacksmith.css";

export const Blacksmith = () => {
  const [selectedTab, setSelectedTab] = useState<BlacksmithTab>("upgrade");

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
        <FacilityTabNav
          tabs={BLACKSMITH_TABS}
          activeTab={selectedTab}
          onTabChange={setSelectedTab}
          facility="blacksmith"
        />

        {/* Tab Content */}
        <div className="blacksmith-content">
          {selectedTab === "upgrade" && <UpgradeTab />}
          {selectedTab === "repair" && <RepairTab />}
          {selectedTab === "dismantle" && <DismantleTab />}
        </div>

        {/* Back Button */}
        <BackToCampButton />
      </div>
    </>
  );
};

export default Blacksmith;
