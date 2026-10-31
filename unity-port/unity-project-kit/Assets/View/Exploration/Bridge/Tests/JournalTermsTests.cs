// #650: the journal's page of terms and statuses (Journal/Script/JournalTerms.cs) copies, numbers and
// all, the words the battle builds from BattleCore Constants: the card glossary
// (CardFaceText.Glossary) and the status panel (StatusChipText.Effect / Decay). Nothing else ties the
// two, so a Constants change would refresh the battle's text and leave the journal's page stale. This
// file is the one place that sees both the journal and BattleCore. Runs under `dotnet test`
// (Exploration.Bridge.Tests.csproj) and in Unity's EditMode runner.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using Depiction;
using Depiction.Bridge;
using Journal;
using NUnit.Framework;

namespace Exploration.Bridge.Tests
{
    public class JournalTermsTests
    {
        /// <summary>
        /// The battle glossary's words the journal does not hold: battle-visual-v1 §7.5 has no row for
        /// them yet. When #459 decides whether they join the journal, change this list.
        /// </summary>
        private static readonly string[] BattleOnlyTerms = { "死力", "締め", "崩し後" };

        /// <summary>
        /// 出血's 効き方 reads "相手のターン開始に" in the journal (§7.3) and "ターン開始に" in the battle.
        /// Which one stands is for design and battle to decide; until then the words are left out of
        /// the comparison on both sides.
        /// </summary>
        private const string BleedOpponentPrefix = "相手の";

        /// <summary>The battle's 効き方 says what a word does at the stacks held now; the journal does not.</summary>
        private const string NowClause = "（いまは";

        private const string EffectHead = "効き方: ";

        private const string DecayHead = "減り方: ";

        // ---- terms ----

        [Test]
        public void EveryJournalTerm_ReadsAsTheBattleGlossarySays()
        {
            Assert.Multiple(() =>
            {
                foreach (JournalRow row in JournalTerms.Terms)
                {
                    string meaning;
                    Assert.That(CardFaceText.Glossary.TryGetValue(row.Label, out meaning), Is.True,
                        "the journal's term " + row.Label + " has no entry in CardFaceText.Glossary");
                    if (meaning == null) continue;
                    Assert.That(row.Text, Is.EqualTo(meaning), "the journal's term " + row.Label);
                }
            });
        }

        [Test]
        public void TheBattleGlossary_HoldsOnlyTheThreeBattleOnlyTermsBeyondTheJournal()
        {
            var journal = new HashSet<string>(JournalTerms.Terms.Select(row => row.Label));
            var battleOnly = CardFaceText.Glossary.Keys.Where(word => !journal.Contains(word)).ToList();
            Assert.That(battleOnly, Is.EquivalentTo(BattleOnlyTerms));
        }

        // ---- statuses ----

        [Test]
        public void EveryJournalStatus_ReadsAsTheBattleStatusPanelSays()
        {
            Assert.Multiple(() =>
            {
                foreach (JournalRow row in JournalTerms.Statuses)
                {
                    string name = NameOf(row);
                    StatusKind kind;
                    Assert.That(TryKindOf(name, out kind), Is.True, "the journal's status " + name + " is no BattleCore StatusKind");
                    if (!TryKindOf(name, out kind)) continue;

                    Assert.That(row.Label, Is.EqualTo(name + "（" + SideOf(kind) + "）"), "who carries " + name);

                    string effect;
                    string decay;
                    Assert.That(TrySplit(row.Text, out effect, out decay), Is.True,
                        "the journal's status " + name + " reads " + EffectHead + "… / " + DecayHead + "…");
                    if (!TrySplit(row.Text, out effect, out decay)) continue;

                    Assert.That(Comparable(kind, effect), Is.EqualTo(Comparable(kind, BattleEffect(kind))), "効き方 of " + name);
                    Assert.That(decay, Is.EqualTo(StatusChipText.Decay(kind)), "減り方 of " + name);
                }
            });
        }

        [Test]
        public void TheJournalStatuses_AreTheBattleWordsThatAreNotBossOnly()
        {
            var battle = ((StatusKind[])Enum.GetValues(typeof(StatusKind)))
                .Where(kind => StatusChipText.KindOf(kind) != ChipKind.Boss)
                .Select(kind => kind.ToLabel())
                .ToList();
            var journal = JournalTerms.Statuses.Select(NameOf).ToList();
            Assert.That(journal, Is.EquivalentTo(battle));
        }

        [Test]
        public void TheJournalPage_HoldsTheRowsToCheck()
        {
            // The tests above would pass on empty lists; this keeps them from passing for that reason.
            Assert.Multiple(() =>
            {
                Assert.That(JournalTerms.Terms.Count, Is.EqualTo(16), "battle-visual-v1 §7.5");
                Assert.That(JournalTerms.Statuses.Count, Is.EqualTo(10), "battle-visual-v1 §7.3, the bosses' words left out");
            });
        }

        // ---- helpers ----

        /// <summary>"出血（相手に付ける）" → "出血".</summary>
        private static string NameOf(JournalRow row)
        {
            int at = row.Label.IndexOf('（');
            return at < 0 ? row.Label : row.Label.Substring(0, at);
        }

        private static bool TryKindOf(string name, out StatusKind kind)
        {
            foreach (StatusKind candidate in (StatusKind[])Enum.GetValues(typeof(StatusKind)))
            {
                if (candidate.ToLabel() == name)
                {
                    kind = candidate;
                    return true;
                }
            }
            kind = default;
            return false;
        }

        /// <summary>The journal's side words (JournalTerms' ToFoe / ToSelf) for the battle's chip family.</summary>
        private static string SideOf(StatusKind kind)
        {
            switch (StatusChipText.KindOf(kind))
            {
                case ChipKind.OnFoe: return "相手に付ける";
                case ChipKind.OnSelf: return "自分に付ける";
                default: return StatusChipText.GroupWord(StatusChipText.KindOf(kind));
            }
        }

        /// <summary>"効き方: X\n減り方: Y" → X and Y.</summary>
        private static bool TrySplit(string text, out string effect, out string decay)
        {
            effect = null;
            decay = null;
            string[] lines = text.Split('\n');
            if (lines.Length != 2) return false;
            if (!lines[0].StartsWith(EffectHead, StringComparison.Ordinal)) return false;
            if (!lines[1].StartsWith(DecayHead, StringComparison.Ordinal)) return false;
            effect = lines[0].Substring(EffectHead.Length);
            decay = lines[1].Substring(DecayHead.Length);
            return true;
        }

        /// <summary>The battle's 効き方 without what it does at the stacks held now.</summary>
        private static string BattleEffect(StatusKind kind)
        {
            string effect = StatusChipText.Effect(kind, 0);
            int at = effect.IndexOf(NowClause, StringComparison.Ordinal);
            return at < 0 ? effect : effect.Substring(0, at);
        }

        private static string Comparable(StatusKind kind, string effect)
        {
            if (kind == StatusKind.Bleed && effect.StartsWith(BleedOpponentPrefix, StringComparison.Ordinal))
                return effect.Substring(BleedOpponentPrefix.Length);
            return effect;
        }
    }
}
