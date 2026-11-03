// #828: the attribute words of a card's trait text (攻撃 / 防御 / スキル / スタンス) printed as the attribute's icon
// (OCB.ATTR: sword / shield / star / flag) in the attribute's frame colour, the colour the battle result's bars use.
// Trial.Geometry's AttrIconText splits the text; the label prints each word as one hidden full-width slot, so the
// fitting and wrapping that measure the text see the room the icon takes, and TextAttrIcons draws the icons over the
// slots wherever TMP laid them out (again whenever the label is laid out again: a face built under an inactive parent,
// a font that arrived late, a resized row).
using System.Collections.Generic;
using TMPro;
using Trial.Geometry;
using UnityEngine;

namespace Trial.Kit
{
    public static partial class Ocb
    {
        /// <summary>Prints <paramref name="text"/> on the label with its attribute words as icons; returns the split (Display is what was laid out).</summary>
        public static AttrIconText PrintAttrIcons(TMP_Text label, string text)
        {
            AttrIconText split = AttrIconText.Of(text);
            PrintAttrIcons(label, split.Display, split.Attrs);
            return split;
        }

        /// <summary>
        /// Prints a display string (AttrIconText.Display, wrapped or not) with one icon per slot, the k-th slot taking
        /// attrs[k]. No attrs: the text is printed as it is and any icons the label had are dropped.
        /// </summary>
        public static void PrintAttrIcons(TMP_Text label, string display, IReadOnlyList<string> attrs)
        {
            if (!label) return;
            display = display ?? "";
            TextAttrIcons marks = label.GetComponent<TextAttrIcons>();
            LabelState st = Label.StateOf(label);
            bool icons = attrs != null && attrs.Count > 0;
            if (!icons)
            {
                if (marks)
                {
                    marks.Set(label, null);
                    // the rich text was ours: a later '<' in plain text must stay text
                    TextStyle s0 = Label.StyleOf(label);
                    if (s0 != null && s0.RichText)
                    {
                        s0 = s0.Clone();
                        s0.RichText = false;
                        Label.Apply(label, s0);
                    }
                }
                if ((st.Text ?? label.text) != display) Label.SetText(label, display);
                return;
            }
            TextStyle s = Label.StyleOf(label);
            if (s != null && !s.RichText)
            {
                s = s.Clone();
                s.RichText = true;
                Label.Apply(label, s);
            }
            string rich = AttrIconText.Rich(display);
            if ((st.Text ?? label.text) != rich) Label.SetText(label, rich);
            if (!marks) marks = label.gameObject.AddComponent<TextAttrIcons>();
            marks.Set(label, attrs);
        }
    }

    /// <summary>The attribute icons over a label's hidden slots (Ocb.PrintAttrIcons), placed from TMP's laid-out glyphs.</summary>
    [AddComponentMenu("")]
    public sealed class TextAttrIcons : MonoBehaviour
    {
        /// <summary>The icon's side in em of the row's font size (the slot is one full-width character).</summary>
        public const float IconEm = 1f;

        /// <summary>Where a slot's centre sits above the baseline when TMP gives no glyph box (the ideographic em box's centre).</summary>
        private const float CentreAboveBaselineEm = 0.38f;

        private TMP_Text _label;
        private readonly List<string> _attrs = new List<string>();
        private readonly List<PathGraphic> _icons = new List<PathGraphic>();
        private bool _dirty;

        /// <summary>The icons, in slot order.</summary>
        public IReadOnlyList<PathGraphic> Icons => _icons;

        internal void Set(TMP_Text label, IReadOnlyList<string> attrs)
        {
            _label = label;
            bool same = attrs != null && attrs.Count == _attrs.Count;
            for (int i = 0; same && i < _attrs.Count; i++) same = _attrs[i] == attrs[i];
            if (!same)
            {
                foreach (PathGraphic g in _icons) if (g) Destroy(g.gameObject);
                _icons.Clear();
                _attrs.Clear();
                if (attrs != null)
                {
                    foreach (string key in attrs)
                    {
                        OcbAttrInfo a = Ocb.AttrOf(key);
                        PathGraphic g = Ocb.Icon(label.rectTransform, a.Icon, 16f, CssColor.Parse(a.FrameCss, Ocb.Theme));
                        g.gameObject.name = "ocbg-attr-icon " + key;
                        g.raycastTarget = false;
                        _icons.Add(g);
                        _attrs.Add(key);
                    }
                }
            }
            Place();
        }

        private void OnEnable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            _dirty = true;
        }

        private void OnDisable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        }

        private void OnTextChanged(Object o)
        {
            if (_label && o == _label) _dirty = true;
        }

        private void LateUpdate()
        {
            if (_dirty) Place();
        }

        private void Place()
        {
            if (!_label || _icons.Count == 0)
            {
                _dirty = false;
                return;
            }
            TMP_TextInfo info = _label.textInfo;
            if ((info == null || info.characterCount == 0) && !string.IsNullOrEmpty(_label.text))
            {
                _label.ForceMeshUpdate();
                info = _label.textInfo;
            }
            // after the forced layout, whose own text-changed event would otherwise ask for this again
            _dirty = false;
            Rect r = _label.rectTransform.rect;
            float fallbackSize = Label.StyleOf(_label)?.Size ?? _label.fontSize;
            int k = 0;
            int count = info != null ? info.characterCount : 0;
            for (int i = 0; i < count && k < _icons.Count; i++)
            {
                TMP_CharacterInfo ci = info.characterInfo[i];
                if (ci.character != AttrIconText.Slot) continue;
                PathGraphic g = _icons[k++];
                if (!g) continue;
                float em = ci.pointSize > 0f ? ci.pointSize : fallbackSize;
                float size = em * IconEm;
                float cx = (ci.origin + ci.xAdvance) * 0.5f;
                float cy = ci.isVisible ? (ci.topLeft.y + ci.bottomLeft.y) * 0.5f : ci.baseLine + CentreAboveBaselineEm * em;
                Ui.Place(g.rectTransform, cx - r.xMin - size * 0.5f, r.yMax - cy - size * 0.5f, size, size);
                g.enabled = true;
            }
            // a slot TMP cut off (overflow) has no icon either
            for (; k < _icons.Count; k++) if (_icons[k]) _icons[k].enabled = false;
        }
    }
}
