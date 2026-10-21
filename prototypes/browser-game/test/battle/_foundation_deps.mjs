// Helper for the battle-foundation tests (Types / Attributes / Columns / Combat / Field / Constants).
// Types.js and Combat.js import Statuses.js statically, as the C# does. While another agent's module
// on that path is not written yet, a static import would make the whole test file fail to load; this
// loads the modules dynamically instead and hands back a skip reason ('needs Statuses.js') so the
// tests report as skipped. Any other load error (a syntax error, a TDZ error from an import cycle)
// is re-thrown: it must fail loudly.

/**
 * @param {string} base   import.meta.url of the test file
 * @param {string[]} paths module paths relative to the test file
 * @returns {Promise<{ mods: object|null, skip: string|false }>}
 */
export async function loadDeps(base, paths) {
  const mods = {};
  try {
    for (const p of paths) Object.assign(mods, await import(new URL(p, base).href));
    return { mods, skip: false };
  } catch (e) {
    if (e && e.code === 'ERR_MODULE_NOT_FOUND') {
      const m = /Cannot find module '([^']+)'/.exec(e.message);
      return { mods: null, skip: `needs ${m ? m[1].split('/').pop() : 'a module'}` };
    }
    throw e;
  }
}
