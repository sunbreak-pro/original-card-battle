// Gaussian blur maths for box-shadow, text-shadow and drop-shadow (kit.md §3.2, §4.4).
//  - box-shadow / text-shadow blur b → σ = b / 2 (measured in Chrome);
//  - filter: drop-shadow(0 0 b) → σ = b (measured);
//  - a blurred straight edge at signed distance d outside the shape has alpha ½·erfc(d / (σ√2)).
using System;

namespace Trial.Geometry
{
    public static class Blur
    {
        private const double Sqrt2 = 1.4142135623730951;

        /// <summary>σ of a CSS box-shadow / text-shadow blur radius (b / 2).</summary>
        public static float SigmaOfShadowBlur(float blur) => blur * 0.5f;

        /// <summary>σ of a CSS filter: drop-shadow() blur (b itself).</summary>
        public static float SigmaOfDropShadow(float blur) => blur;

        /// <summary>The error function (W. J. Cody's rational approximations; |error| &lt; 1e-15 in double).</summary>
        public static float Erf(float x) => (float)ErfD(x);

        /// <summary>1 − erf(x), computed directly so the tail keeps its precision.</summary>
        public static float Erfc(float x) => (float)ErfcD(x);

        /// <summary>The standard normal CDF Φ(x) = ½·erfc(−x/√2).</summary>
        public static float Phi(float x) => (float)(0.5 * ErfcD(-x / Sqrt2));

        /// <summary>
        /// Alpha (0..1, times the shadow colour's alpha) of a blurred straight edge at signed distance
        /// <paramref name="distOutside"/> (positive = outside the shape). σ = 0 is the hard edge (½ on it).
        /// </summary>
        public static float EdgeAlpha(float distOutside, float sigma)
        {
            if (!(sigma > 0f)) return distOutside > 0f ? 0f : distOutside < 0f ? 1f : 0.5f;
            return (float)(0.5 * ErfcD(distOutside / (sigma * Sqrt2)));
        }

        /// <summary>
        /// A blurred band of width w (a blurred stroke): Φ((d + w/2)/σ) − Φ((d − w/2)/σ) at signed distance d from
        /// its centre line. σ = 0 is the hard band.
        /// </summary>
        public static float BandAlpha(float d, float width, float sigma)
        {
            float h = width * 0.5f;
            if (!(sigma > 0f)) return Math.Abs(d) < h ? 1f : Math.Abs(d) == h ? 0.5f : 0f;
            double a = 0.5 * ErfcD(-(d + h) / (sigma * Sqrt2));
            double b = 0.5 * ErfcD(-(d - h) / (sigma * Sqrt2));
            return (float)(a - b);
        }

        /// <summary>
        /// The Stroker.Soft profile of filter: drop-shadow(0 0 b c) under a stroke of width w (both in the same units
        /// as Soft's distance): alpha × BandAlpha(d, w, b). Its reach is about w/2 + 3σ.
        /// </summary>
        public static Func<float, float> StrokeShadowProfile(float width, float sigma, float alpha = 1f) =>
            d => alpha * BandAlpha(d, width, sigma);

        /// <summary>Half extent a Soft band needs for a blurred stroke (w/2 + 3σ).</summary>
        public static float StrokeShadowReach(float width, float sigma) => width * 0.5f + 3f * Math.Max(0f, sigma);

        // ---- erf / erfc (W. J. Cody, "Rational Chebyshev approximations for the error function", 1969) ----

        private static readonly double[] A = { 3.16112374387056560e00, 1.13864154151050156e02, 3.77485237685302021e02, 3.20937758913846947e03, 1.85777706184603153e-1 };
        private static readonly double[] B = { 2.36012909523441209e01, 2.44024637934444173e02, 1.28261652607737228e03, 2.84423683343917062e03 };
        private static readonly double[] C = { 5.64188496988670089e-1, 8.88314979438837594e00, 6.61191906371416295e01, 2.98635138197400131e02, 8.81952221241769090e02, 1.71204761263407058e03, 2.05107837782607147e03, 1.23033935479799725e03, 2.15311535474403846e-8 };
        private static readonly double[] D = { 1.57449261107098347e01, 1.17693950891312499e02, 5.37181101862009858e02, 1.62138957456669019e03, 3.29079923573345963e03, 4.36261909014324716e03, 3.43936767414372164e03, 1.23033935480374942e03 };
        private static readonly double[] P = { 3.05326634961232344e-1, 3.60344899949804439e-1, 1.25781726111229246e-1, 1.60837851487422766e-2, 6.58749161529837803e-4, 1.63153871373020978e-2 };
        private static readonly double[] Q = { 2.56852019228982242e00, 1.87295284992346725e00, 5.27905102951428412e-1, 6.05183413124413191e-2, 2.33520497626869185e-3 };
        private const double SqrPi = 5.6418958354775628695e-1; // 1/√π

        public static double ErfD(double x)
        {
            double y = Math.Abs(x);
            if (y <= 0.46875)
            {
                double z = y > 1.11e-16 ? y * y : 0;
                double xn = A[4] * z, xd = z;
                for (int i = 0; i < 3; i++) { xn = (xn + A[i]) * z; xd = (xd + B[i]) * z; }
                return x * (xn + A[3]) / (xd + B[3]);
            }
            double r = 1 - ErfcPositive(y);
            return x < 0 ? -r : r;
        }

        public static double ErfcD(double x)
        {
            double y = Math.Abs(x);
            if (y <= 0.46875) return 1 - ErfD(x);
            double r = ErfcPositive(y);
            return x < 0 ? 2 - r : r;
        }

        private static double ErfcPositive(double y)
        {
            double result;
            if (y <= 4.0)
            {
                double xn = C[8] * y, xd = y;
                for (int i = 0; i < 7; i++) { xn = (xn + C[i]) * y; xd = (xd + D[i]) * y; }
                result = (xn + C[7]) / (xd + D[7]);
            }
            else
            {
                if (y >= 26.543) return 0;
                double z = 1 / (y * y);
                double xn = P[5] * z, xd = z;
                for (int i = 0; i < 4; i++) { xn = (xn + P[i]) * z; xd = (xd + Q[i]) * z; }
                result = z * (xn + P[4]) / (xd + Q[4]);
                result = (SqrPi - result) / y;
            }
            // exp(−y²) split to keep precision (Cody's ysq trick)
            double ysq = Math.Truncate(y * 16) / 16;
            double del = (y - ysq) * (y + ysq);
            return Math.Exp(-ysq * ysq) * Math.Exp(-del) * result;
        }
    }
}
