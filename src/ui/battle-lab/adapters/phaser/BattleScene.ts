// Phaser 4 battle scene adapter.
//
// Renders the "range × stamina" bakeoff combat entirely inside a Phaser canvas.
// No React, no DOM overlay — all UI is canvas Text + Graphics.
//
// Architecture: thin store (let state + dispatch) wraps the shared core
// reducer.  dispatch() mutates state and calls _render() which destroys last
// frame's objects and rebuilds from the new state.  A single persistent
// Graphics object (_rangeMarker) is kept alive across renders so it can be
// tweened smoothly on distance changes.
//
// NOTE — Japanese text in canvas (evaluation note):
//   Phaser.Text draws via the browser Canvas 2D API and uses whatever CJK font
//   the OS provides (Hiragino Sans on macOS, Yu Gothic on Windows, Noto on
//   Linux).  Characters render correctly in practice, but the font-metrics
//   calculation is tuned for Latin glyphs, so CJK characters can be clipped
//   vertically unless extra padding is added.  Cards and log lines are given
//   explicit top padding to compensate.  Word-wrap for card descriptions uses
//   Phaser's built-in width-limited wrap; it works but requires careful fixed-
//   width tuning — any text that overflows its allocated row is simply clipped.
//   Multi-line hand cards needed more vertical space than the React/HTML version
//   because canvas layout has no automatic reflow.  See report §テキスト主体UI.

import Phaser from "phaser";
import type { BattleAction, BattleState } from "@/ui/battle-lab/core/types";
import type { CardView } from "@/ui/battle-lab/core/viewModel";
import { battleReducer, initState } from "@/ui/battle-lab/core/battleReducer";
import {
  describeHand,
  isBattleOver,
  enemyRangeHint,
} from "@/ui/battle-lab/core/viewModel";
import {
  MAX_STAMINA,
  PLAYER_MAX_HP,
  FATIGUE_THRESHOLD,
} from "@/ui/battle-lab/core/constants";
import { ENEMY_DEF } from "@/ui/battle-lab/core/enemy";

// ── Canvas dimensions ────────────────────────────────────────────────────────

export const CANVAS_W = 800;
export const CANVAS_H = 700;

// ── Font ─────────────────────────────────────────────────────────────────────

// Phaser Text renders through the browser canvas 2D API, so system CJK fonts
// are available.  Fallback to sans-serif covers most Linux / Android setups.
const FONT_JP = '"Hiragino Sans", "Noto Sans JP", "Yu Gothic", sans-serif';

// ── Colour palette (0xRRGGBB for Phaser Graphics / CSS strings for Text) ──────

const C = {
  bg: 0x0d1117,
  panel: 0x161b22,
  panelBorder: 0x30363d,
  separator: 0x30363d,
  cardNormal: 0x1e2a3a,
  cardDisabled: 0x1c1c1c,
  cardBorder: 0x4a6fa5,
  cardBorderOff: 0x2a3040,
  hpBg: 0x333333,
  hpPlayer: 0x22c55e,
  hpEnemy: 0xef4444,
  stamina: 0xeab308,
  staminaFatigue: 0xf97316,
  guard: 0x60a5fa,
  nodeInactive: 0x374151,
  nodeActive: 0xf59e0b,
  markerGlow: 0xfbbf24,
  btnPrimary: 0x3b82f6,
  btnHover: 0x60a5fa,
  btnDisabled: 0x374151,
  overlayBg: 0x000000,
  winPanel: 0x052e16,
  losePanel: 0x1f0a0a,
  dmgEnemy: 0xf87171,
  dmgPlayer: 0xfbbf24,
} as const;

// CSS strings for Phaser Text `color` style property
const T = {
  white: "#e6edf3",
  dim: "#8b949e",
  red: "#f87171",
  green: "#4ade80",
  yellow: "#fbbf24",
  orange: "#f97316",
  blue: "#60a5fa",
  gold: "#f59e0b",
} as const;

// ── Layout constants ──────────────────────────────────────────────────────────

// Range track
const TRACK_Y = 65;
const NODE_XS: readonly number[] = [200, 400, 600];

// Combatant panels
const PANEL_Y = 100;
const PANEL_H = 95;
const PANEL_PAD = 8;
const PLAYER_X = 8;
const PLAYER_W = 384;
const ENEMY_X = 408;
const ENEMY_W = 384;
const BAR_W = 180;
const BAR_H = 10;

// Hand cards
const CARD_Y = 205;
const CARD_W = 248;
const CARD_H = 120;
const CARD_PAD = 8;
const CARD_XS: readonly number[] = [20, 276, 532];

// End-turn button (centre)
const BTN_CX = CANVAS_W / 2;
const BTN_CY = 340;
const BTN_W = 190;
const BTN_H = 38;

// Battle log
const LOG_TOP = 375;
const LOG_TITLE_H = 20;
const LOG_ENTRY_H = 20;
const LOG_MAX = 8;
const LOG_X = 16;
const LOG_TEXT_W = CANVAS_W - LOG_X * 2;

// ── Scene ─────────────────────────────────────────────────────────────────────

export class BattleScene extends Phaser.Scene {
  private _state: BattleState;
  /** Previous render state — used to compute HP delta for damage pop tweens. */
  private _prevState: BattleState | null = null;
  /** Previous distance index — used to detect range changes for the marker tween. */
  private _prevDistIdx: number = -1;
  /**
   * Persistent range-position marker (a glowing disc).  Kept alive across
   * renders so it can be Tween-moved to the new node position when distance
   * changes — this is the primary "演出" (visual flourish) for Phaser.
   */
  private _rangeMarker: Phaser.GameObjects.Graphics | null = null;
  /** All game objects created in the last _render() pass; destroyed at the top
   *  of the next.  The range marker and damage-pop texts are NOT in this list. */
  private _renderObjs: Phaser.GameObjects.GameObject[] = [];

  public constructor() {
    super({ key: "BattleScene" });
    this._state = initState();
  }

  public create(): void {
    this.cameras.main.setBackgroundColor(C.bg);
    this._render();
  }

  // ── Dispatch / store ───────────────────────────────────────────────────────

  private _dispatch(action: BattleAction): void {
    const prev = this._state;
    this._state = battleReducer(this._state, action);

    if (action.type === "RESTART") {
      // Snap the marker immediately — no slide animation on restart.
      this._prevState = null;
      this._prevDistIdx = -1;
      if (this._rangeMarker) {
        this.tweens.killTweensOf(this._rangeMarker);
        this._rangeMarker.x = NODE_XS[this._state.distanceIndex];
      }
    } else {
      this._prevState = prev;
    }

    this._render();
  }

  // ── Render pass ───────────────────────────────────────────────────────────

  private _render(): void {
    // Destroy last frame's objects before rebuilding
    for (const obj of this._renderObjs) {
      if (obj.active) obj.destroy();
    }
    this._renderObjs = [];

    const s = this._state;
    const prev = this._prevState;
    const isOver = isBattleOver(s.result);

    // Animate persistent range marker to new node position
    this._updateRangeMarker(s.distanceIndex);

    // Build all static UI elements from current state
    this._drawHeader(s.turn);
    this._drawRangeTrack(s.distanceIndex);
    this._drawCombatantPanel(
      PLAYER_X,
      PANEL_Y,
      PLAYER_W,
      PANEL_H,
      "あなた（剣士）",
      s.playerHp,
      PLAYER_MAX_HP,
      s.playerStamina,
      s.playerGuard,
      null,
    );
    this._drawCombatantPanel(
      ENEMY_X,
      PANEL_Y,
      ENEMY_W,
      PANEL_H,
      ENEMY_DEF.name,
      s.enemyHp,
      ENEMY_DEF.maxHp,
      s.enemyStamina,
      0,
      enemyRangeHint(),
    );

    const hand = describeHand(s);
    hand.forEach((card, i) => {
      if (i < CARD_XS.length) this._drawCard(CARD_XS[i], CARD_Y, card);
    });

    this._drawEndTurnButton(isOver);
    this._drawLog(s.log);

    if (isOver) this._drawOverlay(s.result === "won");

    // Damage pop tweens — fire for any HP loss since the last render
    if (prev) {
      const eDmg = prev.enemyHp - s.enemyHp;
      const pDmg = prev.playerHp - s.playerHp;
      if (eDmg > 0)
        this._tweenDamage(
          ENEMY_X + ENEMY_W / 2,
          PANEL_Y + 30,
          eDmg,
          C.dmgEnemy,
        );
      if (pDmg > 0)
        this._tweenDamage(
          PLAYER_X + PLAYER_W / 2,
          PANEL_Y + 30,
          pDmg,
          C.dmgPlayer,
        );
    }

    this._prevDistIdx = s.distanceIndex;
  }

  // ── Range marker (persistent, tweened) ───────────────────────────────────

  private _updateRangeMarker(distIdx: number): void {
    const targetX = NODE_XS[distIdx];

    if (!this._rangeMarker) {
      // First render — create at current position, no animation
      this._rangeMarker = this._buildMarkerGraphic(targetX);
      return;
    }

    if (this._prevDistIdx >= 0 && this._prevDistIdx !== distIdx) {
      // Distance changed — slide the marker to the new node (primary 演出)
      this.tweens.killTweensOf(this._rangeMarker);
      this.tweens.add({
        targets: this._rangeMarker,
        x: targetX,
        duration: 350,
        ease: "Power2.easeOut",
      });
    } else {
      // No change or restart — snap immediately
      this._rangeMarker.x = targetX;
    }
  }

  private _buildMarkerGraphic(x: number): Phaser.GameObjects.Graphics {
    const g = this.add.graphics();
    // Outer glow ring
    g.fillStyle(C.markerGlow, 0.28);
    g.fillCircle(0, 0, 19);
    // Solid inner disc
    g.fillStyle(C.nodeActive, 1);
    g.fillCircle(0, 0, 11);
    g.x = x;
    g.y = TRACK_Y;
    g.setDepth(5);
    return g;
  }

  // ── Utility helpers ───────────────────────────────────────────────────────

  /** Register a game object so it is destroyed at the top of the next _render(). */
  private _reg<T extends Phaser.GameObjects.GameObject>(obj: T): T {
    this._renderObjs.push(obj);
    return obj;
  }

  /** Create a Text object, register it, and return it for optional chaining. */
  private _txt(
    x: number,
    y: number,
    content: string,
    style: Phaser.Types.GameObjects.Text.TextStyle,
    depth = 1,
  ): Phaser.GameObjects.Text {
    return this._reg(
      this.add
        .text(x, y, content, { fontFamily: FONT_JP, ...style })
        .setDepth(depth),
    );
  }

  /** Draw a left-anchored horizontal bar (background + coloured fill). */
  private _bar(
    x: number,
    y: number,
    w: number,
    h: number,
    value: number,
    max: number,
    fillColor: number,
  ): void {
    const g = this.add.graphics();
    g.fillStyle(C.hpBg, 1);
    g.fillRoundedRect(x, y, w, h, 3);
    const fw = Math.max(0, Math.floor((w * Math.min(value, max)) / max));
    if (fw > 0) {
      g.fillStyle(fillColor, 1);
      g.fillRoundedRect(x, y, fw, h, 3);
    }
    this._reg(g);
  }

  // ── Header ────────────────────────────────────────────────────────────────

  private _drawHeader(turn: number): void {
    this._txt(16, 11, "間合い × スタミナ（Phaser版）", {
      fontSize: "16px",
      fontStyle: "bold",
      color: T.white,
    });
    this._txt(CANVAS_W - 16, 11, `ターン ${turn}`, {
      fontSize: "14px",
      color: T.dim,
    }).setOrigin(1, 0);

    const sep = this.add.graphics();
    sep.lineStyle(1, C.separator, 0.8);
    sep.lineBetween(0, 36, CANVAS_W, 36);
    this._reg(sep);
  }

  // ── Range track ───────────────────────────────────────────────────────────

  private _drawRangeTrack(distIdx: number): void {
    const labels = ["近", "中", "遠"];

    // Connecting line behind the nodes
    const line = this.add.graphics();
    line.lineStyle(2, C.panelBorder, 1);
    line.lineBetween(NODE_XS[0], TRACK_Y, NODE_XS[2], TRACK_Y);
    this._reg(line);

    // Three nodes
    NODE_XS.forEach((nx, i) => {
      const active = i === distIdx;
      const ng = this.add.graphics();
      ng.fillStyle(active ? C.nodeActive : C.nodeInactive, 1);
      ng.fillCircle(nx, TRACK_Y, 12);
      this._reg(ng);

      this._txt(nx, TRACK_Y + 16, labels[i], {
        fontSize: "12px",
        color: active ? T.gold : T.dim,
        align: "center",
      }).setOrigin(0.5, 0);
    });
  }

  // ── Combatant panels ──────────────────────────────────────────────────────

  private _drawCombatantPanel(
    x: number,
    y: number,
    w: number,
    h: number,
    name: string,
    hp: number,
    maxHp: number,
    stamina: number,
    guard: number,
    hint: string | null,
  ): void {
    const bg = this.add.graphics();
    bg.fillStyle(C.panel, 1);
    bg.fillRoundedRect(x, y, w, h, 6);
    bg.lineStyle(1, C.panelBorder, 1);
    bg.strokeRoundedRect(x, y, w, h, 6);
    this._reg(bg);

    // Name
    this._txt(x + PANEL_PAD, y + 7, name, {
      fontSize: "13px",
      fontStyle: "bold",
      color: T.white,
    });

    // HP bar + label
    const barX = x + PANEL_PAD;
    this._bar(
      barX,
      y + 26,
      BAR_W,
      BAR_H,
      hp,
      maxHp,
      hint ? C.hpEnemy : C.hpPlayer,
    );
    this._txt(barX + BAR_W + 6, y + 25, `HP ${hp}/${maxHp}`, {
      fontSize: "11px",
      color: T.dim,
    });

    // Stamina bar + label
    const fatigued = stamina < FATIGUE_THRESHOLD;
    const stColor = fatigued ? C.staminaFatigue : C.stamina;
    this._bar(barX, y + 43, BAR_W, BAR_H, stamina, MAX_STAMINA, stColor);
    const stLabel = fatigued
      ? `気力 ${stamina}/${MAX_STAMINA}（疲労）`
      : `気力 ${stamina}/${MAX_STAMINA}`;
    this._txt(barX + BAR_W + 6, y + 42, stLabel, {
      fontSize: "11px",
      color: fatigued ? T.orange : T.dim,
    });

    // Guard (shown for player when active)
    if (guard > 0) {
      this._txt(x + PANEL_PAD, y + 61, `ガード: ${guard}`, {
        fontSize: "12px",
        color: T.blue,
      });
    }

    // Enemy range hint
    if (hint) {
      this._txt(x + PANEL_PAD, y + 61, hint, {
        fontSize: "10px",
        color: T.dim,
        wordWrap: { width: w - PANEL_PAD * 2 },
        lineSpacing: 1,
      });
    }
  }

  // ── Hand cards ────────────────────────────────────────────────────────────

  private _drawCard(x: number, y: number, card: CardView): void {
    const {
      instanceId,
      name,
      cost,
      type,
      playable,
      disabledReason,
      effectiveRangeLabel,
      damage,
      guard,
      shiftLabel,
      description,
    } = card;

    const bgColor = playable ? C.cardNormal : C.cardDisabled;
    const borderColor = playable ? C.cardBorder : C.cardBorderOff;
    const textAlpha = playable ? 1 : 0.55;

    // Card background — kept as a variable so hover handlers can redraw it
    const bg = this.add.graphics();
    const redrawBg = (borderC: number, borderW: number): void => {
      bg.clear();
      bg.fillStyle(bgColor, 1);
      bg.fillRoundedRect(x, y, CARD_W, CARD_H, 6);
      bg.lineStyle(borderW, borderC, 1);
      bg.strokeRoundedRect(x, y, CARD_W, CARD_H, 6);
    };
    redrawBg(borderColor, playable ? 1.5 : 1);
    this._reg(bg);

    // Card name
    this._txt(x + CARD_PAD, y + 7, name, {
      fontSize: "13px",
      fontStyle: "bold",
      color: playable ? T.white : T.dim,
    }).setAlpha(textAlpha);

    // Stamina cost (right-aligned)
    this._txt(x + CARD_W - CARD_PAD, y + 7, `気力 ${cost}`, {
      fontSize: "12px",
      color: T.yellow,
    })
      .setOrigin(1, 0)
      .setAlpha(textAlpha);

    // Card type + effective range label
    const typeLabel =
      type === "attack" ? "攻撃" : type === "guard" ? "防御" : "移動";
    const rangePart = effectiveRangeLabel
      ? `  有効: ${effectiveRangeLabel}`
      : "";
    this._txt(x + CARD_PAD, y + 24, typeLabel + rangePart, {
      fontSize: "11px",
      color: T.dim,
    }).setAlpha(textAlpha);

    // Primary effect line: damage / guard bonus / shift
    let effectLine = "";
    let effectColor: string = T.white;
    if (damage) {
      effectLine = damage.text;
      effectColor = damage.offRange || damage.fatigued ? T.orange : T.green;
    } else if (guard > 0) {
      effectLine = `ガード +${guard}`;
      effectColor = T.blue;
    }
    if (shiftLabel) {
      effectLine += (effectLine ? "  " : "") + shiftLabel;
      if (!damage && guard === 0) effectColor = T.gold;
    }
    if (effectLine) {
      this._txt(x + CARD_PAD, y + 38, effectLine, {
        fontSize: "11px",
        color: effectColor,
      }).setAlpha(textAlpha);
    }

    // Description (word-wrapped, may clip if too long)
    this._txt(x + CARD_PAD, y + 55, description, {
      fontSize: "11px",
      color: T.dim,
      wordWrap: { width: CARD_W - CARD_PAD * 2 },
      fixedWidth: CARD_W - CARD_PAD * 2,
    }).setAlpha(textAlpha);

    // Disabled reason (bottom of card, only when unaffordable)
    if (disabledReason) {
      this._txt(x + CARD_PAD, y + CARD_H - 20, disabledReason, {
        fontSize: "11px",
        color: T.red,
      });
    }

    // Transparent hit zone for click / hover (playable cards only)
    if (playable) {
      const zone = this._reg(
        this.add.rectangle(
          x + CARD_W / 2,
          y + CARD_H / 2,
          CARD_W,
          CARD_H,
          0xffffff,
          0,
        ),
      );
      zone.setDepth(2);
      zone.setInteractive({ useHandCursor: true });
      zone.on("pointerdown", () => {
        this._dispatch({ type: "PLAY_CARD", instanceId });
      });
      zone.on("pointerover", () => {
        redrawBg(C.markerGlow, 2);
      });
      zone.on("pointerout", () => {
        redrawBg(borderColor, 1.5);
      });
    }
  }

  // ── End-turn button ───────────────────────────────────────────────────────

  private _drawEndTurnButton(isOver: boolean): void {
    const btnColor = isOver ? C.btnDisabled : C.btnPrimary;

    const g = this.add.graphics();
    const redrawBtn = (c: number): void => {
      g.clear();
      g.fillStyle(c, 1);
      g.fillRoundedRect(
        BTN_CX - BTN_W / 2,
        BTN_CY - BTN_H / 2,
        BTN_W,
        BTN_H,
        8,
      );
    };
    redrawBtn(btnColor);
    this._reg(g);

    this._txt(BTN_CX, BTN_CY, "ターン終了", {
      fontSize: "15px",
      fontStyle: "bold",
      color: isOver ? T.dim : T.white,
      align: "center",
    })
      .setOrigin(0.5)
      .setDepth(2);

    if (!isOver) {
      const zone = this._reg(
        this.add.rectangle(BTN_CX, BTN_CY, BTN_W, BTN_H, 0xffffff, 0),
      );
      zone.setDepth(3);
      zone.setInteractive({ useHandCursor: true });
      zone.on("pointerdown", () => {
        this._dispatch({ type: "END_TURN" });
      });
      zone.on("pointerover", () => {
        redrawBtn(C.btnHover);
      });
      zone.on("pointerout", () => {
        redrawBtn(C.btnPrimary);
      });
    }
  }

  // ── Battle log ────────────────────────────────────────────────────────────

  private _drawLog(log: BattleState["log"]): void {
    this._txt(LOG_X, LOG_TOP, "戦闘ログ", {
      fontSize: "13px",
      fontStyle: "bold",
      color: T.dim,
    });

    // Show newest entries at the top (reverse order), capped to LOG_MAX
    const entries = [...log].reverse().slice(0, LOG_MAX);
    entries.forEach((entry, i) => {
      this._txt(
        LOG_X,
        LOG_TOP + LOG_TITLE_H + i * LOG_ENTRY_H,
        `• ${entry.text}`,
        { fontSize: "12px", color: T.dim, fixedWidth: LOG_TEXT_W },
      );
    });
  }

  // ── Result overlay ────────────────────────────────────────────────────────

  private _drawOverlay(won: boolean): void {
    const D = 20; // base depth for this layer

    // Full-canvas dim backdrop
    this._reg(
      this.add.rectangle(
        CANVAS_W / 2,
        CANVAS_H / 2,
        CANVAS_W,
        CANVAS_H,
        C.overlayBg,
        0.82,
      ),
    ).setDepth(D);

    // Result panel
    const PX = 200,
      PY = 235,
      PW = 400,
      PH = 210;
    const panel = this.add.graphics();
    panel.fillStyle(won ? C.winPanel : C.losePanel, 1);
    panel.fillRoundedRect(PX, PY, PW, PH, 12);
    panel.lineStyle(2, won ? 0x4ade80 : 0xf87171, 1);
    panel.strokeRoundedRect(PX, PY, PW, PH, 12);
    this._reg(panel).setDepth(D + 1);

    this._txt(400, 275, won ? "勝利！" : "敗北", {
      fontSize: "36px",
      fontStyle: "bold",
      color: won ? T.green : T.red,
    })
      .setOrigin(0.5)
      .setDepth(D + 2);

    this._txt(400, 325, won ? "敵を打ち倒した！" : "力尽きた…", {
      fontSize: "16px",
      color: T.white,
    })
      .setOrigin(0.5)
      .setDepth(D + 2);

    // Restart button
    const BX = 400,
      BY = 385,
      BW = 180,
      BH = 44;
    const btnBg = this.add.graphics();
    const redrawRestartBtn = (c: number): void => {
      btnBg.clear();
      btnBg.fillStyle(c, 1);
      btnBg.fillRoundedRect(BX - BW / 2, BY - BH / 2, BW, BH, 8);
    };
    redrawRestartBtn(C.btnPrimary);
    this._reg(btnBg).setDepth(D + 2);

    this._txt(BX, BY, "もう一度", {
      fontSize: "16px",
      fontStyle: "bold",
      color: T.white,
    })
      .setOrigin(0.5)
      .setDepth(D + 3);

    const restartZone = this._reg(
      this.add.rectangle(BX, BY, BW, BH, 0xffffff, 0),
    );
    restartZone.setDepth(D + 4);
    restartZone.setInteractive({ useHandCursor: true });
    restartZone.on("pointerdown", () => {
      this._dispatch({ type: "RESTART" });
    });
    restartZone.on("pointerover", () => {
      redrawRestartBtn(C.btnHover);
    });
    restartZone.on("pointerout", () => {
      redrawRestartBtn(C.btnPrimary);
    });
  }

  // ── Damage pop tween (演出) ────────────────────────────────────────────────

  /**
   * Spawn a floating damage number that rises and fades.  This is the Phaser
   * "演出" showcase: a one-liner with Tween + scale that would require
   * considerably more boilerplate in a DOM/CSS approach.
   */
  private _tweenDamage(
    cx: number,
    y: number,
    value: number,
    hexColor: number,
  ): void {
    const cssColor = "#" + hexColor.toString(16).padStart(6, "0");
    const t = this.add
      .text(cx, y, `−${value}`, {
        fontFamily: FONT_JP,
        fontSize: "26px",
        fontStyle: "bold",
        color: cssColor,
        stroke: "#000000",
        strokeThickness: 3,
      })
      .setOrigin(0.5)
      .setDepth(30);

    this.tweens.add({
      targets: t,
      y: y - 65,
      alpha: 0,
      scale: 1.5,
      duration: 900,
      ease: "Power2.easeOut",
      onComplete: () => {
        if (t.active) t.destroy();
      },
    });
  }
}
