// Small UGUI construction helpers shared by the View pieces. The whole screen is
// built in code, so no scene or prefab YAML is hand-edited.
#if UNITY_2021_2_OR_NEWER
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class UiKit
{
    public static Font Font { get; private set; }

    public static void EnsureFont()
    {
        if (Font == null) Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    public static RectTransform Rect(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    /// <summary>Anchored box from fractions: x 0..1 left→right, y 0..1 bottom→top.</summary>
    public static RectTransform Box(RectTransform parent, string name, float xMin, float yMin, float xMax, float yMax)
    {
        return Rect(parent, name, new Vector2(xMin, yMin), new Vector2(xMax, yMax));
    }

    /// <summary>A fixed-size element anchored at one point of its parent.</summary>
    public static RectTransform Point(RectTransform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 offset)
    {
        var rt = Rect(parent, name, anchor, anchor);
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = offset;
        return rt;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    public static Image Image(RectTransform parent, string name, Sprite sprite, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        var rt = Rect(parent, name, anchorMin, anchorMax);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    public static Image Fill(RectTransform parent, string name, Color color)
    {
        return Image(parent, name, ProceduralArt.White, color, Vector2.zero, Vector2.one);
    }

    public static Image Sprite(RectTransform parent, string name, Sprite sprite, Color color, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 offset)
    {
        var rt = Point(parent, name, anchor, pivot, size, offset);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    public static Image Panel(RectTransform parent, string name, float xMin, float yMin, float xMax, float yMax)
    {
        return Image(parent, name, ProceduralArt.White, BattleTheme.Panel, new Vector2(xMin, yMin), new Vector2(xMax, yMax));
    }

    /// <summary>Four thin edges inside <paramref name="parent"/> (an inner frame / emboss line).</summary>
    public static Image[] Frame(RectTransform parent, Color color, float thickness, float inset = 0f)
    {
        var edges = new Image[4];
        edges[0] = Image(parent, "FrameTop", ProceduralArt.White, color, new Vector2(0, 1), new Vector2(1, 1));
        edges[0].rectTransform.offsetMin = new Vector2(inset, -thickness - inset);
        edges[0].rectTransform.offsetMax = new Vector2(-inset, -inset);
        edges[1] = Image(parent, "FrameBottom", ProceduralArt.White, color, new Vector2(0, 0), new Vector2(1, 0));
        edges[1].rectTransform.offsetMin = new Vector2(inset, inset);
        edges[1].rectTransform.offsetMax = new Vector2(-inset, thickness + inset);
        edges[2] = Image(parent, "FrameLeft", ProceduralArt.White, color, new Vector2(0, 0), new Vector2(0, 1));
        edges[2].rectTransform.offsetMin = new Vector2(inset, inset);
        edges[2].rectTransform.offsetMax = new Vector2(thickness + inset, -inset);
        edges[3] = Image(parent, "FrameRight", ProceduralArt.White, color, new Vector2(1, 0), new Vector2(1, 1));
        edges[3].rectTransform.offsetMin = new Vector2(-thickness - inset, inset);
        edges[3].rectTransform.offsetMax = new Vector2(-inset, -inset);
        return edges;
    }

    public static void SetFrameColor(Image[] frame, Color color)
    {
        foreach (var e in frame) e.color = color;
    }

    public static Text Text(RectTransform parent, string name, int size, TextAnchor anchor, Color color, string text = "")
    {
        EnsureFont();
        var rt = Rect(parent, name, Vector2.zero, Vector2.one);
        rt.offsetMin = new Vector2(10f, 6f);
        rt.offsetMax = new Vector2(-10f, -6f);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = Font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color;
        t.text = text;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.supportRichText = false;
        return t;
    }

    public static Text Label(RectTransform parent, string name, int size, TextAnchor anchor, Color color, Vector2 anchorPoint, Vector2 pivot, Vector2 size2, Vector2 offset, string text = "")
    {
        EnsureFont();
        var rt = Point(parent, name, anchorPoint, pivot, size2, offset);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = Font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color;
        t.text = text;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.supportRichText = false;
        return t;
    }

    public static Button Button(RectTransform parent, string name, string label, Action onClick, Color bg, Color fg, int fontSize,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        var rt = Rect(parent, name, anchorMin, anchorMax);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = ProceduralArt.White;
        img.color = bg;
        img.raycastTarget = true;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        var colors = button.colors;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
        button.colors = colors;
        if (onClick != null) button.onClick.AddListener(() => onClick());
        Text(rt, "Label", fontSize, TextAnchor.MiddleCenter, fg, label);
        return button;
    }

    public static CanvasGroup Group(RectTransform rt)
    {
        var g = rt.gameObject.GetComponent<CanvasGroup>();
        return g != null ? g : rt.gameObject.AddComponent<CanvasGroup>();
    }

    public static void OnPointer(GameObject go, EventTriggerType type, Action<BaseEventData> handler)
    {
        var trigger = go.GetComponent<EventTrigger>();
        if (trigger == null) trigger = go.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(data => handler(data));
        trigger.triggers.Add(entry);
    }

    public static void ClearChildren(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            // Detach first: Destroy is deferred and layout groups would still count the child.
            Transform old = parent.GetChild(i);
            old.SetParent(null, false);
            UnityEngine.Object.Destroy(old.gameObject);
        }
    }
}
#endif
