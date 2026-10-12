// The light a unit's body keeps while it holds a stance (battle_ui_ux_v2 §5.11 順 2 / §5.3 型 StanceGlow,
// #501). Pure C#, so `dotnet test` holds when the light is on with the same rule the screen follows.
//
// The light is read off the frame alone: on while the unit's stance list (UnitFrame.Stances) holds at
// least one stance, off while it is empty. Since battle_core_v4 v4.4 (#454) a stance joins a list and
// is never replaced, so a second stance adds no second light (§5.11 順 2: 2 つ目からは光を重ねず) and
// there is no old light to fade out under a new one. The light goes out only when the list empties.
namespace Depiction
{
    /// <summary>What the screen does to a unit's stance light when a frame lands.</summary>
    public enum StanceGlowChange
    {
        /// <summary>The light stays as it is (on and still held, or off and still empty).</summary>
        Keep,
        /// <summary>The first stance was put down: the light fades in.</summary>
        Light,
        /// <summary>The last stance is gone: the light fades out.</summary>
        Fade,
    }

    public static class StanceGlow
    {
        /// <summary>§5.11 順 2: the light is the stance attribute's colour at α 0.25.</summary>
        public const float Alpha = 0.25f;

        /// <summary>The light is on while <paramref name="unit"/> holds at least one stance.</summary>
        public static bool IsOn(UnitFrame unit)
        {
            return unit != null && unit.Stances != null && unit.Stances.Count > 0;
        }

        /// <summary>What a light now <paramref name="shown"/> (or not) does when <paramref name="unit"/>'s frame lands.</summary>
        public static StanceGlowChange Change(bool shown, UnitFrame unit)
        {
            bool on = IsOn(unit);
            if (on == shown) return StanceGlowChange.Keep;
            return on ? StanceGlowChange.Light : StanceGlowChange.Fade;
        }
    }
}
