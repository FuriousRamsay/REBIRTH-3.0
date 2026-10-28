"""Native service/cosmetic route scope; parent integration required."""
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[3]
SOURCE=(ROOT/'Scripts/Vehicles/Camper/RebirthCamperRestorationBinding.cs').read_text()
RESOLVER=(ROOT/'Scripts/Vehicles/Restoration/RebirthVehicleEntityResolver.cs').read_text()


class NativeEditorBinding(unittest.TestCase):
    def test_native_service_owns_admission_and_open(self):
        self.assertIn('vehicle.AllowActivationCommand("service",player)',SOURCE)
        self.assertIn('vehicle.OnEntityActivated(new EntityActivationCommand("service"',SOURCE)
        self.assertNotIn('StartInteraction(',SOURCE)
        self.assertNotIn('OpenEntity(',SOURCE)
        self.assertNotIn('ReadInto(',SOURCE)

    def test_explicit_actor_world_time_and_once_scope(self):
        self.assertIn('ReferenceEquals(player,request.Player)',SOURCE)
        self.assertIn('Time.realtimeSinceStartup<=request.Until',SOURCE)
        self.assertIn('pending.Remove(vehicle)',SOURCE)
        self.assertIn('ThreadManager.IsMainThread()',SOURCE)
        self.assertIn('!RebirthCamperModuleInteraction.Current(vehicle,player)',SOURCE)

    def test_no_body_requirement_or_fabricated_part_inventory(self):
        self.assertNotIn('HasBody(',SOURCE)
        self.assertNotIn('new ItemValue',SOURCE)
        self.assertNotIn('new RebirthVehicleAssembly',SOURCE)
        self.assertIn('rebirthCamperNativeVehicleEditor',SOURCE)

    def test_resolution_preserves_loaded_stacks_and_authority(self):
        self.assertLess(RESOLVER.index('TryGetEntity('),RESOLVER.index('TryReadAssemblyFromVehicleItem('))
        self.assertLess(RESOLVER.index('TryReadAssemblyFromVehicleItem('),RESOLVER.index('new RebirthVehicleAssembly'))
        self.assertIn('entity.world.IsRemote()',RESOLVER)
        self.assertIn('ReferenceEquals(entity.world.GetEntity(entity.entityId),entity)',RESOLVER)
        self.assertNotIn('new RebirthInstalledVehiclePart',RESOLVER)
        self.assertNotIn('.Parts.Add',RESOLVER)
        self.assertLess(RESOLVER.index('WriteAssemblyToVehicleItem(entity,created)'),RESOLVER.index('RegisterEntity(entity,created)'))
        self.assertIn('!string.IsNullOrEmpty(payload))return true',RESOLVER)


if __name__=='__main__':unittest.main()
