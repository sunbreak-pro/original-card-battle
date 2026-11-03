// The next heir's name as the player may write it (#105, concept-v3 §16.1, Koudai-san 2026-11-03): the hall draws a
// name, puts it in this field, and the player writes over it or leaves it. Not in the browser trial (it drew the name
// only), so the look is this port's own, kept to the hall's tokens: a caption over a one-line field the width of the
// 装束 column (the figure stands to its right), the drawn name as the placeholder when the field is emptied. The
// name the life starts with is Inheritance.ChosenHeirName(Value, drawn): blanks and control characters never reach it.
//
// Japanese goes through the IME: TMP_InputField reads the composition through the input module's BaseInput, so while
// the field has the focus JournalMemoIme stands in for it with the Input System's IME API (as the journal's memo sheet
// does). KeyStack keeps every key but Escape away from the hall while the field is focused and turns the IME on.
using System;
using InheritanceCore;
using TMPro;
using Trial.Geometry;
using Trial.Kit;
using Trial.Script;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Trial.Meta
{
    internal sealed class HeirNameField
    {
        // scene text: the JS has no name field, so MetaText (the JS's table) does not hold this line
        internal const string Caption = "名前は書き換えられる";

        internal const string CaptionCss = "font: 400 16px/1.2 var(--font-body); letter-spacing: .08em; color: var(--steel);";
        internal const string FieldCss = "background: var(--ground); border: var(--stroke-rule) solid var(--rule); border-radius: var(--radius-tag);";
        internal const string FocusCss = "border-color: var(--amber);";
        internal const string TextCss = "font: 700 26px/1 var(--font-display); letter-spacing: .1em; white-space: nowrap;";
        internal const float FieldH = 48f, CaptionGap = 6f, PadX = 14f;

        private readonly RectTransform _root;
        private readonly BoxGraphic _box;
        private readonly TMP_InputField _input;
        private readonly JournalMemoIme _ime;
        private readonly Action<string> _changed;
        private BaseInputModule _module;
        private BaseInput _prevOverride;
        private int _submittedFrame = -10;
        private bool _disposed;

        private HeirNameField(RectTransform parent, string drawn, float w, Action<string> changed)
        {
            _changed = changed;
            TextStyle caption = MetaStyle.Text(CaptionCss);
            float ch = caption.LineHeightPx;
            _root = Ui.Div(parent, "ocbg-meta-heir-namefield", 0f, 0f, w, ch + CaptionGap + FieldH);
            UiNode.Of(_root);
            Label.Make(_root, Caption, caption, 0f, 0f, w, ch, "caption");

            RectTransform field = Ui.Div(_root, "field", 0f, ch + CaptionGap, w, FieldH);
            GameObject go = field.gameObject;
            go.SetActive(false);
            _box = BoxGraphic.On(field, FieldCss);
            _box.raycastTarget = true;

            RectTransform area = Ui.Div(field, "text-area");
            Ui.Fill(area);
            area.gameObject.AddComponent<RectMask2D>();
            TextStyle text = MetaStyle.Text(TextCss);
            TextStyle ph = text.Clone();
            ph.Color = ph.Color.MulAlpha(0.45f);
            TextMeshProUGUI placeholder = NewText(area, "placeholder", ph);
            placeholder.text = drawn ?? "";
            TextMeshProUGUI label = NewText(area, "text", text);

            _ime = go.AddComponent<JournalMemoIme>();
            _input = go.AddComponent<TMP_InputField>();
            _input.transition = Selectable.Transition.None;
            _input.navigation = new Navigation { mode = Navigation.Mode.None };
            _input.targetGraphic = _box;
            _input.textViewport = area;
            _input.textComponent = label;
            _input.placeholder = placeholder;
            _input.lineType = TMP_InputField.LineType.SingleLine;
            _input.characterLimit = InheritanceRules.HeirNameMaxLength;
            _input.richText = false;
            _input.restoreOriginalTextOnEscape = false;
            _input.onFocusSelectAll = true;
            _input.resetOnDeActivation = true;
            _input.customCaretColor = true;
            _input.caretBlinkRate = 1000f / 1060f;   // as the memo sheet: the system's 530 ms on, 530 ms off
            ApplyColors();
            _input.SetTextWithoutNotify(drawn ?? "");
            _input.interactable = false;
            _input.onValueChanged.AddListener(v => { if (!_disposed) _changed?.Invoke(v); });
            _input.onSubmit.AddListener(_ => _submittedFrame = Time.frameCount);
            _input.onSelect.AddListener(_ => OnFocus(true));
            _input.onDeselect.AddListener(_ => OnFocus(false));
            Materials.GammaEncodingChanged += ApplyColors;
            go.SetActive(true);
        }

        /// <summary>A caption and the field under <paramref name="parent"/>, w wide, holding the drawn name.</summary>
        public static HeirNameField Create(RectTransform parent, string drawn, float w, Action<string> changed) =>
            new HeirNameField(parent, drawn, w, changed);

        public RectTransform Root => _root;

        /// <summary>What the field holds now (the drawn name until the player writes over it).</summary>
        public string Value => _input ? _input.text : "";

        /// <summary>Whether the field takes the pointer and the keyboard (off until the hall has stamped the name).</summary>
        public bool Interactable
        {
            get => _input && _input.interactable;
            set
            {
                if (_input) _input.interactable = value;
            }
        }

        /// <summary>The field has the keyboard.</summary>
        public bool Focused => !_disposed && _input && _input.isFocused;

        /// <summary>The EventSystem has the field selected (TMP deactivates itself on Enter and stays selected).</summary>
        public bool Selected
        {
            get
            {
                if (_disposed || !_input) return false;
                EventSystem es = EventSystem.current;
                return es && es.currentSelectedGameObject == _input.gameObject;
            }
        }

        /// <summary>The IME is composing (its Enter and Esc belong to the composition).</summary>
        public bool Composing => _ime && _ime.Composing;

        /// <summary>
        /// Enter ended the editing this frame or the last: the hall must not take the same press as 出立 (TMP and the
        /// key stack read one keydown in the same frame, in an order neither controls).
        /// </summary>
        public bool JustSubmitted => Time.frameCount - _submittedFrame <= 1;

        private static TextMeshProUGUI NewText(RectTransform parent, string name, TextStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            Label.Apply(t, style);
            t.raycastTarget = false;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.margin = new Vector4(PadX, 0f, PadX, 0f);
            return t;
        }

        private void ApplyColors()
        {
            if (!_input) return;
            _input.caretColor = Materials.VertexColor(DsTokens.Amber);
            _input.selectionColor = Materials.VertexColor(DsTokens.Amber.MulAlpha(0.35f));
        }

        private void OnFocus(bool on)
        {
            if (_disposed) return;
            if (_box) _box.SetCss(FieldCss + (on ? FocusCss : ""));
            if (on) InstallIme();
            else RemoveIme();
        }

        private void InstallIme()
        {
            EventSystem es = EventSystem.current;
            BaseInputModule m = es ? es.currentInputModule : null;
            if (!m || !_ime) return;
            if (m.inputOverride != _ime)
            {
                _prevOverride = m.inputOverride;
                m.inputOverride = _ime;
            }
            _module = m;
            _ime.Hook();
        }

        private void RemoveIme()
        {
            if (_module && _module.inputOverride == _ime) _module.inputOverride = _prevOverride;
            _module = null;
            _prevOverride = null;
            if (_ime) _ime.Unhook();
        }

        /// <summary>Gives the keyboard back to the hall.</summary>
        public void Blur()
        {
            if (!_input) return;
            EventSystem es = EventSystem.current;
            if (es && es.currentSelectedGameObject == _input.gameObject) es.SetSelectedGameObject(null);
            else _input.DeactivateInputField();
        }

        public void Dispose()
        {
            if (_disposed) return;
            if (_input && _input.isFocused) Blur();
            RemoveIme();
            _disposed = true;
            Materials.GammaEncodingChanged -= ApplyColors;
            EventSystem es = EventSystem.current;
            if (es && _root && es.currentSelectedGameObject && es.currentSelectedGameObject.transform.IsChildOf(_root)) es.SetSelectedGameObject(null);
        }
    }
}
