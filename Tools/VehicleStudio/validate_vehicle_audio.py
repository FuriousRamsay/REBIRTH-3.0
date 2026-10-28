"""Native playback evidence, explicitly separate from human listening."""
import math


def validate(data, entity, vehicle_name):
    if data.get('kind') != 'REBIRTH_NATIVE_VEHICLE_AUDIO_TRACE' or data.get('status') != 'CAPTURED_NOT_HUMAN_LISTENING' or data.get('error'):
        raise ValueError('Complete native audio trace required.')
    progress = {}
    for sample in data.get('samples', []):
        if sample.get('entityId') != entity or sample.get('vehicleName') != vehicle_name or sample.get('hornGroup') != 'suv_horn':
            raise ValueError('Audio trace vehicle/native horn identity changed.')
        if sample.get('listenerPaused') is not False or not _positive(sample.get('listenerVolume')):
            continue
        for source in sample.get('sources', []):
            if source.get('entityId') != entity:
                raise ValueError('Unassociated audio source cannot prove vehicle playback.')
            if source.get('isPlaying') is not True or source.get('active') is not True or source.get('mute') is not False:
                continue
            if not all(_positive(source.get(key)) for key in ('volume', 'pitch', 'clipLength', 'clipSamples')):
                continue
            if not source.get('clip') or type(source.get('timeSamples')) is not int or source['timeSamples'] < 0:
                continue
            distance, maximum = source.get('listenerDistance'), source.get('maxDistance')
            if not _finite(distance) or not _positive(maximum) or distance < 0 or distance > maximum:
                continue
            key = (source.get('group'), source.get('sourceId'), source.get('clipInstanceId'), source['clip'])
            progress.setdefault(key, set()).add(source['timeSamples'])
    advancing = [key for key, positions in progress.items() if len(positions) >= 2]
    horns = [key for key in advancing if key[0] == 'suv_horn']
    engines = [key for key in advancing if isinstance(key[0], str) and key[0].startswith('Vehicles/Suv/')]
    if not horns or not engines:
        raise ValueError('Actual advancing native engine and horn playback required; dispatched input is insufficient.')
    return dict(engineGroups=sorted({key[0] for key in engines}), hornClips=sorted({key[3] for key in horns}),
                nativePlaybackVerified=True, humanListeningVerified=False)


def _finite(value):
    return type(value) in (int, float) and math.isfinite(value)


def _positive(value):
    return _finite(value) and value > 0
