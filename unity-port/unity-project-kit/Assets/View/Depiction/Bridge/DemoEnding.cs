// The end of a demo battle that cannot strand the player (#297). Pure C#, no UnityEngine.
// DemoFlow takes the battle's 「降参する」 down, tallies the battle (DemoSession.Finish / Surrender)
// and then puts the end screen up. If the tally or the end screen's words throw, the exception goes
// to the caller's report (Debug.LogException in Unity) and a plain end screen stands in, whose
// buttons do not touch the run: the mode screen again, or the deck screen.
using System;

namespace Depiction.Bridge
{
    public static class DemoEnding
    {
        /// <summary>
        /// Tallies the battle: runs <paramref name="tally"/> and says whether it went through. A throw
        /// is reported, never passed on, so the flow still gets to an end screen.
        /// </summary>
        public static bool Tally(Action tally, Action<Exception> report)
        {
            if (tally == null) throw new ArgumentNullException(nameof(tally));
            try
            {
                tally();
                return true;
            }
            catch (Exception e)
            {
                Report(report, e);
                return false;
            }
        }

        /// <summary>
        /// The end screen to show: <paramref name="build"/>'s when the battle was tallied and the words
        /// came out, else <see cref="Fallback"/>. A throw from <paramref name="build"/> is reported.
        /// </summary>
        public static DemoEndScreen Screen(bool tallied, Func<DemoEndScreen> build, Action<Exception> report)
        {
            if (!tallied || build == null) return Fallback();
            try
            {
                DemoEndScreen screen = build();
                return screen ?? Fallback();
            }
            catch (Exception e)
            {
                Report(report, e);
                return Fallback();
            }
        }

        /// <summary>
        /// The end screen when the battle could not be tallied or its words could not be made: no
        /// result, and only the ways that start afresh — the mode screen (same deck) and the deck
        /// screen. 「もう一度」 and the chain's rest stay away: they would step a run left half-done.
        /// </summary>
        public static DemoEndScreen Fallback()
        {
            var screen = new DemoEndScreen
            {
                Title = "結果を表示できませんでした",
                Won = false,
                CanGoOn = false,
                CanAgain = false,
                CanChooseEnemy = true,
                ChooseEnemyLabel = "戦い方を選び直す",
            };
            screen.Lines.Add("戦闘の記録の途中で問題が起きました。");
            screen.Lines.Add("戦い方かデッキを選び直してください。");
            return screen;
        }

        private static void Report(Action<Exception> report, Exception e)
        {
            if (report == null) return;
            try
            {
                report(e);
            }
            catch (Exception)
            {
                // A report that throws must not take the fallback away.
            }
        }
    }
}
