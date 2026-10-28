using System;
using System.Collections.Generic;
using System.Linq;

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
        /// §2.2: pay, judge the traits once, then Attack → Move → Guard → Skill → Stance. The same
        /// routine resolves a card and an enemy action, because both are written off the same face
        /// table.
        ///
        /// <paramref name="unit"/> is the acting enemy in an enemy phase, and the enemy whose N and
        /// Guard the card reads on the player's turn. <paramref name="foes"/> are the enemies a
        /// player's opponent-directed faces land on — none for a card aimed at nobody, one, or every
        /// one in reach for all (§7.4); an enemy's foe is always the player. A pushed line resolves
        /// the far foe first, a pulled one the near foe first, so that nobody is blocked by a
        /// neighbour that was about to move.
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
        private static BattleState Resolve(
            BattleState state,
            Actor actor,
            int unit,
            IReadOnlyList<int> foes,
            string sourceId,
            string sourceName,
            BattleAttribute attributes,
            int column,
            Face face,
            IReadOnlyList<Trait> traits,
            TargetKind targets,
            int cost,
            TraitContext context,
            IRng rng,
            List<BattleEvent> events)
        {
            Actor foeSide = Opponent(actor);
            int gap = state.GapTo(unit);

            // The traits are judged on the board from before the card (the context), before any face.
            var outcome = Traits.EvaluateAll(traits, context);
            int paid = Math.Max(0, cost - outcome.CostDown);

            var self = Get(state, actor, unit);
            self = self with { Stamina = self.Stamina - paid };
            state = Set(state, actor, unit, self);
            events.Add(new StaminaSpent(actor, paid, self.Stamina) { Unit = unit });

            // The whiff is known before anything resolves, and is announced first so that whoever
            // reads the stream knows the trait below is judged on a blow that will not land.
            bool directed = EnemyAi.IsOpponentDirected(attributes, face, targets);
            bool whiff = directed && !face.ReachOrDefault.Contains(gap);
            if (whiff)
            {
                if (actor == Actor.Player)
                {
                    throw new InvalidOperationException($"Card \"{sourceId}\" was resolved out of reach; CanPlay should have refused it.");
                }
                events.Add(new ActionWhiffed(actor, sourceId, gap, face.ReachOrDefault) { Unit = unit });
            }

            // The sides the opponent-directed faces land on: (the foe's unit; ignored for the player).
            var hit = new List<int>();
            if (actor == Actor.Enemy)
            {
                if (directed && !whiff) hit.Add(unit);
            }
            else
            {
                hit.AddRange(foes);
            }

            foreach (var trait in traits)
            {
                events.Add(new TraitEvaluated(actor, sourceId, trait, Traits.Evaluate(trait, context)) { Unit = unit });
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
                focus = FocusStep.Of(attributes, column, face);
                state = Consume(state, actor, unit, StatusKind.Focus, events);
            }
            bool attacks = attributes.HasFlag(BattleAttribute.Attack) && hit.Count > 0;
            int guardBase = face.Guard + focus.Guard + outcome.GuardBonus;
            int intimidate = 0;
            if (Get(state, actor, unit).Statuses.Has(StatusKind.Intimidate) && (attacks || guardBase > 0))
            {
                intimidate = Constants.IntimidatePenalty;
                state = Consume(state, actor, unit, StatusKind.Intimidate, events);
            }

            if (attacks)
            {
                events.Add(new FaceResolved(actor, sourceId, FaceKind.Attack) { Unit = unit });
                self = Get(state, actor, unit);
                int followUp = self.FollowUp;
                if (followUp > 0) state = Set(state, actor, unit, self with { FollowUp = 0 });
                int conversion = outcome.Convert ? Half(face.Guard + focus.Guard) : 0;
                bool empowered = self.Statuses.Has(StatusKind.Empower);
                bool empowerUsed = false;

                foreach (int foe in hit)
                {
                    if (foeSide == Actor.Enemy && !state.Enemies[foe].Alive) continue;

                    int stanceBonus = StanceAttackBonus(state, actor, unit, foeSide, foe, events);

                    // §2.4 hits (#189): each blow meets Guard on its own. §17.6 F6: 追撃 rides the
                    // first blow only and the trait's +n every blow; 強化 rides only the blows 脆化
                    // does not take (§5.1, §19.5 S13).
                    for (int blow = 0; blow < Math.Max(1, face.Hits); blow++)
                    {
                        if (foeSide == Actor.Enemy && !state.Enemies[foe].Alive) break;
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
                            face.Power + focus.Power - intimidate, outcome.PowerBonus + stanceBonus,
                            blow == 0 ? followUp : 0, conversion, mult);
                        var (damage, guardAfter, absorbed) = Combat.ApplyGuard(raw, other.Guard);
                        other = other with { Guard = guardAfter, Hp = Math.Max(0, other.Hp - damage) };
                        state = Set(state, foeSide, foe, other);
                        events.Add(new DamageDealt(actor, foeSide, raw, absorbed, damage, other.Guard, other.Hp) { Unit = foeSide == Actor.Enemy ? foe : unit });

                        if (!Combat.IsDefeated(other.Hp))
                        {
                            state = OnHitReactions(state, actor, unit, foeSide, foe, absorbed, events);
                        }
                        state = OnFall(state, foeSide, foe, events, out bool over);
                        if (over) return state;

                        // 見切り may have taken the attacker down.
                        if (Combat.IsDefeated(Get(state, actor, unit).Hp))
                        {
                            state = OnFall(state, actor, unit, events, out _);
                            return state;
                        }

                        // A face of several blows puts its opponent statuses on after the first
                        // (二段斬り: 脆化 on the first blow, for the second).
                        if (blow == 0 && face.Hits >= 2 && (foeSide == Actor.Player || state.Enemies[foe].Alive))
                        {
                            foreach (var grant in face.StatusList)
                            {
                                if (grant.OnSelf || grant.Stacks <= 0) continue;
                                state = ApplyStatus(state, actor, foeSide, foe, foeSide == Actor.Enemy ? foe : unit, grant.Kind, focus.StacksOf(grant), events, grant.Cap);
                            }
                        }

                        // #253: a stance that gives a status on every attack does so blow by blow, so
                        // the word is there for the next blow (脆化 makes it ×1.5).
                        if (foeSide == Actor.Player || state.Enemies[foe].Alive)
                        {
                            state = StanceStatusOnAttack(state, actor, unit, foeSide, foe, events);
                        }
                    }

                    int broken = face.Break + outcome.BreakBonus;
                    if (broken > 0 && (foeSide == Actor.Player || state.Enemies[foe].Alive))
                    {
                        state = Break(state, actor, foeSide, foe, broken, foeSide == Actor.Enemy ? foe : unit, events);
                    }
                }

                if (empowerUsed) state = Consume(state, actor, unit, StatusKind.Empower, events);
            }

            if (AttributeRule.HasMovement(face))
            {
                events.Add(new FaceResolved(actor, sourceId, FaceKind.Move) { Unit = unit });
            }
            if (face.Move != 0) state = MoveSelf(state, actor, unit, face.Move, events);
            if (face.Push != 0 && hit.Count > 0)
            {
                // §7.3: push (away) / pull (in) the opponent; Guard does not stop it. A large opponent
                // refuses it, and so does one standing in 錨の構え. The cells a push could not take
                // become wall damage; a pull just stops.
                int cells = Combat.CellsAfterSlow(Math.Abs(face.Push), Get(state, actor, unit).Statuses);
                bool blockedBySlow = false;
                foreach (int foe in PushOrder(state, foeSide, hit, face.Push))
                {
                    if (foeSide == Actor.Enemy && !state.Enemies[foe].Alive) continue;
                    var other = Get(state, foeSide, foe);
                    int eventUnit = foeSide == Actor.Enemy ? foe : unit;
                    if (other.Size >= 2)
                    {
                        events.Add(new PushRefused(foeSide, other.Size) { Unit = eventUnit });
                        continue;
                    }
                    var immune = other.StanceList.FirstOrDefault(entry => entry.Def.Hook == StanceHook.PushImmune && !entry.StoppedIn(state.Turn));
                    if (immune != null)
                    {
                        events.Add(new StanceFired(foeSide, immune.Source, StanceHook.PushImmune) { Unit = eventUnit });
                        events.Add(new PushRefused(foeSide, other.Size) { Unit = eventUnit, ByStance = true });
                        continue;
                    }
                    if (cells == 0)
                    {
                        if (!blockedBySlow) events.Add(new MoveBlocked(actor, StatusKind.Slow) { Unit = unit });
                        blockedBySlow = true;
                        continue;
                    }

                    // A positive Push sends the opponent away from us, which is "away" from their foe (−).
                    var shift = Field.Move(state, foeSide, -Math.Sign(face.Push) * cells, foe);
                    if (shift.To != shift.From)
                    {
                        other = other with { Cell = shift.To };
                        state = Set(state, foeSide, foe, other);
                        events.Add(new CellsMoved(foeSide, shift.From, shift.To, Pushed: true) { Unit = foe });
                    }
                    if (face.Push > 0 && shift.Blocked > 0)
                    {
                        state = RaiseScale(state, foeSide, events);
                        other = Get(state, foeSide, foe);
                        int raw = Field.WallDamage(shift.Blocked);
                        var (damage, guardAfter, absorbed) = Combat.ApplyGuard(raw, other.Guard);
                        other = other with { Guard = guardAfter, Hp = Math.Max(0, other.Hp - damage) };
                        state = Set(state, foeSide, foe, other);
                        events.Add(new WallHit(foeSide, shift.Blocked, raw, absorbed, damage, other.Guard, other.Hp) { Unit = foe });
                        state = OnFall(state, foeSide, foe, events, out bool over);
                        if (over) return state;
                    }
                }
            }

            // A Guard +n trait lands as Guard even on a card without a Guard face
            // (swordsman_cards_v4.md §1.2), and a move card may carry a small Guard of its own.
            if (attributes.HasFlag(BattleAttribute.Guard))
            {
                events.Add(new FaceResolved(actor, sourceId, FaceKind.Guard) { Unit = unit });
            }
            int guardGain = guardBase > 0 ? Math.Max(0, guardBase - intimidate) : 0;
            if (guardGain > 0)
            {
                self = Get(state, actor, unit);
                self = self with { Guard = self.Guard + guardGain };
                state = Set(state, actor, unit, self);
                events.Add(new GuardGained(actor, guardGain, self.Guard) { Unit = unit });
            }

            if (attributes.HasFlag(BattleAttribute.Skill))
            {
                events.Add(new FaceResolved(actor, sourceId, FaceKind.Skill) { Unit = unit });
            }
            int heal = face.Heal > 0 ? face.Heal + focus.Heal : 0;
            if (heal > 0)
            {
                self = Get(state, actor, unit);
                int hpAfter = Math.Min(self.MaxHp, self.Hp + heal);
                state = Set(state, actor, unit, self with { Hp = hpAfter });
                events.Add(new Healed(actor, hpAfter - self.Hp, hpAfter) { Unit = unit });
            }

            // §5: the face's statuses, then the trait's. The player holds at most six kinds; an enemy
            // has no kind limit. A ターンで減る型 word stops at four on either side (#205).
            // Statuses on the opponent need the blow to have reached them.
            // A face of several blows has put its opponent statuses on already, after the first blow.
            // 集中 grows the face's 出血 / 再生 to the next column; a trait's grant is not the column's.
            var grants = new List<StatusGrant>();
            foreach (var grant in face.StatusList)
            {
                if (grant.OnSelf || face.Hits < 2 || !attacks) grants.Add(grant with { Stacks = focus.StacksOf(grant) });
            }
            grants.AddRange(outcome.GrantList);
            foreach (var grant in grants)
            {
                if (grant.Stacks <= 0) continue;
                if (grant.OnSelf)
                {
                    state = ApplyStatus(state, actor, actor, unit, unit, grant.Kind, grant.Stacks, events, grant.Cap);
                    continue;
                }
                foreach (int foe in hit)
                {
                    if (foeSide == Actor.Enemy && !state.Enemies[foe].Alive) continue;
                    state = ApplyStatus(state, actor, foeSide, foe, foeSide == Actor.Enemy ? foe : unit, grant.Kind, grant.Stacks, events, grant.Cap);
                }
            }
            if (face.StaminaGain > 0) state = GainStamina(state, actor, unit, face.StaminaGain, events);

            // #334 (roster §6.4 root_st): the face stops the newest permanent effect of each opponent
            // it reached. Like a status on the opponent, it needs the blow to have reached them.
            if (face.StopStance)
            {
                foreach (int foe in hit)
                {
                    if (foeSide == Actor.Enemy && !state.Enemies[foe].Alive) continue;
                    if (Combat.IsDefeated(Get(state, foeSide, foe).Hp)) continue;
                    state = StopNewestStance(state, foeSide, foe, foeSide == Actor.Enemy ? foe : unit, events);
                }
            }

            // §4 (v4.4): the stance face joins the list of permanent effects. Nothing ends and there
            // is no cap; the same stance twice is two entries and works twice.
            if (attributes.HasFlag(BattleAttribute.Stance))
            {
                events.Add(new FaceResolved(actor, sourceId, FaceKind.Stance) { Unit = unit });
            }
            if (face.Stance != null)
            {
                self = Get(state, actor, unit);
                state = Set(state, actor, unit, self.WithStance(face.Stance, sourceId));
                events.Add(new StanceSet(actor, sourceId, sourceName, face.Stance) { Unit = unit });
            }

            int draw = face.Draw + outcome.Draw;
            if (draw > 0 && actor == Actor.Player)
            {
                state = DrawCards(state, draw, rng, events);
            }

            // 追撃 waits for the next attack face, not this card's own (§2.3).
            if (outcome.FollowUp > 0)
            {
                self = Get(state, actor, unit);
                state = Set(state, actor, unit, self with { FollowUp = self.FollowUp + outcome.FollowUp });
            }
            return state;
        }
    }
}
