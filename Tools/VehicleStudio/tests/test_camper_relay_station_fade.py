from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[3]
VISUAL = (ROOT / 'Scripts/Vehicles/Camper/Energy/RebirthCamperRelayVisuals.cs').read_text()
APPEARANCE = (ROOT / 'Scripts/Vehicles/Camper/RebirthCamperCampStationAppearance.cs').read_text()


class RelayStationFade(unittest.TestCase):
    def test_existing_station_shader_and_lifetime_owner(self):
        self.assertIn('Shader.Find("REBIRTH/CamperCampFade")', VISUAL)
        self.assertIn('material.HasProperty("_CampVisibility")&&material.shader!=null', VISUAL)
        self.assertLess(VISUAL.index('fade=material.shader'), VISUAL.index('Shader.Find('))
        self.assertIn('new Material(source)', VISUAL)
        self.assertIn('private void OnDestroy()', VISUAL)
        self.assertIn('Object.Destroy(material)', VISUAL)

    def test_same_fade_group_and_opacity_path(self):
        self.assertIn('view.Power,view.Stairs,view.Relays', APPEARANCE)
        self.assertIn('view.Block.SetFloat("_CampVisibility", view.Opacity)', APPEARANCE)
        self.assertNotIn('Time.deltaTime', VISUAL)

    def test_late_native_asset_refreshes_existing_binding(self):
        self.assertIn('if(view.Relays!=relays)', APPEARANCE)
        self.assertIn('view.RelayTargets=targets.ToArray();BindFadeGroups(view)', APPEARANCE)
        self.assertIn('if(!fade){Wait(vehicle,binding,"station fade shader not loaded");return;}', VISUAL)

    def test_deployed_only_trigger_and_no_premature_relay_reveal(self):
        self.assertIn('target.enabled=show&&target.gameObject.activeInHierarchy', APPEARANCE)
        self.assertNotIn('group.SetActive(true);binding.Group', VISUAL)
        self.assertIn('material.SetFloat("_CampVisibility",0)', VISUAL)

    def test_mount_geometry_uses_live_model_pose(self):
        self.assertIn('var frame=vehicle.ModelTransform;', VISUAL)
        self.assertNotIn('root.InverseTransformPoint', VISUAL)
        self.assertNotIn('root.TransformPoint', VISUAL)
        self.assertIn('frame.TransformPoint(anchors[index])', VISUAL)
        self.assertIn('frame.TransformDirection(normals[index])', VISUAL)
        self.assertIn('Vector3.ProjectOnPlane(frame.forward,normal)', VISUAL)


if __name__ == '__main__':
    unittest.main()
