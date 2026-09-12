using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>
    /// The contract a battle View implements. In Unity this is a UGUI MonoBehaviour;
    /// it stays pure C# here so a fake view can exercise it under <c>dotnet test</c>.
    /// The View animates from <see cref="BattleViewModel.Events"/> (what just happened)
    /// and then shows the settled values (everything else).
    /// </summary>
    public interface IBattleView
    {
        void Render(BattleViewModel vm);
    }

    /// <summary>Flattened, render-ready projection of <see cref="BattleState"/> (battle_ui_ux_v1.md §1.1).</summary>
    public sealed record BattleViewModel(
        int Turn,
        int Floor,
        int TimeLimitLeft,
        int TimeLimitMax,
        int MiasmaPercent,
        int MiasmaDensity,
        int MiasmaPenalty,
        int Disclosure,
        int PlayerHp,
        int PlayerMaxHp,
        int PlayerStamina,
        int PlayerMaxStamina,
        int PlayerGuard,
        string EnemyName,
        int EnemyHp,
        int EnemyMaxHp,
        int EnemyStamina,
        int EnemyMaxStamina,
        int EnemyGuard,
        int DistanceIndex,
        string DistanceLabel,
        OmenView? Omen,
        IReadOnlyList<CardView> Hand,
        int DrawPileCount,
        int DiscardPileCount,
        IReadOnlyList<LogEntry> Log,
        IReadOnlyList<BattleEvent> Events,
        GameResult Result,
        bool BattleOver,
        bool ReserveWillTrigger,
        string ReservePreview,
        JournalView Journal)
    {
        public static BattleViewModel From(BattleState state) => new BattleViewModel(
            Turn: state.Turn,
            Floor: state.Init.Floor,
            TimeLimitLeft: state.Init.TimeLimitLeft,
            TimeLimitMax: state.Init.TimeLimitMax,
            MiasmaPercent: state.Init.MiasmaPercent,
            MiasmaDensity: state.Init.MiasmaDensity,
            MiasmaPenalty: Combat.MiasmaStaminaPenalty(state.Init.MiasmaPercent),
            Disclosure: state.Init.Disclosure,
            PlayerHp: state.PlayerHp,
            PlayerMaxHp: state.PlayerMaxHp,
            PlayerStamina: state.PlayerStamina,
            PlayerMaxStamina: state.PlayerMaxStamina,
            PlayerGuard: state.PlayerGuard,
            EnemyName: Enemy.Def.Name,
            EnemyHp: state.EnemyHp,
            EnemyMaxHp: state.EnemyMaxHp,
            EnemyStamina: state.EnemyStamina,
            EnemyMaxStamina: state.EnemyMaxStamina,
            EnemyGuard: state.EnemyGuard,
            DistanceIndex: state.DistanceIndex,
            DistanceLabel: ViewModel.DistanceLabel(state.DistanceIndex),
            Omen: state.Omen != null ? ViewModel.DescribeOmen(state.Omen, state.DistanceIndex, state.Init.Disclosure) : null,
            Hand: ViewModel.DescribeHand(state),
            DrawPileCount: state.DrawPile.Count,
            DiscardPileCount: state.DiscardPile.Count,
            Log: state.Log,
            Events: state.Events,
            Result: state.Result,
            BattleOver: ViewModel.IsBattleOver(state.Result),
            ReserveWillTrigger: Combat.ReserveGuard(state.PlayerStamina) > 0,
            ReservePreview: ViewModel.ReservePreview(state.PlayerStamina),
            Journal: ViewModel.DescribeJournal(state.Init.Disclosure));
    }
}
