// One node of the exploration map (ExploreScene.js:481-502 buildMap's node, :506-521 updateMap's classes;
// explore.css:84-102, :205): the 88 × 88 box (.ocbg-explore-node), its lift wrapper (face + label: the hover lift,
// the resolved fade), the face with its icon and the standing styles (border, shadow, background, the boss outline,
// the beckon of a node the player can walk to or jump to), the label under it, and the arrival ripple (the face's
// ::after). Owned by EX-MAP.
//
// The JS keeps { el, face, label, kind, standing } per node and restyles it through class names; here the classes are
// the states of two CssStates (the face's and the lift's, registered in the stylesheet's cascade order) and two CSS
// animations (beckon, ripple). updateMap records its class changes (SetStanding / SetLive / SetPinned / SetJump) and
// Commit applies them at once, as the browser computes the style once after the task: the first commit after the
// node is built is instant (the JS sets the classes in the task that built the node, so nothing transitions).
using System;
using DungeonCore;
using Exploration;
using TMPro;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;

namespace Trial.Explore
{
    public sealed class ExploreNodeView
    {
        // ------------------------------------------------------------------ explore.css (verbatim)

        /// <summary>explore.css:84 .ocbg-explore-node (the 88 × 88 box: pointer target, no paint).</summary>
        public const string NodeCss = "position: absolute; pointer-events: auto; cursor: default; outline: none;";

        /// <summary>explore.css:85 .ocbg-explore-node-lift.</summary>
        public const string LiftCss = "position: absolute; inset: 0; transition: transform 90ms cubic-bezier(.4,0,.2,1), opacity 300ms;";

        /// <summary>explore.css:86 .ocbg-explore-node:hover .ocbg-explore-node-lift.</summary>
        public const string LiftHoverCss = "transform: translateY(-6px) scale(1.04);";

        /// <summary>explore.css:87 .ocbg-explore-node.is-live (the cursor only: Unity draws no cursor).</summary>
        public const string LiveCss = "cursor: pointer;";

        /// <summary>explore.css:88 .ocbg-explore-node-face.</summary>
        public const string FaceCss = "position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; background: var(--panel-solid); border: 2px solid var(--steel); border-radius: var(--radius-panel); box-shadow: var(--shadow-card); transition: border-color 300ms, box-shadow 300ms, background-color 300ms;";

        /// <summary>explore.css:89 .ocbg-explore-node-label.</summary>
        public const string LabelCss = "position: absolute; left: 50%; top: 96px; transform: translateX(-50%); font: 700 16px/20px var(--font-body); color: var(--ink); white-space: nowrap; text-shadow: 0 0 6px var(--ground), 0 1px 2px var(--ground);";

        /// <summary>explore.css:90 .is-current .ocbg-explore-node-face.</summary>
        public const string CurrentCss = "border: 3px solid var(--amber); box-shadow: var(--shadow-card), var(--shadow-reach);";

        /// <summary>explore.css:91 .is-reachable .ocbg-explore-node-face (the animation is BeckonAnim).</summary>
        public const string ReachableCss = "border-color: var(--amber); animation: ocbg-explore-beckon 2.4s ease-in-out infinite;";

        /// <summary>explore.css:93 .is-revisit .ocbg-explore-node-face.</summary>
        public const string RevisitCss = "border: 2px solid color-mix(in srgb, var(--ink) 60%, transparent);";

        /// <summary>explore.css:94 .is-resolved .ocbg-explore-node-lift.</summary>
        public const string ResolvedLiftCss = "opacity: .6;";

        /// <summary>explore.css:95 .is-resolved .ocbg-explore-node-face.</summary>
        public const string ResolvedCss = "border: 1px solid var(--rule);";

        /// <summary>explore.css:96 .is-far .ocbg-explore-node-face.</summary>
        public const string FarCss = "border: 2px dashed var(--steel); background: color-mix(in srgb, var(--panel-solid) 72%, transparent); box-shadow: none;";

        /// <summary>explore.css:97 .kind-boss .ocbg-explore-node-face.</summary>
        public const string BossCss = "outline: 2px solid var(--boss); outline-offset: 4px;";

        /// <summary>explore.css:98 .kind-boss.is-far .ocbg-explore-node-face.</summary>
        public const string BossFarCss = "outline-style: dashed;";

        /// <summary>explore.css:100 .ocbg-explore-node-face::after (the arrival ripple's ring).</summary>
        public const string AfterCss = "content: \"\"; position: absolute; inset: -3px; border-radius: var(--radius-panel); border: 2px solid var(--amber); opacity: 0; pointer-events: none;";

        /// <summary>explore.css:101 .is-arrived .ocbg-explore-node-face::after's animation.</summary>
        public const string RippleAnim = "ocbg-explore-ripple 820ms cubic-bezier(.22,.61,.36,1)";

        /// <summary>explore.css:205 .is-jump .ocbg-explore-node-face (trial: a 標の石 can land here; the animation is BeckonAnim).</summary>
        public const string JumpCss = "border: 2px dashed var(--amber); box-shadow: var(--shadow-reach); animation: ocbg-explore-beckon 2.4s ease-in-out infinite;";

        /// <summary>explore.css:91 / :205 animation (ocbg-explore-beckon: box-shadow card + 0 → 18px 2px amber 38 % → 0).</summary>
        public const string BeckonAnim = "ocbg-explore-beckon 2.4s ease-in-out infinite";

        // the face's states, in the stylesheet's cascade order (specificity, then place: kind-boss.is-far is the most specific)
        private const string SCurrent = ".is-current", SReachable = ".is-reachable", SRevisit = ".is-revisit", SResolved = ".is-resolved",
            SFar = ".is-far", SBoss = ".kind-boss", SJump = ".is-jump", SBossFar = ".kind-boss.is-far", SHover = ":hover";

        // ------------------------------------------------------------------ the record buildMap keeps ({ el, face, label, kind, standing })

        /// <summary>.ocbg-explore-node (role=button, data-node): the 88 × 88 pointer target.</summary>
        public RectTransform El;

        /// <summary>.ocbg-explore-node-lift (hover: translateY(−6px) scale(1.04); resolved: opacity .6).</summary>
        public RectTransform Lift;

        /// <summary>.ocbg-explore-node-face (the icon in its box).</summary>
        public RectTransform Face;

        /// <summary>.ocbg-explore-node-label (KindLabel).</summary>
        public TMP_Text Label;

        public NodeKind Kind;

        /// <summary>The standing drawn last (null until updateMap drew it).</summary>
        public NodeStanding? Standing;

        /// <summary>data-node.</summary>
        public int Id;

        /// <summary>The node's row (the enter animation's delay).</summary>
        public int Row;

        /// <summary>The element's pointer listeners (mouseenter / mouseleave / click; :hover).</summary>
        public ClickArea Hit;

        /// <summary>The face's box (its border, background, shadows and the boss outline).</summary>
        public BoxGraphic FaceBox;

        /// <summary>ico(n.Kind, 44, nodeIconColor(n)) in the face (redrawn when the standing changes).</summary>
        public PathGraphic Icon;

        /// <summary>The face's ::after (the arrival ripple's ring, inset −3).</summary>
        public BoxGraphic After;

        /// <summary>.is-live (map live and not busy): the cursor only.</summary>
        public bool Live;

        /// <summary>.is-pinned: no rule draws it (the selection frame marks the pinned node).</summary>
        public bool Pinned;

        /// <summary>.is-jump: a 標の石 is aimed and can land here.</summary>
        public bool Jump;

        private CssStates _face;
        private CssStates _lift;
        private CssAnimator _beckon;
        private CssAnimator _ripple;
        private bool _faceDirty, _liftDirty, _styled;

        /// <summary>The node is still on screen (the map was not rebuilt since).</summary>
        public bool Alive => El;

        // js: scenes/explore/ExploreScene.js:481
        /// <summary>
        /// The node of buildMap at (x, y) (its box's top-left: the centre minus NODE / 2) under the nodes layer: the box,
        /// the lift, the face (empty until the first SetStanding draws its icon), the ripple ring, the label.
        /// </summary>
        public static ExploreNodeView Make(RectTransform parent, NodeCard n, float x, float y)
        {
            if (n == null) throw new ArgumentNullException(nameof(n));
            const float node = ExploreStyle.Node;
            var v = new ExploreNodeView { Id = n.Id, Row = n.Row, Kind = n.Kind };

            // .ocbg-explore-node kind-…: position absolute at the node's box, pointer-events: auto (under the map's none)
            v.El = Ui.Div(parent, "ocbg-explore-node kind-" + ExploreStyle.KindClass(n.Kind), x, y, node, node);
            UiNode.Of(v.El).PointerEvents = PointerEvents.Auto;
            v.Hit = ClickArea.On(v.El);

            // .ocbg-explore-node-lift: inset 0
            v.Lift = Ui.Div(v.El, "ocbg-explore-node-lift", 0f, 0f, node, node);
            v._lift = CssStates.Of(v.Lift).Base(LiftCss).State(SHover, LiftHoverCss).State(SResolved, ResolvedLiftCss);
            v._lift.Apply(instant: true);

            // .ocbg-explore-node-face: inset 0; a pointer target (the lifted face is still the node)
            v.FaceBox = BoxGraphic.Make(v.Lift, FaceCss, 0f, 0f, node, node, "ocbg-explore-node-face");
            v.FaceBox.raycastTarget = true;
            v.Face = v.FaceBox.rectTransform;
            v._face = CssStates.Of(v.FaceBox).Base(FaceCss)
                .State(SCurrent, CurrentCss)
                .State(SReachable, ReachableCss)
                .State(SRevisit, RevisitCss)
                .State(SResolved, ResolvedCss)
                .State(SFar, FarCss)
                .State(SBoss, BossCss, n.Kind == NodeKind.Boss)
                .State(SJump, JumpCss)
                .State(SBossFar, BossFarCss);
            v._face.Apply(instant: true);

            // ::after (inset −3px of the face's padding box, opacity 0): drawn after the face's content
            v.After = BoxGraphic.Make(v.Face, AfterCss, -3f, -3f, node + 6f, node + 6f, "after");
            UiNode.Of(v.After).Opacity = 0f;
            v.PlaceAfter();

            // .ocbg-explore-node-label: left 50%, top 96, translateX(−50%); the label is part of the node (it takes the hover)
            TextStyle ls = TextStyle.FromCss(LabelCss);
            TMP_Text label = global::Trial.Kit.Label.Make(v.Lift, n.KindLabel ?? "", ls, 0f, 96f, null, null, "ocbg-explore-node-label");
            float w = label.rectTransform.sizeDelta.x;
            Ui.Place(label.rectTransform, node / 2f - w / 2f, 96f);
            TextGlow.For(label);
            label.raycastTarget = true;
            v.Label = label;

            // .ocbg-explore-node:hover .ocbg-explore-node-lift
            v.Hit.HoverChanged += on => v.SetHover(on);
            return v;
        }

        // js: scenes/explore/ExploreScene.js:510
        /// <summary>
        /// rec.el.className = 'ocbg-explore-node kind-… ' + STANDING_CLASS[standing] and the face's icon redrawn in its
        /// colour. The class reset also drops is-arrived (a ripple stops) and is-live / is-pinned / is-jump, which the caller
        /// sets again before Commit (as updateMap toggles them right after).
        /// </summary>
        public void SetStanding(NodeCard n, Rgba iconColor)
        {
            if (n == null || !El) return;
            Standing = n.Standing;
            ClearArrived();
            NodeStanding s = n.Standing;
            _faceDirty |= _face.SetState(SCurrent, s == NodeStanding.Current, false);
            _faceDirty |= _face.SetState(SReachable, s == NodeStanding.Reachable, false);
            _faceDirty |= _face.SetState(SRevisit, s == NodeStanding.Revisit, false);
            _faceDirty |= _face.SetState(SResolved, s == NodeStanding.Resolved, false);
            _faceDirty |= _face.SetState(SFar, s == NodeStanding.Far, false);
            _faceDirty |= _face.SetState(SBossFar, Kind == NodeKind.Boss && s == NodeStanding.Far, false);
            _liftDirty |= _lift.SetState(SResolved, s == NodeStanding.Resolved, false);

            // rec.face.textContent = ''; rec.face.appendChild(ico(n.Kind, 44, nodeIconColor(n))): centred in the face
            if (Icon) Ui.Remove(Icon);
            Icon = ExploreIcons.Ico(Face, n.Kind, 44f, iconColor);
            Ui.Place(Icon.rectTransform, (ExploreStyle.Node - 44f) / 2f, (ExploreStyle.Node - 44f) / 2f);
            Icon.transform.SetAsFirstSibling(); // ::after stays above the content
        }

        // js: scenes/explore/ExploreScene.js:517
        /// <summary>classList.toggle('is-live', on): the pointer cursor (no look of its own).</summary>
        public void SetLive(bool on) => Live = on;

        // js: scenes/explore/ExploreScene.js:518
        /// <summary>classList.toggle('is-pinned', on) (no rule draws it).</summary>
        public void SetPinned(bool on) => Pinned = on;

        // js: scenes/explore/ExploreScene.js:520
        /// <summary>classList.toggle('is-jump', on): trial, a 標の石 aimed that can land here.</summary>
        public void SetJump(bool on)
        {
            Jump = on;
            _faceDirty |= _face.SetState(SJump, on, false);
        }

        /// <summary>
        /// The recorded class changes applied at once (the browser's next style computation): the beckon stops when the
        /// node no longer beckons (its values go back first, so the new state's shadow is the one drawn), the states
        /// apply (transitions run, except on the first commit), and the beckon starts when it newly applies (the same
        /// animation name on a reachable node that became a jump target keeps running, as CSS does).
        /// </summary>
        public void Commit()
        {
            if (!El) return;
            bool instant = !_styled;
            bool beckoned = _beckon != null && _beckon.Running;
            bool beckons = Standing == NodeStanding.Reachable || Jump;
            if (!beckons && _beckon != null)
            {
                _beckon.Stop();
                _beckon = null;
            }
            // The beckon keeps running across the change (a reachable node becomes, or stops being, a jump target): CSS
            // starts no box-shadow transition then (the before- and after-change styles both hold the animated shadow), and
            // what else differs between those states changes at once (border-style) or not at all (border-color amber).
            bool keep = beckoned && beckons;
            if (_faceDirty || instant)
            {
                _face.Apply(instant || keep);
                PlaceAfter();
                // the state machine wrote its own shadow over the running beckon: put the animated one back at once
                if (keep) Resample(_beckon);
            }
            if (_liftDirty || instant) _lift.Apply(instant);
            _faceDirty = _liftDirty = false;
            _styled = true;
            if (beckons && (_beckon == null || !_beckon.Running)) _beckon = CssAnimator.Play(FaceBox, BeckonAnim);
        }

        /// <summary>
        /// The face's ::after box. The face (position: absolute) is its containing block, so `inset: -3px` is measured from
        /// the face's padding box: the ring follows the face's border width in force (2px: 90 × 90 at −1; is-current's
        /// 3px: 88 × 88 at 0, the node's own edge; is-resolved's 1px: 92 × 92 at −2).
        /// </summary>
        private void PlaceAfter()
        {
            if (!After || !FaceBox) return;
            const float inset = 3f;
            float bw = FaceBox.Style.Top.Used;
            float at = bw - inset;
            float size = ExploreStyle.Node - 2f * bw + 2f * inset;
            Ui.Place(After.rectTransform, at, at, size, size);
        }

        /// <summary>A running animation's value for now written again (after something else wrote the same property).</summary>
        private static void Resample(CssAnimator a)
        {
            if (a == null || !a.Running) return;
            if (a.Animation.TrySample(a.LocalTime, out AnimProps p)) a.Apply(p);
        }

        /// <summary>:hover on the node: the lift rises (transform 90ms std).</summary>
        public void SetHover(bool on)
        {
            if (_lift) _lift.SetState(SHover, on);
        }

        // js: scenes/explore/ExploreScene.js:1282
        /// <summary>classList.remove('is-arrived'); reflow; add('is-arrived'): the ripple runs again from its start.</summary>
        public void Arrive()
        {
            if (!After) return;
            ClearArrived();
            _ripple = CssAnimator.Play(After, RippleAnim);
        }

        // js: scenes/explore/ExploreScene.js:1285
        /// <summary>classList.remove('is-arrived'): the ripple stops where it is (it ended at 820 ms; the class goes at 900).</summary>
        public void ClearArrived()
        {
            if (_ripple == null) return;
            _ripple.Stop();
            _ripple = null;
        }

        /// <summary>explore.css:371 .ocbg-explore.is-interlude .ocbg-explore-node { pointer-events: none } (on = auto).</summary>
        public void SetPointerEvents(bool on)
        {
            if (El) UiNode.Of(El).PointerEvents = on ? PointerEvents.Auto : PointerEvents.None;
        }
    }
}
