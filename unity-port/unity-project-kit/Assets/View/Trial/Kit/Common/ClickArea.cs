// Pointer listeners on an element (the JS onclick / onmouseenter / onmouseleave / onpointerdown / onpointerup): uGUI
// pointer events turned into C# events, with the microtask queue drained after each (as the browser runs microtasks
// after every event listener). The element becomes a raycast target: its own graphic, or an invisible HitArea.
// `click` fires for the primary button only, as the browser's click event does.
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Trial.Kit
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class ClickArea : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        /// <summary>click (primary button).</summary>
        public event Action<PointerEventData> Click;
        /// <summary>mouseenter / pointerenter.</summary>
        public event Action<PointerEventData> Enter;
        /// <summary>mouseleave / pointerleave.</summary>
        public event Action<PointerEventData> Exit;
        /// <summary>pointerdown (any button).</summary>
        public event Action<PointerEventData> Down;
        /// <summary>pointerup (any button).</summary>
        public event Action<PointerEventData> Up;

        /// <summary>The pointer is over the element (CSS :hover).</summary>
        public bool Hovered { get; private set; }

        /// <summary>The element as a pointer target (adds the listener component; turns its graphic's raycast on, or adds a HitArea).</summary>
        public static ClickArea On(Component element)
        {
            if (!element) throw new ArgumentNullException(nameof(element));
            GameObject go = element.gameObject;
            ClickArea a = go.GetComponent<ClickArea>();
            if (!a) a = go.AddComponent<ClickArea>();
            Graphic g = go.GetComponent<Graphic>();
            if (g) g.raycastTarget = true;
            else Hit.Area((RectTransform)go.transform);
            return a;
        }

        private static void Run(Action<PointerEventData> handlers, PointerEventData e)
        {
            if (handlers == null) return;
            foreach (Delegate d in handlers.GetInvocationList())
            {
                try { ((Action<PointerEventData>)d)(e); }
                catch (Exception ex) { Debug.LogException(ex); }
                Microtasks.Drain();
            }
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e != null && e.button != PointerEventData.InputButton.Left) return;
            Run(Click, e);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            Hovered = true;
            try { HoverChanged?.Invoke(true); }
            catch (Exception ex) { Debug.LogException(ex); }
            Run(Enter, e);
        }

        public void OnPointerExit(PointerEventData e)
        {
            Hovered = false;
            try { HoverChanged?.Invoke(false); }
            catch (Exception ex) { Debug.LogException(ex); }
            Run(Exit, e);
        }

        public void OnPointerDown(PointerEventData e) => Run(Down, e);

        public void OnPointerUp(PointerEventData e) => Run(Up, e);

        /// <summary>CSS :hover turned on or off (enter, leave, or the element hidden while hovered).</summary>
        public event Action<bool> HoverChanged;

        // A hidden element is no longer under the pointer: :hover goes, but no mouseleave fires (as in the browser).
        private void OnDisable()
        {
            if (!Hovered) return;
            Hovered = false;
            try { HoverChanged?.Invoke(false); }
            catch (Exception ex) { Debug.LogException(ex); }
        }
    }
}
