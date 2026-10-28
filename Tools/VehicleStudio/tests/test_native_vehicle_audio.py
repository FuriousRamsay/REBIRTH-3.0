import copy
import sys
import unittest
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from validate_native_vehicle_audio import validate_audio

def trace():
    samples=[]
    for time in (1, 1.05):
        sources=[]
        for source_id, group in ((1,'suv_idle_lp'),(2,'suv_horn')):
            sources.append(dict(entityId=22,route='playingOnEntity.environment',group=group,sourceId=source_id,
                                clip=group,clipInstanceId=source_id+10,clipSamples=10000,timeSamples=100 if time==1 else 200,
                                isPlaying=True,active=True,mute=False,volume=.5,pitch=1))
        samples.append(dict(entityId=22,hornGroup='suv_horn',capturedTime=time,listenerVolume=1,listenerPaused=False,sources=sources))
    return dict(kind='REBIRTH_NATIVE_VEHICLE_AUDIO_TRACE',status='CAPTURED_NOT_HUMAN_LISTENING',error=None,samples=samples)

class NativeAudio(unittest.TestCase):
    def test_actual_playback_and_advancing_clips_pass(self):
        self.assertEqual(set(validate_audio(trace(),22)['witnessed']), {'engine','horn'})
    def test_requests_mute_wrong_entity_invalid_gain_and_frozen_cursor_fail(self):
        for field, value in (('isPlaying',False),('mute',True),('entityId',99),('volume',float('nan')),('pitch',0),('timeSamples',100),('route','nearbySource')):
            with self.subTest(field=field):
                evidence=trace()
                for sample in evidence['samples']: sample['sources'][1][field]=value
                with self.assertRaises(ValueError): validate_audio(evidence,22)
    def test_listener_pause_and_unknown_clip_group_fail(self):
        evidence=trace()
        for sample in evidence['samples']: sample['listenerPaused']=True
        with self.assertRaises(ValueError): validate_audio(evidence,22)
        evidence=trace()
        for sample in evidence['samples']: sample['sources'][0]['group']='unrelated_suv_sound'
        with self.assertRaises(ValueError): validate_audio(evidence,22)

if __name__=='__main__': unittest.main()
