# Generates the glow (emission) atlases HexGlow/SquareGlow: same layout as make-tile-atlases.ps1 (4x4 cells), per tile a soft
# gradient from a warm white core to a dark orange rim, no noise (DESIGN-v11 M1). Deterministic.
# The shader multiplies this with _EmissiveColor = glowRamp tint (medium: orange x 0.55, peak: white x 1.2).
# -Preview <png>: also renders the three glow states (normal / medium / peak, 3x3 hex tiles each) from HexBlack + HexGlow.
param([string]$OutDir, [string]$Preview, [string]$VariantsDir)
if (-not $OutDir) { $OutDir = Join-Path $PSScriptRoot '..\GameData\AblatorDecals\Textures' }
$OutDir = (Resolve-Path $OutDir).Path
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class Glow
{
    const int Cells = 4;
    // colours of the emission texture (linear-ish, 0..1): core, rim, outermost rim (bevel)
    static readonly double[] Core = { 1.0, 0.93, 0.76 }, Rim = { 0.68, 0.27, 0.06 }, Edge = { 0.32, 0.09, 0.02 };
    static readonly double[] Flat = { 0.95, 0.74, 0.48 }; // 1.7.1: the common tone the atlas is pulled towards

    static double Smooth(double e0, double e1, double x)
    {
        double t = Math.Max(0, Math.Min(1, (x - e0) / (e1 - e0)));
        return t * t * (3 - 2 * t);
    }

    // distance to the tile outline in the 256 px cell units of the tile atlases, and its value at the centre
    static double Dist(bool hex, double x, double y, out double max)
    {
        max = hex ? 110.85 : 127.5;
        return hex ? 110.85 - Math.Max(Math.Abs(x), Math.Abs(x) * 0.5 + Math.Abs(y) * 0.866) : 127.5 - Math.Max(Math.Abs(x), Math.Abs(y));
    }

    // emission colour at cell point (x, y) (cell units, centre 0), cell factor f
    public static double[] At(bool hex, double x, double y, double f)
    {
        // rim zone from the outline distance, core from a mix with the round distance (no star-shaped contours)
        double max, d = Dist(hex, x, y, out max), th = Math.Max(0, d / max), tr = Math.Max(0, 1 - Math.Sqrt(x * x + y * y) / max);
        double e = Smooth(0, 0.1, th), c = Smooth(0.05, 0.8, 0.5 * th + 0.5 * tr);
        var o = new double[3];
        for (int k = 0; k < 3; k++) o[k] = f * (Edge[k] + (Rim[k] - Edge[k]) * e + (Core[k] - Rim[k]) * c);
        return o;
    }

    static byte B(double v) { return (byte)Math.Max(0, Math.Min(255, v * 255 + 0.5)); }

    static void Save(string path, byte[] px, int w, int h)
    {
        using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        {
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(px, 0, data.Scan0, px.Length);
            bmp.UnlockBits(data);
            bmp.Save(path, ImageFormat.Png);
        }
    }

    public static void Make(string path, bool hex, int size, int seed, bool ceramic = false)
    {
        var rnd = new Random(seed);
        int cell = size / Cells;
        var px = new byte[size * size * 4];
        for (int c = 0; c < Cells * Cells; c++)
        {
            double f = 0.97 + 0.03 * rnd.NextDouble(); // 1.7.1: barely any brightness per tile (less contrast between tiles)
            int cx = c % Cells, cy = c / Cells;
            double phase = (c + 0.5) * 2.39996;
            for (int ly = 0; ly < cell; ly++)
                for (int lx = 0; lx < cell; lx++)
                {
                    double x = (lx + 0.5) * 256.0 / cell - 128, y = (ly + 0.5) * 256.0 / cell - 128;
                    double[] o = At(hex, x, y, f);
                    if (ceramic || !hex)
                    {
                        // Soft asymmetric hot spot in the glaze. The rim stays cool on every cell.
                        double max, d = Dist(hex, x, y, out max);
                        double u = x / max, v = y / max;
                        double field = 0.5 + 0.5 * Math.Cos(2.8*u + phase) * Math.Sin(3.4*v - phase);
                        double core = Smooth(0.02, ceramic ? 0.68 : 0.82, d / max) * (0.80 + 0.20 * field);
                        double rim = Smooth(0, 0.12, d / max);
                        o[0] = f * (0.10 + 0.51 * rim + 0.39 * core);
                        o[1] = f * (0.025 + 0.19 * rim + (ceramic ? 0.75 : 0.67) * core);
                        o[2] = f * (0.008 + 0.055 * rim + (ceramic ? 0.77 : 0.65) * core);
                    }
                    // 1.7.1: flattened towards one warm tone (user: "man sieht alle Kacheln noch sehr genau ... schwarze Ränder"):
                    // the rim keeps a soft hint of the tile, no dark outline
                    for (int k = 0; k < 3; k++) o[k] = 0.25 * o[k] + 0.75 * f * Flat[k];
                    int i = ((cy * cell + ly) * size + cx * cell + lx) * 4;
                    px[i] = B(o[2]); px[i + 1] = B(o[1]); px[i + 2] = B(o[0]); px[i + 3] = 255;
                }
        }
        Save(path, px, size, size);
    }

    static double[,,] Load(string path, out int w, out int h)
    {
        using (var bmp = new Bitmap(path))
        {
            w = bmp.Width; h = bmp.Height;
            var o = new double[w, h, 3];
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var px = new byte[w * h * 4];
            Marshal.Copy(data.Scan0, px, 0, px.Length);
            bmp.UnlockBits(data);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    for (int k = 0; k < 3; k++) o[x, y, k] = px[(y * w + x) * 4 + 2 - k] / 255.0;
            return o;
        }
    }

    // Three panels of 3 x 3 pointy-top hex tiles: lit black tile (albedo x light) + emission texture x tint x strength,
    // tints/strengths as glowRamp (medium 1 0.5 0.14 x 0.55, peak 1 1 1 x 1.2). Joints dark.
    public static void Preview(string path, string albedoPath, string glowPath, bool hex)
    {
        int aw, ah, gw, gh;
        double[,,] alb = Load(albedoPath, out aw, out ah), glo = Load(glowPath, out gw, out gh);
        double[][] tint = { new[] { 0.0, 0, 0 }, new[] { 1 * 0.55, 0.5 * 0.55, 0.14 * 0.55 }, new[] { 1.2, 1.2, 1.2 } };
        const double s = 0.7;                     // preview px per atlas cell unit (tile 256 units high)
        double W = (hex ? 221.7 : 256) * s, H = 256 * s;        // hex flat-to-flat, corner-to-corner
        int panelW = (int)(3.5 * W) + 20, panelH = (int)(2 * (hex ? 0.75 : 1) * H + H) + 20, imgW = panelW * 3, imgH = panelH;
        var px = new byte[imgW * imgH * 4];
        var cellOf = new Random(5);
        int[,] cells = new int[3, 3];
        for (int r = 0; r < 3; r++) for (int q = 0; q < 3; q++) cells[r, q] = cellOf.Next(16);
        for (int p = 0; p < 3; p++)
            for (int py = 0; py < imgH; py++)
                for (int pxl = 0; pxl < panelW; pxl++)
                {
                    double bx = pxl - 10 - W / 2, by = py - 10 - H / 2;
                    double[] col = { 0.02, 0.02, 0.025 };   // background / joint
                    for (int r = 0; r < 3; r++)
                        for (int q = 0; q < 3; q++)
                        {
                            double cxp = q * W + (hex ? (r % 2) * W / 2 : 0), cyp = r * (hex ? 0.75 : 1) * H;
                            double x = (bx - cxp) / s, y = (by - cyp) / s, max;
                            double d = Dist(hex, x, y, out max);
                            if (d < 3) continue;          // outside the tile or in the joint (about 6 units wide)
                            int cell = cells[r, q], cx = cell % 4, cy = cell / 4;
                            int ax = Math.Min(aw - 1, (int)((cx * 256 + 128 + x) * aw / 1024.0)), ay = Math.Min(ah - 1, (int)((cy * 256 + 128 + y) * ah / 1024.0));
                            int gx = Math.Min(gw - 1, (int)((cx * 256 + 128 + x) * gw / 1024.0)), gy = Math.Min(gh - 1, (int)((cy * 256 + 128 + y) * gh / 1024.0));
                            for (int k = 0; k < 3; k++) col[k] = alb[ax, ay, k] * 0.9 + glo[gx, gy, k] * tint[p][k];
                        }
                    int i = (py * imgW + p * panelW + pxl) * 4;
                    px[i] = B(col[2]); px[i + 1] = B(col[1]); px[i + 2] = B(col[0]); px[i + 3] = 255;
                }
        using (var bmp = new Bitmap(imgW, imgH + 28, PixelFormat.Format32bppArgb))
        {
            BitmapData data = bmp.LockBits(new Rectangle(0, 28, imgW, imgH), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(px, 0, data.Scan0, px.Length);
            bmp.UnlockBits(data);
            using (Graphics g = Graphics.FromImage(bmp))
            using (var font = new Font("Arial", 13f, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                g.FillRectangle(Brushes.Black, 0, 0, imgW, 28);
                string[] names = { "normal (< 550 K)", "mittel (800 K: orange x 0.55)", "Spitze (1300 K: weiss x 1.2)" };
                for (int p = 0; p < 3; p++) g.DrawString(names[p], font, Brushes.White, p * panelW + 10, 6);
            }
            bmp.Save(path, ImageFormat.Png);
        }
    }
}
'@
[Glow]::Make((Join-Path $OutDir 'HexGlow.png'), $true, 512, 61, $false)
[Glow]::Make((Join-Path $OutDir 'SquareGlow.png'), $false, 512, 62, $false)
[Glow]::Make((Join-Path $OutDir 'HexCeramicGlow.png'), $true, 512, 63, $true)
[Glow]::Make((Join-Path $OutDir 'SquareCeramicGlow.png'), $false, 512, 64, $true)
"saved glow atlases to $OutDir"
if ($Preview) {
    $dir = Split-Path -Parent $Preview
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }
    [Glow]::Preview([IO.Path]::GetFullPath($Preview), (Join-Path $OutDir 'HexBlack.png'), (Join-Path $OutDir 'HexGlow.png'), $true)
    "saved $Preview"
}

if ($VariantsDir) {
    $dir = (New-Item -Force -ItemType Directory $VariantsDir).FullName
    foreach ($shape in 'Hex', 'Square') {
        foreach ($finish in 'Black', 'Ceramic') {
            $base = if ($finish -eq 'Black') { "${shape}Black" } else { 'Ceramic' }
            $map = if ($finish -eq 'Black') { "${shape}Glow" } else { "${shape}CeramicGlow" }
            [Glow]::Preview((Join-Path $dir "${shape}${finish}.png"), (Join-Path $OutDir "$base.png"), (Join-Path $OutDir "$map.png"), ($shape -eq 'Hex'))
        }
    }
}
