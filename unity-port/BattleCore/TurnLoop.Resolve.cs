using System;
using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>
    /// One card or enemy action, resolved (§2.2, #584): <see cref="Resolve"/> pays and judges the
    /// traits once, then runs the stages in order — attack, movement, Guard and skill, the statuses
    /// and the stance.
    /// </summary>
    public static partial class TurnLoop
    {
        /// <summary>
        /// The card or enemy action <see cref="Resolve"/> works on, as written off the face table: its
        /// id and name, its attributes, column and face (an enemy action's face already read at N),
        /// its traits, whom it aims at, and the cost it is paid at before a コスト −1 trait.
        /// </summary>
        private sealed record ResolveSource(
            string Id, string Name, BattleAttribute Attributes, int Column, Face Face,
            IReadOnlyList<Trait> Traits, TargetKind Targets, int Cost);

        /// <summary>A player's card as <see cref="Resolve"/> takes it, paid at <paramref name="cost"/> (a コストの割引 stance already taken off).</summary>
        private static ResolveSource CardSource(CardDef def, int cost) =>
            new ResolveSource(def.Id, def.Name, def.Attributes, def.Column, def.Face, def.AllTraits, def.Targets, cost);

        /// <summary>An enemy action as <see cref="Resolve"/> takes it, with the face it shows at the gap it is carried out from.</summary>
        private static ResolveSource ActionSource(EnemyActionDef action, Face face) =>
            new ResolveSource(action.Id, action.Name, action.Attributes, action.Column, face, TraitsOf(action.Trait), action.Targets, action.Cost);

        /// <summary>
        /// One card or action handed to <see cref="Resolve"/>: who plays it (the player, or enemy
        /// Unit), what it is, the enemies a player's opponent-directed faces land on (Foes; see
        /// Resolve) and the board its traits are judged on, read from before the card.
        /// </summary>
        private sealed record Play(Actor Actor, int Unit, IReadOnlyList<int> Foes, ResolveSource Source, TraitContext Context);

        /// <summary>
        /// What the stages of one <see cref="Resolve"/> share once the cost is paid and the traits are
        /// judged. Hit holds the units the opponent-directed faces land on: the enemies for a player's
        /// card, the acting unit for an enemy's action (its foe is always the player), none on a whiff.
        /// Focus and Intimidate are what 集中 and 威圧 put on this card; both are already spent.
        /// </summary>
        private sealed record Resolution(
            Play Play, IReadOnlyList<int> Hit, TraitOutcome Outcome, FocusStep Focus, int Intimidate, bool Attacks, int GuardBase)
        {
            public Actor Actor => Play.Actor;
            public int Unit => Play.Unit;
            public ResolveSource Source => Play.Source;
            public Face Face => Play.Source.Face;
            public Actor FoeSide => Opponent(Play.Actor);

            /// <summary>The unit an event about <paramref name="foe"/> carries: the foe's own for an enemy, the acting enemy's for the player.</summary>
            public int EventUnit(int foe) => FoeSide == Actor.Enemy ? foe : Unit;

            /// <summary>Whether <paramref name="foe"/> is still there to land on: the player always is, an enemy while it stands.</summary>
            public bool Stands(BattleState state, int foe) => FoeSide == Actor.Player || state.Enemies[foe].Alive;
        }

        /// <summary>
        /// §2.2: pay, judge the traits once, then Attack → Move → Guard → Skill → Stance. The same
        /// routine resolves a card and an enemy action, because both are written off the same face
        /// table.
        ///
        /// The play's Unit is the acting enemy in an enemy phase, and the enemy whose N and Guard the
        /// card reads on the player's turn. Its Foes are the enemies a player's opponent-directed
        /// faces land on — none for a card aimed at nobody, one, or every one in reach for all
        /// (§7.4); an enemy's foe is always the player. A pushed line resolves the far foe first, a
        /// pulled one the near foe first, so that nobody is blocked by a neighbour that was about to
        /// move.
        ///
        /// When an enemy falls and others still stand, it leaves the line and the card goes on; the
        /// routine stops after the face that ended the battle (§17.6 F9), and when the one acting
        /// falls to a 見切り return.
        ///
        /// §6 空振り: an enemy action whose reach does not cover N at this moment skips its
        /// opponent-directed faces (attack and 崩し, push / pull, the statuses on the opponent) and
        /// still resolves the rest. A player card never gets here out of reach (CanPlay refuses it).
        ///
        /// The statuses the one acting holds work here: 集中 (the player's next card, one column to
        /// the right — <see cref="FocusStep"/>: power, Guard, heal and the face's 出血 / 再生), 威圧
        /// (−3 on power and Guard; spent by an action whose attack face lands or that has a Guard,
        /// so a whiff with no Guard leaves it), 強化 (×1.5 on the attack face) and a
        /// waiting 追撃. On the one hit: 脆化 (×1.5, taken before 強化 — §19.5 S13, one multiplier per
        /// blow) and 見切り.
        /// </summary>
        private static BattleState Resolve(BattleState state, Play play, IRng rng, List<BattleEvent> events)
        {
            state = OpenResolution(state, play, events, out var r);

            state = ResolveAttack(state, r, events, out bool over);
            if (over) return state;
            state = ResolveMovement(state, r, events, out over);
            if (over) return state;
            state = ResolveGuardAndSkill(state, r, events);
            state = ResolveStatuses(state, r, events);
            state = ResolveStance(state, r, events);

            int draw = r.Face.Draw + r.Outcome.Draw;
            if (draw > 0 && r.Actor == Actor.Player)
            {
                state = DrawCards(state, draw, rng, events);
            }

            // 追撃 waits for the next attack face, not this card's own (§2.3).
            if (r.Outcome.FollowUp > 0)
            {
                var self = Get(state, r.Actor, r.Unit);
                state = Set(state, r.Actor, r.Unit, self with { FollowUp = self.FollowUp + r.Outcome.FollowUp });
            }
            return state;
        }

        /// <summary>
        /// The part of <see cref="Resolve"/> before any face: pay, announce a whiff, judge the traits
        /// and take what they give at once (the next turn's recovery, stamina), then spend 集中 and
        /// 威圧 on this card. What the stages share comes back as <paramref name="resolution"/>.
        /// </summary>
        private static BattleState OpenResolution(BattleState state, Play play, List<BattleEvent> events, out Resolution resolution)
        {
            Actor actor = play.Actor;
            int unit = play.Unit;
            var source = play.Source;
            var face = source.Face;
            int gap = state.GapTo(unit);

            // The traits are judged on the board from before the card (the context), before any face.
            var outcome = Traits.EvaluateAll(source.Traits, play.Context);
            int paid = Math.Max(0, source.Cost - outcome.CostDown);

            var self = Get(state, actor, unit);
            self = self with { Stamina = self.Stamina - paid };
            state = Set(state, actor, unit, self);
            events.Add(new StaminaSpent(actor, paid, self.Stamina) { Unit = unit });

            // The whiff is known before anything resolves, and is announced first so that whoever
            // reads the stream knows the trait below is judged on a blow that will not land.
            bool directed = EnemyAi.IsOpponentDirected(source.Attributes, face, source.Targets);
            bool whiff = directed && !face.ReachOrDefault.Contains(gap);
            if (whiff)
            {
                if (actor == Actor.Player)
                {
                    throw new InvalidOperationException($"Card \"{source.Id}\" was resolved out of reach; CanPlay should have refused it.");
                }
                events.Add(new ActionWhiffed(actor, source.Id, gap, face.ReachOrDefault) { Unit = unit });
            }

            // The sides the opponent-directed faces land on: (the foe's unit; ignored for the player).
            var hit = new List<int>();
            if (actor == Actor.Enemy)
            {
                if (directed && !whiff) hit.Add(unit);
            }
            else
            {
                hit.AddRange(play.Foes);
            }

            foreach (var trait in source.Traits)
            {
                events.Add(new TraitEvaluated(actor, source.Id, trait, Traits.Evaluate(trait, play.Context)) { Unit = unit });
            }
            if (outcome.NextTurnRecoveryBonus != 0)
            {
                self = Get(state, actor, unit);
                self = self with { NextTurnRecoveryBonus = self.NextTurnRecoveryBonus + outcome.NextTurnRecoveryBonus };
                state = Set(state, actor, unit, self);
            }
            if (outcome.StaminaGain > 0) state = GainStamina(state, actor, unit, outcome.StaminaGain, events);

            // 集中: the player's next card, whatever it is. 威圧: the next action whose attack face lands
            // or that has a Guard (§5: a whiff, a skill or a move, each with no Guard, and a rest leave it).
            var focus = FocusStep.None;
            if (actor == Actor.Player && Get(state, actor, unit).Statuses.Has(StatusKind.Focus))
            {
                focus = FocusStep.Of(source.Attributes, source.Column, face);
                state = Consume(state, actor, unit, StatusKind.Focus, events);
            }
            bool attacks = source.Attributes.HasFlag(BattleAttribute.Attack) && hit.Count > 0;
            int guardBase = face.Guard + focus.Guard + outcome.GuardBonus;
            int intimidate = 0;
            if (Get(state, actor, unit).Statuses.Has(StatusKind.Intimidate) && (attacks || guardBase > 0))
            {
                intimidate = Constants.IntimidatePenalty;
                state = Consume(state, actor, unit, StatusKind.Intimidate, events);
            }

            resolution = new Resolution(play, hit, outcome, focus, intimidate, attacks, guardBase);
            return state;
        }

        /// <summary>
        /// §2.2 Attack: every blow of the face on every foe hit, then its 崩し. Over is true when a
        /// blow ended the battle (§17.6 F9) or the attacker fell to a 見切り return; Resolve stops there.
        /// </summary>
        private static BattleState ResolveAttack(BattleState state, Resolution r, List<BattleEvent> events, out bool over)
        {
            over = false;
            if (!r.Attacks) return state;
            Actor actor = r.Actor;
            int unit = r.Unit;
            Actor foeSide = r.FoeSide;
            var face = r.Face;

            events.Add(new FaceResolved(actor, r.Source.Id, FaceKind.Attack) { Unit = unit });
            var self = Get(state, actor, unit);
            int followUp = self.FollowUp;
            if (followUp > 0) state = Set(state, actor, unit, self with { FollowUp = 0 });
            int conversion = r.Outcome.Convert ? Half(face.Guard + r.Focus.Guard) : 0;
            bool empowered = self.Statuses.Has(StatusKind.Empower);
            bool empowerUsed = false;

            foreach (int foe in r.Hit)
            {
                if (!r.Stands(state, foe)) continue;

                int stanceBonus = StanceAttackBonus(state, actor, unit, foeSide, foe, events);

                // §2.4 hits (#189): each blow meets Guard on its own. §17.6 F6: 追撃 rides the
                // first blow only and the trait's +n every blow; 強化 rides only the blows 脆化
                // does not take (§5.1, §19.5 S13).
                for (int blow = 0; blow < Math.Max(1, face.Hits); blow++)
                {
                    if (!r.Stands(state, foe)) break;
                    state = RaiseScale(state, foeSide, events);
                    var other = Get(state, foeSide, foe);

                    // §19.5 S13: one multiplier per blow, 脆化 first.
                    double mult = 1.0;
                    if (other.Statuses.Has(StatusKind.Fragile))
                    {
                        mult = Constants.FragileMult;
                        state = Consume(state, foeSide, foe, StatusKind.Fragile, events);
                        other = Get(state, foeSide, foe);
                    }
                    else if (empowered)
                    {
                        mult = Constants.EmpowerMult;
                        empowerUsed = true;
                    }

                    int raw = Combat.ComputeRawPower(
                        face.Power + r.Focus.Power - r.Intimidate, r.Outcome.PowerBonus + stanceBonus,
                        blow == 0 ? followUp : 0, conversion, mult);
                    var (damage, guardAfter, absorbed) = Combat.ApplyGuard(raw, other.Guard);
                    other = other with { Guard = guardAfter, Hp = Math.Max(0, other.Hp - damage) };
                    state = Set(state, foeSide, foe, other);
                    events.Add(new DamageDealt(actor, foeSide, raw, absorbed, damage, other.Guard, other.Hp) { Unit = r.EventUnit(foe) });

                    if (!Combat.IsDefeated(other.Hp))
                    {
                        state = OnHitReactions(state, actor, unit, foeSide, foe, absorbed, events);
                    }
                    state = OnFall(state, foeSide, foe, events, out over);
                    if (over) return state;

                    // 見切り may have taken the attacker down.
                    if (Combat.IsDefeated(Get(state, actor, unit).Hp))
                    {
                        state = OnFall(state, actor, unit, events, out _);
                        over = true;
                        return state;
                    }

                    state = AfterBlow(state, r, foe, blow, events);
                }

                int broken = face.Break + r.Outcome.BreakBonus;
                if (broken > 0 && r.Stands(state, foe))
                {
                    state = Break(state, actor, foeSide, foe, broken, r.EventUnit(foe), events);
                }
            }

            if (empowerUsed) state = Consume(state, actor, unit, StatusKind.Empower, events);
            return state;
        }

        /// <summary>
        /// The end of one blow on <paramref name="foe"/> that did not end the battle (the foe itself
        /// may have fallen): the opponent statuses of a face of several blows after its first, and a
        /// stance that gives a status on every attack.
        /// </summary>
        private static BattleState AfterBlow(BattleState state, Resolution r, int foe, int blow, List<BattleEvent> events)
        {
            // A face of several blows puts its opponent statuses on after the first
            // (二段斬り: 脆化 on the first blow, for the second).
            if (blow == 0 && r.Face.Hits >= 2 && r.Stands(state, foe))
            {
                foreach (var grant in r.Face.StatusList)
                {
                    if (grant.OnSelf || grant.Stacks <= 0) continue;
                    state = ApplyStatus(state, r.Actor, r.FoeSide, foe, r.EventUnit(foe), grant.Kind, r.Focus.StacksOf(grant), events, grant.Cap);
                }
            }

            // #253: a stance that gives a status on every attack does so blow by blow, so
            // the word is there for the next blow (脆化 makes it ×1.5).
            if (r.Stands(state, foe))
            {
                state = StanceStatusOnAttack(state, r.Actor, r.Unit, r.FoeSide, foe, events);
            }
            return state;
        }

        /// <summary>
        /// §2.2 Move: the mover's own move, then the push or pull of each foe hit (§7.3,
        /// <see cref="PushFoes"/>). Over is true when wall damage ended the battle; Resolve stops there.
        /// </summary>
        private static BattleState ResolveMovement(BattleState state, Resolution r, List<BattleEvent> events, out bool over)
        {
            over = false;
            if (AttributeRule.HasMovement(r.Face))
            {
                events.Add(new FaceResolved(r.Actor, r.Source.Id, FaceKind.Move) { Unit = r.Unit });
            }
            if (r.Face.Move != 0) state = MoveSelf(state, r.Actor, r.Unit, r.Face.Move, events);
            if (r.Face.Push == 0 || r.Hit.Count == 0) return state;
            return PushFoes(state, r, events, out over);
        }

        /// <summary>
        /// §2.2 Guard, then Skill: the Guard the card gives (威圧 taken off), then its heal (集中 grows it).
        /// </summary>
        private static BattleState ResolveGuardAndSkill(BattleState state, Resolution r, List<BattleEvent> events)
        {
            Actor actor = r.Actor;
            int unit = r.Unit;
            var attributes = r.Source.Attributes;

            // A Guard +n trait lands as Guard even on a card without a Guard face
            // (swordsman_cards_v4.md §1.2), and a move card may carry a small Guard of its own.
            if (attributes.HasFlag(BattleAttribute.Guard))
            {
                events.Add(new FaceResolved(actor, r.Source.Id, FaceKind.Guard) { Unit = unit });
            }
            int guardGain = r.GuardBase > 0 ? Math.Max(0, r.GuardBase - r.Intimidate) : 0;
            if (guardGain > 0)
            {
                var self = Get(state, actor, unit);
                self = self with { Guard = self.Guard + guardGain };
                state = Set(state, actor, unit, self);
                events.Add(new GuardGained(actor, guardGain, self.Guard) { Unit = unit });
            }

            if (attributes.HasFlag(BattleAttribute.Skill))
            {
                events.Add(new FaceResolved(actor, r.Source.Id, FaceKind.Skill) { Unit = unit });
            }
            int heal = r.Face.Heal > 0 ? r.Face.Heal + r.Focus.Heal : 0;
            if (heal > 0)
            {
                var self = Get(state, actor, unit);
                int hpAfter = Math.Min(self.MaxHp, self.Hp + heal);
                state = Set(state, actor, unit, self with { Hp = hpAfter });
                events.Add(new Healed(actor, hpAfter - self.Hp, hpAfter) { Unit = unit });
            }
            return state;
        }

        /// <summary>§5: the statuses the face and the traits give, then the face's stamina gain.</summary>
        private static BattleState ResolveStatuses(BattleState state, Resolution r, List<BattleEvent> events)
        {
            Actor actor = r.Actor;
            int unit = r.Unit;

            // §5: the face's statuses, then the trait's. The player holds at most six kinds; an enemy
            // has no kind limit. A ターンで減る型 word stops at four on either side (#205).
            // Statuses on the opponent need the blow to have reached them.
            // A face of several blows has put its opponent statuses on already, after the first blow.
            // 集中 grows the face's 出血 / 再生 to the next column; a trait's grant is not the column's.
            var grants = new List<StatusGrant>();
            foreach (var grant in r.Face.StatusList)
            {
                if (grant.OnSelf || r.Face.Hits < 2 || !r.Attacks) grants.Add(grant with { Stacks = r.Focus.StacksOf(grant) });
            }
            grants.AddRange(r.Outcome.GrantList);
            foreach (var grant in grants)
            {
                if (grant.Stacks <= 0) continue;
                if (grant.OnSelf)
                {
                    state = ApplyStatus(state, actor, actor, unit, unit, grant.Kind, grant.Stacks, events, grant.Cap);
                    continue;
                }
                foreach (int foe in r.Hit)
                {
                    if (!r.Stands(state, foe)) continue;
                    state = ApplyStatus(state, actor, r.FoeSide, foe, r.EventUnit(foe), grant.Kind, grant.Stacks, events, grant.Cap);
                }
            }
            if (r.Face.StaminaGain > 0) state = GainStamina(state, actor, unit, r.Face.StaminaGain, events);
            return state;
        }

        /// <summary>
        /// §4 (v4.4) Stance: the face stops the newest permanent effect of each opponent it reached
        /// (#334), then its own stance joins the list.
        /// </summary>
        private static BattleState ResolveStance(BattleState state, Resolution r, List<BattleEvent> events)
        {
            Actor actor = r.Actor;
            int unit = r.Unit;
            var source = r.Source;

            // #334 (roster §6.4 root_st): the face stops the newest permanent effect of each opponent
            // it reached. Like a status on the opponent, it needs the blow to have reached them.
            if (r.Face.StopStance)
            {
                foreach (int foe in r.Hit)
                {
                    if (!r.Stands(state, foe)) continue;
                    if (Combat.IsDefeated(Get(state, r.FoeSide, foe).Hp)) continue;
                    state = StopNewestStance(state, r.FoeSide, foe, r.EventUnit(foe), events);
                }
            }

            // §4 (v4.4): the stance face joins the list of permanent effects. Nothing ends and there
            // is no cap; the same stance twice is two entries and works twice.
            if (source.Attributes.HasFlag(BattleAttribute.Stance))
            {
                events.Add(new FaceResolved(actor, source.Id, FaceKind.Stance) { Unit = unit });
            }
            if (r.Face.Stance != null)
            {
                var self = Get(state, actor, unit);
                state = Set(state, actor, unit, self.WithStance(r.Face.Stance, source.Id));
                events.Add(new StanceSet(actor, source.Id, source.Name, r.Face.Stance) { Unit = unit });
            }
            return state;
        }
    }
}
