// The meta screens' computed times and breaths: the numbers title.js, hall.js and ending.js work out at
// mount (a delay that grows with the letters of a name, the moment the 襲名 seal lands, the ending card's
// t1 / t2 / t3) and the cosine breaths they paint every frame (the title's foot light, the hall's flames, the
// ending's glow). Times are ms after the scene mounts, on the scene's clock; `now` is the frame time in ms.
// The fixed durations, eases and delays stay with each scene, written as the JS writes them.
using System;
using System.Collections.Generic;
using InheritanceCore;

namespace Trial.Script
{
    /// <summary>The ending card's three beats (ending.js:107-120): heir line, rule, next line; the button 300 ms after t3.</summary>
    public readonly struct EndingTimeline
    {
        public readonly int T1;
        public readonly int T2;
        public readonly int T3;

        public EndingTimeline(int t1, int t2, int t3)
        {
            T1 = t1;
            T2 = t2;
            T3 = t3;
        }

        /// <summary>The 継承の間へ button's delay (ending.js:120: t3 + 300).</summary>
        public int ButtonAt => T3 + 300;
    }

    public static class MetaTimelines
    {
        // ---- letters ----

        /// <summary>
        /// A string's letters as the JS spreads it (<c>[...s]</c>): one entry per code point, a surrogate pair kept
        /// whole. The scenes put each in a box of its own (the title's tagline, the heir's name, the ending's title
        /// and sentence, spaces included) and time them by their count.
        /// </summary>
        public static IReadOnlyList<string> Chars(string s)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(s)) return list;
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                {
                    list.Add(s.Substring(i, 2));
                    i++;
                }
                else
                {
                    list.Add(s[i].ToString());
                }
            }
            return list;
        }

        // ---- the cosine breath ----

        /// <summary>
        /// 0 → 1 → 0 once a period: <c>0.5 - 0.5 * Math.cos(((now + phase) / period) * 2 * Math.PI)</c>, the breath
        /// every meta light takes (title.js:147, hall.js:383, ending.js:124).
        /// </summary>
        public static double Breath(double nowMs, double periodMs, double phaseMs = 0)
        {
            return 0.5 - 0.5 * Math.Cos(((nowMs + phaseMs) / periodMs) * 2 * Math.PI);
        }

        /// <summary>The heir's foot light on the title breathes with the spring: opacity 0.55〜0.90, every 4200 ms.</summary>
        // js: scenes/meta/title.js:146
        public static double TitleFootLight(double nowMs)
        {
            double k = Breath(nowMs, 4200);
            return 0.55 + 0.35 * k;
        }

        /// <summary>The flame on the hall's <paramref name="i"/>-th untaken mark: opacity 0.55〜1, every 1900 ms, 700 ms apart.</summary>
        // js: scenes/meta/hall.js:381
        public static double HallFlameOpacity(double nowMs, int i)
        {
            double k = Breath(nowMs, 1900, i * 700);
            return 0.55 + 0.45 * k;
        }

        /// <summary>The same flame's scale (both axes): 0.85〜1.10.</summary>
        // js: scenes/meta/hall.js:385
        public static double HallFlameScale(double nowMs, int i)
        {
            double k = Breath(nowMs, 1900, i * 700);
            return 0.85 + 0.25 * k;
        }

        /// <summary>
        /// The ending's glow: a death leaves a cold breath of the place (0.35〜0.60 every 6200 ms); a life closed alive
        /// keeps the warm light of the heirs (0.60〜0.85 every 3800 ms).
        /// </summary>
        // js: scenes/meta/ending.js:123
        public static double EndingGlow(double nowMs, bool died)
        {
            double k = Breath(nowMs, died ? 6200 : 3800);
            return (died ? 0.35 : 0.6) + 0.25 * k;
        }

        // ---- the title (title.js:122-143) ----

        /// <summary>The tagline's <paramref name="i"/>-th letter, both columns counted through (0〜23): 900 + i × 85.</summary>
        // js: scenes/meta/title.js:135
        public static int TitleTaglineCharAt(int i) => 900 + i * 85;

        /// <summary>The menu's <paramref name="i"/>-th item: 1800 + i × 110.</summary>
        // js: scenes/meta/title.js:139
        public static int TitleMenuItemAt(int i) => 1800 + i * 110;

        // ---- the hall's arrival and the naming (hall.js:343-379) ----

        /// <summary>riseIn's delay of the <paramref name="i"/>-th element (util.js:104): delay + i × step.</summary>
        // js: scenes/meta/util.js:104
        public static int RiseInAt(int delay, int step, int i) => delay + i * step;

        /// <summary>The left column (heading, each past life's card, the rules box): riseIn delay 380, step 90.</summary>
        // js: scenes/meta/hall.js:353
        public static int HallLivesAt(int i) => RiseInAt(380, 90, i);

        /// <summary>The middle column (heading, the seven strata): riseIn delay 480, step 60 (dy 10).</summary>
        // js: scenes/meta/hall.js:354
        public static int HallStrataAt(int i) => RiseInAt(480, 60, i);

        /// <summary>The right column (heading, the generation): riseIn delay 1000, step 80.</summary>
        // js: scenes/meta/hall.js:357
        public static int HallHeirAt(int i) => RiseInAt(1000, 80, i);

        /// <summary>The heir's name, letter <paramref name="i"/>: 1250 + i × 110.</summary>
        // js: scenes/meta/hall.js:362
        public static int HallNameCharAt(int i) => 1250 + i * 110;

        /// <summary>When the 襲名 seal starts down: 1250 + letters × 110 + 240 (1820 for a three-letter name).</summary>
        // js: scenes/meta/hall.js:364
        public static int SealAt(int nameLetters) => 1250 + nameLetters * 110 + 240;

        /// <summary><see cref="SealAt(int)"/> for a name, counted as the JS spreads it.</summary>
        // js: scenes/meta/hall.js:364
        public static int SealAt(string name) => SealAt(Chars(name).Count);

        /// <summary>The seal lands (170 ms, ease in): the moment heirNamed sounds and the ring spreads.</summary>
        // js: scenes/meta/hall.js:368
        public static int SealStampedAt(int sealAt) => sealAt + 170;

        /// <summary>
        /// 装束, the figure and the survivor's box after the stamp: 120 + i × 120 ms after it (each 520 ms, out),
        /// so <see cref="SealStampedAt"/> + this from the mount.
        /// </summary>
        // js: scenes/meta/hall.js:372
        public static int HallAttireAfterStamp(int i) => 120 + i * 120;

        /// <summary>出立の支度へ: sealAt + 500.</summary>
        // js: scenes/meta/hall.js:376
        public static int HallStartButtonAt(int sealAt) => sealAt + 500;

        /// <summary>The key line: sealAt + 700.</summary>
        // js: scenes/meta/hall.js:378
        public static int HallKeysAt(int sealAt) => sealAt + 700;

        // ---- the ending card (ending.js:94-120) ----

        /// <summary>The title's letter <paramref name="i"/>: 800 + i × 140.</summary>
        // js: scenes/meta/ending.js:106
        public static int EndingTitleCharAt(int i) => 800 + i * 140;

        /// <summary>The sentence's letter <paramref name="i"/> (spaces count): t1 + 200 + i × 38.</summary>
        // js: scenes/meta/ending.js:110
        public static int EndingSentenceCharAt(int t1, int i) => t1 + 200 + i * 38;

        /// <summary>Line <paramref name="i"/> of what stays or passes on: t2 + 300 + i × 260.</summary>
        // js: scenes/meta/ending.js:115
        public static int EndingLineAt(int t2, int i) => t2 + 300 + i * 260;

        /// <summary>
        /// t1 = 800 + titleLetters × 140 + 200; t2 = t1 + 200 + sentenceLetters × 38 + 200;
        /// t3 = t2 + 300 + lines × 260 + 200. 倒れた on a three-line card (「第 7 層の戦いで倒れた。」, 13 letters): 1420 /
        /// 2314 / 3594. (meta-a.md §4.2 counts that sentence as 14 letters and prints 2352 / 3632: the count is off by
        /// one, the formula is the JS's.)
        /// </summary>
        // js: scenes/meta/ending.js:107
        public static EndingTimeline Ending(int titleLetters, int sentenceLetters, int lines)
        {
            int t1 = 800 + titleLetters * 140 + 200;
            int t2 = t1 + 200 + sentenceLetters * 38 + 200;
            int t3 = t2 + 300 + lines * 260 + 200;
            return new EndingTimeline(t1, t2, t3);
        }

        /// <summary>
        /// <see cref="Ending(int,int,int)"/> for the view the card prints (pass it through
        /// <see cref="MetaLines.WithWaitingTrace"/> first, as ending.js does: a cleared life's wait adds a line).
        /// </summary>
        // js: scenes/meta/ending.js:107
        public static EndingTimeline Ending(EndingView view) =>
            Ending(Chars(view.Title).Count, Chars(view.Sentence).Count, view.Lines.Count);
    }
}
