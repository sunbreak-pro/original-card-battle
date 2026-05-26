// DepthSelector - Small horizontal depth selection buttons

import type { Depth } from "@/types/campTypes";
import { DEPTH_DISPLAY_INFO } from "@/constants/dungeonConstants";

interface DepthSelectorProps {
  selectedDepth: Depth | null;
  onDepthSelect: (depth: Depth) => void;
}

export function DepthSelector({
  selectedDepth,
  onDepthSelect,
}: DepthSelectorProps) {
  return (
    <div className="depth-selector">
      <div className="depth-selector-label">深度選択 (テスト用)</div>
      <div className="depth-selector-buttons">
        {([1, 2, 3, 4, 5] as Depth[]).map((depth) => (
          <button
            key={depth}
            className={`depth-selector-btn ${selectedDepth === depth ? "depth-selected" : ""}`}
            onClick={() => onDepthSelect(depth)}
            title={DEPTH_DISPLAY_INFO[depth].japaneseName}
          >
            {depth}
          </button>
        ))}
      </div>
    </div>
  );
}
