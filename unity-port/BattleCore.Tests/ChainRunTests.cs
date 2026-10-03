using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// §12 連戦モード as a run (#52): the orders, HP / stamina / 瘴気 / layer carried across battles,
    /// the rest, the same order again after a clean sweep, and the chain-&lt;日時&gt;.json log.
    /// </summary>
    public class ChainRunTests
    {
        /// <summary>
        /// Fights one battle of the run to its end: a greedy hand (the first card it can play, at the
        /// nearest enemy it reaches), against enemies cut down to <paramref name="enemyHp"/> so the
        /// three battles end in a few turns with the enemies still getting their phases in.
        /// </summary>
        private static (BattleState State, List<BattleEvent> Events) Fight(BattleSetup setup, IRng rng, int enemyHp = 30)
        {
            var start = TurnLoop.Start(setup, rng);
            var state = start.State;
            for (int i = 0; i < state.Enemies.Count; i++)
            {
                state = state.WithEnemy(i, state.Enemies[i].Body with { Hp = Math.Min(enemyHp, state.Enemies[i].Body.Hp) });
            }
            var events = new List<BattleEvent>(start.Events);
            while (state.Result == GameResult.Ongoing && state.Turn < 60)
            {
                var step = TurnLoop.BeginPlayerTurn(state, rng);
                events.AddRange(step.Events);
                state = step.State;
                while (state.Result == GameResult.Ongoing)
                {
                    var play = state.Hand
                        .SelectMany(c => state.Living.Select(t => (Card: c, Target: t)))
                        .FirstOrDefault(p => TurnLoop.CanPlay(state, p.Card.InstanceId, p.Target) == PlayRefusal.None);
                    if (play.Card == null) break;
                    step = TurnLoop.PlayCard(state, play.Card.InstanceId, rng, play.Target);
                    events.AddRange(step.Events);
                    state = step.State;
                }
                if (state.Result != GameResult.Ongoing) break;
                step = TurnLoop.EndTurn(state, rng);
                events.AddRange(step.Events);
                state = step.State;
            }
            return (state, events);
        }

        /// <summary>A battle on this setup, ended at once with this result, HP and stamina.</summary>
        private static BattleState Ended(BattleSetup setup, GameResult result, int hp, int stamina)
        {
            var state = TurnLoop.Start(setup, new SeededRng(1)).State;
            return state with { Player = state.Player with { Hp = hp, Stamina = stamina }, Result = result };
        }

        private static ChainRun FightNext(ChainRun run, IRng rng, out BattleSetup setup, out BattleState finished)
        {
            setup = run.NextSetup(PrototypeDeck.Build());
            var (state, events) = Fight(setup, rng);
            finished = state;
            return run.Finish(state, events);
        }

        // ---- Orders ----

        [Test]
        public void TheDefaultOrder_IsSection12sThree_AndNineMayBePicked()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ChainOrder.Default.Select(b => b.ToString()), Is.EqualTo(new[] { "polearm_warped@1", "shadow_hound@2", "armored_warden@4" }));
                Assert.That(ChainOrder.Default, Has.Count.EqualTo(Constants.ChainBattlesDefault));
                Assert.That(ChainOrder.Nine, Has.Count.EqualTo(9));
                Assert.That(ChainOrder.Nine[2].EnemyIds, Is.EqualTo(new[] { "polearm_warped", "crossbow_hunter" }), "roster §9's third is the pair");
                Assert.That(ChainOrder.Nine.Select(b => b.Layer), Is.Ordered, "the layer only goes down");
                Assert.That(ChainOrder.Nine.Select(b => b.FieldCells), Is.EqualTo(new[] { 6, 6, 7, 6, 6, 7, 6, 6, 7 }), "7 for a size-2 enemy and for the pair");
                Assert.That(ChainOrder.Nine.SelectMany(b => b.Defs), Has.None.Null, "every id is one the core knows");
            });
        }

        [Test]
        public void TheChainSwitch_OverridesTheOrder()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ChainOrder.FromArgs(new string[0]), Is.SameAs(ChainOrder.Default));
                Assert.That(ChainOrder.FromArgs(new[] { "-batchmode", "-chain", "9" }), Is.SameAs(ChainOrder.Nine));
                Assert.That(ChainOrder.FromArgs(new[] { "-chain", "nine" }), Is.SameAs(ChainOrder.Nine));

                var custom = ChainOrder.FromArgs(new[] { "-chain", "shadow_hound, mist_archer@5,polearm_warped+crossbow_hunter" });
                Assert.That(custom.Select(b => b.ToString()), Is.EqualTo(new[] { "shadow_hound@1", "mist_archer@5", "polearm_warped+crossbow_hunter@5" }),
                    "a battle with no layer stays on the one before");

                Assert.That(() => ChainOrder.Parse("polearm_warped,nobody"), Throws.InstanceOf<KeyNotFoundException>());
                Assert.That(() => ChainOrder.Parse("polearm_warped@8"), Throws.InstanceOf<ArgumentOutOfRangeException>());
                Assert.That(() => ChainOrder.Parse("polearm_warped,,shadow_hound"), Throws.ArgumentException);
                Assert.That(() => ChainOrder.Parse("polearm_warped+shadow_hound+mist_archer+pack_alpha"),
                    Throws.ArgumentException, "ENEMIES_MAX 3");
                Assert.That(() => ChainOrder.FromArgs(new[] { "-chain" }), Throws.ArgumentException);
                Assert.That(() => ChainOrder.Parse("armored_warden+abyss_angler"), Throws.ArgumentException,
                    "two size-2 enemies need 9 cells, wider than FIELD_CELLS_MAX 8");
            });
        }

        // ---- What carries ----

        [Test]
        public void AThreeBattleChain_CarriesHpAndStamina_IntoEveryNextBattle()
        {
            var rng = new SeededRng(5);
            var run = ChainRun.Start();
            var finishes = new List<BattleState>();
            var setups = new List<BattleSetup>();
            for (int i = 0; i < 3; i++)
            {
                run = FightNext(run, rng, out var setup, out var finished);
                setups.Add(setup);
                finishes.Add(finished);
                Assert.That(finished.Result, Is.EqualTo(GameResult.Won), $"battle {i + 1} is won");
                if (i < 2) run = run.GoOn(rest: false);
            }

            Assert.Multiple(() =>
            {
                Assert.That(setups[0].StartHp, Is.EqualTo(Constants.PlayerMaxHp));
                for (int i = 1; i < 3; i++)
                {
                    Assert.That(setups[i].StartHp, Is.EqualTo(finishes[i - 1].Player.Hp), $"battle {i + 1} starts on the HP battle {i} left");
                    Assert.That(setups[i].StartStamina, Is.EqualTo(Math.Min(setups[i].PlayerMaxStamina, finishes[i - 1].Player.Stamina)),
                        $"battle {i + 1} starts on the stamina battle {i} left");
                }
                Assert.That(finishes.Take(2).Select(f => f.Player.Hp), Has.Some.LessThan(Constants.PlayerMaxHp), "HP was lost on the way, so carrying it shows");
                Assert.That(finishes.Take(2).Select(f => f.Player.Stamina), Has.Some.LessThan(Constants.BaseMaxStamina), "and stamina was spent");
                Assert.That(run.Stage, Is.EqualTo(ChainStage.Over));
                Assert.That(run.AllWon, Is.True);
                Assert.That(run.Log.Select(e => e.StartHp), Is.EqualTo(setups.Select(s => s.StartHp)));
            });
        }

        [Test]
        public void TheRest_ChangesTheNextBattlesStartingHp()
        {
            // The first battle is won on HP 20 and stamina 4, set outright so the test does not lean on a seed.
            var run = ChainRun.Start();
            var first = Ended(run.NextSetup(PrototypeDeck.Build()), GameResult.Won, hp: 20, stamina: 4);
            run = run.Finish(first, Array.Empty<BattleEvent>());
            Assert.That(run.Stage, Is.EqualTo(ChainStage.BetweenBattles));

            var rested = run.GoOn(rest: true).NextSetup(PrototypeDeck.Build());
            var pressed = run.GoOn(rest: false).NextSetup(PrototypeDeck.Build());

            Assert.Multiple(() =>
            {
                Assert.That(pressed.StartHp, Is.EqualTo(20));
                Assert.That(rested.StartHp, Is.EqualTo(35), "HP 30% of 50");
                Assert.That(rested.StartHp, Is.GreaterThan(pressed.StartHp));
                Assert.That(rested.StartStamina, Is.EqualTo(rested.PlayerMaxStamina), "stamina full");
                Assert.That(pressed.StartStamina, Is.EqualTo(4));
            });
        }

        [Test]
        public void Miasma_GrowsByDensityTimesThreeABattle_AndTakesMaxStaminaAsItGoes()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Chain.AccumulateMiasma(0, 1), Is.EqualTo(3), "濃度 1 なら 3%");
                Assert.That(Chain.AccumulateMiasma(95, 7), Is.EqualTo(100));
                Assert.That(Chain.MaxStaminaAt(19), Is.EqualTo(10));
                Assert.That(Chain.MaxStaminaAt(20), Is.EqualTo(9));
                Assert.That(Chain.MaxStaminaAt(80), Is.EqualTo(6), "§13: max stamina 6 at 80%");
                Assert.That(Chain.MaxStaminaAt(100), Is.EqualTo(6), "−4 at most");
            });

            // The nine, walked without fighting: the gauge each battle is fought under, and its max stamina.
            var gauges = new List<int>();
            int gauge = 0;
            foreach (var battle in ChainOrder.Nine)
            {
                gauge = Chain.AccumulateMiasma(gauge, battle.Density);
                gauges.Add(gauge);
            }
            Assert.That(gauges, Is.EqualTo(new[] { 3, 6, 12, 21, 33, 45, 60, 75, 90 }));
            Assert.That(gauges.Select(Chain.MaxStaminaAt), Is.EqualTo(new[] { 10, 10, 10, 9, 9, 8, 7, 7, 6 }));

            // In a run: the gauge and the layer carry, and the rest adds none.
            var run = ChainRun.Start(ChainOrder.Parse("polearm_warped@4,shadow_hound@6"));
            Assert.That(run.NextSetup(PrototypeDeck.Build()).PlayerMaxStamina, Is.EqualTo(10), "9% before the first");
            run = FightNext(run, new SeededRng(5), out _, out _);
            Assert.That(run.MiasmaPercent, Is.EqualTo(9));
            Assert.That(run.MiasmaForNext, Is.EqualTo(24), "between battles it reads the battle to come, not the one just won");
            run = run.GoOn(rest: true);
            Assert.Multiple(() =>
            {
                Assert.That(run.MiasmaPercent, Is.EqualTo(9), "階層間の休憩 does not accumulate");
                Assert.That(run.Layer, Is.EqualTo(6));
                Assert.That(run.MiasmaForNext, Is.EqualTo(24));
                Assert.That(run.NextSetup(PrototypeDeck.Build()).PlayerMaxStamina, Is.EqualTo(9));
            });
        }

        [Test]
        public void AGaugeReaching100_IsMiasmaDeath_AndEndsThePassLost()
        {
            // Five battles on layer 7 (濃度 7): 21 / 42 / 63 / 84, and the fifth would bring the gauge to 100%.
            var run = ChainRun.Start(ChainOrder.Parse("polearm_warped@7,polearm_warped,polearm_warped,polearm_warped,polearm_warped"));
            var gauges = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                if (run.Stage == ChainStage.BetweenBattles) run = run.GoOn(rest: true);
                gauges.Add(run.MiasmaForNext);
                run = run.Finish(Ended(run.NextSetup(PrototypeDeck.Build()), GameResult.Won, hp: 40, stamina: 5), Array.Empty<BattleEvent>());
            }
            var root = (Dictionary<string, object?>)MiniJson.Parse(ChainLog.ToJson(run, new DateTime(2026, 10, 4)))!;

            Assert.Multiple(() =>
            {
                Assert.That(gauges, Is.EqualTo(new[] { 21, 42, 63, 84 }));
                Assert.That(run.Stage, Is.EqualTo(ChainStage.Over), "no rest is offered: it would not lower the gauge");
                Assert.That(run.DiedOfMiasma, Is.True, "100% is 瘴気死 (concept-v3 §6)");
                Assert.That(run.AllWon, Is.False, "the pass ends lost");
                Assert.That(run.MiasmaPercent, Is.EqualTo(Chain.MiasmaMax));
                Assert.That(run.ThisPass, Has.Count.EqualTo(4), "the fifth battle is never fought");
                Assert.That(run.ThisPass.Select(e => e.Tally.Result), Has.All.EqualTo(GameResult.Won));
                Assert.That(() => run.GoOn(rest: true), Throws.InvalidOperationException);
                Assert.That(() => run.NextSetup(PrototypeDeck.Build()), Throws.InvalidOperationException);
                Assert.That(root["result"], Is.EqualTo("lost"));
                Assert.That(root["miasmaDeath"], Is.EqualTo(true));
                Assert.That(root["reachedBattle"], Is.EqualTo(4.0));
            });

            var again = run.Again();
            Assert.Multiple(() =>
            {
                Assert.That(again.DiedOfMiasma, Is.False);
                Assert.That(again.MiasmaPercent, Is.EqualTo(0));
                Assert.That(again.MiasmaForNext, Is.EqualTo(21));
            });
        }

        [Test]
        public void ALoss_EndsTheChain_AndACleanSweepRunsTheSameOrderAgain()
        {
            var run = ChainRun.Start(ChainOrder.Parse("polearm_warped,shadow_hound"));
            var setup = run.NextSetup(PrototypeDeck.Build());
            var lost = TurnLoop.Start(setup, new SeededRng(1)).State;
            lost = lost with { Player = lost.Player with { Hp = 0 }, Result = GameResult.Lost };
            var afterLoss = run.Finish(lost, Array.Empty<BattleEvent>());
            Assert.Multiple(() =>
            {
                Assert.That(afterLoss.Stage, Is.EqualTo(ChainStage.Over));
                Assert.That(afterLoss.AllWon, Is.False);
                Assert.That(() => afterLoss.GoOn(rest: true), Throws.InvalidOperationException);
            });

            var rng = new SeededRng(5);
            run = FightNext(run, rng, out _, out _).GoOn(rest: false);
            run = FightNext(run, rng, out _, out _);
            Assert.That(run.AllWon, Is.True);

            var again = run.Again();
            Assert.Multiple(() =>
            {
                Assert.That(again.Pass, Is.EqualTo(2));
                Assert.That(again.Index, Is.EqualTo(0));
                Assert.That(again.Order, Is.SameAs(run.Order));
                Assert.That(again.Stage, Is.EqualTo(ChainStage.Ready));
                Assert.That(again.MiasmaPercent, Is.EqualTo(0));
                Assert.That(again.NextSetup(PrototypeDeck.Build()).StartHp, Is.EqualTo(Constants.PlayerMaxHp), "a new pass starts fresh");
                Assert.That(again.Log, Has.Count.EqualTo(2), "the log keeps the first pass");
                Assert.That(again.ThisPass, Is.Empty);
            });
        }

        // ---- The log ----

        [Test]
        public void TheLog_IsJson_WithSection12sItems()
        {
            var rng = new SeededRng(5);
            var run = ChainRun.Start();
            run = FightNext(run, rng, out _, out _).GoOn(rest: true);
            run = FightNext(run, rng, out _, out _).GoOn(rest: false);
            run = FightNext(run, rng, out _, out _);
            var at = new DateTime(2026, 10, 4, 15, 30, 5);

            var root = (Dictionary<string, object?>)MiniJson.Parse(ChainLog.ToJson(run, at))!;
            var battles = ((List<object?>)root["battles"]!).Cast<Dictionary<string, object?>>().ToList();
            var total = (Dictionary<string, object?>)root["total"]!;

            Assert.Multiple(() =>
            {
                Assert.That(ChainLog.FileName(at), Is.EqualTo("chain-20261004-153005.json"));
                Assert.That(root["format"], Is.EqualTo(ChainLog.Format));
                Assert.That(root["writtenAt"], Is.EqualTo("2026-10-04T15:30:05"));
                Assert.That(root["result"], Is.EqualTo("won"));
                Assert.That(root["miasmaDeath"], Is.EqualTo(false));
                Assert.That(root["reachedBattle"], Is.EqualTo(3.0), "何戦目");
                Assert.That(((List<object?>)root["order"]!).Count, Is.EqualTo(3));
                Assert.That(battles, Has.Count.EqualTo(3));
                Assert.That(battles.Select(b => b["battle"]), Is.EqualTo(new object[] { 1.0, 2.0, 3.0 }));
                Assert.That(battles.Select(b => b["rested"]), Is.EqualTo(new object[] { false, true, false }));
                for (int i = 0; i < 3; i++)
                {
                    var tally = run.Log[i].Tally;
                    var b = battles[i];
                    Assert.That(b["hpLeft"], Is.EqualTo((double)tally.HpLeft), "残 HP");
                    Assert.That(b["turns"], Is.EqualTo((double)tally.Turns));
                    Assert.That(b["traitsFired"], Is.EqualTo((double)tally.TraitsFired), "特性の発動回数");
                    var attributes = (Dictionary<string, object?>)b["attributes"]!;
                    Assert.That(attributes.Keys, Is.EquivalentTo(new[] { "attack", "guard", "skill", "stance" }), "属性の内訳, all four every time");
                    Assert.That(attributes["attack"], Is.EqualTo((double)tally.CountOf(BattleAttribute.Attack)));
                    Assert.That(b["miasmaPercent"], Is.EqualTo((double)run.Log[i].MiasmaPercent));
                }
                double average = run.Log.Average(e => e.Tally.Turns);
                Assert.That((double)total["averageTurns"]!, Is.EqualTo(average).Within(0.006), "平均ターン数");
                Assert.That(total["battles"], Is.EqualTo(3.0));
                Assert.That(total["traitsFired"], Is.EqualTo((double)run.Log.Sum(e => e.Tally.TraitsFired)));
            });
        }

        [Test]
        public void TheLog_IsWrittenToChainDatetimeJson()
        {
            var run = FightNext(ChainRun.Start(), new SeededRng(5), out _, out _);
            string dir = Path.Combine(Path.GetTempPath(), "chain-log-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                string path = ChainLog.Write(dir, run, new DateTime(2026, 10, 4, 9, 0, 0));
                Assert.That(Path.GetFileName(path), Is.EqualTo("chain-20261004-090000.json"));
                string text = File.ReadAllText(path, Encoding.UTF8);
                Assert.That(text, Is.EqualTo(ChainLog.ToJson(run, new DateTime(2026, 10, 4, 9, 0, 0))));
                Assert.That(MiniJson.Parse(text), Is.InstanceOf<Dictionary<string, object?>>());
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
        }

        /// <summary>
        /// Just enough JSON to read the log back (objects, arrays, strings, numbers as double, true /
        /// false / null), strict about what it is given. The tests also run inside Unity, which has
        /// no System.Text.Json.
        /// </summary>
        private sealed class MiniJson
        {
            private readonly string _s;
            private int _i;

            private MiniJson(string s) { _s = s; }

            public static object? Parse(string text)
            {
                var p = new MiniJson(text);
                var value = p.Value();
                p.Space();
                if (p._i != text.Length) throw new FormatException($"Trailing text at {p._i}.");
                return value;
            }

            private void Space()
            {
                while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
            }

            private char Peek()
            {
                Space();
                if (_i >= _s.Length) throw new FormatException("Unexpected end.");
                return _s[_i];
            }

            private void Expect(char c)
            {
                if (Peek() != c) throw new FormatException($"Expected '{c}' at {_i}, found '{_s[_i]}'.");
                _i++;
            }

            private object? Value()
            {
                char c = Peek();
                if (c == '{') return Obj();
                if (c == '[') return Arr();
                if (c == '"') return Str();
                if (Word("true")) return true;
                if (Word("false")) return false;
                if (Word("null")) return null;
                return Number();
            }

            private bool Word(string w)
            {
                if (string.CompareOrdinal(_s, _i, w, 0, w.Length) != 0) return false;
                _i += w.Length;
                return true;
            }

            private Dictionary<string, object?> Obj()
            {
                var o = new Dictionary<string, object?>();
                Expect('{');
                if (Peek() == '}') { _i++; return o; }
                while (true)
                {
                    string key = Str();
                    if (o.ContainsKey(key)) throw new FormatException($"Duplicate key \"{key}\".");
                    Expect(':');
                    o[key] = Value();
                    if (Peek() == ',') { _i++; continue; }
                    Expect('}');
                    return o;
                }
            }

            private List<object?> Arr()
            {
                var a = new List<object?>();
                Expect('[');
                if (Peek() == ']') { _i++; return a; }
                while (true)
                {
                    a.Add(Value());
                    if (Peek() == ',') { _i++; continue; }
                    Expect(']');
                    return a;
                }
            }

            private string Str()
            {
                Expect('"');
                var sb = new StringBuilder();
                while (true)
                {
                    if (_i >= _s.Length) throw new FormatException("Unterminated string.");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c < 0x20) throw new FormatException("Raw control character in a string.");
                    if (c != '\\') { sb.Append(c); continue; }
                    char e = _s[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _i += 4;
                            break;
                        default: throw new FormatException($"Bad escape \\{e}.");
                    }
                }
            }

            private double Number()
            {
                int start = _i;
                if (_s[_i] == '-') _i++;
                while (_i < _s.Length && (char.IsDigit(_s[_i]) || _s[_i] == '.' || _s[_i] == 'e' || _s[_i] == 'E' || _s[_i] == '+' || _s[_i] == '-')) _i++;
                if (start == _i) throw new FormatException($"Unexpected '{_s[_i]}' at {_i}.");
                return double.Parse(_s.Substring(start, _i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }
    }
}
