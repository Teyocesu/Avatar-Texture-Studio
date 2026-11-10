using System;
using System.Collections.Generic;
using System.Numerics;
using AvatarTextureStudio.Creative.Core;

namespace AvatarTextureStudio.Quality.Core
{
    internal struct PixelValue
    {
        public double R, G, B, A;
        public static PixelValue operator +(PixelValue a, PixelValue b) => new PixelValue { R = a.R + b.R, G = a.G + b.G, B = a.B + b.B, A = a.A + b.A };
        public static PixelValue operator *(PixelValue a, double b) => new PixelValue { R = a.R * b, G = a.G * b, B = a.B * b, A = a.A * b };
    }
    internal static class DiagnosticPixels
    {
        public static PixelValue Read(ColorImage image, int x, int y, bool data)
        {
            int p = y * image.Width + x;
            double alpha = image.Channel(p, 3) / 255d;
            double Channel(int c) => data ? image.Channel(p, c) / 255d : ColorwayProcessor.Decode(image.Channel(p, c) / 255d, image.IsSRGB) * alpha;
            return new PixelValue { R = Channel(0), G = Channel(1), B = Channel(2), A = alpha };
        }
        public static PixelValue Sample(ColorImage image, Vector2 uv, UvWrap u, UvWrap v, bool data)
        {
            float x = UvLayout.Wrap(uv.X, u) * image.Width - .5f, y = UvLayout.Wrap(uv.Y, v) * image.Height - .5f;
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y); double tx = x - x0, ty = y - y0;
            int Index(int n, int size, UvWrap mode)
            {
                if (mode == UvWrap.Repeat) return (n % size + size) % size;
                return Math.Max(0, Math.Min(size - 1, n));
            }
            PixelValue At(int px, int py) => Read(image, Index(px, image.Width, u), Index(py, image.Height, v), data);
            return (At(x0, y0) * (1 - tx) + At(x0 + 1, y0) * tx) * (1 - ty) + (At(x0, y0 + 1) * (1 - tx) + At(x0 + 1, y0 + 1) * tx) * ty;
        }
        public static void Write(PixelValue p, byte[] output, int offset, bool srgb, bool data)
        {
            double alpha = Math.Max(0, Math.Min(1, p.A));
            byte Byte(double c) => (byte)Math.Max(0, Math.Min(255, Math.Floor(c * 255 + .5)));
            byte alphaByte = Byte(alpha);
            byte Color(double c)
            {
                if (!data && alphaByte == 0) return 0;
                c = data ? c : alpha > 0 ? c / alpha : 0; c = Math.Max(0, Math.Min(1, c));
                if (!data && srgb) c = c <= .0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1 / 2.4) - .055;
                return Byte(c);
            }
            output[offset] = Color(p.R); output[offset + 1] = Color(p.G); output[offset + 2] = Color(p.B); output[offset + 3] = alphaByte;
        }
    }
    internal static class DiagnosticMips
    {
        public const long MaxBytes = 384L * 1024 * 1024;
        public static List<ColorImage> Build(ColorImage source, bool data, Func<float, bool> cancel = null)
        {
            var levels = new List<ColorImage> { source }; long bytes = (long)source.Width * source.Height * 4;
            int total = 0; for (int w = source.Width, h = source.Height; w > 1 || h > 1; w = Math.Max(1, w / 2), h = Math.Max(1, h / 2)) total++;
            while (levels[levels.Count - 1].Width > 1 || levels[levels.Count - 1].Height > 1)
            {
                var previous = levels[levels.Count - 1]; int width = Math.Max(1, previous.Width / 2), height = Math.Max(1, previous.Height / 2);
                bytes += (long)width * height * 4;
                if (levels.Count >= 14 || bytes > MaxBytes) throw new ArgumentException("CPU diagnostic mip chain exceeds 14 levels / 384 MiB.");
                var pixels = new byte[width * height * 4];
                for (int y = 0; y < height; y++)
                {
                    if ((y & 15) == 0) SeamGeometry.CheckCancel(cancel, ((levels.Count - 1) + (float)y / height) / Math.Max(1, total));
                    double y0 = (double)y * previous.Height / height, y1 = (double)(y + 1) * previous.Height / height;
                    for (int x = 0; x < width; x++)
                    {
                        double x0 = (double)x * previous.Width / width, x1 = (double)(x + 1) * previous.Width / width; PixelValue sum = default;
                        for (int sy = (int)Math.Floor(y0); sy < Math.Ceiling(y1); sy++)
                        for (int sx = (int)Math.Floor(x0); sx < Math.Ceiling(x1); sx++)
                        {
                            double weight = (Math.Min(sx + 1, x1) - Math.Max(sx, x0)) * (Math.Min(sy + 1, y1) - Math.Max(sy, y0));
                            sum += DiagnosticPixels.Read(previous, sx, sy, data) * weight;
                        }
                        DiagnosticPixels.Write(sum * (1 / ((x1 - x0) * (y1 - y0))), pixels, (y * width + x) * 4, source.IsSRGB, data);
                    }
                }
                levels.Add(new ColorImage(width, height, source.IsSRGB, pixels));
            }
            return levels;
        }
    }
    internal sealed class SeamMeasurement
    {
        public SeamPair Pair;
        public int Samples, Unresolved, VisibleColorSamples, PotentialContrast, ExteriorContrast;
        public double MeanDeltaE100, MaxDeltaE100, MeanAlpha, MaxAlpha, MeanDifference, MaxDifference, WorstT;
    }
    internal static class SeamAnalyzer
    {
        public const int MaxSamples = 1000000;
        public const double ConcernThreshold = .05;
        public static List<SeamMeasurement> Analyze(SeamTopology topology, ColorImage image, bool data, UvWrap u, UvWrap v, Func<float, bool> cancel = null)
        {
            var output = new List<SeamMeasurement>(); int visited = 0;
            Vector2 size = new Vector2(image.Width, image.Height);
            int Count(SeamPair pair) => Math.Max(2, Math.Min(256, (int)Math.Ceiling(Math.Max(Vector2.Multiply(pair.A.End - pair.A.Start, size).Length(), Vector2.Multiply(pair.B.End - pair.B.Start, size).Length()))));
            int total = 0;
            foreach (var pair in topology.Pairs) { total += Count(pair); if (total > MaxSamples) throw new ArgumentException("Seam analysis exceeds one million samples per level; use a smaller mesh. No partial measurements were substituted."); }
            foreach (var pair in topology.Pairs)
            {
                var m = new SeamMeasurement { Pair = pair }; output.Add(m);
                int samples = Count(pair);
                for (int i = 0; i < samples; i++)
                {
                    if ((visited++ & 63) == 0) SeamGeometry.CheckCancel(cancel, (float)visited / Math.Max(1, total));
                    float t = (i + .5f) / samples;
                    if (!Probe(pair.A, t, size, out var insideA, out var outsideA) || !Probe(pair.B, t, size, out var insideB, out var outsideB)) { m.Unresolved++; continue; }
                    var a = DiagnosticPixels.Sample(image, insideA, u, v, data); var b = DiagnosticPixels.Sample(image, insideB, u, v, data);
                    double difference = Difference(a, b, data), alpha = Math.Abs(a.A - b.A); m.Samples++;
                    m.MeanDifference += difference; m.MeanAlpha += alpha; m.MaxAlpha = Math.Max(m.MaxAlpha, alpha);
                    if (difference > m.MaxDifference) { m.MaxDifference = difference; m.WorstT = t; }
                    if (difference > ConcernThreshold) m.PotentialContrast++;
                    if (!data && a.A > 1 / 255d && b.A > 1 / 255d)
                    {
                        ColorwayProcessor.ToLab(a.R / a.A, a.G / a.A, a.B / a.A, out double l1, out double a1, out double b1);
                        ColorwayProcessor.ToLab(b.R / b.A, b.G / b.A, b.B / b.A, out double l2, out double a2, out double b2);
                        double delta = 100 * Math.Sqrt((l1 - l2) * (l1 - l2) + (a1 - a2) * (a1 - a2) + (b1 - b2) * (b1 - b2));
                        m.VisibleColorSamples++; m.MeanDeltaE100 += delta; m.MaxDeltaE100 = Math.Max(m.MaxDeltaE100, delta);
                    }
                    if (Difference(a, DiagnosticPixels.Sample(image, outsideA, u, v, data), data) > ConcernThreshold
                        || Difference(b, DiagnosticPixels.Sample(image, outsideB, u, v, data), data) > ConcernThreshold) m.ExteriorContrast++;
                }
                m.MeanDifference /= Math.Max(1, m.Samples); m.MeanAlpha /= Math.Max(1, m.Samples); m.MeanDeltaE100 /= Math.Max(1, m.VisibleColorSamples);
            }
            return output;
        }
        static bool Probe(SeamEdge edge, float t, Vector2 size, out Vector2 inside, out Vector2 outside)
        {
            var point = Vector2.Lerp(edge.Start, edge.End, t); var direction = Vector2.Multiply(edge.End - edge.Start, size);
            if (direction.LengthSquared() < 1e-12f) { inside = outside = default; return false; }
            var normal = Vector2.Normalize(new Vector2(-direction.Y, direction.X));
            if (Vector2.Dot(normal, Vector2.Multiply(edge.Third - point, size)) < 0) normal = -normal;
            inside = point + Vector2.Divide(normal * .5f, size); outside = point - Vector2.Divide(normal, size);
            float inset = Vector2.Dot(Vector2.Multiply(inside - point, size), normal);
            if (inset < .25f || inset > .75f) return false;
            return UvLayout.Barycentric(inside, edge.Start, edge.End, edge.Third, out _);
        }
        public static double Difference(PixelValue a, PixelValue b, bool data)
        {
            double rgb = Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));
            if (data) return Math.Max(rgb, Math.Abs(a.A - b.A));
            return Math.Max(rgb, Math.Max(Math.Abs(a.R - a.A - b.R + b.A), Math.Max(Math.Abs(a.G - a.A - b.G + b.A), Math.Abs(a.B - a.A - b.B + b.A))));
        }
    }
}
