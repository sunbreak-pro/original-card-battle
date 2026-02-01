/**
 * Library Component
 *
 * Main library facility screen with tabs for:
 * - Card Encyclopedia
 * - Enemy Bestiary
 * - Game Tips
 */

import React, { useState } from "react";
import type { LibraryTab } from "@/types/campTypes";
import { LIBRARY_TABS } from "@/constants/campConstants";
import { CardEncyclopediaTab } from "./CardEncyclopediaTab";
import { EnemyEncyclopediaTab } from "./EnemyEncyclopediaTab";
import { GameTipsTab } from "./GameTipsTab";
import FacilityHeader from "../../componentsHtml/FacilityHeader";
import BackToCampButton from "../../componentsHtml/BackToCampButton";
import FacilityTabNav from "../../componentsHtml/FacilityTabNav";
import "../../../css/camps/Library.css";

export const Library: React.FC = () => {
  const [activeTab, setActiveTab] = useState<LibraryTab>("cards");

  const renderTabContent = () => {
    switch (activeTab) {
      case "cards":
        return <CardEncyclopediaTab />;
      case "enemies":
        return <EnemyEncyclopediaTab />;
      case "tips":
        return <GameTipsTab />;
      default:
        return null;
    }
  };

  return (
    <div className="library-screen">
      {/* Header */}
      <FacilityHeader title="図書館" />

      {/* Tab Navigation */}
      <FacilityTabNav
        tabs={LIBRARY_TABS}
        activeTab={activeTab}
        onTabChange={setActiveTab}
        facility="library"
      />

      {/* Tab Content */}
      {renderTabContent()}

      {/* Back Button */}
      <BackToCampButton />
    </div>
  );
};

export default Library;
