param([string]$Out)
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class Ceramic
{
    // Tileable value noise: lattice wraps with the given period.
    static double Lattice(int x, int y, int period, int seed)
    {
        x = ((x % period) + period) % period;
        y = ((y % period) + period) % period;
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535.0;
        }
    }

    static double Noise(double u, double v, int period, int seed)
    {
        double x = u * period, y = v * period;
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        double fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        double a = Lattice(x0, y0, period, seed), b = Lattice(x0 + 1, y0, period, seed);
        double c = Lattice(x0, y0 + 1, period, seed), d = Lattice(x0 + 1, y0 + 1, period, seed);
        return (a + (b - a) * fx) + ((c + (d - c) * fx) - (a + (b - a) * fx)) * fy;
    }

    public static void Make(string path, int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        var data = bmp.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        var px = new byte[size * size * 4];
        var rnd = new Random(7);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            double u = (double)x / size, v = (double)y / size;
            double blotch = Noise(u, v, 6, 1) * 0.55 + Noise(u, v, 12, 2) * 0.3 + Noise(u, v, 32, 3) * 0.15;
            double grain = Noise(u, v, 128, 4) * 0.6 + rnd.NextDouble() * 0.4;
            double g = 0.155 + (blotch - 0.5) * 0.07 + (grain - 0.5) * 0.035;
            int val = (int)Math.Max(0, Math.Min(255, g * 255));
            int i = (y * size + x) * 4;
            px[i] = (byte)Math.Min(255, val + 3); // B, slightly cool
            px[i + 1] = (byte)val;
            px[i + 2] = (byte)val;
            px[i + 3] = 255;
        }
        Marshal.Copy(px, 0, data.Scan0, px.Length);
        bmp.UnlockBits(data);
        bmp.Save(path, ImageFormat.Png);
        bmp.Dispose();
    }
}
'@
[Ceramic]::Make($Out, 512)
"saved $Out"
