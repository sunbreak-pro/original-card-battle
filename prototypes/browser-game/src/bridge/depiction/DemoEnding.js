// Port of unity-port/unity-project-kit/Assets/View/Depiction/Bridge/DemoEnding.cs
// The end of a demo battle that cannot strand the player (#297). Pure C#, no UnityEngine.
// DemoFlow takes the battle's 「降参する」 down, tallies the battle (DemoSession.Finish / Surrender)
// and then puts the end screen up. If the tally or the end screen's words throw, the exception goes
// to the caller's report (Debug.LogException in Unity) and a plain end screen stands in, whose
// buttons do not touch the run: the mode screen again, or the deck screen.
import { ArgumentNullException } from '../../shared/cs.js';
import { DemoEndScreen } from './DemoSession.js';

/** new ArgumentNullException(nameof(x)) with .NET's message. */
function argNull(name) {
  const e = new ArgumentNullException(`Value cannot be null. (Parameter '${name}')`);
  e.ParamName = name;
  return e;
}

/** C# private static void Report(Action<Exception> report, Exception e) */
function Report(report, e) {
  if (report == null) return;
  try {
    report(e);
  } catch {
    // A report that throws must not take the fallback away.
  }
}

export const DemoEnding = {
  /**
   * Tallies the battle: runs `tally` (Action) and says whether it went through. A throw is
   * reported, never passed on, so the flow still gets to an end screen.
   */
  Tally(tally, report) {
    if (tally == null) throw argNull('tally');
    try {
      tally();
      return true;
    } catch (e) {
      Report(report, e);
      return false;
    }
  },

  /**
   * The end screen to show: `build`'s (Func<DemoEndScreen>) when the battle was tallied and the
   * words came out, else Fallback. A throw from `build` is reported.
   */
  Screen(tallied, build, report) {
    if (!tallied || build == null) return DemoEnding.Fallback();
    try {
      const screen = build();
      return screen ?? DemoEnding.Fallback();
    } catch (e) {
      Report(report, e);
      return DemoEnding.Fallback();
    }
  },

  /**
   * The end screen when the battle could not be tallied or its words could not be made: no
   * result, and only the ways that start afresh — the mode screen (same deck) and the deck
   * screen. 「もう一度」 and the chain's rest stay away: they would step a run left half-done.
   */
  Fallback() {
    const screen = new DemoEndScreen({
      Title: '結果を表示できませんでした',
      Won: false,
      CanGoOn: false,
      CanAgain: false,
      CanChooseEnemy: true,
      ChooseEnemyLabel: '戦い方を選び直す',
    });
    screen.Lines.push('戦闘の記録の途中で問題が起きました。');
    screen.Lines.push('戦い方かデッキを選び直してください。');
    return screen;
  },
};
