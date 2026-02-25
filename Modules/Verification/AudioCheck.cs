namespace NeonLite.Modules.Verification
{
    [Module]
    internal static class AudioCheck
    {
        const bool priority = true;
        const bool active = true;

        const string FAIL = "SFX volume is 0";

        static void Activate(bool _)
        {
            Patching.AddPatch(typeof(MenuScreenOptionsPanel), "ApplyChanges", PostApply, Patching.PatchTarget.Postfix);
        }

        static string CheckVerification()
        {
            if (GameDataManager.prefs.volSFX * GameDataManager.prefs.volMaster <= 0)
                return FAIL;
            return null;
        }

        static void PostApply()
        {
            if (GameDataManager.prefs.volSFX * GameDataManager.prefs.volMaster <= 0)
                Verifier.SetRunUnverifiable(typeof(AudioCheck), FAIL);
        }
    }
}
