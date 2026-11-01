// dotnet-only: the module contracts (BattleContracts.cs) hold every member docs/BATTLE_SCENE.md §6a-§6c lists and every
// one player.js / BattleScene.js calls on hand / field / hud (the JS names below were read off those files), in the
// port's PascalCase. A module implementer codes against these interfaces, so a missing member is a contract hole.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Trial.Battle.Flow.Tests
{
    public class BattleEnvContractsTests
    {
        private static readonly string[] HandJs =
        {
            // §6a + hand.js api (846-1202)
            "sameHand", "build", "bind", "layout", "clear", "count", "faces", "setInteractable", "refreshPlayable",
            "setStatusMarks", "isHolding", "heldFace", "hoveredFace", "debugState", "hasSelection", "detailOpen", "framedUnit",
            "flyDrawn", "discardAll", "release", "ghostCard", "dropCard", "settle", "returnToHand", "letGo", "setBlocked",
            "setGrabLock", "taggedUnits", "showRefusal", "hideRefusal", "select", "confirm", "selectedIndex", "cycleTarget",
            "toggleSelectedDetail", "fadeAll", "cancel", "autoDrag", "destroy",
            // the listeners the scene routes (hand.js:604-747)
            "onPointerDown", "onPointerMove", "onPointerUp", "onPointerCancel", "onContext", "onLeave", "onBlur",
        };

        private static readonly string[] FieldJs =
        {
            // §6b + field.js api (509-995)
            "applyFrame", "showGapTo", "enemyUnits", "setExtras", "drawAim", "showReach", "retireAim", "retireAimOf", "focusAim",
            "hideReach", "setShift", "shiftX", "nudgeOf", "setLowHp", "pulseLowHp", "appearEnemies", "setEnemyTurnShade",
            "setPose", "setDown", "enemyAway", "setEnemyHomeCell", "snapHome", "stanceGlowShown", "setStanceGlow",
            "fadeStanceGlow", "lunge", "returnHome", "enemyMotion", "recoil", "flash", "heave", "fall", "fadeFoot", "dimFeet",
            "bump", "shakeArena", "moveOnFloor", "snapOnFloor", "showFreeStep", "focusFreeStep", "hideFreeStep",
            "freeStepShown", "freeStepFocus", "freeStepDirs", "freeStepCentre", "recountGap", "setGap", "strike", "wallHit",
            "traitAccent", "mark", "burst", "floatText", "shieldBlock", "shieldAt", "ghost", "destroy",
        };

        private static readonly string[] HudJs =
        {
            // §6c + hud.js api (547-649)
            "applyFrame", "follow", "status", "omen", "syncOmenLook", "omenOf", "enemyUnits", "stamina", "reserveHint",
            "setEnemyTurn", "endTurn", "banner", "resultCard", "resultCardNow", "chainBanner", "lossWordOut", "settleEnd",
            "settleEndNow", "retireOmens", "liftOmen", "setRestFocus", "setLog", "flyOmenToStances", "lossInk", "speed",
            "setSpeedVisible", "closePinned", "drawerOpen", "closeDrawer", "destroy",
            // the listeners the scene routes (hud.js:440-460)
            "onPinDown", "onPinContext", "onPointerMove",
        };

        private static readonly string[] StatusJs =
        {
            // hud/status.js api, the members the player and the hand read
            "hp", "shown", "box", "setGuard", "popGuard", "crackGuard", "setHp", "animateHp", "animateTrail", "snapTrail",
            "setHpKeepingTrail", "showHpLoss", "setStatus", "playStatus", "setStamina", "lightPips", "popPips", "greyOut",
            "setDown", "setFocus",
        };

        private static readonly string[] BadgeJs =
        {
            // hud/omen.js api (154-339) but the hud's own place / show / destroy
            "bind", "setSwatchShown", "setTail", "setBehind", "setMoveDir", "promotePlan", "popIn", "blink", "flare", "reveal",
            "traitWord", "setActing", "fadeOut", "hideNow", "fadeOutAll", "hideAllNow", "strikeSide", "strikeSideNow", "box",
            "visible",
        };

        private static readonly string[] OmenFacadeJs =
        {
            // hud.js:516-536
            "bind", "popIn", "blink", "flare", "setActing", "fadeOut", "hideNow", "fadeOutAll", "hideAllNow", "strikeSide",
            "strikeSideNow",
        };

        private static readonly string[] GeomJs =
        {
            // docs/BATTLE_SCENE.md §2.4 and every env.geom.x = … of field.js / hud.js / hand.js / BattleScene.js
            "figure", "receiver", "receivers", "freeStepBoxes", "cellBox", "pileMiddle", "omenBox", "omenShown", "omenBoxes",
            "endTurnBox", "statusBox", "statusBoxes", "stanceBox", "stanceSlot", "coversPointer", "drawerRight", "handCard",
            "journalCovers", "journalEdge",
        };

        private static string Pascal(string camel) => char.ToUpperInvariant(camel[0]) + camel.Substring(1);

        private static HashSet<string> MembersOf(Type t) =>
            new HashSet<string>(t.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m is MethodInfo mi ? !mi.IsSpecialName : m is PropertyInfo)
                .Select(m => m.Name));

        private static void AssertHolds(Type contract, IEnumerable<string> js)
        {
            HashSet<string> have = MembersOf(contract);
            var missing = js.Select(Pascal).Where(n => !have.Contains(n)).ToList();
            Assert.That(missing, Is.Empty, contract.Name + " misses " + string.Join(", ", missing));
        }

        [Test]
        public void IHand_HoldsEveryMemberOfTheJsHand() => AssertHolds(typeof(IHand), HandJs);

        [Test]
        public void IField_HoldsEveryMemberOfTheJsField() => AssertHolds(typeof(IField), FieldJs);

        [Test]
        public void IHud_HoldsEveryMemberOfTheJsHud() => AssertHolds(typeof(IHud), HudJs);

        [Test]
        public void IStatusPanel_HoldsWhatThePlayerReads() => AssertHolds(typeof(IStatusPanel), StatusJs);

        [Test]
        public void IOmenBadge_HoldsEveryMemberOfTheJsBadge() => AssertHolds(typeof(IOmenBadge), BadgeJs);

        [Test]
        public void IOmenFacade_HoldsEveryMemberOfHudOmen() => AssertHolds(typeof(IOmenFacade), OmenFacadeJs);

        [Test]
        public void BattleGeom_HoldsEveryGetterOfTheRegistry() => AssertHolds(typeof(BattleGeom), GeomJs);

        [Test]
        public void EveryBeat_ReturnsATrialPromise()
        {
            // A JS method that returns a Promise returns Trial.Promise (or Promise<T>), never a Task.
            foreach (Type t in new[] { typeof(IHand), typeof(IField), typeof(IHud), typeof(IStatusPanel), typeof(IOmenBadge), typeof(IOmenFacade), typeof(IHudStamina), typeof(IHudEndTurn) })
                foreach (MethodInfo m in t.GetMethods())
                    Assert.That(m.ReturnType.FullName, Does.Not.StartWith("System.Threading.Tasks"), t.Name + "." + m.Name);
        }

        [Test]
        public void TheUnitArguments_DefaultToMinusOne_TheJsUndefined()
        {
            foreach (Type t in new[] { typeof(IHand), typeof(IField), typeof(IHud) })
                foreach (MethodInfo m in t.GetMethods())
                    foreach (ParameterInfo p in m.GetParameters())
                        if (p.Name == "unit" && p.HasDefaultValue)
                            Assert.That(p.DefaultValue, Is.EqualTo(-1), t.Name + "." + m.Name);
        }
    }
}
