namespace BattleCore.Sim;

/// <summary>
/// One deck the bench fights with. Rows are card ids of <see cref="CardCatalog"/> and their copies,
/// so the bench measures whatever numbers the catalog holds today (swordsman_cards_v4 v4.5 at the
/// time of writing). Basis says why the deck is built this way.
/// </summary>
public sealed record SimDeck(string Id, string Name, IReadOnlyList<(string CardId, int Copies)> Rows, string Basis)
{
    public int Count => Rows.Sum(r => r.Copies);

    /// <summary>The deck as card instances ("thrust-0", "thrust-1", …), in row order. TurnLoop.Start shuffles it.</summary>
    public List<CardInstance> Build()
    {
        var deck = new List<CardInstance>(Count);
        foreach (var (cardId, copies) in Rows)
        {
            var def = CardCatalog.ById(cardId);
            for (int copy = 0; copy < copies; copy++) deck.Add(new CardInstance($"{cardId}-{copy}", def));
        }
        return deck;
    }

    /// <summary>Whether any row is a stance card (§4: it goes to the exile pile when played).</summary>
    public bool HasStance => Rows.Any(r => Cards.IsStanceCard(CardCatalog.ById(r.CardId)));
}

/// <summary>
/// The decks of battle_core_v4 §13. The six deck types are §19.8's (S17 replaced the old five):
/// 溜め放ち / 振り子 / 研ぎ / 血路 / 一型 / 背水. Each is 20 cards (the §8 floor) with at most 3 of a
/// kind, so <see cref="Cards.Validate"/> passes. The greedy player does not plan a type's two-turn
/// rhythm on purpose; the type is what the deck holds, and the bench reads how that deck fares.
///
/// Roles are v4.5's (swordsman_cards_v4, #333 / #363): the fourteen stance cards are stance only
/// (they carry nothing but the stance face), and 観察 / 間合い切り / 覚悟 count as 防御 (§2.1's fold:
/// they carry a Guard).
/// </summary>
public static class Decks
{
    /// <summary>§13: the deck types, in §19.8's order.</summary>
    public static IReadOnlyList<SimDeck> Archetypes { get; } = new[]
    {
        // §19.8-1 溜め放ち: one card and 構え on the charge turn, then 3〜4 cards that fire 締め / 手薄 /
        // 連打 / 連動 together. 呼吸を整える (温存: 次ターン回復 +1) is the charge; 三日月斬り / 間合い切り are
        // 締め; 観察 is 手薄; 柄頭打ち / 糸打ち are 連打; 受け太刀 is 連動(攻撃); 小手打ち and 足運び are the
        // cheap first plays that start the chain. Since v4.5 観察 (Guard 3 + draw) and 間合い切り
        // (Guard 4 + back 1) are 防御 too, so the charge turn has cheap guards besides 呼吸を整える.
        new SimDeck("charge_release", "溜め放ち", new[]
        {
            ("brace", 3), ("wrist_cut", 3), ("crescent_cut", 3), ("pommel_strike", 2), ("line_lash", 2),
            ("observe", 2), ("riposte_guard", 2), ("footwork", 2), ("break_off", 1),
        }, "§19.8-1: 温存で溜め、締め / 手薄 / 連打 / 連動 で放つ"),

        // §19.8-2 振り子: guard far, strike close. The close attacks read 間合い 0 (袈裟斬り / 牙の突進 /
        // 体当たり); 伸び突き / 猪突猛進 pay at 2 or more; the movement cards (S10) carry the swing both
        // ways, and 水の構え (stance only: +2 recovery on turns that start at 2 or more) pays for the
        // turns that start far.
        new SimDeck("pendulum", "振り子", new[]
        {
            ("kesa_cut", 3), ("fang_rush", 2), ("body_check", 2), ("reach_thrust", 2), ("boar_rush", 1),
            ("back_leap", 3), ("slide_step", 2), ("footwork", 2), ("step_in_guard", 1), ("step_out_guard", 1),
            ("water_stance", 1),
        }, "§19.8-2: 溜めで離れ、放ちで詰める。間合いの境目の札と移動の札"),

        // §19.8-3 研ぎ: stack 強化 / 集中 / 見切り on the charge turn and spend them on column-3 attacks
        // (集中 moves 突き / 横薙ぎ to column 4). 集中 / 鬨の声 / 覚悟 / 群れの咆哮 empower (覚悟 is 防御
        // since v4.5: Guard 3 with its 強化), 静の受け focuses, 返しの構え parries (防御 + スキル, not a
        // stance card despite its name, so the type holds no stance). 足運び / 摺り足 close in: a deck
        // with no step forward cannot reach an enemy that keeps its distance (大黒蛇 セルク stays at 3
        // and the battle stalls).
        new SimDeck("whetstone", "研ぎ", new[]
        {
            ("focus", 3), ("war_cry", 2), ("resolve", 2), ("pack_howl", 2), ("calm_guard", 2),
            ("riposte_stance", 1), ("thrust", 3), ("side_sweep", 2), ("footwork", 2), ("slide_step", 1),
        }, "§19.8-3: 強化 / 集中 / 見切りを積み、列 3 の攻撃で使い切る"),

        // §19.8-4 血路: 出血 and 脆化, then the cards that read them (§19.8 names 裂き斬り / 棘斬り / 血の舞 /
        // 槍衾). 裂き斬り / 棘斬り / 血の舞 put 出血 on; 裂き斬り and 気迫 read 相手の状態(出血). The two
        // stances are stance only since v4.4: 槍衾 bleeds whoever hits the player (and its +3 Guard reads
        // 出血 when it is put down), 狼の構え adds +3 to attacks on a bleeding foe. 棘斬り reads 崩し後, so
        // 急所突き breaks for it. 石礫 / 狩人の印 put 脆化 on; 足運び closes in.
        new SimDeck("blood_road", "血路", new[]
        {
            ("rend", 3), ("thorn_cut", 3), ("blood_dance", 3), ("spear_wall", 1), ("spirit_roar", 2),
            ("wolf_stance", 1), ("stone_throw", 2), ("hunter_mark", 1), ("vital_thrust", 2), ("footwork", 2),
        }, "§19.8-4: 出血と脆化を積み、相手の状態(出血) の札を連ねる"),

        // §19.8-5 一型: a stance on turn 1 and the turn-start / conditional bonus from then on, the rest
        // column-1 cards played 2〜3 a turn without charging. 岩の構え gives 3 Guard each turn start,
        // 流れの構え pays after a move (足運び / 間合い切り), and 鉄壁の構え (v4.5) takes 1 off one 防御
        // card each turn. 鉄壁 only pays when a 防御 card is in the hand, and the v4.4 deck held four,
        // all of cost 1 (返しの構え / 間合い切り), so it saved 1 on about four turns in five. 受け太刀 × 2
        // takes the place of 峰打ち × 2 (whose 鈍足 condition nothing in the deck put on): a column-2
        // guard that 鉄壁 brings to 1 and that grows +3 after the column-1 attacks the turn opens with.
        // Six 防御 cards of 20 put one in about 92% of the hands.
        new SimDeck("one_stance", "一型", new[]
        {
            ("rock_stance", 2), ("iron_wall", 1), ("flow_stance", 1), ("wrist_cut", 3), ("riposte_guard", 2),
            ("pierce", 2), ("vital_thrust", 3), ("riposte_stance", 2), ("footwork", 2), ("break_off", 2),
        }, "§19.8-5: 1 ターン目にスタンス、残りは列 1 の安い札。鉄壁の構えの割引が効くよう防御の札を 6 枚"),

        // §19.8-6 背水: run stamina dry and strike from 2 or more with the cards that grow there and under
        // 死力 — 背水の陣 / 伸び突き / 覚悟 (§19.8's 背水の型 is not in the catalog; 覚悟 is 防御 since v4.5
        // and gives 再生 under 死力). 投げ刃 / 探り突き / 石礫 strike from afar; 後ろ跳び / 間合い切り /
        // 跳び退り keep the gap and one 足運び steps back into 1〜2 when the enemy keeps to 3 or more.
        new SimDeck("back_to_water", "背水", new[]
        {
            ("last_stand", 3), ("reach_thrust", 3), ("resolve", 2), ("throw_blade", 2), ("probe_thrust", 2),
            ("back_leap", 2), ("break_off", 2), ("leap_back", 1), ("footwork", 1), ("stone_throw", 2),
        }, "§19.8-6: 遠間 × 死力 で伸びる札。後ろへ動く札で間合いを保つ"),
    };

    /// <summary>
    /// §13 「コスト 1 だけで組んだデッキ」 (§18.4-1, swordsman_cards_v4 §5-1 「コスト 1 の 25 種だけで組んだ
    /// デッキ」): every catalog card whose cost is 1, one copy each, in canon order. Topped up with a
    /// second copy in canon order if the catalog had fewer than 20, cut at 40 if it had more.
    /// </summary>
    public static SimDeck CostOneOnly()
    {
        var ones = CardCatalog.All.Where(c => c.Cost == 1).Select(c => c.Id).ToList();
        var rows = ones.Select(id => (CardId: id, Copies: 1)).ToList();
        for (int i = 0; rows.Sum(r => r.Copies) < Constants.DeckMin && ones.Count > 0; i = (i + 1) % ones.Count)
        {
            rows[i] = (rows[i].CardId, rows[i].Copies + 1);
        }
        while (rows.Sum(r => r.Copies) > Constants.DeckMax) rows.RemoveAt(rows.Count - 1);
        return new SimDeck("cost_one", "コスト 1 だけ", rows, $"コスト 1 の札 {ones.Count} 種を 1 枚ずつ（§18.4-1）");
    }

    /// <summary>The deck the demo starts with (<see cref="PrototypeDeck"/>, ten kinds × 2).</summary>
    public static SimDeck Initial() =>
        new SimDeck("initial", "初期デッキ", PrototypeDeck.Kinds.Select(d => (CardId: d.Id, Copies: PrototypeDeck.Copies)).ToList(),
            "試運転のデッキ（PrototypeDeck、10 種 × 2）");

    /// <summary>
    /// swordsman_cards_v4 §2's initial forty, #1〜#40, named one by one so that a change to the
    /// catalog's order cannot change the deck. battle_core_v4 §14-4 reasons about this deck
    /// (「初期 40 種を 1 枚ずつ入れたデッキ」, ten of them moving), so §13 基準 11 is judged on it.
    /// </summary>
    public static readonly IReadOnlyList<string> InitialFortyIds = new[]
    {
        "thrust", "kesa_cut", "overhead", "wrist_cut", "side_sweep",
        "flat_strike", "probe_thrust", "pierce", "throw_blade", "reach_thrust",
        "brace", "iron_block", "low_guard", "riposte_guard", "deep_breath",
        "water_stance", "rock_stance", "flow_stance", "first_aid", "spirit_roar",
        "focus", "observe", "war_cry", "second_wind", "footwork",
        "back_leap", "slide_step", "break_off", "lunge", "feint",
        "boar_rush", "rend", "body_check", "stone_throw", "parry_cut",
        "guard_thrust", "step_in_guard", "step_out_guard", "iron_wall", "twist_away",
    };

    /// <summary>The initial forty × 1 (<see cref="InitialFortyIds"/>): §13 基準 11's deck.</summary>
    public static SimDeck InitialForty() =>
        new SimDeck("initial_forty", "初期 40 種 × 1", InitialFortyIds.Select(id => (CardId: id, Copies: 1)).ToList(),
            "swordsman_cards_v4 §2 の #1〜#40 を 1 枚ずつ（battle_core_v4 §14-4 のデッキ）");

    /// <summary>
    /// swordsman_cards_v4 §5-5: every 素直 card (no trait, not a stance) × 3. A reference row, not a
    /// §13 criterion.
    /// </summary>
    public static SimDeck Plain()
    {
        var rows = CardCatalog.All
            .Where(c => c.AllTraits.Count == 0 && !Cards.IsStanceCard(c))
            .Select(c => (CardId: c.Id, Copies: Constants.CopiesMax))
            .ToList();
        while (rows.Sum(r => r.Copies) > Constants.DeckMax) rows.RemoveAt(rows.Count - 1);
        return new SimDeck("plain", "素直だけ", rows, "特性の無い札 × 3（swordsman_cards_v4 §5-5）");
    }

    /// <summary>
    /// The plain column-3 cards a stance card is swapped for in a no-stance twin, in this order:
    /// the canon's two plain attacks and two plain guards (swordsman_cards_v4 §2 #1 / #5 / #12 / #15).
    /// </summary>
    public static readonly IReadOnlyList<string> StanceSubstitutes = new[] { "thrust", "iron_block", "side_sweep", "deep_breath" };

    /// <summary>
    /// §13 「スタンス無しデッキ」: the deck with every stance card swapped for a plain card
    /// (<see cref="StanceSubstitutes"/>, filling each up to 3 before the next). Null when the deck holds
    /// no stance card, since the twin would be the deck itself.
    /// </summary>
    public static SimDeck? WithoutStances(SimDeck deck)
    {
        if (!deck.HasStance) return null;
        var rows = deck.Rows.Where(r => !Cards.IsStanceCard(CardCatalog.ById(r.CardId))).ToList();
        int missing = deck.Count - rows.Sum(r => r.Copies);
        foreach (var sub in StanceSubstitutes)
        {
            if (missing == 0) break;
            int at = rows.FindIndex(r => r.CardId == sub);
            int held = at < 0 ? 0 : rows[at].Copies;
            int add = Math.Min(missing, Constants.CopiesMax - held);
            if (add <= 0) continue;
            if (at < 0) rows.Add((sub, add));
            else rows[at] = (sub, held + add);
            missing -= add;
        }
        if (missing > 0) throw new InvalidOperationException($"No room to swap the stance cards out of \"{deck.Id}\".");
        return new SimDeck(deck.Id + "_no_stance", deck.Name + "（スタンス無し）", rows, "スタンスの札を素直な札へ差し替え");
    }

    /// <summary>The no-stance twins of the archetypes that hold a stance card.</summary>
    public static IReadOnlyList<SimDeck> NoStanceTwins() =>
        Archetypes.Select(WithoutStances).Where(d => d != null).Select(d => d!).ToList();
}
