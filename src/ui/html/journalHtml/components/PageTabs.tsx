/**
 * PageTabs Component
 *
 * Vertical tabs displayed on the left edge of the journal book.
 * Shows 4 chapter tabs: 戦術 (Tactics), 記憶 (Memories), 思考 (Thoughts), 設定 (Settings)
 */

import type { JournalPage } from "@/types/journalTypes";

interface PageTabsProps {
  currentPage: JournalPage;
  onPageChange: (page: JournalPage) => void;
}

interface TabConfig {
  page: JournalPage;
  label: string;
  icon: string;
}

const TABS: TabConfig[] = [
  { page: "tactics", label: "戦術", icon: "⚔️" },
  { page: "memories", label: "記憶", icon: "📚" },
  { page: "thoughts", label: "思考", icon: "✍️" },
  { page: "settings", label: "設定", icon: "⚙️" },
];

export function PageTabs({ currentPage, onPageChange }: PageTabsProps) {
  return (
    <nav className="journal-tabs" role="tablist" aria-label="手記のページ">
      {TABS.map((tab) => (
        <button
          key={tab.page}
          className={`journal-tab ${currentPage === tab.page ? "active" : ""}`}
          onClick={() => onPageChange(tab.page)}
          role="tab"
          aria-selected={currentPage === tab.page}
          aria-controls={`journal-page-${tab.page}`}
        >
          <span className="journal-tab-icon" aria-hidden="true">
            {tab.icon}
          </span>
          {tab.label}
        </button>
      ))}
    </nav>
  );
}
