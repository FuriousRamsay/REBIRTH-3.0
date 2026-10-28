"""Require native entity-associated playing sources and observed sample advancement."""
import argparse
import json
import math
from pathlib import Path
ENGINE_GROUPS = {'suv_idle_lp', 'suv_max_speed_lp', 'suv_accel1', 'suv_accel2', 'suv_accel3', 'suv_decel1', 'suv_decel2'}

def validate_audio(trace, entity_id):
    if trace.get('kind') != 'REBIRTH_NATIVE_VEHICLE_AUDIO_TRACE' or trace.get('status') != 'CAPTURED_NOT_HUMAN_LISTENING' or trace.get('error'):
        raise ValueError('Successful native audio trace required.')
    witnessed = {}; progress = {}; observed = set(); horn_group = None
    for sample in trace.get('samples', []):
        if sample.get('entityId') != entity_id:
            raise ValueError('Audio trace vehicle identity changed.')
        if horn_group is None: horn_group = sample.get('hornGroup')
        if not horn_group or sample.get('hornGroup') != horn_group:
            raise ValueError('Stable configured native horn group required.')
        stamp = sample.get('capturedTime')
        if type(stamp) not in (int, float) or not math.isfinite(stamp):
            raise ValueError('Finite native audio sample time required.')
        if sample.get('listenerPaused') or not math.isfinite(sample.get('listenerVolume', float('nan'))) or sample['listenerVolume'] <= 0:
            continue
        for source in sample.get('sources', []):
            group = source.get('group')
            kind = 'horn' if group == horn_group else 'engine' if group in ENGINE_GROUPS else None
            if kind is None: continue
            if source.get('entityId') != entity_id or not source.get('route', '').startswith(('loopingOnEntity.', 'sequenceOnEntity.', 'playingOnEntity.')):
                raise ValueError('Native vehicle-ID audio association required.')
            gain, pitch = source.get('volume'), source.get('pitch')
            if any(type(v) not in (int, float) or not math.isfinite(v) for v in (gain, pitch)):
                raise ValueError('Finite source gain and pitch required.')
            if not (source.get('isPlaying') and source.get('active') and not source.get('mute') and gain > 0 and pitch > 0
                    and source.get('clip') and source.get('clipInstanceId') and source.get('clipSamples', 0) > 0):
                continue
            cursor = source.get('timeSamples')
            if type(cursor) is not int or cursor < 0 or cursor >= source['clipSamples']:
                raise ValueError('Actual valid clip sample cursor required.')
            key = (kind, source.get('sourceId'), source['clipInstanceId'])
            prior = progress.get(key)
            if prior and stamp > prior[0] and cursor != prior[1]:
                observed.add(kind)
                witnessed[kind] = {'group': group, 'clip': source['clip'], 'sourceId': source['sourceId'], 'gain': gain, 'pitch': pitch}
            progress[key] = stamp, cursor
    if observed != {'engine', 'horn'}:
        raise ValueError('Playing native engine and horn clips with sample advancement required; input requests do not count.')
    return {'status': 'PASS_NATIVE_SOURCE_PLAYBACK_NOT_HUMAN_LISTENING', 'entityId': entity_id, 'witnessed': witnessed}

if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('trace'); parser.add_argument('--entity', type=int, required=True)
    args = parser.parse_args(); print(json.dumps(validate_audio(json.loads(Path(args.trace).read_text()), args.entity), indent=2))
