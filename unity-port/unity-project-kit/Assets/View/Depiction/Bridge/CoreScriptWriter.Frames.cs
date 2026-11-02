// CoreScriptWriter (see CoreScriptWriter.cs): frames, the screen as the models stand after an event.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;

namespace Depiction.Bridge
{
    public sealed partial class CoreScriptWriter
    {
        // ---- frames -------------------------------------------------------------------------

        // js: CoreScriptWriter.js:1011
        internal DepictionEvent NewEvent(DepictionEventKind kind, string title)
        {
            _order += 1;
            _whiffed = false;
            _rebuildPending = false;
            return new DepictionEvent { Order = _order, Kind = kind, Title = title };
        }

        // js: CoreScriptWriter.js:1019
        internal void Close(DepictionEvent ev, BattleState after)
        {
            if (ev == null) return;
            ev.After = Snapshot(after);
        }

        // js: CoreScriptWriter.js:1025
        private DepictionFrame Snapshot(BattleState after)
        {
            var frame = new DepictionFrame
            {
                Outcome = after.Result == GameResult.Won ? BattleOutcome.Won
                    : after.Result == GameResult.Lost ? BattleOutcome.Lost
                    : BattleOutcome.Ongoing,
                // Floor and miasma belong to the exploration layer (#99); the chain position is the demo's (#191).
                Corner = new CornerFrame { Turn = _turn, Floor = 1, ChainIndex = ChainIndex, ChainTotal = ChainTotal, MiasmaPercent = 0 },
            };
            int focus = FocusUnit;
            frame.Player = UnitOf(_player, showStamina: true, gap: GapTo(focus));
            // #589: every enemy's share — its number panel, omen, cells and stripes. Frame.Enemy, Frame.Omen
            // and the floor's enemy cells are the focus enemy's share, the same objects (FloorOf).
            BattleState shown = null;
            for (int k = 0; k < _tracks.Count; k++)
            {
                EnemyTrack track = _tracks[k];
                // battle-visual-v1 §4.5 (#455): the enemy's stamina is on its number panel (a dot and the number).
                UnitFrame unit = UnitOf(track.Model, showStamina: true, gap: null);
                // #288: the figure wears the art filed under the enemy's id; the View only looks it up.
                unit.ArtId = track.Def.Id;
                // §4.5 構えの盾: solid while the stamina it holds meets 構え's threshold (残り 3 以上), the
                // rule's own Combat.ReserveGuard; an elite's panel takes the phosphor frame.
                unit.ReserveLikely = !track.Model.Down && Combat.ReserveGuard(track.Model.Stamina) > 0;
                unit.Elite = track.Def.Rank == EnemyRank.Elite;
                var enemy = new EnemyFrame
                {
                    Unit = unit,
                    Omen = WithPlan(k, track.OmenVisible ? OmenFrameOf(after, k) : new OmenFrame { Visible = false }),
                    // Where it stands and its own N.
                    Cell = track.Model.Cell,
                    Size = Math.Max(1, track.Model.Size),
                    Gap = Fallen(k) ? -1 : GapTo(k),
                    // battle_ui_ux_v2 §1.4: 45°, −45°, upright, and the swatch once there are two or more.
                    StripeDirection = EnemyFrame.DirectionOf(k),
                    Swatch = _tracks.Count >= 2,
                };
                if (track.OmenVisible && after.Enemies.Count > k)
                {
                    enemy.AimCells = FloorCells.AimOf(track.Omen, after.Enemies[k].Body, track.Model.Cell);
                    // #455: the thick stripes and the move's frame are the core's, read on the board as shown.
                    if (shown == null) shown = AsShown(after);
                    enemy.DenseCells = FloorCells.DenseOf(shown, k);
                    enemy.MoveCells = FloorCells.MoveOf(shown, k);
                }
                frame.Enemies.Add(enemy);
            }
            frame.Enemy = frame.Enemies[focus].Unit;
            frame.Omen = frame.Enemies[focus].Omen;
            // battle-visual-v1 §4.7 (#455): the dashed dots, the core's estimate of the next recovery.
            frame.Player.NextRecovery = TurnLoop.PreviewRecovery(AsShown(after));
            frame.Floor = FloorOf(after, frame.Enemies[focus], focus);
            // battle-visual-v1 §4.7 (#455): the piles' counts, as the move left them.
            frame.Piles = new PileFrame { Shown = true, Draw = after.DrawPile.Count, Discard = after.DiscardPile.Count, Exile = after.Exiled.Count };
            // battle_ui_ux_v2 §1.1 C4 (#455): what each pile holds, for the list a click on it opens.
            PileCards.Fill(frame.Piles, after);
            // battle-visual-v1 §4.6 (#455): both sides' stance lists, from the stances the models hold.
            frame.StanceListsShown = true;
            for (int k = 0; k < frame.Enemies.Count; k++)
            {
                OmenFrame omen = frame.Enemies[k].Omen;
                if (omen.Visible) omen.Hit = HitMark(k, frame.Enemies[k].AimCells);
                OmenDisclosure.Apply(omen, DisclosureOf(k));
            }
            bool waiting = _playerActs && after.Result == GameResult.Ongoing;
            // battle-visual-v1 §4.9 (#455): the attributes the player has played this turn, as the core
            // folds and keeps them (CombatantState.PlayedThisTurn, cleared when the turn ends).
            frame.Played = PlayedOf(after.Player.PlayedThisTurn);
            // battle_ui_ux_v2 §1.1 B6 (#455): a 追撃 waiting for the next attack face, as the core keeps it.
            frame.Played.FollowUp = after.Player.FollowUp;
            foreach (CardInstance card in _hand)
            {
                // Every number on the face is read against the enemy the card would go to (TargetOf);
                // with one enemy that is 0, the target the one-enemy writer read.
                int target = TargetOf(after, card.InstanceId);
                PlayPreview preview = TurnLoop.Preview(after, card.InstanceId, target);
                CardFace face = CoreText.Face(card, preview, CoreText.ReachesNobody(after, card.InstanceId));
                face.ReachCells = FloorCells.ReachOf(card.Def, _player.Cell, _player.Size, after.FieldCells);
                // battle-visual-v1 §6.3 (#242): lit when any enemy in reach satisfies the trait, with each
                // enemy's own verdict for the one a held card frames. A card the state no longer holds
                // (the hand mirrored here runs ahead of it mid-event) keeps the preview's lamp.
                List<bool> perEnemy;
                bool lit = CardFaceText.Lamps(after, card.InstanceId, out perEnemy);
                if (preview != null)
                {
                    face.TraitLit = lit;
                    face.LitOn = perEnemy;
                }
                // battle-visual-v1 §4.5 (#507): the HP the card would take off each enemy, wall included.
                face.HpLossOn = CardFaceText.HpLoss(after, card.InstanceId);
                // §4.9 / §5.3 (#455): the 構えの予告 once this card's cost is paid, the rule's own
                // Combat.ReserveGuard on what would be left.
                face.StanceHintAfterPay = waiting ? ReserveHint(_player.Stamina - face.Cost) : "";
                // §5.2's frames per enemy, and the receiver the card goes to unnamed.
                face.ReachOn = ReachOn(after, card.Def);
                face.TargetUnit = AimsAtOne(card.Def) ? target : -1;
                // §5.2 / §6.3: the cost and the 構えの予告 while the card frames each enemy.
                face.CostOn = CostOn(after, card, face.Cost);
                face.StanceHintAfterPayOn = face.CostOn.Select(cost => waiting ? ReserveHint(_player.Stamina - cost) : "").ToList();
                frame.Hand.Add(face);
            }
            frame.StanceHint = waiting ? ReserveHint(_player.Stamina) : "";
            // §5.5 (#455): the screen sinks below the throw line while the enemy has the turn.
            frame.EnemyTurn = _enemyTurn && after.Result == GameResult.Ongoing;
            frame.StanceDivider = Constants.ReserveThreshold; // §4.7: the stamina tag's divider
            return frame;
        }

        /// <summary>js: CoreScriptWriter.js:1123 — 構えの予告 for <paramref name="stamina"/> left at the turn end: "+3" when 構え would give Guard, "" when not.</summary>
        private static string ReserveHint(int stamina)
        {
            int reserve = Combat.ReserveGuard(Math.Max(0, stamina));
            return reserve > 0 ? "+" + reserve : "";
        }

        /// <summary>js: CoreScriptWriter.js:1129 — §4.9 (#455): the played attributes counted per kind, the last one marked. A card of movement only counts as none.</summary>
        private static PlayedFrame PlayedOf(IReadOnlyList<BattleAttribute> played)
        {
            var frame = new PlayedFrame { Shown = true };
            foreach (BattleAttribute attribute in played)
            {
                if (attribute == BattleAttribute.None) continue;
                frame.Add(CoreText.KindOf(attribute));
            }
            return frame;
        }

        /// <summary>
        /// js: CoreScriptWriter.js:1144 — battle-visual-v1 §4.2 (#242): the floor as the models stand —
        /// the line's width, both figures' cells, N, and the cells the omen on screen aims at (none while
        /// it is hidden). The enemy members are <paramref name="focus"/>'s share (#589: the same lists);
        /// every enemy's own cells are its EnemyFrame.
        /// </summary>
        private FloorFrame FloorOf(BattleState after, EnemyFrame focus, int unit)
        {
            return new FloorFrame
            {
                Cells = after.FieldCells,
                PlayerCell = _player.Cell,
                PlayerSize = Math.Max(1, _player.Size),
                EnemyCell = focus.Cell,
                EnemySize = focus.Size,
                Gap = GapTo(unit),
                AimCells = focus.AimCells,
                DenseCells = focus.DenseCells,
                MoveCells = focus.MoveCells,
            };
        }

        /// <summary>
        /// js: CoreScriptWriter.js:1185 — <paramref name="after"/> as the frame being written shows it
        /// (#455): the models' cells, the player's stamina and the omens on screen. A move's frames are
        /// all read against the state the whole move left, which can be past them — an EndTurn ends on
        /// the next omen, after the enemy has moved — so what the core reads for a frame is read on this.
        /// </summary>
        private BattleState AsShown(BattleState after)
        {
            if (after.Enemies.Count == 0) return after;
            BattleState shown = after with { Player = after.Player with { Cell = _player.Cell, Stamina = _player.Stamina } };
            for (int k = 0; k < after.Enemies.Count && k < _tracks.Count; k++)
            {
                EnemyTrack track = _tracks[k];
                EnemyUnit unit = after.Enemies[k];
                shown = shown.WithUnit(k, unit with { Body = unit.Body with { Cell = track.Model.Cell }, Omen = track.Omen ?? unit.Omen });
            }
            return shown;
        }

        /// <summary>
        /// js: CoreScriptWriter.js:1203 — battle-visual-v1 §4.4 当たり外れの印 (#242): the core's
        /// OmenPreview.Lands whenever the preview read belongs to the omen on screen and is not a rest —
        /// it plays the enemy's turn start first (a stage's Sway moves it), so it can differ from the cells
        /// drawn now. Otherwise the aimed cells against the player's near edge, as the core counts N
        /// (FloorCells.HitOf). None for an omen that aims at nobody. Per enemy.
        /// </summary>
        private OmenHit HitMark(int unit, IReadOnlyList<int> aim)
        {
            EnemyTrack track = _tracks[unit];
            if (track.Omen == null || track.Omen.Label.Reach == null) return OmenHit.None;
            if (track.OmenPreview != null && Equals(track.OmenPreviewFor, track.Omen) && !track.OmenPreview.Rests)
            {
                return track.OmenPreview.Lands ? OmenHit.Lands : OmenHit.Misses;
            }
            return FloorCells.HitOf(track.Omen, aim, _player.Cell, _player.Size);
        }

        /// <summary>
        /// js: CoreScriptWriter.js:1218 — the omen badge. Its number is the core's (TurnLoop.PreviewOmen),
        /// taken whenever the state read is the player's turn with the shown omen standing; otherwise the
        /// last number it was shown with stays, or the face alone before there is one. Per enemy, each
        /// read against its own enemy's actions.
        /// </summary>
        private OmenFrame OmenFrameOf(BattleState after, int unit)
        {
            EnemyTrack track = _tracks[unit];
            if (track.Omen != null && after.Phase == BattlePhase.PlayerAction && after.Enemies.Count > unit
                && Equals(after.Enemies[unit].Omen, track.Omen))
            {
                track.OmenPreview = TurnLoop.PreviewOmen(after, unit);
                track.OmenPreviewFor = track.Omen;
            }
            return CoreText.OmenOf(track.Omen, track.Def, Equals(track.OmenPreviewFor, track.Omen) ? track.OmenPreview : null);
        }

        /// <summary>
        /// js: CoreScriptWriter.js:1233 — #50: the 予定 beside the omen: its kind (icon and word), its
        /// cells, and for attack and guard the face number (#349). The second action's predicted value
        /// comes after this (#242). It outlives the spent first omen. Per enemy.
        /// </summary>
        private OmenFrame WithPlan(int unit, OmenFrame frame)
        {
            EnemyTrack track = _tracks[unit];
            if (!track.PlanVisible || track.Plan == null) return frame;
            OmenKind kind = track.Plan.Label.Kind;
            EnemyActionDef planned;
            track.Def.Actions.TryGetValue(track.Plan.ActionId, out planned);
            frame.PlanVisible = true;
            frame.PlanIcon = CoreText.IconOf(kind, planned);
            frame.PlanKindLabel = CoreText.OmenKindWord(kind);
            frame.PlanValueText = CoreText.OmenValueOf(kind, planned, null);
            frame.PlanStatuses = CoreText.OmenStatusesOf(kind, planned);
            frame.PlanSideGlyph = track.Plan.Label.Reach != null ? track.Plan.Label.Reach.ToText() : "";
            frame.PlanChanged = track.PlanChanged;
            return frame;
        }

        /// <summary>js: CoreScriptWriter.js:1249 — what a whiff strikes off: the number the omen badge was showing, or the face alone. Per enemy.</summary>
        private int ShownOmenPower(string actionId, int unit)
        {
            EnemyTrack track = _tracks[unit];
            if (track.OmenPreview != null && track.OmenPreviewFor != null && track.OmenPreviewFor.ActionId == actionId) return track.OmenPreview.RawPower;
            return CoreText.FacePower(track.Def.Actions[actionId].Face);
        }

        // js: CoreScriptWriter.js:1256
        private static UnitFrame UnitOf(UnitModel unit, bool showStamina, int? gap)
        {
            var frame = new UnitFrame
            {
                Hp = unit.Hp, HpMax = unit.HpMax, Guard = unit.Guard,
                ShowStamina = showStamina, Stamina = unit.Stamina, StaminaMax = unit.StaminaMax,
                HasRange = gap.HasValue, Down = unit.Down,
            };
            if (gap.HasValue) frame.RangeGlyph = CoreText.GapGlyph(gap.Value);
            // battle-visual-v1 §4.6 (#455, #605): the stances held are the list at the unit's edge, each
            // row with its hover panel text; none of them is a status chip.
            frame.Stances = StatusChipText.StanceRows(unit.Stances);
            foreach (KeyValuePair<StatusKind, int> pair in unit.Statuses)
            {
                frame.Statuses.Add(StatusChipText.Chip(pair.Key, pair.Value));
            }
            return frame;
        }

        /// <summary>js: CoreScriptWriter.js:1278 — the player, or enemy <paramref name="unit"/>.</summary>
        private UnitModel Unit(Actor actor, int unit)
        {
            return actor == Actor.Player ? _player : _tracks[unit].Model;
        }

        /// <summary>js: CoreScriptWriter.js:1283 — enemy <paramref name="unit"/>'s def (a stance's name is looked up in it), the first enemy's for a unit the writer does not know.</summary>
        private EnemyDef DefOf(int unit)
        {
            return unit >= 0 && unit < _tracks.Count ? _tracks[unit].Def : _tracks[0].Def;
        }
    }
}
