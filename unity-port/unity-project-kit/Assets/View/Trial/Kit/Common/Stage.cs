// stage.js (src/scenes/common/stage.js), the shared scene utilities, for the Unity stage. Most of it already lives in
// the kit and is reached from here under its JS name:
//   W, H · place → Ui.Place · createStage → TrialStage.Create · EASE → Trial.Ease · createClock → a clock on the stage's
//   ClockDriver · pushKeys → KeyStack.Push · the Tab-focus watch → Focus (always on)
// and the two pieces that are this file's own: noticeLine (the app's one-line notice) and confirmCard (the confirm card
// with its key rules). h() has no single counterpart: Ui.Div makes an element, Label.Make a text node.
using System;
using System.Collections.Generic;
using TMPro;
using Trial.Geometry;
using UnityEngine;

namespace Trial.Kit
{
    /// <summary>confirmCard's options (stage.js:178): { title, body, ok = '進む', cancel = 'やめる', danger }.</summary>
    public sealed class ConfirmOpts
    {
        public string Title;
        public string Body;
        /// <summary>The OK button's label (empty = 「進む」).</summary>
        public string Ok;
        /// <summary>The cancel button's label (empty = 「やめる」).</summary>
        public string Cancel;
        /// <summary>The OK button is .is-danger.</summary>
        public bool Danger;
        /// <summary>
        /// white-space of the body: normal in common.css; the exploration screen's stylesheet makes it pre-line
        /// (explore.css:397), which the scene passes here since a stylesheet cannot reach the card in the kit.
        /// </summary>
        public WhiteSpace BodyWhiteSpace = WhiteSpace.Normal;
    }

    /// <summary>An open confirm card: its elements (the DOM the JS queries) and its answer.</summary>
    public sealed class ConfirmCardView
    {
        public RectTransform Host { get; internal set; }
        /// <summary>.ocbg-confirm: the full-host shade (z 900) holding the card.</summary>
        public RectTransform Shade { get; internal set; }
        /// <summary>.ocbg-confirm-card (explore animates it: ocbg-explore-plate-in).</summary>
        public RectTransform Card { get; internal set; }
        public UiNode CardNode { get; internal set; }
        /// <summary>.ocbg-confirm-row.</summary>
        public RectTransform Row { get; internal set; }
        /// <summary>The h3, or null without a title.</summary>
        public TextMeshProUGUI Title { get; internal set; }
        public TextMeshProUGUI Body { get; internal set; }
        public OcbgButton CancelButton { get; internal set; }
        public OcbgButton OkButton { get; internal set; }
        /// <summary>The Tab scope of the card's two buttons (cancel, then OK).</summary>
        public FocusScope Scope { get; internal set; }
        /// <summary>The answer: true = OK.</summary>
        public Promise<bool> Answer { get; internal set; }
        /// <summary>Not answered yet and still on screen.</summary>
        public bool IsOpen => !Answered && Shade;
        public bool Answered { get; internal set; }

        /// <summary>The card's button Tab moved the focus to, or null (stage.js `focused()`).</summary>
        public OcbgButton FocusedButton
        {
            get
            {
                Focusable f = Focus.Current;
                if (f == null) return null;
                if (OkButton != null && f == OkButton.Focusable) return OkButton;
                if (CancelButton != null && f == CancelButton.Focusable) return CancelButton;
                return null;
            }
        }

        /// <summary>`.ocbg-btn:not(.is-primary)`.click(): answers false (main.js reloadStored, explore's destroy).</summary>
        public void Cancel() => CancelButton?.Press();

        /// <summary>The OK button's click.</summary>
        public void Confirm() => OkButton?.Press();
    }

    /// <summary>A notice line (stage.js noticeLine).</summary>
    public sealed class NoticeView
    {
        public RectTransform Root { get; internal set; }
        public UiNode Node { get; internal set; }
        public BoxGraphic Box { get; internal set; }
        public TextMeshProUGUI Text { get; internal set; }
        public CssStates States { get; internal set; }

        /// <summary>classList.add('is-leaving'): fades out over .6s (the rule's transition).</summary>
        public void Leave()
        {
            if (Root) States.SetState(".is-leaving", true);
        }

        /// <summary>el.remove().</summary>
        public void Remove()
        {
            if (Root) Ui.Remove(Root);
        }
    }

    public static class Stage
    {
        // js: scenes/common/stage.js:4
        public const float W = 1920f;
        // js: scenes/common/stage.js:5
        public const float H = 1080f;

        private static readonly List<ConfirmCardView> Open = new List<ConfirmCardView>();

        // js: scenes/common/stage.js:34
        /// <summary>place(el, x, y, w, h): left / top in stage px, and the size when given.</summary>
        public static RectTransform Place(RectTransform el, float x, float y, float? w = null, float? h = null) => Ui.Place(el, x, y, w, h);

        // js: scenes/common/stage.js:47
        /// <summary>createStage(container): the 1920×1080 stage scaled to the screen, letterboxed with --ground.</summary>
        public static TrialStage CreateStage(string name = "TrialStage") => TrialStage.Create(name);

        // js: scenes/common/stage.js:87
        /// <summary>
        /// createClock(onFrame): a scene clock in stage time (tweens, waits; Stop(true) resolves what is pending, as
        /// clock.stop() does), stepped every frame; onFrame(dt, now) with dt clamped to 50 ms.
        /// </summary>
        public static TrialClock CreateClock(Action<float, float> onFrame = null)
        {
            TrialClock c = CssTime.Driver.CreateClock();
            if (onFrame != null) c.OnFrame((dtScene, dtReal) => onFrame(dtReal, c.Now));
            return c;
        }

        // js: scenes/common/stage.js:136
        /// <summary>pushKeys(handler): the top handler gets keys first; true swallows. Returns the pop function.</summary>
        public static Action PushKeys(Func<KeyEvent, bool> handler) => KeyStack.Push(handler);

        // ---- noticeLine ----------------------------------------------------------------------------------------

        // js: scenes/common/stage.js:152
        /// <summary>
        /// noticeLine(text, x, y, w, ms): a one-line notice at stage coordinates (the app's own word about the game:
        /// nothing can be saved, a save failed). ms > 0: it starts to fade at ms − 600 and leaves at ms (real time, as
        /// setTimeout). The JS returns a detached element the caller appends; here the caller names the parent.
        /// </summary>
        public static NoticeView NoticeLine(RectTransform parent, string text, float x, float y, float w, float ms = 0f)
        {
            var v = new NoticeView();
            v.Root = Ui.Div(parent, "ocbg-notice");
            v.Node = UiNode.Of(v.Root);
            v.Box = BoxGraphic.On(v.Root, CommonStyles.NoticeCss);
            BoxStyle s = v.Box.Style;
            Insets bw = s.BorderWidths, pad = s.Padding;
            float contentW = Math.Max(0f, w - bw.Horizontal - pad.Horizontal);   // box-sizing: border-box
            TextStyle ts = CommonStyles.NoticeText();
            int lines = TextMeasure.Lines(text ?? "", ts, contentW);
            float textH = lines * ts.LineHeightPx;
            v.Text = Label.Make(v.Root, text ?? "", ts, bw.Left + pad.Left, bw.Top + pad.Top, contentW, textH, "text");
            v.Node.Place(x, y, w, bw.Vertical + pad.Vertical + textH);
            v.States = CssStates.Of(v.Root);
            v.States.Base(CommonStyles.NoticeCss).State(".is-leaving", CommonStyles.NoticeLeavingCss);
            v.States.Apply(instant: true);
            v.Node.Z = 880;
            if (ms > 0f)
            {
                TrialClock clock = CssTime.Clock;
                clock.WaitReal(Math.Max(0f, ms - 600f)).Then(() => v.Leave());
                clock.WaitReal(ms).Then(() => v.Remove());
            }
            return v;
        }

        // ---- confirmCard ---------------------------------------------------------------------------------------

        // js: scenes/common/stage.js:183
        /// <summary>
        /// confirmCard(host, opts): a confirm card over host (the screens never use window.confirm). Resolves true / false.
        /// Enter confirms and Esc cancels, but a button of the card that Tab moved the focus to takes Enter (and Space)
        /// itself: a focused 「やめる」 never confirms (EXTENSIONS X-8). Only a fresh press answers: repeats of a held
        /// key, and Ctrl / Meta / Alt + Enter or Space, are swallowed. Other keys pass to the handlers below.
        /// </summary>
        public static Promise<bool> ConfirmCard(RectTransform host, ConfirmOpts opts) => OpenConfirm(host, opts).Answer;

        /// <summary>confirmCard, with the card's elements (OpenConfirms, Cancel, FocusedButton) for the screens that query them.</summary>
        public static ConfirmCardView OpenConfirm(RectTransform host, ConfirmOpts opts)
        {
            if (!host) throw new ArgumentNullException(nameof(host));
            opts = opts ?? new ConfirmOpts();
            var v = new ConfirmCardView { Host = host };
            var answer = new PromiseSource<bool>();
            v.Answer = answer.Promise;

            // .ocbg-confirm: abs inset 0, z 900, flex centre, the scrim
            Rect hr = host.rect;
            float hw = hr.width > 0f ? hr.width : W, hh = hr.height > 0f ? hr.height : H;
            v.Shade = Ui.Div(host, "ocbg-confirm");
            UiNode shadeNode = UiNode.Of(v.Shade);
            shadeNode.Place(0f, 0f, hw, hh);
            BoxGraphic shadeBox = BoxGraphic.On(v.Shade, CommonStyles.ConfirmCss);
            shadeBox.raycastTarget = true;   // the shade takes the pointer: nothing under it is clicked

            // .ocbg-confirm-card.ocb-solid: width 760, padding 32 36 28, shadow-tip (border-box)
            v.Card = Ui.Div(v.Shade, "ocbg-confirm-card");
            v.CardNode = UiNode.Of(v.Card);
            BoxGraphic cardBox = BoxGraphic.On(v.Card);
            cardBox.Style = CommonStyles.ConfirmCardBox();
            Insets bw = cardBox.Style.BorderWidths, pad = cardBox.Style.Padding;
            const float cardW = 760f;
            float x0 = bw.Left + pad.Left;
            float innerW = cardW - bw.Horizontal - pad.Horizontal;
            float y = bw.Top + pad.Top;

            if (!string.IsNullOrEmpty(opts.Title))
            {
                TextStyle h3 = CommonStyles.ConfirmTitle();
                float th = TextMeasure.Lines(opts.Title, h3, innerW) * h3.LineHeightPx;
                v.Title = Label.Make(v.Card, opts.Title, h3, x0, y, innerW, th, "h3");
                y += th + 12f;   // margin-bottom 12
            }
            TextStyle p = CommonStyles.ConfirmBody();
            p.WhiteSpace = opts.BodyWhiteSpace;
            string body = opts.Body ?? "";
            float bh = TextMeasure.Lines(body, p, innerW) * p.LineHeightPx;   // an empty <p> is 0 tall
            v.Body = Label.Make(v.Card, body, p, x0, y, innerW, bh, "p");
            y += bh + 28f;   // .ocbg-confirm-row margin-top 28

            // .ocbg-confirm-row: flex, justify-content flex-end, gap 16; the buttons stretch to the row's height
            v.Row = Ui.Div(v.Card, "ocbg-confirm-row");
            v.CancelButton = OcbgButton.Make(v.Row, string.IsNullOrEmpty(opts.Cancel) ? "やめる" : opts.Cancel, false, false, "cancel");
            v.OkButton = OcbgButton.Make(v.Row, string.IsNullOrEmpty(opts.Ok) ? "進む" : opts.Ok, true, opts.Danger, "ok");
            Vector2 cs = v.CancelButton.NaturalSize, os = v.OkButton.NaturalSize;
            float rowH = Math.Max(cs.y, os.y);
            Ui.Place(v.Row, x0, y, innerW, rowH);
            v.OkButton.SetBox(innerW - os.x, 0f, os.x, rowH);
            v.CancelButton.SetBox(innerW - os.x - 16f - cs.x, 0f, cs.x, rowH);
            y += rowH + pad.Bottom + bw.Bottom;
            float cardH = y;
            v.CardNode.Place((hw - cardW) * 0.5f, (hh - cardH) * 0.5f, cardW, cardH);

            shadeNode.Z = 900;

            // Tab cycles the card's buttons in DOM order (cancel, OK)
            v.Scope = Focus.PushScope("confirm");
            v.CancelButton.FocusIn(v.Scope);
            v.OkButton.FocusIn(v.Scope);

            Action pop = null;
            void Done(bool ok)
            {
                if (v.Answered) return;
                v.Answered = true;
                pop?.Invoke();
                v.Scope.Dispose();
                Open.Remove(v);
                if (v.Shade) Ui.Remove(v.Shade);
                answer.TryResolve(ok);
            }

            v.CancelButton.Click += () => Done(false);
            v.OkButton.Click += () => Done(true);

            // js: scenes/common/stage.js:199
            pop = KeyStack.Push(e =>
            {
                if (e.Key == "Escape")
                {
                    if (!e.Repeat) Done(false);
                    return true;
                }
                if (e.Key == "Enter" || e.Key == " ")
                {
                    if (e.Repeat || e.CtrlKey || e.MetaKey || e.AltKey) return true;
                    OcbgButton b = v.FocusedButton;
                    if (b != null) { b.Press(); return true; }
                    if (e.Key == "Enter") { Done(true); return true; }
                    return true;
                }
                return false;
            });

            // A host destroyed under an open card takes the card with it (the JS element goes; its promise and key
            // handler stay, as in the browser): the Tab scope must not outlive its buttons.
            v.Shade.gameObject.AddComponent<ConfirmShade>().OnGone = () =>
            {
                if (v.Answered) return;
                v.Scope.Dispose();
                Open.Remove(v);
            };

            Open.Add(v);
            return v;
        }

        /// <summary>Every confirm card on screen, in the order they opened (document.querySelectorAll('.ocbg-confirm')).</summary>
        public static IReadOnlyList<ConfirmCardView> OpenConfirms
        {
            get
            {
                Open.RemoveAll(c => !c.Shade);
                return Open;
            }
        }

        /// <summary>The cards on screen under a root (root.querySelectorAll('.ocbg-confirm')); null = anywhere.</summary>
        public static List<ConfirmCardView> ConfirmsIn(Transform root)
        {
            var o = new List<ConfirmCardView>();
            foreach (ConfirmCardView c in OpenConfirms)
                if (c.Shade && (root == null || c.Shade.IsChildOf(root))) o.Add(c);
            return o;
        }

        /// <summary>Whether a card is on screen under root (root.querySelector('.ocbg-confirm')).</summary>
        public static bool AnyConfirmOpen(Transform root = null) => ConfirmsIn(root).Count > 0;

        /// <summary>The last card opened under root, or null.</summary>
        public static ConfirmCardView LastConfirmIn(Transform root = null)
        {
            List<ConfirmCardView> l = ConfirmsIn(root);
            return l.Count > 0 ? l[l.Count - 1] : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Open.Clear();
        }
    }

    /// <summary>On a confirm shade: tells the card it was destroyed.</summary>
    [AddComponentMenu("")]
    public sealed class ConfirmShade : MonoBehaviour
    {
        public Action OnGone;

        private void OnDestroy()
        {
            try { OnGone?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
