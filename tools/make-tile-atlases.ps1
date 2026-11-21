# Generates HexBlack/SquareBlack/SquareWhite/HexWhite atlases (4x4 cells of 256 px, alpha = gloss). Deterministic.
param([string]$OutDir)
if (-not $OutDir) { $OutDir = Join-Path $PSScriptRoot '..\GameData\AblatorDecals\Textures' }
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Force $OutDir | Out-Null }
$OutDir = (Resolve-Path $OutDir).Path
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

public static class Atlas
{
    const int Cell = 256, Cells = 4, Size = 1024;

    static double Lat(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535.0;
        }
    }

    // Smooth value noise, not tiled (every cell has its own seed).
    static double Noise(double x, double y, int seed)
    {
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        double fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        double a = Lat(x0, y0, seed), b = Lat(x0 + 1, y0, seed);
        double c = Lat(x0, y0 + 1, seed), d = Lat(x0 + 1, y0 + 1, seed);
        double top = a + (b - a) * fx, bot = c + (d - c) * fx;
        return top + (bot - top) * fy;
    }

    static double Smooth(double e0, double e1, double x)
    {
        double t = Math.Max(0, Math.Min(1, (x - e0) / (e1 - e0)));
        return t * t * (3 - 2 * t);
    }

    static double Lerp(double a, double b, double t) { return a + (b - a) * t; }

    static int[] Shuffled(Random rnd, int n)
    {
        int[] p = new int[n];
        for (int i = 0; i < n; i++) p[i] = i;
        for (int i = n - 1; i > 0; i--) { int j = rnd.Next(i + 1); int t = p[i]; p[i] = p[j]; p[j] = t; }
        return p;
    }

    static double[,] TextMask(Random rnd, int w, int h, Font font)
    {
        string l1 = "V070-19" + rnd.Next(1000, 10000);
        string l2 = "-" + rnd.Next(100, 1000);
        double[,] m = new double[w, h];
        using (Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Black);
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                g.DrawString(l1, font, Brushes.White, 0, 0);
                g.DrawString(l2, font, Brushes.White, 0, 14);
            }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    m[x, y] = bmp.GetPixel(x, y).R / 255.0;
        }
        return m;
    }

    // kind: 0 = hex black, 1 = square black, 2 = square white, 3 = hex white (white style, hex cells)
    public static void Make(string path, int kind, int seed)
    {
        bool hex = kind == 0 || kind == 3, white = kind >= 2;
        Random rnd = new Random(seed);
        double[] fac = new double[16];
        int[] perm = Shuffled(rnd, 16);
        for (int i = 0; i < 16; i++) fac[perm[i]] = (kind == 0 ? 0.65 : 0) + (kind == 0 ? 0.35 : 1.0) * i / 15.0;
        bool[] hasText = new bool[16], odd = new bool[16];
        int[] p1 = Shuffled(rnd, 16), p2 = Shuffled(rnd, 16);
        for (int i = 0; i < 8; i++) hasText[p1[i]] = kind > 0;
        for (int i = 0; i < (white ? 6 : 3); i++) odd[p2[i]] = kind > 0;

        FontFamily ff;
        try { ff = new FontFamily("Consolas"); } catch (Exception) { ff = new FontFamily("Arial"); }
        Font font = new Font(ff, 12f, FontStyle.Bold, GraphicsUnit.Pixel);

        byte[] px = new byte[Size * Size * 4];
        for (int c = 0; c < 16; c++)
        {
            int cx = c % Cells, cy = c / Cells;
            int cs = seed * 1000 + c * 10;
            double f = fac[c];
            double tint = (rnd.NextDouble() * 2 - 1) * (kind == 0 ? 0.05 : 0.02);

            // chips / scratches
            bool wear = kind == 0 || odd[c];
            int nChips = wear ? 2 + rnd.Next(3) : 0;
            double[] chX = new double[nChips], chY = new double[nChips], chR = new double[nChips];
            for (int i = 0; i < nChips; i++)
            {
                double a = rnd.NextDouble() * Math.PI * 2, r = 60 + rnd.NextDouble() * 45;
                chX[i] = 127.5 + Math.Cos(a) * r; chY[i] = 127.5 + Math.Sin(a) * r;
                chR[i] = 2 + rnd.NextDouble() * 3.5;
            }
            int nSc = wear ? 1 + rnd.Next(2) : 0;
            double[] sx0 = new double[nSc], sy0 = new double[nSc], sx1 = new double[nSc], sy1 = new double[nSc];
            for (int i = 0; i < nSc; i++)
            {
                double a = rnd.NextDouble() * Math.PI * 2, r = rnd.NextDouble() * 80, ang = rnd.NextDouble() * Math.PI, len = 12 + rnd.NextDouble() * 22;
                sx0[i] = 127.5 + Math.Cos(a) * r; sy0[i] = 127.5 + Math.Sin(a) * r;
                sx1[i] = sx0[i] + Math.Cos(ang) * len; sy1[i] = sy0[i] + Math.Sin(ang) * len;
            }
            // part number (hex: further in, so it stays inside the hexagon; same number of random draws)
            int tx = hex ? 70 + rnd.Next(0, 20) : 22 + rnd.Next(0, 30), ty = hex ? 132 + rnd.Next(0, 26) : 150 + rnd.Next(0, 40);
            double[,] mask = TextMask(rnd, 110, 30, font);

            for (int ly = 0; ly < Cell; ly++)
            for (int lx = 0; lx < Cell; lx++)
            {
                double x = lx - 127.5, y = ly - 127.5;
                double d = hex
                    ? 110.85 - Math.Max(Math.Abs(x), Math.Abs(x) * 0.5 + Math.Abs(y) * 0.866)
                    : 127.5 - Math.Max(Math.Abs(x), Math.Abs(y));
                int ex = Math.Min(Math.Min(lx, ly), Math.Min(Cell - 1 - lx, Cell - 1 - ly));
                double n1 = Noise(lx / 40.0, ly / 40.0, cs) * 0.5 + Noise(lx / 16.0, ly / 16.0, cs + 1) * 0.3 + Noise(lx / 6.0, ly / 6.0, cs + 2) * 0.2;
                double grain = Lat(lx, ly, cs + 3) - 0.5;
                double n2 = Noise(lx / 9.0, ly / 9.0, cs + 4);
                double sw = kind == 0 ? 3 + 2 * n2 : 2.5 + 1.5 * n2;
                double w = 1 - Smooth(sw - 1, sw + 1, d);
                if (ex < 2) w = 1;

                double r, g, b, alpha;
                double body, saum;
                if (kind == 0)
                {
                    body = 0.16 * f * (1 + (n1 - 0.5) * 0.5 + grain * 0.15);
                    if (Lat(lx, ly, cs + 5) > 0.994) body *= 1.6;
                    saum = 0.16 * f + 0.075 + (n2 - 0.5) * 0.05;
                    alpha = 0.25 + 0.2 * (n2 * 0.6 + (grain + 0.5) * 0.4);
                    double gg = Lerp(body, saum, w);
                    r = gg * (1 + tint); g = gg; b = gg * (1 - tint);
                }
                else if (kind == 1)
                {
                    body = (0.05 + 0.08 * f) * (1 + (n1 - 0.5) * 0.5 + grain * 0.15);
                    if (odd[c]) body += 0.045 + (n1 - 0.5) * 0.05;
                    saum = body + 0.065 + (n2 - 0.5) * 0.03;
                    alpha = 0.5 + 0.3 * (n2 * 0.6 + (grain + 0.5) * 0.4);
                    if (odd[c]) alpha -= 0.15;
                    double gg = Lerp(body, saum, w);
                    r = gg * (1 + tint); g = gg; b = gg * (1 - tint);
                }
                else
                {
                    body = (0.80 + 0.12 * f) * (1 + (n1 - 0.5) * 0.06 + grain * 0.03);
                    saum = body - 0.22 + (n2 - 0.5) * 0.04;
                    alpha = 0.15 + 0.15 * (n2 * 0.6 + (grain + 0.5) * 0.4);
                    double gg = Lerp(body, saum, w);
                    r = gg * (1 + tint); g = gg; b = gg * (1 - tint);
                    if (odd[c])
                    {
                        double dirt = Smooth(0.25, 0.95, Noise(lx / 110.0, ly / 110.0, cs + 6)) * 0.15 * (1 - w);
                        r *= 1 - 0.2 * dirt; g *= 1 - 0.26 * dirt; b *= 1 - 0.34 * dirt;
                    }
                }

                // chips and scratches (never in the rim zone)
                if (w < 0.5)
                {
                    double hit = 0;
                    for (int i = 0; i < nChips; i++)
                    {
                        double dx = lx - chX[i], dy = ly - chY[i];
                        double rr = chR[i] * (0.6 + 0.8 * Noise(lx * 0.4, ly * 0.4, cs + 7));
                        double dist = Math.Sqrt(dx * dx + dy * dy);
                        if (dist < rr) hit = Math.Max(hit, 1 - dist / rr);
                    }
                    for (int i = 0; i < nSc; i++)
                    {
                        double vx = sx1[i] - sx0[i], vy = sy1[i] - sy0[i];
                        double t = Math.Max(0, Math.Min(1, ((lx - sx0[i]) * vx + (ly - sy0[i]) * vy) / (vx * vx + vy * vy)));
                        double dx = lx - (sx0[i] + vx * t), dy = ly - (sy0[i] + vy * t);
                        double dist = Math.Sqrt(dx * dx + dy * dy);
                        if (dist < 0.9) hit = Math.Max(hit, 0.6 * (1 - dist / 0.9));
                    }
                    if (hit > 0)
                    {
                        double tone = white ? -0.22 : (kind == 0 ? 0.12 : 0.1);
                        r += tone * hit; g += tone * hit; b += tone * hit;
                        alpha += white ? 0.05 * hit : -0.1 * hit;
                    }
                }

                // part number
                if (hasText[c])
                {
                    int mx = lx - tx, my = ly - ty;
                    if (mx >= 0 && my >= 0 && mx < 110 && my < 30)
                    {
                        double m = mask[mx, my] * (kind == 1 ? 0.7 : 0.6);
                        double tr = kind == 1 ? 0.88 : 0.22, tg = kind == 1 ? 0.84 : 0.22, tb = kind == 1 ? 0.55 : 0.24;
                        r = Lerp(r, tr, m); g = Lerp(g, tg, m); b = Lerp(b, tb, m);
                    }
                }

                int i0 = ((cy * Cell + ly) * Size + cx * Cell + lx) * 4;
                px[i0] = (byte)Math.Max(0, Math.Min(255, b * 255 + 0.5));
                px[i0 + 1] = (byte)Math.Max(0, Math.Min(255, g * 255 + 0.5));
                px[i0 + 2] = (byte)Math.Max(0, Math.Min(255, r * 255 + 0.5));
                px[i0 + 3] = (byte)Math.Max(0, Math.Min(255, alpha * 255 + 0.5));
            }
        }

        using (Bitmap bmp = new Bitmap(Size, Size, PixelFormat.Format32bppArgb))
        {
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, Size, Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(px, 0, data.Scan0, px.Length);
            bmp.UnlockBits(data);
            bmp.Save(path, ImageFormat.Png);
        }
        font.Dispose();
    }
}
'@
[Atlas]::Make((Join-Path $OutDir 'HexBlack.png'), 0, 11)
[Atlas]::Make((Join-Path $OutDir 'SquareBlack.png'), 1, 22)
[Atlas]::Make((Join-Path $OutDir 'SquareWhite.png'), 2, 33)
[Atlas]::Make((Join-Path $OutDir 'HexWhite.png'), 3, 44)
"saved atlases to $OutDir"
