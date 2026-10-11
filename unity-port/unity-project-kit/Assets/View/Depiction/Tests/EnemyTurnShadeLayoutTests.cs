// #455: battle-visual-v1 §5.5's shade on the enemy's turn (Script/EnemyTurnShadeLayout.cs). Runs under
// `dotnet test` (Depiction.Script.Tests.csproj) and in Unity's EditMode runner from the same file.
using NUnit.Framework;

namespace Depiction.Tests
{
    public class EnemyTurnShadeLayoutTests
    {
        [Test]
        public void TheShade_RunsFromY790ToTheBottom_ClearToBlack72()
        {
            Assert.That(EnemyTurnShadeLayout.Top, Is.EqualTo(790f), "the throw line's height (§5.3)");
            Assert.That(EnemyTurnShadeLayout.Height, Is.EqualTo(290f));
            Assert.That(EnemyTurnShadeLayout.TopAlpha, Is.EqualTo(0f));
            Assert.That(EnemyTurnShadeLayout.BottomAlpha, Is.EqualTo(0.72f));
        }

        [Test]
        public void TheEndTurnPlate_StandsAt40Percent()
        {
            Assert.That(EnemyTurnShadeLayout.EndTurnAlpha, Is.EqualTo(0.4f), "§4.9 敵の番は不透明度 40%");
        }
    }
}
