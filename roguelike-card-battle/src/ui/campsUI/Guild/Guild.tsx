import { useState } from "react";
import { useGameState } from "../../../domain/camps/contexts/GameStateContext";
import PromotionTab from "./Exam";
import RumorsTab from "./RumorsTab";
import QuestsTab from "./QuestsTab";
import "./Guild.css";

type GuildTab = "promotion" | "rumors" | "quests";

export const Guild = () => {
  const [selectedTab, setSelectedTab] = useState<GuildTab>("promotion");
  const { returnToCamp } = useGameState();

  return (
    <div className="guild-screen">
      {/* Header */}
      <header className="guild-header">
        <h1 className="guild-title">🍺 Guild Tavern</h1>
        <p className="guild-subtitle">
          A place for adventurers to gather, rest, and grow stronger
        </p>
      </header>

      {/* Tab Navigation */}
      <nav className="guild-tabs">
        <button
          className={`guild-tab ${selectedTab === "promotion" ? "active" : ""}`}
          onClick={() => setSelectedTab("promotion")}
        >
          <span className="tab-icon">⚔️</span>
          <span className="tab-label">Exams</span>
        </button>
        <button
          className={`guild-tab ${selectedTab === "rumors" ? "active" : ""}`}
          onClick={() => setSelectedTab("rumors")}
        >
          <span className="tab-icon">📰</span>
          <span className="tab-label">Rumors</span>
        </button>
        <button
          className={`guild-tab ${selectedTab === "quests" ? "active" : ""}`}
          onClick={() => setSelectedTab("quests")}
        >
          <span className="tab-icon">📜</span>
          <span className="tab-label">Quests</span>
        </button>
      </nav>

      {/* Tab Content */}
      <div className="guild-content">
        {selectedTab === "promotion" && <PromotionTab />}
        {selectedTab === "rumors" && <RumorsTab />}
        {selectedTab === "quests" && <QuestsTab />}
      </div>

      {/* Back Button */}
      <button className="guild-back-button" onClick={returnToCamp}>
        ← Back to Camp
      </button>
    </div>
  );
};

export default Guild;
