# Generates gleaming stainless-steel tile atlases (HexSteel, SquareSteel) and full PBR Metallic-Smoothness (*_MET.png: R=Metallic, A=Smoothness)
# plus tangent-space Normal maps (*_NRM.png: KSP auto-converts *_NRM to DXT5nm) for all finishes (Black, Steel, Ceramic, White).
# Deterministic. Does not touch HexBlack/SquareBlack/HexWhite/SquareWhite/Ceramic diffuse PNGs.
param([string]$OutDir)
if (-not $OutDir) { $OutDir = Join-Path $PSScriptRoot '..\GameData\AblatorDecals\Textures' }
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Force $OutDir | Out-Null }
$OutDir = (Resolve-Path $OutDir).Path

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class PbrMaps
{
    static double Lat(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535.0;
        }
    }

    static double Noise(double x, double y, int seed)
    {
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        double fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        double a = Lat(x0, y0, seed), b = Lat(x0 + 1, y0, seed);
        double c = Lat(x0, y0 + 1, seed), d = Lat(x0 + 1, y0 + 1, seed);
        return (a + (b - a) * fx) + ((c + (d - c) * fx) - (a + (b - a) * fx)) * fy;
    }

    static double TileNoise(double u, double v, int period, int seed)
    {
        double x = u * period, y = v * period;
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        double fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        int mx0 = ((x0 % period) + period) % period, mx1 = (mx0 + 1) % period;
        int my0 = ((y0 % period) + period) % period, my1 = (my0 + 1) % period;
        double a = Lat(mx0, my0, seed), b = Lat(mx1, my0, seed);
        double c = Lat(mx0, my1, seed), d = Lat(mx1, my1, seed);
        return (a + (b - a) * fx) + ((c + (d - c) * fx) - (a + (b - a) * fx)) * fy;
    }

    static double Smooth(double e0, double e1, double x)
    {
        double t = Math.Max(0, Math.Min(1, (x - e0) / (e1 - e0)));
        return t * t * (3 - 2 * t);
    }

    static double Clamp01(double x) { return x < 0 ? 0 : x > 1 ? 1 : x; }
    static double Lerp(double a, double b, double t) { return a + (b - a) * t; }
    static byte ToByte(double v) { return (byte)Math.Max(0, Math.Min(255, v * 255.0 + 0.5)); }

    static int[] Shuffled(Random rnd, int n)
    {
        int[] p = new int[n];
        for (int i = 0; i < n; i++) p[i] = i;
        for (int i = n - 1; i > 0; i--) { int j = rnd.Next(i + 1); int t = p[i]; p[i] = p[j]; p[j] = t; }
        return p;
    }

    // Signed distance inside cell (in pixels of a `cell`-sized cell): >0 inside tile, <=0 outside / seam
    static double TileDist(bool hex, double lx, double ly, int cell, out double gx, out double gy)
    {
        double half = (cell - 1) * 0.5;
        double x = lx - half, y = ly - half;
        double scale = cell / 256.0;
        if (hex)
        {
            double r = 110.85 * scale;
            double ax = Math.Abs(x), ay = Math.Abs(y);
            double d1 = ax;
            double d2 = ax * 0.5 + ay * 0.8660254;
            if (d1 >= d2)
            {
                gx = x >= 0 ? -1.0 : 1.0;
                gy = 0.0;
                return r - d1;
            }
            else
            {
                gx = (x >= 0 ? -0.5 : 0.5);
                gy = (y >= 0 ? -0.8660254 : 0.8660254);
                return r - d2;
            }
        }
        else
        {
            double r = 127.5 * scale;
            double ax = Math.Abs(x), ay = Math.Abs(y);
            if (ax >= ay) { gx = x >= 0 ? -1.0 : 1.0; gy = 0.0; return r - ax; }
            else { gx = 0.0; gy = y >= 0 ? -1.0 : 1.0; return r - ay; }
        }
    }

    static void SaveRgba(string path, int size, byte[] bgra)
    {
        using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        {
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(bgra, 0, data.Scan0, bgra.Length);
            bmp.UnlockBits(data);
            bmp.Save(path, ImageFormat.Png);
        }
    }

    // Gleaming PBR Stainless Steel Albedo (1024x1024, 4x4 cells). Clean mirror-steel panels (no part numbers!),
    // distinct per-tile F0 reflectance variation (gunmetal -> polished silver) + subtle warm/cool steel tint + crisp dark bevel seam.
    public static void MakeSteelAlbedo(string path, bool hex, int seed)
    {
        const int Size = 1024, Cells = 4, Cell = 256;
        var rnd = new Random(seed);
        int[] perm = Shuffled(rnd, 16);
        double[] tileRefl = new double[16];
        double[] tileWarm = new double[16];
        for (int i = 0; i < 16; i++)
        {
            // Range 0.56..0.94 so individual hex tiles catch dramatic bright/dark reflections like stamped stainless panels
            tileRefl[perm[i]] = 0.56 + 0.38 * (i / 15.0);
            tileWarm[i] = (rnd.NextDouble() * 2 - 1) * 0.045; // subtle warm champagne <-> cool chrome tint
        }

        byte[] px = new byte[Size * Size * 4];
        for (int c = 0; c < 16; c++)
        {
            int cx = c % Cells, cy = c / Cells;
            int cs = seed * 1000 + c * 17;
            double baseRefl = tileRefl[c];
            double warm = tileWarm[c];
            double brushAngle = (rnd.NextDouble() * 2 - 1) * 0.15; // slight per-tile brush direction variance
            double ca = Math.Cos(brushAngle), sa = Math.Sin(brushAngle);

            for (int ly = 0; ly < Cell; ly++)
            for (int lx = 0; lx < Cell; lx++)
            {
                double gx, gy;
                double d = TileDist(hex, lx, ly, Cell, out gx, out gy);
                int ex = Math.Min(Math.Min(lx, ly), Math.Min(Cell - 1 - lx, Cell - 1 - ly));

                double ux = lx * ca - ly * sa, uy = lx * sa + ly * ca;
                double brush = Noise(ux / 85.0, uy / 1.8, cs + 1) * 0.65 + Noise(ux / 28.0, uy / 0.9, cs + 2) * 0.35;
                double blotch = Noise(lx / 55.0, ly / 55.0, cs + 3);
                double grain = Lat(lx, ly, cs + 4) - 0.5;

                // Very crisp thin dark seam at the edge of each tile (so tiles read clearly even at gap=0)
                double seam = 1.0 - Smooth(1.5, 4.2, d);
                if (ex < 2) seam = 1.0;

                double body = baseRefl * (1.0 + (brush - 0.5) * 0.10 + (blotch - 0.5) * 0.06 + grain * 0.02);
                double rimCol = 0.14;
                double v = Lerp(body, rimCol, seam);

                double r = Clamp01(v * (1.0 + warm));
                double g = Clamp01(v * (1.0 + warm * 0.35));
                double b = Clamp01(v * (1.0 - warm * 0.85 + 0.015)); // slight cool stainless bias
                double a = Clamp01(Lerp(0.84, 0.20, seam)); // alpha also carries gloss for legacy fallback

                int i0 = ((cy * Cell + ly) * Size + cx * Cell + lx) * 4;
                px[i0] = ToByte(b);
                px[i0 + 1] = ToByte(g);
                px[i0 + 2] = ToByte(r);
                px[i0 + 3] = ToByte(a);
            }
        }
        SaveRgba(path, Size, px);
    }

    // PBR Atlas Maps (512x512, 4x4 cells of 128 px):
    //   metPath -> R=G=B=Metallic, A=Smoothness (exact TU/Metallic, Resurfaced/Standard, Unity Standard layout)
    //   nrmPath -> RGB tangent-space normal (X->R, Y->G [OpenGL/Unity +Y up], Z->B), saved as *_NRM.png so KSP converts to DXT5nm
    // finish: 0 = Black, 1 = White, 2 = Steel
    public static void MakeAtlasPbr(string metPath, string nrmPath, bool hex, int finish, int seed)
    {
        const int Size = 512, Cells = 4, Cell = 128;
        bool steel = finish == 2, white = finish == 1, squareBlack = finish == 0 && !hex;
        var rnd = new Random(seed);
        int[] permS = Shuffled(rnd, 16);
        int[] permO = Shuffled(rnd, 16);
        bool[] odd = new bool[16];
        for (int i = 0; i < 4; i++) odd[permO[i]] = true;

        double[] cellSmooth = new double[16];
        double[] cellMetal = new double[16];
        double[] tiltX = new double[16], tiltY = new double[16];
        for (int i = 0; i < 16; i++)
        {
            double t = i / 15.0;
            if (steel)
            {
                // Dramatic per-tile smoothness: satin brushed (0.72) up to near-mirror chrome (0.96)
                cellSmooth[permS[i]] = 0.72 + 0.24 * t;
                cellMetal[permS[i]] = 0.94 + 0.05 * t;
            }
            else if (squareBlack)
            {
                // Shuttle HRSI RCG borosilicate glaze: glossy-satin with distinct tile-to-tile sheen
                cellSmooth[permS[i]] = 0.44 + 0.24 * t;
                cellMetal[permS[i]] = 0.03;
            }
            else if (white)
            {
                cellSmooth[permS[i]] = 0.20 + 0.14 * t;
                cellMetal[permS[i]] = 0.01;
            }
            else
            {
                // Starship Hex Black TUFROC ceramic: semi-matte with subtle specular variation
                cellSmooth[permS[i]] = 0.26 + 0.20 * t;
                cellMetal[permS[i]] = 0.04;
            }
            // Per-tile micro-tilt: stronger on stamped steel panels (±3.0 deg -> sin ≈ 0.052) for mosaic shimmer!
            double maxTilt = steel ? 0.055 : 0.018;
            double ang = rnd.NextDouble() * Math.PI * 2.0;
            double mag = (0.35 + 0.65 * rnd.NextDouble()) * maxTilt;
            tiltX[i] = Math.Cos(ang) * mag;
            tiltY[i] = Math.Sin(ang) * mag;
        }

        byte[] metPx = new byte[Size * Size * 4];
        byte[] nrmPx = new byte[Size * Size * 4];

        for (int c = 0; c < 16; c++)
        {
            int cx = c % Cells, cy = c / Cells;
            int cs = seed * 1000 + c * 23;
            double sm0 = cellSmooth[c] - (squareBlack && odd[c] ? 0.14 : 0.0);
            double mt0 = cellMetal[c];
            double tx = tiltX[c], ty = tiltY[c];

            for (int ly = 0; ly < Cell; ly++)
            for (int lx = 0; lx < Cell; lx++)
            {
                double gx, gy;
                double d = TileDist(hex, lx, ly, Cell, out gx, out gy);
                int ex = Math.Min(Math.Min(lx, ly), Math.Min(Cell - 1 - lx, Cell - 1 - ly));

                double seam = 1.0 - Smooth(0.8, 2.3, d);
                if (ex < 1) seam = 1.0;

                // Bevel zone right inside the seam: slopes down toward tile edge
                double bevelBand = Smooth(0.5, 2.0, d) * (1.0 - Smooth(2.0, 5.2, d));
                double bevelSlope = steel ? 0.42 : 0.34;

                double micro = Noise(lx / 18.0, ly / 18.0, cs + 1) * 0.55 + Noise(lx / 6.0, ly / 6.0, cs + 2) * 0.45;
                double brush = steel ? (Noise(lx / 42.0, ly / 1.1, cs + 3) * 0.65 + Noise(lx / 14.0, ly / 0.6, cs + 4) * 0.35) : micro;

                // Metallic & Smoothness
                double m = Lerp(mt0, steel ? 0.22 : 0.02, seam);
                double s = sm0 + (brush - 0.5) * (steel ? 0.12 : 0.10) + (micro - 0.5) * 0.05;
                s = Lerp(Clamp01(s), steel ? 0.18 : 0.10, seam);

                // Tangent-space normal (Unity UV has v=0 at bottom, PNG row 0 is top -> flip Y gradient!)
                double nx = tx - gx * bevelSlope * bevelBand;
                double ny = ty + gy * bevelSlope * bevelBand; // +gy because PNG +y is down, UV +v is up
                if (steel)
                {
                    double bUp = Noise(lx / 42.0, (ly - 0.5) / 1.1, cs + 3);
                    double bDn = Noise(lx / 42.0, (ly + 0.5) / 1.1, cs + 3);
                    ny += (bDn - bUp) * 0.035 * (1.0 - seam);
                }
                else
                {
                    double mL = Noise((lx - 0.5) / 7.0, ly / 7.0, cs + 5);
                    double mR = Noise((lx + 0.5) / 7.0, ly / 7.0, cs + 5);
                    double mU = Noise(lx / 7.0, (ly - 0.5) / 7.0, cs + 5);
                    double mD = Noise(lx / 7.0, (ly + 0.5) / 7.0, cs + 5);
                    double amp = squareBlack ? 0.045 : 0.065;
                    nx += (mL - mR) * amp * (1.0 - seam);
                    ny += (mD - mU) * amp * (1.0 - seam);
                }
                double nz = Math.Sqrt(Math.Max(0.05, 1.0 - nx * nx - ny * ny));
                double inv = 1.0 / Math.Sqrt(nx * nx + ny * ny + nz * nz);
                nx *= inv; ny *= inv; nz *= inv;

                int i0 = ((cy * Cell + ly) * Size + cx * Cell + lx) * 4;
                byte mb = ToByte(m), sb = ToByte(s);
                metPx[i0] = mb; metPx[i0 + 1] = mb; metPx[i0 + 2] = mb; metPx[i0 + 3] = sb;

                nrmPx[i0] = ToByte(nz * 0.5 + 0.5);     // B = Z
                nrmPx[i0 + 1] = ToByte(ny * 0.5 + 0.5); // G = Y
                nrmPx[i0 + 2] = ToByte(nx * 0.5 + 0.5); // R = X
                nrmPx[i0 + 3] = 255;
            }
        }
        SaveRgba(metPath, Size, metPx);
        SaveRgba(nrmPath, Size, nrmPx);
    }

    // Tileable PBR maps for Plain Ceramic (512x512)
    public static void MakeCeramicPbr(string metPath, string nrmPath)
    {
        const int Size = 512;
        byte[] metPx = new byte[Size * Size * 4];
        byte[] nrmPx = new byte[Size * Size * 4];
        double eps = 1.0 / Size;
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            double u = (double)x / Size, v = (double)y / Size;
            double blotch = TileNoise(u, v, 8, 11) * 0.6 + TileNoise(u, v, 24, 12) * 0.4;
            double m = 0.02;
            double s = Clamp01(0.24 + (blotch - 0.5) * 0.14);

            double hL = TileNoise(u - eps, v, 32, 13) * 0.6 + TileNoise(u - eps, v, 64, 14) * 0.4;
            double hR = TileNoise(u + eps, v, 32, 13) * 0.6 + TileNoise(u + eps, v, 64, 14) * 0.4;
            double hU = TileNoise(u, v - eps, 32, 13) * 0.6 + TileNoise(u, v - eps, 64, 14) * 0.4;
            double hD = TileNoise(u, v + eps, 32, 13) * 0.6 + TileNoise(u, v + eps, 64, 14) * 0.4;
            double nx = (hL - hR) * 0.22;
            double ny = (hD - hU) * 0.22;
            double nz = Math.Sqrt(Math.Max(0.1, 1.0 - nx * nx - ny * ny));

            int i0 = (y * Size + x) * 4;
            byte mb = ToByte(m), sb = ToByte(s);
            metPx[i0] = mb; metPx[i0 + 1] = mb; metPx[i0 + 2] = mb; metPx[i0 + 3] = sb;

            nrmPx[i0] = ToByte(nz * 0.5 + 0.5);
            nrmPx[i0 + 1] = ToByte(ny * 0.5 + 0.5);
            nrmPx[i0 + 2] = ToByte(nx * 0.5 + 0.5);
            nrmPx[i0 + 3] = 255;
        }
        SaveRgba(metPath, Size, metPx);
        SaveRgba(nrmPath, Size, nrmPx);
    }
}
'@

[PbrMaps]::MakeSteelAlbedo((Join-Path $OutDir 'HexSteel.png'), $true, 177)
[PbrMaps]::MakeSteelAlbedo((Join-Path $OutDir 'SquareSteel.png'), $false, 188)

[PbrMaps]::MakeAtlasPbr((Join-Path $OutDir 'HexBlack_MET.png'),    (Join-Path $OutDir 'HexBlack_NRM.png'),    $true,  0, 11)
[PbrMaps]::MakeAtlasPbr((Join-Path $OutDir 'SquareBlack_MET.png'), (Join-Path $OutDir 'SquareBlack_NRM.png'), $false, 0, 22)
[PbrMaps]::MakeAtlasPbr((Join-Path $OutDir 'HexWhite_MET.png'),    (Join-Path $OutDir 'HexWhite_NRM.png'),    $true,  1, 44)
[PbrMaps]::MakeAtlasPbr((Join-Path $OutDir 'SquareWhite_MET.png'), (Join-Path $OutDir 'SquareWhite_NRM.png'), $false, 1, 33)
[PbrMaps]::MakeAtlasPbr((Join-Path $OutDir 'HexSteel_MET.png'),    (Join-Path $OutDir 'HexSteel_NRM.png'),    $true,  2, 177)
[PbrMaps]::MakeAtlasPbr((Join-Path $OutDir 'SquareSteel_MET.png'), (Join-Path $OutDir 'SquareSteel_NRM.png'), $false, 2, 188)
[PbrMaps]::MakeCeramicPbr((Join-Path $OutDir 'Ceramic_MET.png'),   (Join-Path $OutDir 'Ceramic_NRM.png'))

"Generated Steel albedos + 14 PBR maps (_MET / _NRM) in $OutDir"
