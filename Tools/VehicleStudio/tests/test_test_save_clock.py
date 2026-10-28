import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from run_camper_qualification import daylight_command


class TestSaveClock(unittest.TestCase):
    def test_earned_day_is_preserved_or_advanced_without_rewind(self):
        world = dict(gameName='CodexTest', day=64, hour=13, isDaytime=True)
        self.assertIsNone(daylight_command(world))
        self.assertEqual(daylight_command(dict(world, hour=3, isDaytime=False)), 'settime 64 9 0')
        self.assertEqual(daylight_command(dict(world, hour=23, isDaytime=False)), 'settime 65 9 0')
        self.assertEqual(world['day'], 64)

    def test_unknown_clock_or_non_test_save_cannot_be_changed(self):
        world = dict(gameName='CodexTest', day=64, hour=13, isDaytime=True)
        for key, value in [('gameName', 'RealSave'), ('day', True), ('day', 0),
                           ('hour', 24), ('hour', False), ('isDaytime', 1)]:
            with self.subTest(key=key, value=value), self.assertRaises(RuntimeError):
                daylight_command(dict(world, **{key: value}))
        for key in world:
            incomplete = dict(world)
            del incomplete[key]
            with self.assertRaises(RuntimeError):
                daylight_command(incomplete)


if __name__ == '__main__':
    unittest.main()
