// battle_ui_ux_v2 §5.3 (#57): which of the eight systems each card and each enemy action plays as.
// Pure C#: BattleCore + Depiction.Script, no UnityEngine. The rule is the grammar's
// (EffectGrammar.Of); this file only reads the core's faces into it and keeps the table by card id,
// so the writer puts on the script what the table says and the View never works a system out.
using System.Collections.Generic;
using BattleCore;

namespace Depiction.Bridge
{
    public static class CardSystems
    {
        /// <summary>
        /// Every card the core knows (CardCatalog.All), by its id: the systems it plays, in the order
        /// its faces resolve (§5.4). A movement-only card maps to an empty list (§5.3: a move has none).
        /// </summary>
        public static readonly IReadOnlyDictionary<string, IReadOnlyList<FxSystem>> Table = Build();

        /// <summary>
        /// The systems of a card. A card the table holds plays its row, so a mastered card that keeps
        /// its id under a new name keeps its look; any other card is read off its own faces.
        /// </summary>
        public static IReadOnlyList<FxSystem> Of(CardDef def)
        {
            if (def == null) return new List<FxSystem>();
            IReadOnlyList<FxSystem> row;
            return Table.TryGetValue(def.Id, out row) ? row : EffectGrammar.Of(FacesOf(def.Name, def.Attributes, def.Face));
        }

        /// <summary>§5.3: an enemy's action is read by the same rule as a card.</summary>
        public static IReadOnlyList<FxSystem> Of(EnemyActionDef action)
        {
            if (action == null) return new List<FxSystem>();
            return EffectGrammar.Of(FacesOf(action.Name, action.Attributes, action.Face));
        }

        /// <summary>The faces as the grammar reads them: what the definition declares, and what its face carries.</summary>
        public static FxFaces FacesOf(string name, BattleAttribute attributes, Face face)
        {
            return new FxFaces
            {
                Name = name ?? "",
                Attacks = attributes.HasFlag(BattleAttribute.Attack) || face.Power > 0,
                Pushes = face.Push != 0,
                Guards = attributes.HasFlag(BattleAttribute.Guard) || face.Guard > 0,
                PutsStatus = face.StatusList.Count > 0,
                Breathes = face.Heal > 0 || face.Draw > 0 || face.StaminaGain > 0,
                Stance = attributes.HasFlag(BattleAttribute.Stance) || face.Stance != null,
            };
        }

        private static Dictionary<string, IReadOnlyList<FxSystem>> Build()
        {
            var table = new Dictionary<string, IReadOnlyList<FxSystem>>();
            foreach (CardDef def in CardCatalog.All)
            {
                table[def.Id] = EffectGrammar.Of(FacesOf(def.Name, def.Attributes, def.Face)).AsReadOnly();
            }
            return table;
        }
    }
}
