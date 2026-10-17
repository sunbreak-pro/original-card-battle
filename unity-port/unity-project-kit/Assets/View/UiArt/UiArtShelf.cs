// Loads the drawn UI parts of battle-visual-v1 §11 (#92) from Resources/UiArt/, falling back to the
// code-drawn shape while a part has no art yet. Nothing on screen calls it yet: the views that draw
// icons belong to the battle lane, which switches them over (the part ids are in UiArtCatalog).
//
// Art goes in as one white shape on a clear ground, 128×128, at Resources/UiArt/icon-<id>.png
// (asset-intake.md §11 has the import settings). The caller tints it, as it tints the line icons now.
#if UNITY_2021_2_OR_NEWER
using System.Collections.Generic;
using UnityEngine;

namespace UiArt.View
{
    public static class UiArtShelf
    {
        private static readonly Dictionary<string, Sprite> Loaded = new Dictionary<string, Sprite>();

        /// <summary>The art of part <paramref name="id"/>, or <paramref name="fallback"/> while it has none (or the id is unknown).</summary>
        public static Sprite Icon(string id, Sprite fallback)
        {
            Sprite art = Art(id);
            return art ? art : fallback;
        }

        /// <summary>Whether part <paramref name="id"/> has its art in.</summary>
        public static bool HasArt(string id)
        {
            return Art(id) != null;
        }

        private static Sprite Art(string id)
        {
            if (Loaded.TryGetValue(id, out Sprite cached)) return cached;
            UiArtPart part = UiArtCatalog.Find(id);
            Sprite art = part != null ? Resources.Load<Sprite>(part.ResourcePath) : null;
            Loaded[id] = art; // a miss is kept too, so a part with no art is looked up once
            return art;
        }
    }
}
#endif
