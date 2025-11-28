import { useState } from "react";

type EquipmentSlot =
  | "weapon"
  | "armor"
  | "helmet"
  | "boots"
  | "accessory1"
  | "accessory2";

interface Equipment {
  id: string;
  name: string;
  slot: EquipmentSlot;
  icon: string;
  durability: number;
  maxDurability: number;
  effects: string[];
}

type EquipmentBarProps = {
  equipment: Equipment[];
  theme: {
    primary: string;
    secondary: string;
    accent: string;
    bg: string;
    glow: string;
  };
};

const slotNames: Record<EquipmentSlot, string> = {
  weapon: "Weapon",
  armor: "Armor",
  helmet: "Helmet",
  boots: "Boots",
  accessory1: "Acc1",
  accessory2: "Acc2",
};

const slotIcons: Record<EquipmentSlot, string> = {
  weapon: "W",
  armor: "A",
  helmet: "H",
  boots: "B",
  accessory1: "R",
  accessory2: "N",
};

const EquipmentBar = ({ equipment, theme }: EquipmentBarProps) => {
  const [hoveredSlot, setHoveredSlot] = useState<EquipmentSlot | null>(null);

  const slots: EquipmentSlot[] = [
    "weapon",
    "armor",
    "helmet",
    "boots",
    "accessory1",
    "accessory2",
  ];

  const getEquipmentForSlot = (slot: EquipmentSlot): Equipment | undefined => {
    return equipment.find((eq) => eq.slot === slot);
  };

  const getDurabilityColor = (
    durability: number,
    maxDurability: number
  ): string => {
    const percentage = (durability / maxDurability) * 100;
    if (percentage > 60) return "#4ade80";
    if (percentage > 30) return "#fbbf24";
    return "#ef4444";
  };

  return (
    <div
      style={{
        display: "flex",
        gap: "8px",
        justifyContent: "center",
        alignItems: "center",
      }}
    >
      {slots.map((slot) => {
        const equip = getEquipmentForSlot(slot);
        const isHovered = hoveredSlot === slot;

        return (
          <div
            key={slot}
            style={{ position: "relative", width: "64px", height: "80px" }}
            onMouseEnter={() => setHoveredSlot(slot)}
            onMouseLeave={() => setHoveredSlot(null)}
          >
            <div
              style={{
                width: "100%",
                height: "100%",
                background: equip
                  ? "linear-gradient(135deg, " +
                    theme.primary +
                    " 0%, " +
                    theme.secondary +
                    " 100%)"
                  : "rgba(40, 40, 40, 0.6)",
                border:
                  "2px solid " +
                  (equip ? theme.accent : "rgba(80, 80, 80, 0.5)"),
                borderRadius: "8px",
                display: "flex",
                flexDirection: "column",
                alignItems: "center",
                justifyContent: "center",
                boxShadow: equip
                  ? "0 4px 12px " + theme.glow
                  : "0 2px 6px rgba(0, 0, 0, 0.5)",
                transition: "all 0.3s ease",
                transform: isHovered ? "translateY(-4px)" : "translateY(0)",
                cursor: equip ? "pointer" : "default",
              }}
            >
              <div
                style={{
                  fontSize: equip ? "32px" : "24px",
                  opacity: equip ? 1 : 0.4,
                }}
              >
                {equip ? equip.icon : slotIcons[slot]}
              </div>

              <div
                style={{
                  fontSize: "10px",
                  color: equip ? theme.accent : "rgba(150, 150, 150, 0.7)",
                  marginTop: "4px",
                  fontWeight: "bold",
                }}
              >
                {slotNames[slot]}
              </div>

              {equip && (
                <div
                  style={{
                    position: "absolute",
                    bottom: "4px",
                    left: "6px",
                    right: "6px",
                    height: "6px",
                    background: "rgba(0, 0, 0, 0.7)",
                    borderRadius: "3px",
                    overflow: "hidden",
                    border: "1px solid rgba(255, 255, 255, 0.2)",
                  }}
                >
                  <div
                    style={{
                      width:
                        (equip.durability / equip.maxDurability) * 100 + "%",
                      height: "100%",
                      background: getDurabilityColor(
                        equip.durability,
                        equip.maxDurability
                      ),
                      transition: "width 0.3s ease",
                    }}
                  />
                </div>
              )}
            </div>

            {equip && isHovered && (
              <div
                style={{
                  position: "absolute",
                  bottom: "90px",
                  left: "50%",
                  transform: "translateX(-50%)",
                  background: "rgba(0, 0, 0, 0.95)",
                  border: "2px solid " + theme.accent,
                  borderRadius: "8px",
                  padding: "12px 16px",
                  minWidth: "200px",
                  whiteSpace: "nowrap",
                  zIndex: 100,
                  boxShadow:
                    "0 4px 16px rgba(0, 0, 0, 0.8), 0 0 20px " + theme.glow,
                  pointerEvents: "none",
                }}
              >
                <div
                  style={{
                    fontWeight: "bold",
                    marginBottom: "8px",
                    color: theme.accent,
                    fontSize: "15px",
                  }}
                >
                  {equip.name}
                </div>

                <div
                  style={{
                    fontSize: "12px",
                    color: "#ccc",
                    marginBottom: "6px",
                  }}
                >
                  Durability: {equip.durability}/{equip.maxDurability}
                </div>

                {equip.effects.length > 0 && (
                  <div
                    style={{
                      fontSize: "12px",
                      color: "#aaa",
                      marginTop: "6px",
                    }}
                  >
                    {equip.effects.map((effect, idx) => (
                      <div key={idx} style={{ marginTop: "2px" }}>
                        - {effect}
                      </div>
                    ))}
                  </div>
                )}

                <div
                  style={{
                    position: "absolute",
                    bottom: "-8px",
                    left: "50%",
                    transform: "translateX(-50%)",
                    width: "0",
                    height: "0",
                    borderLeft: "8px solid transparent",
                    borderRight: "8px solid transparent",
                    borderTop: "8px solid " + theme.accent,
                  }}
                />
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
};

export default EquipmentBar;
