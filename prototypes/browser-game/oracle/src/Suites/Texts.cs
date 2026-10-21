// Suite 4 (texts): CoreText / CardFaceText / StatusChipText for every card (and mastery step), every
// status, every enemy action, and every omen shown in the first 2 battles per enemy setup.
using BattleCore;
using Depiction;
using Depiction.Bridge;

namespace Oracle;

public static class TextsSuite
{
    public static int Run(string outDir)
    {
        using var file = new SuiteFile(outDir, "texts", "CardDef", "EnemyDef");

        // ---- cards and their mastery steps ----
        foreach (var def in CardCatalog.All)
        {
            file.Add("card:" + def.Id, new OrderedMap { ["Card"] = def.Id, ["Rank"] = 0 }, () => CardTexts(def));
            var profile = MasteryProfiles.Find(def.Id);
            if (profile == null) continue;
            for (int rank = 1; rank <= Mastery.TopRank; rank++)
            {
                int r = rank;
                file.Add($"card:{def.Id}@{r}", new OrderedMap { ["Card"] = def.Id, ["Rank"] = r }, () => CardTexts(Mastery.Master(def, r, profile)));
            }
        }

        // ---- statuses ----
        foreach (StatusKind kind in Enum.GetValues(typeof(StatusKind)))
        {
            file.Add("status:" + kind, new OrderedMap { ["Kind"] = kind }, () =>
            {
                var chips = new List<object>();
                var effects = new List<object>();
                foreach (int stacks in StatusStacks)
                {
                    chips.Add(Tables.Try(() => StatusChipText.Chip(kind, stacks)));
                    effects.Add(Tables.Try(() => StatusChipText.Effect(kind, stacks)));
                }
                return new OrderedMap
                {
                    ["Stacks"] = StatusStacks,
                    ["Chip"] = chips,
                    ["Effect"] = effects,
                    ["KindOf"] = Tables.Try(() => StatusChipText.KindOf(kind)),
                    ["GroupWord"] = Tables.Try(() => StatusChipText.GroupWord(StatusChipText.KindOf(kind))),
                    ["Decay"] = Tables.Try(() => StatusChipText.Decay(kind)),
                    ["GlyphOf"] = CoreText.GlyphOf(kind),
                    ["MoveBlockedLine"] = CoreText.MoveBlockedLine(kind),
                    ["ToToken"] = kind.ToToken(),
                    ["ToLabel"] = kind.ToLabel(),
                };
            });
        }

        // ---- status chip rows (a set of words plus stances) ----
        foreach (var (id, set, stances) in ChipRows())
        {
            var input = new OrderedMap { ["Statuses"] = set, ["Stances"] = stances };
            file.Add("chips:" + id, input, () => new OrderedMap
            {
                ["Chips"] = Tables.Try(() => StatusChipText.Chips(set, stances)),
                ["StanceChips"] = Tables.Try(() => StatusChipText.StanceChips(stances)),
                ["CoreChips"] = Tables.Try(() => CoreText.Chips(set, stances)),
            });
        }

        // ---- enemy actions (switch overrides included) ----
        foreach (var enemy in Enemies.All)
        {
            var actions = new List<(string Source, EnemyActionDef Action)>();
            foreach (var kv in enemy.Actions) actions.Add(("Actions", kv.Value));
            if (enemy.Switches != null)
            {
                foreach (var sw in enemy.Switches)
                {
                    if (sw.Overrides == null) continue;
                    foreach (var a in sw.Overrides) actions.Add(("Switch:" + sw.Id, a));
                }
            }
            foreach (var (source, action) in actions)
            {
                var input = new OrderedMap { ["Enemy"] = enemy.Id, ["Source"] = source, ["Action"] = action.Id };
                file.Add($"action:{enemy.Id}:{source}:{action.Id}", input, () => ActionTexts(action));
            }
        }

        // ---- omens shown in the first 2 battles per enemy setup ----
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var spec in BattleRun.FirstTwoPerEnemy())
        {
            var trace = BattleRun.Fight(spec);
            foreach (int i in trace.DecisionSteps)
            {
                var state = trace.Steps[i].Result.State;
                for (int unit = 0; unit < state.Enemies.Count; unit++)
                {
                    var e = state.Enemies[unit];
                    if (!e.Alive) continue;
                    OmenPreview preview = TurnLoop.PreviewOmen(state, unit);
                    foreach (var (which, omen) in new[] { ("Omen", e.Omen), ("Plan", e.Plan) })
                    {
                        if (omen == null) continue;
                        var input = new OrderedMap
                        {
                            ["Enemy"] = e.Def.Id,
                            ["Omen"] = omen,
                            ["Preview"] = which == "Omen" ? preview : null,
                        };
                        string key = new CaseWriter(null).Write(input).ToJsonString(Json.Compact);
                        if (!seen.Add(key)) continue;
                        file.Add($"omen:{e.Def.Id}:{seen.Count}", input, () => new OrderedMap
                        {
                            ["OmenOf"] = CoreText.OmenOf(omen, e.Def, which == "Omen" ? preview : null),
                            ["OmenOfNoPreview"] = CoreText.OmenOf(omen, e.Def),
                            ["OmenKindWord"] = Tables.Try(() => CoreText.OmenKindWord(omen.Label.Kind)),
                            ["ReachText"] = omen.Label.Reach?.ToText(),
                        });
                    }
                }
            }
        }

        return file.Count;
    }

    public static readonly int[] StatusStacks = { 1, 2, 3, 5 };

    private static IEnumerable<(string, StatusSet, string[])> ChipRows()
    {
        yield return ("empty", StatusSet.Empty, Array.Empty<string>());
        yield return ("bleed_fragile", StatusSet.Of((StatusKind.Bleed, 3), (StatusKind.Fragile, 1)), Array.Empty<string>());
        yield return ("self_words", StatusSet.Of((StatusKind.Empower, 2), (StatusKind.Focus, 1), (StatusKind.Parry, 1), (StatusKind.Regen, 4)), new[] { "岩の構え" });
        yield return ("boss_words", StatusSet.Of((StatusKind.MiasmaShroud, 1), (StatusKind.Binding, 2), (StatusKind.Hook, 1)), new[] { "水の構え", "水の構え", "流れの構え" });
        yield return ("everything", StatusSet.Of(Enum.GetValues(typeof(StatusKind)).Cast<StatusKind>().Select(k => (k, 2)).ToArray()), new[] { "鉄壁の構え" });
    }

    public static OrderedMap CardTexts(CardDef def)
    {
        var traits = def.AllTraits.Select(t => (object)new OrderedMap
        {
            ["Trait"] = t,
            ["TraitLine"] = Tables.Try(() => CoreText.TraitLine(t)),
            ["ConditionWord"] = Tables.Try(() => CoreText.ConditionWord(t)),
            ["EffectWord"] = Tables.Try(() => CoreText.EffectWord(t)),
            ["Condition"] = Tables.Try(() => CardFaceText.Condition(t, def)),
            ["Effect"] = Tables.Try(() => CardFaceText.Effect(t, def)),
            ["ConditionClause"] = Tables.Try(() => CardFaceText.ConditionClause(t, def)),
            ["EffectClause"] = Tables.Try(() => CardFaceText.EffectClause(t, def)),
        }).ToList();
        string[] rows = CardFaceText.TraitRows(def);
        return new OrderedMap
        {
            ["Def"] = def,
            ["FaceOf"] = Tables.Try(() => CoreText.FaceOf(def)),
            ["KindOf"] = CoreText.KindOf(def.Attribute),
            ["KindOfDeclared"] = CoreText.KindOf(def.Attributes),
            ["KindWord"] = CoreText.KindWord(CoreText.KindOf(def.Attribute)),
            ["SystemOf"] = Tables.Try(() => CoreText.SystemOf(def)),
            ["ReachHint"] = CoreText.ReachHint(def.Attributes, def.Face, def.Targets),
            ["ReachText"] = CoreText.ReachText(def.Attributes, def.Face, def.Targets),
            ["Describe"] = Tables.Try(() => CoreText.Describe(def.Face, def.Attributes)),
            ["ValueOf"] = CoreText.ValueOf(def.Face, def.Attributes),
            ["FacePower"] = CoreText.FacePower(def.Face),
            ["TraitLines"] = Tables.Try(() => CoreText.TraitLines(def)),
            ["StanceText"] = Tables.Try(() => CoreText.StanceText(def.Face.Stance)),
            ["Traits"] = traits,
            ["Values"] = CardFaceText.Values(def.Face, def.Attributes),
            ["TextLines"] = CardFaceText.TextLines(def.Face, def.Attributes),
            ["LastingLines"] = CardFaceText.LastingLines(def.Face),
            ["TraitRows"] = rows,
            ["HoverLine"] = Tables.Try(() => CardFaceText.HoverLine(def)),
            ["Detail"] = Tables.Try(() => CardFaceText.Detail(def)),
            ["KindLine"] = Tables.Try(() => CardFaceText.KindLine(def)),
            ["TermsOf"] = Tables.Try(() => CardFaceText.TermsOf(def)),
            ["TermMarks"] = Tables.Try(() => CardFaceText.TermMarks(def, rows[0], rows[1])),
            ["AttributeWord"] = Tables.Try(() => CardFaceText.AttributeWord(def.Attribute)),
            ["BoundRefusal"] = CoreText.BoundRefusal(def.Name),
        };
    }

    public static OrderedMap ActionTexts(EnemyActionDef action)
    {
        var kinds = new OrderedMap();
        foreach (OmenKind kind in Enum.GetValues(typeof(OmenKind)))
        {
            kinds.Add(kind, new OrderedMap
            {
                ["IconOf"] = CoreText.IconOf(kind, action),
                ["OmenStatusesOf"] = Tables.Try(() => CoreText.OmenStatusesOf(kind, action)),
                ["OmenValueOf"] = Tables.Try(() => CoreText.OmenValueOf(kind, action, null)),
            });
        }
        return new OrderedMap
        {
            ["Action"] = action,
            ["SystemOf"] = Tables.Try(() => CoreText.SystemOf(action)),
            ["Describe"] = Tables.Try(() => CoreText.Describe(action.Face, action.Attributes)),
            ["ValueOf"] = CoreText.ValueOf(action.Face, action.Attributes),
            ["FacePower"] = CoreText.FacePower(action.Face),
            ["TraitLine"] = Tables.Try(() => CoreText.TraitLine(action.Trait)),
            ["StanceText"] = Tables.Try(() => CoreText.StanceText(action.Face.Stance)),
            ["ByOmenKind"] = kinds,
        };
    }
}
