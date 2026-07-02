// Pixi canvas portion of the PixiJS bakeoff adapter.
//
// DOM / Pixi split (record for bakeoff comparison):
//   Pixi <canvas>: full background, distance-track 3-node bar with ⚔ marker,
//                  combatant HP/stamina bars with text labels, guard badge,
//                  damage-number pop FX (rAF fade + upward drift)
//   DOM overlay:   hand cards, end-turn button, battle log, result modal
//                  → lives in PixiBattle.tsx / sibling components
//
// StrictMode safety (mirroring BattleCanvas.tsx §Step 0-A / issue #602):
//   BattleField is loaded via React.lazy from PixiBattle, which keeps
//   <Application> out of the synchronous double-render. The inner
//   mountedRef + ready gate defers <PixiStage> to the first real commit.

import type { JSX } from "react";
import { Suspense, useCallback, useEffect, useRef, useState } from "react";
import { useApplication } from "@pixi/react";
import type { Graphics as PixiGraphics } from "pixi.js";

// Re-use Phase 1 PixiStage — extend({Container,Graphics,Sprite,Text}) runs
// inside PixiStage.tsx on module load, making <pixiContainer> / <pixiGraphics>
// / <pixiText> valid JSX intrinsics from the moment this module evaluates.
import { PixiStage } from "@/ui/pixi/core/PixiStage";

import type { BattleState } from "@/ui/battle-lab/core/types";
import {
  FATIGUE_THRESHOLD,
  MAX_STAMINA,
  PLAYER_MAX_HP,
  RANGE_LABEL,
  RANGE_ORDER,
} from "@/ui/battle-lab/core/constants";
import { ENEMY_DEF } from "@/ui/battle-lab/core/enemy";
import { enemyRangeHint } from "@/ui/battle-lab/core/viewModel";

// ── palette (hex numbers, no CSS strings inside Pixi draw calls) ────────────
const C = {
  bg: 0x14110f,
  panelPlayer: 0x1b1714,
  panelEnemy: 0x1f1513,
  border: 0x3a322a,
  borderGold: 0xb88a3e,
  borderEnemy: 0x4a2f2a,
  nodeInactive: 0x1d1916,
  nodeActive: 0x2a2118,
  textMain: 0xe8e0d4,
  textGold: 0xd8c49a,
  textMuted: 0x9a8c74,
  textBlue: 0x9ec4e6,
  textRed: 0xd06a5a,
  barTrack: 0x0e0c0a,
  hpFill: 0xc4443a,
  staminaFill: 0x4fae8c,
  fatigueFill: 0xb8923e,
  popPlayer: 0xff5555, // player takes damage → bright red
  popEnemy: 0xf0d060, // enemy takes damage → gold
} as const;

// ── layout helpers ──────────────────────────────────────────────────────────

interface Layout {
  W: number;
  H: number;
  // distance track
  trackY: number;
  trackH: number;
  nodeW: number;
  nodeH: number;
  nodeGap: number;
  trackStartX: number;
  // combatant panels
  panelY: number;
  panelH: number;
  panelPad: number;
  leftX: number;
  leftW: number;
  rightX: number;
  rightW: number;
  barH: number;
  barGap: number;
  nameFontSize: number;
  barFontSize: number;
  hintFontSize: number;
  // damage pop anchor centres
  playerPopX: number;
  playerPopY: number;
  enemyPopX: number;
  enemyPopY: number;
}

function buildLayout(W: number, H: number): Layout {
  const trackY = H * 0.03;
  const trackH = H * 0.24;
  const nodeW = W * 0.24;
  const nodeH = trackH * 0.88;
  const nodeGap = W * 0.04;
  const trackTotal = nodeW * 3 + nodeGap * 2;
  const trackStartX = (W - trackTotal) / 2;

  const panelY = trackY + trackH + H * 0.04;
  const panelH = H - panelY - H * 0.03;
  const panelPad = W * 0.016;
  const leftX = W * 0.02;
  const leftW = W * 0.46;
  const rightX = W * 0.52;
  const rightW = W * 0.46;
  const barH = Math.max(14, H * 0.07);
  const barGap = H * 0.018;
  const nameFontSize = Math.max(11, H * 0.045);
  const barFontSize = Math.max(9, H * 0.034);
  const hintFontSize = Math.max(8, H * 0.028);

  // damage pop anchor: centre of each panel's HP bar area
  const playerPopX = leftX + leftW / 2;
  const playerPopY = panelY + H * 0.12;
  const enemyPopX = rightX + rightW / 2;
  const enemyPopY = panelY + H * 0.12;

  return {
    W,
    H,
    trackY,
    trackH,
    nodeW,
    nodeH,
    nodeGap,
    trackStartX,
    panelY,
    panelH,
    panelPad,
    leftX,
    leftW,
    rightX,
    rightW,
    barH,
    barGap,
    nameFontSize,
    barFontSize,
    hintFontSize,
    playerPopX,
    playerPopY,
    enemyPopX,
    enemyPopY,
  };
}

// ── damage pop hook ──────────────────────────────────────────────────────────

interface PopState {
  /** Increments on each new pop emission; used as rAF effect dependency. */
  id: number;
  label: string;
  alpha: number;
  /** Upward pixel offset from the anchor Y. */
  yOffset: number;
}

const POP_LIFETIME_MS = 1100;
const POP_DRIFT_PX = 52;

/**
 * Tracks one HP value and emits a fading damage-number pop whenever it
 * decreases. The rAF loop follows the pattern from EffectLayer.tsx.
 */
function useSinglePop(currentHp: number): PopState | null {
  const prevHpRef = useRef(currentHp);
  const popIdRef = useRef(0);
  const rafRef = useRef<number | null>(null);
  const [pop, setPop] = useState<PopState | null>(null);

  // Detect HP decrease and emit a new pop.
  useEffect(() => {
    const dmg = prevHpRef.current - currentHp;
    prevHpRef.current = currentHp;
    if (dmg <= 0) return;
    popIdRef.current += 1;
    setPop({ id: popIdRef.current, label: `-${dmg}`, alpha: 1, yOffset: 0 });
  }, [currentHp]);

  // rAF fade-out + upward drift loop, keyed on pop.id so it restarts only
  // when a new pop is emitted (alpha/yOffset ticks are intentionally excluded).
  useEffect(() => {
    if (!pop) return;
    const start = performance.now();

    const tick = (now: number) => {
      const elapsed = now - start;
      const remaining = 1 - elapsed / POP_LIFETIME_MS;
      if (remaining <= 0) {
        setPop(null);
        rafRef.current = null;
        return;
      }
      const yOffset = (1 - remaining) * POP_DRIFT_PX;
      setPop((prev) => (prev ? { ...prev, alpha: remaining, yOffset } : prev));
      rafRef.current = requestAnimationFrame(tick);
    };

    rafRef.current = requestAnimationFrame(tick);
    return () => {
      if (rafRef.current !== null) {
        cancelAnimationFrame(rafRef.current);
        rafRef.current = null;
      }
    };
    // Re-run only when a new pop id is emitted, matching EffectLayer.tsx pattern
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pop?.id]);

  return pop;
}

// ── sub-components (all internal, not exported) ──────────────────────────────

interface LayoutProps {
  readonly layout: Layout;
}

function Background({ layout: { W, H } }: LayoutProps): JSX.Element {
  const draw = useCallback(
    (g: PixiGraphics) => {
      g.clear();
      g.rect(0, 0, W, H);
      g.fill(C.bg);
    },
    [W, H],
  );
  return <pixiGraphics draw={draw} />;
}

interface RangeTrackProps extends LayoutProps {
  readonly distanceIndex: number;
}

function RangeTrack({ layout, distanceIndex }: RangeTrackProps): JSX.Element {
  const { trackY, trackH, nodeW, nodeH, nodeGap, trackStartX, nameFontSize } =
    layout;
  const nodeY = trackY + (trackH - nodeH) / 2;
  const cornerR = nodeH * 0.12;

  const draw = useCallback(
    (g: PixiGraphics) => {
      g.clear();
      RANGE_ORDER.forEach((_, i) => {
        const nx = trackStartX + i * (nodeW + nodeGap);
        const isActive = i === distanceIndex;
        // node background
        g.roundRect(nx, nodeY, nodeW, nodeH, cornerR);
        g.fill(isActive ? C.nodeActive : C.nodeInactive);
        // node border
        g.roundRect(nx, nodeY, nodeW, nodeH, cornerR);
        g.stroke({
          color: isActive ? C.borderGold : C.border,
          width: isActive ? 2 : 1,
        });
        if (isActive) {
          // subtle glow: larger semi-transparent rect
          g.roundRect(nx - 2, nodeY - 2, nodeW + 4, nodeH + 4, cornerR + 2);
          g.fill({ color: C.borderGold, alpha: 0.12 });
        }
      });
    },
    [trackStartX, nodeW, nodeH, nodeGap, nodeY, distanceIndex, cornerR],
  );

  const fs = nameFontSize * 0.9;
  const markerFs = nameFontSize;

  return (
    <pixiContainer>
      <pixiGraphics draw={draw} />
      {RANGE_ORDER.map((band, i) => {
        const nx = trackStartX + i * (nodeW + nodeGap);
        const cx = nx + nodeW / 2;
        const isActive = i === distanceIndex;
        return (
          <pixiContainer key={band}>
            {/* range band label */}
            <pixiText
              text={RANGE_LABEL[band]}
              x={cx}
              y={nodeY + nodeH * 0.28}
              anchor={{ x: 0.5, y: 0.5 }}
              style={{
                fill: isActive ? C.textGold : C.textMain,
                fontSize: fs,
                fontFamily: "system-ui, sans-serif",
              }}
            />
            {/* ⚔ current-position marker */}
            {isActive && (
              <pixiText
                text="⚔"
                x={cx}
                y={nodeY + nodeH * 0.72}
                anchor={{ x: 0.5, y: 0.5 }}
                style={{
                  fill: 0xe6b84e,
                  fontSize: markerFs,
                  fontFamily: "system-ui, sans-serif",
                }}
              />
            )}
          </pixiContainer>
        );
      })}
    </pixiContainer>
  );
}

interface BarDrawArgs {
  g: PixiGraphics;
  x: number;
  y: number;
  w: number;
  h: number;
  fillRatio: number;
  fillColor: number;
  cornerR: number;
}

function drawBar({
  g,
  x,
  y,
  w,
  h,
  fillRatio,
  fillColor,
  cornerR,
}: BarDrawArgs): void {
  // track
  g.roundRect(x, y, w, h, cornerR);
  g.fill(C.barTrack);
  g.roundRect(x, y, w, h, cornerR);
  g.stroke({ color: C.border, width: 1 });
  // fill
  const fillW = Math.max(0, Math.min(w, w * fillRatio));
  if (fillW > 0) {
    g.roundRect(x, y, fillW, h, cornerR);
    g.fill(fillColor);
  }
}

interface CombatantPanelProps extends LayoutProps {
  readonly side: "player" | "enemy";
  readonly hp: number;
  readonly maxHp: number;
  readonly stamina: number;
  readonly guard?: number;
  readonly hintText?: string;
  readonly name: string;
}

function CombatantPanel({
  layout,
  side,
  hp,
  maxHp,
  stamina,
  guard,
  hintText,
  name,
}: CombatantPanelProps): JSX.Element {
  const {
    panelY,
    panelH,
    panelPad,
    leftX,
    leftW,
    rightX,
    rightW,
    barH,
    barGap,
    nameFontSize,
    barFontSize,
    hintFontSize,
  } = layout;

  const panelX = side === "player" ? leftX : rightX;
  const panelW = side === "player" ? leftW : rightW;
  const isFatigued = stamina < FATIGUE_THRESHOLD;
  const cornerR = panelH * 0.04;
  const barCornerR = barH * 0.35;
  const innerW = panelW - panelPad * 2;

  // vertical positions inside panel
  const nameY = panelY + panelPad + nameFontSize * 0.6;
  const hp1Y = nameY + nameFontSize * 0.6 + barGap;
  const st1Y = hp1Y + barH + barGap;
  const extraY = st1Y + barH + barGap * 0.8;

  const draw = useCallback(
    (g: PixiGraphics) => {
      g.clear();
      // panel background
      g.roundRect(panelX, panelY, panelW, panelH, cornerR);
      g.fill(side === "player" ? C.panelPlayer : C.panelEnemy);
      g.roundRect(panelX, panelY, panelW, panelH, cornerR);
      g.stroke({
        color: side === "player" ? C.border : C.borderEnemy,
        width: 1,
      });

      const bx = panelX + panelPad;

      // HP bar
      drawBar({
        g,
        x: bx,
        y: hp1Y,
        w: innerW,
        h: barH,
        fillRatio: maxHp > 0 ? hp / maxHp : 0,
        fillColor: C.hpFill,
        cornerR: barCornerR,
      });

      // stamina bar
      drawBar({
        g,
        x: bx,
        y: st1Y,
        w: innerW,
        h: barH,
        fillRatio: MAX_STAMINA > 0 ? stamina / MAX_STAMINA : 0,
        fillColor: isFatigued ? C.fatigueFill : C.staminaFill,
        cornerR: barCornerR,
      });

      // guard badge outline
      if (guard !== undefined && guard > 0) {
        const badgeW = innerW * 0.42;
        const badgeH = barH * 0.85;
        g.roundRect(bx, extraY, badgeW, badgeH, badgeH * 0.3);
        g.fill({ color: 0x1a2a3a, alpha: 1 });
        g.roundRect(bx, extraY, badgeW, badgeH, badgeH * 0.3);
        g.stroke({ color: 0x4a6a8a, width: 1 });
      }
    },
    [
      panelX,
      panelY,
      panelW,
      panelH,
      panelPad,
      innerW,
      hp1Y,
      st1Y,
      barH,
      barCornerR,
      cornerR,
      side,
      hp,
      maxHp,
      stamina,
      isFatigued,
      guard,
      extraY,
    ],
  );

  const bx = panelX + panelPad;
  const cx = panelX + panelW / 2;

  return (
    <pixiContainer>
      <pixiGraphics draw={draw} />

      {/* name */}
      <pixiText
        text={name}
        x={bx}
        y={nameY}
        anchor={{ x: 0, y: 0.5 }}
        style={{
          fill: C.textGold,
          fontSize: nameFontSize,
          fontFamily: "system-ui, sans-serif",
        }}
      />

      {/* HP label (centred on bar) */}
      <pixiText
        text={`HP ${hp} / ${maxHp}`}
        x={cx}
        y={hp1Y + barH / 2}
        anchor={{ x: 0.5, y: 0.5 }}
        style={{
          fill: C.textMain,
          fontSize: barFontSize,
          fontFamily: "system-ui, sans-serif",
          dropShadow: { color: 0x000000, blur: 3, distance: 0, alpha: 1 },
        }}
      />

      {/* stamina label (centred on bar) */}
      <pixiText
        text={`気力 ${stamina} / ${MAX_STAMINA}${isFatigued ? "（疲労）" : ""}`}
        x={cx}
        y={st1Y + barH / 2}
        anchor={{ x: 0.5, y: 0.5 }}
        style={{
          fill: C.textMain,
          fontSize: barFontSize,
          fontFamily: "system-ui, sans-serif",
          dropShadow: { color: 0x000000, blur: 3, distance: 0, alpha: 1 },
        }}
      />

      {/* guard badge text */}
      {guard !== undefined && guard > 0 && (
        <pixiText
          text={`ガード ${guard}`}
          x={bx + innerW * 0.21}
          y={extraY + barH * 0.85 * 0.5}
          anchor={{ x: 0.5, y: 0.5 }}
          style={{
            fill: C.textBlue,
            fontSize: hintFontSize,
            fontFamily: "system-ui, sans-serif",
          }}
        />
      )}

      {/* enemy range hint */}
      {hintText && (
        <pixiText
          text={hintText}
          x={bx}
          y={extraY}
          anchor={{ x: 0, y: 0 }}
          style={{
            fill: C.textMuted,
            fontSize: hintFontSize,
            fontFamily: "system-ui, sans-serif",
            wordWrap: true,
            wordWrapWidth: innerW,
            lineHeight: hintFontSize * 1.4,
          }}
        />
      )}
    </pixiContainer>
  );
}

interface DamagePopProps {
  readonly pop: PopState;
  readonly cx: number;
  readonly cy: number;
  readonly color: number;
}

function DamagePop({ pop, cx, cy, color }: DamagePopProps): JSX.Element {
  const fontSize = 28;
  return (
    <pixiText
      text={pop.label}
      x={cx}
      y={cy - pop.yOffset}
      anchor={{ x: 0.5, y: 0.5 }}
      alpha={pop.alpha}
      style={{
        fill: color,
        fontSize,
        fontFamily: "system-ui, sans-serif",
        fontWeight: "bold",
        dropShadow: { color: 0x000000, blur: 4, distance: 0, alpha: 0.8 },
      }}
    />
  );
}

// ── BattleFieldContent — rendered inside <Application> ──────────────────────

interface BattleFieldContentProps {
  readonly state: BattleState;
}

function BattleFieldContent({ state }: BattleFieldContentProps): JSX.Element {
  const { app } = useApplication();
  const W = app.screen.width;
  const H = app.screen.height;

  const playerPop = useSinglePop(state.playerHp);
  const enemyPop = useSinglePop(state.enemyHp);

  // Skip render until the canvas has measured its size.
  if (W === 0 || H === 0) return <pixiContainer />;

  const layout = buildLayout(W, H);
  const hint = enemyRangeHint();

  return (
    <pixiContainer>
      <Background layout={layout} />
      <RangeTrack layout={layout} distanceIndex={state.distanceIndex} />
      <CombatantPanel
        layout={layout}
        side="player"
        name="あなた（剣士）"
        hp={state.playerHp}
        maxHp={PLAYER_MAX_HP}
        stamina={state.playerStamina}
        guard={state.playerGuard}
      />
      <CombatantPanel
        layout={layout}
        side="enemy"
        name={ENEMY_DEF.name}
        hp={state.enemyHp}
        maxHp={ENEMY_DEF.maxHp}
        stamina={state.enemyStamina}
        hintText={hint}
      />
      {playerPop && (
        <DamagePop
          pop={playerPop}
          cx={layout.playerPopX}
          cy={layout.playerPopY}
          color={C.popPlayer}
        />
      )}
      {enemyPop && (
        <DamagePop
          pop={enemyPop}
          cx={layout.enemyPopX}
          cy={layout.enemyPopY}
          color={C.popEnemy}
        />
      )}
    </pixiContainer>
  );
}

// ── BattleField (public export) — StrictMode gate + PixiStage mount ─────────

export interface BattleFieldProps {
  readonly state: BattleState;
}

/**
 * Pixi canvas panel for the bakeoff.
 *
 * StrictMode safety: this component is consumed via React.lazy from
 * PixiBattle.tsx, which keeps <Application> outside the synchronous
 * double-render. The inner mountedRef + ready gate (mirroring BattleCanvas.tsx)
 * ensures <PixiStage> mounts exactly once after the first real commit.
 */
export function BattleField({ state }: BattleFieldProps): JSX.Element {
  const hostRef = useRef<HTMLDivElement>(null);
  const mountedRef = useRef(false);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    if (mountedRef.current) return;
    mountedRef.current = true;
    // Intentional one-shot post-commit setState (§Step 0-A / issue #602):
    // this effect runs exactly once after StrictMode's simulated unmount+remount.
    setReady(true);
  }, []);

  return (
    <div ref={hostRef} className="pb-field-host">
      {ready && (
        <Suspense fallback={null}>
          <PixiStage resizeTo={hostRef}>
            <BattleFieldContent state={state} />
          </PixiStage>
        </Suspense>
      )}
    </div>
  );
}
