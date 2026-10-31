// The battle scene's clocks: the scaled scene clock (Unity's UiTween with UiTween.Speed) and an unscaled
// one for input timings (WaitForSecondsRealtime). One frame loop, from the shared stage.createClock.
//
// Every length the scene animates is a nominal ms written at battle speed ×1.25 (EffectCatalog). The
// scene clock runs at scale = BattleSpeed.TweenSpeed(step) (×1.0 → 0.8, ×1.25 → 1, ×1.5 → 1.2), times
// L.FF_SCALE while fast-forward is held, and L.SHORT_SCALE when 演出短縮 is on or the battle is halted
// (battle_ui_ux_v2 §5.0: 補間と待ちの ms を 1 ms). The scale is read every frame, so a change takes effect
// at once on tweens and waits already running, as UiTween.Speed does.
//
// After destroy() pending promises never resolve: a destroyed scene's flow stops where it stands and
// never reaches the source again.
import { createClock, EASE } from '../common/stage.js';
import { BattleSpeed, BattleSpeedStep } from '../../bridge/depictionScript/BattleSpeed.js';
import { L } from './layout.js';

/** settings.speed (1 | 1.25 | 1.5) → BattleSpeedStep. Anything else reads as the default (×1.25). */
export function stepOfSpeed(speed) {
  if (speed === 1 || speed === '1' || speed === '1.0') return BattleSpeedStep.Slow;
  if (speed === 1.5 || speed === '1.5') return BattleSpeedStep.Fast;
  if (speed === 1.25 || speed === '1.25') return BattleSpeedStep.Normal;
  return BattleSpeed.Default;
}

/** BattleSpeedStep → the number settings.speed keeps (1 | 1.25 | 1.5). */
export function speedOfStep(step) {
  return step === BattleSpeedStep.Slow ? 1 : step === BattleSpeedStep.Fast ? 1.5 : 1.25;
}

function easeOf(ease) {
  return typeof ease === 'function' ? ease : EASE[ease] || EASE.out;
}

/**
 * @param settings the app's settings object (speed, shortFx), read live every frame.
 * @returns {{ clock, real, destroy() }}
 */
export function createBattleClock(settings) {
  let sceneNow = 0;
  let alive = true;
  let ff = false;
  let halted = false;
  let paused = false;
  let scale = 1;
  let tweens = [];
  let runs = [];
  let timers = [];
  const frameFns = new Set();

  function computeScale() {
    // An overlay over the battle (the journal, the settings) holds the scene still (scene addition).
    if (paused) return 0;
    let s = BattleSpeed.TweenSpeed(stepOfSpeed(settings && settings.speed));
    if (halted || (settings && settings.shortFx)) s *= L.SHORT_SCALE;
    else if (ff) s *= L.FF_SCALE;
    return s;
  }
  /** The factor a paced run (runPaced) advances at: the scene's, but under fast-forward at most L.FF_FLOAT_SCALE × the speed. */
  function pacedScale() {
    if (paused) return 0;
    if (!ff || halted || (settings && settings.shortFx)) return scale;
    return Math.min(scale, BattleSpeed.TweenSpeed(stepOfSpeed(settings && settings.speed)) * L.FF_FLOAT_SCALE);
  }
  let paced = [];

  function step(dtReal) {
    scale = computeScale();
    const dt = dtReal * scale;
    sceneNow += dt;
    const dtPaced = dtReal * pacedScale();
    const done = [];
    // Review 19 (a 払's motes, started inside its own run, stayed on the screen for good): a tween, run or wait started
    // from inside another's callback this frame is kept — each list is taken whole, and what was added meanwhile is
    // put back after the survivors.
    const curTweens = tweens; tweens = [];
    const leftTweens = curTweens.filter((tw) => {
      if (sceneNow < tw.t0) return true;
      if (!tw.from) {
        tw.from = {};
        for (const k in tw.to) tw.from[k] = tw.obj[k];
      }
      const p = tw.ms > 0 ? Math.min(1, (sceneNow - tw.t0) / tw.ms) : 1;
      const e = tw.ease(p);
      for (const k in tw.to) tw.obj[k] = tw.from[k] + (tw.to[k] - tw.from[k]) * e;
      if (p >= 1) { done.push(tw.res); return false; }
      return true;
    });
    tweens = leftTweens.concat(tweens);
    const curRuns = runs; runs = [];
    const leftRuns = curRuns.filter((r) => {
      const p = r.ms > 0 ? Math.min(1, (sceneNow - r.t0) / r.ms) : 1;
      try { r.fn(r.ease(p), p); } catch (err) { console.error(err); }
      if (p >= 1) { done.push(r.res); return false; }
      return true;
    });
    runs = leftRuns.concat(runs);
    const curTimers = timers; timers = [];
    timers = curTimers.filter((tm) => (sceneNow >= tm.at ? (done.push(tm.res), false) : true)).concat(timers);
    const curPaced = paced; paced = [];
    const leftPaced = curPaced.filter((r) => {
      r.elapsed += dtPaced;
      const p = r.ms > 0 ? Math.min(1, r.elapsed / r.ms) : 1;
      try { r.fn(r.ease(p), p); } catch (err) { console.error(err); }
      if (p >= 1) { done.push(r.res); return false; }
      return true;
    });
    paced = leftPaced.concat(paced);
    done.forEach((r) => r());
    frameFns.forEach((fn) => {
      try { fn(dt, dtReal); } catch (err) { console.error(err); }
    });
  }

  const base = createClock((dtReal) => { if (alive) step(dtReal); });

  const clock = {
    /** Scene time in nominal ms. */
    get now() { return sceneNow; },
    /** The factor scene time runs at now. */
    get scale() { return scale; },
    get fastForward() { return ff; },
    /** Tween numeric properties of obj to `to` over `ms` nominal ms. Replaces running tweens of the same keys. */
    tween(obj, to, ms, ease, delay) {
      if (!alive) return new Promise(() => {});
      return new Promise((res) => {
        const keys = Object.keys(to);
        tweens = tweens.filter((tw) => {
          if (tw.obj !== obj) return true;
          keys.forEach((k) => delete tw.to[k]);
          if (!Object.keys(tw.to).length) { tw.res(); return false; }
          return true;
        });
        tweens.push({ obj, to: { ...to }, from: null, ms: Math.max(0, ms || 0), ease: easeOf(ease), t0: sceneNow + (delay || 0), res });
      });
    },
    /** UiTween.Run: fn(easedT, rawT) every frame until `ms` nominal ms have passed; fn(1, 1) last. */
    run(ms, ease, fn) {
      if (!alive) return new Promise(() => {});
      ms = Math.max(0, ms || 0);
      if (ms === 0) {
        try { fn(1, 1); } catch (err) { console.error(err); }
        return Promise.resolve();
      }
      return new Promise((res) => runs.push({ ms, ease: easeOf(ease), fn, t0: sceneNow, res }));
    },
    /**
     * A run that fast-forward speeds up less (review 16: at ×1000 a damage number lived two frames): as run(), but
     * while fast-forward is on it advances at most L.FF_FLOAT_SCALE × the battle speed. For what stands beside the
     * flow and never holds it — the floating numbers and words.
     */
    runPaced(ms, ease, fn) {
      if (!alive) return new Promise(() => {});
      ms = Math.max(0, ms || 0);
      if (ms === 0 || (settings && settings.shortFx) || halted) return clock.run(ms, ease, fn);
      return new Promise((res) => paced.push({ ms, ease: easeOf(ease), fn, elapsed: 0, res }));
    },
    /** UiTween.Wait: resolves after `ms` nominal ms of scene time. */
    wait(ms) {
      if (!alive) return new Promise(() => {});
      ms = Math.max(0, ms || 0);
      if (ms === 0) return Promise.resolve();
      return new Promise((res) => timers.push({ at: sceneNow + ms, res }));
    },
    /** fn(dtScene, dtReal) every frame. Returns an unsubscribe function. */
    onFrame(fn) {
      frameFns.add(fn);
      return () => frameFns.delete(fn);
    },
    setFastForward(on) { ff = !!on; },
    setHalted(on) { halted = !!on; },
    /** Holds scene time still (0 scale) while an overlay covers the battle; real time runs on. */
    setPaused(on) { paused = !!on; },
    get paused() { return paused; },
  };

  const real = {
    get now() { return base.now; },
    /** Unscaled wait in real ms (input timings, the refusal line's 3.5 s). */
    wait(ms) {
      if (!alive) return new Promise(() => {});
      return base.wait(Math.max(0, ms || 0)).then(() => (alive ? undefined : new Promise(() => {})));
    },
  };

  return {
    clock,
    real,
    destroy() {
      alive = false;
      tweens = [];
      runs = [];
      timers = [];
      paced = [];
      frameFns.clear();
      base.stop();
    },
  };
}
