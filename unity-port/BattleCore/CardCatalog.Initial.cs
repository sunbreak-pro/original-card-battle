namespace BattleCore
{
    /// <summary>
    /// swordsman_cards_v4.md §2: the initial forty (#1-#40), in canon order. Every one of them
    /// changes its name with mastery; eight are 素直 (no trait, +2 already in the number).
    /// </summary>
    public static partial class CardCatalog
    {
        // ---- A ----

        /// <summary>#1. The plain column-3 attack: 21 on the ruler plus the 素直 +2.</summary>
        public static readonly CardDef Thrust = new CardDef(
            "thrust", "突き", BattleAttribute.Attack, 3,
            new Face(Power: 23),
            Description: "まっすぐ突く");

        /// <summary>#2. Pays +5 for standing adjacent.</summary>
        public static readonly CardDef KesaCut = new CardDef(
            "kesa_cut", "袈裟斬り", BattleAttribute.Attack, 2,
            new Face(Power: 13),
            AtMost(0, TraitEffect.PowerBonus, 5),
            Description: "肩口から斬り下ろす");

        /// <summary>#3. A heavy blow into an attack omen (a guard omen shows on about 5% of plays, #208).</summary>
        public static readonly CardDef Overhead = new CardDef(
            "overhead", "大上段", BattleAttribute.Attack, 2,
            new Face(Power: 13),
            OmenIs(OmenKind.Attack, TraitEffect.HeavyBlow),
            Description: "振りかぶって叩き落とす");

        /// <summary>#4. The light column-1 cut that hands back a stamina point when adjacent.</summary>
        public static readonly CardDef WristCut = new CardDef(
            "wrist_cut", "小手打ち", BattleAttribute.Attack, 1,
            new Face(Power: 6),
            AtMost(0, TraitEffect.StaminaGain, 1),
            Description: "手元を狙う軽い一撃");

        /// <summary>#5. The second plain column-3 attack, the same 23 as the thrust.</summary>
        public static readonly CardDef SideSweep = new CardDef(
            "side_sweep", "横薙ぎ", BattleAttribute.Attack, 3,
            new Face(Power: 23),
            Description: "水平に薙ぐ");

        /// <summary>#6. Breaks 1 stamina, and hits +5 harder at a slowed foe.</summary>
        public static readonly CardDef FlatStrike = new CardDef(
            "flat_strike", "峰打ち", BattleAttribute.Attack, 1,
            new Face(Power: 6, Break: 1),
            FoeHas(StatusKind.Slow, TraitEffect.PowerBonus, 5),
            Description: "足の止まった相手の体力を削る");

        /// <summary>#7. Reaches 0〜2 (so 11, not 13) and pays +3 into an attack omen.</summary>
        public static readonly CardDef ProbeThrust = new CardDef(
            "probe_thrust", "探り突き", BattleAttribute.Attack, 2,
            new Face(Power: 11, Reach: new Reach(0, 2)),
            OmenIs(OmenKind.Attack, TraitEffect.PowerBonus, 3),
            Description: "相手の出方を探る突き");

        /// <summary>#8. A column-1 heavy blow once the foe's stamina is broken.</summary>
        public static readonly CardDef Pierce = new CardDef(
            "pierce", "貫き", BattleAttribute.Attack, 1,
            new Face(Power: 6),
            When(TraitCondition.Broken, TraitEffect.HeavyBlow),
            Description: "崩れた相手を刺し貫く");

        /// <summary>#9. One of the four cards that reach 3 (so 9, not 13); draws when thrown from a gap of 2 or more.</summary>
        public static readonly CardDef ThrowBlade = new CardDef(
            "throw_blade", "投げ刃", BattleAttribute.Attack, 2,
            new Face(Power: 9, Reach: new Reach(1, 3)),
            AtLeast(2, TraitEffect.Draw, 1),
            Description: "小刀を投げる");

        /// <summary>#10. Reaches 1〜2 and pays +5 at a gap of 2 — where the polearm punishes.</summary>
        public static readonly CardDef ReachThrust = new CardDef(
            "reach_thrust", "伸び突き", BattleAttribute.Attack, 2,
            new Face(Power: 13, Reach: new Reach(1, 2)),
            AtLeast(2, TraitEffect.PowerBonus, 5),
            Description: "腕を伸ばし切って突く");

        // ---- G ----

        /// <summary>#11. The single-attribute Guard; rewards holding stamina back.</summary>
        public static readonly CardDef Brace = new CardDef(
            "brace", "呼吸を整える", BattleAttribute.Guard, 2,
            new Face(Guard: 9),
            Reserve(6, TraitEffect.NextTurnRecovery, 1),
            Targets: TargetKind.Self,
            Description: "息を整えて受けに備える");

        /// <summary>#12. The plain column-3 Guard: 15 on the ruler plus the 素直 +2.</summary>
        public static readonly CardDef IronBlock = new CardDef(
            "iron_block", "鉄の受け", BattleAttribute.Guard, 3,
            new Face(Guard: 17),
            Targets: TargetKind.Self,
            Description: "刃を立てて重い一撃を受ける");

        /// <summary>#13. The one close-range threshold at 1 rather than 0: +3 Guard within a gap of 1.</summary>
        public static readonly CardDef LowGuard = new CardDef(
            "low_guard", "低い構え", BattleAttribute.Guard, 3,
            new Face(Guard: 15),
            AtMost(1, TraitEffect.GuardBonus, 3),
            Targets: TargetKind.Self,
            Description: "腰を落として近くで受ける");

        /// <summary>#14. Guard that grows +3 after an attack this turn.</summary>
        public static readonly CardDef RiposteGuard = new CardDef(
            "riposte_guard", "受け太刀", BattleAttribute.Guard, 2,
            new Face(Guard: 9),
            Combo(BattleAttribute.Attack, TraitEffect.GuardBonus, 3),
            Targets: TargetKind.Self,
            Description: "斬った勢いのまま受けに回る");

        /// <summary>#15. The second plain column-3 Guard, the same 17 as the iron block.</summary>
        public static readonly CardDef DeepBreath = new CardDef(
            "deep_breath", "深い呼吸", BattleAttribute.Guard, 3,
            new Face(Guard: 17),
            Targets: TargetKind.Self,
            Description: "深く息を吸って受ける");

        // ---- St ----

        /// <summary>#16. Stance: +2 recovery on turns that start at a gap of 2 or more; costs one less after a move.</summary>
        public static readonly CardDef WaterStance = new CardDef(
            "water_stance", "水の構え", BattleAttribute.Stance, 2,
            new Face(Stance: new StanceDef(StanceHook.TurnStart, StanceWhen.GapAtLeast, Threshold: 2, Recovery: 2)),
            Combo(BattleAttribute.Move, TraitEffect.CostDown, 1),
            Targets: TargetKind.Self,
            Description: "退いて息を戻す型");

        /// <summary>#17. Stance: 3 Guard at every turn start; +3 Guard now into an attack omen.</summary>
        public static readonly CardDef RockStance = new CardDef(
            "rock_stance", "岩の構え", BattleAttribute.Stance, 2,
            new Face(Stance: new StanceDef(StanceHook.TurnStart, Guard: 3)),
            OmenIs(OmenKind.Attack, TraitEffect.GuardBonus, 3),
            Targets: TargetKind.Self,
            Description: "動かず受ける型");

        /// <summary>#18. Stance: +5 on attacks in a turn something moved; draws after a move.</summary>
        public static readonly CardDef FlowStance = new CardDef(
            "flow_stance", "流れの構え", BattleAttribute.Stance, 3,
            new Face(Stance: new StanceDef(StanceHook.AttackBonus, StanceWhen.MovedThisTurn, Power: 5)),
            Combo(BattleAttribute.Move, TraitEffect.Draw, 1),
            Targets: TargetKind.Self,
            Description: "動いてから斬る型");

        // ---- Sk ----

        /// <summary>#19. The plain heal: 13 on the ruler plus the 素直 +2.</summary>
        public static readonly CardDef FirstAid = new CardDef(
            "first_aid", "応急処置", BattleAttribute.Skill, 3,
            new Face(Heal: 15),
            Targets: TargetKind.Self,
            Description: "傷口を縛る");

        /// <summary>#20. Intimidates at 0〜2, and makes a bleeding foe fragile too.</summary>
        public static readonly CardDef SpiritRoar = new CardDef(
            "spirit_roar", "気迫", BattleAttribute.Skill, 1,
            new Face(Reach: new Reach(0, 2), Statuses: new[] { Foe(StatusKind.Intimidate, 2) }),
            FoeHas(StatusKind.Bleed, TraitEffect.Status, grant: Foe(StatusKind.Fragile, 2)),
            Description: "声で相手の次の一手を鈍らせる");

        /// <summary>#21. The column-3 skill: focus, empower and a draw; one more draw as the first play.</summary>
        public static readonly CardDef Focus = new CardDef(
            "focus", "集中", BattleAttribute.Skill, 3,
            new Face(Draw: 1, Statuses: new[] { Self(StatusKind.Focus, 2), Self(StatusKind.Empower, 2) }),
            When(TraitCondition.FirstPlay, TraitEffect.Draw, 1),
            Targets: TargetKind.Self,
            Description: "次の一手に力を溜める");

        /// <summary>#22. Draws two, three when it leaves the hand thin.</summary>
        public static readonly CardDef Observe = new CardDef(
            "observe", "観察", BattleAttribute.Skill, 1,
            new Face(Draw: 2),
            When(TraitCondition.Thin, TraitEffect.Draw, 1),
            Targets: TargetKind.Self,
            Description: "相手をよく見る");

        /// <summary>#23. Empowers self and intimidates the foe at 0〜2; fragile too on a chain.</summary>
        public static readonly CardDef WarCry = new CardDef(
            "war_cry", "鬨の声", BattleAttribute.Skill, 2,
            new Face(Reach: new Reach(0, 2), Statuses: new[] { Self(StatusKind.Empower, 2), Foe(StatusKind.Intimidate, 2) }),
            When(TraitCondition.Chain, TraitEffect.Status, grant: Foe(StatusKind.Fragile, 2)),
            Description: "叫んで己を奮わせ相手を怯ませる");

        /// <summary>#24. Regen 3 and a stamina point, one more point while regen already runs.</summary>
        public static readonly CardDef SecondWind = new CardDef(
            "second_wind", "息継ぎ", BattleAttribute.Skill, 3,
            new Face(StaminaGain: 1, Statuses: new[] { Self(StatusKind.Regen, 3) }),
            SelfHas(StatusKind.Regen, TraitEffect.StaminaGain, 1),
            Targets: TargetKind.Self,
            Description: "少しずつ傷を癒す");

        // ---- M ----

        /// <summary>#25. Steps forward one and draws; a stamina point as the first play.</summary>
        public static readonly CardDef Footwork = new CardDef(
            "footwork", "足運び", BattleAttribute.Move, 1,
            new Face(Move: 1, Draw: 1),
            When(TraitCondition.FirstPlay, TraitEffect.StaminaGain, 1),
            Targets: TargetKind.Self,
            Description: "先に足を運ぶ");

        /// <summary>#26. Leaps back two behind 4 Guard, +3 more into an attack omen.</summary>
        public static readonly CardDef BackLeap = new CardDef(
            "back_leap", "後ろ跳び", BattleAttribute.Move, 1,
            new Face(Move: -2, Guard: 4),
            OmenIs(OmenKind.Attack, TraitEffect.GuardBonus, 3),
            Targets: TargetKind.Self,
            Description: "跳んで間合いを外す");

        /// <summary>#27. Steps forward one behind 4 Guard; draws after a guard.</summary>
        public static readonly CardDef SlideStep = new CardDef(
            "slide_step", "摺り足", BattleAttribute.Move, 1,
            new Face(Move: 1, Guard: 4),
            Combo(BattleAttribute.Guard, TraitEffect.Draw, 1),
            Targets: TargetKind.Self,
            Description: "受けの後に静かに詰める");

        /// <summary>#28. Steps back one and regains a stamina point, one more as the third play or later.</summary>
        public static readonly CardDef BreakOff = new CardDef(
            "break_off", "間合い切り", BattleAttribute.Move, 1,
            new Face(Move: -1, StaminaGain: 1),
            When(TraitCondition.Finisher, TraitEffect.StaminaGain, 1),
            Targets: TargetKind.Self,
            Description: "手を尽くして間合いを切る");

        // ---- A+M ----

        /// <summary>#29. Hits at 1〜2, then steps in one; +3 after a stance.</summary>
        public static readonly CardDef Lunge = new CardDef(
            "lunge", "踏み込み斬り", BattleAttribute.Attack | BattleAttribute.Move, 3,
            new Face(Power: 14, Move: 1, Reach: new Reach(1, 2)),
            Combo(BattleAttribute.Stance, TraitEffect.PowerBonus, 3),
            Description: "型を決めてから踏み込んで斬る");

        /// <summary>#30. Hit, then step back one; +3 Guard when stamina is held back.</summary>
        public static readonly CardDef Feint = new CardDef(
            "feint", "牽制", BattleAttribute.Attack | BattleAttribute.Move, 2,
            new Face(Power: 8, Move: -1),
            Reserve(4, TraitEffect.GuardBonus, 3),
            Description: "牽制して退く");

        /// <summary>#31. The answer to being shoved away: reaches 1〜2, the gap is read before the move, so from 2 it lands at 20 and closes in.</summary>
        public static readonly CardDef BoarRush = new CardDef(
            "boar_rush", "猪突猛進", BattleAttribute.Attack | BattleAttribute.Move, 3,
            new Face(Power: 14, Move: 2, Reach: new Reach(1, 2)),
            AtLeast(2, TraitEffect.PowerBonus, 6),
            Description: "遠くから一気に駆けて斬る");

        // ---- A+Sk ----

        /// <summary>#32. Bleed 1 on the hit, one more stack into a foe already bleeding.</summary>
        public static readonly CardDef Rend = new CardDef(
            "rend", "裂き斬り", BattleAttribute.Attack | BattleAttribute.Skill, 1,
            new Face(Power: 4, Statuses: new[] { Foe(StatusKind.Bleed, 1) }),
            FoeHas(StatusKind.Bleed, TraitEffect.Status, grant: Foe(StatusKind.Bleed, 1)),
            Description: "傷口をさらに裂く");

        /// <summary>#33. The column-1 attack that slows; a heavy blow when adjacent.</summary>
        public static readonly CardDef BodyCheck = new CardDef(
            "body_check", "体当たり", BattleAttribute.Attack | BattleAttribute.Skill, 1,
            new Face(Power: 4, Statuses: new[] { Foe(StatusKind.Slow, 2) }),
            AtMost(0, TraitEffect.HeavyBlow),
            Description: "詰め切って身体ごとぶつかる");

        /// <summary>#34. Reaches 1〜3 (so 4, not 8) with fragile and intimidate; costs one less from a gap of 2 or more.</summary>
        public static readonly CardDef StoneThrow = new CardDef(
            "stone_throw", "石礫", BattleAttribute.Attack | BattleAttribute.Skill, 2,
            new Face(Power: 4, Reach: new Reach(1, 3), Statuses: new[] { Foe(StatusKind.Fragile, 2), Foe(StatusKind.Intimidate, 2) }),
            AtLeast(2, TraitEffect.CostDown, 1),
            Description: "離れていても石は投げられる");

        // ---- A+G ----

        /// <summary>#35. The plain attack-and-guard: 14 / 10 on the ruler plus the 素直 +2 each.</summary>
        public static readonly CardDef ParryCut = new CardDef(
            "parry_cut", "受け流し斬り", BattleAttribute.Attack | BattleAttribute.Guard, 3,
            new Face(Power: 16, Guard: 12),
            Description: "受け流しざまに斬る");

        /// <summary>#36. Adjacent only (so 17, not 14); +3 after a guard.</summary>
        public static readonly CardDef GuardThrust = new CardDef(
            "guard_thrust", "柄当て", BattleAttribute.Attack | BattleAttribute.Guard, 3,
            new Face(Power: 17, Guard: 10, Reach: Reach.Only(0)),
            Combo(BattleAttribute.Guard, TraitEffect.PowerBonus, 3),
            Description: "受けた勢いで柄を突き出す");

        // ---- G+M ----

        /// <summary>#37. Guard while closing in: 10 on the ruler plus the 素直 +2.</summary>
        public static readonly CardDef StepInGuard = new CardDef(
            "step_in_guard", "足捌き・前", BattleAttribute.Guard | BattleAttribute.Move, 3,
            new Face(Guard: 12, Move: 1),
            Targets: TargetKind.Self,
            Description: "受けながら詰める");

        /// <summary>#38. Guard while backing off: 10 on the ruler plus the 素直 +2.</summary>
        public static readonly CardDef StepOutGuard = new CardDef(
            "step_out_guard", "足捌き・後", BattleAttribute.Guard | BattleAttribute.Move, 3,
            new Face(Guard: 12, Move: -1),
            Targets: TargetKind.Self,
            Description: "受けながら退く");

        // ---- G+St ----

        /// <summary>#39. Guard now and a stance of 3 Guard at every turn start; +3 while parrying.</summary>
        public static readonly CardDef IronWall = new CardDef(
            "iron_wall", "鉄壁の構え", BattleAttribute.Guard | BattleAttribute.Stance, 3,
            new Face(Guard: 10, Stance: new StanceDef(StanceHook.TurnStart, Guard: 3)),
            SelfHas(StatusKind.Parry, TraitEffect.GuardBonus, 3),
            Targets: TargetKind.Self,
            Description: "見切った上で壁になる");

        // ---- Sk+M ----

        /// <summary>#40. Slow, fatigue and intimidate at 0〜1, then step back one; a stamina point after an attack.</summary>
        public static readonly CardDef TwistAway = new CardDef(
            "twist_away", "翻し足", BattleAttribute.Skill | BattleAttribute.Move, 3,
            new Face(Move: -1, Statuses: new[] { Foe(StatusKind.Slow, 2), Foe(StatusKind.Fatigue, 2), Foe(StatusKind.Intimidate, 2) }),
            Combo(BattleAttribute.Attack, TraitEffect.StaminaGain, 1),
            Description: "斬った後に身を翻し足を封じる");
    }
}
