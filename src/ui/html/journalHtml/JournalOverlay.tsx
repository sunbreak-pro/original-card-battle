/**
 * JournalOverlay Component
 *
 * Full-screen overlay displaying the journal (手記) with book-style UI.
 * Contains page tabs and renders the current page content.
 */

import { useJournal } from "@/contexts/JournalContext";
import { PageTabs } from "./components/PageTabs";
import { TacticsPage } from "./pages/TacticsPage";
import { MemoriesPage } from "./pages/MemoriesPage";
import { ThoughtsPage } from "./pages/ThoughtsPage";
import { SettingsPage } from "./pages/SettingsPage";
import "@/ui/css/journal/Journal.css";
import "@/ui/css/journal/JournalAnimations.css";

// Page title mapping
const PAGE_TITLES: Record<string, string> = {
  tactics: "戦術",
  memories: "記憶",
  thoughts: "思考",
  settings: "設定",
};

export function JournalOverlay() {
  const { isOpen, currentPage, closeJournal, setPage } = useJournal();

  // Don't render if not open
  if (!isOpen) {
    return null;
  }

  // Handle backdrop click (close on background click)
  const handleBackdropClick = (e: React.MouseEvent) => {
    if (e.target === e.currentTarget) {
      closeJournal();
    }
  };

  // Render current page content
  const renderPageContent = () => {
    switch (currentPage) {
      case "tactics":
        return <TacticsPage />;
      case "memories":
        return <MemoriesPage />;
      case "thoughts":
        return <ThoughtsPage />;
      case "settings":
        return <SettingsPage />;
      default:
        return null;
    }
  };

  return (
    <div
      className="journal-overlay journal-backdrop"
      onClick={handleBackdropClick}
      role="dialog"
      aria-modal="true"
      aria-labelledby="journal-title"
    >
      <div className="journal-book">
        {/* Page tabs on left edge */}
        <PageTabs currentPage={currentPage} onPageChange={setPage} />

        {/* Main content area */}
        <div className="journal-content">
          {/* Header */}
          <header className="journal-header">
            <h2 id="journal-title" className="journal-title">
              {PAGE_TITLES[currentPage] || "手記"}
            </h2>
            <button
              className="journal-close"
              onClick={closeJournal}
              aria-label="手記を閉じる"
            >
              ✕
            </button>
          </header>

          {/* Page content */}
          {renderPageContent()}
        </div>
      </div>
    </div>
  );
}
