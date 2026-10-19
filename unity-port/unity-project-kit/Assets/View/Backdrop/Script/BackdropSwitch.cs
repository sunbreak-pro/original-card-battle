// Which layers are on screen while the backdrop switches (#607): the one shown, the one fading out under
// it, and how far the 400 ms crossfade of battle_ui_ux_v2 §4.3 has gone. Pure C#, so `dotnet test` pins
// the switching that BackdropLayers draws.
namespace Backdrop
{
    public sealed class BackdropSwitch
    {
        /// <summary>What a call to <see cref="Show"/> asks the drawing side to do.</summary>
        public enum Change
        {
            /// <summary>The layer is already shown: draw nothing new.</summary>
            None,
            /// <summary>The first layer: draw it at full, with nothing to fade from.</summary>
            First,
            /// <summary>Draw the new layer at 0 over the shown one and fade it in.</summary>
            Fade,
            /// <summary>A third layer mid-fade: drop the one fading out, then fade as <see cref="Fade"/>.</summary>
            FadeDroppingOldest,
        }

        /// <summary>The layer drawn on top (1〜7), or 0 before the first.</summary>
        public int Shown { get; private set; }

        /// <summary>The layer fading out under <see cref="Shown"/>, or 0 when none is.</summary>
        public int Leaving { get; private set; }

        private double _fadeStartMs;

        /// <summary>Turns to <paramref name="layer"/> (held to 1〜7) at <paramref name="nowMs"/>.</summary>
        public Change Show(int layer, double nowMs)
        {
            int next = BackdropSpec.Clamp(layer);
            if (next == Shown) return Change.None;
            Change change = Shown == 0 ? Change.First : Leaving != 0 ? Change.FadeDroppingOldest : Change.Fade;
            Leaving = Shown;
            Shown = next;
            _fadeStartMs = nowMs;
            return change;
        }

        /// <summary>
        /// The shown layer's alpha at <paramref name="nowMs"/>. When it reaches 1 the fade is over and
        /// <see cref="Leaving"/> goes back to 0, so the drawing side can drop the layer under it.
        /// </summary>
        public float Tick(double nowMs)
        {
            if (Leaving == 0) return 1f;
            float t = BackdropMotion.Crossfade(nowMs - _fadeStartMs);
            if (t >= 1f) Leaving = 0;
            return t;
        }
    }
}
