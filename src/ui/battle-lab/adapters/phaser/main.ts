// Phaser bakeoff entry point.
// Creates a single Phaser.Game instance and mounts it into #root.
// React is not involved — this file and BattleScene.ts are the entire adapter.

import Phaser from "phaser";
import { BattleScene, CANVAS_W, CANVAS_H } from "./BattleScene";
import "./phaser-bakeoff.css";

new Phaser.Game({
  type: Phaser.AUTO,
  width: CANVAS_W,
  height: CANVAS_H,
  parent: "root",
  scene: BattleScene,
  scale: {
    mode: Phaser.Scale.FIT,
    autoCenter: Phaser.Scale.CENTER_BOTH,
  },
  backgroundColor: "#0d1117",
});
