import type { Item } from "../../../domain/item_equipment/type/ItemTypes";
import type { EquipmentQuality } from "../../../domain/item_equipment/type/EquipmentType";
import { QUALITY_NAMES, MAX_EQUIPMENT_LEVEL } from "../../../domain/camps/types/BlacksmithTypes";

interface BlacksmithItemCardProps {
  item: Item;
  isSelected: boolean;
  onClick: () => void;
  showDurability?: boolean;
}

const BlacksmithItemCard: React.FC<BlacksmithItemCardProps> = ({
  item,
  isSelected,
  onClick,
  showDurability = false,
}) => {
  const level = item.level ?? 0;
  const quality = item.quality ?? "normal";
  const durability = item.durability ?? 100;
  const maxDurability = item.maxDurability ?? 100;
  const durabilityPercent = (durability / maxDurability) * 100;

  const getDurabilityClass = () => {
    if (durabilityPercent <= 20) return "critical";
    if (durabilityPercent <= 50) return "low";
    return "";
  };

  return (
    <div
      className={`equipment-card rarity-${item.rarity} ${isSelected ? "selected" : ""}`}
      onClick={onClick}
    >
      {/* Level Badge */}
      <span className={`level-badge ${level >= MAX_EQUIPMENT_LEVEL ? "max" : ""}`}>
        Lv.{level}
      </span>

      {/* Quality Badge */}
      <span className={`quality-badge ${quality}`}>
        {QUALITY_NAMES[quality as EquipmentQuality]}
      </span>

      {/* Item Icon */}
      <span className="item-icon">{item.type}</span>

      {/* Item Name */}
      <span className="item-name">{item.name}</span>

      {/* Durability Bar (optional) */}
      {showDurability && (
        <div className="durability-bar">
          <div
            className={`durability-fill ${getDurabilityClass()}`}
            style={{ width: `${durabilityPercent}%` }}
          />
        </div>
      )}
    </div>
  );
};

export default BlacksmithItemCard;
