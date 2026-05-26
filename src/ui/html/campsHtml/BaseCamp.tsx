/**
 * BaseCamp - Isometric 2.5D camp hub
 *
 * Features a central campfire with facilities arranged in a circular pattern.
 * Each facility card uses 3D transforms for an isometric view.
 */

import { useState } from "react";
import { useGameState } from "@/contexts/GameStateContext";
import { usePlayer } from "@/contexts/PlayerContext";
import type { FacilityType } from "@/types/campTypes";
import { FACILITY_NAV_ITEMS, FACILITY_ISOMETRIC_POSITIONS, type IsometricPosition } from "@/constants/campConstants";
import FacilityHeader from "../componentsHtml/FacilityHeader";
import "../../css/camps/BaseCamp.css";

// ============================================================================
// Facility Card Component
// ============================================================================

interface FacilityCardProps {
  type: FacilityType;
  name: string;
  description: string;
  icon: string;
  position: IsometricPosition;
  onEnter: () => void;
  showBadge?: boolean;
}

const FacilityCard = ({
  type,
  name,
  description,
  icon,
  position,
  onEnter,
  showBadge,
}: FacilityCardProps) => {
  const [isHovered, setIsHovered] = useState(false);

  return (
    <div
      className={`facility-card ${type} ${position} unlocked ${isHovered ? "hovered" : ""}`}
      data-position={position}
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
      onClick={onEnter}
    >
      {/* Background decorations */}
      <div className="facility-bg-pattern" />
      <div className="facility-glow" />

      {/* Notification badge */}
      {showBadge && <div className="facility-notification-badge" />}

      {/* Icon */}
      <div className="facility-icon">{icon}</div>

      {/* Name */}
      <div className="facility-name">{name}</div>

      {/* Description */}
      <div className="facility-description">{description}</div>

      {/* Hover effect */}
      {isHovered && (
        <div className="facility-hover-effect">
          <div className="hover-text enter">Enter →</div>
        </div>
      )}
    </div>
  );
};

// ============================================================================
// Central Campfire Component
// ============================================================================

const CampfireCenter = () => {
  return (
    <div className="campfire-center">
      <div className="campfire-glow" />
      <div className="campfire-icon">🔥</div>
      <div className="campfire-sparks">
        <span className="spark" />
        <span className="spark" />
        <span className="spark" />
      </div>
    </div>
  );
};

// ============================================================================
// BaseCamp Component
// ============================================================================

const BaseCamp = () => {
  const { navigateTo } = useGameState();
  const { playerData } = usePlayer();
  const hasNewStock = playerData.progression.shopStockState?.hasNewStock ?? false;

  return (
    <div className="base-camp">
      {/* Header */}
      <FacilityHeader title="basecamp" variant="basecamp" />

      {/* Background decorations */}
      <div className="camp-background">
        <div className="bg-stars" />
        <div className="bg-fog" />
        <div className="bg-ground" />
      </div>

      {/* Isometric container */}
      <div className="isometric-container">
        {/* Central campfire */}
        <CampfireCenter />

        {/* Facility cards arranged in circle */}
        <div className="facilities-isometric">
          {FACILITY_NAV_ITEMS.map((item) => (
            <FacilityCard
              key={item.facilityType}
              type={item.facilityType}
              name={item.label}
              description={item.description}
              icon={item.icon}
              position={FACILITY_ISOMETRIC_POSITIONS[item.facilityType]}
              onEnter={() => navigateTo(item.screen)}
              showBadge={item.facilityType === "shop" && hasNewStock}
            />
          ))}
        </div>
      </div>
    </div>
  );
};

export default BaseCamp;
