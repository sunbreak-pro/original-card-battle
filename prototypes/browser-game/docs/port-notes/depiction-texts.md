# Port notes: depiction texts (Bridge)

Files (C# under `unity-port/unity-project-kit/Assets/View/Depiction/Bridge/`):

| C# | JS |
| --- | --- |
| `CoreText.cs` | `src/bridge/depiction/CoreText.js` |
| `CardFaceText.cs` | `src/bridge/depiction/CardFaceText.js` |
| `StatusChipText.cs` | `src/bridge/depiction/StatusChipText.js` |
| `DeckBuilder.cs` | `src/bridge/depiction/DeckBuilder.js` (`DeckFilter`, `DeckPage`, `DeckBuilder`) |
| `DepartureBuilder.cs` | `src/bridge/depiction/DepartureBuilder.js` (`SupplyOption`, `DepartureBuilder`) |

Every Japanese line a card face, omen badge, status chip, hover line, right-click detail, refusal,
deck screen and departure screen prints comes from these five files.

## Golden texts (C# oracle for these files)

- `scratch-csharp/texts/` is a console project that compiles the C# sources in directly
  (BattleCore, Depiction/Script, Depiction/Bridge — no ProjectReference, so nothing is built under
  the read-only `unity-port/`). `dotnet run -c Release -- test/depiction/texts.golden.json` rewrites
  the golden file (1.5 MB). Regenerate it whenever the C# texts change.
- It prints: the face (`FaceOf` and `Face(card, null)`) and every text helper of all 80 cards; every
  enemy action's `OmenOf` (with and without an `OmenPreview(17, 9, true, false)`), `SystemOf`,
  `IconOf`, and the switch overrides; every `OmenKind`; every `StanceHook × StanceWhen` with six
  amount rows (`StanceText`); every `TraitCondition × TraitEffect × 3 variants` on 5 cards
  (`Condition`, `Effect`, `ConditionClause`, `EffectClause`, `TraitLine`, `ConditionWord`,
  `EffectWord`); every `StatusKind` chip at 1〜3 stacks, its glyph, family, decay and stopped-move
  line; stance chips; unit chips; the glossary; the exception messages; the deck screen (156
  `Random(seed, size)` saves incl. negative and int.Min/Max seeds, 30 saved strings and their
  notices, 100 filtered pages, titles, options); a departure screen driven step by step; and the
  hands of 48 real battle states (two hounds / polearm, gaps 0〜3, three decks, a fallen enemy, no
  stamina) with `Face(card, preview, ReachesNobody)`, `Face(card, preview)`, `Lamps`, `HpLoss`.
- The tests (`test/depiction/{CoreText,CardFaceText,StatusChipText,DeckBuilder,DepartureBuilder}.test.mjs`,
  helper `test/depiction/_texts.mjs`) compare all of it with `deepStrictEqual`, plus the
  representative NUnit cases of `CoreVocabularyTests`, `CoreOmenIconTests`, `ReachHintTests`,
  `BossWordTextTests`, `UnknownValueTests`, `PreviewNumberTests`, `CardFaceVisualTests`,
  `TermMarkTests`, `HpWickReadoutTests`, `DeckBuilderTests`, `DepartureBuilderTests`.
- The helper loads every module dynamically: while a module another agent owns is missing, the
  dependent tests are reported as `skip: needs <module>` instead of failing the file.

## API shapes (what callers rely on)

- Static classes are JS classes with static members (`CoreText.Face(...)`, `CardFaceText.Glossary`).
  Consts are static fields (`CardFaceText.MaxValues`, `CoreText.OmenStatusMax`,
  `StatusChipText.OnFoeGroup`, `DeckBuilder.RandomDefaultSize`...). C# private helpers keep their
  names as static methods (`CoreText.WhenWord`, `CardFaceText.Mark`, `DeckBuilder.Read`...).
- **Overloads folded into one function** (every C# call shape gives the C# result):
  - `CoreText.Face(card, preview)` / `Face(card, preview, outOfReach)`: `outOfReach` left
    `undefined` is the two-argument overload (`preview != null && !preview.InReach`).
  - `CoreText.SystemOf(CardDef)` / `SystemOf(EnemyActionDef)`: one function (same members read).
  - `CoreText.IconOf(kind)` / `IconOf(kind, action)`: `action` omitted or null reads the kind alone.
  - `CoreText.Chips(statuses, stanceName = "")` / `Chips(statuses, IReadOnlyList<string>)`: an array
    second argument is the list overload, anything else the single-name one.
- **`out` parameters** (CONVENTIONS: `{ ok, value }`, ok = the C# return value, value = the out):
  - `CardFaceText.Lamps(state, instanceId)` → `{ ok: bool, value: bool[] }` (CoreScriptWriter reads it so).
  - `DeckBuilder.LoadOrPrototype(saved, text)` → `DeckBuilder` (the C# 2-argument overload);
    `DeckBuilder.LoadOrPrototype(saved, text, true)` (any third argument) → `{ ok: DeckBuilder, value: notice }`.
  - private `DeckBuilder.Read(saved)` → `{ ok: DeckBuilder, value: notice }`.
- `CardFaceText.PrintedForms(word)` returns an array (C# `IEnumerable<string>` iterator).
- `CardFaceText.Glossary` is a `Map` in the C# insertion order, built once on first read (a lazy
  static getter, so the module does no work at evaluation time across import cycles).
- `DeckBuilder.AttributeOptions` / `CostOptions` → arrays of `{ Key, Value }` (C# KeyValuePair).
- `new DeckFilter({ Attribute, Cost })` stands for the C# object initializer (the fields can also be set).
- `DeckBuilder.Owned` is an array of ids (null owns the whole catalog, as in C#).
- `SupplyOption` takes the C# positional parameters `(id, name, note, startCellsBack = 0,
  startStaminaBonus = 0, firstHitGuard = 0, bossWordWard = 0)`; a C# named-argument call passes 0
  for the ones it skips.
- `DepartureBuilder.Talent` is a plain field (C# `{ get; private set; }`); `Tools` / `Consumables`
  return the live lists as C#'s `IReadOnlyList` views do.
- Script objects (`CardFace`, `CardValue`, `TermMark`, `CardDetail`, `TermDefinition`, `StatusChip`,
  `OmenFrame`, `OmenStatus`) are made with `new X()` and the initializer's members assigned in the
  C# order, so DepictionTypes' own defaults (`LitOn = []`, `StanceHintAfterPay = null`, ...) stay.

## Translation choices

- `HasFlag` → `hasFlag` (cs.js); a nullable enum's `HasValue` → `!= null` (BattleAttribute.None is 0,
  so never a truthiness test). `StatusKind.ToLabel()` → `EnumTokens.ToLabel(kind)`; `Reach.ToText()`
  stays a method of the Reach record.
- `List.Sort(comparison)` in `TermMarks` is unstable in C#; both comparisons there never tie (the
  words are distinct, two marks of a row never share a start), so `Array.prototype.sort` gives the
  same order. `TermRow` is compared through `enumIndex` (C# `enum.CompareTo`).
- `int.TryParse` (saved deck strings) is ported as NumberStyles.Integer with the invariant signs:
  optional leading/trailing white space (U+0009〜U+000D, U+0020), one `+`/`-`, ASCII digits only,
  trailing NULs after the white space, the value inside int. Pinned by golden strings
  (`" thrust:2"`, `"thrust: 2"`, `"thrust:+2"`, `"thrust:02"`, `"thrust:2\0"`, `"thrust:99999999999"`,
  `"thrust:２"`). A culture whose minus sign is not `-` is not modelled (Unity runs invariant-like here).
- `double.ToString(InvariantCulture)` for 1.5 → `String(1.5)` ("1.5").
- Exception messages copy .NET's `Message` text: `"<msg> (Parameter '<name>')\nActual value was <v>."`
  for ArgumentOutOfRangeException, `"Value cannot be null. (Parameter '<name>')"` for
  ArgumentNullException. The newline is `\n` (Linux `Environment.NewLine`, what `dotnet test` and
  the oracle print; a Windows Unity build would show `\r\n`).
- `Dictionary.TryGetValue` on `EnemyDef.Actions` reads the port's `Map` (a plain-object fallback is
  kept for safety); a missing id (the rest action) leaves the action null as C#'s out default does.

## C# quirks ported as they are

- `CoreText.StanceText` for a `TurnEnd` stance with `When = Always` reads `毎ターン開始に…`
  (WhenWord's default is the turn-start wording). No card or enemy carries that pairing today
  (根渡り / 霧足 are `GapAtLeast`), so nothing on screen shows it; the golden stance grid pins it.
- `CardFaceText.Effect` / `EffectClause` / `CoreText.EffectWord` print "" for a `Status` trait
  without a Grant (C# comment: empty on purpose).
- `DeckBuilder.Random` adds whatever it draws and relies on `Add` refusing a fourth copy; the draw
  order (and so every saved string) is the SeededRng sequence, pinned by the golden saves.

## Verification

- In the game tree the 165 tests skip until `src/core/battle/Mastery.js`, `src/core/battle/Enemies.js`
  and `src/bridge/depictionScript/DepictionTypes.js` exist (TurnLoop.js imports the first two; the
  five files import the third). The DeckBuilder / DepartureBuilder cases that start a battle also
  need `BattleLaunch.js` / `DemoSession.js` and their `depictionScript` imports.
- Meanwhile the five files were run against a scratch harness
  (`scratchpad/verify-texts/`, rebuilt by its `sync.sh`): the real `src/` and tests, with stand-ins
  only for the modules still missing — Enemies and the card data rebuilt from a C# data dump, a
  DepictionTypes with the same defaults, an empty Mastery. With the real Types, Statuses, Cards,
  CardCatalog, DeckRules, Traits, IRng, EnemyAi and TurnLoop: 159 of 165 pass, including every
  golden battle hand (Face with the preview, ReachesNobody, Lamps, HpLoss over 48 states); 5 skip
  (BattleLaunch / DemoSession); 1 fails only because the stand-in Mastery has no `ContextsOf`
  (`AHandCard_PrintsTheCostTheCoreWouldTake` plays a card).
