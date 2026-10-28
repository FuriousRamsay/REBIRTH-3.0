import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from run_camper_qualification import repairable_surface_holes, repair_area_empty


class PadHoleEvidence(unittest.TestCase):
    def test_new_entities_block_repair_even_when_dead_and_invalid_scans_refuse(self):
        track = self.evidence()
        state = {'player': {'position': [-330, 40, 1750]}}
        self.assertTrue(repair_area_empty(track, state, {'count': 0, 'entities': []}))
        self.assertFalse(repair_area_empty(track, state, {'count': 1, 'entities': [
            {'position': [-309, 40, 1780], 'dead': True}]}))
        with self.assertRaises(RuntimeError):
            repair_area_empty(track, state, {'count': 2, 'entities': []})

    def evidence(self):
        return dict(xMin=-305, zMin=1778, width=12, length=20, floorBlockY=41,
                    missingBlocks=[dict(position=dict(x=-305, y=41, z=1786), loaded=True,
                                       expectedType=7051, actualType=0, actualBlock='air')])

    def test_real_captured_air_hole_is_eligible_for_native_revalidation(self):
        self.assertEqual(repairable_surface_holes(self.evidence()), ((-305, 41, 1786),))

    def test_unloaded_unknown_nonair_or_outside_cells_are_never_repair_candidates(self):
        for key, value in [('loaded', False), ('actualType', 7051), ('actualBlock', 'stone'),
                           ('expectedType', True), ('position', dict(x=-306, y=41, z=1786)),
                           ('position', dict(x=-305, y=42, z=1786))]:
            data = self.evidence()
            data['missingBlocks'][0][key] = value
            with self.subTest(key=key, value=value):
                self.assertIsNone(repairable_surface_holes(data))

    def test_unknown_empty_duplicate_and_large_failure_sets_do_not_repair(self):
        for rows in [None, [], self.evidence()['missingBlocks'] * 2,
                     self.evidence()['missingBlocks'] * 33]:
            data = self.evidence()
            data['missingBlocks'] = rows
            self.assertIsNone(repairable_surface_holes(data))


if __name__ == '__main__':
    unittest.main()
