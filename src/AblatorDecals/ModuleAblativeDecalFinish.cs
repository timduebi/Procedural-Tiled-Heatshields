namespace AblatorDecals
{
    // Tile finish (Black, White, Ceramic); ModuleAblativeDecal reads it, shows it in its PAW and picks the texture from
    // (pattern, finish). A module of its own so B9PartSwitch can drive pattern and finish with two switchers: B9 refuses two
    // switchers on one module, and deselecting a subtype reloads the module's original node, which would reset the other one.
    public class ModuleAblativeDecalFinish : PartModule
    {
        [KSPField(isPersistant = true)] public string finish = "Black";

        // B9PartSwitch sets the field through PartModule.Load, also after OnStart: let the decal rebuild (editor only)
        public override void OnLoad(ConfigNode node)
        {
            if (part == null) return;
            ModuleAblativeDecal decal = part.FindModuleImplementing<ModuleAblativeDecal>();
            if (decal != null) decal.SettingsLoaded();
        }
    }
}
