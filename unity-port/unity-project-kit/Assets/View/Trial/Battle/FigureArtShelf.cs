// The standing pictures the trial battle can put on its figures (#772).
//
// The pictures stay where asset-intake.md §4 puts them (Assets/Art/Characters/<id>/), outside Resources, so the trial
// cannot load them by path. This shelf, kept under Resources, refers to them instead; the Editor rebuilds it from the
// folders whenever a picture there is imported, moved or deleted (Trial/Editor/FigureArtShelfBuilder.cs). A missing
// shelf or a missing entry leaves the figure in its silhouette.
using System;
using UnityEngine;

namespace Trial.Battle
{
    public sealed class FigureArtShelf : ScriptableObject
    {
        /// <summary>Resources/Trial/FigureArtShelf.asset (under Assets/View/Resources).</summary>
        public const string ResourcePath = "Trial/FigureArtShelf";

        [Serializable]
        public sealed class Entry
        {
            [Tooltip("The art id the script names, as the folder under Assets/Art/Characters is named.")]
            public string id = "";
            public Sprite idle;
            public Sprite act;
            public Sprite hit;
            public Sprite down;
        }

        public Entry[] entries = Array.Empty<Entry>();

        private static FigureArtShelf _loaded;
        private static bool _looked;

        // Domain reload may be off: a shelf built between runs is looked up again on the next play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _loaded = null;
            _looked = false;
        }

        /// <summary>The shelf under Resources, loaded once per play; null when there is none.</summary>
        public static FigureArtShelf Loaded
        {
            get
            {
                if (!_looked)
                {
                    _looked = true;
                    _loaded = Resources.Load<FigureArtShelf>(ResourcePath);
                }
                return _loaded ? _loaded : null;
            }
        }

        /// <summary>The idle picture of `id` on the loaded shelf; null when there is no shelf, no entry or no picture.</summary>
        public static Sprite IdleOf(string id)
        {
            FigureArtShelf shelf = Loaded;
            return shelf ? shelf.Idle(id) : null;
        }

        public Sprite Idle(string id)
        {
            if (string.IsNullOrEmpty(id) || entries == null) return null;
            foreach (Entry e in entries)
                if (e != null && e.id == id) return e.idle ? e.idle : null;
            return null;
        }
    }
}
