using HarmonyLib;

namespace Rebirth.Vehicles.Camper
{
    // Called explicitly by REBIRTH bootstrap; does not scan or patch the whole assembly.
    public static class RebirthCamperPatchInstaller
    {
        private static readonly object Sync = new object();
        private static readonly Harmony Harmony = new Harmony("rebirth.camper.production");
        private static int installed;

        public static void Install()
        {
            lock (Sync)
            {
                // Remember each completed class so a failed later step can be retried safely.
                if (installed < 1)
                {
                    RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthCamperInitialDye));
                    installed = 1;
                }
                if (installed < 2)
                {
                    RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthCamperCosmeticMigration));
                    installed = 2;
                }
                if (installed < 3)
                {
                    RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthCamperTerrainLog));
                    installed = 3;
                }
                if (installed < 4)
                {
                    // Older bundles have no rear-lamp targets and are skipped
                    // by the hook. Authored bundles use native vehicle state.
                    RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthCamperRearLights));
                    installed = 4;
                }
                if (installed < 5)
                {
                    RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthCamperBedPosePatch));
                    installed = 5;
                }
            }
        }
    }
}
