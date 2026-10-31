// meta/util.js styler: a tweenable view of an element's opacity and transform — clock.Tween(p, ("o", 1), ("y", 0), 300).
// Keys: o (opacity), x, y (px, y down), s (scale), r (deg, clockwise). Every set writes the element's opacity and its
// whole CSS transform `translate(x, y) scale(s) rotate(r)` (the UiNode's transform channel), as the JS apply() does.
using System;
using UnityEngine;

namespace Trial.Kit
{
    public sealed class Styler : ITweenable
    {
        private float _o = 1f, _x, _y, _s = 1f, _r;

        /// <summary>p.el: the element's node.</summary>
        public UiNode El { get; }

        private Styler(UiNode el) { El = el; }

        // js: scenes/meta/util.js:34
        /// <summary>styler(el, init): o 1, x 0, y 0, s 1, r 0 unless init says otherwise; applied at once.</summary>
        public static Styler Of(UiNode el, float o = 1f, float x = 0f, float y = 0f, float s = 1f, float r = 0f)
        {
            if (el == null) throw new ArgumentNullException(nameof(el));
            var p = new Styler(el) { _o = o, _x = x, _y = y, _s = s, _r = r };
            p.Apply();
            return p;
        }

        public static Styler Of(RectTransform el, float o = 1f, float x = 0f, float y = 0f, float s = 1f, float r = 0f)
            => Of(UiNode.Of(el), o, x, y, s, r);

        public float O { get => _o; set { _o = value; Apply(); } }
        public float X { get => _x; set { _x = value; Apply(); } }
        public float Y { get => _y; set { _y = value; Apply(); } }
        public float S { get => _s; set { _s = value; Apply(); } }
        public float R { get => _r; set { _r = value; Apply(); } }

        /// <summary>p.set(vals): assigns the given keys, applies once, returns p.</summary>
        public Styler Set(float? o = null, float? x = null, float? y = null, float? s = null, float? r = null)
        {
            if (o.HasValue) _o = o.Value;
            if (x.HasValue) _x = x.Value;
            if (y.HasValue) _y = y.Value;
            if (s.HasValue) _s = s.Value;
            if (r.HasValue) _r = r.Value;
            Apply();
            return this;
        }

        private void Apply()
        {
            if (!El) return;
            El.Opacity = _o;
            // translate(x, y) scale(s) rotate(r): the whole transform property.
            El.SetTransform(_x, _y, _s, _s, _r);
        }

        public float GetTween(string key)
        {
            switch (key)
            {
                case "o": return _o;
                case "x": return _x;
                case "y": return _y;
                case "s": return _s;
                case "r": return _r;
                default: return 0f;
            }
        }

        public void SetTween(string key, float value)
        {
            switch (key)
            {
                case "o": O = value; break;
                case "x": X = value; break;
                case "y": Y = value; break;
                case "s": S = value; break;
                case "r": R = value; break;
            }
        }
    }
}
