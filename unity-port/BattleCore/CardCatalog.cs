using System;
using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>
    /// Card data, written as records from card_document/swordsman_cards_v4.md. The numbers are the
    /// canon's and are not derived here (the 素直 +2 is already inside 23 and 12).
    ///
    /// The slice carries ten of the eighty (#71). #51 widens <see cref="All"/> to the rest without
    /// changing the record shape.
    /// </summary>
    public static class CardCatalog
    {
        private static Trait At(Position side, TraitEffect effect, int amount = 0) =>
            new Trait(TraitCondition.SelfPosition, effect, amount, ConditionPosition: side);

        private static Trait Reserve(int threshold, TraitEffect effect, int amount) =>
            new Trait(TraitCondition.Reserve, effect, amount, Threshold: threshold);

        // ---- Attack ----

        /// <summary>#1. The plain column-3 attack: 21 on the ruler plus the 素直 +2.</summary>
        public static readonly CardDef Thrust = new CardDef(
            "thrust", "突き", BattleAttribute.Attack, 3,
            new Face(Power: 23),
            Description: "まっすぐ突く");

        /// <summary>#2. Pays for standing near.</summary>
        public static readonly CardDef KesaCut = new CardDef(
            "kesa_cut", "袈裟斬り", BattleAttribute.Attack, 2,
            new Face(Power: 13),
            At(Position.Near, TraitEffect.PowerBonus, 5),
            Description: "肩口から斬り下ろす");

        /// <summary>#10. Pays for standing far — the side the polearm punishes.</summary>
        public static readonly CardDef ReachThrust = new CardDef(
            "reach_thrust", "伸び突き", BattleAttribute.Attack, 2,
            new Face(Power: 13),
            At(Position.Far, TraitEffect.PowerBonus, 5),
            Description: "腕を伸ばし切って突く");

        /// <summary>#33. The column-1 attack and the one card that applies 鈍足.</summary>
        public static readonly CardDef BodyCheck = new CardDef(
            "body_check", "体当たり", BattleAttribute.Attack | BattleAttribute.Skill, 1,
            new Face(Power: 4, Status: StatusKind.Slow, StatusStacks: Constants.StatusApplyDefault),
            At(Position.Near, TraitEffect.HeavyBlow),
            Description: "近間で身体ごとぶつかる");

        // ---- Guard ----

        /// <summary>#11. The single-attribute Guard; rewards holding stamina back.</summary>
        public static readonly CardDef Brace = new CardDef(
            "brace", "呼吸を整える", BattleAttribute.Guard, 2,
            new Face(Guard: 9),
            Reserve(6, TraitEffect.NextTurnRecovery, 1),
            TargetKind.Self,
            "息を整えて受けに備える");

        /// <summary>#51. Attack and Guard in one card, better near.</summary>
        public static readonly CardDef ShieldBash = new CardDef(
            "shield_bash", "盾打ち", BattleAttribute.Attack | BattleAttribute.Guard, 2,
            new Face(Power: 8, Guard: 6),
            At(Position.Near, TraitEffect.GuardBonus, 3),
            Description: "固めた盾で打つ");

        // ---- Switching sides ----

        /// <summary>#30. Hit, then step out to far.</summary>
        public static readonly CardDef Feint = new CardDef(
            "feint", "牽制", BattleAttribute.Attack | BattleAttribute.Move, 2,
            new Face(Power: 8, MoveTo: Position.Far),
            Reserve(4, TraitEffect.GuardBonus, 3),
            Description: "牽制して退く");

        /// <summary>#31. The answer to being shoved far: power is read before the move, so it lands at 20.</summary>
        public static readonly CardDef BoarRush = new CardDef(
            "boar_rush", "猪突猛進", BattleAttribute.Attack | BattleAttribute.Move, 3,
            new Face(Power: 14, MoveTo: Position.Near),
            At(Position.Far, TraitEffect.PowerBonus, 6),
            Description: "遠くから一気に駆けて斬る");

        /// <summary>#37. Guard while closing in: 10 on the ruler plus the 素直 +2.</summary>
        public static readonly CardDef StepInGuard = new CardDef(
            "step_in_guard", "足捌き・前", BattleAttribute.Guard | BattleAttribute.Move, 3,
            new Face(Guard: 12, MoveTo: Position.Near),
            Targets: TargetKind.Self,
            Description: "受けながら詰める");

        /// <summary>#38. Guard while backing off.</summary>
        public static readonly CardDef StepOutGuard = new CardDef(
            "step_out_guard", "足捌き・後", BattleAttribute.Guard | BattleAttribute.Move, 3,
            new Face(Guard: 12, MoveTo: Position.Far),
            Targets: TargetKind.Self,
            Description: "受けながら退く");

        /// <summary>Every card the core knows, in canon order (#1 first). Ten for the slice; eighty after #51.</summary>
        public static readonly IReadOnlyList<CardDef> All = new[]
        {
            Thrust, KesaCut, ReachThrust, Brace, Feint, BoarRush, BodyCheck, StepInGuard, StepOutGuard, ShieldBash,
        };

        public static CardDef ById(string id)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            foreach (var def in All)
            {
                if (string.Equals(def.Id, id, StringComparison.Ordinal)) return def;
            }
            throw new KeyNotFoundException($"Unknown card id \"{id}\".");
        }
    }

    /// <summary>
    /// The player deck the slice fights the polearm with: ten kinds × 2 = 20 cards, the floor of §8.
    /// Why these ten is written on #71 and in the PR that added this file.
    /// </summary>
    public static class PrototypeDeck
    {
        public const int Copies = 2;

        public static IReadOnlyList<CardDef> Kinds => CardCatalog.All;

        /// <summary>Laid out in canon order and not shuffled; the turn loop shuffles with its own RNG.</summary>
        public static List<CardInstance> Build() => Cards.BuildDeck(Kinds, Copies);
    }
}
