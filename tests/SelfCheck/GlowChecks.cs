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
    }
}
