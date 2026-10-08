namespace BattleCore.Sim;

/// <summary>
/// §13's positioning policies (間合いの方針). Each is a bias added to the greedy value of a play that
/// changes N to the nearest enemy; none forbids a card outright except <see cref="KeepTwo"/>, which
/// refuses to close inside 2 on its own.
/// </summary>
public enum Positioning
{
    /// <summary>
    /// No bias beyond what a move does now (the omen it dodges, the cards it brings into reach), and
    /// a small pull in from N 3 or more (<see cref="GreedyPlayer.ApproachValue"/>).
    /// </summary>
    Neutral,

    /// <summary>詰め続ける: every cell closer is worth <see cref="GreedyPlayer.CellBias"/>, every cell away costs it.</summary>
    Close,

    /// <summary>離れ続ける: the mirror of Close.</summary>
    Away,

    /// <summary>出入りする: Close until an attack has been played this turn, Away after it (step in, strike, step out).</summary>
    InOut,

    /// <summary>
    /// 間合い 2 以上に居続ける (§13 離れて削る型): a play that closes N to below 2 costs
    /// <see cref="GreedyPlayer.KeepTwoPenalty"/> — from 2 or more, and from 1 to 0 as well; a play that
    /// widens N from below 2 earns CellBias a cell.
    /// </summary>
    KeepTwo,
}

/// <summary>One play the player chose: the card instance, the enemy it is aimed at, and its value.</summary>
public sealed record Choice(string InstanceId, int Target, double Value);

/// <summary>
/// The bench's player (§13: 「予兆に対して最も期待値の高い 1〜2 枚を選ぶ」貪欲法). The exact rule:
///
/// 1. At the turn start, if 俊敏 offers a free step, the step (forward, back, or none) with the
///    highest value is taken when that value is above 0.
/// 2. Every card in the hand that <see cref="TurnLoop.CanPlay"/> allows, against every standing enemy
///    it can aim at, is played on a copy of the board. Its value is <see cref="Value"/> after minus
///    before, plus <see cref="DrawValue"/> per card it draws, plus the reach and positioning terms.
/// 3. The highest value is played if it is above 0, and step 2 runs again on the new board. The turn
///    ends when no play is worth more than 0 (or none is allowed), or once <see cref="MaxPlays"/>
///    cards have been played this turn when a cap is set. Uncapped (the default), the player plays
///    as many cards as stay worth their stamina — usually 1〜3.
///
/// A card the play draws is valued by count only (<see cref="DrawValue"/> each): the copy draws the
/// real next cards of the shuffled pile, so reading them would let the player see its future draws.
/// <see cref="ReachDelta"/> and the draw count therefore look only at the cards that were in the hand
/// before the play.
///
/// <see cref="Value"/> reads the board the way the omen says the turn will end: the enemies' HP, the
/// HP the shown omens would take off if the turn ended now (<see cref="TurnLoop.PreviewOmen"/>, which
/// counts the Guard and the 構え the player would hold), the statuses and stances at fixed worths, and
/// the stamina that would still be there next turn. Ties go to the earlier card in the hand. Nothing is
/// random: a copy is resolved with its own fixed seed, so the main RNG is never drawn from.
/// </summary>
public sealed class GreedyPlayer
{
    // Worths, in HP. A stamina point carried into next turn is worth StaminaValue, up to the max less
    // the 3 recovered (the rest would be lost to the cap). The status worths are a rough HP each.
    public const double WinValue = 10_000;
    public const double KillBonus = 15;
    public const double StaminaValue = 2;
    public const double EnemyStaminaValue = 1;
    public const double DrawValue = 2;
    public const double StanceValue = 10;
    public const double FollowUpValue = 0.6;
    public const double ReachValue = 2.5;
    public const double CellBias = 6;

    /// <summary>
    /// Neutral's one positional term: a cell closed from N 3 or more (§7.2: 「届く札がほぼ無い、仕切り直しの
    /// 距離」) is worth this much, so a player facing an enemy that keeps away still walks in.
    /// </summary>
    public const double ApproachValue = 2;

    public const int FarGap = 3;
    public const double KeepTwoPenalty = 50;

    /// <summary>The seed every look-ahead copy is resolved with (draws on the copy only).</summary>
    private const int LookSeed = 0;

    public Positioning Positioning { get; }

    /// <summary>
    /// The most cards played in one turn; 0 for no cap (the default). §13 words the player as one that
    /// picks 「最も期待値の高い 1〜2 枚」; --max-plays 2 reads that literally.
    /// </summary>
    public int MaxPlays { get; }

    public GreedyPlayer(Positioning positioning = Positioning.Neutral, int maxPlays = 0)
    {
        if (maxPlays < 0) throw new ArgumentOutOfRangeException(nameof(maxPlays), "0 (no cap) or more");
        Positioning = positioning;
        MaxPlays = maxPlays;
    }

    /// <summary>The free step to take now (+1 forward, −1 back), or 0 for none.</summary>
    public int ChooseFreeStep(BattleState state)
    {
        if (!TurnLoop.CanTakeFreeStep(state)) return 0;
        double before = Value(state);
        int bestDirection = 0;
        double best = 0;
        foreach (int direction in new[] { 1, -1 })
        {
            var after = TurnLoop.TakeFreeStep(state, direction).State;
            double value = Value(after) - before + ReachDelta(state, after, HeldIds(state, null)) + PositionBias(state, after);
            if (value > best)
            {
                best = value;
                bestDirection = direction;
            }
        }
        return bestDirection;
    }

    /// <summary>
    /// The best play worth more than 0, or null to end the turn. <paramref name="playedThisTurn"/> is
    /// how many cards the player has already played this turn, read against <see cref="MaxPlays"/>.
    /// </summary>
    public Choice? Choose(BattleState state, int playedThisTurn = 0)
    {
        if (state.Result != GameResult.Ongoing || state.Phase != BattlePhase.PlayerAction) return null;
        if (MaxPlays > 0 && playedThisTurn >= MaxPlays) return null;
        double before = Value(state);
        Choice? best = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var card in state.Hand)
        {
            // Copies of one kind are the same play; the first in the hand stands for them.
            if (!seen.Add(card.Def.Id)) continue;
            foreach (int target in TargetsFor(state, card.Def))
            {
                if (TurnLoop.CanPlay(state, card.InstanceId, target) != PlayRefusal.None) continue;
                double value = Score(state, card, target, before);
                if (value > 0 && (best == null || value > best.Value)) best = new Choice(card.InstanceId, target, value);
            }
        }
        return best;
    }

    /// <summary>
    /// What playing the card at the target is worth, from a copy of the board. The copy draws the real
    /// next cards of the pile, so only the cards held before the play are read: each card drawn is
    /// worth <see cref="DrawValue"/> whatever it is, and the value never depends on the order of the
    /// draw pile.
    /// </summary>
    public double Score(BattleState state, CardInstance card, int target, double valueBefore)
    {
        var after = TurnLoop.PlayCard(state, card.InstanceId, new SeededRng(LookSeed), target).State;
        var held = HeldIds(state, card);
        int drawn = after.Hand.Count(c => !held.Contains(c.InstanceId));
        return Value(after) - valueBefore + DrawValue * drawn + ReachDelta(state, after, held) + PositionBias(state, after);
    }

    /// <summary>The instance ids in the hand now, but <paramref name="played"/>: the cards the player already knows.</summary>
    public static HashSet<string> HeldIds(BattleState state, CardInstance? played)
    {
        var held = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in state.Hand)
        {
            if (played == null || c.InstanceId != played.InstanceId) held.Add(c.InstanceId);
        }
        return held;
    }

    /// <summary>
    /// The enemies a card can be aimed at: each standing enemy for a card aimed at one opponent, else
    /// the nearest (a card aimed at all or at the player reads that one and ignores the pick).
    /// </summary>
    public static IEnumerable<int> TargetsFor(BattleState state, CardDef def)
    {
        if (EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets) && def.Targets == TargetKind.One)
        {
            return state.Living;
        }
        int nearest = state.Nearest;
        return nearest < 0 ? Array.Empty<int>() : new[] { nearest };
    }

    /// <summary>
    /// How good the board is for the player if the turn ended now. Fixed worths (HP units): see the
    /// constants. Only the shown omens are read: an elite's or boss's second action is not counted.
    /// </summary>
    public static double Value(BattleState state)
    {
        if (state.Result == GameResult.Won) return WinValue;
        if (state.Result == GameResult.Lost) return -WinValue;

        var player = state.Player;
        double value = player.Hp - Incoming(state);
        for (int i = 0; i < state.Enemies.Count; i++)
        {
            var enemy = state.Enemies[i];
            if (!enemy.Alive)
            {
                value += KillBonus;
                continue;
            }
            value -= enemy.Body.Hp;
            value += FoeStatusValue(enemy.Body.Statuses);
            value -= EnemyStaminaValue * enemy.Body.Stamina;
        }
        value += SelfStatusValue(player.Statuses);
        value += StanceValue * player.StanceList.Count;
        int carried = player.Stamina + player.NextTurnRecoveryBonus;
        value += StaminaValue * Math.Clamp(carried, 0, Math.Max(0, player.MaxStamina - Constants.StaminaRecovery));
        value += FollowUpValue * player.FollowUp;
        return value;
    }

    /// <summary>The HP the shown omens would take off the player if the turn ended now (0 for a rest or a whiff).</summary>
    public static int Incoming(BattleState state)
    {
        if (state.Result != GameResult.Ongoing || state.Phase != BattlePhase.PlayerAction) return 0;
        int total = 0;
        foreach (int i in state.Living)
        {
            var omen = TurnLoop.PreviewOmen(state, i);
            if (omen == null || omen.Rests || !omen.Lands) continue;
            total += omen.Damage;
        }
        return total;
    }

    /// <summary>出血 s ticks 2s, 2(s−1), … 2 over its turns: s(s+1) in all.</summary>
    private static double Ticks(int stacks) => stacks * (stacks + 1);

    public static double FoeStatusValue(StatusSet s) =>
        Ticks(s.Stacks(StatusKind.Bleed))
        + 5 * Math.Min(s.Stacks(StatusKind.Fragile), 3)
        + 3 * Math.Min(s.Stacks(StatusKind.Intimidate), 3)
        + 1 * Math.Min(s.Stacks(StatusKind.Slow), 4)
        + 1.5 * Math.Min(s.Stacks(StatusKind.Fatigue), 4);

    public static double SelfStatusValue(StatusSet s) =>
        5 * Math.Min(s.Stacks(StatusKind.Empower), 3)
        + 6 * Math.Min(s.Stacks(StatusKind.Focus), 2)
        + 2 * Math.Min(s.Stacks(StatusKind.Parry), 2)
        + 0.5 * Ticks(s.Stacks(StatusKind.Regen))
        + 1 * Math.Min(s.Stacks(StatusKind.Swift), 2);

    /// <summary>
    /// <see cref="ReachValue"/> for each card of <paramref name="held"/> (the hand before the play, the
    /// played card left out) that aims at an opponent and has one in reach, after the play against
    /// before it. A card the play drew is not in <paramref name="held"/>, so it never counts. This is
    /// what makes a step in worth taking when it lets the rest of the hand land.
    /// </summary>
    public static double ReachDelta(BattleState before, BattleState after, IReadOnlySet<string> held)
    {
        if (after.Result != GameResult.Ongoing) return 0;
        return ReachValue * (InReach(after, held) - InReach(before, held));
    }

    /// <summary>How many hand cards among <paramref name="held"/> aim at an opponent that stands in their reach.</summary>
    public static int InReach(BattleState state, IReadOnlySet<string> held)
    {
        int count = 0;
        foreach (var card in state.Hand)
        {
            if (!held.Contains(card.InstanceId)) continue;
            if (AimsAndReaches(state, card.Def)) count++;
        }
        return count;
    }

    /// <summary>Whether the card aims at an opponent and one standing enemy is inside its reach.</summary>
    public static bool AimsAndReaches(BattleState state, CardDef def)
    {
        if (!EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets)) return false;
        var reach = def.Face.ReachOrDefault;
        foreach (int i in state.Living)
        {
            if (reach.Contains(state.GapTo(i))) return true;
        }
        return false;
    }

    /// <summary>The policy's bias for the change in N to the nearest standing enemy (see <see cref="Positioning"/>).</summary>
    public double PositionBias(BattleState before, BattleState after)
    {
        if (after.Result != GameResult.Ongoing || before.Nearest < 0 || after.Nearest < 0) return 0;
        int gapBefore = before.GapTo(before.Nearest);
        int gapAfter = after.GapTo(after.Nearest);
        int closer = gapBefore - gapAfter;
        switch (Positioning)
        {
            case Positioning.Neutral:
                return gapBefore >= FarGap && closer > 0 ? ApproachValue * closer : 0;
            case Positioning.Close:
                return CellBias * closer;
            case Positioning.Away:
                return -CellBias * closer;
            case Positioning.InOut:
                bool struck = before.Player.PlayedThisTurn.Contains(BattleAttribute.Attack);
                return struck ? -CellBias * closer : CellBias * closer;
            case Positioning.KeepTwo:
                if (gapAfter < 2 && gapAfter < gapBefore) return -KeepTwoPenalty;
                if (gapBefore < 2 && gapAfter > gapBefore) return CellBias * (gapAfter - gapBefore);
                return 0;
            default:
                return 0;
        }
    }
}
