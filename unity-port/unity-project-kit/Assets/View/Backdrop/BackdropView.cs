// Draws the layer's backdrop behind the battle (battle-visual-v1 §2.4, #89). The drawing itself is
// BackdropLayers (#607), which any screen can use; this file only finds the battle, puts the layers in
// place, and keeps them on the battle's layer.
//
// No scene or prefab change is needed: after a scene loads, a DepictionPlayer with a backdrop image gets
// one BackdropView, which builds its layers just above that image, behind everything else on the canvas.
// The layer follows the number the corner info shows (CornerFrame.Floor), so a battle on layer 4 turns
// the backdrop to layer 4 with the 400 ms crossfade of battle_ui_ux_v2 §4.3, with no change on the
// battle's side. Show(layer) does the same for a caller that knows the layer itself.
//
// BackdropView is the battle's alone: Attach does nothing while one is in the scene, and the battle scene
// loads over the hidden exploration scene (#99), so the exploration screen uses BackdropLayers instead.
#if UNITY_2021_2_OR_NEWER
using Depiction.View;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Backdrop.View
{
    public class BackdropView : MonoBehaviour
    {
        [Tooltip("The battle whose backdrop this draws. Left empty, the one in the scene is found.")]
        public DepictionPlayer player;
        [Tooltip("The layer drawn before the battle shows one (1〜7).")]
        [Range(1, BackdropSpec.Layers)]
        public int layer = 1;
        [Tooltip("Follow the layer the corner info shows. Off, only Show(layer) changes it.")]
        public bool followCornerInfo = true;

        /// <summary>Where the art of layer n is looked for under Resources (n = 1〜7).</summary>
        public const string ArtPath = BackdropLayers.ArtPath;

        private BackdropLayers _layers;

        // Called by Unity after every scene load: one BackdropView per battle, made on the spot.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachOnLoad()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded; // domain reload may be off: never hook twice
            SceneManager.sceneLoaded += OnSceneLoaded;
            Attach();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Attach();
        }

        private static void Attach()
        {
            DepictionPlayer battle = FindAnyObjectByType<DepictionPlayer>();
            if (!battle || !battle.backdrop || FindAnyObjectByType<BackdropView>()) return;
            var go = new GameObject("BackdropView");
            go.AddComponent<BackdropView>().player = battle;
        }

        /// <summary>Turns the backdrop to <paramref name="layer"/> (held to 1〜7), crossfading over 400 ms. The same layer again does nothing.</summary>
        public void Show(int layer)
        {
            this.layer = BackdropSpec.Clamp(layer);
            if (_layers) _layers.Show(this.layer);
        }

        private void Start()
        {
            if (!player) player = FindAnyObjectByType<DepictionPlayer>();
            if (!player || !player.backdrop)
            {
                enabled = false;
                return;
            }
            Transform under = player.backdrop.transform;
            _layers = BackdropLayers.Create((RectTransform)under.parent, layer);
            _layers.transform.SetSiblingIndex(under.GetSiblingIndex() + 1); // just above the plain backdrop, behind the rest
        }

        private void Update()
        {
            if (_layers && followCornerInfo) Show(ShownLayer());
        }

        /// <summary>The layer the corner info shows now, or the current one while it shows none.</summary>
        private int ShownLayer()
        {
            CornerInfoView corner = player ? player.cornerInfo : null;
            if (corner && corner.floorText && int.TryParse(corner.floorText.text, out int shown) && shown > 0) return shown;
            return layer;
        }
    }
}
#endif
