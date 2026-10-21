// Suite 8 (backdrop): BackdropSpec / ExplorationBackdrop / MiasmaLook / BackdropMotion values for every
// layer (0〜8, so the clamp shows) and miasma 0〜100 step 5.
using Backdrop;

namespace Oracle;

public static class BackdropSuite
{
    public static readonly float[] Heights = { 0f, 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.52f, 0.54f, 0.56f, 0.6f, 0.7f, 0.8f, 0.9f, 1f };
    public static readonly double[] Times = { 0, 70, 140, 500, 1234.5, 3000, 7000, 12345 };

    public static int Run(string outDir)
    {
        using var file = new SuiteFile(outDir, "backdrop");

        file.Add("table:BackdropSpec", new OrderedMap { ["Type"] = "BackdropSpec" }, () => new OrderedMap
        {
            ["Static"] = Tables.Static(typeof(BackdropSpec)),
            ["ExplorationStatic"] = Tables.Static(typeof(ExplorationBackdropSpec)),
            ["MiasmaLookStatic"] = Tables.Static(typeof(MiasmaLook)),
            ["MotionStatic"] = Tables.Static(typeof(BackdropMotion)),
            ["Clamp"] = Enumerable.Range(-1, 11).Select(BackdropSpec.Clamp).ToList(),
            ["Hex"] = new[] { Rgba.Hex("#ff1f4f"), Rgba.Hex("10141d", 0.5f), Rgba.Bytes(157, 166, 186, 0.26f) },
        });

        for (int layer = 0; layer <= 8; layer++)
        {
            int l = layer;
            file.Add($"layer:{l}", new OrderedMap { ["Layer"] = l }, () => Layer(l));
        }

        for (int percent = 0; percent <= 100; percent += 5)
        {
            int p = percent;
            file.Add($"miasma:{p}", new OrderedMap { ["Percent"] = p }, () => Miasma(p));
        }

        file.Add("motion", new OrderedMap { ["Times"] = Times }, Motion);
        file.Add("switch", new OrderedMap { ["Script"] = "Show/Tick sequence" }, Switch);
        return file.Count;
    }

    private static object Layer(int layer)
    {
        var b = BackdropSpec.For(layer);
        var e = ExplorationBackdropSpec.For(layer);
        var hazeAlpha = b.Hazes.Select(h => Heights.Select(y => BackdropSpec.HazeAlphaAt(h, y)).ToList()).ToList();
        var flicker = b.Hazes.Select(h => Times.Select(ms => BackdropMotion.FlickerAlpha(h.Flicker, ms)).ToList()).ToList();
        return new OrderedMap
        {
            ["For"] = b,
            ["SkyAt"] = Heights.Select(y => BackdropSpec.SkyAt(b, y)).ToList(),
            ["HazeAlphaAt"] = hazeAlpha,
            ["BrightestAt"] = Heights.Select(y => BackdropSpec.BrightestAt(b, y)).ToList(),
            ["Luminance"] = Heights.Select(y => BackdropSpec.Luminance(BackdropSpec.BrightestAt(b, y))).ToList(),
            ["ReachRedContrast"] = BackdropSpec.ReachRedContrast(b),
            ["TileContrast"] = BackdropSpec.Contrast(BackdropSpec.ReachRed, b.Tile),
            ["FlickerAlpha"] = flicker,
            ["Exploration"] = e,
            ["ExplorationBrightestAt"] = Heights.Select(y => ExplorationBackdropSpec.BrightestAt(e, y)).ToList(),
            ["LowestContrastInk"] = ExplorationBackdropSpec.LowestContrast(e, ExplorationBackdropSpec.Ink),
            ["LowestContrastAmber"] = ExplorationBackdropSpec.LowestContrast(e, ExplorationBackdropSpec.Amber),
            ["PeakLuminance"] = ExplorationBackdropSpec.PeakLuminance(e),
            ["Mix"] = new[] { 0f, 0.25f, 0.5f, 1f }.Select(t => BackdropSpec.Mix(b.Sky[0].Color, b.Tile, t)).ToList(),
            ["Over"] = BackdropSpec.Over(b.Tile, b.Sky[0].Color),
        };
    }

    private static object Miasma(int p)
    {
        var next = new[] { p, p + 1, p + 5, p + 10, p + 20, 100 };
        return new OrderedMap
        {
            ["FillAt"] = MiasmaLook.FillAt(p),
            ["Next"] = next,
            ["Warning"] = next.Select(n => MiasmaLook.Warning(p, n)).ToList(),
            ["AheadColor"] = next.Select(n => MiasmaLook.AheadColor(p, n)).ToList(),
            ["ForecastMark"] = next.Select(n => MiasmaLook.ForecastMark(p, n)).ToList(),
            ["BreathPeriodMs"] = MiasmaLook.BreathPeriodMs(p),
            ["Breath"] = Times.Select(ms => MiasmaLook.Breath(p, ms)).ToList(),
            ["FillAlpha"] = Times.Select(ms => MiasmaLook.FillAlpha(p, ms)).ToList(),
            ["ShadeAt"] = MiasmaLook.ShadeAt(p),
            ["ShadeAtTimes"] = Times.Select(ms => MiasmaLook.ShadeAt(p, ms)).ToList(),
            ["Rise"] = Times.Select(ms => MiasmaLook.Rise(p, Math.Min(100, p + 10), ms)).ToList(),
            ["EdgeFalloff"] = new[] { 0f, 0.3f, 0.5f, 0.8f, 0.95f, 1f, 1.2f }.Select(d => MiasmaLook.EdgeFalloff(MiasmaLook.ShadeAt(p), d)).ToList(),
            ["ShadeAlphaAt"] = new[] { 0f, 0.3f, 0.5f, 0.8f, 0.95f, 1f, 1.2f }.Select(d => MiasmaLook.ShadeAlphaAt(MiasmaLook.ShadeAt(p), d)).ToList(),
            ["Shaded"] = new[] { 0f, 0.5f, 1f }.Select(d => MiasmaLook.Shaded(BackdropSpec.For(3).Tile, MiasmaLook.ShadeAt(p), d)).ToList(),
        };
    }

    private static object Motion()
    {
        var motes = BackdropSpec.For(7).Motes;
        var at = new List<object>();
        var alpha = new List<object>();
        for (int index = 0; index < 6; index++)
        {
            foreach (var ms in Times)
            {
                BackdropMotion.MoteAt(motes, index, ms, 1920f, 1080f, out float x, out float y);
                at.Add(new OrderedMap { ["X"] = x, ["Y"] = y });
                alpha.Add(BackdropMotion.MoteAlpha(motes, index, ms));
            }
        }
        var hash = new List<object>();
        foreach (int index in new[] { 0, 1, 2, 7, 100, -3, int.MaxValue }) foreach (int salt in new[] { 0, 1, 5 }) hash.Add(BackdropMotion.Hash(index, salt));
        return new OrderedMap
        {
            ["Motes"] = motes,
            ["MoteAt"] = at,
            ["MoteAlpha"] = alpha,
            ["Crossfade"] = new[] { -10.0, 0, 100, 200, 399.9, 400, 1000 }.Select(BackdropMotion.Crossfade).ToList(),
            ["Hash"] = hash,
            ["Ticks"] = MiasmaLook.Ticks(),
            ["CornerDistance"] = new[] { (0f, 0f), (0.5f, 0.5f), (1f, 1f), (0.25f, 0.75f) }.Select(xy => MiasmaLook.CornerDistance(xy.Item1, xy.Item2, 16f / 9f)).ToList(),
            ["FlickerStill"] = BackdropMotion.FlickerAlpha(Flicker.Still, 1000),
            ["FlickerFlash"] = Times.Select(ms => BackdropMotion.FlickerAlpha(new Flicker(900, 0.6f, true), ms)).ToList(),
        };
    }

    private static object Switch()
    {
        var sw = new BackdropSwitch();
        var log = new List<object>();
        foreach (var (layer, now) in new[] { (1, 0.0), (1, 50.0), (2, 100.0), (3, 200.0), (3, 300.0), (9, 1000.0) })
        {
            var change = sw.Show(layer, now);
            log.Add(new OrderedMap { ["Show"] = layer, ["At"] = now, ["Change"] = change, ["Shown"] = sw.Shown, ["Leaving"] = sw.Leaving });
            foreach (var dt in new[] { 0.0, 150.0, 450.0 })
            {
                float t = sw.Tick(now + dt);
                log.Add(new OrderedMap { ["Tick"] = now + dt, ["T"] = t, ["Shown"] = sw.Shown, ["Leaving"] = sw.Leaving });
            }
        }
        return log;
    }
}
