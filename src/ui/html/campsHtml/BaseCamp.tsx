import { useState } from "react";
import { useGameState } from "@/contexts/GameStateContext";
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
}: {
  type: FacilityType;
  name: string;
  description: string;
  icon: string;
  onEnter: () => void;
}) => {
  const [isHovered, setIsHovered] = useState(false);

  return (
    <div
      className={`facility-card ${type} unlocked ${isHovered ? "hovered" : ""}`}
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
      onClick={onEnter}
    >
      {/* 背景装飾 */}
      <div className="facility-bg-pattern" />
      <div className="facility-glow" />

      {/* アイコン */}
      <div className="facility-icon">{icon}</div>

      {/* 施設名 */}
      <div className="facility-name">{name}</div>

      {/* 説明文 */}
      <div className="facility-description">{description}</div>

      {/* ホバーエフェクト */}
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

  return (
    <div className="base-camp">
      {/* ヘッダー */}
      <FacilityHeader title="basecamp" variant="basecamp" />

      {/* 背景装飾 */}
      <div className="camp-background">
        <div className="bg-stars" />
        {/* <div className="bg-fog" /> */}
        <div className="bg-ground" />
      </div>

      {/* 施設グリッド */}
      <div className="facilities-grid">
        {FACILITY_NAV_ITEMS.map((item) => (
          <FacilityCard
            key={item.facilityType}
            type={item.facilityType}
            name={item.label}
            description={item.description}
            icon={item.icon}
            onEnter={() => navigateTo(item.screen)}
          />
        ))}
      </div>
    </div>
  );
};

export default BaseCamp;
