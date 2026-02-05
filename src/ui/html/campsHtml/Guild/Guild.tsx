import { useState } from "react";
import type { GuildTab } from "@/types/campTypes";
import { GUILD_TABS } from "@/constants/campConstants";
import PromotionTab from "./Exam";
import RumorsTab from "./RumorsTab";
import QuestsTab from "./QuestsTab";
import FacilityHeader from "../../componentsHtml/FacilityHeader";
import BackToCampButton from "../../componentsHtml/BackToCampButton";
import FacilityTabNav from "../../componentsHtml/FacilityTabNav";
import "../../../css/camps/Guild.css";

export const Guild = () => {
  const [selectedTab, setSelectedTab] = useState<GuildTab>("promotion");

  return (
    <>
      <img
        className="guild-background"
        alt="Guild Background"
        src="/assets/images/facility-backgrounds/Guild-background.png"
      ></img>
      <div className="guild-screen">
        {/* Header */}
        <FacilityHeader title="酒場" />

        {/* Tab Navigation */}
        <FacilityTabNav
          tabs={GUILD_TABS}
          activeTab={selectedTab}
          onTabChange={setSelectedTab}
          facility="guild"
        />

        {/* Tab Content */}
        <div className="guild-content">
          {selectedTab === "promotion" && <PromotionTab />}
          {selectedTab === "rumors" && <RumorsTab />}
          {selectedTab === "quests" && <QuestsTab />}
        </div>
        {/* Back Button */}
        <BackToCampButton />
      </div>
    </>
  );
};

export default Guild;
