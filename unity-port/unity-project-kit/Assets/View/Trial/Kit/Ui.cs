// The h() / place() side of stage.js for uGUI: plain RectTransforms placed in stage px (origin top-left, y down).
// Every Trial element uses anchor = pivot = (0, 1) and anchoredPosition = (x, −y) (TRIAL-PORT.md convention 5).
using System;
using UnityEngine;

namespace Trial.Kit
{
    public static class Ui
    {
        /// <summary>An empty element under parent (a div): anchor = pivot = (0,1), at (0,0), size 0, on the parent's layer.</summary>
        public static RectTransform Div(RectTransform parent, string name = "div")
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? "div" : name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            if (parent != null)
            {
                go.layer = parent.gameObject.layer;
                rt.SetParent(parent, false);
            }
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            return rt;
        }

        /// <summary>A div placed at once: Div + Place.</summary>
        public static RectTransform Div(RectTransform parent, string name, float x, float y, float? w = null, float? h = null)
            => Place(Div(parent, name), x, y, w, h);

        // js: scenes/common/stage.js:34
        /// <summary>
        /// place(el, x, y, w, h): left / top in stage px, and the size when given. When the element has a UiNode the
        /// node's box is set (its transform channels and origin stay); otherwise anchor = pivot = (0,1) and
        /// anchoredPosition = (x, −y).
        /// </summary>
        public static RectTransform Place(RectTransform rt, float x, float y, float? w = null, float? h = null)
        {
            if (rt == null) throw new ArgumentNullException(nameof(rt));
            UiNode node = rt.GetComponent<UiNode>();
            if (node)
            {
                node.Place(x, y, w, h);
                return rt;
            }
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            if (w.HasValue || h.HasValue)
                rt.sizeDelta = new Vector2(w ?? rt.sizeDelta.x, h ?? rt.sizeDelta.y);
            return rt;
        }

        /// <summary>CSS position: absolute; inset: 0 — the element fills its parent.</summary>
        public static RectTransform Fill(RectTransform rt)
        {
            if (rt == null) throw new ArgumentNullException(nameof(rt));
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>Sets the size only (CSS width / height).</summary>
        public static RectTransform Size(RectTransform rt, float w, float h)
        {
            UiNode node = rt.GetComponent<UiNode>();
            if (node) { node.W = w; node.H = h; return rt; }
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Left / top of a placed element in stage px (the inverse of Place for anchor (0,1) elements).</summary>
        public static Vector2 PosOf(RectTransform rt)
        {
            UiNode node = rt.GetComponent<UiNode>();
            if (node) return new Vector2(node.X, node.Y);
            Vector2 p = rt.pivot, s = rt.sizeDelta;
            return new Vector2(rt.anchoredPosition.x - p.x * s.x, -rt.anchoredPosition.y - (1f - p.y) * s.y);
        }

        /// <summary>Removes every child: detached first (so layout and counts see them gone at once), then destroyed.</summary>
        public static void Clear(RectTransform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform c = parent.GetChild(i);
                c.SetParent(null, false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(c.gameObject);
                else UnityEngine.Object.DestroyImmediate(c.gameObject);
            }
        }

        /// <summary>el.remove(): detach and destroy one element.</summary>
        public static void Remove(Component el)
        {
            if (!el) return;
            GameObject go = el.gameObject;
            go.transform.SetParent(null, false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(go);
            else UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
