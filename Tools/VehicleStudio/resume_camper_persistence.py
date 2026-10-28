"""Read/assert a staged checkpoint after an independently authorized same-save restart.

Does not authorize, launch, quit or restart anything. Requires an explicit matching
new session receipt and known nearby normally placed camper ID supplied by controller.
"""
import argparse
import json
from pathlib import Path
import shutil
import threading
from run_camper_qualification import Suite, wait_quiescent, prepare_safe_arrival
from camper_ui_qualification import CamperUI, persistence_image


def compare_checkpoint(checkpoint, snapshot):
    if checkpoint.get('kind') != 'CAMPER_NATIVE_UI_PERSISTENCE_CHECKPOINT' or checkpoint.get('save') != 'CodexTest':
        raise RuntimeError('Expected same-save camper checkpoint required.')
    expected = checkpoint.get('expected')
    if expected != persistence_image(snapshot):
        changed = [key for key in persistence_image(snapshot) if not isinstance(expected, dict) or expected.get(key) != snapshot[key]]
        raise RuntimeError('Actual restored camper state differs: ' + ', '.join(changed))


def run(pid, receipt, entity, checkpoint_path):
    suite = Suite(pid, receipt)
    checkpoint = json.loads(Path(checkpoint_path).read_text(encoding='utf-8-sig'))
    if checkpoint.get('sourcePid') == pid:
        raise RuntimeError('A new independently authorized restart session is required for save/load evidence.')
    monitor = threading.Thread(target=suite.monitor, daemon=True)
    monitor.start()
    try:
        prepare_safe_arrival(suite.act, suite.raw)
        wait_quiescent(lambda: suite.raw('/guard'), lambda value: suite.report['phases'].append({'quiescence': value}))
        ui = CamperUI(suite, entity)
        ui.approach()
        snapshot = ui.snapshot('after_independently_authorized_reload')
        compare_checkpoint(checkpoint, snapshot)
        ui.screenshot('restored_vehicle')
        suite.identity()
        if suite.failure:
            raise RuntimeError(suite.failure)
        suite.report.update(status='ASSERTED_SAME_SAVE_NATIVE_STATE_NOT_VISUAL_OR_MULTIPLAYER_ACCEPTANCE',
            checkpoint=str(Path(checkpoint_path).resolve()), sourcePid=checkpoint['sourcePid'], restoredEntity=entity)
    except Exception as error:
        suite.report.update(status='FAIL', error=str(error))
    finally:
        suite.stop.set(); monitor.join(timeout=12)
        if suite.log.exists():
            shutil.copyfile(suite.log, suite.folder / 'Player.log')
        path = suite.folder / 'result.json'
        path.write_text(json.dumps(suite.report, indent=2), encoding='utf-8')
        print(path, flush=True)
    return int(suite.report['status'] == 'FAIL')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--owned-pid', required=True, type=int)
    parser.add_argument('--authorized-session-receipt', required=True)
    parser.add_argument('--entity', required=True, type=int)
    parser.add_argument('--checkpoint', required=True)
    args = parser.parse_args()
    raise SystemExit(run(args.owned_pid, args.authorized_session_receipt, args.entity, args.checkpoint))
