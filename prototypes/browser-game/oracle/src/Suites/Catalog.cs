// Suite 1 (catalog): every CardDef (and its mastery steps), every EnemyDef, the static tables.
using System.Reflection;
using BattleCore;
using BattleCore.Sim;

namespace Oracle;

public static class Tables
{
    /// <summary>A static class's public constants, static fields and static get-only properties, in declaration order.</summary>
    public static OrderedMap Static(Type t)
    {
        var m = new OrderedMap();
        foreach (var member in t.GetMembers(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).OrderBy(x => x.MetadataToken))
        {
            switch (member)
            {
                case FieldInfo f:
                    if (typeof(Delegate).IsAssignableFrom(f.FieldType)) continue;
                    m.Add(f.Name, f.IsLiteral ? f.GetRawConstantValue() : f.GetValue(null));
                    break;
                case PropertyInfo p when p.GetIndexParameters().Length == 0 && p.GetMethod != null:
                    if (typeof(Delegate).IsAssignableFrom(p.PropertyType)) continue;
                    m.Add(p.Name, p.GetValue(null));
                    break;
            }
        }
        return m;
    }

    /// <summary>A literal of a const enum field comes back as its underlying int; turn it back into the enum.</summary>
    public static object Try(Func<object> f)
    {
        try
        {
            return f();
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}

public static class CatalogSuite
{
    public static int Run(string outDir)
    {
        using var file = new SuiteFile(outDir, "catalog");

        foreach (var def in CardCatalog.All)
        {
            var input = new OrderedMap { ["Id"] = def.Id };
            file.Add("card:" + def.Id, input, () =>
            {
                var byId = CardCatalog.ById(def.Id);
                var profile = MasteryProfiles.Find(def.Id);
                var ranks = new List<object>();
                if (profile != null)
                {
                    for (int rank = 1; rank <= Mastery.TopRank; rank++) ranks.Add(Tables.Try(() => Mastery.Master(byId, rank, profile)));
                }
                var gapHolds = new List<object>();
                for (int gap = 0; gap <= 5; gap++) gapHolds.Add(Mastery.GapHolds(byId, gap, profile));
                return new OrderedMap
                {
                    ["Def"] = byId,
                    ["Profile"] = profile,
                    ["Ranks"] = ranks,
                    ["IsStanceCard"] = Cards.IsStanceCard(byId),
                    ["StanceStandsAlone"] = Cards.StanceStandsAlone(byId),
                    ["MultiHitCarriesNoFoeStatus"] = Cards.MultiHitCarriesNoFoeStatus(byId),
                    ["HasMovement"] = AttributeRule.HasMovement(byId.Face),
                    ["IsMovementCard"] = AttributeRule.IsMovementCard(byId.Attributes, byId.Face),
                    ["IsOpponentDirected"] = EnemyAi.IsOpponentDirected(byId.Attributes, byId.Face, byId.Targets),
                    ["GapHolds0to5"] = gapHolds,
                };
            });
        }

        foreach (var enemy in Enemies.All)
        {
            var input = new OrderedMap { ["Id"] = enemy.Id };
            file.Add("enemy:" + enemy.Id, input, () =>
            {
                var byId = Enemies.ById(enemy.Id);
                var validation = new OrderedMap();
                foreach (var kv in byId.Actions) validation.Add(kv.Key, Cards.ValidateEnemyAction(kv.Value));
                return new OrderedMap
                {
                    ["Def"] = byId,
                    ["ValidateEnemyAction"] = validation,
                };
            });
        }

        file.Add("table:Constants", new OrderedMap { ["Type"] = "Constants" }, () => Tables.Static(typeof(Constants)));
        file.Add("table:Columns", new OrderedMap { ["Type"] = "Columns" }, () =>
        {
            var m = Tables.Static(typeof(Columns));
            var costs = new List<object>();
            for (int c = 0; c <= 5; c++) costs.Add(Tables.Try(() => Columns.CostOf(c)));
            m.Add("CostOf0to5", costs);
            return m;
        });
        file.Add("table:Statuses", new OrderedMap { ["Type"] = "Statuses" }, () =>
        {
            var m = Tables.Static(typeof(Statuses));
            var kinds = new OrderedMap();
            foreach (StatusKind kind in Enum.GetValues(typeof(StatusKind)))
            {
                kinds.Add(kind, new OrderedMap
                {
                    ["DecayOf"] = Statuses.DecayOf(kind),
                    ["IsOwn"] = Tables.Try(() => Statuses.IsOwn(kind)),
                    ["IsBossOnly"] = Statuses.IsBossOnly(kind),
                    ["ToToken"] = kind.ToToken(),
                    ["ToLabel"] = kind.ToLabel(),
                });
            }
            m.Add("Kinds", kinds);
            return m;
        });
        file.Add("table:PrototypeDeck", new OrderedMap { ["Type"] = "PrototypeDeck" }, () => new OrderedMap
        {
            ["Copies"] = PrototypeDeck.Copies,
            ["Kinds"] = PrototypeDeck.Kinds.Select(d => d.Id).ToList(),
            ["Build"] = PrototypeDeck.Build().Select(c => c.InstanceId).ToList(),
            ["Validate"] = Cards.Validate(PrototypeDeck.Build()),
        });
        file.Add("table:MasteryProfiles", new OrderedMap { ["Type"] = "MasteryProfiles" }, () => new OrderedMap
        {
            ["All"] = MasteryProfiles.All,
            ["TopRank"] = Mastery.TopRank,
            ["MovementGuardByRank"] = Mastery.MovementGuardByRank,
            ["MovementExtraByRank"] = Mastery.MovementExtraByRank,
        });
        file.Add("table:Decks", new OrderedMap { ["Type"] = "Decks" }, () =>
        {
            var all = new List<SimDeck>(Decks.Archetypes) { Decks.CostOneOnly(), Decks.Initial(), Decks.InitialForty(), Decks.Plain() };
            all.AddRange(Decks.NoStanceTwins());
            return all.Select(d => new OrderedMap
            {
                ["Deck"] = d,
                ["Build"] = d.Build().Select(c => c.InstanceId).ToList(),
                ["Validate"] = Cards.Validate(d.Build()),
            }).ToList();
        });
        return file.Count;
    }
}
