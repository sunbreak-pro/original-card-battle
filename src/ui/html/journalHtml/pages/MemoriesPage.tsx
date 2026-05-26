/**
 * MemoriesPage Component
 *
 * Encyclopedia page within the Journal overlay.
 * Displays cards, enemies, equipment, and events with discovery tracking.
 */

import { useState, useMemo } from "react";
import { useJournal } from "@/contexts/JournalContext";
import { usePlayer } from "@/contexts/PlayerContext";
import type { MemoriesCategory } from "@/types/journalTypes";
import "@/ui/css/journal/Memories.css";

// Encyclopedia data imports
import { getCardsByClass } from "@/constants/data/journal/CardEncyclopediaData";
import { classifyCards } from "@/domain/cards/logic/cardClassification";
import {
  getAllEnemies,
  getEnemyStats,
  isBossEnemy,
} from "@/constants/data/journal/EnemyEncyclopediaData";
import {
  getAllEquipment,
  getEquipmentStats,
  SLOT_NAMES,
  RARITY_NAMES,
  CLASS_NAMES,
} from "@/constants/data/journal/EquipmentEncyclopediaData";
import {
  getAllEvents,
  getEventStats,
  EVENT_CATEGORY_NAMES,
} from "@/constants/data/journal/EventEncyclopediaData";
import {
  getAllTips,
  CATEGORY_NAMES as TIP_CATEGORY_NAMES,
} from "@/constants/data/journal/GameTipsData";

// Category configuration
const CATEGORY_CONFIG: Record<
  MemoriesCategory,
  { label: string; icon: string }
> = {
  cards: { label: "カード", icon: "🃏" },
  enemies: { label: "敵", icon: "👹" },
  equipment: { label: "装備", icon: "⚔️" },
  events: { label: "イベント", icon: "📜" },
};

export function MemoriesPage() {
  const { memoriesCategory, setMemoriesCategory, discovery } = useJournal();
  const [searchText, setSearchText] = useState("");

  // Render category content based on current selection
  const renderCategoryContent = () => {
    switch (memoriesCategory) {
      case "cards":
        return <CardsSection discovery={discovery} searchText={searchText} />;
      case "enemies":
        return <EnemiesSection discovery={discovery} searchText={searchText} />;
      case "equipment":
        return <EquipmentSection discovery={discovery} searchText={searchText} />;
      case "events":
        return <EventsSection discovery={discovery} searchText={searchText} />;
      default:
        return null;
    }
  };

  return (
    <div className="journal-page memories-page">
      {/* Category tabs */}
      <div className="memories-category-tabs">
        {(Object.keys(CATEGORY_CONFIG) as MemoriesCategory[]).map((cat) => (
          <button
            key={cat}
            className={`memories-category-tab ${memoriesCategory === cat ? "active" : ""}`}
            onClick={() => setMemoriesCategory(cat)}
          >
            <span className="memories-tab-icon">{CATEGORY_CONFIG[cat].icon}</span>
            <span className="memories-tab-label">{CATEGORY_CONFIG[cat].label}</span>
          </button>
        ))}
      </div>

      {/* Search bar */}
      <div className="memories-search">
        <input
          type="text"
          className="memories-search-input"
          placeholder="検索..."
          value={searchText}
          onChange={(e) => setSearchText(e.target.value)}
        />
        {searchText && (
          <button
            className="memories-search-clear"
            onClick={() => setSearchText("")}
            aria-label="検索をクリア"
          >
            ✕
          </button>
        )}
      </div>

      {/* Content */}
      <div className="memories-content">{renderCategoryContent()}</div>
    </div>
  );
}

// ============================================================================
// Cards Section
// ============================================================================

interface SectionProps {
  discovery: { cards: string[]; enemies: string[]; equipment: string[]; events: string[] };
  searchText: string;
}

const CLASSIFICATION_LABELS = {
  baseCards: "基礎カード",
  derivedCards: "派生カード",
  talentCards: "才能カード",
} as const;

function CardsSection({ discovery, searchText }: SectionProps) {
  const { playerData } = usePlayer();
  const playerClass = playerData.persistent.playerClass;

  const allCards = useMemo(() => getCardsByClass(playerClass), [playerClass]);
  const discoveredSet = useMemo(() => new Set(discovery.cards), [discovery.cards]);

  const filteredCards = useMemo(() => {
    if (!searchText) return allCards;
    const lower = searchText.toLowerCase();
    return allCards.filter(
      (card) =>
        card.name.toLowerCase().includes(lower) ||
        card.description.toLowerCase().includes(lower)
    );
  }, [allCards, searchText]);

  const classified = useMemo(() => classifyCards(filteredCards), [filteredCards]);

  const totalDiscovered = allCards.filter((c) => discoveredSet.has(c.cardTypeId)).length;
  const totalCards = allCards.length;

  const sections = (["baseCards", "derivedCards", "talentCards"] as const).filter(
    (key) => classified[key].length > 0 || !searchText
  );

  return (
    <div className="memories-section">
      {/* Stats bar */}
      <div className="memories-stats-bar">
        <div className="memories-stat">
          <span className="memories-stat-value">
            {totalDiscovered}/{totalCards}
          </span>
          <span className="memories-stat-label">発見済み</span>
        </div>
        <div className="memories-stat">
          <span className="memories-stat-value">
            {totalCards > 0 ? Math.round((totalDiscovered / totalCards) * 100) : 0}%
          </span>
          <span className="memories-stat-label">達成率</span>
        </div>
      </div>

      {/* Classified card sections */}
      <div className="memories-cards-classified">
        {sections.map((key) => {
          const cards = classified[key];
          const sectionDiscovered = cards.filter((c) => discoveredSet.has(c.cardTypeId)).length;
          return (
            <div key={key} className="memories-card-classification">
              <div className="memories-classification-header">
                <span className="memories-classification-title">
                  {CLASSIFICATION_LABELS[key]}
                </span>
                <span className="memories-classification-count">
                  {sectionDiscovered}/{cards.length}
                </span>
              </div>
              {cards.length === 0 ? (
                <div className="memories-classification-empty">
                  該当カードなし
                </div>
              ) : (
                <div className="memories-grid cards-grid">
                  {cards.map((card) => {
                    const isDiscovered = discoveredSet.has(card.cardTypeId);
                    return (
                      <div
                        key={card.cardTypeId}
                        className={`memories-card-entry ${isDiscovered ? "discovered" : "undiscovered"}`}
                      >
                        {isDiscovered ? (
                          <>
                            <div className="entry-header">
                              <span className="entry-name">{card.name}</span>
                              <span className="entry-rarity">
                                Cost: {card.cost}
                              </span>
                            </div>
                            <div className="entry-details">
                              <span className="entry-element">
                                {card.element.join(", ")}
                              </span>
                            </div>
                            <p className="entry-description">{card.description}</p>
                          </>
                        ) : (
                          <div className="entry-locked">
                            <span className="locked-icon">❓</span>
                            <span className="locked-text">未発見</span>
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          );
        })}
      </div>

      {filteredCards.length === 0 && (
        <div className="memories-empty">
          <span>該当するカードが見つかりません</span>
        </div>
      )}
    </div>
  );
}

// ============================================================================
// Enemies Section
// ============================================================================

function EnemiesSection({ discovery, searchText }: SectionProps) {
  const allEnemies = useMemo(() => getAllEnemies(), []);
  const stats = useMemo(() => getEnemyStats(), []);

  const filteredEnemies = useMemo(() => {
    if (!searchText) return allEnemies;
    const lower = searchText.toLowerCase();
    return allEnemies.filter(
      (enemy) =>
        enemy.name.toLowerCase().includes(lower) ||
        (enemy.nameJa?.toLowerCase().includes(lower) ?? false) ||
        enemy.description.toLowerCase().includes(lower)
    );
  }, [allEnemies, searchText]);

  const discoveredSet = useMemo(() => new Set(discovery.enemies), [discovery.enemies]);
  const discoveredCount = discoveredSet.size;

  return (
    <div className="memories-section">
      {/* Stats bar */}
      <div className="memories-stats-bar">
        <div className="memories-stat">
          <span className="memories-stat-value">
            {discoveredCount}/{stats.total}
          </span>
          <span className="memories-stat-label">発見済み</span>
        </div>
        <div className="memories-stat">
          <span className="memories-stat-value">{stats.normalCount}</span>
          <span className="memories-stat-label">通常</span>
        </div>
        <div className="memories-stat">
          <span className="memories-stat-value">{stats.bossCount}</span>
          <span className="memories-stat-label">ボス</span>
        </div>
      </div>

      {/* Enemy list */}
      <div className="memories-grid enemies-grid">
        {filteredEnemies.map((enemy) => {
          const isDiscovered = discoveredSet.has(enemy.id);
          const isBoss = isBossEnemy(enemy);
          return (
            <div
              key={enemy.id}
              className={`memories-enemy-entry ${isDiscovered ? "discovered" : "undiscovered"} ${isBoss ? "boss" : ""}`}
            >
              {isDiscovered ? (
                <>
                  <div className="entry-header">
                    <span className="entry-name">
                      {enemy.nameJa || enemy.name}
                    </span>
                    <span className={`entry-badge ${isBoss ? "boss" : "normal"}`}>
                      {isBoss ? "BOSS" : "Normal"}
                    </span>
                  </div>
                  <div className="entry-stats">
                    <span>HP: {enemy.baseMaxHp}</span>
                    <span>SPD: {enemy.baseSpeed}</span>
                  </div>
                  <p className="entry-description">{enemy.description}</p>
                </>
              ) : (
                <div className="entry-locked">
                  <span className="locked-icon">👤</span>
                  <span className="locked-text">未遭遇</span>
                </div>
              )}
            </div>
          );
        })}
      </div>

      {filteredEnemies.length === 0 && (
        <div className="memories-empty">
          <span>該当する敵が見つかりません</span>
        </div>
      )}
    </div>
  );
}

// ============================================================================
// Equipment Section
// ============================================================================

function EquipmentSection({ discovery, searchText }: SectionProps) {
  const allEquipment = useMemo(() => getAllEquipment(), []);
  const stats = useMemo(
    () => getEquipmentStats(new Set(discovery.equipment)),
    [discovery.equipment]
  );

  const filteredEquipment = useMemo(() => {
    if (!searchText) return allEquipment;
    const lower = searchText.toLowerCase();
    return allEquipment.filter(
      (eq) =>
        eq.name.toLowerCase().includes(lower) ||
        eq.description.toLowerCase().includes(lower)
    );
  }, [allEquipment, searchText]);

  const discoveredSet = useMemo(
    () => new Set(discovery.equipment),
    [discovery.equipment]
  );

  return (
    <div className="memories-section">
      {/* Stats bar */}
      <div className="memories-stats-bar">
        <div className="memories-stat">
          <span className="memories-stat-value">
            {stats.discovered}/{stats.total}
          </span>
          <span className="memories-stat-label">発見済み</span>
        </div>
        <div className="memories-stat">
          <span className="memories-stat-value">
            {Math.round((stats.discovered / stats.total) * 100)}%
          </span>
          <span className="memories-stat-label">達成率</span>
        </div>
      </div>

      {/* Equipment list */}
      <div className="memories-grid equipment-grid">
        {filteredEquipment.map((eq) => {
          const isDiscovered = discoveredSet.has(eq.id);
          const classLabel = eq.classRestriction
            ? CLASS_NAMES[eq.classRestriction]
            : CLASS_NAMES.common;
          return (
            <div
              key={eq.id}
              className={`memories-equipment-entry ${isDiscovered ? "discovered" : "undiscovered"} rarity-${eq.rarity}`}
            >
              {isDiscovered ? (
                <>
                  <div className="entry-header">
                    <span className="entry-name">{eq.name}</span>
                    <span className={`entry-rarity rarity-${eq.rarity}`}>
                      {RARITY_NAMES[eq.rarity]}
                    </span>
                  </div>
                  <div className="entry-details">
                    <span className="entry-slot">
                      {SLOT_NAMES[eq.equipmentSlot] || eq.equipmentSlot}
                    </span>
                    <span className="entry-class">{classLabel}</span>
                  </div>
                  <p className="entry-description">{eq.description}</p>
                  {eq.skills.length > 0 && (
                    <div className="entry-skills">
                      {eq.skills.slice(0, 1).map((skill) => (
                        <span key={skill.id} className="skill-tag">
                          {skill.nameJa}
                        </span>
                      ))}
                    </div>
                  )}
                </>
              ) : (
                <div className="entry-locked">
                  <span className="locked-icon">🔒</span>
                  <span className="locked-text">未入手</span>
                </div>
              )}
            </div>
          );
        })}
      </div>

      {filteredEquipment.length === 0 && (
        <div className="memories-empty">
          <span>該当する装備が見つかりません</span>
        </div>
      )}
    </div>
  );
}

// ============================================================================
// Events Section (combines events and tips)
// ============================================================================

function EventsSection({ discovery, searchText }: SectionProps) {
  const [viewMode, setViewMode] = useState<"events" | "tips">("events");

  const allEvents = useMemo(() => getAllEvents(), []);
  const allTips = useMemo(() => getAllTips(), []);
  const eventStats = useMemo(
    () => getEventStats(new Set(discovery.events)),
    [discovery.events]
  );

  const filteredEvents = useMemo(() => {
    if (!searchText) return allEvents;
    const lower = searchText.toLowerCase();
    return allEvents.filter(
      (event) =>
        event.title.toLowerCase().includes(lower) ||
        event.titleJa.includes(searchText) ||
        event.description.toLowerCase().includes(lower)
    );
  }, [allEvents, searchText]);

  const filteredTips = useMemo(() => {
    if (!searchText) return allTips;
    const lower = searchText.toLowerCase();
    return allTips.filter(
      (tip) =>
        tip.title.toLowerCase().includes(lower) ||
        tip.content.toLowerCase().includes(lower)
    );
  }, [allTips, searchText]);

  const discoveredSet = useMemo(
    () => new Set(discovery.events),
    [discovery.events]
  );

  return (
    <div className="memories-section">
      {/* Mode toggle */}
      <div className="memories-mode-toggle">
        <button
          className={`mode-toggle-btn ${viewMode === "events" ? "active" : ""}`}
          onClick={() => setViewMode("events")}
        >
          イベント
        </button>
        <button
          className={`mode-toggle-btn ${viewMode === "tips" ? "active" : ""}`}
          onClick={() => setViewMode("tips")}
        >
          ヒント
        </button>
      </div>

      {viewMode === "events" ? (
        <>
          {/* Stats bar */}
          <div className="memories-stats-bar">
            <div className="memories-stat">
              <span className="memories-stat-value">
                {eventStats.discovered}/{eventStats.total}
              </span>
              <span className="memories-stat-label">達成済み</span>
            </div>
            <div className="memories-stat">
              <span className="memories-stat-value">
                {Math.round((eventStats.discovered / eventStats.total) * 100)}%
              </span>
              <span className="memories-stat-label">達成率</span>
            </div>
          </div>

          {/* Events list */}
          <div className="memories-list events-list">
            {filteredEvents.map((event) => {
              const isDiscovered = discoveredSet.has(event.id);
              return (
                <div
                  key={event.id}
                  className={`memories-event-entry ${isDiscovered ? "discovered" : "undiscovered"}`}
                >
                  {isDiscovered || event.isUnlocked ? (
                    <>
                      <span className="event-icon">{event.icon}</span>
                      <div className="event-content">
                        <div className="event-header">
                          <span className="entry-name">{event.titleJa}</span>
                          <span className="event-category">
                            {EVENT_CATEGORY_NAMES[event.category]}
                          </span>
                        </div>
                        <p className="entry-description">{event.descriptionJa}</p>
                      </div>
                    </>
                  ) : (
                    <>
                      <span className="event-icon">❓</span>
                      <div className="event-content">
                        <span className="locked-text">未達成</span>
                      </div>
                    </>
                  )}
                </div>
              );
            })}
          </div>

          {filteredEvents.length === 0 && (
            <div className="memories-empty">
              <span>該当するイベントが見つかりません</span>
            </div>
          )}
        </>
      ) : (
        <>
          {/* Tips stats */}
          <div className="memories-stats-bar">
            <div className="memories-stat">
              <span className="memories-stat-value">{allTips.length}</span>
              <span className="memories-stat-label">ヒント数</span>
            </div>
          </div>

          {/* Tips list */}
          <div className="memories-list tips-list">
            {filteredTips.map((tip) => (
              <div key={tip.id} className="memories-tip-entry">
                <div className="tip-header">
                  <span className="entry-name">{tip.title}</span>
                  <span className="tip-category">
                    {TIP_CATEGORY_NAMES[tip.category]}
                  </span>
                </div>
                <p className="entry-description">{tip.content}</p>
              </div>
            ))}
          </div>

          {filteredTips.length === 0 && (
            <div className="memories-empty">
              <span>該当するヒントが見つかりません</span>
            </div>
          )}
        </>
      )}
    </div>
  );
}
