using System;

namespace AblatorDecals
{
    // Visual tile surface temperature (pure math, tests/SelfCheck runs it outside the game).
    // C dT/dt = h f (Text - T) - eps sigma (T^4 - Tskin^4) - k (T - Tskin), integrated linearised-implicit (stable for any dt).
    public class TileGlow
    {
        public const double Sigma = 5.670374e-11; // kW/m2K4

        public double Emissivity = 0.85, HeatCapacity = 6, Conduction = 0.02; // -, kJ/m2K, kW/m2K

        public double T { get; private set; }

        public TileGlow(double skinTemp) { T = skinTemp; }

        // 1.7.1: local glow. A tile area facing the flow by f = max(0, n.v) (fMax: the most exposed area of the vessel) glows with
        // skin + (T - skin) x LocalWeight: 0.15 on faces in the lee, 1.25 on the most exposed (nose, belly, leading edges), smoothstep
        // in between, so the gradient has no visible steps between neighbouring glow bins. 1.9: lee darker, so the nose and leading
        // edges light up cherry red first.
        public static double LocalWeight(double f, double fMax)
        {
            double x = fMax > 1e-6 ? Math.Max(0, Math.Min(1, f / fMax)) : 1;
            return 0.15 + 1.10 * x * x * (3 - 2 * x);
        }

        // 1.7.4: directional heating acts on windward faces (fb); convective cooling in cold air acts across the boundary layer.
        public static double EffectiveFacing(double fb, double extTemp, double tileTemp) =>
            extTemp < tileTemp ? Math.Max(fb, 0.65) : fb;

        // 1.7.4: per-bin display temperature. Directional LocalWeight (0.15..1.25) smoothly activates only while external air
        // is hot enough to heat tiles (1.9: > 720 K, full at 900 K), so pitching up in cool air after reentry never flashes red.
        public static double BinTemp(double skin, double shown, double extTemp, double f, double fMax)
        {
            double u = Math.Max(0.0, Math.Min(1.0, (extTemp - 720.0) / 180.0));
            double activeHeat = u * u * (3.0 - 2.0 * u);
            double w = 1.0 + (LocalWeight(f, fMax) - 1.0) * activeHeat;
            double baseT = Math.Min(skin, shown);
            return baseT + (shown - baseT) * w;
        }

        // joints glow with the tiles (1.7.1: no black lines on hot tiles), dimmer
        public const float JointGlow = 0.75f;

        // h in kW/m2K, f = max(0, n.v) of the bin, returns the new temperature
        public double Step(double dt, double skinTemp, double extTemp, double h, double f)
        {
            if (dt <= 0) return T;
            double hf = Math.Max(0, h) * Math.Max(0, f);
            double t3 = T * T * T;
            double es = Emissivity * Sigma;
            double drive = hf * (extTemp - T) - es * (t3 * T - Math.Pow(skinTemp, 4)) - Conduction * (T - skinTemp);
            double dT = drive / (HeatCapacity / dt + hf + 4 * es * t3 + Conduction);
            T = Math.Max(0, T + dT);
            return T;
        }
    }
}
