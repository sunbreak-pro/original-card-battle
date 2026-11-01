// The battle scene's frame (battle-flow §3.1, battle-views §5): one MonoBehaviour on the scene root, after the
// EventSystem (its button clicks of the frame have run), in this order every frame —
//   1. the microtask queue (continuations of the frame's uGUI handlers),
//   2. the keys (KeyStack.Pump: the scene's pushKeys handler, keyup; a no-op when the stage pumped them already),
//   3. the pointer (BattleInput.PumpPointer: move / leave, down, up, contextmenu, each listener followed by a drain),
//   4. the clock (BattleClock.Step with the unscaled frame ms: tweens, runs, waits, then every OnFrame in the order the
//      modules subscribed — backdrop, field, free step, hud, hand, player's follow / floor reach — then a drain).
// The stage px of the pointer and the game's own Time.timeScale do not touch the battle (unscaled time).
using Trial.Kit;
using UnityEngine;

namespace Trial.Battle
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class BattleSceneDriver : MonoBehaviour
    {
        /// <summary>The scene it drives (null once the scene is destroyed).</summary>
        internal BattleSceneHandle Scene;

        private void Update()
        {
            BattleSceneHandle s = Scene;
            if (s == null || s.Destroyed) return;
            s.Frame(Time.unscaledDeltaTime * 1000f);
        }

        /// <summary>window blur: KeyStack.Blurred reaches the hand and the scene (the stage reports it too; once a frame).</summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && Scene != null && !Scene.Destroyed) KeyStack.Blur();
        }

        /// <summary>visibilitychange: the app hidden.</summary>
        private void OnApplicationPause(bool paused)
        {
            BattleSceneHandle s = Scene;
            if (s == null || s.Destroyed) return;
            if (paused) KeyStack.Blur();
            s.Input?.OnVisibility(paused);
        }

        /// <summary>The root went without the app's destroy(): the scene is torn down here (its root is already going).</summary>
        private void OnDestroy()
        {
            BattleSceneHandle s = Scene;
            Scene = null;
            if (s == null || s.Destroyed) return;
            s.RootDying = true;
            s.Destroy();
        }
    }
}
