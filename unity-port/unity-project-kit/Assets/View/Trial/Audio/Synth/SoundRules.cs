// The pure rules around the sounds: settings normalisation, ambience ids, the miasma line variant, the step plan and its
// calls (the two-sounds rule), the ±3 % alternation and the step pan, the per-name gaps and the design system's hover
// throttle, and the battle's two-sound budget (scenes/battle/player.js).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Trial.Audio.Synth
{
    /// <summary>One planned play: a name, its delay, and the options a miasma line carries.</summary>
    public readonly struct SoundCall
    {
        public readonly string Name;
        public readonly double DelayMs;
        public readonly double? Mult;
        public readonly double? PitchMul;

        public SoundCall(string name, double delayMs = 0, double? mult = null, double? pitchMul = null)
        {
            Name = name; DelayMs = delayMs; Mult = mult; PitchMul = pitchMul;
        }

        public override string ToString() => Name + "@" + DelayMs.ToString(CultureInfo.InvariantCulture);
    }

    public static class SoundRules
    {
        // ---- settings (Audio.js:127-144) ---------------------------------------------------------------------------

        /// <summary>js: scenes/audio/Audio.js:127 clamp01(v, dflt): non-finite → the default, else clamped to 0..1.</summary>
        public static double Clamp01(double v, double dflt)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return dflt;
            return v < 0 ? 0 : v > 1 ? 1 : v;
        }

        /// <summary>
        /// js: scenes/audio/Audio.js:134 normalizeSettings(s) on a parsed blob (a missing key is undefined; values go through JS
        /// <c>Number()</c>): each volume clamped to 0..1 with non-finite → default; muted only if it is a bool.
        /// </summary>
        public static SoundSettings NormalizeSettings(IReadOnlyDictionary<string, object> s)
        {
            SoundSettings d = SoundData.SoundSettingsDefaults();
            if (s == null) return d;
            return new SoundSettings
            {
                Master = Clamp01(JsNumber(s, "master"), d.Master),
                Effects = Clamp01(JsNumber(s, "effects"), d.Effects),
                Ambience = Clamp01(JsNumber(s, "ambience"), d.Ambience),
                Reverb = Clamp01(JsNumber(s, "reverb"), d.Reverb),
                Muted = s.TryGetValue("muted", out object m) && m is bool b ? b : d.Muted,
            };
        }

        /// <summary>The typed form (every field present): clamps the volumes, NaN → default.</summary>
        public static SoundSettings NormalizeSettings(SoundSettings s)
        {
            SoundSettings d = SoundData.SoundSettingsDefaults();
            if (s == null) return d;
            return new SoundSettings
            {
                Master = Clamp01(s.Master, d.Master),
                Effects = Clamp01(s.Effects, d.Effects),
                Ambience = Clamp01(s.Ambience, d.Ambience),
                Reverb = Clamp01(s.Reverb, d.Reverb),
                Muted = s.Muted,
            };
        }

        private static double JsNumber(IReadOnlyDictionary<string, object> s, string key)
        {
            if (!s.TryGetValue(key, out object v)) return double.NaN; // undefined
            return JsNumber(v);
        }

        /// <summary>JS <c>Number(v)</c> for the values a parsed settings blob can hold.</summary>
        public static double JsNumber(object v)
        {
            switch (v)
            {
                case null: return 0;
                case bool b: return b ? 1 : 0;
                case double d: return d;
                case float f: return f;
                case int i: return i;
                case long l: return l;
                case decimal m: return (double)m;
                case string str:
                    {
                        string t = str.Trim();
                        if (t.Length == 0) return 0;
                        // JS StringToNumber also reads unsigned 0x / 0o / 0b literals ("0x10" → 16).
                        if (t.Length > 2 && t[0] == '0')
                        {
                            char p = char.ToLowerInvariant(t[1]);
                            int radix = p == 'x' ? 16 : p == 'o' ? 8 : p == 'b' ? 2 : 0;
                            if (radix != 0) return RadixNumber(t.Substring(2), radix);
                        }
                        // .NET also reads spellings such as "infinity" that JS reads as NaN: both are non-finite, so a setting
                        // ends at its default either way.
                        return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double r) ? r : double.NaN;
                    }
                default: return double.NaN;
            }
        }

        private static double RadixNumber(string digits, int radix)
        {
            double value = 0;
            foreach (char c in digits)
            {
                int d = c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'z' ? c - 'a' + 10 : c >= 'A' && c <= 'Z' ? c - 'A' + 10 : 99;
                if (d >= radix) return double.NaN;
                value = value * radix + d;
            }
            return value;
        }

        // ---- ids and lines (Audio.js:147-170) ---------------------------------------------------------------------

        // JS /^\d+$/ (no m flag): $ is the very end. .NET's $ also matches before a final "\n", so \z.
        private static readonly Regex Digits = new Regex("^[0-9]+\\z", RegexOptions.CultureInvariant);

        /// <summary>
        /// js: scenes/audio/Audio.js:147 ambienceId(id): 1..7 / "3" / "layer3" / "title" → an ambience id; null, "" or "none" →
        /// null (silence, returns true); anything else → false (the JS's undefined). As in the JS, a fractional number in 1..7
        /// gives "layer" + its text (3.5 → "layer3.5", not a place: setAmbience would then fail).
        /// </summary>
        public static bool TryAmbienceId(object id, out string result)
        {
            result = null;
            if (id == null) return true;
            if (id is string s0 && (s0.Length == 0 || s0 == "none")) return true;
            bool isNumber = id is double || id is float || id is int || id is long;
            string text = isNumber ? JsNumberText(JsNumber(id)) : id is bool bb ? (bb ? "true" : "false") : id.ToString();
            if (isNumber || Digits.IsMatch(text))
            {
                double n = isNumber ? JsNumber(id) : double.Parse(text, CultureInfo.InvariantCulture);
                if (n >= 1 && n <= 7)
                {
                    result = "layer" + JsNumberText(n);
                    return true;
                }
                return false;
            }
            if (id is string s && SoundData.AmbienceDefs.ContainsKey(s))
            {
                result = s;
                return true;
            }
            return false;
        }

        private static string JsNumberText(double n) => n.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>js: scenes/audio/Audio.js:157 miasmaLineIndex: 20 → 0, 40 → 1, 60 → 2, 80 or more → 3; below or non-finite → 0.</summary>
        public static int MiasmaLineIndex(double linePct)
        {
            double i = JsRound(linePct / 20) - 1;
            return i < 0 || double.IsNaN(i) || double.IsInfinity(i) ? 0 : i > 3 ? 3 : (int)i;
        }

        /// <summary>JS <c>Math.round</c>: halves round up (toward +∞).</summary>
        public static double JsRound(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return x;
            double f = Math.Floor(x);
            return x - f >= 0.5 ? f + 1 : f;
        }

        /// <summary>
        /// js: scenes/audio/Audio.js:166 stepPlan({ fresh, line, miasmaDeath, result }): the step (action) and at most one result,
        /// by priority miasma death &gt; miasma line &gt; the node's own result.
        /// </summary>
        public static string[] StepPlan(bool fresh = true, double line = 0, bool miasmaDeath = false, string result = null)
        {
            string action = fresh ? "nodeStep" : "nodeReturn";
            bool lineSet = line != 0 && !double.IsNaN(line);
            string res = miasmaDeath ? "miasmaDeath" : lineSet ? "miasmaLine" : string.IsNullOrEmpty(result) ? null : result;
            return res != null ? new[] { action, res } : new[] { action };
        }

        /// <summary>
        /// js: scenes/audio/Audio.js:805 miasmaLine(pct, delayMs): the swell for one 20 % line, 10 % louder and a whole tone lower
        /// per line (<c>mult = 1 + 0.1i</c>, <c>pitchMul = MIASMA_LINE_PITCH[i]</c>).
        /// </summary>
        public static SoundCall MiasmaLineCall(double linePct, double delayMs = 0)
        {
            int i = MiasmaLineIndex(linePct);
            return new SoundCall("miasmaLine", delayMs, 1 + i * 0.1, SoundData.MiasmaLinePitch[i]);
        }

        /// <summary>
        /// js: scenes/audio/Audio.js:797 stepTo(step): the calls it makes: the action now, and the result 280 ms later (a miasma
        /// line through <see cref="MiasmaLineCall"/>).
        /// </summary>
        public static SoundCall[] StepToCalls(bool fresh = true, double line = 0, bool miasmaDeath = false, string result = null)
        {
            string[] plan = StepPlan(fresh, line, miasmaDeath, result);
            if (plan.Length == 1) return new[] { new SoundCall(plan[0]) };
            SoundCall second = plan[1] == "miasmaLine" ? MiasmaLineCall(line, SoundData.ResultAfterStepMs) : new SoundCall(plan[1], SoundData.ResultAfterStepMs);
            return new[] { new SoundCall(plan[0]), second };
        }

        // ---- alternation (Audio.js:728-732, bundle.js:835) --------------------------------------------------------

        /// <summary>The ±3 % pitch alternation by the play counter after its increment: <c>((turn % 3) − 1)·0.03</c>.</summary>
        public static double Wobble(int turn) => (turn % 3 - 1) * 0.03;

        /// <summary>Whether a timbre alternates: paper and wood in the design system; paper, wood and step in the meta synth.</summary>
        public static bool Wobbles(string timbre, bool meta) => timbre == "paper" || timbre == "wood" || (meta && timbre == "step");

        /// <summary>js: scenes/audio/Audio.js:728 the step's pan by the step counter after its increment: odd → +0.12 (right).</summary>
        public static double StepPan(int stepTurn) => stepTurn % 2 != 0 ? 0.12 : -0.12;

        // ---- gaps -------------------------------------------------------------------------------------------------

        /// <summary>
        /// js: scenes/audio/Audio.js:720 the per-name gap of the meta presets: a play inside <c>gap</c> ms of the last accepted play
        /// of the same name is refused; an accepted play records its time (a refused one records nothing).
        /// </summary>
        public sealed class GapGate
        {
            private readonly Dictionary<string, double> lastAt = new Dictionary<string, double>();

            /// <summary>True to play now (and records the time); false inside the gap. <paramref name="gapMs"/> null or 0: always.</summary>
            public bool TryAccept(string name, double? gapMs, double now)
            {
                if (gapMs.HasValue && gapMs.Value != 0 && lastAt.TryGetValue(name, out double last) && now - last < gapMs.Value / 1000) return false;
                lastAt[name] = now;
                return true;
            }

            public void Clear() => lastAt.Clear();
        }

        /// <summary>
        /// js: vendor/ds/bundle.js:830 the design system's hover throttle: refused when a hover was played less than 50 ms ago — but a
        /// last hover at time 0 does not count (<c>lastAt.hover &amp;&amp; …</c> is false for 0, a JS quirk kept as is).
        /// </summary>
        public static bool DsHoverThrottled(double? lastHoverAt, double now) =>
            lastHoverAt.HasValue && lastHoverAt.Value != 0 && now - lastHoverAt.Value < SoundData.DsHoverThrottleS;

        // ---- the battle's two-sound budget (scenes/battle/player.js) -----------------------------------------------

        /// <summary>js: scenes/battle/player.js:204 HIT_SOUND: the hit sound of a blow by its strike system (打 for pushes and pulls).</summary>
        public static string HitSoundOf(string strikeSystem)
        {
            switch (strikeSystem)
            {
                case "Slash": return "hitSlash";
                case "Thrust": return "hitThrust";
                case "Sweep": return "hitSlash";
                case "Strike": return "hitBlunt";
                default: return null;
            }
        }

        /// <summary>
        /// js: scenes/battle/player.js:230-238, 375-380, 443-447, 1594-1595: the sounds left in an event's budget of two
        /// (sound.md 「1 回の行動につき 2 つまで」), of which <see cref="EssentialLeft"/> are kept for the event's own action and
        /// result (the blow's system hit, the body's hit, the Guard's block or break).
        /// </summary>
        public sealed class SoundBudget
        {
            public int Budget { get; private set; } = 2;
            public int EssentialLeft { get; private set; }

            /// <summary>js: player.js:446 beginPlan: a new event, with the count of essential sounds its cues will ask for.</summary>
            public void Begin(int essentialSounds)
            {
                Budget = 2;
                EssentialLeft = Math.Min(Budget, essentialSounds);
            }

            /// <summary>js: player.js:1594 a card play: the release sound (the hand's) is the action.</summary>
            public void BeginCardPlay()
            {
                Budget = 1;
                EssentialLeft = Math.Min(Budget, EssentialLeft);
            }

            /// <summary>js: player.js:375 sfxBudget(name, essential): true when the sound may play (and spends a slot).</summary>
            public bool TryPlay(string name, bool essential = false)
            {
                if (string.IsNullOrEmpty(name) || Budget <= 0) return false;
                if (essential) EssentialLeft = Math.Max(0, EssentialLeft - 1);
                else if (Budget - EssentialLeft <= 0) return false;
                Budget -= 1;
                return true;
            }
        }
    }
}
