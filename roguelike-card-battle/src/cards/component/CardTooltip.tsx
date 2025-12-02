// import React from "react";
type Depth = 1 | 2 | 3 | 4 | 5;

interface Card {
  id: string;
  name: string;
  cost: number;
  description: string;
  depthEfficiency: Record<Depth, number>;
  basePower?: number;
  effectivePower?: number;
  gemLevel: number;
  talentProgress: number;
  talentThreshold: number;
}

type CardTooltipProps = {
  card: Card;
  currentDepth: Depth;
  position: { x: number; y: number };
  theme: {
    primary: string;
    secondary: string;
    accent: string;
    bg: string;
    glow: string;
  };
};

const CardTooltip = ({
  card,
  currentDepth,
  position,
  theme,
}: CardTooltipProps) => {
  const getEfficiencyColor = (efficiency: number): string => {
    if (efficiency >= 1.2) return "#44ff44";
    if (efficiency >= 1.0) return "#88ff88";
    if (efficiency >= 0.8) return "#ffff44";
    if (efficiency >= 0.6) return "#ff8844";
    return "#ff4444";
  };

  const talentPercentage = (card.talentProgress / card.talentThreshold) * 100;

  return (
    <div
      style={{
        position: "absolute",
        left: position.x + "px",
        top: position.y + "px",
        background: "rgba(0, 0, 0, 0.95)",
        border: "2px solid " + theme.accent,
        borderRadius: "12px",
        padding: "16px",
        minWidth: "280px",
        maxWidth: "400px",
        zIndex: 1000,
        boxShadow: "0 4px 20px rgba(0, 0, 0, 0.9), 0 0 24px " + theme.glow,
        pointerEvents: "none",
        color: "#fff",
      }}
    >
      <div
        style={{
          fontSize: "18px",
          fontWeight: "bold",
          color: theme.accent,
          marginBottom: "12px",
        }}
      >
        {card.name}
      </div>

      <div
        style={{
          fontSize: "13px",
          color: "#ccc",
          marginBottom: "16px",
          lineHeight: "1.5",
        }}
      >
        {card.description}
      </div>

      {card.basePower !== undefined && (
        <div style={{ marginBottom: "12px" }}>
          <div style={{ fontSize: "12px", color: "#aaa", marginBottom: "4px" }}>
            Base Power: {card.basePower}
          </div>
          {card.effectivePower !== undefined &&
            card.effectivePower !== card.basePower && (
              <div style={{ fontSize: "12px", color: "#4ade80" }}>
                Effective Power: {card.effectivePower}
              </div>
            )}
        </div>
      )}

      <div style={{ marginBottom: "12px" }}>
        <div style={{ fontSize: "12px", color: "#aaa", marginBottom: "6px" }}>
          Depth Efficiency:
        </div>
        <div
          style={{
            display: "flex",
            gap: "6px",
            justifyContent: "space-between",
          }}
        >
          {([1, 2, 3, 4, 5] as Depth[]).map((depth) => {
            const efficiency = card.depthEfficiency[depth];
            return (
              <div
                key={depth}
                style={{
                  flex: 1,
                  textAlign: "center",
                  padding: "6px 4px",
                  background:
                    depth === currentDepth
                      ? "rgba(255, 255, 255, 0.1)"
                      : "rgba(0, 0, 0, 0.3)",
                  borderRadius: "4px",
                  border:
                    depth === currentDepth
                      ? "2px solid " + theme.accent
                      : "1px solid rgba(100, 100, 100, 0.3)",
                }}
              >
                <div
                  style={{
                    fontSize: "10px",
                    color: "#888",
                    marginBottom: "2px",
                  }}
                >
                  D{depth}
                </div>
                <div
                  style={{
                    fontSize: "13px",
                    fontWeight: "bold",
                    color: getEfficiencyColor(efficiency),
                  }}
                >
                  {Math.round(efficiency * 100)}%
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {card.gemLevel > 0 && (
        <div style={{ marginBottom: "12px" }}>
          <div style={{ fontSize: "12px", color: "#aaa", marginBottom: "4px" }}>
            Gem Level:{" "}
            <span style={{ color: "#fbbf24", fontWeight: "bold" }}>
              {card.gemLevel}
            </span>
          </div>
        </div>
      )}

      <div>
        <div style={{ fontSize: "12px", color: "#aaa", marginBottom: "4px" }}>
          Talent Progress: {card.talentProgress} / {card.talentThreshold}
        </div>
        <div
          style={{
            width: "100%",
            height: "8px",
            background: "rgba(0, 0, 0, 0.7)",
            borderRadius: "4px",
            overflow: "hidden",
            border: "1px solid rgba(255, 255, 255, 0.2)",
          }}
        >
          <div
            style={{
              width: talentPercentage + "%",
              height: "100%",
              background: "linear-gradient(90deg, #a855f7 0%, #ec4899 100%)",
              transition: "width 0.3s ease",
            }}
          />
        </div>
      </div>

      <div
        style={{
          position: "absolute",
          bottom: "-10px",
          left: "50%",
          transform: "translateX(-50%)",
          width: "0",
          height: "0",
          borderLeft: "10px solid transparent",
          borderRight: "10px solid transparent",
          borderTop: "10px solid " + theme.accent,
        }}
      />
    </div>
  );
};

export default CardTooltip;
