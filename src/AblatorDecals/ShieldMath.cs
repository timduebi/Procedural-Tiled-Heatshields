using System;

namespace AblatorDecals
{
    // Pure math, no Unity/KSP types, so tests/ShieldMathCheck can run it outside the game.
    public static class ShieldMath
    {
        // Share of the parent's frontal area this shield covers, before the per-parent 100 % cap.
        public static double RawCoverage(double shieldArea, double facing, double parentExposedArea)
        {
            if (parentExposedArea <= 0) return 0;
            return shieldArea * Math.Max(0, facing) / parentExposedArea;
        }

        // Scales this shield down so all shields on one parent sum to at most 100 %.
        public static double Coverage(double raw, double rawSumOnParent)
        {
            if (rawSumOnParent <= 0) return 0;
            return raw * Math.Min(1, 1 / rawSumOnParent);
        }

        // Returns the absorbed flux in kW; unitsUsed is the ablator burned this tick.
        public static double Absorb(double coverage, double convectionFluxKW, double ablatorUnits,
            double kJPerUnit, double dt, out double unitsUsed)
        {
            unitsUsed = 0;
            double q = coverage * Math.Max(0, convectionFluxKW);
            if (q <= 0 || ablatorUnits <= 0 || kJPerUnit <= 0 || dt <= 0) return 0;
            unitsUsed = q * dt / kJPerUnit;
            if (unitsUsed > ablatorUnits)
            {
                unitsUsed = ablatorUnits;
                q = unitsUsed * kJPerUnit / dt;
            }
            return q;
        }
    }
}
