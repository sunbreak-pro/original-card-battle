// Draws one layer's backdrop inside a parent area (battle-visual-v1 §2.4, #89, #607): the sky gradient,
// the layer's art if one is in Resources, the haze with its breathing light, and the drifting specks.
// Every value and every motion comes from Backdrop.BackdropSpec / BackdropMotion / BackdropSwitch; this
// file only lays them out.
//
// It knows nothing of the battle, so any screen can lay the same place behind itself:
// BackdropLayers.Create(parent, layer) fills the parent and animates on its own, and Show(layer) turns it
// with the 400 ms crossfade of battle_ui_ux_v2 §4.3. The battle's BackdropView is one user; the exploration
// screen lays its veil (ExplorationBackdropSpec.Veil) over this as a full-screen Image of its own.
//
// The art for a layer goes in Resources/Backdrops/layer-<n> (a Sprite, opaque, 1920×1080). Without it the
// code-only backdrop is the whole picture; with it the art sits over the sky and under the haze.
#if UNITY_2021_2_OR_NEWER
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Backdrop.View
{
    public class BackdropLayers : MonoBehaviour
    {
        /// <summary>Where the art of layer n is looked for under Resources (n = 1〜7).</summary>
        public const string ArtPath = "Backdrops/layer-";

        private sealed class Slot
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public LayerBackdrop Spec;
            public readonly List<KeyValuePair<Image, Haze>> Hazes = new List<KeyValuePair<Image, Haze>>();
            public readonly List<Image> Motes = new List<Image>();
        }

        private readonly BackdropSwitch _switch = new BackdropSwitch();
        private RectTransform _root;
        private Slot _shown;
        private Slot _leaving;

        /// <summary>The layer drawn on top (1〜7).</summary>
        public int Layer => _switch.Shown;

        /// <summary>
        /// Fills <paramref name="parent"/> with the backdrop of <paramref name="layer"/> (held to 1〜7), as its
        /// last child. It takes no clicks, so the screen's own pieces go above it.
        /// </summary>
        public static BackdropLayers Create(RectTransform parent, int layer)
        {
            RectTransform root = UiKit.Rect(parent, "BackdropLayers", Vector2.zero, Vector2.one);
            CanvasGroup group = UiKit.Group(root);
            group.blocksRaycasts = false;
            group.interactable = false;
            BackdropLayers layers = root.gameObject.AddComponent<BackdropLayers>();
            layers._root = root;
            layers.Show(layer);
            return layers;
        }

        /// <summary>Turns the backdrop to <paramref name="layer"/> (held to 1〜7), crossfading over 400 ms. The same layer again does nothing.</summary>
        public void Show(int layer)
        {
            if (!_root) return;
            BackdropSwitch.Change change = _switch.Show(layer, NowMs);
            if (change == BackdropSwitch.Change.None) return;
            if (change == BackdropSwitch.Change.FadeDroppingOldest) Destroy(_leaving.Root.gameObject);
            _leaving = _shown;
            _shown = Build(BackdropSpec.For(_switch.Shown));
            _shown.Group.alpha = change == BackdropSwitch.Change.First ? 1f : 0f;
        }

        private static double NowMs => Time.unscaledTimeAsDouble * 1000.0;

        private void Update()
        {
            if (_shown == null) return;
            double now = NowMs;
            _shown.Group.alpha = _switch.Tick(now);
            if (_leaving != null && _switch.Leaving == 0)
            {
                Destroy(_leaving.Root.gameObject);
                _leaving = null;
            }
            Animate(_shown, now);
            if (_leaving != null) Animate(_leaving, now);
        }

        private void Animate(Slot slot, double now)
        {
            foreach (KeyValuePair<Image, Haze> pair in slot.Hazes)
            {
                Rgba c = pair.Value.Color;
                pair.Key.color = new Color(c.R, c.G, c.B, c.A * BackdropMotion.FlickerAlpha(pair.Value.Flicker, now));
            }
            Motes motes = slot.Spec.Motes;
            Vector2 size = _root.rect.size;
            for (int i = 0; i < slot.Motes.Count; i++)
            {
                BackdropMotion.MoteAt(motes, i, now, size.x, size.y, out float x, out float y);
                Image mote = slot.Motes[i];
                mote.rectTransform.anchoredPosition = new Vector2(x, -y);
                Rgba c = motes.Color;
                mote.color = new Color(c.R, c.G, c.B, c.A * BackdropMotion.MoteAlpha(motes, i, now));
            }
        }

        private Slot Build(LayerBackdrop spec)
        {
            var slot = new Slot { Spec = spec };
            slot.Root = UiKit.Rect(_root, "Layer" + spec.Layer, Vector2.zero, Vector2.one);
            slot.Group = UiKit.Group(slot.Root);
            slot.Group.blocksRaycasts = false;
            Vector2 size = _root.rect.size;

            UiKit.Image(slot.Root, "Sky", SkySprite(spec), Color.white, Vector2.zero, Vector2.one);
            Sprite art = Resources.Load<Sprite>(ArtPath + spec.Layer);
            if (art) UiKit.Image(slot.Root, "Art", art, Color.white, Vector2.zero, Vector2.one);

            foreach (Haze haze in spec.Hazes)
            {
                foreach (Image image in HazeImages(slot.Root, haze, size)) slot.Hazes.Add(new KeyValuePair<Image, Haze>(image, haze));
            }

            Vector2 top = new Vector2(0f, 1f);
            Vector2 speck = new Vector2(spec.Motes.SizePx, spec.Motes.SizePx);
            for (int i = 0; i < spec.Motes.Count; i++)
            {
                slot.Motes.Add(UiKit.Sprite(slot.Root, "Mote" + i, ProceduralArt.SoftCircle, Color.clear, top, new Vector2(0.5f, 0.5f), speck, Vector2.zero));
            }
            return slot;
        }

        private static IEnumerable<Image> HazeImages(RectTransform parent, Haze haze, Vector2 size)
        {
            switch (haze.Shape)
            {
                case HazeShape.TopGlow:
                case HazeShape.Band:
                    yield return UiKit.Image(parent, haze.Shape.ToString(), VerticalAlpha(haze), Color.clear, Vector2.zero, Vector2.one);
                    break;
                case HazeShape.Radial:
                {
                    float across = haze.Size * size.y;
                    yield return UiKit.Sprite(parent, "Radial", RadialSprite, Color.clear, new Vector2(haze.X, 1f - haze.Y),
                        new Vector2(0.5f, 0.5f), new Vector2(across, across), Vector2.zero);
                    break;
                }
                case HazeShape.Shafts:
                {
                    float width = haze.Size * size.x;
                    float spacing = size.x * ShaftSpacing;
                    for (int i = 0; i < haze.Count; i++)
                    {
                        float x = haze.X * size.x + (i - (haze.Count - 1) / 2f) * spacing;
                        Image shaft = UiKit.Sprite(parent, "Shaft" + i, ShaftSprite, Color.clear, Vector2.zero, new Vector2(0.5f, 0.5f),
                            new Vector2(width, size.y * 1.6f), new Vector2(x, size.y / 2f));
                        shaft.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -haze.Angle);
                        yield return shaft;
                    }
                    break;
                }
            }
        }

        /// <summary>How far apart the shafts of one haze stand, as a share of the screen's width.</summary>
        private const float ShaftSpacing = 0.18f;
        private const int Tall = 128;

        // Made once per layer and kept: a layer seen again reuses its textures.
        private static readonly Dictionary<string, Sprite> Made = new Dictionary<string, Sprite>();

        private static Sprite Kept(string key, System.Func<Sprite> make)
        {
            if (!Made.TryGetValue(key, out Sprite sprite) || !sprite) Made[key] = sprite = make();
            return sprite;
        }

        private static Sprite SkySprite(LayerBackdrop spec)
        {
            return Kept("sky" + spec.Layer, () => MakeSprite(2, Tall, (x, y) =>
            {
                Rgba c = BackdropSpec.SkyAt(spec, 1f - y / (Tall - 1f));
                return new Color(c.R, c.G, c.B, 1f);
            }));
        }

        // The haze's shape in white; the image's colour carries its colour and its breathing alpha.
        private static Sprite VerticalAlpha(Haze haze)
        {
            var unit = new Haze { Shape = haze.Shape, Color = new Rgba(1f, 1f, 1f, 1f), Y = haze.Y, Size = haze.Size };
            string key = haze.Shape + "/" + haze.Y + "/" + haze.Size;
            return Kept(key, () => MakeSprite(2, Tall, (x, y) => new Color(1f, 1f, 1f, BackdropSpec.HazeAlphaAt(unit, 1f - y / (Tall - 1f)))));
        }

        private static Sprite _radial;
        private static Sprite RadialSprite => _radial ? _radial : _radial = MakeSprite(64, 64, (x, y) =>
        {
            float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f);
            return new Color(1f, 1f, 1f, 1f - d);
        });

        private static Sprite _shaft;
        private static Sprite ShaftSprite => _shaft ? _shaft : _shaft = MakeSprite(32, 2, (x, y) =>
        {
            float edge = Mathf.Abs(x / 31f * 2f - 1f); // 0 in the middle, 1 at the sides
            return new Color(1f, 1f, 1f, Mathf.Clamp01((1f - edge) * 2f));
        });

        private static Sprite MakeSprite(int w, int h, System.Func<int, int, Color> pixel)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++) pixels[y * w + x] = pixel(x, y);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
#endif
