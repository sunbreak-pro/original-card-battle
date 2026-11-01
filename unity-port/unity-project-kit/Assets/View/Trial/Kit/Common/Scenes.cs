// The scene contract every trial screen keeps (docs/ARCH.md of the JS: `mountX(host, ctx) → { destroy(), refresh?() }`).
// A scene is mounted into a 1920×1080 host under the stage by the router (Trial.Boot), which destroys it when it
// leaves; scenes never navigate themselves, they call back through their ctx.
namespace Trial.Kit
{
    /// <summary>A mounted screen: the router calls Destroy when it leaves, Refresh after an overlay over it closes.</summary>
    public interface IScene
    {
        void Destroy();

        /// <summary>JS refresh?(): may do nothing.</summary>
        void Refresh();
    }

    /// <summary>A screen that opens over another one (settings, owned cards, the journal): it can also close itself.</summary>
    public interface IOverlay : IScene
    {
        void Close();
    }
}
