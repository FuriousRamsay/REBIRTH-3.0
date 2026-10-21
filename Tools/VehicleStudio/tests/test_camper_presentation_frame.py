"""Origin-shifted, rotated live-pose regression for diagnostic coordinates."""
import math
from pathlib import Path
import unittest


class PresentationFrame(unittest.TestCase):
    def test_world_points_follow_live_pose_not_factory_container(self):
        origin = (0, 32, 976)
        vehicle_world = (14.576, 45.311, 992.359)
        for angle in (0, 90, 173):
            c, s = math.cos(math.radians(angle)), math.sin(math.radians(angle))
            local = (0, 1.1, 1.6)
            shifted = tuple(a-b for a, b in zip(vehicle_world, origin))
            point = (shifted[0]+c*local[0]+s*local[2], shifted[1]+local[1],
                     shifted[2]-s*local[0]+c*local[2])
            world = tuple(a+b for a, b in zip(point, origin))
            self.assertAlmostEqual(world[1], 46.411)
            self.assertAlmostEqual(math.hypot(world[0]-vehicle_world[0], world[2]-vehicle_world[2]), 1.6)
            delta = tuple(a-b for a, b in zip(point, shifted))
            recovered = (c*delta[0]-s*delta[2], delta[1], s*delta[0]+c*delta[2])
            for actual, expected in zip(recovered, local):
                self.assertAlmostEqual(actual, expected)
            self.assertGreater(math.dist(world, tuple(a+b for a, b in zip(local, origin))), 10)

    def test_production_observer_uses_live_frame_for_all_local_coordinates(self):
        source = (Path(__file__).resolve().parents[3] / 'Scripts/Vehicles/Camper/RebirthCamperTrace.cs').read_text()
        section = source.split('private static JObject Presentation', 1)[1]
        self.assertIn('var frame=model??vehicle.PhysicsTransform;', section)
        self.assertNotIn('root.InverseTransformPoint', section)
        self.assertNotIn('root.TransformPoint', section)
        self.assertIn('ground.transform.IsChildOf(root)', section)
        self.assertEqual(section.count('frame.TransformPoint('), 3)
        self.assertEqual(section.count('frame.InverseTransformPoint('), 3)


if __name__ == '__main__':
    unittest.main()
