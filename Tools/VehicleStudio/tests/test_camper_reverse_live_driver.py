import importlib.util
from pathlib import Path
import sys
import unittest

ROOT=Path(__file__).resolve().parents[3]
sys.path.insert(0,str(ROOT/'Tools/GameBridge'))
from camper_reverse_illumination_live import reverse_profile,released_profile


class ReverseDriver(unittest.TestCase):
    def test_reverse_requires_actual_velocity_and_complete_profile(self):
        p=dict(forwardVelocity=-1,rearEmittingCount=2,rearMaxEffectiveIntensity=3.25,rearMaxRange=6,rearMaxSpotAngle=60)
        reverse_profile(p)
        for key,value in [('forwardVelocity',0),('rearEmittingCount',1),('rearMaxEffectiveIntensity',.65),('rearMaxRange',4),('rearMaxSpotAngle',48)]:
            bad=dict(p);bad[key]=value
            with self.assertRaises(AssertionError):reverse_profile(bad)

    def test_release_rejects_stuck_boost_or_motion(self):
        p=dict(forwardVelocity=0,rearMaxEffectiveIntensity=.12,rearLights=[dict(effectiveIntensity=.12,range=4,spotAngle=48)])
        released_profile(p)
        p['forwardVelocity']=.325
        released_profile(p)
        for key,value in [('forwardVelocity',.5),('rearMaxEffectiveIntensity',3.25)]:
            bad=dict(p);bad[key]=value
            with self.assertRaises(AssertionError):released_profile(bad)

    def test_driver_does_not_fake_physics(self):
        source=(ROOT/'Tools/GameBridge/camper_reverse_illumination_live.py').read_text()
        self.assertIn("r.call('/move','POST',forward=-1",source)
        self.assertIn('forward=1 if velocity<0 else -1',source)
        self.assertIn('native parked vehicle did not settle after exit',source)
        self.assertIn("r.call('/key','POST',name='E')",source)
        self.assertNotIn("r.call('/console'",source)
        self.assertNotIn('setvelocity',source)


if __name__=='__main__':unittest.main()
