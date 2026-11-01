using System.Linq;
using NUnit.Framework;

namespace Trial.Geometry.Tests
{
    public class KeyframesTests
    {
        [Test]
        public void AllTwentyKeyframes_ParseIntoTracks()
        {
            string[] names = CssKeyframes.Names.ToArray();
            Assert.That(names.Length, Is.EqualTo(20));
            foreach (string n in names)
            {
                CssKeyframes k = CssKeyframes.Get(n);
                Assert.That(k, Is.Not.Null, n);
                Assert.That(k.Name, Is.EqualTo(n));
                Assert.That(k.Properties.Any(), Is.True, n);
                foreach (string p in k.Properties)
                    Assert.That(k.OffsetsOf(p).Count, Is.GreaterThanOrEqualTo(1), n + " " + p);
            }
            Assert.That(CssKeyframes.Get("nope"), Is.Null);
            Assert.That(CssKeyframes.Get("ocbg-explore-beckon").OffsetsOf("box-shadow"), Is.EqualTo(new[] { 0f, 0.5f, 1f }));
        }

        [Test]
        public void Easing_AppliesPerSegment()
        {
            CssKeyframes pulse = CssKeyframes.Get("ocbg-explore-pulse"); // 1 → .45 (50 %) → 1
            float e = Ease.CssInOut(0.25f);
            Assert.That(pulse.Sample(0.125f, Ease.CssInOut).Opacity.Value, Is.EqualTo(1f + (0.45f - 1f) * e).Within(1e-5f));
            Assert.That(pulse.Sample(0.625f, Ease.CssInOut).Opacity.Value, Is.EqualTo(0.45f + (1f - 0.45f) * e).Within(1e-5f));
            Assert.That(pulse.Sample(0.5f, Ease.CssInOut).Opacity.Value, Is.EqualTo(0.45f).Within(1e-6f));
            // land: 0 % → 60 % → 100 %; at 30 % the first segment is half way
            CssKeyframes land = CssKeyframes.Get("ocbg-explore-land");
            float h = Ease.Decel(0.5f);
            AnimProps a = land.Sample(0.3f, Ease.Decel);
            Assert.That(a.Sx.Value, Is.EqualTo(1.18f + (0.96f - 1.18f) * h).Within(1e-5f));
            Assert.That(a.Sy.Value, Is.EqualTo(0.84f + (1.05f - 0.84f) * h).Within(1e-5f));
            AnimProps end = land.Sample(1f, Ease.Decel);
            Assert.That((end.Sx.Value, end.Sy.Value), Is.EqualTo((1f, 1f)));
        }

        [Test]
        public void Transforms_InterpolateFunctionByFunction()
        {
            CssKeyframes plateIn = CssKeyframes.Get("ocbg-explore-plate-in");
            AnimProps p0 = plateIn.Sample(0f, Ease.Linear);
            Assert.That((p0.Opacity.Value, p0.Ty.Value), Is.EqualTo((0f, 18f)));
            AnimProps p1 = plateIn.Sample(1f, Ease.Linear);
            Assert.That((p1.Opacity.Value, p1.Ty.Value), Is.EqualTo((1f, 0f)));
            AnimProps deal = CssKeyframes.Get("ocbg-explore-deal").Sample(0.5f, Ease.Linear);
            Assert.That(deal.Ty.Value, Is.EqualTo(18f).Within(1e-4f));
            Assert.That(deal.Rot.Value, Is.EqualTo(-2f).Within(1e-4f));
            // scale(1.08) translateY(-6px): the translation happens in the scaled space
            AnimProps win = CssKeyframes.Get("ocbg-explore-window").Sample(1f, Ease.Linear);
            Assert.That(win.Sy.Value, Is.EqualTo(1.08f).Within(1e-5f));
            Assert.That(win.Ty.Value, Is.EqualTo(-6.48f).Within(1e-4f));
            AnimProps drawer = CssKeyframes.Get("ocbg-explore-drawer").Sample(0.25f, Ease.Linear);
            Assert.That(drawer.TxPercent.Value, Is.EqualTo(75f).Within(1e-4f));
            AnimProps ripple = CssKeyframes.Get("ocbg-explore-ripple").Sample(1f, Ease.Linear);
            Assert.That((ripple.Opacity.Value, ripple.Sx.Value), Is.EqualTo((0f, 1.55f)));
        }

        [Test]
        public void Shadows_InterpolateLayerByLayer()
        {
            AnimProps beckon = CssKeyframes.Get("ocbg-explore-beckon").Sample(0.5f, Ease.CssInOut);
            Assert.That(beckon.BoxShadows.Length, Is.EqualTo(2));
            Assert.That(beckon.BoxShadows[0], Is.EqualTo(DsTokens.ShadowCard[0]));
            Assert.That((beckon.BoxShadows[1].Blur, beckon.BoxShadows[1].Spread), Is.EqualTo((18f, 2f)));
            Assert.That(beckon.BoxShadows[1].Color.A, Is.EqualTo(0.38f).Within(1e-5f));
            AnimProps stamp = CssKeyframes.Get("ocbg-meta-stamp").Sample(0.5f, Ease.Linear);
            Assert.That(stamp.BoxShadows[0].Spread, Is.EqualTo(11f).Within(1e-4f));
            Assert.That(stamp.BoxShadows[0].Color.A, Is.EqualTo(0.35f).Within(1e-4f));
            Assert.That(stamp.BoxShadows[0].Color.WithAlpha(1f).Approximately(DsTokens.Amber, 1e-4f), Is.True);
            AnimProps pip = CssKeyframes.Get("ocb-pip-lit").Sample(0.5f, Ease.Linear);
            Assert.That(pip.Sx.Value, Is.EqualTo(1.175f).Within(1e-5f));
            Assert.That(pip.BoxShadows[0].Spread, Is.EqualTo(4f).Within(1e-5f));
            AnimProps roll = CssKeyframes.Get("ocbg-explore-roll").Sample(0.25f, Ease.CssOut);
            Assert.That(roll.TextShadows[0].Blur, Is.EqualTo(12f).Within(1e-5f));
            Assert.That(roll.TextShadows[0].Color.A, Is.EqualTo(0.7f).Within(1e-5f));
            AnimProps flow = CssKeyframes.Get("ocbg-explore-flow").Sample(0.25f, Ease.Linear);
            Assert.That(flow.DashOffset.Value, Is.EqualTo(18f).Within(1e-5f));
        }

        [Test]
        public void Sample_ReturnsCopies_OfTheCachedFrames()
        {
            // a held value (before the first / after the last keyframe) is the cached frames' own data
            CssKeyframes beckon = CssKeyframes.Get("ocbg-explore-beckon");
            AnimProps p = beckon.Sample(0f, Ease.CssInOut);
            p.BoxShadows[1] = new BoxShadow(9f, 9f, 9f, 9f, Rgba.White);
            Assert.That(beckon.Sample(0f, Ease.CssInOut).BoxShadows[1].Blur, Is.EqualTo(0f));
            CssKeyframes roll = CssKeyframes.Get("ocbg-explore-roll");
            AnimProps r = roll.Sample(1f, Ease.CssOut);
            r.TextShadows[0] = new TextShadow(5f, 5f, 5f, Rgba.White);
            Assert.That(roll.Sample(1f, Ease.CssOut).TextShadows[0].Blur, Is.EqualTo(0f));
        }

        [Test]
        public void Shorthand_DurationDelayEasingFill()
        {
            CssAnimation a = CssAnimation.Parse("ocbg-explore-plate-in 700ms 120ms cubic-bezier(.22,.61,.36,1) both");
            Assert.That((a.Name, a.DurationMs, a.DelayMs, a.Fill), Is.EqualTo(("ocbg-explore-plate-in", 700f, 120f, AnimFill.Both)));
            Assert.That(a.Easing(0.5f), Is.EqualTo(Ease.Decel(0.5f)).Within(1e-6f));
            Assert.That(a.Keyframes, Is.Not.Null);
            CssAnimation b = CssAnimation.Parse("ocb-breathe 8s ease-in-out infinite reverse");
            Assert.That(b.IsInfinite, Is.True);
            Assert.That((b.DurationMs, b.Direction), Is.EqualTo((8000f, AnimDirection.Reverse)));
            CssAnimation c = CssAnimation.Parse("ocbg-explore-roll 900ms ease-out");
            Assert.That((c.Iterations, c.Fill, c.EasingText), Is.EqualTo((1f, AnimFill.None, "ease-out")));
            CssAnimation d = CssAnimation.Parse("ocbg-explore-window 9s ease-in-out infinite alternate");
            Assert.That(d.Direction, Is.EqualTo(AnimDirection.Alternate));
            CssAnimation defaults = CssAnimation.Parse("ocb-drift linear infinite");
            Assert.That((defaults.DurationMs, defaults.EasingText), Is.EqualTo((0f, "linear")));
            Assert.That(CssAnimation.ParseList("none"), Is.Empty);
            Assert.That(CssAnimation.ParseList("ocbg-explore-fade 260ms ease-out both, ocbg-explore-pulse 1.6s ease-in-out infinite").Count, Is.EqualTo(2));
        }

        [Test]
        public void Timing_DelayFillIterationsDirection()
        {
            CssAnimation plate = CssAnimation.Parse("ocbg-explore-plate-in 700ms 120ms cubic-bezier(.22,.61,.36,1) both");
            Assert.That(plate.TrySample(0f, out AnimProps before), Is.True); // backwards fill: the first frame during the delay
            Assert.That(before.Opacity.Value, Is.EqualTo(0f));
            Assert.That(plate.TrySample(5000f, out AnimProps after), Is.True);
            Assert.That(after.Opacity.Value, Is.EqualTo(1f));
            Assert.That(plate.TrySample(120f + 350f, out AnimProps mid), Is.True);
            Assert.That(mid.Opacity.Value, Is.EqualTo(Ease.Decel(0.5f)).Within(1e-5f));

            CssAnimation ripple = CssAnimation.Parse("ocbg-explore-ripple 820ms cubic-bezier(.22,.61,.36,1)");
            Assert.That(ripple.TrySample(820f, out _), Is.False); // no fill: gone after the end
            Assert.That(ripple.TrySample(-1f, out _), Is.False);
            Assert.That(ripple.IsFinished(820f), Is.True);

            CssAnimation alt = CssAnimation.Parse("ocbg-explore-fade 100ms linear 3 alternate");
            alt.TryProgress(150f, out float p, out int it);
            Assert.That((p, it), Is.EqualTo((0.5f, 1)));
            alt.TryProgress(130f, out float q, out _);
            Assert.That(q, Is.EqualTo(0.7f).Within(1e-5f)); // the second iteration runs backwards

            CssAnimation pulse = CssAnimation.Parse("ocbg-explore-pulse 1.6s ease-in-out infinite");
            Assert.That(pulse.TrySample(1600f * 7 + 800f, out AnimProps far), Is.True);
            Assert.That(far.Opacity.Value, Is.EqualTo(0.45f).Within(1e-5f));
        }

        [Test]
        public void Transitions_ParseAndCover()
        {
            var t = TransitionSpec.ParseList("transform 90ms cubic-bezier(.4,0,.2,1), opacity 300ms");
            Assert.That(t.Count, Is.EqualTo(2));
            Assert.That((t[0].Property, t[0].DurationMs), Is.EqualTo(("transform", 90f)));
            Assert.That(t[0].Easing(0.5f), Is.EqualTo(Ease.Standard(0.5f)).Within(1e-6f));
            Assert.That((t[1].Property, t[1].DurationMs, t[1].EasingText), Is.EqualTo(("opacity", 300f, "ease")));
            var b = TransitionSpec.ParseList("border-color .14s, background .14s");
            Assert.That(b[1].DurationMs, Is.EqualTo(140f).Within(1e-4f));
            Assert.That(b[1].Covers("background-color"), Is.True);
            Assert.That(b[0].Covers("border-left-color"), Is.True);
            Assert.That(b[0].Covers("opacity"), Is.False);
            Assert.That(TransitionSpec.ParseList("none"), Is.Empty);
            var all = TransitionSpec.ParseList("opacity 400ms ease-in-out 120ms");
            Assert.That((all[0].DelayMs, all[0].EasingText), Is.EqualTo((120f, "ease-in-out")));
        }
    }
}
