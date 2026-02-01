/**
 * CharacterCard Component
 *
 * Displays a selectable character class card with stats,
 * unique mechanic info, and description.
 */

import React from "react";
import type { CharacterClassInfo } from "@/constants/data/characters/CharacterClassData";

interface CharacterCardProps {
  classInfo: CharacterClassInfo;
  isSelected: boolean;
  onSelect: () => void;
}

/**
 * Get icon based on class type
 */
function getClassIcon(iconType: string): string {
  switch (iconType) {
    case "sword":
      return "\u2694\uFE0F"; // Crossed swords
    case "staff":
      return "\u2728"; // Sparkles (magic)
    case "summon":
      return "\u{1F47B}"; // Ghost (summoning)
    default:
      return "\u2B50"; // Star
  }
}

export const CharacterCard: React.FC<CharacterCardProps> = ({
  classInfo,
  isSelected,
  onSelect,
}) => {
  const {
    name,
    japaneseName,
    description,
    uniqueMechanic,
    mechanicDescription,
    stats,
    isAvailable,
    themeColor,
    icon,
  } = classInfo;

  const cardClasses = [
    "character-card",
    isSelected && "selected",
    !isAvailable && "disabled",
  ]
    .filter(Boolean)
    .join(" ");

  const handleClick = () => {
    if (isAvailable) {
      onSelect();
    }
  };

  return (
    <div
      className={cardClasses}
      style={{ "--theme-color": themeColor } as React.CSSProperties}
      onClick={handleClick}
    >
      {/* Coming Soon overlay for unavailable classes */}
      {!isAvailable && (
        <div className="coming-soon-overlay">
          <span className="coming-soon-text">Coming Soon</span>
          <span className="coming-soon-subtext">Under Development</span>
        </div>
      )}

      {/* Selection indicator */}
      <div className="selection-indicator">
        <span className="selection-checkmark">{"\u2713"}</span>
      </div>

      {/* Header with icon and name */}
      <div className="character-card-header">
        <div className="character-icon">{getClassIcon(icon)}</div>
        <div className="character-name-block">
          <h3 className="character-class-name">{name}</h3>
          <span className="character-class-japanese">{japaneseName}</span>
        </div>
      </div>

      {/* Stats */}
      <div className="character-stats">
        <div className="stat-row">
          <span className="stat-label">HP</span>
          <span className="stat-value">{stats.hp}</span>
        </div>
        <div className="stat-row">
          <span className="stat-label">AP</span>
          <span className="stat-value">{stats.ap}</span>
        </div>
        <div className="stat-row">
          <span className="stat-label">Speed</span>
          <span className="stat-value">{stats.speed}</span>
        </div>
        <div className="stat-row">
          <span className="stat-label">Energy</span>
          <span className="stat-value">{stats.cardActEnergy}</span>
        </div>
      </div>

      {/* Unique Mechanic */}
      <div className="unique-mechanic">
        <div className="mechanic-title">{uniqueMechanic}</div>
        <div className="mechanic-description">{mechanicDescription}</div>
      </div>

      {/* Description */}
      <p className="character-description">{description}</p>
    </div>
  );
};
