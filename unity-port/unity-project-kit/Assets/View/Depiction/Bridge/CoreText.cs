// Every string and glyph the screen prints about a core card, action or omen is written here, so
// the View keeps printing what it is given. Pure C#: BattleCore + Depiction.Script, no UnityEngine.
using System.Collections.Generic;
using BattleCore;

namespace Depiction.Bridge
{
    public static class CoreText
    {
        /// <summary>
        /// v4.3 (#162) on the v4.2 screen: the script still knows two standing slots (near / far),
        /// so the gap N is shown on the player's tag as a number and the slot is picked by N — 0〜1
        /// stands in the near slot, 2 and more in the far one. The cells on the floor, the lit reach
        /// and the aimed cells are #163's, and this mapping goes with them.
        /// </summary>
        public static RangeSide SideOf(int gap)
        {
            return gap <= 1 ? RangeSide.Near : RangeSide.Far;
        }

        /// <summary>The one glyph on the player's tag: N itself ("0", "2").</summary>
        public static string GapGlyph(int gap)
        {
            return gap.ToString();
        }

        public static UnitSide Side(Actor actor)
        {
            return actor == Actor.Player ? UnitSide.Player : UnitSide.Enemy;
        }

        /// <summary>The colour role a card borrows: the one attribute it counts as (§2.1, v4.4). Movement is an effect, not a role of its own.</summary>
        public static CardKind KindOf(BattleAttribute attributes)
        {
            if (attributes.HasFlag(BattleAttribute.Attack)) return CardKind.Attack;
            if (attributes.HasFlag(BattleAttribute.Guard)) return CardKind.Guard;
            if (attributes.HasFlag(BattleAttribute.Skill)) return CardKind.Skill;
            return attributes.HasFlag(BattleAttribute.Stance) ? CardKind.Stance : CardKind.Skill;
        }

        public static string KindWord(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.Attack: return "攻撃";
                case CardKind.Guard: return "防御";
                case CardKind.Move: return "ムーブ";
                case CardKind.Skill: return "技";
                default: return "構え";
            }
        }

        /// <summary>How hard a beat looks, 1..4. The same steps LiveTurn uses; the thresholds are #30's to settle.</summary>
        /// <summary>§5.3: the system a card's blow draws, from its name and faces (StrikeSystems.Of).</summary>
        public static StrikeSystem SystemOf(CardDef def)
        {
            return StrikeSystems.Of(def.Name, def.Attributes.HasFlag(BattleAttribute.Attack),
                def.Attributes.HasFlag(BattleAttribute.Guard) || def.Face.Guard > 0, def.Face.Move != 0, def.Face.Push != 0);
        }

        /// <summary>§5.3: the same for an enemy action: the shape of its move and of its blow.</summary>
        public static StrikeSystem SystemOf(EnemyActionDef action)
        {
            return StrikeSystems.Of(action.Name, action.Attributes.HasFlag(BattleAttribute.Attack),
                action.Attributes.HasFlag(BattleAttribute.Guard) || action.Face.Guard > 0, action.Face.Move != 0, action.Face.Push != 0);
        }

        // ---- cards --------------------------------------------------------------------------

        /// <summary>
        /// The face of a catalog card that is not dealt: the deck screen prints the same face as the
        /// hand does (no preview, so the cost is the printed one and no lamp is lit). The face's id is
        /// the card's own id.
        /// </summary>
        public static CardFace FaceOf(CardDef def)
        {
            return Face(new CardInstance(def.Id, def), null, false);
        }

        /// <summary>
        /// The face of one dealt card. <paramref name="preview"/> comes from the core
        /// (TurnLoop.Preview), so the lamp is the core's verdict and not a guess made here. The reach
        /// hint reads the preview's own enemy only; with several enemies use the overload that takes
        /// <see cref="ReachesNobody"/>.
        /// </summary>
        public static CardFace Face(CardInstance card, PlayPreview preview)
        {
            return Face(card, preview, preview != null && !preview.InReach);
        }

        /// <summary>
        /// The face of one dealt card. <paramref name="outOfReach"/> is true when no standing enemy
        /// is inside the card's reach (<see cref="ReachesNobody"/>); it fills <see cref="CardFace.ReachHint"/>.
        /// </summary>
        public static CardFace Face(CardInstance card, PlayPreview preview, bool outOfReach)
        {
            CardDef def = card.Def;
            CardKind kind = KindOf(def.Attribute);
            bool single = def.Targets == TargetKind.One;
            var face = new CardFace
            {
                Id = card.InstanceId,
                Name = def.Name,
                // The cost a コスト −1 trait leaves right now is the core's (TurnLoop.Preview).
                Cost = preview != null ? preview.Cost : def.Cost,
                Kind = kind,
                Aim = single ? CardAim.Single : CardAim.Self,
                Affects = single ? (UnitSide?)null : UnitSide.Player,
                TypeLabel = KindWord(kind) + (single ? "・敵単体" : "・自分"),
                Description = Describe(def.Face, def.Attributes),
                ValueText = ValueOf(def.Face, def.Attributes),
                TraitText = TraitLines(def),
                TraitLit = def.Trait != null && preview != null && preview.TraitHolds,
                // §2.4: the reach is printed on the card; the two-valued RequiredRange cannot hold it,
                // so the core's CanPlay (OutOfReach) is what refuses the card, not the script.
                RequiredRange = null,
                RequiredRangeGlyph = ReachText(def.Attributes, def.Face, def.Targets),
                ReachHint = outOfReach ? ReachHint(def.Attributes, def.Face, def.Targets) : "",
            };
            // battle-visual-v1 §6 / §7 / §9 (#242): values, rows, trait box, hover line and detail.
            CardFaceText.Fill(face, def);
            return face;
        }

        /// <summary>
        /// #261: true when the card aims at the opponent and every standing enemy is outside its
        /// reach. Each enemy is asked through the core's own verdict (TurnLoop.Preview's InReach),
        /// which looks at the gap only — so a card short of stamina alone is not out of reach, and
        /// the answer holds in any phase. False for a card not in the hand or with nobody standing.
        /// </summary>
        public static bool ReachesNobody(BattleState state, string instanceId)
        {
            bool anyone = false;
            foreach (int unit in state.Living)
            {
                PlayPreview preview = TurnLoop.Preview(state, instanceId, unit);
                if (preview == null) return false;
                if (preview.InReach) return false;
                anyone = true;
            }
            return anyone;
        }

        /// <summary>
        /// #261: the line beside a card nobody is in reach of ("相手との間合いが 1〜2 のとき使用可能",
        /// "相手との間合いが 0 のとき使用可能"), or empty for a card that aims at nobody.
        /// </summary>
        public static string ReachHint(BattleAttribute attributes, Face face, TargetKind targets)
        {
            string reach = ReachText(attributes, face, targets);
            return reach.Length == 0 ? "" : "相手との間合いが " + reach + " のとき使用可能";
        }

        /// <summary>The reach as printed ("0〜1"), or empty for a card that aims at nobody.</summary>
        public static string ReachText(BattleAttribute attributes, Face face, TargetKind targets)
        {
            return EnemyAi.IsOpponentDirected(attributes, face, targets) ? face.ReachOrDefault.ToText() : "";
        }

        /// <summary>
        /// One short sentence per face, in resolution order. §5: a status on the opponent reads
        /// 「<語> を n 付与する」, one on the one playing 「自分に <語> を n 付与する」.
        /// </summary>
        public static string Describe(Face face, BattleAttribute attributes)
        {
            var sentences = new List<string>();
            if (attributes.HasFlag(BattleAttribute.Attack)) sentences.Add("敵に " + face.Power + " ダメージ。");
            if (face.Break > 0) sentences.Add("崩し " + face.Break + "。");
            if (face.Move > 0) sentences.Add("前へ " + face.Move + " 動く。");
            else if (face.Move < 0) sentences.Add("後ろへ " + (-face.Move) + " 動く。");
            if (face.Push > 0) sentences.Add("敵を " + face.Push + " マス押す。");
            else if (face.Push < 0) sentences.Add("敵を " + (-face.Push) + " マス引く。");
            if (face.Guard > 0) sentences.Add("Guard " + face.Guard + " を得る。");
            if (face.Heal > 0) sentences.Add("HP を " + face.Heal + " 回復する。");
            foreach (StatusGrant grant in face.StatusList)
            {
                sentences.Add((grant.OnSelf ? "自分に" : "") + grant.Kind.ToLabel() + "を " + grant.Stacks + " 付与する。");
            }
            if (face.Draw > 0) sentences.Add("カードを " + face.Draw + " 枚引く。");
            if (face.StaminaGain > 0) sentences.Add("スタミナ +" + face.StaminaGain + "。");
            if (face.Stance != null) sentences.Add("構え: " + StanceText(face.Stance) + "。");
            return string.Join("", sentences);
        }

        /// <summary>§4: what a stance does while it stands, as one clause ("毎ターン開始に Guard +3").</summary>
        public static string StanceText(StanceDef stance)
        {
            if (stance == null) return "";
            switch (stance.Hook)
            {
                case StanceHook.TurnStart:
                    return Spaced(WhenWord(stance, "始まるターンに"), Gains(stance.Guard, stance.Recovery, 0));
                case StanceHook.TurnEnd:
                    return Spaced(WhenWord(stance, "終えたターンに"), Gains(stance.Guard, 0, stance.NextRecovery));
                case StanceHook.AttackBonus:
                    switch (stance.When)
                    {
                        case StanceWhen.MovedThisTurn: return "移動したターン、アタック +" + stance.Power;
                        case StanceWhen.GapAtMost: return "間合い " + stance.Threshold + " の相手へのアタック +" + stance.Power;
                        case StanceWhen.TargetHasStatus:
                            return (stance.Status.HasValue ? stance.Status.Value.ToLabel() : "") + "中の敵へのアタック +" + stance.Power;
                        default: return "アタック +" + stance.Power;
                    }
                case StanceHook.OnHit:
                {
                    var parts = new List<string>();
                    if (stance.Stamina > 0) parts.Add("スタミナ +" + stance.Stamina);
                    if (stance.Guard > 0) parts.Add("Guard +" + stance.Guard);
                    if (stance.Status.HasValue) parts.Add("敵に" + stance.Status.Value.ToLabel() + " " + stance.StatusStacks);
                    return "被弾のたび" + (stance.OncePerTurn ? "（ターン 1 回）" : "") + string.Join("、", parts);
                }
                case StanceHook.PushImmune: return "押す / 引くを受けない";
                case StanceHook.BreakOnFoeMove: return "敵が自分で動くたび崩し " + stance.Break;
                case StanceHook.StatusOnAttack:
                    // #253: once a blow, so a face of two blows gives it twice.
                    return "アタックのたび敵に" + (stance.Status.HasValue ? stance.Status.Value.ToLabel() : "") + " " + stance.StatusStacks;
                case StanceHook.CostDiscount:
                    // §4 コストの割引 (鉄壁の構え, #333), in the attribute words the deck screen uses.
                    return "毎ターン 1 回、" + (stance.Attribute.HasValue ? DeckBuilder.AttributeWords(stance.Attribute.Value) : "") + "のカードのコスト −1";
                default: return "";
            }
        }

        private static string WhenWord(StanceDef stance, string tail)
        {
            switch (stance.When)
            {
                case StanceWhen.GapAtLeast: return "間合い " + stance.Threshold + " 以上で" + tail;
                case StanceWhen.GapAtMost: return "間合い " + stance.Threshold + " 以下で" + tail;
                case StanceWhen.OmenAttack: return "予兆が攻撃のターン開始に";
                default: return "毎ターン開始に";
            }
        }

        /// <summary>Japanese then a Latin word ("Guard") get a half-width space between them (rules: 和欧間).</summary>
        private static string Spaced(string head, string tail)
        {
            bool latin = tail.Length > 0 && tail[0] < 0x80;
            return latin ? head + " " + tail : head + tail;
        }

        private static string Gains(int guard, int recovery, int nextRecovery)
        {
            var parts = new List<string>();
            if (guard > 0) parts.Add("Guard +" + guard);
            if (recovery > 0) parts.Add("回復 +" + recovery);
            if (nextRecovery > 0) parts.Add("次の回復 +" + nextRecovery);
            return string.Join("、", parts);
        }

        /// <summary>
        /// The number on a face: an attack's power over all its blows as one sum (#248: "12", never
        /// "6×2"), else its Guard, else its heal.
        /// </summary>
        public static string ValueOf(Face face, BattleAttribute attributes)
        {
            if (attributes.HasFlag(BattleAttribute.Attack)) return FacePower(face).ToString();
            if (face.Guard > 0) return face.Guard.ToString();
            return face.Heal > 0 ? face.Heal.ToString() : "";
        }

        /// <summary>The attack face alone, every blow summed: power × hits.</summary>
        public static int FacePower(Face face)
        {
            return face.Power * System.Math.Max(1, face.Hits);
        }

        /// <summary>Every trait of the card on one lamp line; 背水の陣's two read "間合い2以上 +5 ／ 死力 +3".</summary>
        public static string TraitLines(CardDef def)
        {
            var lines = new List<string>();
            foreach (Trait trait in def.AllTraits) lines.Add(TraitLine(trait));
            return string.Join(" ／ ", lines);
        }

        /// <summary>The trait as printed on the lamp line: condition, then effect ("間合い0 +5", "残4 Guard+3").</summary>
        public static string TraitLine(Trait trait)
        {
            if (trait == null) return "";
            string effect = EffectWord(trait);
            string condition = ConditionWord(trait);
            return condition.Length == 0 ? effect : condition + " " + effect;
        }

        private static string ConditionWord(Trait trait)
        {
            switch (trait.Condition)
            {
                case BattleCore.TraitCondition.GapAtMost:
                    return trait.Threshold == 0 ? "間合い0" : "間合い" + trait.Threshold + "以下";
                case BattleCore.TraitCondition.GapAtLeast:
                    return "間合い" + trait.Threshold + "以上";
                case BattleCore.TraitCondition.Unguarded: return "無防備";
                case BattleCore.TraitCondition.Reserve: return "残" + trait.Threshold;
                case BattleCore.TraitCondition.Combo: return "連動" + AttributeWord(trait.Attribute);
                case BattleCore.TraitCondition.Moved: return "移動後";
                case BattleCore.TraitCondition.OmenIs: return "予兆" + (trait.Omen.HasValue ? OmenKindWord(trait.Omen.Value) : "");
                case BattleCore.TraitCondition.Desperate: return "死力";
                case BattleCore.TraitCondition.FirstPlay: return "初手";
                case BattleCore.TraitCondition.Finisher: return "締め";
                case BattleCore.TraitCondition.Broken: return "崩し後";
                case BattleCore.TraitCondition.Chain: return "連打";
                case BattleCore.TraitCondition.Thin: return "手薄";
                case BattleCore.TraitCondition.FoeHas: return "相手" + (trait.Watch.HasValue ? trait.Watch.Value.ToLabel() : "");
                case BattleCore.TraitCondition.SelfHas: return "自分" + (trait.Watch.HasValue ? trait.Watch.Value.ToLabel() : "");
                default: return "";
            }
        }

        private static string EffectWord(Trait trait)
        {
            switch (trait.Effect)
            {
                case TraitEffect.PowerBonus: return "+" + trait.Amount;
                case TraitEffect.GuardBonus: return "Guard+" + trait.Amount;
                case TraitEffect.NextTurnRecovery: return "回復+" + trait.Amount;
                case TraitEffect.HeavyBlow: return "重撃";
                case TraitEffect.StaminaGain: return "スタミナ+" + trait.Amount;
                case TraitEffect.Draw: return "ドロー+" + trait.Amount;
                case TraitEffect.Status:
                    return trait.Grant == null ? "" : (trait.Grant.OnSelf ? "自分" : "") + trait.Grant.Kind.ToLabel() + "+" + trait.Grant.Stacks;
                case TraitEffect.CostDown: return "コスト-" + trait.Amount;
                case TraitEffect.Convert: return "転換";
                case TraitEffect.FollowUp: return "追撃";
                case TraitEffect.BreakBonus: return "崩し+" + trait.Amount;
                default: return "";
            }
        }

        private static string AttributeWord(BattleAttribute attribute)
        {
            if (attribute.HasFlag(BattleAttribute.Attack)) return "(攻)";
            if (attribute.HasFlag(BattleAttribute.Guard)) return "(防)";
            if (attribute.HasFlag(BattleAttribute.Skill)) return "(技)";
            if (attribute.HasFlag(BattleAttribute.Stance)) return "(構)";
            return "";
        }

        // ---- the boss words that stop a move (#335) -------------------------------------------

        /// <summary>
        /// The line a move that did not happen shows over the one who could not move (#335):
        /// 呪縛 stops every move of the holder's own, 鉤爪 catches a move back (enemy_roster_v4 §4.1 /
        /// §5.1). Empty for any other cause; 鈍足 has no line of its own yet.
        /// </summary>
        public static string MoveBlockedLine(StatusKind by)
        {
            switch (by)
            {
                case StatusKind.Binding: return by.ToLabel() + "で動けない";
                case StatusKind.Hook: return by.ToLabel() + "で下がれない";
                default: return "";
            }
        }

        /// <summary>Why a card of movement only goes back to the hand while the player is bound (#335, roster §4.1 呪縛).</summary>
        public static string BoundRefusal(string cardName)
        {
            return "「" + cardName + "」は移動が中心の札です。" + StatusKind.Binding.ToLabel() + "が付いている間は出せません";
        }

        // ---- omen ---------------------------------------------------------------------------

        /// <summary>
        /// The omen's kind as a word: the words of battle-visual-v1 4.4, which #243 brought the
        /// roster to as well (防御 / 移動). BattleCore's EnemyAi.ToText reads the same words (#357).
        /// </summary>
        public static string OmenKindWord(OmenKind kind)
        {
            switch (kind)
            {
                case OmenKind.Attack: return "攻撃";
                case OmenKind.Guard: return "防御";
                case OmenKind.Move: return "移動";
                case OmenKind.Skill: return "技";
                case OmenKind.Stance: return "構え";
                case OmenKind.Rest: return "休み";
                default: return "";
            }
        }

        /// <summary>The icon of the omen's kind (#349): the skill kind wears the status-apply icon; its final look is #350.</summary>
        public static OmenIcon IconOf(OmenKind kind)
        {
            switch (kind)
            {
                case OmenKind.Attack: return OmenIcon.Attack;
                case OmenKind.Guard: return OmenIcon.Guard;
                case OmenKind.Skill: return OmenIcon.Status;
                case OmenKind.Move: return OmenIcon.Move;
                case OmenKind.Stance: return OmenIcon.Stance;
                case OmenKind.Rest: return OmenIcon.Rest;
                default: return OmenIcon.None;
            }
        }

        /// <summary>
        /// The number beside the omen's icon (#349): attack and guard only. An attack reads the
        /// core's <paramref name="preview"/> when there is one, else its face (power × hits); a guard
        /// reads the face's Guard. A move with a Guard face (踏み込み) shows none, since its kind is 移動.
        /// </summary>
        public static string OmenValueOf(OmenKind kind, EnemyActionDef action, OmenPreview preview)
        {
            if (action == null) return "";
            switch (kind)
            {
                case OmenKind.Attack:
                    return preview != null ? preview.RawPower.ToString() : FacePower(action.Face).ToString();
                case OmenKind.Guard:
                    return action.Face.Guard > 0 ? action.Face.Guard.ToString() : "";
                default:
                    return "";
            }
        }

        /// <summary>
        /// §6: 種別 (icon and word) + 狙うマス, plus one number for attack and guard (#349). Until the
        /// floor shows the aimed cells (#242) the reach goes where the one-character side used to be
        /// ("1〜2").
        ///
        /// For an attack the number is the core's (#248, TurnLoop.PreviewOmen): the whole action's
        /// power before the player's Guard — every blow, the wall, and the trait as the board stands
        /// now — and what it would be if it landed when it does not reach. Without a
        /// <paramref name="preview"/> it is the face alone (power × hits). A guard shows the face's
        /// Guard; any other kind shows no number.
        /// </summary>
        public static OmenFrame OmenOf(Omen omen, EnemyDef enemy, OmenPreview preview = null)
        {
            if (omen == null) return new OmenFrame { Visible = false };

            OmenKind kind = omen.Label.Kind;
            EnemyActionDef action;
            // A rest has no entry in the table (EnemyAi.RestActionId): action stays null, no number.
            enemy.Actions.TryGetValue(omen.ActionId, out action);
            return new OmenFrame
            {
                Visible = true,
                Icon = IconOf(kind),
                KindLabel = OmenKindWord(kind),
                SideGlyph = omen.Label.Reach != null ? omen.Label.Reach.ToText() : "",
                ValueText = OmenValueOf(kind, action, preview),
            };
        }

        // ---- units --------------------------------------------------------------------------

        /// <summary>
        /// One side's gauges. <paramref name="gap"/> is N, shown on the tag of the player only: the
        /// screen has one tag slot per side and one number says it all (#163 draws the cells).
        /// </summary>
        public static UnitFrame UnitOf(CombatantState unit, bool showStamina, int? gap, IReadOnlyList<string> stanceNames = null)
        {
            var frame = new UnitFrame
            {
                Hp = unit.Hp,
                HpMax = unit.MaxHp,
                Guard = unit.Guard,
                ShowStamina = showStamina,
                Stamina = unit.Stamina,
                StaminaMax = unit.MaxStamina,
                HasRange = gap.HasValue,
            };
            if (gap.HasValue)
            {
                frame.Range = SideOf(gap.Value);
                frame.RangeGlyph = GapGlyph(gap.Value);
            }
            frame.Statuses = Chips(unit.Statuses, stanceNames ?? new string[0]);
            return frame;
        }

        /// <summary>
        /// The chips under a gauge: the stance in the slot first (§4, #188; it has no stacks to count),
        /// then the status words in their fixed order.
        /// </summary>
        public static List<StatusChip> Chips(StatusSet statuses, string stanceName = "")
        {
            return Chips(statuses, string.IsNullOrEmpty(stanceName) ? new string[0] : new[] { stanceName });
        }

        /// <summary>
        /// The chips under a gauge (v4.4): the stances held first, one chip per stance, then the status
        /// words in their fixed order. The same stance twice is one chip with a count of 2, since it
        /// works twice (<see cref="StanceChips"/>).
        /// </summary>
        public static List<StatusChip> Chips(StatusSet statuses, IReadOnlyList<string> stanceNames)
        {
            return StatusChipText.Chips(statuses, stanceNames);
        }

        /// <summary>
        /// One chip per distinct stance name in the order first held; a stance held more than once
        /// shows its count in Stacks, a single one shows none. The panel text is StatusChipText's (#242).
        /// </summary>
        public static List<StatusChip> StanceChips(IReadOnlyList<string> stanceNames)
        {
            return StatusChipText.StanceChips(stanceNames);
        }

        /// <summary>
        /// #334 (roster §6.4 root_st): the line shown when a permanent effect stops for a turn, and the
        /// one shown when it works again. The stance keeps its chip the while; only these lines say so.
        /// </summary>
        public static string StanceStoppedLine(string stanceName) => "構え停止・" + stanceName;

        /// <summary>#334: see <see cref="StanceStoppedLine"/>.</summary>
        public static string StanceResumedLine(string stanceName) => "構え復帰・" + stanceName;
    }
}
