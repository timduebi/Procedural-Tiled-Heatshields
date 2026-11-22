using System;

namespace AblatorDecals
{
    // 1.10: seamless procedural reentry streak textures (pure managed, testable outside the game). Streaks run along V (the
    // wind direction on the overlay mesh, TileHost.EnsureStreaks), alpha 0 between them so the tiles show through.
    //   HexBlack    Starship: crisp feathery pale ash / silver / warm tan streaks with fine plasma filaments
    //   SquareBlack Shuttle: broader soft smoky ash-grey flow bands and mottled soot wisps
    //   Soot        white / steel / ceramic: dark charcoal-brown soot streaks
    public enum StreakStyle { HexBlack, SquareBlack, Soot }

    public static class CharStreaks
    {
        // RGBA32, rows bottom to top (Texture2D.LoadRawTextureData order); size must be a multiple of every period used (16)
        public static byte[] Generate(StreakStyle style, int size, int seed = 1)
        {
            var px = new byte[size * size * 4];
            for (int j = 0; j < size; j++)
            {
                float v = (float)j / size;
                for (int i = 0; i < size; i++)
                {
                    float u = (float)i / size;
                    Pixel(style, u, v, seed, out float r, out float g, out float b, out float a);
                    int o = 4 * (j * size + i);
                    px[o] = ToByte(r); px[o + 1] = ToByte(g); px[o + 2] = ToByte(b); px[o + 3] = ToByte(a);
                }
            }
            return px;
        }

        // one texel; u, v in [0, 1), periodic in both (every period is an integer)
        public static void Pixel(StreakStyle style, float u, float v, int seed, out float r, out float g, out float b, out float a)
        {
            // organic waviness: streaks meander slightly across the flow (warp periodic in v and u)
            float warp = 0.012f * (2 * Noise(0.5f, v * 3, 1, 3, seed + 11) - 1) + 0.005f * (2 * Noise(u * 4, v * 8, 4, 8, seed + 12) - 1);
            float uu = u + warp;
            float patch = Smooth(0.25f, 0.75f, Noise(u * 3, v * 2, 3, 2, seed + 13)); // patchy: some areas stay clean
            float tint = Noise(u * 6, v * 2, 6, 2, seed + 14);
            switch (style)
            {
                case StreakStyle.HexBlack:
                {
                    // broad soft fans, crisp streaks inside them and fine plasma filaments clustered in the fans (feathery)
                    float fan = Streaks(uu, v, 14, 2, 0.55f, 0.60f, 1.1f, seed);
                    float cluster = Streaks(uu, v, 14, 2, 0.80f, 0.60f, 0.6f, seed);           // same fans, wider: where filaments live
                    float crisp = Streaks(uu, v, 44, 2, 0.35f, 0.70f, 1.4f, seed + 1) * (0.25f + 0.75f * cluster);
                    float fil = Streaks(uu, v, 120, 3, 0.30f, 0.65f, 1.2f, seed + 2) * (0.15f + 0.85f * cluster);
                    float veil = 0.20f * Smooth(0.55f, 0.95f, Noise(uu * 24, v * 2, 24, 2, seed + 3)); // faint ash veil
                    a = 0.65f * Screen(0.70f * fan, crisp, 0.80f * fil, veil) * (0.45f + 0.55f * patch);
                    r = Lerp(0.80f, 0.80f, tint); g = Lerp(0.82f, 0.72f, tint); b = Lerp(0.85f, 0.62f, tint); // silver -> warm tan
                    break;
                }
                case StreakStyle.SquareBlack:
                {
                    // broad soft smoky flow bands, a few medium streaks and elongated soot wisps (Shuttle belly patina)
                    float band = Streaks(uu, v, 8, 1, 0.60f, 0.70f, 0.7f, seed);
                    float mid = Streaks(uu, v, 26, 2, 0.45f, 0.55f, 1.0f, seed + 1);
                    float wisp = Smooth(0.50f, 0.90f, Noise(uu * 32, v * 6, 32, 6, seed + 2));
                    a = 0.55f * Screen(0.70f * band, 0.60f * mid, 0.30f * wisp, 0) * (0.45f + 0.55f * patch);
                    r = Lerp(0.64f, 0.84f, tint); g = Lerp(0.64f, 0.85f, tint); b = Lerp(0.66f, 0.87f, tint); // ash grey -> pale silver
                    break;
                }
                default:
                {
                    float fan = Streaks(uu, v, 12, 2, 0.55f, 0.60f, 1.0f, seed);
                    float cluster = Streaks(uu, v, 12, 2, 0.80f, 0.60f, 0.6f, seed);
                    float crisp = Streaks(uu, v, 36, 2, 0.40f, 0.65f, 1.3f, seed + 1) * (0.30f + 0.70f * cluster);
                    float fil = Streaks(uu, v, 90, 3, 0.30f, 0.55f, 1.2f, seed + 2) * (0.20f + 0.80f * cluster);
                    float wisp = Smooth(0.55f, 0.92f, Noise(uu * 24, v * 6, 24, 6, seed + 3));
                    a = 0.65f * Screen(0.60f * fan, 0.80f * crisp, 0.60f * fil, 0.25f * wisp) * (0.45f + 0.55f * patch);
                    r = Lerp(0.10f, 0.14f, tint); g = Lerp(0.08f, 0.11f, tint); b = Lerp(0.07f, 0.09f, tint); // charcoal brown
                    break;
                }
            }
        }

        private static float Screen(float a, float b, float c, float d) => 1 - (1 - a) * (1 - b) * (1 - c) * (1 - d);

        // one layer of streaks: cols columns across u (a share "present" of them carry streaks), perV streaks per column along
        // v, each starting from a point, widening into a bright head, then fading and thinning out (exponent fade) downstream; width = share of a column
        private static float Streaks(float uu, float v, int cols, int perV, float width, float present, float fade, int seed)
        {
            float x = uu * cols;
            int c = (int)Math.Floor(x);
            float fx = x - c, best = 0;
            for (int dc = -1; dc <= 1; dc++)
            {
                int cc = Mod(c + dc, cols);
                float y = v * perV + Hash(cc, 3, seed);
                int rep = Mod((int)Math.Floor(y), perV);
                float t = y - (float)Math.Floor(y); // 0 at the head
                int id = cc * 7 + rep;
                if (Hash(id, 0, seed) > present) continue;
                float len = 0.50f + 0.45f * Hash(id, 4, seed);
                if (t >= len) continue;
                float k = t / len;
                float w = width * (0.5f + Hash(id, 2, seed)) * (0.15f + 0.85f * Smooth(0f, 0.25f, k)) * (1 - 0.5f * k); // pointed start, widens, thins out
                float d = Math.Abs(fx - (dc + 0.5f + 0.8f * (Hash(id, 1, seed) - 0.5f))) / w;
                if (d >= 1) continue;
                float across = 1 - d * d;
                float along = (float)Math.Pow(1 - k, fade) * Smooth(0f, 0.10f, k);
                best = Math.Max(best, across * across * along * (0.55f + 0.45f * Hash(id, 5, seed)));
            }
            return best;
        }

        // periodic value noise on an integer lattice (periods px, py), smoothstep-interpolated, [0, 1]
        public static float Noise(float x, float y, int px, int py, int seed)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            int x0 = Mod(ix, px), x1 = Mod(ix + 1, px), y0 = Mod(iy, py), y1 = Mod(iy + 1, py);
            float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed), c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
            return Lerp(Lerp(a, b, fx), Lerp(c, d, fx), fy);
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        private static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
        private static float Smooth(float e0, float e1, float x) { float t = Math.Max(0, Math.Min(1, (x - e0) / (e1 - e0))); return t * t * (3 - 2 * t); }
        private static byte ToByte(float f) => (byte)Math.Max(0, Math.Min(255, (int)Math.Round(f * 255)));
    }
}
