import sys
import tempfile
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from launch_camper_qualification import validate_launch, require_owned, observe_session


class LaunchSupervision(unittest.TestCase):
    def test_stale_receipt_waits_until_new_owned_session_then_replacement_fails(self):
        stale = {'pid': 20, 'port': 8765, 'token': 'old-token'}
        owned = {'pid': 21, 'port': 8765, 'token': 'owned-token'}
        self.assertIsNone(observe_session(stale, None, 21))
        expected = observe_session(owned, None, 21)
        self.assertEqual(observe_session(dict(owned), expected, 21), owned)
        for replacement in (stale, dict(owned, token='replacement'), dict(owned, port=9999)):
            with self.assertRaises(RuntimeError):
                observe_session(replacement, expected, 21)
        with self.assertRaises(RuntimeError):
            observe_session({'pid': 21}, None, 21)
    def test_existing_disposable_save_only(self):
        with tempfile.TemporaryDirectory() as temporary:
            saves = Path(temporary)
            config = {'world': 'World', 'save': 'CodexTest'}
            with self.assertRaises(RuntimeError):
                validate_launch(config, saves)
            (saves / 'World/CodexTest').mkdir(parents=True)
            validate_launch(config, saves)
            for name in ('Purge', 'RealSave', '../CodexTest'):
                with self.assertRaises(RuntimeError):
                    validate_launch(dict(config, save=name), saves)

    def test_only_exact_owned_pid_and_executable_may_continue(self):
        executable = Path('game/7DaysToDie.exe').resolve()
        row = {'Id': 21, 'Path': str(executable)}
        require_owned([row], 21, executable)
        for rows in ([], [row, row], [dict(row, Id=22)], [dict(row, Path='other/7DaysToDie.exe')]):
            with self.assertRaises(RuntimeError):
                require_owned(rows, 21, executable)


if __name__ == '__main__':
    unittest.main()
