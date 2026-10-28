using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using Platform;

namespace Rebirth.VehicleStudio.Diagnostics
{
    public sealed class ConsoleCmdCamperDyeCheck : ConsoleCmdAbstract
    {
        public override bool IsExecuteOnClient => true;
        public override DeviceFlag AllowedDeviceTypesClient => DeviceFlag.StandaloneWindows;
        public override string[] getCommands() => new[] { "camperdyecheck" };
        public override string getDescription() => "Check actual native camper item construction and clone/removal cosmetics in CodexTest.";
        public override string getHelp() => "camperdyecheck: creates in-memory test items only; does not give or place vehicles.";
        public override void Execute(List<string> args, CommandSenderInfo sender)
        {
            string stage = "preflight";
            try
            {
                if (GameManager.IsDedicatedServer || GamePrefs.GetString(EnumGamePrefs.GameName) != "CodexTest")
                    throw new Exception("Disposable CodexTest only.");
                stage = "actual production hook ownership";
                var originals = new System.Reflection.MethodBase[] {
                    AccessTools.Constructor(typeof(ItemValue), new[] { typeof(int), typeof(int), typeof(int), typeof(bool), typeof(string[]), typeof(float) }),
                    AccessTools.Method(typeof(ItemValue), "ReadData"),
                    AccessTools.Method(typeof(EntityVehicle), "CheckForOutOfWorld") };
                string[] hookNames = { "RebirthCamperInitialDye", "RebirthCamperCosmeticMigration", "RebirthCamperTerrainLog" };
                var hookOwners = new string[3];
                for (int index = 0; index < originals.Length; index++)
                {
                    var patches = Harmony.GetPatchInfo(originals[index]);
                    if (patches == null) throw new Exception("Production patch missing: " + hookNames[index]);
                    string expectedName = hookNames[index];
                    var matching = patches.Postfixes.Concat(patches.Transpilers).Where(p =>
                        p.PatchMethod.DeclaringType.FullName == expectedName).ToArray();
                    if (matching.Length != 1 || matching[0].owner != "rebirth.camper.production"
                        || matching[0].PatchMethod.DeclaringType.Assembly == typeof(CamperDiagnosticsMod).Assembly)
                        throw new Exception("Expected exactly one actual production owner for " + expectedName);
                    hookOwners[index] = matching[0].owner + ":" + matching[0].PatchMethod.DeclaringType.Assembly.GetName().Name;
                }
                ItemClass camper = ItemClass.GetItemClass("rebirthCamper80sPlaceable");
                if (camper == null || !camper.Properties.GetBool("RebirthCamperRandomInitialDye"))
                    throw new Exception("Random initial dye candidate is not enabled.");
                stage = "native construction";
                var created = new ItemValue(camper.Id, true);
                var dye = created.GetCosmeticMod(0);
                if (dye == null || dye.IsEmpty()) throw new Exception("Fresh item has no dye.");
                stage = "initial clone";
                var clone = created.Clone();
                if (clone.GetCosmeticMod(0) == null || clone.GetCosmeticMod(0).type != dye.type) throw new Exception("Clone rerolled initial dye.");
                stage = "removal clone";
                created.SetCosmeticMod(0, ItemValue.None);
                var removedClone = created.Clone();
                var removed = removedClone.GetCosmeticMod(0);
                if (removed != null && !removed.IsEmpty()) throw new Exception("Clone restored a removed dye.");
                stage = "red replacement lookup";
                ItemClass red = ItemClass.GetItemClass("modDyeRed");
                if (red == null) throw new Exception("Native red dye lookup returned null.");
                created.SetCosmeticMod(0, new ItemValue(red.Id, false));
                if (created.Clone().GetCosmeticMod(0).type != red.Id) throw new Exception("Clone replaced player-selected dye.");
                stage = "native serialization";
                if (RoundTrip(created).GetCosmeticMod(0).type != red.Id) throw new Exception("Native serialization replaced selected dye.");
                if (!RoundTrip(removedClone).GetCosmeticMod(0).IsEmpty()) throw new Exception("Native serialization restored removed dye.");
                var legacy = created.Clone();
                legacy.cosmeticMods = null;
                var migrated = RoundTrip(legacy);
                if (migrated.CosmeticModCount != 1 || !migrated.GetCosmeticMod(0).IsEmpty())
                    throw new Exception("Legacy camper did not receive exactly one empty cosmetic slot.");
                stage = "result export";
                Directory.CreateDirectory(CamperDiagnosticsMod.ExportFolder);
                string file = Path.Combine(CamperDiagnosticsMod.ExportFolder, "dye_check_" + Guid.NewGuid().ToString("N") + ".json");
                File.WriteAllText(file, JsonConvert.SerializeObject(new {
                    kind = "NATIVE_CAMPER_DYE_CONSTRUCTION_CHECK", status = "PASS_CREATION_CLONE_REMOVAL",
                    productionHookOwners = hookOwners,
                    initialDye = dye.ItemClass == null ? "unresolved type=" + dye.type : dye.ItemClass.GetItemName(), clonePreserved = true, removalPreserved = true,
                    replacementPreserved = true, visualAndSaveReloadAcceptance = false
                    , nativeSerializationPreserved = true, legacyEmptySlotMigrated = true
                }, Formatting.Indented));
                SdtdConsole.Instance.Output("[CamperDyeCheck] PASS " + file);
            }
            catch (Exception error) { SdtdConsole.Instance.Output("*** ERROR [CamperDyeCheck] stage=" + stage + " " + error); }
        }

        private static ItemValue RoundTrip(ItemValue source)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = MemoryPools.poolBinaryWriter.AllocSync(true))
                {
                    writer.SetBaseStream(stream);
                    source.Write(writer);
                    writer.Flush();
                }
                stream.Position = 0;
                var loaded = new ItemValue();
                using (var reader = MemoryPools.poolBinaryReader.AllocSync(true))
                {
                    reader.SetBaseStream(stream);
                    loaded.Read(reader);
                }
                return loaded;
            }
        }
    }
}
