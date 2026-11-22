using System;
using AblatorDecals;

// Tile glow model: equilibrium, stability, cooling, no flow.
static class GlowChecks
{
    static void Check(bool ok, string what)
    {
        if (!ok) throw new Exception("FAIL: " + what);
        Console.WriteLine("ok   " + what);
    }

    public static void Run()
    {
        // equilibrium under constant flow (dt = 0.02 s and a coarse 0.5 s)
        foreach (double dt in new[] { 0.02, 0.5 })
        {
            var g = new TileGlow(1500);
            for (int i = 0; i < (int)(600 / dt); i++) g.Step(dt, 1500, 6000, 0.05, 1);
            Check(g.T > 1650 && g.T < 1850, $"glow equilibrium dt={dt}: T={g.T:F0} K in 1650..1850");
        }

        // stability: huge flow and large steps never overshoot the driving temperature or go NaN
        var s = new TileGlow(300);
        double max = 0;
        for (int i = 0; i < 400; i++) { s.Step(0.5, 300, 6000, 5, 1); max = Math.Max(max, s.T); if (double.IsNaN(s.T)) break; }
        Check(!double.IsNaN(max) && max <= 6000 + 1e-6, $"glow stable at dt = 0.5 s (max {max:F0} K)");

        // cooling back to the skin temperature without flow
        var c = new TileGlow(1700);
        for (int i = 0; i < 4000; i++) c.Step(0.5, 400, 6000, 0.05, 0);
        Check(Math.Abs(c.T - 400) < 5, $"glow cools to skin temperature: T={c.T:F1} K");

        // f = 0 keeps the skin temperature
        var z = new TileGlow(900);
        for (int i = 0; i < 200; i++) z.Step(0.5, 900, 6000, 0.05, 0);
        Check(Math.Abs(z.T - 900) < 1e-6, "glow with f = 0 stays at skin temperature");

        // 1.7.4: pitching up in cool air after reentry never spikes display temp; hot reentry still shows windward gradient
        double levelCool = TileGlow.BinTemp(650, 520, 320, 0.0, 0.8);
        double pitchUpCool = TileGlow.BinTemp(650, 520, 320, 0.8, 0.8);
        Check(Math.Abs(levelCool - 520) < 1e-6 && Math.Abs(pitchUpCool - 520) < 1e-6,
            $"glow cool-air pitch-up: level={levelCool:F1} K, pitchUp={pitchUpCool:F1} K (no red flash)");
        double hotLee = TileGlow.BinTemp(1000, 1400, 2200, 0.0, 1.0);
        double hotWind = TileGlow.BinTemp(1000, 1400, 2200, 1.0, 1.0);
        Check(hotWind > 1450 && hotLee < 1200, $"glow hot reentry gradient: lee={hotLee:F0} K, windward={hotWind:F0} K");
        Check(TileGlow.EffectiveFacing(0.0, 310, 850) >= 0.65 && TileGlow.EffectiveFacing(0.0, 2000, 850) == 0.0,
            "glow effective facing: cools in level flight, heats only windward");

        // 1.9: lee 0.15 .. stagnation 1.25; directional weighting off at <= 720 K air, full at >= 900 K
        Check(Math.Abs(TileGlow.LocalWeight(0, 1) - 0.15) < 1e-9 && Math.Abs(TileGlow.LocalWeight(1, 1) - 1.25) < 1e-9,
            $"glow local weight 0.15..1.25 ({TileGlow.LocalWeight(0, 1):F2}..{TileGlow.LocalWeight(1, 1):F2})");
        Check(Math.Abs(TileGlow.BinTemp(600, 900, 720, 1.0, 1.0) - 900) < 1e-9 && Math.Abs(TileGlow.BinTemp(600, 900, 720, 0.0, 1.0) - 900) < 1e-9,
            "glow onset: no directional weighting at 720 K air");
        Check(Math.Abs(TileGlow.BinTemp(600, 900, 900, 0.0, 1.0) - 645) < 1e-6, "glow onset: full lee weighting at 900 K air");

        // 1.9: fast post-reentry cool-down (module defaults: heat capacity 10, conduction 0.08, emissivity 0.95) in 250 K air
        // under a dense-air film (h = 0.5 kW/m2K): 1300 K below 720 K (no glow) within 15 s; the 1.8 defaults took > 30 s
        double CoolTime(double cap, double cond)
        {
            var g = new TileGlow(1300) { Emissivity = 0.95, HeatCapacity = cap, Conduction = cond };
            double t = 0;
            while (g.T > 720 && t < 120) { g.Step(0.02, 250, 250, 0.5, 1); t += 0.02; }
            return t;
        }
        double fast = CoolTime(10, 0.08), slow = CoolTime(30, 0.02);
        Check(fast < 15 && slow > 30, $"glow post-reentry cool-down 1300 -> 720 K: {fast:F1} s (1.8 defaults {slow:F1} s)");
    }
}
