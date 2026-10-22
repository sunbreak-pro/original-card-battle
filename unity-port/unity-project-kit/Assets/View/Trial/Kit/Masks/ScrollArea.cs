// An overflow-y: auto box (meta-b §6.2, kit.md §3.1 scrollbars): a viewport that clips (RectMask2D), a content
// child that moves by scrollTop, the mouse wheel (100 px a notch, Chrome's step, eased over 150 ms like Chrome's
// smooth scrolling), optional mandatory snapping (scroll-snap-type: y mandatory: the deck list's rows), the edge fades
// that follow is-more-above / is-more-below (FadeMask), and the place of an 8 px scrollbar thumb.
//
// The thumb's look is the owner's (meta: steel, journal: washi-ink 30 %, radius 4): ScrollArea only sizes and moves
// the Thumb RectTransform. A custom ::-webkit-scrollbar takes layout room, so while the content overflows and the
// thumb is shown, ContentWidth is the box width minus the thumb's.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Trial.Kit
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class ScrollArea : MonoBehaviour, IScrollHandler
    {
        /// <summary>Px per wheel notch (Chrome on Windows).</summary>
        public float WheelPx = 100f;

        /// <summary>Wheel scrolls ease over SmoothMs (Chrome's smooth scrolling); false jumps.</summary>
        public bool SmoothWheel = true;

        public float SmoothMs = 150f;

        /// <summary>Shows the thumb (::-webkit-scrollbar width 8); the owner draws into Thumb.</summary>
        public bool ShowThumb;

        public float ThumbWidth = 8f;

        /// <summary>The smallest thumb length (Chrome keeps a thumb at least this long).</summary>
        public float ThumbMin = 18f;

        /// <summary>scroll-snap-type: y mandatory: the snap positions (scrollTop values), or null.</summary>
        public Func<IReadOnlyList<float>> SnapPoints;

        /// <summary>The viewport (this element's box).</summary>
        public RectTransform Viewport => (RectTransform)transform;

        /// <summary>What scrolls: put the rows here (top-left = scrollTop 0).</summary>
        public RectTransform Content { get; private set; }

        /// <summary>Where the thumb goes (sized and moved here; hidden when nothing overflows or ShowThumb is off).</summary>
        public RectTransform Thumb { get; private set; }

        public FadeMask Fade { get; private set; }

        /// <summary>Raised after scrollTop changed (the JS's `scroll` listener: is-more-above / below, lazy rows).</summary>
        public event Action Scrolled;

        private float _scrollTop;
        private float? _scrollHeight;
        private float _from, _to, _t0 = -1f;
        private string _fadeBelow, _fadeAbove, _fadeBoth;
        private int _fadeState = -1;

        /// <summary>A scroll box at (x, y) in parent's stage px, w × h.</summary>
        public static ScrollArea Make(RectTransform parent, string name, float x, float y, float w, float h)
        {
            RectTransform box = Ui.Div(parent, string.IsNullOrEmpty(name) ? "scroll" : name, x, y, w, h);
            return Attach(box);
        }

        /// <summary>Turns an existing box into a scroll box.</summary>
        public static ScrollArea Attach(RectTransform box)
        {
            ScrollArea a = box.GetComponent<ScrollArea>();
            if (a) return a;
            a = box.gameObject.AddComponent<ScrollArea>();
            a.Build();
            return a;
        }

        private void Build()
        {
            if (!GetComponent<RectMask2D>()) gameObject.AddComponent<RectMask2D>();
            Hit.Area(Viewport); // the wheel lands on the box, not only on its rows
            Content = Ui.Div(Viewport, "content");
            Content.sizeDelta = new Vector2(Viewport.rect.width, 0f);
            Thumb = Ui.Div(Viewport, "thumb");
            Thumb.gameObject.SetActive(false);
            Fade = FadeMask.Of(Viewport);
            Apply();
        }

        // ---- metrics ------------------------------------------------------------------------------------

        /// <summary>clientHeight.</summary>
        public float ClientHeight => Viewport.rect.height;

        /// <summary>The box width the rows may use (minus the thumb while it shows).</summary>
        public float ContentWidth => Viewport.rect.width - (ShowThumb && MaxScroll > 0.5f ? ThumbWidth : 0f);

        /// <summary>scrollHeight: set it after laying the rows out, or leave it null to use Content's height.</summary>
        public float ScrollHeight
        {
            get => Math.Max(ClientHeight, _scrollHeight ?? (Content ? Content.rect.height : 0f));
            set { _scrollHeight = value; Clamp(); Apply(); }
        }

        public float MaxScroll => Math.Max(0f, ScrollHeight - ClientHeight);

        /// <summary>scrollTop (clamped to 0..MaxScroll).</summary>
        public float ScrollTop
        {
            get => _scrollTop;
            set
            {
                _t0 = -1f;
                SetTop(value);
            }
        }

        /// <summary>The slack every JS list uses for its edge cues (hall.js:146-148, departure.js:357-361, ExploreScene.js:1909-1911).</summary>
        public const float EdgeSlackPx = 2f;

        // js: scenes/meta/hall.js:146 (more = scrollHeight − clientHeight > 2)
        /// <summary>The content overflows the box by more than the slack.</summary>
        public bool More => MaxScroll > EdgeSlackPx;

        // js: scenes/meta/hall.js:148 (is-more-above: more && scrollTop > 2)
        public bool MoreAbove => More && _scrollTop > EdgeSlackPx;

        // js: scenes/meta/hall.js:147 (is-more-below: more && scrollTop + clientHeight < scrollHeight − 2)
        public bool MoreBelow => More && _scrollTop < MaxScroll - EdgeSlackPx;

        private void SetTop(float v)
        {
            float top = Math.Max(0f, Math.Min(MaxScroll, v));
            if (Math.Abs(top - _scrollTop) < 0.0001f) { Apply(); return; }
            _scrollTop = top;
            Apply();
            try { Scrolled?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
            Microtasks.Drain();
        }

        private void Clamp()
        {
            float top = Math.Max(0f, Math.Min(MaxScroll, _scrollTop));
            if (top != _scrollTop) _scrollTop = top;
        }

        /// <summary>Re-reads the content height, clamps, moves the thumb and the fades (call after the rows changed).</summary>
        public void Refresh()
        {
            Clamp();
            Apply();
            if (Fade) Fade.MarkDirty();
        }

        // ---- scrolling ----------------------------------------------------------------------------------

        /// <summary>el.scrollBy(0, dy).</summary>
        public void ScrollBy(float dy, bool smooth = false) => ScrollTo(Target + dy, smooth);

        /// <summary>el.scrollTo(0, top); smooth eases over SmoothMs.</summary>
        public void ScrollTo(float top, bool smooth = false)
        {
            top = Math.Max(0f, Math.Min(MaxScroll, top));
            if (!smooth || SmoothMs <= 0f) { ScrollTop = top; return; }
            _from = _scrollTop;
            _to = top;
            _t0 = Time.unscaledTime;
        }

        /// <summary>scrollIntoView({block: 'nearest'}) for a row at itemTop (content px) itemHeight tall.</summary>
        public void ScrollIntoView(float itemTop, float itemHeight, bool smooth = false)
        {
            float top = Target;
            if (itemTop < top) ScrollTo(itemTop, smooth);
            else if (itemTop + itemHeight > top + ClientHeight) ScrollTo(itemTop + itemHeight - ClientHeight, smooth);
        }

        /// <summary>Where the scroll is heading (the end of a running smooth scroll).</summary>
        public float Target => _t0 >= 0f ? _to : _scrollTop;

        public void OnScroll(PointerEventData eventData)
        {
            float perTick = 1f;
            if (EventSystem.current && EventSystem.current.currentInputModule is InputSystemUIInputModule m && Math.Abs(m.scrollDeltaPerTick) > 1e-5f)
                perTick = m.scrollDeltaPerTick;
            float notches = eventData.scrollDelta.y / perTick;
            if (Math.Abs(notches) < 1e-4f) return;
            // uGUI's +y is the wheel turned away (content goes down); the DOM's deltaY is the other way round.
            float dy = -notches * WheelPx;
            float target = Target + dy;
            IReadOnlyList<float> snaps = SnapPoints?.Invoke();
            if (snaps != null && snaps.Count > 0) target = NextSnap(snaps, Target, dy);
            ScrollTo(target, SmoothWheel);
            eventData.Use();
        }

        // Mandatory snapping on a wheel step: the next snap position in the scroll's direction, at least one.
        private static float NextSnap(IReadOnlyList<float> snaps, float from, float dy)
        {
            float want = from + dy;
            float best = from;
            float bestD = float.MaxValue;
            foreach (float s in snaps)
            {
                if (dy > 0f && s <= from + 0.5f) continue;
                if (dy < 0f && s >= from - 0.5f) continue;
                float d = Math.Abs(s - want);
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        // ---- fades (is-more-above / is-more-below) ---------------------------------------------------------

        /// <summary>
        /// The mask-image of each state, as the CSS writes them: below (only more below), above (only more above),
        /// both. A null state has no mask.
        /// </summary>
        public void SetFades(string below, string above, string both)
        {
            _fadeBelow = below;
            _fadeAbove = above;
            _fadeBoth = both;
            _fadeState = -1;
            ApplyFade();
        }

        private void ApplyFade()
        {
            if (!Fade) return;
            int state = (MoreAbove ? 1 : 0) | (MoreBelow ? 2 : 0);
            if (state != _fadeState)
            {
                _fadeState = state;
                string css = state == 3 ? _fadeBoth : state == 2 ? _fadeBelow : state == 1 ? _fadeAbove : null;
                if (string.IsNullOrEmpty(css)) Fade.Clear(); else Fade.SetCss(css);
            }
            Fade.MarkDirty();
        }

        // ---- the frame ----------------------------------------------------------------------------------

        private void Update()
        {
            if (_t0 < 0f) return;
            float p = SmoothMs > 0f ? (Time.unscaledTime - _t0) * 1000f / SmoothMs : 1f;
            if (p >= 1f)
            {
                float to = _to;
                _t0 = -1f;
                SetTop(to);
                return;
            }
            float e = 1f - (1f - p) * (1f - p) * (1f - p);
            float v = _from + (_to - _from) * e;
            float keep = _t0;
            SetTop(v);
            _t0 = keep;
        }

        private void Apply()
        {
            if (!Content) return;
            Content.anchorMin = Content.anchorMax = new Vector2(0f, 1f);
            Content.pivot = new Vector2(0f, 1f);
            Content.anchoredPosition = new Vector2(0f, _scrollTop);
            float client = ClientHeight;
            float max = MaxScroll;
            bool show = ShowThumb && max > 0.5f;
            if (Thumb)
            {
                if (Thumb.gameObject.activeSelf != show) Thumb.gameObject.SetActive(show);
                if (show)
                {
                    float total = ScrollHeight;
                    float len = Math.Max(ThumbMin, client * client / total);
                    float y = max > 0f ? _scrollTop / max * (client - len) : 0f;
                    Ui.Place(Thumb, Viewport.rect.width - ThumbWidth, y, ThumbWidth, len);
                    if (Thumb.GetSiblingIndex() != Viewport.childCount - 1) Thumb.SetAsLastSibling();
                }
            }
            ApplyFade();
        }
    }
}
