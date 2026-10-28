"""Observe one explicitly owned game PID, its real log and bridge heartbeat. Never kills or launches processes."""
import argparse
import ctypes
from ctypes import wintypes
import json
import math
import os
from pathlib import Path
import re
import shutil
import subprocess
import time
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parent
MOD = ROOT.parents[1]
FATAL = re.compile(
    r'\bERR\b|\bEXC\b|\b[A-Za-z][A-Za-z0-9_.]*Exception\s*:|'
    r'^\s*(?:\*\*\*\s*)?ERROR(?:\s|:)|DXGI_ERROR_|Fatal error|'
    r'\bd3d(?:11|12)\s*:.*(?:failed|error|out of memory)|'
    r'Could not allocate memory|Failed to allocate.*memory|'
    r'Crash!!!|Access violation|\bSIGSEGV\b|Assertion failed', re.I)
WARNING = re.compile(r'\bWRN\b|\bwarning\b|missing!', re.I)


class _ProcessEntry32(ctypes.Structure):
    _fields_ = [('dwSize', wintypes.DWORD), ('cntUsage', wintypes.DWORD),
                ('th32ProcessID', wintypes.DWORD), ('th32DefaultHeapID', ctypes.c_size_t),
                ('th32ModuleID', wintypes.DWORD), ('cntThreads', wintypes.DWORD),
                ('th32ParentProcessID', wintypes.DWORD), ('pcPriClassBase', wintypes.LONG),
                ('dwFlags', wintypes.DWORD), ('szExeFile', wintypes.WCHAR * 260)]


def running_game_pids():
    """Read-only native process snapshot; no subprocess cost on each input gate."""
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    kernel.CreateToolhelp32Snapshot.argtypes = [wintypes.DWORD, wintypes.DWORD]
    kernel.CreateToolhelp32Snapshot.restype = wintypes.HANDLE
    kernel.Process32FirstW.argtypes = [wintypes.HANDLE, ctypes.POINTER(_ProcessEntry32)]
    kernel.Process32NextW.argtypes = kernel.Process32FirstW.argtypes
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    snapshot = kernel.CreateToolhelp32Snapshot(2, 0)
    if snapshot == ctypes.c_void_p(-1).value:
        raise ctypes.WinError(ctypes.get_last_error())
    try:
        entry = _ProcessEntry32()
        entry.dwSize = ctypes.sizeof(entry)
        if not kernel.Process32FirstW(snapshot, ctypes.byref(entry)):
            raise ctypes.WinError(ctypes.get_last_error())
        games = set()
        while True:
            if entry.szExeFile.lower().startswith('7daystodie'):
                games.add(entry.th32ProcessID)
            if not kernel.Process32NextW(snapshot, ctypes.byref(entry)):
                if ctypes.get_last_error() != 18:  # ERROR_NO_MORE_FILES
                    raise ctypes.WinError(ctypes.get_last_error())
                break
        return games
    finally:
        kernel.CloseHandle(snapshot)


def heartbeat_stall_ms(ping):
    value = ping.get('mainThreadStallMs')
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value) or value < 0:
        raise ValueError('Missing or invalid native main-thread heartbeat age')
    return value


def run(pid, duration):
    folder = ROOT / 'reports' / 'game_health' / uuid.uuid4().hex
    folder.mkdir(parents=True)
    log = Path(os.environ['USERPROFILE']) / 'AppData/LocalLow/The Fun Pimps/7 Days To Die/Player.log'
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel.OpenProcess.restype = wintypes.HANDLE
    kernel.GetExitCodeProcess.argtypes = [wintypes.HANDLE, ctypes.POINTER(wintypes.DWORD)]
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    handle = kernel.OpenProcess(0x1000, False, pid)
    if not handle:
        raise OSError('Cannot open the specified owned game process for read-only observation')
    started = time.monotonic()
    seen = 0
    stalls = 0
    heartbeat_misses = 0
    connected = False
    last_heartbeat = started
    last_state = 'starting'
    last_print = -10
    summary = {'kind': 'REBIRTH_CAMPER_GAME_HEALTH', 'pid': pid, 'status': 'RUNNING', 'gameplayAcceptance': False}
    print(f'[1 MONITOR] Owned PID={pid}; report={folder}; checking log and heartbeat every 3 seconds', flush=True)
    try:
        with (folder / 'observations.jsonl').open('w', encoding='utf-8') as output:
            while time.monotonic() - started < duration:
                exit_code = wintypes.DWORD()
                if not kernel.GetExitCodeProcess(handle, ctypes.byref(exit_code)):
                    raise OSError('Owned process observation failed')
                if exit_code.value != 259:
                    summary.update(status='PROCESS_EXITED', processExitCode=exit_code.value)
                    break
                lines = log.read_text(encoding='utf-8', errors='replace').splitlines() if log.exists() else []
                if len(lines) < seen:
                    seen = 0
                fresh = lines[seen:]
                seen = len(lines)
                observation = {'elapsedSeconds': round(time.monotonic() - started, 1), 'logLines': seen,
                               'newWarnings': [line for line in fresh if WARNING.search(line)],
                               'newErrors': [line for line in fresh if FATAL.search(line)]}
                if observation['newErrors']:
                    summary.update(status='FAIL_LOG', errors=observation['newErrors'])
                try:
                    session = json.loads((MOD / 'Tools/GameBridge/out/session.json').read_text(encoding='utf-8-sig'))
                    req = urllib.request.Request(f'http://127.0.0.1:{session["port"]}/ping', headers={'X-Bridge-Token': session['token']})
                    with urllib.request.urlopen(req, timeout=2) as response:
                        ping = json.load(response)
                    observation['heartbeat'] = ping
                    if ping.get('pid') != pid:
                        if connected:
                            summary.update(status='FAIL_PID_MISMATCH')
                        raise ValueError('Bridge heartbeat does not belong to the specified owned PID')
                    if not ping.get('ok'):
                        raise ValueError('Bridge heartbeat reports failure')
                    try:
                        stall_age = heartbeat_stall_ms(ping)
                    except ValueError:
                        summary.update(status='FAIL_INVALID_MAIN_THREAD_HEARTBEAT')
                        raise
                    connected = True
                    last_heartbeat = time.monotonic()
                    last_state = ping.get('state', 'unknown')
                    heartbeat_misses = 0
                    limit = 10000 if ping.get('state') == 'ingame' else 30000
                    stalls = stalls + 1 if stall_age > limit else 0
                    if stalls >= 3:
                        summary.update(status='FAIL_MAIN_THREAD_STALL', heartbeat=ping)
                except Exception as exc:
                    observation['heartbeatError'] = str(exc)
                    heartbeat_misses += 1
                    # Loading XUi can block request dispatch temporarily. Keep the
                    # missing-response evidence, but use elapsed time, not sample count alone.
                    missing_seconds = time.monotonic() - last_heartbeat
                    observation['heartbeatMissingSeconds'] = round(missing_seconds, 1)
                    missing_limit = 10 if last_state == 'ingame' else 60
                    if connected and heartbeat_misses >= 3 and missing_seconds >= missing_limit:
                        summary.update(status='FAIL_HEARTBEAT_UNAVAILABLE', error=str(exc))
                if observation['elapsedSeconds'] - last_print >= 10 or summary['status'] != 'RUNNING':
                    try:
                        gpu = subprocess.run(['nvidia-smi', '--query-gpu=name,memory.total,memory.used,temperature.gpu,utilization.gpu', '--format=csv,noheader'], capture_output=True, text=True, timeout=2)
                        observation['gpuSample'] = gpu.stdout.strip() if gpu.returncode == 0 else gpu.stderr.strip()
                    except Exception as exc:
                        observation['gpuSampleError'] = str(exc)
                    print(f'[HEARTBEAT] elapsed={observation["elapsedSeconds"]}s; state={observation.get("heartbeat", {}).get("state", "unavailable")}; stallMs={observation.get("heartbeat", {}).get("mainThreadStallMs")}; newErrors={len(observation["newErrors"])}', flush=True)
                    last_print = observation['elapsedSeconds']
                output.write(json.dumps(observation) + '\n')
                output.flush()
                if summary['status'] != 'RUNNING':
                    break
                time.sleep(3)
            else:
                summary['status'] = 'OBSERVATION_DURATION_ENDED'
    except KeyboardInterrupt:
        summary['status'] = 'MONITOR_CANCELLED_GAME_UNTOUCHED'
    except Exception as exc:
        summary.update(status='FAIL_MONITOR', error=str(exc))
    finally:
        kernel.CloseHandle(handle)
        if log.exists():
            shutil.copyfile(log, folder / 'Player.log')
        summary['elapsedSeconds'] = round(time.monotonic() - started, 1)
        (folder / 'result.json').write_text(json.dumps(summary, indent=2) + '\n')
        print(f'[2 RESULT] {summary["status"]}; report={folder / "result.json"}; process cleanup remains with the controller', flush=True)
    return 1 if summary['status'].startswith('FAIL') else 0


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--pid', type=int, required=True)
    parser.add_argument('--duration', type=int, default=1800)
    args = parser.parse_args()
    raise SystemExit(run(args.pid, args.duration))
