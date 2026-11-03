namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Polyfill so `init` accessors compile on netstandard2.1 (ExplorationScreen, NodePanel; #597).
    /// DungeonCore and BattleCore carry their own copies; all are internal, so they never collide.
    /// </summary>
    internal static class IsExternalInit
    {
    }
}
