// DepictionPlayer's parts: each enemy's parts, the parts made at run time, and what keeps them beside the figures (#585).
#if UNITY_2021_2_OR_NEWER
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    public partial class DepictionPlayer
    {
        // ---- battle_ui_ux_v2 §2.2 (#589): the enemies' parts ----------------------------------------------

        /// <summary>
        /// Takes copies of the enemy's parts before the parts lay themselves out. Each view builds its
        /// children once, in its Awake, and keeps them in fields no copy gets, so a copy of a live part would
        /// build them all a second time on top of the first. This runs first (DefaultExecutionOrder), and
        /// under an inactive holder a copy wakes nothing until it is moved out.
        /// </summary>
        private void TakeEnemyTemplates()
        {
            if (!enemyFigure || !enemyStatus || !omenBadge) return;
            var holder = new GameObject("EnemyTemplates");
            holder.SetActive(false);
            holder.transform.SetParent(transform, false);
            _figureTemplate = Instantiate(enemyFigure, holder.transform);
            _statusTemplate = Instantiate(enemyStatus, holder.transform);
            _badgeTemplate = Instantiate(omenBadge, holder.transform);
        }

        /// <summary>
        /// Readies the parts for a battle of <paramref name="count"/> enemies: further parts are copied from the
        /// templates, and those beyond the count go hidden until a battle needs them again. The next bind
        /// brings a figure back (FigureView.SetDown stands up whatever is not active).
        /// </summary>
        private void EnsureEnemies(int count)
        {
            while (_enemies.Count < count)
            {
                if (!_figureTemplate)
                {
                    Debug.LogError("[Depiction] the scene has no enemy parts to copy for a battle of " + count + " enemies; only the first is shown");
                    break;
                }
                _enemies.Add(CopyEnemy(_enemies.Count));
            }
            int shown = Mathf.Clamp(count, 1, _enemies.Count);
            if (shown == _enemyCount && _enemies.Count == shown) return;
            for (int k = 1; k < _enemies.Count; k++)
            {
                bool used = k < shown;
                if (!used) _enemies[k].Figure.gameObject.SetActive(false);
                _enemies[k].Status.gameObject.SetActive(used);
                _enemies[k].Badge.gameObject.SetActive(used);
            }
            if (shown != _enemyCount)
            {
                _snapPanels = true;
                _snapBadges = true;
            }
            _enemyCount = shown;
        }

        private EnemyParts CopyEnemy(int k)
        {
            return new EnemyParts
            {
                Figure = CopyPart(_figureTemplate, enemyFigure.transform, "EnemyFigure" + (k + 1)),
                Status = CopyPart(_statusTemplate, enemyStatus.transform, "EnemyStatus" + (k + 1)),
                Badge = CopyPart(_badgeTemplate, omenBadge.transform, "OmenBadge" + (k + 1)),
                Home = enemyFigure.Rect.anchoredPosition,
            };
        }

        /// <summary>A copy of <paramref name="template"/> beside the scene's own: moved out from under the inactive holder, it wakes and lays itself out.</summary>
        private static T CopyPart<T>(T template, Transform scenePart, string partName) where T : Component
        {
            T copy = Instantiate(template, template.transform.parent);
            copy.name = partName;
            copy.transform.SetParent(scenePart.parent, false);
            copy.transform.SetSiblingIndex(scenePart.GetSiblingIndex() + 1);
            return copy;
        }

        /// <summary>The enemy <paramref name="k"/>'s parts; the first's for one the screen does not draw.</summary>
        private EnemyParts EnemyAt(int k)
        {
            return k >= 0 && k < _enemyCount ? _enemies[k] : _enemies[0];
        }

        /// <summary>The enemy a cue goes to (Cue.Unit, EnemyScreen).</summary>
        private EnemyParts EnemyOf(Cue cue) => EnemyAt(EnemyScreen.IndexOf(cue, _enemyCount));

        private bool SeveralEnemies => _enemyCount > 1;

        /// <summary>The frame the screen stands on, or null before a battle.</summary>
        private DepictionFrame FrameNow => _source != null ? _source.Frame : null;

        private void SnapEnemiesHome()
        {
            for (int k = 0; k < _enemyCount; k++)
            {
                if (_enemies[k].Figure) SnapHome(_enemies[k].Figure, _enemies[k].Home);
            }
        }

        /// <summary>§4.4 敵の番: only <paramref name="acting"/>'s omen may keep the acting light; −1 puts them all out.</summary>
        private void SetActingExcept(int acting)
        {
            for (int k = 0; k < _enemies.Count; k++)
            {
                if (k != acting && _enemies[k].Badge) _enemies[k].Badge.SetActing(false);
            }
        }

        /// <summary>§2.2 の 1: the figure nearer the player is drawn in front. The figures trade the places they already hold among the siblings.</summary>
        private void DrawNearerInFront(DepictionFrame frame)
        {
            int[] order = EnemyScreen.BackToFront(frame);
            var places = new int[order.Length];
            for (int i = 0; i < order.Length; i++) places[i] = _enemies[order[i]].Figure.transform.GetSiblingIndex();
            System.Array.Sort(places);
            for (int i = 0; i < order.Length; i++)
            {
                Transform figure = _enemies[order[i]].Figure.transform;
                if (figure.GetSiblingIndex() != places[i]) figure.SetSiblingIndex(places[i]);
            }
        }

        // ---- battle-visual-v1 §4.2 / §4.7 (#242) ------------------------------------------------------

        /// <summary>
        /// Makes the parts the scene from before #242 does not have: the floor, under the figures, the
        /// stamina tag, which takes the player's stamina over from the pips in the player's panel, and
        /// the piles under it (#455).
        /// </summary>
        private void BuildVisualParts()
        {
            if (enemyFigure && enemyFigure.Rect.parent is RectTransform arena) _floor = new FloorView(arena);
            RectTransform root = RefScreen.Root(handArea ? handArea : (Transform)transform);
            if (root && playerStatus)
            {
                int below = handArea && handArea.parent == root ? handArea.GetSiblingIndex() : -1;
                _staminaTag = new StaminaTagView(root, below);
                playerStatus.PipsElsewhere = true;
                playerStatus.StaminaShown = (stamina, max) => _staminaTag.SetStamina(stamina, max);
            }
            if (root)
            {
                int below = handArea && handArea.parent == root ? handArea.GetSiblingIndex() : -1;
                _piles = new PilesView(root, below);
                below = handArea && handArea.parent == root ? handArea.GetSiblingIndex() : -1;
                _stanceLists = new StanceListView(root, below);
                below = handArea && handArea.parent == root ? handArea.GetSiblingIndex() : -1;
                _played = new PlayedAttributesView(root, below);
                below = handArea && handArea.parent == root ? handArea.GetSiblingIndex() : -1;
                _reserveHint = new ReserveHintView(root, below);
                // §5.4: over the hand, so a card's back does not hide the reason it came back.
                int above = handArea && handArea.parent == root ? handArea.GetSiblingIndex() + 1 : -1;
                _refusalLine = new RefusalLineView(root, above);
                // §5.5: over the hand and everything else below the throw line.
                above = handArea && handArea.parent == root ? handArea.GetSiblingIndex() + 1 : -1;
                _enemyShade = new EnemyTurnShadeView(root, above);
                // battle_ui_ux_v2 §1.1 C4: a click on a pile opens its list over everything.
                _pileList = new PileListView(root);
                _piles.Clicked += pile => _pileList.Toggle(pile);
                // §4.9: the 構えの予告 is made here at its own place; the scene's icon and number give way to it.
                if (stanceHintIcon) stanceHintIcon.gameObject.SetActive(false);
                if (stanceHintText) stanceHintText.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// battle-visual-v1 §5.5 / §4.9 (#455): on the enemy's turn the screen sinks below y 790 and the
        /// end-turn plate stands at 40%. Whose turn it is comes from the script (DepictionFrame.EnemyTurn).
        /// </summary>
        private void ShowEnemyTurn(bool on)
        {
            _enemyShade?.Show(on);
            if (endTurn) UiKit.Group(endTurn).alpha = on ? EnemyTurnShadeLayout.EndTurnAlpha : 1f;
        }

        /// <summary>
        /// §4.9 / §5.3 (#455): while a card is held, the 構えの予告 reads as it would once the card is paid
        /// for (CardFace.StanceHintAfterPay, the script's); otherwise as the frame has it.
        /// </summary>
        private void ShowReserveHint(CardView held)
        {
            if (_reserveHint == null) return;
            string afterPay = !held || held.Face == null ? null
                : SeveralEnemies ? held.Face.StanceHintAfterPayFor(EnemyScreen.FramedUnit(held.Face, _pickedUnit))
                : held.Face.StanceHintAfterPay;
            _reserveHint.Show(afterPay ?? _frameStanceHint);
        }

        /// <summary>
        /// battle-visual-v1 §4.5 / §5.2 HP の減る分 (#455): while a card is held, the HP it would take
        /// off each enemy (CardFace.HpLossFor(k), the script's) shows as wick stripes on that enemy's HP
        /// bar (battle_ui_ux_v2 §2.2 の 4); with no card held, or a loss of 0, there are none.
        /// </summary>
        private void ShowHpLoss(CardView held)
        {
            for (int k = 0; k < _enemyCount && k < _enemies.Count; k++)
            {
                if (_enemies[k].Status) _enemies[k].Status.ShowHpLoss(held && held.Face != null ? held.Face.HpLossFor(k) : 0);
            }
        }

        /// <summary>
        /// §4.7 (#455): the piles with the script's counts. While they are drawn, drawn cards fly from
        /// the draw pile and the discarded hand and the played card's ghost fly to the discard pile
        /// (battle_ui_ux_v2 「スタミナの札と束」), so the scene's two points move onto them.
        /// </summary>
        private void ApplyPiles(PileFrame piles)
        {
            if (_piles == null) return;
            _piles.Bind(piles);
            _pileList?.Show(piles);
            if (!_piles.Shown) return;
            if (deckPoint) deckPoint.position = _piles.MiddleOf(PileKind.Draw);
            if (discardPoint) discardPoint.position = _piles.MiddleOf(PileKind.Discard);
        }

        /// <summary>
        /// §4.2 / §4.3: the floor of <paramref name="frame"/>, with the player standing on its cells now
        /// and each enemy's home on its own (an enemy is moved there by the next return or snap, so a
        /// return already under way is not cut short). With a floor the one gap number is the floor's,
        /// so the figure's own tag stays down.
        /// </summary>
        private void ApplyFloor(DepictionFrame frame)
        {
            if (_floor == null || !playerFigure) return;
            _floor.Bind(frame, playerFigure.transform.position, FloorFocus.GapUnit(frame, _pickedUnit, _chosenUnit), _badgeHovered);
            _reachFor = null; // Bind put the reach out; the next Update lights it again for the card still hovered

            if (!_floor.Shown) return;
            FloorFrame floor = frame.Floor;
            playerFigure.SetRange(false, "");
            playerFigure.transform.position = _floor.FeetOf(floor.PlayerCell, floor.PlayerSize);
            SetPlayerHome(playerFigure);
            SetEnemyHomes(frame);
        }

        /// <summary>
        /// battle_ui_ux_v2 §5.8 提示 (#766): the cells the omen just shown aims at are drawn in from the left
        /// beside the badge's drop. It holds nothing up: the frame's Bind at the event's end lets it run on.
        /// </summary>
        private void DrawOmenStripes(DepictionFrame after)
        {
            if (_floor == null || after == null) return;
            IEnumerator play = _floor.DrawStripesIn(after, _effects, out int cells);
            if (cells > 0) StartCoroutine(Effect(EffectId.StripeDraw, play, _floor.SettleStripes, cells));
        }

        /// <summary>
        /// battle_ui_ux_v2 §5.8 実行 (#766): after the event's beats, the stripes of an omen that is gone
        /// (spent, rested or fallen with its enemy) fade out instead of going with the frame. Called just
        /// before the frame is applied, so its Bind lets the fade run on.
        /// </summary>
        private void FadeSpentStripes(DepictionFrame after)
        {
            if (_floor == null || after == null) return;
            IEnumerator play = _floor.FadeStripesOut(after, _effects, out int cells);
            if (cells > 0) StartCoroutine(Effect(EffectId.StripeFade, play, _floor.SettleStripes));
        }

        /// <summary>Each enemy's home is its cell in <paramref name="frame"/>.</summary>
        private void SetEnemyHomes(DepictionFrame frame)
        {
            for (int k = 0; k < _enemyCount; k++)
            {
                EnemyParts foe = _enemies[k];
                if (foe.Figure) foe.Home = AnchoredAt(foe.Figure.Rect, _floor.FeetOf(EnemyScreen.CellOf(frame, k), EnemyScreen.SizeOf(frame, k)));
            }
        }

        /// <summary>The anchored position that puts <paramref name="rt"/>'s pivot on the world point <paramref name="world"/>.</summary>
        private static Vector2 AnchoredAt(RectTransform rt, Vector3 world)
        {
            Vector3 was = rt.position;
            rt.position = world;
            Vector2 anchored = rt.anchoredPosition;
            rt.position = was;
            return anchored;
        }

        /// <summary>
        /// With a floor, the parts tied to an enemy keep to its x: the omen badge and the release zone
        /// over the enemy's home, kept 24 px inside the screen's edges. The badge's bottom also stands
        /// 14 px over the enemy's head (§4.4, #455: the figure is 226 or 264 px tall since §4.3); the
        /// release zone's height stays where the scene put it. The number panels follow their
        /// figures with or without a floor (§4.5). With several enemies the badges share one row (§5.16)
        /// and slide apart (§5.8), and the release zone stands over the enemy a held card is aimed at.
        /// </summary>
        private void FollowFigures()
        {
            if (!enemyFigure || !playerFigure || _enemies.Count == 0) return;
            RectTransform root = RefScreen.Root(enemyFigure.transform);
            if (!root) return;
            if (_floor != null && _floor.Shown)
            {
                if (SeveralEnemies) PlaceBadgeRow(root);
                else if (omenBadge)
                {
                    Vector2 home = EnemyHomeOnScreen(root, _enemies[0]);
                    KeepX(omenBadge.Rect, root, home.x);
                    float head = home.y - FigureFrame.HeightFor(EnemySizeOf(0));
                    KeepBottom(omenBadge.Rect, root, head - FigureFrame.OmenOverHead);
                }
                if (receiver) KeepX((RectTransform)receiver.transform, root, EnemyHomeOnScreen(root, ReceiverEnemy()).x);
            }
            float playerX = RefScreen.Of(root, _playerHomeSet ? _playerHome : playerFigure.transform.position).x;
            FollowPanels(root, playerX);
        }

        /// <summary>The enemy the release zone stands over.</summary>
        private EnemyParts ReceiverEnemy()
        {
            if (_aimedUnit >= 0) return EnemyAt(_aimedUnit);
            return EnemyAt(FrameNow != null ? EnemyScreen.FocusOf(FrameNow) : 0);
        }

        /// <summary>
        /// §2.2 の 2 / §5.8 / §5.16 (#589): each standing enemy's badge over its head, on one row 14 px over
        /// the highest head, slid apart where two would overlap (OmenRowLayout). The slide eases out over
        /// 200 ms, so an enemy that walks next to another has its badge make room as it arrives. A fallen
        /// enemy's badge is hidden and takes no place.
        /// </summary>
        private void PlaceBadgeRow(RectTransform root)
        {
            DepictionFrame frame = FrameNow;
            if (frame == null) return;
            var standing = new List<int>();
            for (int k = 0; k < _enemyCount; k++)
            {
                if (EnemyScreen.Stands(frame, k) && _enemies[k].Badge) standing.Add(k);
            }
            if (standing.Count == 0) return;
            var centres = new float[standing.Count];
            var heights = new float[standing.Count];
            float feetY = 0f;
            for (int i = 0; i < standing.Count; i++)
            {
                Vector2 home = EnemyHomeOnScreen(root, _enemies[standing[i]]);
                centres[i] = home.x;
                heights[i] = FigureFrame.HeightFor(EnemySizeOf(standing[i]));
                feetY = home.y;
            }
            float[] lefts = OmenRowLayout.Lefts(centres);
            float bottom = OmenRowLayout.RowBottom(feetY, heights);
            float share = 1f - Mathf.Exp(-3f * FrameMotionSpeed * Time.unscaledDeltaTime * 1000f / OmenRowLayout.SlideMs);
            for (int i = 0; i < standing.Count; i++)
            {
                EnemyParts foe = _enemies[standing[i]];
                float slide = OmenRowLayout.Slide(lefts[i], centres[i]);
                foe.BadgeSlide = _snapBadges || Mathf.Abs(slide - foe.BadgeSlide) < 0.25f ? slide : Mathf.Lerp(foe.BadgeSlide, slide, share);
                KeepX(foe.Badge.Rect, root, centres[i] + foe.BadgeSlide);
                KeepBottom(foe.Badge.Rect, root, bottom);
            }
            _snapBadges = false;
        }

        /// <summary>The figure's place between its motions is where it stands now (<paramref name="figure"/> the player's only).</summary>
        private void SetPlayerHome(FigureView figure)
        {
            if (figure) SetPlayerHome(figure, figure.transform.position);
        }

        private void SetPlayerHome(FigureView figure, Vector3 world)
        {
            if (!figure || figure != playerFigure) return;
            _playerHome = world;
            _playerHomeSet = true;
        }

        /// <summary>
        /// battle-visual-v1 §4.5 (#455): each number panel at y 614 under its figure's home, 300 wide for
        /// the player and as wide as each enemy's cells allow (StatusPanelLayout), pushed apart where they
        /// would meet. They ease toward their places, all by the same share each frame, so panels
        /// apart before and apart after are never over each other on the way. A fallen enemy's grey panel
        /// keeps its place in the row, under its emptied cell (§2.2 の 6).
        /// </summary>
        private void FollowPanels(RectTransform root, float playerX)
        {
            if (!playerStatus || !enemyStatus) return;
            FloorFrame floor = FrameNow != null ? FrameNow.Floor : null;
            int cells = _floor != null && _floor.Shown && floor != null ? floor.Cells : 0;
            playerStatus.SetWidth(StatusPanelLayout.PlayerWidth);
            for (int k = 0; k < _enemyCount; k++)
            {
                float width = StatusPanelLayout.EnemyPanelWidth(cells, EnemySizeOf(k));
                if (!Mathf.Approximately(width, _enemies[k].Status.LaidOutWidth)) _snapPanels = true; // a new line: no easing across widths
                _enemies[k].Status.SetWidth(width);
            }
            float[] lefts = PanelPlaces(root, playerX);
            if (_snapPanels)
            {
                _playerPanelLeft = lefts[0];
                for (int k = 0; k < _enemyCount; k++) _enemies[k].PanelLeft = lefts[k + 1];
                _snapPanels = false;
            }
            else
            {
                float share = 1f - Mathf.Exp(-PanelFollowRate * FrameMotionSpeed * Time.unscaledDeltaTime);
                _playerPanelLeft = Mathf.Abs(lefts[0] - _playerPanelLeft) < 0.25f ? lefts[0] : Mathf.Lerp(_playerPanelLeft, lefts[0], share);
                for (int k = 0; k < _enemyCount; k++)
                {
                    EnemyParts foe = _enemies[k];
                    foe.PanelLeft = Mathf.Abs(lefts[k + 1] - foe.PanelLeft) < 0.25f ? lefts[k + 1] : Mathf.Lerp(foe.PanelLeft, lefts[k + 1], share);
                }
            }
            playerStatus.PlaceAt(root, new Vector2(_playerPanelLeft, StatusPanelLayout.Top));
            for (int k = 0; k < _enemyCount; k++) _enemies[k].Status.PlaceAt(root, new Vector2(_enemies[k].PanelLeft, StatusPanelLayout.Top));
        }

        /// <summary>Where the panels belong now: the player's first, then each enemy's (StatusPanelLayout, from the figures' homes).</summary>
        private float[] PanelPlaces(RectTransform root, float playerX)
        {
            var centres = new float[_enemyCount + 1];
            var widths = new float[_enemyCount + 1];
            centres[0] = playerX;
            widths[0] = StatusPanelLayout.PlayerWidth;
            for (int k = 0; k < _enemyCount; k++)
            {
                centres[k + 1] = EnemyHomeOnScreen(root, _enemies[k]).x;
                widths[k + 1] = _enemies[k].Status.LaidOutWidth;
            }
            if (SeveralEnemies) return StatusPanelLayout.PlaceByX(centres, widths);
            StatusPanelLayout.Place(centres[0], widths[0], centres[1], widths[1], out float playerLeft, out float enemyLeft);
            return new[] { playerLeft, enemyLeft };
        }

        /// <summary>Where an enemy's home is on the reference screen.</summary>
        private static Vector2 EnemyHomeOnScreen(RectTransform root, EnemyParts foe)
        {
            return RefScreen.Of(root, foe.Figure.Rect.parent.TransformPoint(EnemyHomeLocal(foe)));
        }

        /// <summary>The enemy's home in its parent's local space (its anchored position, back through its anchors).</summary>
        private static Vector3 EnemyHomeLocal(EnemyParts foe)
        {
            RectTransform rt = foe.Figure.Rect;
            Vector2 now = rt.anchoredPosition;
            return rt.localPosition + (Vector3)(foe.Home - now);
        }

        /// <summary>Moves <paramref name="rt"/> so its middle stands on screen x <paramref name="x"/>, its whole width 24 px inside the edges.</summary>
        private static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>
        /// Moves <paramref name="rt"/> up or down so its bottom edge stands on screen y <paramref name="bottom"/>.
        /// Read from its rect and pivot, not its corners, so a pop or a flare scaling it does not move it.
        /// </summary>
        private static void KeepBottom(RectTransform rt, RectTransform root, float bottom)
        {
            float pivotY = bottom - rt.rect.height * rt.pivot.y;
            Vector2 now = RefScreen.Of(root, rt.position);
            if (Mathf.Abs(now.y - pivotY) < 0.5f) return;
            Vector3 to = RefScreen.World(root, new Vector2(now.x, pivotY));
            rt.position = new Vector3(rt.position.x, to.y, rt.position.z);
        }

        private static void KeepX(RectTransform rt, RectTransform root, float x)
        {
            Vector3[] corners = Corners;
            rt.GetWorldCorners(corners);
            float left = RefScreen.Of(root, corners[0]).x;
            float right = RefScreen.Of(root, corners[2]).x;
            float half = (right - left) * 0.5f;
            float target = Mathf.Clamp(x, 24f + half, BattleTheme.RefWidth - 24f - half);
            float shift = target - (left + half);
            if (Mathf.Abs(shift) < 0.5f) return;
            Vector3 from = RefScreen.World(root, new Vector2(left + half, 0f));
            Vector3 to = RefScreen.World(root, new Vector2(target, 0f));
            rt.position += new Vector3(to.x - from.x, 0f, 0f);
        }

        /// <summary>
        /// §4.2 / §5.1 / §5.2: while an enemy-aimed card is hovered or held, the cells it reaches light
        /// up (CardFace.ReachCells, the script's). Anything else puts them out.
        /// </summary>
        private void UpdateFloorReach()
        {
            if (_floor == null) return;
            CardView card = _dragging ? _dragging : (!_busy ? _hovered : null);
            if (!card || card.Face == null || card.Face.Aim != CardAim.Single) card = null;
            // By reference: a played card is destroyed, and Unity's == would call it equal to null.
            if (ReferenceEquals(card, _reachFor)) return;
            _reachFor = card;
            if (card) _floor.ShowReach(card.Face.ReachCells);
            else _floor.HideReach();
        }

        // ---- battle_ui_ux_v2 §2.2 の 2 / の 3 / §3.4 (#762): whose gap, whose aims ---------------------

        private readonly List<ScreenBox> _badgeBoxes = new List<ScreenBox>();
        private readonly List<int> _badgeUnits = new List<int>();

        /// <summary>
        /// With several enemies: Tab moves the gap number to the next standing enemy, a held card over an
        /// enemy shows that enemy's N until it is let go, and a pointer on an omen badge keeps that enemy's
        /// aimed cells and fades the others'. Whose number and which aims are FloorFocus's and FloorStripes'
        /// answers. While an event plays its cues own the gap tag (a move counts it again), so it waits.
        /// </summary>
        private void UpdateFloorFocus()
        {
            DepictionFrame frame = FrameNow;
            if (_floor == null || !_floor.Shown || frame == null) return;
            if (SeveralEnemies && TryTab()) _chosenUnit = FloorFocus.NextTab(frame, FloorFocus.GapUnit(frame, -1, _chosenUnit));
            _badgeHovered = BadgeUnderPointer(frame);
            _floor.ShowHovered(_badgeHovered);
            if (!_busy) _floor.ShowGapOf(FloorFocus.GapUnit(frame, _pickedUnit, _chosenUnit));
        }

        /// <summary>The standing enemy whose omen badge is under the pointer, or −1. A held card owns the pointer.</summary>
        private int BadgeUnderPointer(DepictionFrame frame)
        {
            if (!SeveralEnemies || _dragging != null || !TryPointer(out Vector2 pointer)) return -1;
            RectTransform root = RefScreen.Root(enemyFigure ? enemyFigure.transform : transform);
            if (!root) return -1;
            Canvas canvas = root.GetComponent<Canvas>();
            Camera camera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(root, pointer, camera, out Vector3 world)) return -1;
            Vector2 at = RefScreen.Of(root, world);
            _badgeBoxes.Clear();
            _badgeUnits.Clear();
            for (int k = 0; k < _enemyCount; k++)
            {
                OmenBadgeView badge = _enemies[k].Badge;
                if (!badge || !badge.isActiveAndEnabled || !EnemyScreen.Stands(frame, k)) continue;
                _badgeBoxes.Add(BoxOn(root, badge.Rect, Vector3.zero));
                _badgeUnits.Add(k);
            }
            int hit = FloorFocus.BoxAt(_badgeBoxes, at.x, at.y);
            return hit >= 0 ? _badgeUnits[hit] : -1;
        }

        /// <summary>True on the frame Tab went down (§3.4: 相手を切り替え).</summary>
        private static bool TryTab()
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null && keyboard.tabKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Tab);
#endif
        }

        /// <summary>
        /// battle-visual-v1 §4.9 (#242): the end-turn plate in amber — a 2 px amber frame over an amber
        /// 16% fill, its label at 24 px. The plate belongs to the scene, so it is restyled here.
        /// </summary>
        private void StyleEndTurn()
        {
            if (!endTurn) return;
            Image plate = endTurn.GetComponent<Image>();
            if (plate)
            {
                plate.sprite = VisualArt.Rounded(6);
                plate.type = Image.Type.Sliced;
                plate.color = BattleTheme.WithAlpha(BattleTheme.Amber, 0.16f);
            }
            foreach (string edge in new[] { "FrameTop", "FrameBottom", "FrameLeft", "FrameRight" })
            {
                Transform old = endTurn.Find(edge);
                if (old) old.gameObject.SetActive(false);
            }
            if (!endTurn.Find("AmberFrame")) VisualArt.Ring(endTurn, "AmberFrame", BattleTheme.Amber, 6, 2);
            Text label = endTurn.GetComponentInChildren<Text>();
            if (label)
            {
                label.fontSize = 24;
                BattleFonts.Use(label, BattleFonts.PanelWord);
                label.color = BattleTheme.Ink;
            }
        }
    }
}
#endif
