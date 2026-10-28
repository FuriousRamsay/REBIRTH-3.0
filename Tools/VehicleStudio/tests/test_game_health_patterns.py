import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from watch_game_health import FATAL, WARNING, heartbeat_stall_ms


class GameHealthPatterns(unittest.TestCase):
    def test_runtime_errors_without_bridge_error_prefix(self):
        for line in ('NullReferenceException: object reference', 'System.IO.IOException: save failed',
                     'ArgumentOutOfRangeException: slot', 'ERROR: Unity rendering failed',
                     '***ERROR diagnostic refused', 'd3d11: failed to create texture',
                     'd3d12: out of memory', 'DXGI_ERROR_DEVICE_REMOVED', 'Fatal error: crash',
                     'Could not allocate memory', 'Assertion failed: native object',
                     '2026-10-11 12:00:00 ERR XML failed', '2026-10-11 EXC native queue'):
            self.assertIsNotNone(FATAL.search(line), line)

    def test_warnings_are_recorded_without_becoming_errors(self):
        for line in ('WRN shader fallback', 'Warning: material unsupported', 'Texture missing!'):
            self.assertIsNotNone(WARNING.search(line), line)
            self.assertIsNone(FATAL.search(line), line)
        self.assertIsNone(FATAL.search('INF bridge errors count=0'))

    def test_invalid_heartbeat_is_not_treated_as_responsive(self):
        for value in (None, True, '0', -1, float('nan'), float('inf')):
            with self.assertRaises(ValueError):
                heartbeat_stall_ms({'mainThreadStallMs': value})
        self.assertEqual(heartbeat_stall_ms({'mainThreadStallMs': 0}), 0)
        self.assertEqual(heartbeat_stall_ms({'mainThreadStallMs': 15000}), 15000)


if __name__ == '__main__':
    unittest.main()
