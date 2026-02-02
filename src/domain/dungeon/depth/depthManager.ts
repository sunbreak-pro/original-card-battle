
export interface DepthInfo {
    depth: number;
    name: string;
}

const DEPTH_TABLE: Map<number, DepthInfo> = new Map([
    [1, { depth: 1, name: "腐食" }],
    [2, { depth: 2, name: "狂乱" }],
    [3, { depth: 3, name: "混沌" }],
    [4, { depth: 4, name: "虚無" }],
    [5, { depth: 5, name: "深淵" }],
]);

export function getDepthInfo(depth: number): DepthInfo {
    const info = DEPTH_TABLE.get(depth);
    if (!info) {
        return DEPTH_TABLE.get(1)!;
    }
    return info;
}

/**
 * Unified neutral theme for all UI
 * Depth is now indicated via text (depth number, Japanese names) only
 */
export const neutralTheme = {
    primary: "#1a1a24",
    secondary: "#2a2a3d",
    accent: "#8b7aa8",
    bg: "linear-gradient(135deg, #0a0a0f 0%, #151520 100%)",
    glow: "rgba(139, 122, 168, 0.25)",
    hover: "#a090b0",
};

export const depthThemes = {
    1: neutralTheme,
    2: neutralTheme,
    3: neutralTheme,
    4: neutralTheme,
    5: neutralTheme,
};