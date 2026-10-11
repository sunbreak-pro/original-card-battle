// What the screen does on the enemy's turn below the throw line (battle-visual-v1 §5.5, #455). Pure C#,
// so `dotnet test` holds the same numbers the screen draws.
namespace Depiction
{
    public static class EnemyTurnShadeLayout
    {
        /// <summary>§5.5: from y 790 down, the screen sinks under black.</summary>
        public const float Top = 790f;

        /// <summary>§5.5 黒 72% のグラデーション: clear at y 790, black 72% at the bottom edge.</summary>
        public const float TopAlpha = 0f;
        public const float BottomAlpha = 0.72f;

        /// <summary>§4.9 ターン終了: the plate stands at 40% while the enemy acts.</summary>
        public const float EndTurnAlpha = 0.4f;

        /// <summary>How tall the shade is on the 1080 screen.</summary>
        public static float Height => 1080f - Top;
    }
}
