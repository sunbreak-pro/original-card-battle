// Pointer helpers in stage px (battle-flow.md §3.3, app.md §4.6):
// - geometric hit tests of an element's box against a stage point (the hand's cardUnder, hover by position);
// - the pointer position in stage px (stage.toStage(e));
// - EventSystem.RaycastAll and "which layer is the top hit in" (the JS e.target / isControl / isForeign checks).
using System;
using System.Collections.Generic;
using Trial.Geometry;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Trial.Kit
{
    public static class Hit
    {
        private static readonly List<RaycastResult> Results = new List<RaycastResult>();
        private static readonly Vector3[] Corners = new Vector3[4];
        private static PointerEventData _ped;
        private static EventSystem _pedOwner;

        private static TrialStage StageOr(TrialStage stage)
        {
            TrialStage s = stage ? stage : TrialStage.Current;
            if (!s) throw new InvalidOperationException("Hit: no TrialStage");
            return s;
        }

        /// <summary>Stage point → rt's local space (Unity units, y up, pivot origin).</summary>
        public static Vector2 StageToLocal(RectTransform rt, Vector2 stagePos, TrialStage stage = null)
        {
            TrialStage s = StageOr(stage);
            Vector3 world = s.Root.TransformPoint(new Vector3(stagePos.x, -stagePos.y, 0f));
            Vector3 local = rt.InverseTransformPoint(world);
            return new Vector2(local.x, local.y);
        }

        /// <summary>Stage point → the element's own CSS box px (x from its left edge, y down from its top edge).</summary>
        public static Vector2 ToBox(RectTransform rt, Vector2 stagePos, TrialStage stage = null)
        {
            Vector2 l = StageToLocal(rt, stagePos, stage);
            Rect r = rt.rect;
            return new Vector2(l.x - r.xMin, r.yMax - l.y);
        }

        /// <summary>Whether the stage point lies inside rt's box (its rotation and scale included).</summary>
        public static bool Contains(RectTransform rt, Vector2 stagePos, TrialStage stage = null)
        {
            if (!rt) return false;
            Vector2 l = StageToLocal(rt, stagePos, stage);
            return rt.rect.Contains(l);
        }

        /// <summary>Whether the stage point lies inside rt's box grown by `pad` px on every side.</summary>
        public static bool Contains(RectTransform rt, Vector2 stagePos, float pad, TrialStage stage = null)
        {
            if (!rt) return false;
            Vector2 l = StageToLocal(rt, stagePos, stage);
            Rect r = rt.rect;
            return l.x >= r.xMin - pad && l.x <= r.xMax + pad && l.y >= r.yMin - pad && l.y <= r.yMax + pad;
        }

        /// <summary>The axis-aligned stage-px bounds of rt (getBoundingClientRect in stage px).</summary>
        public static RectF StageRect(RectTransform rt, TrialStage stage = null)
        {
            TrialStage s = StageOr(stage);
            rt.GetWorldCorners(Corners);
            float l = float.MaxValue, t = float.MaxValue, r = float.MinValue, b = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = s.Root.InverseTransformPoint(Corners[i]);
                float x = p.x, y = -p.y;
                if (x < l) l = x;
                if (x > r) r = x;
                if (y < t) t = y;
                if (y > b) b = y;
            }
            return RectF.FromLtrb(l, t, r, b);
        }

        /// <summary>The pointer's screen position (Mouse, else the current Pointer). False without a pointer.</summary>
        public static bool PointerScreen(out Vector2 screenPos)
        {
            Mouse m = Mouse.current;
            if (m != null) { screenPos = m.position.ReadValue(); return true; }
            Pointer p = Pointer.current;
            if (p != null) { screenPos = p.position.ReadValue(); return true; }
            screenPos = default;
            return false;
        }

        /// <summary>The pointer in stage px (stage.toStage(e)). False without a pointer or a stage.</summary>
        public static bool PointerStage(out Vector2 stagePos, TrialStage stage = null)
        {
            TrialStage s = stage ? stage : TrialStage.Current;
            if (!s || !PointerScreen(out Vector2 sp)) { stagePos = default; return false; }
            stagePos = s.ToStage(sp);
            return true;
        }

        /// <summary>EventSystem.RaycastAll at a screen position, top first. The list is reused: copy it to keep it.</summary>
        public static List<RaycastResult> RaycastAll(Vector2 screenPos)
        {
            Results.Clear();
            EventSystem es = EventSystem.current;
            if (!es) return Results;
            if (_ped == null || _pedOwner != es)
            {
                _ped = new PointerEventData(es);
                _pedOwner = es;
            }
            _ped.Reset();
            _ped.position = screenPos;
            es.RaycastAll(_ped, Results);
            return Results;
        }

        /// <summary>The top raycast target at a screen position (e.target), or null.</summary>
        public static GameObject TopAt(Vector2 screenPos)
        {
            List<RaycastResult> r = RaycastAll(screenPos);
            return r.Count > 0 ? r[0].gameObject : null;
        }

        /// <summary>The first component T on the top hit or its parents (e.target.closest(...)), or null.</summary>
        public static T TopComponentAt<T>(Vector2 screenPos) where T : Component
        {
            GameObject go = TopAt(screenPos);
            return go ? go.GetComponentInParent<T>() : null;
        }

        /// <summary>Which of `layers` the top hit sits in (the JS's layer.hud.contains(e.target)), or null when none / no hit.</summary>
        public static RectTransform TopLayerAt(Vector2 screenPos, IReadOnlyList<RectTransform> layers)
        {
            GameObject go = TopAt(screenPos);
            if (!go || layers == null) return null;
            Transform t = go.transform;
            for (int i = 0; i < layers.Count; i++)
                if (layers[i] && t.IsChildOf(layers[i])) return layers[i];
            return null;
        }

        /// <summary>Whether the top hit at a screen position lies under root.</summary>
        public static bool IsOver(Vector2 screenPos, Transform root)
        {
            GameObject go = TopAt(screenPos);
            return go && root && go.transform.IsChildOf(root);
        }

        /// <summary>Makes rt's whole box a pointer target without drawing anything (a HitArea; added when missing).</summary>
        public static HitArea Area(RectTransform rt)
        {
            HitArea a = rt.GetComponent<HitArea>();
            if (!a) a = rt.gameObject.AddComponent<HitArea>();
            a.raycastTarget = true;
            return a;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Results.Clear();
            _ped = null;
            _pedOwner = null;
        }
    }

    /// <summary>An invisible raycast target over its RectTransform's box (no mesh, no draw call): a DOM box that takes pointer events.</summary>
    [AddComponentMenu("")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HitArea : UnityEngine.UI.Graphic
    {
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh) => vh.Clear();
    }
}
