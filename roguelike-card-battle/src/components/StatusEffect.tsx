import { useState } from 'react';

type StatusEffectType = 'strength' | 'defense' | 'vulnerable' | 'weak' | 'poison' | 'regen';

interface StatusEffect {
  id: string;
  type: StatusEffectType;
  name: string;
  icon: string;
  value: number;
  duration: number;
  description: string;
  isDebuff: boolean;
}

interface StatusEffectDisplayProps {
  effects: StatusEffect[];
  theme: {
    primary: string;
    secondary: string;
    accent: string;
    bg: string;
    glow: string;
  };
}

const StatusEffectDisplay = ({ effects, theme }: StatusEffectDisplayProps) => {
  const [hoveredEffect, setHoveredEffect] = useState<string | null>(null);

  if (effects.length === 0) return null;

  return (
    <div
      style={{
        display: "flex",
        gap: "8px",
        flexWrap: "wrap",
        marginTop: "12px",
      }}
    >
      {effects.map((effect) => (
        <div
          key={effect.id}
          style={{
            position: "relative",
            width: "48px",
            height: "48px",
            background: effect.isDebuff
              ? "linear-gradient(135deg, #5f1e1e, #9d2e2e)"
              : `linear-gradient(135deg, ${theme.primary}, ${theme.secondary})`,
            border: `2px solid ${effect.isDebuff ? '#d94a4a' : theme.accent}`,
            borderRadius: "8px",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            fontSize: "24px",
            cursor: "pointer",
            transition: "all 0.3s ease",
            boxShadow: effect.isDebuff
              ? "0 4px 12px rgba(217, 74, 74, 0.5)"
              : `0 4px 12px ${theme.glow}`,
          }}
          onMouseEnter={() => setHoveredEffect(effect.id)}
          onMouseLeave={() => setHoveredEffect(null)}
        >
          {effect.icon}

          {/* ���p�ø */}
          <div
            style={{
              position: "absolute",
              bottom: "-6px",
              right: "-6px",
              width: "24px",
              height: "24px",
              background: "#000",
              border: `2px solid ${effect.isDebuff ? '#d94a4a' : theme.accent}`,
              borderRadius: "50%",
              fontSize: "12px",
              fontWeight: "bold",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              color: "#fff",
              boxShadow: "0 2px 6px rgba(0, 0, 0, 0.8)",
            }}
          >
            {effect.duration}
          </div>

          {/* ���Bn������� */}
          {hoveredEffect === effect.id && (
            <div
              style={{
                position: "absolute",
                bottom: "60px",
                left: "50%",
                transform: "translateX(-50%)",
                background: "rgba(0, 0, 0, 0.95)",
                border: `2px solid ${effect.isDebuff ? '#d94a4a' : theme.accent}`,
                borderRadius: "8px",
                padding: "10px 14px",
                whiteSpace: "nowrap",
                fontSize: "14px",
                zIndex: 100,
                boxShadow: `0 4px 16px rgba(0, 0, 0, 0.8), 0 0 20px ${effect.isDebuff ? 'rgba(217, 74, 74, 0.5)' : theme.glow}`,
                pointerEvents: "none",
              }}
            >
              <div
                style={{
                  fontWeight: "bold",
                  marginBottom: "6px",
                  color: effect.isDebuff ? '#ff8080' : theme.accent,
                  fontSize: "15px",
                }}
              >
                {effect.name} ({effect.value})
              </div>
              <div style={{ fontSize: "12px", color: "#ccc" }}>
                {effect.description}
              </div>
              <div
                style={{
                  fontSize: "11px",
                  color: "rgba(255, 255, 255, 0.6)",
                  marginTop: "4px",
                  fontStyle: "italic",
                }}
              >
                {effect.duration} turn{effect.duration !== 1 ? 's' : ''} remaining
              </div>

              {/* ������n�p */}
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
                  borderTop: `8px solid ${effect.isDebuff ? '#d94a4a' : theme.accent}`,
                }}
              />
            </div>
          )}
        </div>
      ))}
    </div>
  );
};

export default StatusEffectDisplay;
