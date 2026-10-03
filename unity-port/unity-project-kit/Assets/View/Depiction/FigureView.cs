// A standing figure shared by the player and enemies, with the one-glyph range tag
// ("近" / "遠") at its feet. It wears the art filed under the id the script names (#288), or the
// ProceduralArt silhouette when there is none. Which way the body faces, the shapes it takes and
// the fall are read from Script/FigureMotion.cs; this view only applies them.
#if UNITY_2021_2_OR_NEWER
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    /// <summary>
    /// One character's pictures (#288): the four the figure can wear. Only <see cref="idle"/> is
    /// required; a pose with no picture shows the idle one. Tools &gt; Depiction &gt; Collect Character
    /// Art fills these from Assets/Art/Characters/&lt;id&gt;/ (CharacterArt names the files).
    /// </summary>
    [System.Serializable]
    public class FigureArt
    {
        [Tooltip("The id the script names (the enemy's id), as the folder under Assets/Art/Characters is named.")]
        public string id = "";
        public Sprite idle;
        public Sprite act;
        public Sprite hit;
        public Sprite down;

        public Sprite For(FigurePose pose)
        {
            Sprite sprite = pose == FigurePose.Act ? act : pose == FigurePose.Hit ? hit : pose == FigurePose.Down ? down : null;
            return sprite != null ? sprite : idle;
        }
    }

    public class FigureView : MonoBehaviour
    {
        public UnitSide side;
        public bool useSpearSilhouette;
        [Tooltip("Tints the silhouette toward the role colour so it reads against the dark backdrop. Real art is never tinted.")]
        public bool tintByRole = true;
        public Image body;
        public RectTransform chest;
        public RectTransform head;
        public RectTransform rangeTag;
        public Image rangeTagFrame;
        public Text rangeGlyph;
        [Tooltip("Corner brackets shown while a throw-line card that lands on this figure is held.")]
        public TargetMarkView targetMark;
        [Tooltip("The art this figure can wear, by id (#288). Filled by Tools > Depiction > Collect Character Art.")]
        public FigureArt[] artShelf = new FigureArt[0];

        public RectTransform Rect => (RectTransform)transform;
        public Color RoleColor => side == UnitSide.Player ? BattleTheme.Accent : BattleTheme.Omen;

        /// <summary>True while the figure wears real art rather than the silhouette.</summary>
        public bool WearsArt => _art != null;

        /// <summary>
        /// The body's scale at rest. Art faces the way it is drawn and is never mirrored; the silhouette
        /// is drawn facing right, so the enemy's is (FigureFacing). Every move returns the body here.
        /// </summary>
        public Vector3 BodyHomeScale => new Vector3(FigureFacing.HomeScaleX(side, WearsArt), 1f, 1f);

        /// <summary>The body's colour at rest: untinted for art, the role tint for the silhouette.</summary>
        public Color BodyHomeColor => WearsArt ? Color.white : _placeholderColor;

        private bool _placeholderTaken;
        private Sprite _placeholder;
        private Color _placeholderColor = Color.white;
        private FigureArt _art;
        private string _artId = "";
        private bool _down;
        private Image _flashMask;
        private Image _flashFill;
        private int _flashes;
        private bool _falling;

        private void Awake()
        {
            TakePlaceholder();
            RestBody();
        }

        /// <summary>
        /// Wears the art filed under <paramref name="artId"/> (the script's UnitFrame.ArtId). An empty
        /// id, or one with no idle picture on the shelf, wears the silhouette. Asking for the id already
        /// worn changes nothing, so a frame can call it every time.
        /// </summary>
        public void ShowArt(string artId)
        {
            artId = artId ?? "";
            if (_placeholderTaken && artId == _artId) return;
            _artId = artId;
            Wear(Find(artId));
        }

        /// <summary>Replacement point for one picture of real art, outside the shelf.</summary>
        public void SetSprite(Sprite sprite)
        {
            if (sprite == null) return;
            _artId = "";
            Wear(new FigureArt { idle = sprite });
        }

        /// <summary>Shows the picture for <paramref name="pose"/>. The silhouette has one picture and ignores it.</summary>
        public void SetPose(FigurePose pose)
        {
            if (_art == null || !body) return;
            if (_falling && pose != FigurePose.Down) return; // a recoil still ending must not stand the fallen back up
            Sprite sprite = _art.For(pose);
            if (sprite != null && body.sprite != sprite) body.sprite = sprite;
        }

        /// <summary>Puts the body back at rest: its facing, no tilt, its colour, the idle picture.</summary>
        public void RestBody()
        {
            if (!body) return;
            RectTransform rt = body.rectTransform;
            rt.localScale = BodyHomeScale;
            rt.localRotation = Quaternion.identity;
            body.color = BodyHomeColor;
            SetPose(FigurePose.Idle);
        }

        /// <summary>The glyph is written by the script ("近" / "遠"); this view only prints it.</summary>
        public void SetRange(bool hasRange, string glyph)
        {
            if (rangeTag) rangeTag.gameObject.SetActive(hasRange);
            if (!hasRange) return;
            if (string.IsNullOrEmpty(glyph)) Debug.LogError("[Depiction] " + name + ": the script gave no range glyph");
            if (rangeGlyph)
            {
                rangeGlyph.text = glyph;
                rangeGlyph.color = RoleColor;
            }
            if (rangeTagFrame) rangeTagFrame.color = RoleColor;
        }

        /// <summary>Flips the tag over in <paramref name="ms"/> and swaps the glyph at the half-way point.</summary>
        public IEnumerator FlipRangeTag(string glyph, float ms)
        {
            if (!rangeTag) yield break;
            yield return UiTween.Run(ms * 0.5f, Ease.In, t => { if (rangeTag) rangeTag.localScale = new Vector3(1f - t, 1f, 1f); });
            if (rangeGlyph) rangeGlyph.text = glyph;
            yield return UiTween.Run(ms * 0.5f, Ease.Out, t => { if (rangeTag) rangeTag.localScale = new Vector3(t, 1f, 1f); });
        }

        // ---- the struck flash ---------------------------------------------------------------

        /// <summary>
        /// The struck figure blinks <paramref name="color"/> and back (EffectId.HitFlash). The silhouette
        /// is tinted, as before. Painted art would barely show a tint (the colour multiplies the paint),
        /// so a silhouette of the picture itself is laid over it in the colour and fades out.
        /// </summary>
        public IEnumerator Flash(Color color, float ms)
        {
            if (!body) yield break;
            if (!WearsArt)
            {
                yield return DepictionFx.Flash(body, color, ms);
                yield break;
            }
            Image fill = FlashFill();
            _flashMask.gameObject.SetActive(true);
            _flashes++;
            yield return UiTween.Run(ms, Ease.Out, t =>
            {
                // The recoil swaps in the hit picture after the flash starts: the cut follows the picture worn.
                if (_flashMask && body && _flashMask.sprite != body.sprite) _flashMask.sprite = body.sprite;
                if (fill) fill.color = BattleTheme.WithAlpha(color, 0.85f * (1f - t));
            });
            _flashes--;
            if (_flashes <= 0 && _flashMask) _flashMask.gameObject.SetActive(false);
        }

        /// <summary>
        /// A Mask cut to the picture's alpha with a plain fill under it: a hard-edged silhouette in any
        /// colour, with the stock UI shader (no shader of our own to ship).
        /// </summary>
        private Image FlashFill()
        {
            if (_flashFill) return _flashFill;
            RectTransform maskRect = UiKit.Rect(body.rectTransform, "FlashMask", Vector2.zero, Vector2.one);
            _flashMask = maskRect.gameObject.AddComponent<Image>();
            _flashMask.preserveAspect = true;
            _flashMask.raycastTarget = false;
            var mask = maskRect.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            _flashFill = UiKit.Fill(maskRect, "Fill", BattleTheme.WithAlpha(Color.white, 0f));
            maskRect.gameObject.SetActive(false);
            return _flashFill;
        }

        // ---- the fall -----------------------------------------------------------------------

        /// <summary>
        /// The fall (EffectId.Defeat, battle_ui_ux_v2 §2.2 の 6): the figure tilts about its feet, sinks
        /// and darkens, then fades out, and is gone with its cell empty. DefeatShape gives every number.
        /// </summary>
        public IEnumerator Fall(float ms)
        {
            // The killing blow's flash and recoil may still be running (they run beside the flow): the
            // fall starts from the body at rest, not from a tint or a lean caught half-way.
            _flashes = 0;
            if (_flashMask) _flashMask.gameObject.SetActive(false);
            RestBody();
            _falling = true;
            SetPose(FigurePose.Down);
            RectTransform rt = Rect;
            CanvasGroup group = UiKit.Group(rt);
            Vector2 from = rt.anchoredPosition;
            Color color = BodyHomeColor;
            yield return UiTween.Run(ms, Ease.Linear, t =>
            {
                if (!rt) return;
                rt.localRotation = Quaternion.Euler(0f, 0f, DefeatShape.Tilt(side, t));
                rt.anchoredPosition = from + new Vector2(0f, DefeatShape.Sink(t));
                if (group) group.alpha = DefeatShape.Alpha(t);
                if (!body) return;
                float b = DefeatShape.Brightness(t);
                body.color = new Color(color.r * b, color.g * b, color.b * b, color.a);
            });
            if (rt) rt.anchoredPosition = from;
            SetDown(true);
        }

        /// <summary>
        /// Gone (the frame's UnitFrame.Down) or standing. A fallen figure is hidden whole — tag and
        /// brackets with it — and comes back at rest when a new battle stands it up again.
        /// </summary>
        public void SetDown(bool down)
        {
            if (down == _down && gameObject.activeSelf == !down) return;
            _down = down;
            Settle();
            gameObject.SetActive(!down);
        }

        /// <summary>
        /// Clears whatever a stopped effect left on the figure (a new battle stops every coroutine
        /// mid-way): the flash, the fall's tilt and fade, and the body's move. Position is the caller's.
        /// </summary>
        public void Settle()
        {
            _falling = false;
            _flashes = 0;
            if (_flashMask) _flashMask.gameObject.SetActive(false);
            Rect.localRotation = Quaternion.identity;
            CanvasGroup group = GetComponent<CanvasGroup>();
            if (group) group.alpha = 1f;
            RestBody();
        }

        // ---- wearing art --------------------------------------------------------------------

        private FigureArt Find(string artId)
        {
            if (string.IsNullOrEmpty(artId) || artShelf == null) return null;
            foreach (FigureArt art in artShelf)
            {
                if (art != null && art.id == artId && art.idle != null) return art;
            }
            return null;
        }

        private void Wear(FigureArt art)
        {
            if (!body) return;
            TakePlaceholder();
            _art = art;
            if (art != null)
            {
                body.sprite = art.idle;
                body.preserveAspect = true;
            }
            else
            {
                body.sprite = _placeholder;
            }
            RestBody();
        }

        /// <summary>Keeps the silhouette and its tint, so a figure that loses its art (the next battle's enemy has none) can go back to it.</summary>
        private void TakePlaceholder()
        {
            if (_placeholderTaken || !body) return;
            if (body.sprite == null)
            {
                body.sprite = useSpearSilhouette ? ProceduralArt.FigureWithSpear : ProceduralArt.Figure;
                if (tintByRole) body.color = Color.Lerp(BattleTheme.InkBlack, RoleColor, 0.42f);
            }
            _placeholder = body.sprite;
            _placeholderColor = body.color;
            _placeholderTaken = true;
        }
    }
}
#endif
