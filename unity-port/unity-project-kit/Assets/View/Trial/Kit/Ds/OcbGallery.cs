// A dev-only page set of every DS component the game uses (kit.md §2.0, §4.14), laid out like the DS's own showcase
// (bundle.js:2695-2920: FloorLane, TurnControls, HandFan, Notices, the long-text gallery) at the battle screen's
// OCB.LAYOUT places, so a page can be put beside the browser's reference screenshots (ref/shots, e.g. 603 / 627 for
// the battle screen, 302 / 306 for the deck's cards).
//
// Put it on an empty GameObject in a scene and press Play. Keys: 1–5 or PgUp / PgDn change the page; on the battle
// page B shows the banner, R the refusal line; L / U toggle the first card's lamp and dark sheet; O toggles an omen's
// .is-active (its box-shadow .12s transition); P replays the stamina pips' ocb-pip-lit; E opens / closes the end paper.
// The sample cards are the DS prototype's SCRIPT cards (bundle.js:966-1040), the texts its own.
using System;
using System.Collections.Generic;
using TMPro;
using Trial.Geometry;
using UnityEngine;

namespace Trial.Kit
{
    [AddComponentMenu("Trial/Dev/Ocb Gallery")]
    public sealed class OcbGallery : MonoBehaviour
    {
        public const int PageCount = 5;

        [Tooltip("The page shown first (0 battle screen, 1 cards, 2 long text, 3 pieces, 4 icons and figures).")]
        public int StartPage;

        [Tooltip("Draw through the gamma target (kit.md §4.12).")]
        public bool UseGammaTarget = true;

        private static readonly string[] PageNames = { "battle screen (OCB.LAYOUT)", "cards and the detail", "long descriptions (fitCardText)", "pieces", "icons, hatches, figures, frames" };

        private TrialStage _stage;
        private RectTransform _layer;
        private readonly List<RectTransform> _pages = new List<RectTransform>();
        private int _page;
        private Action _popKeys;
        private TextMeshProUGUI _title;

        private OcbElement _banner, _refusal;
        private OcbCard _firstCard;
        private OcbOmenPlate _omen;
        private OcbStaminaPlate _stamina;
        private OcbStaminaData _staminaData;
        private OcbEnd _end;

        private void Start()
        {
            _stage = TrialStage.Current ? TrialStage.Current : TrialStage.Create("TrialStage (ocb gallery)", UseGammaTarget);
            _layer = _stage.Layer("ocb gallery", 0);
            BoxGraphic.Make(_layer, "background: var(--ground)", 0f, 0f, TrialStage.W, TrialStage.H, "ground");
            for (int i = 0; i < PageCount; i++) _pages.Add(Ui.Div(_layer, "page " + i, 0f, 0f, TrialStage.W, TrialStage.H));
            _title = Label.Make(_layer, "", TextStyle.FromCss("font: 600 14px/18px var(--font-mono); color: var(--ink-muted)"), 24f, 1058f, 1872f, 18f, "page");
            Build(0, BuildBattle);
            Build(1, BuildCards);
            Build(2, BuildLongText);
            Build(3, BuildPieces);
            Build(4, BuildIcons);
            ShowPage(Mathf.Clamp(StartPage, 0, PageCount - 1));
            _popKeys = KeyStack.Push(OnKey);
        }

        private void Build(int page, Action<RectTransform> build)
        {
            try { build(_pages[page]); }
            catch (Exception e) { Debug.LogException(e, this); }
        }

        private void OnDestroy()
        {
            _popKeys?.Invoke();
            _popKeys = null;
        }

        private bool OnKey(KeyEvent e)
        {
            if (e.Repeat) return false;
            if (e.Key.Length == 1 && e.Key[0] >= '1' && e.Key[0] < '1' + PageCount) { ShowPage(e.Key[0] - '1'); return true; }
            switch (e.Key)
            {
                case "PageDown": ShowPage((_page + 1) % PageCount); return true;
                case "PageUp": ShowPage((_page + PageCount - 1) % PageCount); return true;
                case "b": case "B": if (_banner != null) _banner.Node.Visible = !_banner.Node.Visible; return true;
                case "r": case "R": if (_refusal != null) _refusal.Node.Visible = !_refusal.Node.Visible; return true;
                case "l": case "L": if (_firstCard != null) _firstCard.SetLamp(!_firstCard.Lit); return true;
                case "u": case "U": if (_firstCard != null) _firstCard.Unpayable = !_firstCard.Unpayable; return true;
                case "o": case "O":
                    if (_omen != null) _omen.Update(new OcbOmenData { Kind = "攻撃", Value = "9", Angle = 45f, Hit = true, Active = !_omen.States.Has(".is-active") });
                    return true;
                case "p": case "P": if (_stamina != null) _stamina.Update(_staminaData); return true;
                case "e": case "E": if (_end != null) _end.Node.Visible = !_end.Node.Visible; return true;
            }
            return false;
        }

        private void ShowPage(int i)
        {
            _page = i;
            for (int k = 0; k < _pages.Count; k++) _pages[k].gameObject.SetActive(k == i);
            Label.SetText(_title, "OcbGallery " + (i + 1) + "/" + PageCount + " — " + PageNames[i]
                + "   (1–5 / PgUp PgDn: page · B banner · R refusal · L lamp · U unpayable · O omen active · P pips · E end paper)");
        }

        // ---- helpers ------------------------------------------------------------------------------------------

        private static readonly TextStyle Caption = TextStyle.FromCss("font: 400 14px/18px var(--font-mono); color: var(--ink-muted)");

        private static void Note(RectTransform parent, string text, float x, float y, float w = 220f) =>
            Label.Make(parent, text, Caption, x, y, w, null, "note");

        private static T At<T>(T el, float x, float y) where T : OcbElement
        {
            el.Place(x, y);
            return el;
        }

        // The DS prototype's SCRIPT cards (bundle.js:966-1040) as Card defs.
        private static OcbCardDef Sample(string id)
        {
            var terms = new List<KeyValuePair<string, string>>();
            switch (id)
            {
                case "ishitsubute":
                    terms.Add(new KeyValuePair<string, string>("脆化", "次に受ける攻撃の威力が 1.5 倍"));
                    terms.Add(new KeyValuePair<string, string>("威圧", "次の行動の威力と Guard が −3"));
                    return new OcbCardDef { Name = "石礫", Attr = "attack", Cost = 2, Values = { new OcbCardValue("sword", 5, "威力") }, Desc = "敵に 脆化 2・威圧 2",
                        Trait = new OcbCardTrait("間合い 2 以上", "→ コスト −1"), Terms = terms,
                        Detail = "敵に 5 ダメージ、脆化 2 と威圧 2 を付ける。出す前の間合いが 2 以上なら、コストが 1 下がって 1。" }.WithReach(0, 3);
                case "nobizuki":
                    terms.Add(new KeyValuePair<string, string>("間合い", "自分と相手のあいだの空きマスの数。0 は隣り合っている"));
                    return new OcbCardDef { Name = "伸び突き", Attr = "attack", Cost = 2, Values = { new OcbCardValue("sword", 15, "威力") }, Desc = "",
                        Trait = new OcbCardTrait("間合い 2 以上", "→ 威力 +5"), Terms = terms,
                        Detail = "敵に 15 ダメージ。出す前の間合いが 2 以上なら、ダメージが 5 増えて 20。届くのは 1〜2 なので、成り立つのは間合い 2 のとき。" }.WithReach(1, 2);
                case "kesagiri":
                    terms.Add(new KeyValuePair<string, string>("間合い", "自分と相手のあいだの空きマスの数。0 は隣り合っている"));
                    return new OcbCardDef { Name = "袈裟斬り", Attr = "attack", Cost = 2, Values = { new OcbCardValue("sword", 15, "威力") }, Desc = "",
                        Trait = new OcbCardTrait("間合い 0", "→ 威力 +5"), Terms = terms,
                        Detail = "敵に 15 ダメージ。出す前の間合いが 0（隣り合っている）なら、ダメージが 5 増えて 20。" }.WithReach(0, 1);
                case "kokyu":
                    terms.Add(new KeyValuePair<string, string>("温存", "この札を出したあとも、スタミナが n 以上残る"));
                    return new OcbCardDef { Name = "呼吸を整える", Attr = "defense", Cost = 2, ReachSelf = true, Values = { new OcbCardValue("shield", 9, "Guard") }, Desc = "",
                        Trait = new OcbCardTrait("温存 残り 6 以上", "→ 次ターン回復+1"), Terms = terms,
                        Detail = "Guard を 9 得る。温存：この札を出したあともスタミナが 6 以上残るなら、次のターン開始のスタミナ回復が 1 増える（3 → 4）。" };
                case "ushirotobi":
                    terms.Add(new KeyValuePair<string, string>("離れる", "自分が動いて、相手との間合いを広げる"));
                    return new OcbCardDef { Name = "後ろ跳び", Attr = "defense", Cost = 1, ReachSelf = true,
                        Values = { new OcbCardValue("back", 2, "離れる"), new OcbCardValue("shield", 4, "Guard") }, Desc = "",
                        Trait = new OcbCardTrait("予兆が攻撃", "→ Guard +3"), Terms = terms,
                        Detail = "2 マス離れ、Guard 4 を得る。いちばん近い敵の予兆が攻撃なら、Guard がさらに 3 増えて 7。" };
                case "iwanokamae":
                    terms.Add(new KeyValuePair<string, string>("永続", "消えずに残る。重ねると重ねた数のぶん効く"));
                    return new OcbCardDef { Name = "岩の構え", Attr = "stance", Cost = 2, ReachSelf = true, Lines = { "ターン開始 Guard +3" },
                        Trait = new OcbCardTrait("置く時 予兆が攻撃", "→ Guard +3（1 回）"), Terms = terms,
                        Detail = "スタンス。置いている間、自分のターン開始に Guard を 3 得る。置くときに敵の予兆が攻撃なら、その場で Guard +3（1 回だけ）。" };
                default:   // fumikomi
                    terms.Add(new KeyValuePair<string, string>("連動 X", "このターンに属性 X のカードをもう出している"));
                    terms.Add(new KeyValuePair<string, string>("詰める", "自分が動いて、相手との間合いを縮める"));
                    return new OcbCardDef { Name = "踏み込み斬り", Attr = "attack", Cost = 3,
                        Values = { new OcbCardValue("sword", 16, "威力"), new OcbCardValue("forward", 1, "詰める") }, Desc = "",
                        Trait = new OcbCardTrait("連動 スタンス", "→ 威力 +3"), Terms = terms,
                        Detail = "敵に 16 ダメージを与え、そのあと 1 マス詰める。連動 スタンス：このターンにスタンス札をもう出していれば、威力 +3 で 19。" }.WithReach(1, 2);
            }
        }

        // ---- page 1: the battle screen ------------------------------------------------------------------------

        private void BuildBattle(RectTransform pg)
        {
            // the DS gallery's ground: linear-gradient(var(--sky-top), var(--meadow-bottom) 52%, var(--earth-top) 52%, var(--earth-bottom))
            GradientGraphic.Css(pg, "linear-gradient(var(--sky-top), var(--meadow-bottom) 52%, var(--earth-top) 52%, var(--earth-bottom))", 0f, 0f, TrialStage.W, TrialStage.H).Fringe = false;

            // FloorLane({ cells 6, player 1, enemy 4, threat on 1–2, reach 4, move 3 }) (bundle.js:2874)
            const int cells = 6, player = 1, enemy = 4;
            float cw = 1320f / cells;
            for (int i = 0; i < cells; i++)
            {
                OcbFloorTile t = At(Ocb.FloorTile(pg, cw - 6f), DsLayout.FloorX0 + i * cw + 3f, DsLayout.FloorY);
                var th = new List<OcbThreat>();
                if (i == 1) th.Add(new OcbThreat(45f));
                if (i == 2) th.Add(new OcbThreat(45f, true));
                t.SetThreat(th, i == 3);
                if (i == enemy) t.ReachOpacity = 1f;
            }
            int gap = enemy - player - 1;
            At(Ocb.DistanceChip(pg, gap.ToString(), gap * cw), gap > 0 ? DsLayout.FloorX0 + (player + 1) * cw : DsLayout.FloorX0 + enemy * cw - 50f, DsLayout.DistY);
            float px = DsLayout.FloorX0 + cw * (player + 0.5f), ex = DsLayout.FloorX0 + cw * (enemy + 0.5f);
            FigureDef pf = Ocb.FigureOf("swordsman"), ef = Ocb.FigureOf("spearman");
            Ui.Place(Ocb.FootLight(pg, pf.Body * 2f, false).rectTransform, px - pf.Body, 540f);
            Ui.Place(Ocb.Figure(pg, "swordsman").rectTransform, px - pf.Cx, DsLayout.FootY - pf.H);
            Ui.Place(Ocb.FootLight(pg, ef.Body * 2f, true).rectTransform, ex - ef.Body, 540f);
            Ui.Place(Ocb.Figure(pg, "spearman").rectTransform, ex - ef.Cx, DsLayout.FootY - ef.H);

            // the omen 14 px over the head, the reserve plate beside it
            At(Ocb.OmenPlate(pg, new OcbOmenData { Kind = "攻撃", Value = "9", Angle = 45f, Hit = true }), ex - 100f, DsLayout.FootY - ef.H - DsLayout.OmenGap - DsLayout.OmenH);
            At(Ocb.ReserveOmen(pg, new OcbOmenData { Kind = "移動", Value = "1" }), ex + 116f, DsLayout.FootY - ef.H - DsLayout.OmenGap - 66f);

            // stance slots, status panels
            At(Ocb.StanceSlot(pg, new OcbStanceData { Name = "岩の構え", Count = 1 }), DsLayout.Edge, DsLayout.StanceY);
            At(Ocb.StanceSlot(pg, new OcbStanceData { Foe = true, Empty = true }), DsLayout.W - DsLayout.Edge - DsLayout.StanceW, DsLayout.StanceY);
            At(Ocb.StatusPanel(pg, new OcbStatusPanelData { Side = "self", Hp = 50, MaxHp = 50, Guard = 3, Chips = { new OcbChipData("focus", 1) } }),
                px - DsLayout.PanelSelfW * 0.5f, DsLayout.PanelY);
            At(Ocb.StatusPanel(pg, new OcbStatusPanelData { Side = "foe", Hp = 60, MaxHp = 60, Loss = 9, Guard = 5, St = 9, Reserve = true,
                Chips = { new OcbChipData("brittle", 2), new OcbChipData("intimidate", 2) } }), ex - DsLayout.PanelFoeW * 0.5f, DsLayout.PanelY);

            // the held card's marks: the four hooks round the enemy, the prediction tag, the throw line
            float fw = ef.Body + 40f, fh = ef.H + 24f;
            At(Ocb.TargetFrame(pg, fw, fh), ex - fw * 0.5f, DsLayout.FootY - fh + 6f);
            At(Ocb.PredTag(pg, "sword", "15"), ex, DsLayout.FootY - fh + 6f);
            Ocb.ThrowLine(pg, DsLayout.ThrowX0, 788.5f, DsLayout.ThrowX1 - DsLayout.ThrowX0);
            At(Ocb.FloatNum(pg, "guard", "+3"), px, 420f);

            // TurnControls (bundle.js:2909)
            At(Ocb.CornerInfo(pg, "3", "1", "12"), DsLayout.Corner.X, DsLayout.Corner.Y);
            At(Ocb.SpeedToggle(pg, 1.25f), DsLayout.Speed.X, DsLayout.Speed.Y);
            At(Ocb.JournalButton(pg), DsLayout.Journal.X, DsLayout.Journal.Y);
            At(Ocb.PlayedAttrs(pg, new Dictionary<string, int> { ["attack"] = 2, ["defense"] = 1 }), DsLayout.Attrs.X, DsLayout.Attrs.Y);
            At(Ocb.GuardPreview(pg, "3"), DsLayout.GuardPre.X, DsLayout.GuardPre.Y);
            At(Ocb.EndTurnButton(pg, false).Button, DsLayout.EndTurn.X, DsLayout.EndTurn.Y);
            _stamina = At(Ocb.StaminaPlate(pg, _staminaData = new OcbStaminaData { St = 10, Max = 10, Pay = 2, ShowPay = true, Lit = { 9 } }), DsLayout.Stamina.X, DsLayout.Stamina.Y);
            string[] piles = { "deck", "discard", "exile" };
            int[] counts = { 12, 3, 0 };
            for (int i = 0; i < 3; i++)
                At(Ocb.Pile(pg, piles[i], counts[i]), DsLayout.Piles.X + i * (DsLayout.Piles.W + DsLayout.Piles.Gap), DsLayout.Piles.Y);

            // HandFan({ cards: 5, hover: 1 }) (bundle.js:2855)
            string[] hand = { "ishitsubute", "nobizuki", "kesagiri", "kokyu", "iwanokamae" };
            int n = hand.Length, hover = 1;
            float sp = n <= 5 ? 160f : n == 6 ? 150f : n == 7 ? 140f : 126f;
            OcbCard hovered = null;
            RectTransform fan = Ui.Div(pg, "hand", 0f, 0f, TrialStage.W, TrialStage.H);
            for (int i = 0; i < n; i++)
            {
                float k = i - (n - 1) * 0.5f;
                bool hov = i == hover;
                OcbCard c = Ocb.Card(fan, Sample(hand[i]), new OcbCardState { Lamp = hov });
                c.Node.SetOrigin(108f / 216f, 1f);   // transform-origin: 108px 304px
                c.Place(DsLayout.Hand.Cx + k * sp - 108f, hov ? 760f : 896f + k * k * 3.2f);
                if (!hov) c.Node.SetStyleTransform(rot: k * 2.5f);
                c.Node.Z = hov ? 50 : i;
                if (hov) hovered = c;
            }
            if (hovered != null)
            {
                OcbElement hl = Ocb.HoverLine(fan, "間合い 2 以上なら、この札の威力 15 → 20", true);
                float cx = Mathf.Clamp(hovered.Node.X + 108f, 16f + 210f, TrialStage.W - 16f - 210f);
                hl.Place(cx - 210f, 760f - 8f - hl.H);
                hl.Node.Z = 60;
            }

            _banner = At(Ocb.Banner(pg, "あなたのターン", false), 0f, DsLayout.BannerY);
            _banner.Node.Visible = false;
            _refusal = Ocb.RefusalLine(pg, "スタミナが足りない（あと 1）");
            _refusal.Place(DsLayout.Hand.Cx - _refusal.W * 0.5f, DsLayout.RefusalBottom - _refusal.H);
            _refusal.Node.Visible = false;
        }

        // ---- page 2: cards and the detail -----------------------------------------------------------------------

        private void BuildCards(RectTransform pg)
        {
            float x = 36f, y = 40f;
            Note(pg, "Card(def, st): lit (L toggles), cost down ▼, self reach, unpayable (U toggles), stance rows, two values, plain trait", x, y - 24f, 1400f);
            _firstCard = At(Ocb.Card(pg, Sample("nobizuki"), new OcbCardState { Lamp = true }), x, y);
            At(Ocb.Card(pg, Sample("ishitsubute"), new OcbCardState { Cost = 1 }), x + 264f, y);
            At(Ocb.Card(pg, Sample("kokyu")), x + 528f, y);
            At(Ocb.Card(pg, Sample("ushirotobi"), new OcbCardState { Unpayable = true }), x + 792f, y);
            At(Ocb.Card(pg, Sample("iwanokamae")), x + 1056f, y);
            At(Ocb.Card(pg, Sample("fumikomi")), x + 1320f, y);
            OcbCardDef plain = Sample("kesagiri");
            plain.Name = "素振り";
            plain.Trait = null;
            plain.Attr = "skill";
            plain.Values.Clear();
            plain.Values.Add(new OcbCardValue("draw", 1, "ドロー"));
            plain.Desc = "札を 1 枚引く。";
            At(Ocb.Card(pg, plain), x + 1584f, y);

            y = 380f;
            Note(pg, "CardDetail(def): 560×280, the card at .8, name / meta / full / terms", x, y - 24f, 900f);
            At(Ocb.CardDetail(pg, Sample("nobizuki")), x, y);
            At(Ocb.CardDetail(pg, Sample("iwanokamae")), x + 600f, y);
            At(Ocb.CardDetail(pg, Sample("ishitsubute"), new OcbCardState { Cost = 1 }), x + 1200f, y);

            y = 720f;
            Note(pg, "scale .72 (the departure grid) and .5", x, y - 24f, 600f);
            string[] ids = { "nobizuki", "kokyu", "iwanokamae", "fumikomi" };
            for (int i = 0; i < ids.Length; i++)
            {
                OcbCard c = Ocb.Card(pg, Sample(ids[i]));
                c.Node.SetOrigin(0f, 0f);
                c.Node.SetStyleTransform(sx: 0.72f, sy: 0.72f);
                c.Place(x + i * 172f, y);
            }
            for (int i = 0; i < ids.Length; i++)
            {
                OcbCard c = Ocb.Card(pg, Sample(ids[i]), new OcbCardState { Lamp = i == 0 });
                c.Node.SetOrigin(0f, 0f);
                c.Node.SetStyleTransform(sx: 0.5f, sy: 0.5f);
                c.Place(x + 720f + i * 124f, y);
            }
            Note(pg, "an unknown attribute keeps the steel frame and ATTR.attack's icon", x + 1240f, y - 24f, 640f);
            OcbCardDef move = Sample("ushirotobi");
            move.Attr = "move";
            At(Ocb.Card(pg, move), x + 1240f, y);
        }

        // ---- page 3: long descriptions (the DS gallery 'text', bundle.js:2771) --------------------------------------

        private void BuildLongText(RectTransform pg)
        {
            Label.Make(pg, "長い説明文の収め方（未決 5）", Ocb.TextOf("font:700 34px/44px var(--font-display)"), 48f, 28f, 1200f, 44f, "title");
            Label.Make(pg, "枠は 188×66 px（スタンスは 88）、本文は 16 px。行頭の句読点は禁則で避けます。文字の大きさの下限は 12 px。",
                Ocb.TextOf("font:400 18px/28px var(--font-body);color:var(--ink-muted)"), 48f, 76f, 1600f, 28f, "sub");
            var lt = new Dictionary<int, string>
            {
                [20] = "敵に脆化 2 を付け、自分は 1 マス離れる。",
                [30] = "間合い 2 以上で出すと、敵の脆化を 1 つ延ばしてから 1 マス押し返す。",
                [40] = "このターンにスタンスを置いていれば、敵の威圧と脆化を 1 つずつ延ばし、自分は 1 マス詰める。",
            };
            string[] modes = { "unity", "a", "b", "c" };
            string[] heads = { "今の Unity", "A 縮めるだけ", "B 縮めてから折り返す", "C 文を 2 段に分ける" };
            int[] lens = { 20, 30, 40 };
            for (int col = 0; col < modes.Length; col++)
            {
                float x = 300f + col * 400f;
                Label.Make(pg, heads[col], Ocb.TextOf("font:700 22px/28px var(--font-display);white-space:nowrap"), x, 128f, null, null, "head");
                for (int row = 0; row < lens.Length; row++)
                {
                    RectTransform holder = Ui.Div(pg, "holder", x, 170f + row * 296f, 216f, 304f);
                    UiNode hn = UiNode.Of(holder);
                    hn.SetOrigin(0f, 0f);
                    hn.SetStyleTransform(sx: 0.88f, sy: 0.88f);
                    Ocb.Card(holder, Sample("nobizuki"), new OcbCardState { Desc = lt[lens[row]], Fit = modes[col] });
                }
            }
            for (int row = 0; row < lens.Length; row++)
                Label.Make(pg, "全角 " + lens[row] + " 字", Ocb.TextOf("font:700 24px/1 var(--font-mono);white-space:nowrap"), 60f, 290f + row * 296f, null, null, "len");
        }

        // ---- page 4: pieces ---------------------------------------------------------------------------------------

        private void BuildPieces(RectTransform pg)
        {
            float x = 24f, y = 40f;
            Note(pg, "StatusChip (every STATUS, boss, .is-hover, 「+n」); hover a chip for :hover", x, y - 24f, 900f);
            float cx = x;
            foreach (string id in Ocb.StatusIds)
            {
                OcbChip c = At(Ocb.StatusChip(pg, new OcbChipData(id, 2)), cx, y);
                cx += c.W + 5f;
            }
            OcbChip boss = At(Ocb.StatusChip(pg, new OcbChipData("bleed", 3, boss: true)), cx, y);
            cx += boss.W + 5f;
            OcbChip hov = At(Ocb.StatusChip(pg, new OcbChipData("slow", 1) { Hover = true }), cx, y);
            cx += hov.W + 5f;
            At(Ocb.MoreChip(pg, 3), cx, y);

            y = 110f;
            Note(pg, "StatusTip, StanceTip", x, y - 22f);
            At(Ocb.StatusTip(pg, "insight", "1"), x, y);
            At(Ocb.StanceTip(pg, "岩の構え", 2, "自分のターン開始に Guard +3"), x + 360f, y);

            Note(pg, "StatusPanel self (7 chips → +n), foe elite with a swatch, foe at Guard 0", x + 720f, y - 22f, 800f);
            At(Ocb.StatusPanel(pg, new OcbStatusPanelData
            {
                Side = "self", Hp = 37, MaxHp = 50, Loss = 6, Guard = 12,
                Chips = { new OcbChipData("focus", 1), new OcbChipData("empower", 2), new OcbChipData("regen", 1), new OcbChipData("agile", 1),
                    new OcbChipData("insight", 1), new OcbChipData("bleed", 2), new OcbChipData("slow", 1) },
            }), x + 720f, y);
            At(Ocb.StatusPanel(pg, new OcbStatusPanelData { Side = "foe", Hp = 48, MaxHp = 60, Guard = 4, St = 7, Reserve = true, Elite = true, SwatchAngle = -45f,
                Chips = { new OcbChipData("brittle", 2) } }), x + 1040f, y);
            At(Ocb.StatusPanel(pg, new OcbStatusPanelData { Side = "foe", Hp = 20, MaxHp = 30, Guard = 0, St = 3 }), x + 1270f, y);

            Note(pg, "StanceSlot: self ×2, foe + swatch, 「+n」, empty, empty foe", x + 1500f, y - 22f, 400f);
            At(Ocb.StanceSlot(pg, new OcbStanceData { Name = "水の構え", Count = 2 }), x + 1600f, y);
            At(Ocb.StanceSlot(pg, new OcbStanceData { Name = "威圧の構え", Foe = true, SwatchAngle = 45f }), x + 1600f, y + 58f);
            At(Ocb.StanceSlot(pg, new OcbStanceData { More = 2 }), x + 1600f, y + 116f);
            At(Ocb.StanceSlot(pg, new OcbStanceData { Empty = true }), x + 1600f, y + 174f);
            OcbStanceSlot hs = At(Ocb.StanceSlot(pg, new OcbStanceData { Empty = true, Foe = true }), x + 1600f, y + 232f);
            hs.Hover = true;

            y = 430f;
            Note(pg, "OmenPlate: every kind (O toggles the first's .is-active), hit / miss marks; ReserveOmen", x, y - 22f, 1200f);
            string[] kinds = { "攻撃", "防御", "移動", "構え", "技", "状態", "休み" };
            for (int i = 0; i < kinds.Length; i++)
            {
                var d = new OcbOmenData { Kind = kinds[i], Value = kinds[i] == "休み" || kinds[i] == "状態" ? null : (i + 3).ToString(), Angle = i < 2 ? 45f : (float?)null,
                    Hit = i == 0 ? true : i == 1 ? false : (bool?)null, Active = false };
                if (kinds[i] == "技") d.StatusIcons.Add("brittle");
                if (kinds[i] == "状態") { d.StatusIcons.Add("slow"); d.StatusIcons.Add("fatigue"); }
                OcbOmenPlate p = At(Ocb.OmenPlate(pg, d), x + i * 214f, y);
                if (i == 0) _omen = p;
            }
            for (int i = 0; i < 3; i++)
                At(Ocb.ReserveOmen(pg, new OcbOmenData { Kind = kinds[i], Value = (i + 2).ToString() }), x + 1500f + (i % 2) * 190f, y + (i / 2) * 76f);

            y = 600f;
            Note(pg, "StaminaPlate (P replays the lit pip), Pile (hover)", x, y - 22f, 600f);
            At(Ocb.StaminaPlate(pg, new OcbStaminaData { St = 6, Max = 10, Pay = 2, ShowPay = true, Next = 2, Lit = { 3 } }), x, y);
            At(Ocb.StaminaPlate(pg, new OcbStaminaData { St = 3, Max = 10 }), x, y + 100f);
            int[] pc = { 0, 2, 5, 12 };
            string[] pk = { "exile", "discard", "deck", "deck" };
            for (int i = 0; i < pc.Length; i++) At(Ocb.Pile(pg, pk[i], pc[i]), x + 260f + i * 86f, y + 16f);

            Note(pg, "HoverLine (lit / plain / long), ReachPanel, RefusalLine", x + 640f, y - 22f, 700f);
            At(Ocb.HoverLine(pg, "間合い 2 以上なら、この札の威力 15 → 20", true), x + 640f, y);
            At(Ocb.HoverLine(pg, "いちばん近い敵の予兆が攻撃なら、この札の Guard 4 → 7", false), x + 640f, y + 70f);
            At(Ocb.HoverLine(pg, "出した後スタミナが 6 以上残れば、次ターンの回復 3 → 4。温存の条件は、この札を出したあとに数えます。", false), x + 640f, y + 140f);
            At(Ocb.ReachPanel(pg, "相手との間合いが 0〜1 のとき使用可能"), x + 1080f, y);
            At(Ocb.RefusalLine(pg, "スタミナが足りない（あと 1）"), x + 1080f, y + 92f);

            Note(pg, "drawer tabs and rows", x + 1400f, y - 22f, 400f);
            At(Ocb.DrawerTabs(pg, new[] { "山札", "捨札", "除外" }, 0), x + 1400f, y);
            At(Ocb.DrawerRow(pg, 420f, DsTokens.Violet, "伸び突き", "2 / 3"), x + 1400f, y + 48f);
            At(Ocb.DrawerRow(pg, 420f, DsTokens.Guard, "呼吸を整える", "1 / 2"), x + 1400f, y + 88f);
            At(Ocb.DrawerRow(pg, 420f, DsTokens.Crimson, "岩の構え", "0 / 1"), x + 1400f, y + 128f);

            y = 880f;
            Note(pg, "FloatNum (damage + label, guard, heal, miss), placed at its centre", x, y - 22f, 700f);
            At(Ocb.FloatNum(pg, "damage", "9", "脆化"), x + 100f, y + 40f);
            At(Ocb.FloatNum(pg, "guard", "+3"), x + 300f, y + 40f);
            At(Ocb.FloatNum(pg, "heal", "+2"), x + 460f, y + 40f);
            At(Ocb.FloatNum(pg, "miss", "外れ"), x + 620f, y + 40f);
            Note(pg, "PredTag, DistanceChip (adjacent / bracketing 220), GuardPreview", x + 760f, y - 22f, 700f);
            At(Ocb.PredTag(pg, "sword", "15"), x + 860f, y + 40f);
            At(Ocb.PredTag(pg, "shield", "7", DsTokens.Guard), x + 1020f, y + 40f);
            At(Ocb.DistanceChip(pg, "0", 0f), x + 1120f, y + 20f);
            At(Ocb.DistanceChip(pg, "2", 220f), x + 1240f, y + 20f);
            At(Ocb.GuardPreview(pg, "3"), x + 1500f, y + 10f);
            At(Ocb.EndTurnButton(pg, true).Button, x + 1640f, y);

            _end = Ocb.End(pg, "勝利", new[] { "槍兵を倒した。", "ターン 4 ・ 残りの HP 37 / 50" }, "もう一度");
            _end.Node.Visible = false;
        }

        // ---- page 5: icons, hatches, figures, frames ----------------------------------------------------------------

        private void BuildIcons(RectTransform pg)
        {
            float x0 = 24f, y = 40f;
            Note(pg, "icon(name, 24, ink) — every OCB.ICONS entry (ICON_LABELS under it)", x0, y - 24f, 900f);
            int i = 0;
            foreach (KeyValuePair<string, string> kv in Ocb.Icons)
            {
                float x = x0 + (i % 18) * 104f, yy = y + (i / 18) * 76f;
                Ui.Place(Ocb.Icon(pg, kv.Key, 24f).rectTransform, x, yy);
                Ui.Place(Ocb.Icon(pg, kv.Key, 36f, DsTokens.Amber).rectTransform, x + 30f, yy);
                Ocb.IconLabels.TryGetValue(kv.Key, out string label);
                Note(pg, kv.Key + "\n" + label, x, yy + 40f, 100f);
                i++;
            }
            y += ((Ocb.Icons.Count + 17) / 18) * 76f + 30f;
            Note(pg, "options: shield fill .2 · shield dashed · heart / pip filled · width 3", x0, y - 24f, 900f);
            Ui.Place(Ocb.Icon(pg, "shield", 48f, DsTokens.Guard, DsTokens.Guard, 0.2f).rectTransform, x0, y);
            Ui.Place(Ocb.Icon(pg, "shield", 48f, DsTokens.Steel, dashed: true).rectTransform, x0 + 64f, y);
            Ui.Place(Ocb.Icon(pg, "heart", 48f, DsTokens.HpText).rectTransform, x0 + 128f, y);
            Ui.Place(Ocb.Icon(pg, "pip", 48f, DsTokens.Amber).rectTransform, x0 + 192f, y);
            Ui.Place(Ocb.Icon(pg, "sword", 48f, DsTokens.Phosphor, width: 3f).rectTransform, x0 + 256f, y);
            Ui.Place(Ocb.Icon(pg, "no-such-icon", 48f).rectTransform, x0 + 320f, y);

            Note(pg, "swatch(angle, 22 / 16), hatch on FloorTile (setThreat: 45, −45, 90 boundary, move, both)", x0 + 440f, y - 24f, 1000f);
            float[] angles = { 45f, -45f, 90f };
            for (int k = 0; k < 3; k++)
            {
                At(Ocb.Swatch(pg, angles[k], 22f), x0 + 440f + k * 34f, y);
                At(Ocb.Swatch(pg, angles[k], 16f), x0 + 440f + k * 34f, y + 30f);
                OcbFloorTile t = At(Ocb.FloorTile(pg, 214f), x0 + 560f + k * 230f, y);
                t.SetThreat(new[] { new OcbThreat(angles[k], k == 2) }, false);
            }
            OcbFloorTile mv = At(Ocb.FloorTile(pg, 214f), x0 + 1250f, y);
            mv.SetThreat(null, true);
            OcbFloorTile both = At(Ocb.FloorTile(pg, 214f), x0 + 1480f, y);
            both.SetThreat(new[] { new OcbThreat(45f), new OcbThreat(-45f) }, true);
            OcbFloorTile lit = At(Ocb.FloorTile(pg, 214f), x0 + 1250f, y + 52f);
            lit.ReachOpacity = 1f;
            OcbFloorTile wipe = At(Ocb.FloorTile(pg, 214f), x0 + 1480f, y + 52f);
            wipe.SetThreat(new[] { new OcbThreat(45f) }, false);
            wipe.SetThreatClip(0.4f);
            Note(pg, "reach lit · wipe 60 %", x0 + 1250f, y + 96f, 400f);

            y += 140f;
            Note(pg, "Figure(kind) with FootLight(body × 2): swordsman, spearman, hound, serpent, warden", x0, y - 24f, 1000f);
            float fx = x0 + 60f, floorY = y + 300f;
            foreach (FigureDef f in Ocb.Figures)
            {
                float cxf = fx + f.Cx;
                Ui.Place(Ocb.FootLight(pg, f.Body * 2f, f.IsFoe).rectTransform, cxf - f.Body, floorY - 30f);
                Ui.Place(Ocb.Figure(pg, f.Name).rectTransform, fx, floorY - f.H);
                Note(pg, f.Name + " " + f.W + "×" + f.H, fx, floorY + 10f, 200f);
                fx += f.W + 80f;
            }

            Note(pg, "TargetFrame: default, unreachable, explore selection (132×146, hook 20, glow 12), pick (236×324, hook 24)", x0 + 1240f, y - 24f, 660f);
            At(Ocb.TargetFrame(pg, 140f, 240f), x0 + 1260f, y + 10f);
            At(Ocb.TargetFrame(pg, 140f, 240f, new OcbFrameOpts { Reachable = false }), x0 + 1420f, y + 10f);
            At(Ocb.TargetFrame(pg, 132f, 146f, new OcbFrameOpts { Hook = 20f, Width = 3f, Glow = 12f }), x0 + 1580f, y + 10f);
            At(Ocb.TargetFrame(pg, 236f, 324f, new OcbFrameOpts { Hook = 24f, Width = 3f }), x0 + 1500f, y + 400f);
            Ocb.ThrowLine(pg, x0 + 60f, floorY + 60f, 1100f);
            Note(pg, ".ocb-throwline", x0 + 60f, floorY + 70f, 200f);
            At(Ocb.Banner(pg, "敵のターン", true), 0f, floorY + 110f);
        }
    }
}
