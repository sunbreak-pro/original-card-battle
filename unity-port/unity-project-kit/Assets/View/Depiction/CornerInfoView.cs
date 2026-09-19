// Top-left corner tag replacing the old top band (battle_ui_ux_v2 §10.2): turn, floor,
// chain dots, miasma %. One small line; deadline, whose-turn and enemy name are not shown.
#if UNITY_2021_2_OR_NEWER
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    public class CornerInfoView : MonoBehaviour
    {
        public Text turnText;
        public Text floorText;
        public Text chainText;
        public Text miasmaText;

        public void Bind(CornerFrame corner)
        {
            if (turnText) turnText.text = corner.Turn.ToString();
            if (floorText) floorText.text = corner.Floor.ToString();
            if (miasmaText) miasmaText.text = corner.MiasmaPercent + "%";
            if (chainText)
            {
                var dots = new StringBuilder();
                for (int i = 1; i <= corner.ChainTotal; i++) dots.Append(i <= corner.ChainIndex ? '●' : '○');
                chainText.text = dots.ToString();
            }
        }
    }
}
#endif
