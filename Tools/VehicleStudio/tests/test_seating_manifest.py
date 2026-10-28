import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from validate_camper_seats import configured_slots, require_production_bootstrap, require_safe_seating


def manifest(count):
    return {'attachMaxCount': count, 'seats': [{'slot': i, 'configuredPose': 40 if i == 0 else 41} for i in range(count)]}


class SeatingManifest(unittest.TestCase):
    def test_parked_airborne_and_remote_vehicles_are_not_safe_seats(self):
        good = {'speed': 0, 'position': {'x': 0, 'y': 40, 'z': 0}, 'wheels': [{'grounded': True} for _ in range(4)]}
        require_safe_seating(good, [2, 40, 0])
        for bad in (dict(good, speed=.5), dict(good, speed=float('nan')),
                    dict(good, wheels=[{'grounded': False} for _ in range(4)]),
                    dict(good, position={'x': 0, 'y': 145, 'z': 0})):
            with self.assertRaises(RuntimeError):
                require_safe_seating(bad, [2, 40, 0])
    def test_actual_production_bootstrap_is_required_before_seating(self):
        evidence = {'state': 'Complete', 'lastFailure': '', 'moduleInstalled': True}
        self.assertEqual(require_production_bootstrap({'productionBootstrap': evidence}), evidence)
        for data in ({}, {'productionBootstrap': {'state': 'Partial', 'lastFailure': '', 'moduleInstalled': True}},
                     {'productionBootstrap': {'state': 'Complete', 'lastFailure': 'native patch refused', 'moduleInstalled': True}},
                     {'productionBootstrap': {'state': 'Complete', 'lastFailure': '', 'moduleInstalled': False}},
                     {'productionBootstrap': {'state': 'Complete', 'moduleInstalled': True}}):
            with self.assertRaises(RuntimeError):
                require_production_bootstrap(data)
    def test_all_authored_capacity_is_captured(self):
        for count in (2, 4, 6, 8):
            self.assertEqual(configured_slots(manifest(count)), list(range(count)))

    def test_missing_duplicate_or_incorrect_pose_is_refused(self):
        invalid = [manifest(1), {'attachMaxCount': True, 'seats': []}]
        row = manifest(4); row['seats'].pop(); invalid.append(row)
        row = manifest(4); row['seats'][3]['slot'] = 2; invalid.append(row)
        row = manifest(4); row['seats'][2]['configuredPose'] = 40; invalid.append(row)
        for data in invalid:
            with self.assertRaises(ValueError):
                configured_slots(data)


if __name__ == '__main__':
    unittest.main()
