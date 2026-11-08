// CoreScriptWriter (see CoreScriptWriter.cs): the beats the event cases share (a trait, a blow, the wall,
// 構え) and the cues for stamina, statuses, HP and Guard.
using System;
using BattleCore;

namespace Depiction.Bridge
{
    public sealed partial class CoreScriptWriter
    {
        // js: CoreScriptWriter.js:696
        private void ApplyTrait(DepictionEvent ev, TraitEvaluated trait)
        {
            UnitSide side = CoreText.Side(trait.Actor);
            if (trait.Outcome.Triggered)
            {
                _traitPower = trait.Outcome.PowerBonus;
                _traitOfPower = trait.Trait;
                ev.Cues.Add(new Cue { Kind = CueKind.TraitFire, Target = side, Text = CoreText.TraitLine(trait.Trait), Unit = trait.Unit });
                return;
            }

            // An enemy blow that reads the gap and found the player outside its threshold: the screen
            // strikes the bonus off the omen badge, so stepping away (or in) visibly paid off. A blow
            // that whiffed has already had its whole number struck off.
            if (!_whiffed
                && trait.Actor == Actor.Enemy
                && (trait.Trait.Condition == BattleCore.TraitCondition.GapAtLeast
                    || trait.Trait.Condition == BattleCore.TraitCondition.GapAtMost)
                && trait.Trait.Effect == TraitEffect.PowerBonus)
            {
                ev.Cues.Add(new Cue
                {
                    Kind = CueKind.SideBonusMiss, Target = UnitSide.Enemy,
                    Amount = trait.Trait.Amount, Text = "+" + trait.Trait.Amount, Unit = trait.Unit,
                });
            }
        }

        /// <summary>
        /// js: CoreScriptWriter.js:723 — one attack, settled. When Guard swallows part of it — and for
        /// every enemy blow — it plays as three beats (swing, shield, wound) so each number has its own
        /// moment; otherwise one slash. Every beat carries the enemy of the exchange (DamageDealt.Unit).
        /// </summary>
        private void ApplyDamage(DepictionEvent ev, DamageDealt damage)
        {
            UnitSide source = CoreText.Side(damage.Actor);
            UnitSide target = CoreText.Side(damage.Target);
            UnitModel unit = Unit(damage.Target, damage.Unit);
            unit.Guard = damage.TargetGuardAfter;
            unit.Hp = damage.TargetHpAfter;

            // #248: the held card's number was its whole power before Guard; the played card shows the same sum.
            if (ev.Kind == DepictionEventKind.PlayCard && damage.Actor == Actor.Player && damage.Unit == _playedUnit)
            {
                _playedPower = Math.Max(0, _playedPower) + damage.Raw;
                ev.PreviewText = _playedPower.ToString();
            }

            string sideLabel = CoreText.HitLabel(_traitPower, _traitOfPower, damage.Multiplier,
                StanceTimesOf(damage.PowerStance, damage.Actor, damage.Unit),
                StanceTimesOf(damage.TakenStance, damage.Target, damage.Unit));
            bool splits = damage.Absorbed > 0 || damage.Actor == Actor.Enemy;
            if (!splits)
            {
                ev.Cues.Add(new Cue
                {
                    Kind = CueKind.Slash, Source = source, Target = target, System = _strike,
                    Fx = EffectGrammar.FromStrike(_strike), TraitAccent = _traitLit,
                    Amount = damage.Raw, Intensity = EffectStrength.Uniform, HpAfter = damage.TargetHpAfter, Unit = damage.Unit,
                    SideLabel = sideLabel,
                });
                return;
            }

            ev.Cues.Add(new Cue
            {
                Kind = CueKind.Slash, Source = source, Target = target, System = _strike,
                Fx = EffectGrammar.FromStrike(_strike), TraitAccent = _traitLit,
                Amount = damage.Raw, Intensity = EffectStrength.Uniform, Unit = damage.Unit,
            });
            if (damage.Absorbed > 0)
            {
                ev.Cues.Add(new Cue
                {
                    Kind = CueKind.GuardBlock, Target = target,
                    Amount = damage.Absorbed, GuardAfter = damage.TargetGuardAfter, Unit = damage.Unit,
                });
            }
            if (damage.Damage > 0)
            {
                ev.Cues.Add(new Cue
                {
                    Kind = CueKind.Hit, Target = target,
                    Amount = damage.Damage, Intensity = EffectStrength.Uniform, HpAfter = damage.TargetHpAfter, Unit = damage.Unit,
                    SideLabel = sideLabel,
                });
            }
        }

        /// <summary>js: CoreScriptWriter.js:766 — §7.3 壁のダメージ: the shield and the wound, like an enemy blow without its swing.</summary>
        private void ApplyWallHit(DepictionEvent ev, WallHit wall)
        {
            UnitSide target = CoreText.Side(wall.Actor);
            UnitModel unit = Unit(wall.Actor, wall.Unit);
            unit.Guard = wall.GuardAfter;
            unit.Hp = wall.HpAfter;
            // The wall a card's push drives the enemy into is part of the card's sum (TurnLoop.Preview).
            if (ev.Kind == DepictionEventKind.PlayCard && wall.Actor == Actor.Enemy && wall.Unit == _playedUnit && _playedPower >= 0)
            {
                _playedPower += wall.Raw;
                ev.PreviewText = _playedPower.ToString();
            }
            if (wall.Absorbed > 0)
            {
                ev.Cues.Add(new Cue { Kind = CueKind.GuardBlock, Target = target, Amount = wall.Absorbed, GuardAfter = wall.GuardAfter, Unit = wall.Unit });
            }
            if (wall.Damage > 0)
            {
                ev.Cues.Add(new Cue
                {
                    Kind = CueKind.Hit, Target = target,
                    Amount = wall.Damage, Intensity = EffectStrength.Uniform, HpAfter = wall.HpAfter, Unit = wall.Unit,
                });
            }
        }

        // js: CoreScriptWriter.js:788
        private void ApplyReserve(DepictionEvent ev, ReserveChecked reserve)
        {
            if (reserve.Actor == Actor.Player)
            {
                _player.Guard = reserve.GuardAfter;
                if (reserve.GuardGained > 0)
                {
                    ev.Cues.Add(new Cue
                    {
                        Kind = CueKind.StanceCue, Target = UnitSide.Player,
                        Amount = reserve.GuardGained, Text = "+" + reserve.GuardGained, GuardAfter = reserve.GuardAfter,
                    });
                }
                return;
            }

            // The enemy's 構え would push its own action past the 2.0 s an event may take (a shove is
            // already windup + swing + shield + wound + the push). It plays with the next omen instead,
            // and the Guard number on screen waits with it.
            if (reserve.GuardGained > 0) _tracks[reserve.Unit].ReserveHeld = reserve;
        }

        /// <summary>js: CoreScriptWriter.js:807 — every enemy's held 構え at the head of <paramref name="ev"/>, the first enemy's first.</summary>
        private void FlushHeldReserves(DepictionEvent ev)
        {
            for (int k = _tracks.Count - 1; k >= 0; k--) FlushHeldReserve(ev, k);
        }

        // js: CoreScriptWriter.js:812
        private void FlushHeldReserve(DepictionEvent ev, int unit)
        {
            if (unit < 0 || unit >= _tracks.Count) return;
            EnemyTrack track = _tracks[unit];
            if (track.ReserveHeld == null) return;
            ReserveChecked reserve = track.ReserveHeld;
            track.ReserveHeld = null;
            track.Model.Guard = reserve.GuardAfter;
            // First in the event: the shield goes up, then the omen it stands behind appears.
            Cue cue = GuardGain(Actor.Enemy, reserve.GuardGained, reserve.GuardAfter);
            cue.Unit = unit;
            ev.Cues.Insert(0, cue);
        }

        /// <summary>js: CoreScriptWriter.js:825 — writes the pips' cue and hands it back; null when nothing is shown (the enemy's stamina, no change).</summary>
        private Cue SetStamina(DepictionEvent ev, Actor actor, int unitIndex, int amount, int staminaAfter)
        {
            UnitModel unit = Unit(actor, unitIndex);
            unit.Stamina = staminaAfter;
            // Only the player's pips play a cue. The enemy's stamina is on its number panel since #455,
            // but it settles with the event's frame: battle_ui_ux_v2 §5.5 順 0 (its pips going by the cost
            // before the blow) is not played yet.
            if (actor != Actor.Player || amount == 0) return null;
            // #588: a held trait gain the core settled before this change plays first, so the pips
            // never step back (息継ぎ's own +1 lands after the trait's).
            ReleaseTraitStamina(ev);
            var cue = new Cue
            {
                Kind = CueKind.StaminaChange, Target = UnitSide.Player,
                Amount = amount, StaminaAfter = staminaAfter, StaminaMax = unit.StaminaMax,
            };
            ev.Cues.Add(cue);
            return cue;
        }

        /// <summary>
        /// battle_ui_ux_v2 §5.9 (#588): plays the held trait stamina beat at the end of the cues written so
        /// far. Called when the card's event closes, and before the next beat that has to come after it:
        /// a draw (the later group's next row) or another stamina change (the pips keep the core's order).
        /// </summary>
        private void ReleaseTraitStamina(DepictionEvent ev)
        {
            if (_traitStaminaHeld == null || ev == null) return;
            ev.Cues.Add(_traitStaminaHeld);
            _traitStaminaHeld = null;
        }

        // js: CoreScriptWriter.js:841
        private Cue SetStatus(DepictionEvent ev, Actor holder, int unitIndex, StatusKind kind, int change, int stacksAfter)
        {
            UnitModel unit = Unit(holder, unitIndex);
            if (stacksAfter > 0) unit.Statuses[kind] = stacksAfter;
            else unit.Statuses.Remove(kind);
            var cue = new Cue
            {
                Kind = CueKind.StatusChange, Target = CoreText.Side(holder),
                Amount = change, Text = kind.ToLabel(), StacksAfter = stacksAfter, Unit = unitIndex,
            };
            ev.Cues.Add(cue);
            return cue;
        }

        /// <summary>js: CoreScriptWriter.js:854 — HP moved without a blow: 出血 / 再生, a heal, a 見切り return. The bar follows the settled value.</summary>
        private Cue SetHp(DepictionEvent ev, Actor actor, int unitIndex, int amount, int hpAfter, string cause)
        {
            Unit(actor, unitIndex).Hp = hpAfter;
            if (amount == 0) return null;
            var cue = new Cue
            {
                Kind = CueKind.HpChange, Target = CoreText.Side(actor),
                Amount = amount, HpAfter = hpAfter, Text = cause, Unit = unitIndex,
            };
            ev.Cues.Add(cue);
            return cue;
        }

        /// <summary>
        /// js: CoreScriptWriter.js:871 — §5.3 (#57): marks a beat that a card or an enemy action plays
        /// with its system, at the one strength every beat has (#30). A beat outside a play (a turn
        /// start's tick, the reserve's Guard) and a loss (a spent pip, a lost stack) keep none. Hands the
        /// cue back, null stays null.
        /// </summary>
        private Cue Grammar(DepictionEvent ev, Cue cue, FxSystem system)
        {
            if (cue == null || !InPlay(ev)) return cue;
            bool counted = cue.Kind == CueKind.StaminaChange || cue.Kind == CueKind.HpChange || cue.Kind == CueKind.StatusChange;
            if (counted && cue.Amount <= 0) return cue;
            cue.Fx = system;
            cue.Intensity = EffectStrength.Uniform;
            return cue;
        }

        /// <summary>The event is a card being played, or the enemy carrying out the action it chose.</summary>
        // js: CoreScriptWriter.js:881
        private bool InPlay(DepictionEvent ev)
        {
            return ev.Kind == DepictionEventKind.PlayCard || (ev.Kind == DepictionEventKind.EnemyAction && _enemyActed);
        }

        /// <summary>js: CoreScriptWriter.js:886 — the display name of a stance's source: a card for the player, an action of the enemy's.</summary>
        /// <summary>#951: a blow's Multiplier stance as the side label names it; the enemy's comes from its own actions.</summary>
        private (int Times, string Name)? StanceTimesOf(StanceTimes stance, Actor holder, int unit)
        {
            if (stance == null) return null;
            return (stance.Times, StanceName(stance.SourceId, holder == Actor.Enemy ? DefOf(unit) : null));
        }

        private static string StanceName(string sourceId, EnemyDef enemy)
        {
            if (string.IsNullOrEmpty(sourceId)) return "";
            EnemyActionDef action;
            if (enemy != null && enemy.Actions.TryGetValue(sourceId, out action)) return action.Name;
            foreach (CardDef def in CardCatalog.All)
            {
                if (def.Id == sourceId) return def.Name;
            }
            return sourceId;
        }

        // js: CoreScriptWriter.js:896
        private static Cue GuardGain(Actor actor, int amount, int guardAfter)
        {
            return new Cue
            {
                Kind = CueKind.GuardGain, Target = CoreText.Side(actor),
                Amount = amount, Intensity = EffectStrength.Uniform, GuardAfter = guardAfter,
            };
        }
    }
}
