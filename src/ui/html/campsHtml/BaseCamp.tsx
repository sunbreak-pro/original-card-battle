import { useState } from "react";
import { useGameState } from "@/contexts/GameStateContext";
import { usePlayer } from "@/contexts/PlayerContext";
import type { FacilityType } from "@/types/campTypes";
import { FACILITY_NAV_ITEMS } from "@/constants/campConstants";
import FacilityHeader from "../componentsHtml/FacilityHeader";
import "../../css/camps/BaseCamp.css";

const FacilityCard = ({
  type,
  name,
  description,
  icon,
  onEnter,
  showBadge,
}: {
  type: FacilityType;
  name: string;
  description: string;
  icon: string;
  onEnter: () => void;
  showBadge?: boolean;
}) => {
  const [isHovered, setIsHovered] = useState(false);

  return (
    <div
      className={`facility-card ${type} unlocked ${isHovered ? "hovered" : ""}`}
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
        <div className="bg-ground" />
      </div>

      {/* Facility grid */}
      <div className="facilities-grid">
        {FACILITY_NAV_ITEMS.map((item) => (
          <FacilityCard
            key={item.facilityType}
            type={item.facilityType}
            name={item.label}
            description={item.description}
            icon={item.icon}
            onEnter={() => navigateTo(item.screen)}
            showBadge={item.facilityType === "shop" && hasNewStock}
          />
        ))}
      </div>
    </div>
  );
};

export default BaseCamp;
