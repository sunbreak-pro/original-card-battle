// The effect grammar of battle_ui_ux_v2 §5.3 (#57): eight systems, one look each. Pure C#, like the
// rest of Script/: no UnityEngine and no BattleCore. The Bridge reads a card's or an action's faces,
// asks this file for its systems and writes them on the script; the View draws the look of the
// system it is handed and never works one out.
//
// The strength stays one constant for now (#30, 2026-10-03): every writer puts EffectStrength.Uniform
// on the cues, so a look differs by system and never by the number a face settled at.
using System;
using System.Collections.Generic;

namespace Depiction
{
    /// <summary>
    /// battle_ui_ux_v2 §5.3 系統: the eight families every card face and enemy action plays as. A
    /// movement has no system of its own (§5.3: the figure's step and the gap's number show it).
    /// </summary>
    public enum FxSystem
    {
        /// <summary>斬: an arc of light across the chest. An attack whose name holds no other word.</summary>
        Slash,

        /// <summary>突: one straight flash and a point of impact.</summary>
        Thrust,

        /// <summary>払: a wide band that sweeps across.</summary>
        Sweep,

        /// <summary>打: a ripple where it lands. Pushes and pulls play as this.</summary>
        Strike,

        /// <summary>盾: a ring stands up in front of the figure. A guard face, a Guard +n.</summary>
        Shield,

        /// <summary>型: a crest at the feet, its light staying a little. A stance face.</summary>
        Form,

        /// <summary>気: a mark falls from above onto the chest. A status put on.</summary>
        Spirit,

        /// <summary>息: a glow at the chest. A heal, a draw, a stamina point.</summary>
        Breath,
    }

    /// <summary>The one shape each system draws (§5.3 形). One system, one form: the same system looks the same.</summary>
    public enum FxForm
    {
        Arc,
        Line,
        Band,
        Ripple,
        Ring,
        Crest,
        Sigil,
        Glow,
    }

    /// <summary>
    /// The faces of one card or one enemy action, as far as the grammar reads them. The Bridge fills it
    /// from the core's definition; nothing here knows the core's types.
    /// </summary>
    public sealed class FxFaces
    {
        /// <summary>The technique's name: the attack's system is read off its words.</summary>
        public string Name = "";
        /// <summary>It has an attack face.</summary>
        public bool Attacks;
        /// <summary>The attack pushes or pulls the opponent (§5.3: 打 before the name's words).</summary>
        public bool Pushes;
        /// <summary>It has a guard face (or a Guard on its face).</summary>
        public bool Guards;
        /// <summary>Its skill face puts a status on someone (気).</summary>
        public bool PutsStatus;
        /// <summary>Its skill face heals, draws or gives stamina (息), when it puts no status.</summary>
        public bool Breathes;
        /// <summary>It has a stance face (型).</summary>
        public bool Stance;
    }

    public static class EffectGrammar
    {
        /// <summary>Every system, in the order of §5.3's table.</summary>
        public static readonly FxSystem[] Systems =
        {
            FxSystem.Slash, FxSystem.Thrust, FxSystem.Sweep, FxSystem.Strike,
            FxSystem.Shield, FxSystem.Form, FxSystem.Spirit, FxSystem.Breath,
        };

        // §5.3: the attack's words, read in the order 突 → 打 → 払 → 斬. 斬 needs no word: it is the default.
        private static readonly string[] ThrustWords = { "突", "貫", "矢", "弩", "撃", "投" };
        private static readonly string[] StrikeWords = { "打", "当", "礫", "圧殺", "押し込み" };
        private static readonly string[] SweepWords = { "薙", "払", "牽制" };

        /// <summary>
        /// §5.3 / §5.4: the systems one card or action plays, in the order it resolves its faces —
        /// attack, (move, which has none), guard, skill, stance. A movement-only card has none.
        /// </summary>
        public static List<FxSystem> Of(FxFaces faces)
        {
            if (faces == null) throw new ArgumentNullException(nameof(faces));
            var systems = new List<FxSystem>(2);
            if (faces.Attacks) systems.Add(BlowOf(faces.Name, faces.Pushes));
            if (faces.Guards) systems.Add(FxSystem.Shield);
            // §5.3: a skill face is 気 when the play puts a status on, 息 when it only heals or draws.
            if (faces.PutsStatus) systems.Add(FxSystem.Spirit);
            else if (faces.Breathes) systems.Add(FxSystem.Breath);
            if (faces.Stance) systems.Add(FxSystem.Form);
            return systems;
        }

        /// <summary>
        /// §5.3: the system of an attack face. A push or pull is what the blow is for, so it is 打
        /// before any word is read; then the name's words in the order 突 → 打 → 払, and 斬 when none.
        /// </summary>
        public static FxSystem BlowOf(string name, bool pushes)
        {
            if (pushes) return FxSystem.Strike;
            name = name ?? "";
            if (HasAny(name, ThrustWords)) return FxSystem.Thrust;
            if (HasAny(name, StrikeWords)) return FxSystem.Strike;
            if (HasAny(name, SweepWords)) return FxSystem.Sweep;
            return FxSystem.Slash;
        }

        /// <summary>The four systems that are a blow: they play inside the attack beat (StrikeShape).</summary>
        public static bool IsBlow(FxSystem system)
        {
            return system == FxSystem.Slash || system == FxSystem.Thrust || system == FxSystem.Sweep || system == FxSystem.Strike;
        }

        /// <summary>The shape a system draws. One form per system and no form shared.</summary>
        public static FxForm FormOf(FxSystem system)
        {
            switch (system)
            {
                case FxSystem.Slash: return FxForm.Arc;
                case FxSystem.Thrust: return FxForm.Line;
                case FxSystem.Sweep: return FxForm.Band;
                case FxSystem.Strike: return FxForm.Ripple;
                case FxSystem.Shield: return FxForm.Ring;
                case FxSystem.Form: return FxForm.Crest;
                case FxSystem.Spirit: return FxForm.Sigil;
                case FxSystem.Breath: return FxForm.Glow;
                default: throw new ArgumentOutOfRangeException(nameof(system), system, "No form is written for this system.");
            }
        }

        /// <summary>
        /// The look a system draws, as one stable id ("fx.ring"). It is read off the system alone, so two
        /// cards of the same system share it whatever their names, numbers or traits.
        /// </summary>
        public static string LookId(FxSystem system)
        {
            return "fx." + FormOf(system).ToString().ToLowerInvariant();
        }

        /// <summary>The system's one character, as §5.3 names it.</summary>
        public static string Glyph(FxSystem system)
        {
            switch (system)
            {
                case FxSystem.Slash: return "斬";
                case FxSystem.Thrust: return "突";
                case FxSystem.Sweep: return "払";
                case FxSystem.Strike: return "打";
                case FxSystem.Shield: return "盾";
                case FxSystem.Form: return "型";
                case FxSystem.Spirit: return "気";
                case FxSystem.Breath: return "息";
                default: throw new ArgumentOutOfRangeException(nameof(system), system, "No glyph is written for this system.");
            }
        }

        /// <summary>The blow shape and enemy motion (<see cref="StrikeSystem"/>) a system plays as.</summary>
        public static StrikeSystem ToStrike(FxSystem system)
        {
            switch (system)
            {
                case FxSystem.Thrust: return StrikeSystem.Thrust;
                case FxSystem.Sweep: return StrikeSystem.Sweep;
                case FxSystem.Strike: return StrikeSystem.Strike;
                case FxSystem.Shield: return StrikeSystem.Shield;
                case FxSystem.Slash: return StrikeSystem.Slash;
                default: return StrikeSystem.Step; // 型 / 気 / 息 carry no blow and no lunge of their own
            }
        }

        /// <summary>The system a blow of this shape belongs to; null for a step, which has none.</summary>
        public static FxSystem? FromStrike(StrikeSystem strike)
        {
            switch (strike)
            {
                case StrikeSystem.Slash: return FxSystem.Slash;
                case StrikeSystem.Thrust: return FxSystem.Thrust;
                case StrikeSystem.Sweep: return FxSystem.Sweep;
                case StrikeSystem.Strike: return FxSystem.Strike;
                case StrikeSystem.Shield: return FxSystem.Shield;
                default: return null;
            }
        }

        private static bool HasAny(string name, string[] words)
        {
            foreach (string word in words)
            {
                if (name.Contains(word)) return true;
            }
            return false;
        }
    }
}
