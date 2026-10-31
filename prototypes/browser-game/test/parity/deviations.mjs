// Intentional, documented deviations of the JS port from the C# oracle. run.mjs counts a case listed
// here as a "deviation" instead of a failure — but only when `accepts` says the difference is the one
// documented, so anything else that breaks in the same case still fails. Every deviation is printed
// with its reason on every run, and one whose case matches the oracle again is reported as stale.
//
// Keep this list short: the port's rule is that it behaves exactly like the C# (docs/CONVENTIONS.md).

/** The exception the C# CoreScriptWriter throws in a two-enemy battle (docs/port-notes/depiction-writer.md). */
function isWriterBug(v) {
  return v != null && typeof v === 'object' && v.$error === 'KeyNotFoundException'
    && /^The given key '.+' was not present in the dictionary\.$/.test(String(v.Message));
}

const MULTI_ENEMY_REASON =
  'JS extension: the depiction writer models every enemy (docs/port-notes/depiction-multi-enemy.md). The C# '
  + 'CoreScriptWriter keeps one omen and reads the crossbow\'s "bolt" against the polearm\'s actions, throwing '
  + 'KeyNotFoundException in the roster §9 pair the exploration hands in on layers 2〜3; the port writes the battle instead.';

export const DEVIATIONS = [
  {
    suite: 'scripts',
    id: 'writer:charge_release|polearm_warped+crossbow_hunter|1',
    reason: MULTI_ENEMY_REASON,
    /**
     * The greedy battle's calls and the opening frame are the oracle's; every step the C# writer
     * threw on is written by the port (an event list, no exception). The events themselves differ
     * by design (the C# models one enemy), so they are not compared.
     */
    accepts({ expected, actual, ctx, firstDiff }) {
      if (!expected || !actual || !Array.isArray(expected.Steps) || !Array.isArray(actual.Steps)) return false;
      if (firstDiff(expected.Opening, actual.Opening, ctx, 'output.Opening')) return false;
      if (expected.Steps.length !== actual.Steps.length) return false;
      let threw = 0;
      for (let i = 0; i < expected.Steps.length; i++) {
        const { Events: want, ...wantCall } = expected.Steps[i];
        const { Events: got, ...gotCall } = actual.Steps[i];
        if (firstDiff(wantCall, gotCall, ctx, `output.Steps[${i}]`)) return false;
        if (!Array.isArray(got)) return false;
        if (isWriterBug(want)) threw += 1;
      }
      return threw > 0;
    },
  },
  {
    suite: 'scripts',
    id: 'source:charge_release|polearm_warped+crossbow_hunter|1',
    reason: MULTI_ENEMY_REASON,
    /**
     * The C# source throws on its first AdvanceAuto, so the oracle holds the exception alone; the port
     * plays the unattended run to its end (or to the case's turn limit).
     */
    accepts({ expected, actual }) {
      return isWriterBug(expected) && actual != null && typeof actual === 'object' && !('$error' in actual)
        && actual.Finished === true && Array.isArray(actual.Moves) && actual.Moves.length > 0;
    },
  },
];

/** The deviation registered for this case, or null. */
export function deviationFor(suite, id) {
  return DEVIATIONS.find((d) => d.suite === suite && d.id === id) ?? null;
}
