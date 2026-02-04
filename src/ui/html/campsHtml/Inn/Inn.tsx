/**
 * Inn - Main inn facility screen
 *
 * Provides rest and dining options to grant bonuses for the next exploration.
 * Also includes a rumor system for flavor text and tips.
 */

import { useState } from "react";
import type { InnTab } from "@/types/campTypes";
import { INN_TABS } from "@/constants/campConstants";
import { usePlayer } from "@/contexts/PlayerContext";
import RestTab from "./RestTab";
import DiningTab from "./DiningTab";
import RumorBubble from "./RumorBubble";
import FacilityHeader from "../../componentsHtml/FacilityHeader";
import BackToCampButton from "../../componentsHtml/BackToCampButton";
import FacilityTabNav from "../../componentsHtml/FacilityTabNav";
import { hasActiveInnBuffs, getInnBuffsSummary } from "@/domain/camps/logic/innLogic";
import "../../../css/camps/Inn.css";

export const Inn = () => {
  const [selectedTab, setSelectedTab] = useState<InnTab>("rest");
  const { playerData } = usePlayer();
  const innBuffs = playerData.progression.innBuffsState;
  const hasBuffs = innBuffs ? hasActiveInnBuffs(innBuffs) : false;
  const buffsSummary = innBuffs ? getInnBuffsSummary(innBuffs) : [];

  return (
    <div className="inn-screen">
      {/* Header */}
      <FacilityHeader title="宿屋" />

      {/* Active Buffs Banner */}
      {hasBuffs && (
        <div className="inn-buffs-banner">
          <span className="buffs-label">次回探索ボーナス:</span>
          <span className="buffs-list">{buffsSummary.join(" / ")}</span>
        </div>
      )}

      {/* Tab Navigation */}
      <FacilityTabNav
        tabs={INN_TABS}
        activeTab={selectedTab}
        onTabChange={setSelectedTab}
        facility="inn"
      />

      {/* Tab Content */}
      <div className="inn-content">
        {selectedTab === "rest" && <RestTab />}
        {selectedTab === "dining" && <DiningTab />}
      </div>

      {/* Rumor Bubble */}
      <RumorBubble />

      {/* Back Button */}
      <BackToCampButton />
    </div>
  );
};

export default Inn;
