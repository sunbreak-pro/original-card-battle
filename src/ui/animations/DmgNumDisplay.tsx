type DamageType = "physical" | "magical" | "heal" | "shield";

interface DamageNumber {
  id: string;
  value: number;
  type: DamageType;
  x: number;
  y: number;
  timestamp: number;
}

interface DamageNumberDisplayProps {
  damageNumbers: DamageNumber[];
}

const DamageNumberDisplay = ({ damageNumbers }: DamageNumberDisplayProps) => {
  return (
    <div>
      {damageNumbers.map((dmg) => (
        <DamageItem key={dmg.id} dmg={dmg} />
      ))}

      <style>
        {`
          @keyframes damageFloat {
            0% {
              opacity: 1;
              transform: translateY(0) scale(0.8);
            }
            20% {
              transform: translateY(-20px) scale(1.2);
            }
            100% {
              opacity: 0;
              transform: translateY(-80px) scale(0.6);
            }
          }
        `}
      </style>
    </div>
  );
};

const DamageItem = ({ dmg }: { dmg: DamageNumber }) => {
  return (
    <div
      style={{
        position: "absolute",
        left: dmg.x + "px",
        top: dmg.y + "px",
        fontSize: "32px",
        fontWeight: "bold",
        color: getColor(dmg.type),
        animation: "damageFloat 0.8s ease-out forwards",
        pointerEvents: "none",
        zIndex: 1000,
      }}
    >
      {getPrefix(dmg.type)}
      {dmg.value}
    </div>
  );
};

const getColor = (type: DamageType): string => {
  switch (type) {
    case "physical":
      return "#ff4444";
    case "magical":
      return "#4488ff";
    case "heal":
      return "#44ff44";
    case "shield":
      return "#44ddff";
    default:
      return "#ffffff";
  }
};

const getPrefix = (type: DamageType): string => {
  if (type === "heal" || type === "shield") {
    return "+";
  }
  return "-";
};

export default DamageNumberDisplay;
