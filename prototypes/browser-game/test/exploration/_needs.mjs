// Loads the modules a test file needs. While a module another agent owns is still missing, the
// tests that need it are skipped as { skip: 'needs <module>' } instead of failing; they run as soon
// as the module lands. Any other load error is thrown, so a broken module still fails the tests.

/**
 * @param {Record<string, string>} specs name → path relative to the calling test file
 * @param {string} base the caller's import.meta.url
 * @returns {Promise<{ skip: string | false, m: Record<string, object> }>}
 */
export async function load(specs, base) {
  const m = {};
  const missing = new Set();
  for (const [name, rel] of Object.entries(specs)) {
    try {
      m[name] = await import(new URL(rel, base).href);
    } catch (e) {
      if (e && e.code === 'ERR_MODULE_NOT_FOUND') {
        const hit = /Cannot find module '([^']+)'/.exec(String(e.message));
        const file = hit ? hit[1].replace(/^.*?\/(src\/)/, '$1') : rel;
        missing.add(file);
      } else {
        throw e;
      }
    }
  }
  return { skip: missing.size === 0 ? false : `needs ${[...missing].join(', ')}`, m };
}
