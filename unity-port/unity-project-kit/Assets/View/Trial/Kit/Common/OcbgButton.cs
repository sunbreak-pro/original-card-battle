// The game's button, `<button class="ocbg-btn [is-primary] [is-danger]">` (common.css:6-12): a box with the label
// centred in its content box, sized to its text (max-content + padding 16 28 + border) unless the layout gives it a
// box (a flex row stretches it). States follow the stylesheet's rules in cascade order (CommonStyles.Btn): :hover from
// the pointer, :focus-visible from Tab (the Focus ring), [disabled]. common.css declares no transition for it, so
// every state change is immediate. onclick is Click; Press() is b.click() (nothing when disabled).
using System;
using TMPro;
using Trial.Geometry;
using UnityEngine;

namespace Trial.Kit
{
    public sealed class OcbgButton
    {
        public RectTransform Root { get; private set; }
        public UiNode Node { get; private set; }
        public BoxGraphic Box { get; private set; }
        public TextMeshProUGUI Label { get; private set; }
        public ClickArea Area { get; private set; }
        public CssStates States { get; private set; }

        /// <summary>onclick.</summary>
        public event Action Click;

        private bool _primary, _danger, _disabled, _hover, _focus;
        private bool _autoSize = true;
        private string _text;

        // js: scenes/common/stage.js:187 (h('button', { class: 'ocbg-btn …', type: 'button', onclick }, text))
        /// <summary>An .ocbg-btn under parent at (0, 0), sized to its text.</summary>
        public static OcbgButton Make(RectTransform parent, string text, bool primary = false, bool danger = false, string name = null)
        {
            var b = new OcbgButton { _primary = primary, _danger = danger, _text = text ?? "" };
            b.Root = Ui.Div(parent, name ?? "ocbg-btn");
            b.Node = UiNode.Of(b.Root);
            b.Box = BoxGraphic.On(b.Root);
            b.Label = global::Trial.Kit.Label.Make(b.Root, b._text, CommonStyles.BtnText(), 0f, 0f, 1f, 1f, "label");
            b.Area = ClickArea.On(b.Box);
            b.Area.Click += _ => b.Press();
            b.Area.HoverChanged += on => b.SetHover(on);
            b.States = CssStates.Of(b.Root);
            b.States.Text = b.Label;
            b.States.Base(CommonStyles.BtnCss)
                .State(":hover", CommonStyles.BtnHoverCss)
                .State(":focus-visible", CommonStyles.BtnFocusVisibleCss)
                .State(".is-primary", CommonStyles.BtnPrimaryCss, primary)
                .State(".is-danger", CommonStyles.BtnDangerCss, danger)
                .State("[disabled]", CommonStyles.BtnDisabledCss)
                .State(".is-primary:hover", CommonStyles.BtnPrimaryHoverCss);
            b.States.Apply(instant: true);
            b.Layout();
            return b;
        }

        public string Text
        {
            get => _text;
            set
            {
                _text = value ?? "";
                global::Trial.Kit.Label.SetText(Label, _text);
                Layout();
            }
        }

        public bool Primary { get => _primary; set { _primary = value; Restate(); } }
        public bool Danger { get => _danger; set { _danger = value; Restate(); } }

        /// <summary>[disabled]: opacity .4, no clicks, Tab skips it.</summary>
        public bool Disabled
        {
            get => _disabled;
            set
            {
                _disabled = value;
                if (Focusable != null) Focusable.Disabled = value;
                Restate();
            }
        }

        public bool Hovered => _hover;

        private void SetHover(bool on)
        {
            _hover = on;
            Restate();
        }

        /// <summary>The Tab focus ring (:focus-visible: 2px solid --wick, offset 2px).</summary>
        public void SetFocusRing(bool on)
        {
            _focus = on;
            Restate();
        }

        private void Restate()
        {
            if (!Root) return;
            bool changed = States.SetState(".is-primary", _primary, false);
            changed |= States.SetState(".is-danger", _danger, false);
            changed |= States.SetState(":hover", _hover, false);
            changed |= States.SetState(":focus-visible", _focus, false);
            changed |= States.SetState("[disabled]", _disabled, false);
            changed |= States.SetState(".is-primary:hover", _primary && _hover, false);
            if (!changed) return;
            States.Apply();
            Layout();
        }

        /// <summary>b.click(): onclick runs unless the button is disabled.</summary>
        public void Press()
        {
            if (_disabled || !Root) return;
            try { Click?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
            Microtasks.Drain();
        }

        // ---- focus -----------------------------------------------------------------------------------------

        /// <summary>Its entry in a Tab scope (null until FocusIn).</summary>
        public Focusable Focusable { get; private set; }

        /// <summary>Adds the button to a Tab scope (DOM order: call in the order the buttons stand).</summary>
        public Focusable FocusIn(FocusScope scope)
        {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            Focusable = scope.Add(Root, Press, SetFocusRing);
            Focusable.Disabled = _disabled;
            return Focusable;
        }

        // ---- size ------------------------------------------------------------------------------------------

        /// <summary>The border widths in force.</summary>
        public Insets Border => Box.Style.BorderWidths;

        /// <summary>The max-content size: text width + padding + border × line height + padding + border.</summary>
        public Vector2 NaturalSize
        {
            get
            {
                BoxStyle s = Box.Style;
                Insets bw = s.BorderWidths, pad = s.Padding;
                TextStyle ts = global::Trial.Kit.Label.StyleOf(Label) ?? CommonStyles.BtnText();
                float tw = TextMeasure.Width(_text, ts);
                return new Vector2(tw + pad.Horizontal + bw.Horizontal, ts.LineHeightPx + pad.Vertical + bw.Vertical);
            }
        }

        /// <summary>Gives the button a box (a flex row stretched it); the label stays centred in the content box.</summary>
        public void SetBox(float x, float y, float w, float h)
        {
            _autoSize = false;
            Node.Place(x, y, w, h);
            Layout();
        }

        /// <summary>Places the button (its size stays).</summary>
        public void Place(float x, float y)
        {
            Node.X = x;
            Node.Y = y;
        }

        /// <summary>Back to the size of its text.</summary>
        public void AutoSize()
        {
            _autoSize = true;
            Layout();
        }

        /// <summary>The label in the content box, vertically centred (a button centres its content).</summary>
        public void Layout()
        {
            if (!Root) return;
            if (_autoSize)
            {
                Vector2 n = NaturalSize;
                Node.W = n.x;
                Node.H = n.y;
            }
            BoxStyle s = Box.Style;
            RectF content = s.ContentBox(new RectF(0f, 0f, Node.W, Node.H));
            TextStyle ts = global::Trial.Kit.Label.StyleOf(Label) ?? CommonStyles.BtnText();
            float lh = ts.LineHeightPx;
            Ui.Place(Label.rectTransform, content.X, content.Y + (content.H - lh) * 0.5f, content.W, lh);
            LabelState st = global::Trial.Kit.Label.StateOf(Label);
            st.AutoW = false;
            st.AutoH = false;
            global::Trial.Kit.Label.Relayout(Label);
        }
    }
}
