// CoreScriptWriter (see CoreScriptWriter.cs): one core event to its beats. ApplyEvent only picks the
// event's case; a case of more than one statement is a function of its own below, named after the
// event, its body as it stood in the switch.
using System;
using BattleCore;

namespace Depiction.Bridge
{
    public sealed partial class CoreScriptWriter
    {
        // ---- one core event → its beats -----------------------------------------------------

        /// <summary>
        /// One core event's beats. #589: each beat says which enemy it is about, the event's own enemy end
        /// (BattleEvent.Unit: the enemy acting, or the one a card read or hit; 0 for the player alone).
        /// </summary>
        internal void Apply(BattleEvent e, DepictionEvent ev)
        {
            int before = ev.Cues.Count;
            ApplyEvent(e, ev);
            for (int i = before; i < ev.Cues.Count; i++) ev.Cues[i].Unit = e.Unit;
        }

        // js: CoreScriptWriter.js:493
        private void ApplyEvent(BattleEvent e, DepictionEvent ev)
        {
            switch (e)
            {
                case GuardCleared cleared:
                    ApplyGuardCleared(ev, cleared);
                    break;
                case StaminaRecovered recovered:
                    SetStamina(ev, recovered.Actor, recovered.Unit, recovered.Amount, recovered.StaminaAfter);
                    break;
                case StaminaSpent spent:
                    SetStamina(ev, spent.Actor, spent.Unit, -spent.Amount, spent.StaminaAfter);
                    break;
                case StaminaGained gained:
                    ApplyStaminaGained(ev, gained);
                    break;
                case StatusTicked ticked:
                    SetStatus(ev, ticked.Actor, ticked.Unit, ticked.Kind, -1, ticked.StacksAfter);
                    break;
                case StatusApplied applied:
                    ApplyStatusApplied(ev, applied);
                    break;
                case MaxStaminaChanged lowered:
                    ApplyMaxStaminaChanged(ev, lowered);
                    break;
                case StatusCleared cleared:
                    // #51 root_sk: the enemy shook the word off.
                    SetStatus(ev, cleared.Actor, cleared.Unit, cleared.Kind, -cleared.Stacks, 0);
                    break;
                case Drawn drawn:
                    ApplyDrawn(ev, drawn);
                    break;
                // §3.14 山札切れ (#455): the discard pile is shuffled into the draw pile; the next Drawn is its first card.
                case DeckReshuffled _:
                    _rebuildPending = true;
                    break;
                // battle-visual-v1 §4.7 / battle_ui_ux_v2 §5.11 順 4 (#455): the played stance card goes to the exile pile.
                case CardExiled exiled when exiled.Actor == Actor.Player && ev.Kind == DepictionEventKind.PlayCard:
                    ev.PlayedTo = PileKind.Exile;
                    break;
                case OmenSet omen:
                    ApplyOmenSet(ev, omen);
                    break;
                case CardPlayed played:
                    ApplyCardPlayed(ev, played);
                    break;
                // #50: the plan is shown from the omen until the second action acts; a phase that stops
                // at one action (a rest, a stop at the first blow) replaces it with 予定変更.
                case PlanSet planned:
                    ApplyPlanSet(planned);
                    break;
                case PlanChanged changed:
                    ApplyPlanChanged(ev, changed);
                    break;
                case ActionExecuted executed:
                    ApplyActionExecuted(ev, executed);
                    break;
                case Rested _:
                    ev.Title = "敵は動けない";
                    break;
                case TraitEvaluated trait:
                    if (trait.Outcome.Triggered) _traitLit = true;
                    ApplyTrait(ev, trait);
                    break;
                case DamageDealt damage:
                    ApplyDamage(ev, damage);
                    break;
                case GuardGained guard:
                    ApplyGuardGained(ev, guard);
                    break;
                case CellsMoved moved:
                    ApplyCellsMoved(ev, moved);
                    break;
                case WallHit wall:
                    ApplyWallHit(ev, wall);
                    break;
                case MoveBlocked blocked:
                    ApplyMoveBlocked(ev, blocked);
                    break;
                case ActionWhiffed whiff:
                    ApplyActionWhiffed(ev, whiff);
                    break;
                case ReserveChecked reserve:
                    ApplyReserve(ev, reserve);
                    break;
                case HandDiscarded discarded:
                    ApplyHandDiscarded(ev, discarded);
                    break;
                // The demo vocabulary (#188): an event that matched none of the cases above (CardExiled
                // outside a played card included) reads its cases next, in the order one switch had them.
                default:
                    ApplyDemoVocabulary(e, ev);
                    break;
            }

            // The omen is spent once the enemy has acted on it (or rested through it).
            if (e is ActionExecuted || e is Rested) _tracks[e.Unit].OmenVisible = false;
        }

        // ---- the demo vocabulary (#188) ----

        /// <summary>ApplyEvent's cases for the demo vocabulary (#188); an event of any other type writes nothing here.</summary>
        private void ApplyDemoVocabulary(BattleEvent e, DepictionEvent ev)
        {
            switch (e)
            {
                case StatusConsumed consumed:
                    SetStatus(ev, consumed.Actor, consumed.Unit, consumed.Kind, -1, consumed.StacksAfter);
                    break;
                case StatusHpChanged changed:
                    SetHp(ev, changed.Actor, changed.Unit, changed.Amount, changed.HpAfter, changed.Kind.ToLabel());
                    break;
                case Healed healed:
                    Grammar(ev, SetHp(ev, healed.Actor, healed.Unit, healed.Amount, healed.HpAfter, "回復"), FxSystem.Breath);
                    break;
                case StaminaBroken broken:
                    SetStamina(ev, broken.Target, broken.Unit, -broken.Amount, broken.StaminaAfter);
                    break;
                case Reflected reflected:
                    ApplyReflected(ev, reflected);
                    break;
                case StanceSet set:
                    ApplyStanceSet(ev, set);
                    break;
                // #334 (roster §6.4 root_st): one permanent effect stops for a turn, then comes back.
                case StanceStopped stopped:
                    ApplyStanceStopped(ev, stopped);
                    break;
                case StanceResumed resumed:
                    ApplyStanceResumed(ev, resumed);
                    break;
                // #288: an enemy that falls with others standing is EnemyDefeated; the last one to fall
                // ends the battle instead (TurnLoop.OnFall), and BattleEnded is all that says so — for every
                // enemy whose fall has not been written yet (the one-enemy writer marked the first enemy only).
                case EnemyDefeated fell:
                    QueueFall(fell.Unit);
                    break;
                case BattleEnded ended when ended.Result == GameResult.Won:
                    for (int k = 0; k < _tracks.Count; k++) QueueFall(k);
                    break;
            }
        }

        // ---- one function per event (ApplyEvent's longer cases) -----------------------------

        private void ApplyGuardCleared(DepictionEvent ev, GuardCleared cleared)
        {
            UnitModel unit = Unit(cleared.Actor, cleared.Unit);
            unit.Guard = 0;
            if (cleared.Cleared > 0)
            {
                ev.Cues.Add(new Cue
                {
                    Kind = CueKind.GuardReset, Target = CoreText.Side(cleared.Actor), GuardAfter = 0,
                    Unit = cleared.Unit,
                });
            }
        }

        private void ApplyStaminaGained(DepictionEvent ev, StaminaGained gained)
        {
            Cue gain = Grammar(ev, SetStamina(ev, gained.Actor, gained.Unit, gained.Amount, gained.StaminaAfter), FxSystem.Breath);
            // §5.9 (#588): a fired trait's stamina +1 is shown after all the faces, not where the
            // core adds it (straight after the traits are judged). The model already holds it, so
            // the frame is right whenever the beat plays.
            if (gain != null && _afterTraits)
            {
                ev.Cues.Remove(gain);
                gain.Unit = gained.Unit;
                _traitStaminaHeld = gain;
            }
        }

        private void ApplyStatusApplied(DepictionEvent ev, StatusApplied applied)
        {
            // #205: a ターンで減る型 word stops at its cap, so what landed can be less than what
            // was given, or nothing; the core says how much.
            if (applied.Landed > 0)
            {
                Grammar(ev, SetStatus(ev, applied.Target, applied.Unit, applied.Kind, applied.Landed, applied.StacksAfter), FxSystem.Spirit);
            }
        }

        private void ApplyMaxStaminaChanged(DepictionEvent ev, MaxStaminaChanged lowered)
        {
            // #51 瘴気纏い: the pips lose their top, and what is held is cut to the new maximum.
            UnitModel unit = Unit(lowered.Actor, lowered.Unit);
            int cut = lowered.StaminaAfter - unit.Stamina;
            unit.StaminaMax = lowered.MaxAfter;
            SetStamina(ev, lowered.Actor, lowered.Unit, cut, lowered.StaminaAfter);
        }

        private void ApplyDrawn(DepictionEvent ev, Drawn drawn)
        {
            // §5.9's later group in its table's order: the trait's stamina +1, then the draw.
            ReleaseTraitStamina(ev);
            _hand.Add(drawn.Card);
            // A hand of six is six core events and one beat on screen.
            Cue last = ev.Cues.Count > 0 ? ev.Cues[ev.Cues.Count - 1] : null;
            if (last == null || last.Kind != CueKind.DrawHand)
            {
                last = Grammar(ev, new Cue { Kind = CueKind.DrawHand, Target = UnitSide.Player, Amount = 0 }, FxSystem.Breath);
                ev.Cues.Add(last);
            }
            // §3.14 (#455): the core made the draw pile again just before this card; the cards
            // already drawn in this beat came off the old pile.
            if (_rebuildPending && last.RebuildAfter == Cue.Unchanged) last.RebuildAfter = last.Amount;
            _rebuildPending = false;
            last.Amount += 1;
        }

        private void ApplyOmenSet(DepictionEvent ev, OmenSet omen)
        {
            EnemyTrack track = _tracks[omen.Unit];
            bool alreadyShown = track.OmenVisible && Equals(track.Omen, omen.Omen);
            // Step 12's omen is a new one even when it is the same action: the old number goes.
            if (omen.Decided)
            {
                track.OmenPreviewFor = null;
                track.Plan = null; // #50: the plan of the phase that just ended; a PlanSet follows if the new omen has one.
                track.PlanVisible = false;
                track.PlanChanged = false;
            }
            track.Omen = omen.Omen;
            track.OmenVisible = true;
            // Step 5 re-announces the omen step 12 already put up; the screen shows it once.
            if (!alreadyShown) ev.Cues.Add(new Cue { Kind = CueKind.OmenShow, Target = UnitSide.Enemy, Unit = omen.Unit });
        }

        private void ApplyCardPlayed(DepictionEvent ev, CardPlayed played)
        {
            _hand.Remove(played.Card);
            _strike = CoreText.SystemOf(played.Card.Def);
            _traitLit = false;
            _traitPower = 0;
            _traitOfPower = null;
            ev.Systems.Clear();
            ev.Systems.AddRange(CardSystems.Of(played.Card.Def));
            _playedPower = -1;
            _playedUnit = played.Unit;
        }

        private void ApplyPlanSet(PlanSet planned)
        {
            EnemyTrack track = _tracks[planned.Unit];
            track.Plan = planned.Plan;
            track.PlanVisible = true;
            track.PlanChanged = false;
        }

        private void ApplyPlanChanged(DepictionEvent ev, PlanChanged changed)
        {
            EnemyTrack track = _tracks[changed.Unit];
            track.Plan = changed.Now;
            track.PlanVisible = true;
            track.PlanChanged = true;
            // #455: an enemy read to 0 shows no plan (battle_ui_ux_v2 §7), so its change has nothing to pop.
            if (OmenDisclosure.ShowsPlan(DisclosureOf(changed.Unit)))
            {
                ev.Cues.Add(new Cue { Kind = CueKind.OmenShow, Target = UnitSide.Enemy, Text = DepictionText.PlanChangedTag, Unit = changed.Unit });
            }
        }

        private void ApplyActionExecuted(DepictionEvent ev, ActionExecuted executed)
        {
            if (_enemyActed) _tracks[executed.Unit].PlanVisible = false; // the second action of the phase spends the plan
            _enemyActed = true;
            ev.Title = executed.Action.Name;
            _strike = CoreText.SystemOf(executed.Action);
            _traitLit = false;
            _traitPower = 0;
            _traitOfPower = null;
            ev.Systems.Clear();
            ev.Systems.AddRange(CardSystems.Of(executed.Action));
            ev.Cues.Add(new Cue { Kind = CueKind.EnemyWindup, Target = UnitSide.Enemy, System = _strike, Unit = executed.Unit });
        }

        private void ApplyGuardGained(DepictionEvent ev, GuardGained guard)
        {
            Unit(guard.Actor, guard.Unit).Guard = guard.GuardAfter;
            Cue cue = GuardGain(guard.Actor, guard.Amount, guard.GuardAfter);
            cue.Unit = guard.Unit;
            ev.Cues.Add(Grammar(ev, cue, FxSystem.Shield));
        }

        private void ApplyCellsMoved(DepictionEvent ev, CellsMoved moved)
        {
            // Whichever side moved, the screen shows the walk on the floor: both figures to the
            // cells they stand on after it, and the new N.
            Unit(moved.Actor, moved.Unit).Cell = moved.To;
            int focus = FocusUnit;
            var cue = new Cue
            {
                Kind = CueKind.RangeSwitch, Target = UnitSide.Player, Pushed = moved.Pushed,
                RangeGlyphAfter = CoreText.GapGlyph(Gap),
                // battle-visual-v1 §4.2 (#242): where both stand after the move, for the floor.
                PlayerCellAfter = _player.Cell, EnemyCellAfter = _tracks[focus].Model.Cell, GapAfter = Gap,
                // battle_ui_ux_v2 §5.7 順 1 (#586): the walk takes 160 ms a cell.
                CellsMoved = Math.Abs(moved.To - moved.From),
            };
            // The enemy that moved, and where it stands now.
            if (moved.Actor == Actor.Enemy)
            {
                cue.Unit = moved.Unit;
                cue.UnitCellAfter = _tracks[moved.Unit].Model.Cell;
                cue.UnitGapAfter = GapTo(moved.Unit);
            }
            ev.Cues.Add(cue);
        }

        private void ApplyMoveBlocked(DepictionEvent ev, MoveBlocked blocked)
        {
            // #335: 呪縛 / 鉤爪 stopped the move; nothing moved, so the line is all there is to show.
            string line = CoreText.MoveBlockedLine(blocked.By);
            if (line.Length > 0)
            {
                ev.Cues.Add(new Cue
                {
                    Kind = CueKind.TraitFire, Target = CoreText.Side(blocked.Actor), Text = line,
                    Unit = blocked.Unit,
                });
            }
        }

        private void ApplyActionWhiffed(DepictionEvent ev, ActionWhiffed whiff)
        {
            // The blow finds nobody: the omen's number is struck off, the way a missed side bonus is.
            _whiffed = true;
            ev.Cues.Add(new Cue
            {
                Kind = CueKind.SideBonusMiss, Target = UnitSide.Enemy,
                Amount = ShownOmenPower(whiff.SourceId, whiff.Unit), Text = "空振り", Unit = whiff.Unit,
            });
        }

        private void ApplyHandDiscarded(DepictionEvent ev, HandDiscarded discarded)
        {
            _hand.Clear();
            if (discarded.Count > 0)
            {
                ev.Cues.Add(new Cue { Kind = CueKind.DiscardHand, Target = UnitSide.Player, Amount = discarded.Count });
            }
        }

        private void ApplyReflected(DepictionEvent ev, Reflected reflected)
        {
            UnitModel struck = Unit(reflected.Target, reflected.Unit);
            struck.Guard = reflected.TargetGuardAfter;
            if (reflected.Absorbed > 0)
            {
                ev.Cues.Add(new Cue
                {
                    Kind = CueKind.GuardBlock, Target = CoreText.Side(reflected.Target),
                    Amount = reflected.Absorbed, GuardAfter = reflected.TargetGuardAfter, Unit = reflected.Unit,
                });
            }
            SetHp(ev, reflected.Target, reflected.Unit, -reflected.Damage, reflected.TargetHpAfter, "見切り");
        }

        private void ApplyStanceSet(DepictionEvent ev, StanceSet set)
        {
            Unit(set.Actor, set.Unit).Stances.Add(set.Name);
            ev.Cues.Add(Grammar(ev, new Cue
            {
                Kind = CueKind.TraitFire, Target = CoreText.Side(set.Actor), Text = "構え・" + set.Name,
                Unit = set.Unit,
            }, FxSystem.Form));
        }

        private void ApplyStanceStopped(DepictionEvent ev, StanceStopped stopped)
        {
            ev.Cues.Add(new Cue
            {
                Kind = CueKind.TraitFire, Target = CoreText.Side(stopped.Actor),
                Text = CoreText.StanceStoppedLine(StanceName(stopped.SourceId, DefOf(stopped.Unit))),
                Unit = stopped.Unit,
            });
        }

        private void ApplyStanceResumed(DepictionEvent ev, StanceResumed resumed)
        {
            ev.Cues.Add(new Cue
            {
                Kind = CueKind.TraitFire, Target = CoreText.Side(resumed.Actor),
                Text = CoreText.StanceResumedLine(StanceName(resumed.SourceId, DefOf(resumed.Unit))),
                Unit = resumed.Unit,
            });
        }
    }
}
