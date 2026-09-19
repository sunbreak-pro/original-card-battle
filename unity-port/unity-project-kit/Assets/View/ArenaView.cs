// L0 Background + L1 Arena: floor palette, two mist layers, wet floor, embers,
// the two figures whose spacing IS the distance, the omen banner above the enemy,
// the target band on the floor, the player's ghost, and floating numbers.
#if UNITY_2021_2_OR_NEWER
using System.Collections;
using System.Collections.Generic;
using BattleCore;
using UnityEngine;
using UnityEngine.UI;

public sealed class FigureView
{
    public RectTransform Root;   // pivot at the feet
    public RectTransform Body;   // rotates / scales for posture
    public Image Rim;
    public Image Fill;
    public Image Flash;
    public CanvasGroup Group;
    public float Sign;           // -1 = player (left), +1 = enemy (right)
    public float Rot;
    public float ScaleY;

    public void ApplyPosture(float rot, float scaleY)
    {
        Rot = rot;
        ScaleY = scaleY;
        // Player (Sign −1) leans clockwise toward the right; enemy (Sign +1) counter-clockwise toward the left.
        Body.localRotation = Quaternion.Euler(0f, 0f, rot * Sign);
        Body.localScale = new Vector3(Sign > 0 ? -1f : 1f, scaleY, 1f);
    }
}

public sealed class ArenaView
{
    public const float FigureWidth = 140f;
    public const float FigureHeight = 300f;
    private const float ArenaWidth = BattleTheme.RefWidth;

    private readonly MonoBehaviour _host;
    private RectTransform _background;
    private RectTransform _arena;
    private Image _gradient;
    private RectTransform _mistNear;
    private RectTransform _mistFar;
    private Image _mistNearImg;
    private Image _mistFarImg;
    private Image _floor;
    private RectTransform _emberRoot;
    private readonly List<RectTransform> _embers = new List<RectTransform>();
    private readonly List<float> _emberPhase = new List<float>();
    private bool _embersOn;

    public FigureView Player { get; private set; }
    public FigureView Enemy { get; private set; }
    private FigureView _ghost;
    private Image _playerGlow;
    private Image _enemyGlow;
    private float _glowScale = 1f;

    private RectTransform _banner;
    private CanvasGroup _bannerGroup;
    private Image _bannerBg;
    private Image _bannerIcon;
    private Text _bannerText;
    private Image _bannerStrike;
    private Text _bannerNote;

    private RectTransform _band;
    private Image _bandFill;
    private RectTransform _bandDashes;
    private CanvasGroup _bandGroup;
    private Text _bandText;
    private Text _distanceLabel;

    private int _floorShown = -1;
    private float _time;
    private RangeBand _range = RangeBand.Mid;
    private float _bannerBaseOffset = 34f;

    public ArenaView(MonoBehaviour host)
    {
        _host = host;
    }

    // ---- build -----------------------------------------------------------------

    public void Build(RectTransform backgroundLayer, RectTransform arenaLayer)
    {
        _background = backgroundLayer;
        _arena = arenaLayer;

        _gradient = UiKit.Fill(_background, "Gradient", Color.white);

        // Mist: two horizontally tiled, seamless layers scrolling at different speeds (parallax).
        var mistMask = UiKit.Box(_background, "MistMask", 0f, BattleTheme.Arena.x, 1f, BattleTheme.Arena.y);
        mistMask.gameObject.AddComponent<RectMask2D>();
        _mistFarImg = MakeMist(mistMask, "MistFar", 0.15f, 0.55f);
        _mistFar = _mistFarImg.rectTransform;
        _mistNearImg = MakeMist(mistMask, "MistNear", 0.0f, 0.45f);
        _mistNear = _mistNearImg.rectTransform;

        // Floor: wet cobble band from the arena bottom up to the floor line (+ a little).
        _floor = UiKit.Image(_arena, "Floor", ProceduralArt.FloorNoise, Color.white,
            new Vector2(0f, 0f), new Vector2(1f, BattleTheme.FloorFraction + 0.06f));
        _floor.type = Image.Type.Tiled;
        var floorLine = UiKit.Image(_arena, "FloorLine", ProceduralArt.White, BattleTheme.WithAlpha(BattleTheme.Warm, 0.18f),
            new Vector2(0f, BattleTheme.FloorFraction), new Vector2(1f, BattleTheme.FloorFraction));
        floorLine.rectTransform.offsetMin = new Vector2(0f, -1f);
        floorLine.rectTransform.offsetMax = new Vector2(0f, 1f);

        _emberRoot = UiKit.Box(_arena, "Embers", 0f, 0f, 1f, 1f);
        for (int i = 0; i < 14; i++)
        {
            var e = UiKit.Sprite(_emberRoot, $"Ember{i}", ProceduralArt.SoftCircle, BattleTheme.WithAlpha(BattleTheme.Warm, 0.7f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(8f, 8f), Vector2.zero);
            _embers.Add(e.rectTransform);
            _emberPhase.Add(i / 14f);
        }
        _emberRoot.gameObject.SetActive(false);

        // Lantern glows at the feet.
        _playerGlow = MakeGlow("PlayerGlow");
        _enemyGlow = MakeGlow("EnemyGlow");

        // Target band (omen aim) sits on the floor under the player.
        _band = UiKit.Point(_arena, "TargetBand", new Vector2(0.5f, BattleTheme.FloorFraction), new Vector2(0.5f, 0.5f),
            new Vector2(ArenaWidth * 0.22f, 14f), Vector2.zero);
        _bandGroup = UiKit.Group(_band);
        _bandFill = UiKit.Fill(_band, "Fill", BattleTheme.Omen);
        _bandDashes = UiKit.Rect(_band, "Dashes", Vector2.zero, Vector2.one);
        for (int i = 0; i < 12; i++)
        {
            float x = (i + 0.5f) / 12f;
            var dash = UiKit.Image(_bandDashes, $"Dash{i}", ProceduralArt.White, BattleTheme.Whiff,
                new Vector2(x, 0f), new Vector2(x, 1f));
            dash.rectTransform.offsetMin = new Vector2(-12f, 0f);
            dash.rectTransform.offsetMax = new Vector2(12f, 0f);
        }
        _bandText = UiKit.Label(_band, "Text", 17, TextAnchor.UpperCenter, BattleTheme.Ink,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(520f, 26f), new Vector2(0f, -3f));
        _bandGroup.alpha = 0f;

        // Figures.
        _ghost = MakeFigure("Ghost", -1f, ProceduralArt.Figure, BattleTheme.Accent);
        _ghost.Group.alpha = 0f;
        Player = MakeFigure("PlayerFigure", -1f, ProceduralArt.Figure, BattleTheme.Accent);
        Enemy = MakeFigure("EnemyFigure", 1f, ProceduralArt.FigureWithSpear, BattleTheme.Omen);

        _distanceLabel = UiKit.Label(_arena, "DistanceLabel", 26, TextAnchor.MiddleCenter, BattleTheme.Ink2,
            new Vector2(0.5f, BattleTheme.FloorFraction), new Vector2(0.5f, 0f), new Vector2(300f, 34f), new Vector2(0f, 10f), "間合い 中");

        // Omen banner (floats above the enemy; positioned every frame so posture tilt does not tilt it).
        _banner = UiKit.Point(_arena, "OmenBanner", new Vector2(0.5f, BattleTheme.FloorFraction), new Vector2(0.5f, 0f),
            new Vector2(440f, 64f), Vector2.zero);
        _bannerGroup = UiKit.Group(_banner);
        var inkEdge = UiKit.Fill(_banner, "InkEdge", BattleTheme.InkBlack).rectTransform;
        inkEdge.offsetMin = new Vector2(-2f, -2f);
        inkEdge.offsetMax = new Vector2(2f, 2f);
        _bannerBg = UiKit.Fill(_banner, "Bg", BattleTheme.WithAlpha(BattleTheme.Omen, 0.85f));
        _bannerIcon = UiKit.Sprite(_banner, "Icon", ProceduralArt.Icon(ProceduralArt.IconKind.Sword), BattleTheme.Ink,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 30f), new Vector2(12f, 0f));
        _bannerText = UiKit.Text(_banner, "Text", 24, TextAnchor.MiddleLeft, BattleTheme.Ink);
        _bannerText.rectTransform.offsetMin = new Vector2(52f, 4f);
        _bannerText.fontStyle = FontStyle.Bold;
        _bannerStrike = UiKit.Image(_banner, "Strike", ProceduralArt.White, BattleTheme.Ink,
            new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.5f));
        _bannerStrike.rectTransform.offsetMin = new Vector2(0f, -2f);
        _bannerStrike.rectTransform.offsetMax = new Vector2(0f, 2f);
        _bannerStrike.gameObject.SetActive(false);
        _bannerNote = UiKit.Label(_banner, "Note", 18, TextAnchor.UpperCenter, BattleTheme.Ink,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(440f, 28f), new Vector2(0f, -2f));
        _bannerGroup.alpha = 0f;

        SetFloor(1);
        PlaceFigures(RangeBand.Mid);
    }

    private Image MakeMist(RectTransform parent, string name, float yMin, float yMax)
    {
        var rt = UiKit.Rect(parent, name, new Vector2(0f, yMin), new Vector2(0f, yMax));
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(ArenaWidth * 2f, 0f);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = ProceduralArt.Mist;
        img.type = Image.Type.Tiled;
        img.raycastTarget = false;
        return img;
    }

    private Image MakeGlow(string name)
    {
        return UiKit.Sprite(_arena, name, ProceduralArt.Glow, BattleTheme.WithAlpha(BattleTheme.Warm, 0.35f),
            new Vector2(0.5f, BattleTheme.FloorFraction), new Vector2(0.5f, 0.5f), new Vector2(520f, 150f), Vector2.zero);
    }

    private FigureView MakeFigure(string name, float sign, Sprite sprite, Color color)
    {
        var f = new FigureView { Sign = sign };
        f.Root = UiKit.Point(_arena, name, new Vector2(0.5f, BattleTheme.FloorFraction), new Vector2(0.5f, 0f),
            new Vector2(FigureWidth, FigureHeight), Vector2.zero);
        f.Group = UiKit.Group(f.Root);
        f.Body = UiKit.Rect(f.Root, "Body", Vector2.zero, Vector2.one);
        f.Body.pivot = new Vector2(0.5f, 0f);
        f.Body.anchorMin = new Vector2(0.5f, 0f);
        f.Body.anchorMax = new Vector2(0.5f, 0f);
        f.Body.sizeDelta = new Vector2(FigureWidth, FigureHeight);
        // Rim light: the same silhouette, shifted toward the lantern side, warm and half transparent.
        f.Rim = UiKit.Image(f.Body, "Rim", sprite, BattleTheme.WithAlpha(BattleTheme.Warm, 0.5f), Vector2.zero, Vector2.one);
        f.Rim.rectTransform.offsetMin = new Vector2(-1.5f, -1f);
        f.Rim.rectTransform.offsetMax = new Vector2(-1.5f, 1f);
        f.Fill = UiKit.Image(f.Body, "Fill", sprite, color, Vector2.zero, Vector2.one);
        f.Flash = UiKit.Image(f.Body, "Flash", sprite, BattleTheme.WithAlpha(Color.white, 0f), Vector2.zero, Vector2.one);
        f.ApplyPosture(0f, 1f);
        return f;
    }

    // ---- per-frame ambience ------------------------------------------------------------

    public void Tick(float dt)
    {
        _time += dt;
        float w = ArenaWidth;
        _mistFar.anchoredPosition = new Vector2(-Mathf.Repeat(_time * 12f, w), 0f);
        _mistNear.anchoredPosition = new Vector2(-Mathf.Repeat(_time * 34f, w), 0f);

        float flicker = 1f + Mathf.Sin(_time / 1.8f * Mathf.PI * 2f) * 0.06f;
        _playerGlow.rectTransform.localScale = new Vector3(flicker * _glowScale, flicker * _glowScale, 1f);
        _enemyGlow.rectTransform.localScale = new Vector3(flicker, flicker, 1f);

        if (_embersOn)
        {
            float h = _arena.rect.height;
            for (int i = 0; i < _embers.Count; i++)
            {
                float p = Mathf.Repeat(_time / (4f + i % 4) + _emberPhase[i], 1f);
                float x = Mathf.Lerp(-w * 0.45f, w * 0.45f, (i + 0.5f) / _embers.Count) + Mathf.Sin(_time * 0.9f + i) * 40f;
                _embers[i].anchoredPosition = new Vector2(x, p * h);
                var img = _embers[i].GetComponent<Image>();
                img.color = BattleTheme.WithAlpha(BattleTheme.Warm, (1f - p) * (0.5f + 0.5f * Mathf.Sin(_time * 6f + i)));
            }
        }

        // Banner follows the enemy's head.
        float top = FigureHeight * Enemy.ScaleY + _bannerBaseOffset;
        _banner.anchoredPosition = new Vector2(Enemy.Root.anchoredPosition.x, top) + _bannerOffset;
    }

    private Vector2 _bannerOffset;

    // ---- settled state ----------------------------------------------------------------

    public void SetFloor(int floor)
    {
        if (floor == _floorShown) return;
        _floorShown = floor;
        var pal = BattleTheme.Floor(floor);
        _gradient.sprite = ProceduralArt.VerticalGradient(pal.Top, pal.Bottom);
        _gradient.color = Color.white;
        _mistFarImg.color = BattleTheme.WithAlpha(pal.Mist, pal.Mist.a * 0.8f);
        _mistNearImg.color = pal.Mist;
        _floor.color = Color.Lerp(pal.Bottom, Color.white, 0.22f);
        _embersOn = pal.Embers;
        _emberRoot.gameObject.SetActive(_embersOn);
    }

    public float PlayerX(RangeBand band) => -BattleTheme.FigureGap(band) / 2f;
    public float EnemyX(RangeBand band) => BattleTheme.FigureGap(band) / 2f;

    public void PlaceFigures(RangeBand band)
    {
        _range = band;
        Player.Root.anchoredPosition = new Vector2(PlayerX(band), 0f);
        Enemy.Root.anchoredPosition = new Vector2(EnemyX(band), 0f);
        var (rot, sy) = BattleTheme.Posture(band);
        Player.ApplyPosture(rot, sy);
        Enemy.ApplyPosture(rot, sy);
        _playerGlow.rectTransform.anchoredPosition = new Vector2(PlayerX(band), 0f);
        _enemyGlow.rectTransform.anchoredPosition = new Vector2(EnemyX(band), 0f);
        _band.anchoredPosition = new Vector2(PlayerX(band), 0f);
        _distanceLabel.text = $"間合い {Constants.RangeLabel[band]}";
    }

    public IEnumerator MoveTo(RangeBand to, bool clamped)
    {
        if (clamped)
        {
            // Bump 40 ms toward the edge and back: "can't go further".
            Vector2 p0 = Player.Root.anchoredPosition;
            Vector2 e0 = Enemy.Root.anchoredPosition;
            yield return UiTween.Run(40f, Ease.Out, t =>
            {
                Player.Root.anchoredPosition = p0 + new Vector2(-8f * Mathf.Sin(t * Mathf.PI), 0f);
                Enemy.Root.anchoredPosition = e0 + new Vector2(8f * Mathf.Sin(t * Mathf.PI), 0f);
            });
            yield break;
        }
        RangeBand from = _range;
        _range = to;
        float px0 = PlayerX(from), px1 = PlayerX(to);
        float ex0 = EnemyX(from), ex1 = EnemyX(to);
        var (r0, s0) = BattleTheme.Posture(from);
        var (r1, s1) = BattleTheme.Posture(to);
        bool labelSwapped = false;
        yield return UiTween.Run(320f, Ease.InOut, t =>
        {
            float px = Mathf.Lerp(px0, px1, t), ex = Mathf.Lerp(ex0, ex1, t);
            Player.Root.anchoredPosition = new Vector2(px, 0f);
            Enemy.Root.anchoredPosition = new Vector2(ex, 0f);
            _playerGlow.rectTransform.anchoredPosition = new Vector2(px, 0f);
            _enemyGlow.rectTransform.anchoredPosition = new Vector2(ex, 0f);
            _band.anchoredPosition = new Vector2(px, 0f);
            float pt = Mathf.Clamp01(t * 320f / 200f); // posture crossfade 200 ms
            Player.ApplyPosture(Mathf.Lerp(r0, r1, pt), Mathf.Lerp(s0, s1, pt));
            Enemy.ApplyPosture(Mathf.Lerp(r0, r1, pt), Mathf.Lerp(s0, s1, pt));
            if (!labelSwapped && t >= 150f / 320f)
            {
                labelSwapped = true;
                _distanceLabel.text = $"間合い {Constants.RangeLabel[to]}";
            }
        });
        PlaceFigures(to);
    }

    // ---- omen banner / target band ------------------------------------------------------

    public void SetOmen(OmenView omen)
    {
        if (omen == null)
        {
            _bannerGroup.alpha = 0f;
            _bandGroup.alpha = 0f;
            return;
        }
        _bannerText.text = omen.BannerText;
        _bannerBg.color = BattleTheme.WithAlpha(BattleTheme.Omen, 0.85f);
        _bannerIcon.sprite = ProceduralArt.Icon(omen.Kind switch
        {
            CardType.Guard => ProceduralArt.IconKind.Shield,
            CardType.Move => ProceduralArt.IconKind.Footprint,
            _ => ProceduralArt.IconKind.Sword,
        });
        _bannerStrike.gameObject.SetActive(false);
        _bannerNote.text = "";
        _bannerGroup.alpha = 1f;
        SetBand(omen.Diff, omen.FloorText);
    }

    /// <summary>Colour the floor band for a diff (0 hit / 1 half / 2 whiff); null hides it.</summary>
    public void SetBand(int? diff, string text)
    {
        if (!diff.HasValue)
        {
            _bandGroup.alpha = 0f;
            return;
        }
        _bandGroup.alpha = 1f;
        _bandText.text = text;
        bool whiff = diff.Value >= Constants.WhiffDiff;
        _bandDashes.gameObject.SetActive(whiff);
        _bandFill.gameObject.SetActive(!whiff);
        _bandFill.color = diff.Value == 0
            ? BattleTheme.WithAlpha(BattleTheme.Omen, 0.55f)
            : BattleTheme.WithAlpha(BattleTheme.Amber, 0.45f);
        foreach (Transform d in _bandDashes) d.GetComponent<Image>().color = BattleTheme.WithAlpha(BattleTheme.Whiff, 0.35f);
    }

    public IEnumerator BannerDrop()
    {
        _bannerGroup.alpha = 0f;
        _host.StartCoroutine(UiTween.Fade(_bandGroup, 0f, 1f, 300f, Ease.Out));
        yield return UiTween.Run(260f, Ease.Out, t =>
        {
            _bannerOffset = new Vector2(0f, 40f * (1f - t));
            _bannerGroup.alpha = t;
        });
        _bannerOffset = Vector2.zero;
    }

    public IEnumerator BannerBlink()
    {
        yield return UiTween.Run(250f, Ease.InOut, t => _bannerGroup.alpha = 1f - 0.6f * Mathf.Sin(t * Mathf.PI));
        _bannerGroup.alpha = 1f;
    }

    /// <summary>Omen executed: solid omen fill for 200 ms.</summary>
    public IEnumerator BannerFill()
    {
        Color a = _bannerBg.color, b = BattleTheme.Omen;
        yield return UiTween.Tint(_bannerBg, b, a, 200f, Ease.Out);
    }

    public IEnumerator BannerStrike(string note)
    {
        _bannerStrike.gameObject.SetActive(true);
        _bannerNote.text = note;
        var rt = _bannerStrike.rectTransform;
        yield return UiTween.Run(200f, Ease.Out, t => rt.anchorMax = new Vector2(Mathf.Lerp(0.1f, 0.9f, t), 0.5f));
    }

    public void BannerNote(string note) => _bannerNote.text = note;

    // ---- ghost ---------------------------------------------------------------------------

    public void ShowGhost(RangeBand after)
    {
        _ghost.Root.anchoredPosition = new Vector2(PlayerX(after), 0f);
        var (rot, sy) = BattleTheme.Posture(after);
        _ghost.ApplyPosture(rot, sy);
        _host.StartCoroutine(UiTween.Fade(_ghost.Group, _ghost.Group.alpha, 0.35f, 200f, Ease.Out));
        _band.anchoredPosition = new Vector2(PlayerX(after), 0f);
    }

    public void HideGhost()
    {
        _ghost.Group.alpha = 0f;
        _band.anchoredPosition = new Vector2(PlayerX(_range), 0f);
    }

    // ---- hit / move beats ---------------------------------------------------------------

    private FigureView Of(Actor who) => who == Actor.Player ? Player : Enemy;

    public IEnumerator StepIn(Actor attacker)
    {
        var f = Of(attacker);
        Vector2 origin = f.Root.anchoredPosition;
        Vector2 target = origin + new Vector2(-f.Sign * 70f, 0f);
        yield return UiTween.Move(f.Root, origin, target, 180f, Ease.Out);
    }

    public IEnumerator StepBack(Actor attacker)
    {
        var f = Of(attacker);
        Vector2 now = f.Root.anchoredPosition;
        Vector2 origin = new Vector2(attacker == Actor.Player ? PlayerX(_range) : EnemyX(_range), 0f);
        yield return UiTween.Move(f.Root, now, origin, 220f, Ease.InOut);
    }

    public IEnumerator HitFlash(Actor target, int cycles)
    {
        var f = Of(target);
        _host.StartCoroutine(UiTween.Run(60f, Ease.Linear, t => f.Flash.color = BattleTheme.WithAlpha(Color.white, 0.9f * (1f - t))));
        yield return UiTween.Shake(f.Root, 6f, cycles, 180f);
    }

    public IEnumerator Stagger(Actor target)
    {
        var f = Of(target);
        float baseRot = f.Rot;
        yield return UiTween.Run(400f, Ease.Out, t =>
        {
            float back = 6f * (1f - t); // lean away from the attacker
            f.Body.localRotation = Quaternion.Euler(0f, 0f, (baseRot - back) * f.Sign);
        });
        f.ApplyPosture(f.Rot, f.ScaleY);
    }

    public IEnumerator Collapse(Actor who)
    {
        var f = Of(who);
        float r0 = f.Rot;
        yield return UiTween.Run(800f, Ease.In, t =>
        {
            f.Body.localRotation = Quaternion.Euler(0f, 0f, (r0 - 80f * t) * f.Sign);
            f.Group.alpha = 1f - t * 0.7f;
        });
    }

    public IEnumerator ShrinkGlow()
    {
        yield return UiTween.Run(600f, Ease.In, t => _glowScale = 1f - t);
    }

    public void ResetFigures()
    {
        _glowScale = 1f;
        Player.Group.alpha = 1f;
        Enemy.Group.alpha = 1f;
        Player.ApplyPosture(Player.Rot, Player.ScaleY);
        Enemy.ApplyPosture(Enemy.Rot, Enemy.ScaleY);
    }

    /// <summary>Floating number above a figure: rises 90 px and fades over 600 ms.</summary>
    public void SpawnNumber(Actor over, string text, Color color, int size, float xOffset = 0f)
    {
        var f = Of(over);
        var label = UiKit.Label(_arena, "Float", size, TextAnchor.MiddleCenter, color,
            new Vector2(0.5f, BattleTheme.FloorFraction), new Vector2(0.5f, 0.5f), new Vector2(360f, 80f),
            f.Root.anchoredPosition + new Vector2(xOffset, FigureHeight * 0.72f));
        label.fontStyle = FontStyle.Bold;
        var outline = label.gameObject.AddComponent<Outline>();
        outline.effectColor = BattleTheme.WithAlpha(BattleTheme.InkBlack, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        var group = UiKit.Group(label.rectTransform);
        Vector2 start = label.rectTransform.anchoredPosition;
        _host.StartCoroutine(FloatAndDestroy(label.rectTransform, group, start));
    }

    private static IEnumerator FloatAndDestroy(RectTransform rt, CanvasGroup group, Vector2 start)
    {
        yield return UiTween.Run(600f, Ease.Out, t =>
        {
            rt.anchoredPosition = start + new Vector2(0f, 90f * t);
            group.alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
        });
        Object.Destroy(rt.gameObject);
    }
}
#endif
