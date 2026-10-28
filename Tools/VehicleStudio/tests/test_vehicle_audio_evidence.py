from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from validate_vehicle_audio import validate


def trace():
    samples = []
    for counter in (100, 200):
        sources = []
        for index, group in enumerate(('suv_horn', 'Vehicles/Suv/suv_idle_lp')):
            sources.append(dict(entityId=21, group=group, sourceId=index + 1, clipInstanceId=index + 10,
                clip=group, isPlaying=True, active=True, mute=False, volume=.7, pitch=1,
                clipLength=1, clipSamples=44100, timeSamples=counter, listenerDistance=2, maxDistance=30))
        samples.append(dict(entityId=21, vehicleName='rebirthCamper80s', hornGroup='suv_horn',
            listenerPaused=False, listenerVolume=1, sources=sources))
    return dict(kind='REBIRTH_NATIVE_VEHICLE_AUDIO_TRACE', status='CAPTURED_NOT_HUMAN_LISTENING', error=None, samples=samples)


class VehicleAudioEvidence(unittest.TestCase):
    def test_actual_progress_is_distinct_from_human_listening(self):
        evidence = validate(trace(), 21, 'rebirthCamper80s')
        self.assertTrue(evidence['nativePlaybackVerified'])
        self.assertFalse(evidence['humanListeningVerified'])

    def test_dispatched_only_static_muted_or_out_of_range_does_not_pass(self):
        data = trace()
        data['samples'] = []
        with self.assertRaises(ValueError):
            validate(data, 21, 'rebirthCamper80s')
        for field, value in (('timeSamples', 0), ('mute', True), ('volume', 0), ('pitch', float('nan')),
                             ('isPlaying', False), ('listenerDistance', 40), ('clip', None)):
            data = trace()
            for sample in data['samples']:
                sample['sources'][0][field] = value
            with self.assertRaises(ValueError):
                validate(data, 21, 'rebirthCamper80s')

    def test_wrong_entity_or_clip_reuse_cannot_manufacture_progress(self):
        for field, value in (('entityId', 22), ('vehicleName', 'vehicleTruck4x4')):
            data = trace()
            data['samples'][0][field] = value
            with self.assertRaises(ValueError):
                validate(data, 21, 'rebirthCamper80s')
        data = trace()
        data['samples'][1]['sources'][0]['clipInstanceId'] = 99
        with self.assertRaises(ValueError):
            validate(data, 21, 'rebirthCamper80s')
