// The choosing screen at a 死亡地点の痕跡 (#106), after the pick screen's pattern (#626, CardPicks): what
// the screen shows is built here from the exploration's pending trace, each card printed with the battle's
// own face at the step it is handed on with, each page as its one line. The choice is a list of keys the
// caller keeps (up to Heritage.PickLimit), toggled here and confirmed here: the trace is collected
// (ExplorationSession.TakeLegacy) and what it hands on goes to Expedition.Inherit. The View only lays the
// choices out and forwards the presses.
//
// ExpeditionLegacy is the other half: what a dying life of the expedition leaves, read when the
// exploration settles the death (ExplorationSession.LegacySource).
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using Depiction;
using Depiction.Bridge;
using DungeonCore;

namespace Exploration.Bridge
{
    /// <summary>
    /// What an expedition's dying life leaves (#106): every card the mastery book holds at 習熟 1 段 or more
    /// with the name it bears at that step, and the journal's filled pages. DungeonCore.Heritage applies the
    /// shallow-death rule on top.
    /// </summary>
    public sealed class ExpeditionLegacy : ILegacySource
    {
        private readonly Expedition _expedition;

        public ExpeditionLegacy(Expedition expedition)
        {
            _expedition = expedition ?? throw new ArgumentNullException(nameof(expedition));
        }

        /// <summary>Null: the stand-in name (Heritage.StandInName) until #110 names the generations.</summary>
        public string Name => null;

        /// <summary>The journal's filled pages (#107 plugs its journal in here). Null: no page.</summary>
        public ILegacyPages Journal { get; set; }

        public IReadOnlyList<LegacyCard> Cards
        {
            get
            {
                _expedition.SyncLife();
                return Legacies.CardsOf(_expedition.Mastery);
            }
        }

        public IReadOnlyList<LegacyPage> Pages => Journal?.FilledPages() ?? Array.Empty<LegacyPage>();
    }

    /// <summary>The mastery side of a legacy: what a book leaves, and a book taking a step in.</summary>
    public static class Legacies
    {
        /// <summary>Every card of the book at 習熟 1 段 or more, by id, with its step and the name it bears there.</summary>
        public static IReadOnlyList<LegacyCard> CardsOf(MasteryBook book)
        {
            if (book == null) throw new ArgumentNullException(nameof(book));
            if (book.Records == null) return Array.Empty<LegacyCard>();
            return book.Records.Values
                .Where(r => r.Rank >= Heritage.MinRank)
                .OrderBy(r => r.CardId, StringComparer.Ordinal)
                .Select(r => new LegacyCard(r.CardId, r.Rank, book.Mastered(CardCatalog.ById(r.CardId)).Name))
                .ToList();
        }

        /// <summary>
        /// The book with a card taken in at <paramref name="rank"/> (concept-v3.md §14: a card keeps its step).
        /// A step the book already passes is kept. The ticks rise to the step's threshold so the card grows on
        /// from there. Only one card of a life holds step 3 (§17.8): when another holds it, the card comes in
        /// at step 2. A card with no mastery profile cannot hold a step and leaves the book as it is.
        /// </summary>
        public static MasteryBook WithRank(MasteryBook book, string cardId, int rank)
        {
            if (book == null) throw new ArgumentNullException(nameof(book));
            if (cardId == null) throw new ArgumentNullException(nameof(cardId));
            MasteryProfile profile = book.ProfileOf(cardId);
            if (profile == null || rank < 1) return book;

            int step = Math.Max(book.RankOf(cardId), Math.Min(rank, BattleCore.Mastery.TopRank));
            string holder = book.TopRankHolder;
            if (step >= BattleCore.Mastery.TopRank && holder != null && holder != cardId) step = BattleCore.Mastery.TopRank - 1;
            int ticks = Math.Max(book.TicksOf(cardId), profile.Thresholds[step - 1]);

            var records = new Dictionary<string, CardMastery>(StringComparer.Ordinal);
            if (book.Records != null)
            {
                foreach (KeyValuePair<string, CardMastery> pair in book.Records) records[pair.Key] = pair.Value;
            }
            records[cardId] = new CardMastery(cardId, ticks, step);
            return book with { Records = records };
        }
    }

    /// <summary>One candidate on the choosing screen.</summary>
    public sealed class HeritageChoice
    {
        public HeritageChoice(LegacyCandidate candidate, CardFace face, string caption, bool selected)
        {
            Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
            Face = face;
            Caption = caption ?? "";
            Selected = selected;
        }

        public LegacyCandidate Candidate { get; }

        /// <summary>What the choice is named by when it is pressed.</summary>
        public string Key => Candidate.Key;

        public LegacyKind Kind => Candidate.Kind;

        /// <summary>The card's name at its step, or the page's one line.</summary>
        public string Title => Candidate.Title;

        /// <summary>A card's face as the hand prints it, at the step it is handed on with. Null for a page.</summary>
        public CardFace Face { get; }

        /// <summary>Printed under the choice: the card's step, or that it is a journal page.</summary>
        public string Caption { get; }

        public bool Selected { get; }
    }

    /// <summary>One frame of the choosing screen.</summary>
    public sealed class HeritagePick
    {
        public static readonly HeritagePick None = new HeritagePick("", "", Array.Empty<HeritageChoice>(), 0);

        public HeritagePick(string title, string prompt, IReadOnlyList<HeritageChoice> choices, int traceId)
        {
            Title = title ?? "";
            Prompt = prompt ?? "";
            Choices = choices ?? Array.Empty<HeritageChoice>();
            TraceId = traceId;
        }

        /// <summary>The trace being chosen from; 0 when nothing is shown.</summary>
        public int TraceId { get; }

        public bool IsShown => TraceId > 0;

        public string Title { get; }

        /// <summary>Who died where and how, and what choosing does.</summary>
        public string Prompt { get; }

        /// <summary>Cards first, deepest step first, then pages (DungeonCore.Heritage.Candidates).</summary>
        public IReadOnlyList<HeritageChoice> Choices { get; }

        public int Limit => Heritage.PickLimit;

        public int SelectedCount => Choices.Count(c => c.Selected);

        /// <summary>「選んだ数 / 上限」 for the confirm button's side.</summary>
        public string CountLine => $"選んだ数 {SelectedCount} / {Limit}";

        public string ConfirmLabel => HeritagePicks.ConfirmLabel;
    }

    public static class HeritagePicks
    {
        public const string Title = "死亡地点の痕跡: 遺産を選ぶ";

        public const string ConfirmLabel = "これを継ぐ";

        public const string PageCaption = "手記の頁";

        /// <summary>
        /// What the choosing screen shows now, with <paramref name="selected"/> marked. Shown while the
        /// exploration's pending trace waits on its choice and no battle is being fought;
        /// <see cref="HeritagePick.None"/> otherwise.
        /// </summary>
        public static HeritagePick For(Expedition expedition, IReadOnlyCollection<string> selected = null)
        {
            if (expedition == null) throw new ArgumentNullException(nameof(expedition));
            ExplorationSession session = expedition.Session;
            DeathTrace trace = session.PendingTrace;
            if (trace == null || session.State.IsOver || expedition.Battle != null) return HeritagePick.None;

            var marked = new HashSet<string>(selected ?? Array.Empty<string>(), StringComparer.Ordinal);
            var choices = trace.Candidates
                .Select(c => new HeritageChoice(c, FaceOf(c), CaptionOf(c), marked.Contains(c.Key)))
                .ToList();
            return new HeritagePick(Title, PromptOf(trace), choices, trace.Id);
        }

        /// <summary>
        /// A choice pressed: taken off the selection when it is on it, put on when it is a candidate and the
        /// selection is under the limit. Anything else leaves the selection as it was.
        /// </summary>
        public static IReadOnlyList<string> Toggle(Expedition expedition, IReadOnlyCollection<string> selected, string key)
        {
            var next = (selected ?? Array.Empty<string>()).ToList();
            if (key == null) return next;
            if (next.Remove(key)) return next;

            HeritagePick pick = For(expedition, next);
            if (!pick.IsShown || next.Count >= pick.Limit || pick.Choices.All(c => c.Key != key)) return next;
            next.Add(key);
            return next;
        }

        /// <summary>
        /// The choice confirmed: the trace is collected and the expedition takes in what it hands on. False
        /// when no trace waits or the selection breaks the rules; nothing changes then.
        /// </summary>
        public static bool Confirm(Expedition expedition, IReadOnlyCollection<string> selected)
        {
            if (expedition == null) throw new ArgumentNullException(nameof(expedition));
            if (!For(expedition).IsShown) return false;
            Bequest bequest = expedition.Session.TakeLegacy(selected ?? Array.Empty<string>());
            if (bequest == null) return false;
            expedition.Inherit(bequest);
            return true;
        }

        /// <summary>「1 代目は第 3 層の階層ボスで倒れた。3 つまで継げます。選ばなかったものは失われます。」</summary>
        public static string PromptOf(DeathTrace trace)
        {
            if (trace == null) throw new ArgumentNullException(nameof(trace));
            string how = trace.Cause == DeathCause.Miasma ? "瘴気に呑まれた" : "倒れた";
            string where = $"第 {trace.Place.Layer} 層の{ExplorationText.KindLabel(trace.Place.Kind)}で";
            string what = trace.Candidates.Count == 0
                ? "継げるものは残っていません。"
                : $"{Heritage.PickLimit} つまで継げます。選ばなかったものは失われます。";
            return $"{trace.Name}は{where}{how}。{what}";
        }

        private static CardFace FaceOf(LegacyCandidate candidate)
        {
            if (candidate.Kind != LegacyKind.Card) return null;
            CardDef def = CardCatalog.ById(candidate.Id);
            MasteryProfile profile = MasteryProfiles.Find(def.Id);
            int rank = Math.Min(candidate.Rank, BattleCore.Mastery.TopRank);
            return CoreText.FaceOf(profile == null || rank < 1 ? def : BattleCore.Mastery.Master(def, rank, profile));
        }

        private static string CaptionOf(LegacyCandidate candidate) =>
            candidate.Kind == LegacyKind.Card ? $"習熟 {candidate.Rank} 段目" : PageCaption;
    }
}
