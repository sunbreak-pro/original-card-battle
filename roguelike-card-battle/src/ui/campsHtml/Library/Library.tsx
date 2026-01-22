/**
 * Library Component
 *
 * Main library facility screen with tabs for:
 * - Card Encyclopedia
 * - Enemy Bestiary
 * - Game Tips
 */

import React, { useState } from "react";
import type { LibraryTab } from "../../../domain/camps/types/LibraryTypes";
import { useGameState } from "../../../domain/camps/contexts/GameStateContext";
import { CardEncyclopediaTab } from "./CardEncyclopediaTab";
import { EnemyEncyclopediaTab } from "./EnemyEncyclopediaTab";
import { GameTipsTab } from "./GameTipsTab";
import "../../css/camps/Library.css";

export const Library: React.FC = () => {
  const [activeTab, setActiveTab] = useState<LibraryTab>("cards");
  const { returnToCamp } = useGameState();

  const tabs: { id: LibraryTab; label: string }[] = [
    { id: "cards", label: "Card Encyclopedia" },
    { id: "enemies", label: "Enemy Bestiary" },
    { id: "tips", label: "Game Tips" },
  ];

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
      <header className="library-header">
        <h1 className="library-title">Library</h1>
        <p className="library-subtitle">Knowledge Archive of the Abyss</p>
      </header>

      {/* Tab Navigation */}
      <nav className="library-tabs">
        {tabs.map((tab) => (
          <button
            key={tab.id}
            className={`library-tab ${activeTab === tab.id ? "active" : ""}`}
            onClick={() => setActiveTab(tab.id)}
          >
            {tab.label}
          </button>
        ))}
      </nav>

      {/* Tab Content */}
      {renderTabContent()}

      {/* Back Button */}
      <button className="library-back-button" onClick={returnToCamp}>
        Return to Camp
      </button>
    </div>
  );
};

export default Library;
