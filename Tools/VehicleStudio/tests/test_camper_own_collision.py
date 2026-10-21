"""Static lifecycle contract; physical reproduction is the independent Unity audit."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[3]
SOURCE = (ROOT / 'Scripts/Vehicles/Camper/RebirthCamperOwnCollision.cs').read_text()
APPEARANCE = (ROOT / 'Scripts/Vehicles/Camper/RebirthCamperCampStationAppearance.cs').read_text()
TURRET = (ROOT / 'Scripts/Vehicles/Camper/Turrets/RebirthCamperTurretRuntime.cs').read_text()
INSTALLER = (ROOT / 'Scripts/Vehicles/Camper/RebirthCamperModulePatchInstaller.cs').read_text() if (ROOT / 'Scripts/Vehicles/Camper/RebirthCamperModulePatchInstaller.cs').exists() else next(ROOT.glob('Scripts/**/RebirthCamperModulePatchInstaller.cs')).read_text()


class OwnCollisionContract(unittest.TestCase):
    def test_full_owned_hierarchy_including_proxy(self):
        self.assertIn('state.Root.GetComponentsInChildren<Collider>(true,state.All)', SOURCE)
        self.assertNotIn('BodyModule', SOURCE)

    def test_actor_and_trigger_exclusion(self):
        self.assertIn('collider.isTrigger', SOURCE)
        self.assertIn('!ReferenceEquals(actor,vehicle)', SOURCE)

    def test_native_compound_owner(self):
        self.assertIn('collider.attachedRigidbody==state.Body', SOURCE)
        self.assertNotIn('IgnoreLayerCollision(', SOURCE.replace('GetIgnoreLayerCollision(', ''))

    def test_driver_and_modifier_refresh(self):
        self.assertIn('nameof(EntityVehicle.SetVehicleDriven)', SOURCE)
        self.assertIn('nameof(Vehicle.SetItemValue)', SOURCE)
        self.assertGreaterEqual(SOURCE.count('RebirthCamperOwnCollision.Invalidate('), 2)
        self.assertIn('typeof(RebirthCamperOwnCollisionDriven)', INSTALLER)
        self.assertIn('typeof(RebirthCamperOwnCollisionParts)', INSTALLER)

    def test_bounded_cached_inventory(self):
        self.assertIn('ConditionalWeakTable<EntityVehicle,State>', SOURCE)
        self.assertIn('state.NextBind=Time.realtimeSinceStartup+5f', SOURCE)
        self.assertEqual(SOURCE.count('GetComponentsInChildren<Collider>'), 1)

    def test_model_and_physics_replacement(self):
        self.assertIn('state.Model!=vehicle.ModelTransform', SOURCE)
        self.assertIn('state.Body!=vehicle.vehicleRB', SOURCE)

    def test_pair_reassertion_only_own_cached_shapes(self):
        self.assertIn('foreach(var detached in state.Detached)', SOURCE)
        self.assertIn('foreach(var compound in state.Compound)', SOURCE)
        self.assertIn('Physics.GetIgnoreCollision(detached,compound)', SOURCE)
        self.assertIn('Physics.IgnoreCollision(detached,compound,true)', SOURCE)

    def test_runtime_part_binding_and_appearance(self):
        self.assertIn('RebirthCamperOwnCollision.Invalidate(vehicle);', TURRET)
        self.assertIn('RebirthCamperOwnCollision.Apply(vehicle);', APPEARANCE)

    def test_selection_proxy_does_not_become_player_wall(self):
        self.assertIn('var proxy=state.Model.GetComponent<BoxCollider>()', SOURCE)
        self.assertIn('player?.vp_FPController?.CharacterController', SOURCE)
        self.assertIn('Physics.IgnoreCollision(proxy,controller,true)', SOURCE)
        self.assertNotIn('proxy.enabled=false', SOURCE)
        self.assertNotIn('proxy.isTrigger=', SOURCE)


if __name__ == '__main__':
    unittest.main()
