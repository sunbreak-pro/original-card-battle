// Tools > Trial > Collect Character Art (#772)
// Rebuilds the trial battle's art shelf (Trial.Battle.FigureArtShelf, under Resources) from Assets/Art/Characters/<id>/,
// with the same folders and names the old Figure prefab's collector reads (Depiction.View.FigureArtCollector). It runs by
// itself after a picture under Assets/Art/Characters is imported, moved or deleted, so a new picture reaches the trial
// without anyone remembering the menu.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Depiction;
using Depiction.View;
using Trial.Battle;
using UnityEditor;
using UnityEngine;

namespace Trial.Tools
{
    public static class FigureArtShelfBuilder
    {
        private const string ResourcesFolder = "Assets/View/Resources";
        private const string ShelfFolder = ResourcesFolder + "/Trial";
        public const string ShelfPath = ShelfFolder + "/FigureArtShelf.asset";

        [MenuItem("Tools/Trial/Collect Character Art")]
        public static void CollectFromMenu() => Rebuild(true);

        /// <summary>The shelf rebuilt from the folders; written only when it changed (or when `log` asks to report anyway).</summary>
        public static FigureArtShelf Rebuild(bool log)
        {
            FigureArt[] found = FigureArtCollector.Collect();
            var entries = new FigureArtShelf.Entry[found.Length];
            for (int i = 0; i < found.Length; i++)
            {
                FigureArt a = found[i];
                entries[i] = new FigureArtShelf.Entry { id = a.id, idle = a.idle, act = a.act, hit = a.hit, down = a.down };
            }

            EnsureFolder("Assets/View", "Resources");
            EnsureFolder(ResourcesFolder, "Trial");
            var shelf = AssetDatabase.LoadAssetAtPath<FigureArtShelf>(ShelfPath);
            bool created = shelf == null;
            bool changed = created || !Same(shelf.entries, entries);
            if (created)
            {
                shelf = ScriptableObject.CreateInstance<FigureArtShelf>();
                shelf.entries = entries;
                AssetDatabase.CreateAsset(shelf, ShelfPath);
            }
            else if (changed)
            {
                shelf.entries = entries;
                EditorUtility.SetDirty(shelf);
            }
            if (changed) AssetDatabase.SaveAssets();
            if (changed || log)
            {
                var ids = new List<string>();
                foreach (FigureArtShelf.Entry e in entries) ids.Add(e.id);
                Debug.Log("[Trial] character art on " + ShelfPath + ": " + (ids.Count == 0 ? "none" : string.Join(", ", ids)) + ".");
            }
            return shelf;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        private static bool Same(FigureArtShelf.Entry[] a, FigureArtShelf.Entry[] b)
        {
            if (a == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                FigureArtShelf.Entry x = a[i], y = b[i];
                if (x == null || x.id != y.id || x.idle != y.idle || x.act != y.act || x.hit != y.hit || x.down != y.down) return false;
            }
            return true;
        }
    }

    /// <summary>Rebuilds the shelf once after any batch of imports, moves or deletions that touches a picture under the art root.</summary>
    internal sealed class FigureArtShelfPostprocessor : AssetPostprocessor
    {
        private static bool _queued;

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (_queued) return;
            if (!Touches(imported) && !Touches(deleted) && !Touches(moved) && !Touches(movedFrom)) return;
            _queued = true;
            // After the import: building assets inside the postprocess would import again in the middle of the batch.
            EditorApplication.delayCall += () =>
            {
                _queued = false;
                FigureArtShelfBuilder.Rebuild(false);
            };
        }

        private static bool Touches(string[] paths)
        {
            if (paths == null) return false;
            string root = CharacterArt.Root + "/";
            foreach (string p in paths)
                if (p != null && p.StartsWith(root, StringComparison.Ordinal) && p.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
#endif
