using System;

namespace AvatarTextureStudio.Creative.Core
{
    internal static class ColorwayProcessor
    {
        // Oklab matrices from Björn Ottosson's published definition. All math is in linear RGB.
        public static byte[] Render(CreativeDocument document, Func<float, bool> cancel = null)
        {
            document.Validate();
            var source = document.Baseline;
            byte[] pixels = source.CopyPixels();
            int count = source.Width * source.Height;
            var active = document.Regions.FindAll(r => r.Enabled && r.Strength != 0 && (r.Hue != 0 || r.Chroma != 1));
            var cosine = new double[active.Count]; var sine = new double[active.Count];
            for (int r = 0; r < active.Count; r++)
            {
                double angle = active[r].Hue * Math.PI / 180; cosine[r] = Math.Cos(angle); sine[r] = Math.Sin(angle);
            }
            for (int p = 0; p < count && active.Count != 0; p++)
            {
                if ((p & 65535) == 0 && cancel != null && cancel((float)p / count))
                    throw new OperationCanceledException("Colorway preview cancelled; editable adjustments are retained.");
                int i = p * 4; bool changed = false;
                double red = 0, green = 0, blue = 0;
                for (int r = 0; r < active.Count; r++)
                {
                    var region = active[r];
                    if (!region.Mask.Contains(p)) continue;
                    if (!changed)
                    {
                        red = Decode(pixels[i] / 255d, source.IsSRGB); green = Decode(pixels[i + 1] / 255d, source.IsSRGB); blue = Decode(pixels[i + 2] / 255d, source.IsSRGB);
                        changed = true;
                    }
                    ToLab(red, green, blue, out double l, out double a, out double b);
                    double targetA = (a * cosine[r] - b * sine[r]) * region.Chroma, targetB = (a * sine[r] + b * cosine[r]) * region.Chroma;
                    a += (targetA - a) * region.Strength; b += (targetB - b) * region.Strength;
                    FromLab(l, a, b, out red, out green, out blue);
                    if (!InGamut(red, green, blue))
                    {
                        double low = 0, high = 1;
                        for (int n = 0; n < 20; n++)
                        {
                            double mid = (low + high) * .5;
                            FromLab(l, a * mid, b * mid, out red, out green, out blue);
                            if (InGamut(red, green, blue)) low = mid; else high = mid;
                        }
                        FromLab(l, a * low, b * low, out red, out green, out blue);
                    }
                    red = Math.Max(0, Math.Min(1, red)); green = Math.Max(0, Math.Min(1, green)); blue = Math.Max(0, Math.Min(1, blue));
                }
                if (!changed) continue;
                pixels[i] = EncodeByte(red, source.IsSRGB); pixels[i + 1] = EncodeByte(green, source.IsSRGB); pixels[i + 2] = EncodeByte(blue, source.IsSRGB);
                // Quantize once after all regions. Alpha is never transformed, including fully transparent selected pixels.
            }
            return pixels;
        }
        public static double Decode(double v, bool srgb) => !srgb ? v : v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4);
        static byte EncodeByte(double v, bool srgb)
        {
            v = Math.Max(0, Math.Min(1, v));
            if (srgb) v = v <= .0031308 ? v * 12.92 : 1.055 * Math.Pow(v, 1 / 2.4) - .055;
            return (byte)Math.Max(0, Math.Min(255, Math.Floor(v * 255 + .5)));
        }
        static bool InGamut(double r, double g, double b) => r >= -1e-7 && r <= 1.0000001 && g >= -1e-7 && g <= 1.0000001 && b >= -1e-7 && b <= 1.0000001;
        public static void ToLab(double r, double g, double b, out double l, out double a, out double bb)
        {
            double x = Math.Pow(.4122214708 * r + .5363325363 * g + .0514459929 * b, 1d / 3);
            double y = Math.Pow(.2119034982 * r + .6806995451 * g + .1073969566 * b, 1d / 3);
            double z = Math.Pow(.0883024619 * r + .2817188376 * g + .6299787005 * b, 1d / 3);
            l = .2104542553 * x + .793617785 * y - .0040720468 * z;
            a = 1.9779984951 * x - 2.428592205 * y + .4505937099 * z;
            bb = .0259040371 * x + .7827717662 * y - .808675766 * z;
        }
        static void FromLab(double l, double a, double b, out double r, out double g, out double blue)
        {
            double x = l + .3963377774 * a + .2158037573 * b, y = l - .1055613458 * a - .0638541728 * b, z = l - .0894841775 * a - 1.291485548 * b;
            x = x * x * x; y = y * y * y; z = z * z * z;
            r = 4.0767416621 * x - 3.3077115913 * y + .2309699292 * z;
            g = -1.2684380046 * x + 2.6097574011 * y - .3413193965 * z;
            blue = -.0041960863 * x - .7034186147 * y + 1.707614701 * z;
        }
    }
}
