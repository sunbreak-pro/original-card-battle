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
import "@/ui/css/journal/Journal.css";

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
        // Phase 4: ThoughtsPage
        return (
          <div className="journal-page">
            <section className="journal-section">
              <h3 className="journal-section-title">思考のページ</h3>
              <p style={{ color: "var(--journal-text-secondary)" }}>
                Phase 4で実装予定: メモ・ノート機能
              </p>
            </section>
          </div>
        );
      case "settings":
        // Phase 3: SettingsPage
        return (
          <div className="journal-page">
            <section className="journal-section">
              <h3 className="journal-section-title">設定のページ</h3>
              <p style={{ color: "var(--journal-text-secondary)" }}>
                Phase 3で実装予定: 音量、明るさ、セーブ/ロード、実績
              </p>
            </section>
          </div>
        );
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
