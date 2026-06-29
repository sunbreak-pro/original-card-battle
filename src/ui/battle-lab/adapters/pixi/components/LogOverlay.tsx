// DOM overlay — battle log list (newest first).

import type { JSX } from "react";
import type { LogEntry } from "@/ui/battle-lab/core/types";

interface LogOverlayProps {
  readonly log: readonly LogEntry[];
}

export function LogOverlay({ log }: LogOverlayProps): JSX.Element {
  const entries = [...log].reverse();
  return (
    <div className="pb-log" aria-label="戦闘ログ">
      <div className="pb-log-title">戦闘ログ</div>
      <ul className="pb-log-list">
        {entries.map((entry) => (
          <li key={entry.id} className="pb-log-entry">
            {entry.text}
          </li>
        ))}
      </ul>
    </div>
  );
}
