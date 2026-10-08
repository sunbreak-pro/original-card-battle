namespace BattleCore.Sim;

/// <summary>
/// The stacked single blow worked out on paper, the way battle_core_v4 §13 基準 15 and
/// swordsman_cards_v4 §5-15 do it: the card's face, plus 集中's step to the next column (§5), plus
/// 追撃 +5, times the one multiplier a blow may carry (×1.5, §5.1 S13), rounded away from zero.
/// WithTrait adds the card's own power trait on top (威力 +n, 重撃 +6, 転換's half Guard), which the
/// canon's example leaves out because its card (横薙ぎ) has none.
/// </summary>
public sealed record DeskBlow(string CardId, string CardName, int Face, int Focus, int FollowUp, int Trait, double Multiplier, int Canon, int WithTrait)
{
    public string Formula => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"({Face} + {Focus} + {FollowUp}) × {Multiplier} = {Canon}");
}

public static class DeskCalc
{
    /// <summary>The desk blow of one attack card (null for a card without an attack face).</summary>
    public static DeskBlow? Of(CardDef def)
    {
        if (!def.Attributes.HasFlag(BattleAttribute.Attack) || def.Face.Power <= 0) return null;
        var focus = FocusStep.Of(def.Attributes, def.Column, def.Face);
        int trait = 0;
        foreach (var t in def.AllTraits)
        {
            trait += t.Effect switch
            {
                TraitEffect.PowerBonus => t.Amount,
                TraitEffect.HeavyBlow => Constants.HeavyBlowPower,
                TraitEffect.Convert => (def.Face.Guard + focus.Guard + 1) / 2,
                _ => 0,
            };
        }
        double mult = Math.Max(Constants.EmpowerMult, Constants.FragileMult);
        int baseSum = def.Face.Power + focus.Power + Constants.FollowUpPower;
        int canon = (int)Math.Round(baseSum * mult, MidpointRounding.AwayFromZero);
        int withTrait = (int)Math.Round((baseSum + trait) * mult, MidpointRounding.AwayFromZero);
        return new DeskBlow(def.Id, def.Name, def.Face.Power, focus.Power, Constants.FollowUpPower, trait, mult, canon, withTrait);
    }

    /// <summary>The card with the highest canon desk blow (ties: the higher WithTrait, then canon order).</summary>
    public static DeskBlow Strongest(IEnumerable<CardDef> cards)
    {
        DeskBlow? best = null;
        foreach (var def in cards)
        {
            var blow = Of(def);
            if (blow == null) continue;
            if (best == null || blow.Canon > best.Canon || (blow.Canon == best.Canon && blow.WithTrait > best.WithTrait)) best = blow;
        }
        return best ?? throw new InvalidOperationException("No attack card to work the desk blow out from.");
    }

    /// <summary>The highest WithTrait over the cards (the card's own trait counted).</summary>
    public static DeskBlow StrongestWithTrait(IEnumerable<CardDef> cards)
    {
        DeskBlow? best = null;
        foreach (var def in cards)
        {
            var blow = Of(def);
            if (blow == null) continue;
            if (best == null || blow.WithTrait > best.WithTrait) best = blow;
        }
        return best ?? throw new InvalidOperationException("No attack card to work the desk blow out from.");
    }

    /// <summary>
    /// §13 基準 15: 「通常敵の HP の 6 割」 from the roster — the highest HP among the normal enemies
    /// (96 per enemy_roster_v4 §0), times 0.6, rounded down (the canon's 57).
    /// </summary>
    public static (int NormalMaxHp, int Limit) SingleHitLimit(IEnumerable<EnemyDef> enemies)
    {
        int hp = enemies.Where(e => e.Rank == EnemyRank.Normal).Max(e => e.MaxHp);
        return (hp, (int)Math.Floor(hp * 0.6));
    }

    /// <summary>
    /// The chance a hand of <paramref name="hand"/> drawn from <paramref name="deck"/> cards holds none
    /// of the <paramref name="marked"/> ones: C(deck − marked, hand) / C(deck, hand).
    /// </summary>
    public static double NoneInHand(int deck, int marked, int hand)
    {
        if (hand > deck) return 0;
        double p = 1;
        for (int i = 0; i < hand; i++) p *= (double)(deck - marked - i) / (deck - i);
        return Math.Max(0, p);
    }
}
