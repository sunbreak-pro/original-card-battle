import type { JSX } from "react";
import { FATIGUE_THRESHOLD } from "../engine/constants";

export interface CombatantPanelProps {
  readonly side: "player" | "enemy";
  readonly name: string;
  readonly hp: number;
  readonly maxHp: number;
  readonly stamina: number;
  readonly maxStamina: number;
  readonly guard?: number;
  readonly hint?: string;
}

function pct(value: number, max: number): number {
  if (max <= 0) return 0;
  return Math.max(0, Math.min(100, (value / max) * 100));
}

export function CombatantPanel({
  side,
  name,
  hp,
  maxHp,
  stamina,
  maxStamina,
  guard,
  hint,
}: CombatantPanelProps): JSX.Element {
  const fatigued = stamina < FATIGUE_THRESHOLD;
  return (
    <div className={`pb-combatant pb-combatant--${side}`}>
      <div className="pb-combatant-name">{name}</div>

      <div className="pb-bar pb-bar--hp">
        <div className="pb-bar-fill" style={{ width: `${pct(hp, maxHp)}%` }} />
        <span className="pb-bar-text">
          HP {hp} / {maxHp}
        </span>
      </div>

      <div
        className={`pb-bar pb-bar--stamina${fatigued ? " is-fatigued" : ""}`}
      >
        <div
          className="pb-bar-fill"
          style={{ width: `${pct(stamina, maxStamina)}%` }}
        />
        <span className="pb-bar-text">
          気力 {stamina} / {maxStamina}
          {fatigued ? "（疲労）" : ""}
        </span>
      </div>

      {guard !== undefined && guard > 0 && (
        <div className="pb-guard">ガード {guard}</div>
      )}
      {hint && <div className="pb-hint">{hint}</div>}
    </div>
  );
}
