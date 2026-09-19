using System;
using System.Collections.Generic;

namespace BattleCore
{
    public sealed record DrawResult(
        IReadOnlyList<CardInstance> Hand,
        IReadOnlyList<CardInstance> DrawPile,
        IReadOnlyList<CardInstance> DiscardPile);

    public static class Cards
    {
        private static Tier T(int power = 0, int guard = 0, int heal = 0, int shift = 0, int brk = 0)
            => new Tier(power, guard, heal, shift, brk);

        private static readonly Tier None = new Tier();

        /// <summary>Swordsman starter set (§7.2). Tiers[0..3] = T0..T3; T0 of a MinInvest 1 card is unused.</summary>
        public static readonly IReadOnlyList<CardDef> CardDefs = new[]
        {
            new CardDef(CardDefId.Thrust, "突き", CardType.Attack, RangeBand.Close, 1,
                new[] { None, T(5), T(8), T(11) }, null, "近接最大火力。"),
            new CardDef(CardDefId.Lunge, "踏み込み斬り", CardType.Attack, RangeBand.Close, 1,
                new[] { None, T(3, shift: -1), T(5, shift: -1), T(7, shift: -1, brk: 1) }, null, "斬りつつ詰める。最適間合いで T3 なら崩す。"),
            new CardDef(CardDefId.Feint, "牽制", CardType.Attack, RangeBand.Mid, 0,
                new[] { T(1, shift: 1), T(3, shift: 1), T(4, shift: 1), T(5, guard: 1, shift: 1) }, null, "削りつつ退く。"),
            new CardDef(CardDefId.StepIn, "足捌き・前", CardType.Move, null, 0,
                new[] { T(shift: -1), T(guard: 1, shift: -1), T(guard: 2, shift: -1), T(guard: 3, shift: -1) }, null, "詰める。投入した分だけ Guard が付く。"),
            new CardDef(CardDefId.StepOut, "足捌き・後", CardType.Move, null, 0,
                new[] { T(shift: 1), T(guard: 1, shift: 1), T(guard: 2, shift: 1), T(guard: 3, shift: 1) }, null, "退く。投入した分だけ Guard が付く。"),
            new CardDef(CardDefId.Brace, "呼吸を整える", CardType.Guard, null, 0,
                new[] { T(guard: 2), T(guard: 4), T(guard: 6), T(guard: 8) },
                new ReserveRule(ReserveKind.Calm, 6, 1), "受けを固める。残 6 以上なら次ターン回復 +1。"),
            new CardDef(CardDefId.FirstAid, "応急処置", CardType.Heal, null, 1,
                new[] { None, T(heal: 3), T(heal: 5), T(heal: 7) }, null, "傷を塞ぐ。"),
        };

        /// <summary>The 12-card test-bench deck (6 kinds × 2). FirstAid is defined but not dealt (§7.2).</summary>
        private static readonly CardDefId[] DeckKinds =
        {
            CardDefId.Thrust, CardDefId.Lunge, CardDefId.Feint,
            CardDefId.StepIn, CardDefId.StepOut, CardDefId.Brace,
        };

        private const int DeckCopies = 2;

        public static CardDef Def(CardDefId id)
        {
            foreach (var def in CardDefs)
            {
                if (def.Id == id) return def;
            }
            throw new ArgumentOutOfRangeException(nameof(id), id, null);
        }

        public static List<CardInstance> CreateInitialDeck()
        {
            var deck = new List<CardInstance>();
            foreach (var id in DeckKinds)
            {
                var def = Def(id);
                for (int copy = 0; copy < DeckCopies; copy++)
                {
                    deck.Add(new CardInstance($"{id.ToToken()}-{copy}", def));
                }
            }
            return deck;
        }

        public static List<T> Shuffle<T>(IReadOnlyList<T> input, IRng rng)
        {
            var arr = new List<T>(input);
            for (int i = arr.Count - 1; i > 0; i--)
            {
                int j = (int)Math.Floor(rng.NextDouble() * (i + 1));
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
            return arr;
        }

        public static DrawResult DrawToHandSize(
            IReadOnlyList<CardInstance> drawPile,
            IReadOnlyList<CardInstance> discardPile,
            IReadOnlyList<CardInstance> hand,
            int target,
            IRng rng)
        {
            var draw = new List<CardInstance>(drawPile);
            var discard = new List<CardInstance>(discardPile);
            var newHand = new List<CardInstance>(hand);
            while (newHand.Count < target)
            {
                if (draw.Count == 0)
                {
                    if (discard.Count == 0) break;
                    draw = Shuffle(discard, rng);
                    discard = new List<CardInstance>();
                }
                var next = draw[0];
                draw.RemoveAt(0);
                newHand.Add(next);
            }
            return new DrawResult(newHand, draw, discard);
        }
    }
}
