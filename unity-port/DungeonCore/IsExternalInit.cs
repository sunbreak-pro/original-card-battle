namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Polyfill so `record` and `init` accessors compile on netstandard2.1.
    /// BattleCore carries its own copy; DungeonCore does not reference BattleCore,
    /// so it needs its own. Both are internal, so the two never collide.
    /// </summary>
    internal static class IsExternalInit
    {
    }
}
