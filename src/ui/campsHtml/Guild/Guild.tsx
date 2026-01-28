import { useState } from "react";
import { useGameState } from "../../../contexts/GameStateContext";
import PromotionTab from "./Exam";
import RumorsTab from "./RumorsTab";
import QuestsTab from "./QuestsTab";
import FacilityHeader from "../../componentsHtml/FacilityHeader";
import "../../css/camps/Guild.css";

type GuildTab = "promotion" | "rumors" | "quests";

export const Guild = () => {
  const [selectedTab, setSelectedTab] = useState<GuildTab>("promotion");
  const { returnToCamp } = useGameState();

  return (
    <>
      <img
        className="guild-background"
        alt="Guild Background"
        src="../../../../public/assets/images/Guild-background.png"
      ></img>
      <div className="guild-screen">
        {/* Header */}
        <FacilityHeader title="酒場" />

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
    </>
  );
};

export default Guild;
