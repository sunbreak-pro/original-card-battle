// Tools > Depiction > Collect Character Art (#288)
// Fills the Figure prefab's art shelf from Assets/Art/Characters/<id>/, named as asset-intake.md §4
// and CharacterArt say. A folder becomes a shelf entry once its idle picture is there; the act, hit
// and down pictures are optional. Re-run it after art is added or renamed: the shelf is rebuilt from
// the folders each time, so it never keeps a picture that is gone.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Depiction.View
{
    public static class FigureArtCollector
    {
        [MenuItem("Tools/Depiction/Collect Character Art")]
        public static void CollectIntoFigurePrefab()
        {
            string path = DepictionPrefabBuilder.PrefabFolder + "/Figure.prefab";
            if (!File.Exists(path))
            {
                Debug.LogWarning("[Depiction] " + path + " is missing; run Build Prefabs And Scene first.");
                return;
            }
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<FigureView>();
                if (view == null)
                {
                    Debug.LogWarning("[Depiction] " + path + " has no FigureView on its root; no art collected.");
                    return;
                }
                view.artShelf = Collect();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                var ids = new List<string>();
                foreach (FigureArt art in view.artShelf) ids.Add(art.id);
                Debug.Log("[Depiction] character art on the Figure prefab: " + (ids.Count == 0 ? "none" : string.Join(", ", ids)) + ".");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>One entry per folder under <see cref="CharacterArt.Root"/> that holds its idle picture.</summary>
        public static FigureArt[] Collect()
        {
            var shelf = new List<FigureArt>();
            if (!AssetDatabase.IsValidFolder(CharacterArt.Root)) return shelf.ToArray();
            foreach (string folder in AssetDatabase.GetSubFolders(CharacterArt.Root))
            {
                string id = Path.GetFileName(folder);
                Sprite idle = AssetDatabase.LoadAssetAtPath<Sprite>(CharacterArt.PathOf(id, FigurePose.Idle));
                if (idle == null)
                {
                    Debug.LogWarning("[Depiction] " + folder + " has no " + CharacterArt.FileName(id, FigurePose.Idle)
                        + " imported as a Sprite; the figure keeps its silhouette for \"" + id + "\".");
                    continue;
                }
                shelf.Add(new FigureArt
                {
                    id = id,
                    idle = idle,
                    act = FirstAct(folder, id),
                    hit = AssetDatabase.LoadAssetAtPath<Sprite>(CharacterArt.PathOf(id, FigurePose.Hit)),
                    down = AssetDatabase.LoadAssetAtPath<Sprite>(CharacterArt.PathOf(id, FigurePose.Down)),
                });
            }
            return shelf.ToArray();
        }

        /// <summary>The act picture: the first chr_&lt;id&gt;_act_*.png by name (one stands for every action for now).</summary>
        private static Sprite FirstAct(string folder, string id)
        {
            string prefix = CharacterArt.FileName(id, FigurePose.Act);
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { folder }))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetDirectoryName(assetPath).Replace('\\', '/') != folder) continue;
                if (Path.GetFileName(assetPath).StartsWith(prefix, System.StringComparison.Ordinal)) paths.Add(assetPath);
            }
            if (paths.Count == 0) return null;
            paths.Sort(System.StringComparer.Ordinal);
            return AssetDatabase.LoadAssetAtPath<Sprite>(paths[0]);
        }
    }
}
