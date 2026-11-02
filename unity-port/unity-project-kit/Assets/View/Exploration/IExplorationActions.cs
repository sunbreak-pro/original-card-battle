// What the exploration screen's presses go to (#729). ExplorationScreenView takes this one port instead
// of one Action per button, so a new button adds a method here, not a constructor argument.
// ExplorationBootstrap implements it and hands each press to ExplorationSession or Exploration.Bridge.
#if UNITY_2021_2_OR_NEWER
using DungeonCore;

namespace Exploration.View
{
    public interface IExplorationActions
    {
        /// <summary>Walk to a node with a choice (the choice only matters on a 休息 node).</summary>
        void Step(int nodeId, RestChoice choice);

        /// <summary>A card button or 「この生を終える」.</summary>
        void Act(ScreenAction action);

        /// <summary>Burn the consumable in a slot.</summary>
        void UseConsumable(int index);

        /// <summary>A band button or a close button: open or close that screen.</summary>
        void Toggle(Overlay overlay);

        /// <summary>A card pressed on the pick screen (#626), by its id.</summary>
        void PickCard(string cardId);

        /// <summary>A choice pressed on the choosing screen at a trace (#717), by its key.</summary>
        void ToggleLegacy(string key);

        /// <summary>The choosing screen's confirm button.</summary>
        void ConfirmLegacy();

        /// <summary>A slot pressed on the swap screen (#719), by its index.</summary>
        void SwapFound(int slot);

        /// <summary>The swap screen's 「諦める」.</summary>
        void DiscardFound();
    }
}
#endif
