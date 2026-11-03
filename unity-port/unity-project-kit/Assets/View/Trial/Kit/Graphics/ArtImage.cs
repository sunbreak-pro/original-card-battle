// A painted picture from Resources, laid as a RawImage (#858: the layers' backdrop art behind the trial screens).
//
// Under the gamma target a colour texture must be sampled without the sRGB decode (Materials.ColorTexturesLinear), but an
// imported picture is sRGB unless its import says otherwise, and the decode would darken it. Such a picture is shown through
// a linear twin holding the same bytes (Graphics.CopyTexture: GammaTarget's present relies on the same UNorm/sRGB copy).
// Without the gamma target, or for a picture imported linear, the texture is shown as imported. Where the copy cannot be
// made the picture is shown as imported, darker, with one warning, rather than not at all.
//
// Lookups are kept, misses too, so a screen that asks every build costs one Resources.Load per path; the cache is cleared
// on entering play mode (domain reload may be off), so a picture dropped in between runs is found.
using System;
using System.Collections.Generic;
using Trial.Geometry;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Trial.Kit
{
    public static class ArtImage
    {
        private static readonly Dictionary<string, Sprite> Loaded = new Dictionary<string, Sprite>();
        private static readonly Dictionary<Texture2D, Texture2D> Twins = new Dictionary<Texture2D, Texture2D>();
        private static bool _warned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Loaded.Clear();
            foreach (Texture2D twin in Twins.Values) Kill(twin);
            Twins.Clear();
            _warned = false;
        }

        /// <summary>The Sprite at Resources/<paramref name="resourcePath"/>, or null when there is none (or the path is null).</summary>
        public static Sprite Load(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return null;
            if (Loaded.TryGetValue(resourcePath, out Sprite kept))
            {
                if (ReferenceEquals(kept, null)) return null;   // a kept miss
                if (kept) return kept;                          // else unloaded since: look again
            }
            Sprite art = Resources.Load<Sprite>(resourcePath);
            Loaded[resourcePath] = art ? art : null;
            return art ? art : null;
        }

        /// <summary>
        /// Lays <paramref name="art"/> over x, y, w × h of <paramref name="parent"/> (stage px, top-left), showing the part of
        /// it given in fractions of the sprite with v from its bottom (cropX, cropY, cropW, cropH; 0, 0, 1, 1 = all of it).
        /// It takes no pointer events. Null when there is nothing to lay.
        /// </summary>
        public static RawImage Lay(RectTransform parent, Sprite art, float x, float y, float w, float h,
            float cropX, float cropY, float cropW, float cropH, string name = "art")
        {
            if (!parent || !art || !art.texture) return null;
            Texture2D source = art.texture;
            RectTransform rt = Ui.Div(parent, name, x, y, w, h);
            var img = rt.gameObject.AddComponent<RawImage>();
            img.raycastTarget = false;
            img.texture = Shown(source);
            img.color = Materials.VertexColor(Rgba.White);
            Rect r = SpriteRect(art);
            float tw = source.width, th = source.height;
            img.uvRect = new Rect((r.x + cropX * r.width) / tw, (r.y + cropY * r.height) / th, cropW * r.width / tw, cropH * r.height / th);
            return img;
        }

        // A sprite packed tightly into an atlas has no textureRect; its rect is then the best there is.
        private static Rect SpriteRect(Sprite art)
        {
            try { return art.textureRect; }
            catch (Exception) { return art.rect; }
        }

        private static Texture Shown(Texture2D source)
        {
            if (!Materials.ColorTexturesLinear || !GraphicsFormatUtility.IsSRGBFormat(source.graphicsFormat)) return source;
            if (Twins.TryGetValue(source, out Texture2D kept) && kept) return kept;
            Texture2D twin = LinearTwin(source);
            if (!twin) return source;
            Twins[source] = twin;
            return twin;
        }

        private static Texture2D LinearTwin(Texture2D source)
        {
            if (SystemInfo.copyTextureSupport == CopyTextureSupport.None)
            {
                Warn(source, "this device cannot copy textures");
                return null;
            }
            Texture2D twin = null;
            try
            {
                twin = new Texture2D(source.width, source.height, GraphicsFormatUtility.GetLinearFormat(source.graphicsFormat),
                    source.mipmapCount, TextureCreationFlags.DontInitializePixels)
                {
                    name = source.name + " (linear)",
                    filterMode = source.filterMode,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave,
                };
                // Released from the CPU before the copy: an Apply after it would upload the empty CPU pixels over the copy.
                twin.Apply(false, true);
                Graphics.CopyTexture(source, twin);
                return twin;
            }
            catch (Exception e)
            {
                Kill(twin);
                Warn(source, e.Message);
                return null;
            }
        }

        private static void Warn(Texture2D source, string why)
        {
            if (_warned) return;
            _warned = true;
            Debug.LogWarning("[ArtImage] " + source.name + " is shown with its sRGB decode, darker than painted (" + why + "). Import it with sRGB off to show it as painted.");
        }

        private static void Kill(UnityEngine.Object o)
        {
            if (!o) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
