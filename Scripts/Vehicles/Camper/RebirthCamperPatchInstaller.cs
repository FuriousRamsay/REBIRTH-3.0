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
                    Harmony.CreateClassProcessor(typeof(RebirthCamperInitialDye)).Patch();
                    installed = 1;
                }
                if (installed < 2)
                {
                    Harmony.CreateClassProcessor(typeof(RebirthCamperCosmeticMigration)).Patch();
                    installed = 2;
                }
                if (installed < 3)
                {
                    Harmony.CreateClassProcessor(typeof(RebirthCamperTerrainLog)).Patch();
                    installed = 3;
                }
                if (installed < 4)
                {
                    // Older bundles have no rear-lamp targets and are skipped
                    // by the hook. Authored bundles use native vehicle state.
                    Harmony.CreateClassProcessor(typeof(RebirthCamperRearLights)).Patch();
                    installed = 4;
                }
                if (installed < 5)
                {
                    Harmony.CreateClassProcessor(typeof(RebirthCamperBedPosePatch)).Patch();
                    installed = 5;
                }
            }
        }
    }
}
