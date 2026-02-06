/**
 * SettingsPage - Journal settings page
 *
 * Integrates existing settings components (SoundSettings, BrightnessSettings,
 * SaveLoadUI, AchievementList) within the journal's parchment style.
 * Two sub-tabs: settings and achievements.
 */

import { useState } from "react";
import { SoundSettings } from "@/ui/html/componentsHtml/SettingsPanels/SoundSettings";
import { BrightnessSettings } from "@/ui/html/componentsHtml/SettingsPanels/BrightnessSettings";
import { SaveLoadUI } from "@/ui/html/componentsHtml/SettingsPanels/SaveLoadUI";
import { AchievementList } from "@/ui/html/componentsHtml/AchievementList";
import { useSettings } from "@/contexts/SettingsContext";
import { useGameState } from "@/contexts/GameStateContext";
import { useJournal } from "@/contexts/JournalContext";
import "@/ui/css/journal/Settings.css";

type SettingsSubTab = "settings" | "achievements";

export function SettingsPage() {
  const [activeTab, setActiveTab] = useState<SettingsSubTab>("settings");
  const { resetSettings } = useSettings();
  const { navigateTo } = useGameState();
  const { closeJournal } = useJournal();

  const handleResetSettings = () => {
    if (window.confirm("設定を初期値にリセットしますか？")) {
      resetSettings();
    }
  };

  const handleNewGame = () => {
    if (window.confirm("ニューゲームを開始しますか？\nセーブしていない進行状況は失われます。")) {
      closeJournal();
      navigateTo("character_select");
    }
  };

  return (
    <div className="journal-page journal-settings-page">
      {/* Sub-tab navigation */}
      <div className="journal-settings-tabs">
        <button
          className={`journal-settings-tab${activeTab === "settings" ? " active" : ""}`}
          onClick={() => setActiveTab("settings")}
        >
          設定
        </button>
        <button
          className={`journal-settings-tab${activeTab === "achievements" ? " active" : ""}`}
          onClick={() => setActiveTab("achievements")}
        >
          実績
        </button>
      </div>

      {/* Settings tab content */}
      {activeTab === "settings" && (
        <div className="journal-settings-content">
          <SoundSettings />
          <BrightnessSettings />
          <SaveLoadUI onNewGame={handleNewGame} />

          <div className="journal-settings-reset">
            <button
              className="journal-btn journal-btn-danger"
              onClick={handleResetSettings}
            >
              設定をリセット
            </button>
          </div>
        </div>
      )}

      {/* Achievements tab content */}
      {activeTab === "achievements" && (
        <div className="journal-achievements-content">
          <AchievementList />
        </div>
      )}
    </div>
  );
}
