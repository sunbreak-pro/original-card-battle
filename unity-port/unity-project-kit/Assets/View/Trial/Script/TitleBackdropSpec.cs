// The place behind the title (scenes/meta/title.js:19 layerBackdrop(7)): the heir stands at the edge of the deepest layer,
// 果てなき世界, so the title shows that layer's backdrop and, once it is painted, that layer's art (#858).
using Backdrop;

namespace Trial.Script
{
    public static class TitleBackdropSpec
    {
        /// <summary>The layer behind the title: the deepest (BackdropSpec.Layers).</summary>
        // js: scenes/meta/title.js:19
        public const int Layer = BackdropSpec.Layers;

        /// <summary>The title's backdrop: BackdropSpec.For(Layer).</summary>
        public static LayerBackdrop For() => BackdropSpec.For(Layer);
    }
}
