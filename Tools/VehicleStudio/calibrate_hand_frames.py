"""Convert captured donor grips to the camper shaft frame; not gameplay acceptance."""
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parent

def mul(a, b):
    x,y,z,w = a; X,Y,Z,W = b
    return (w*X+x*W+y*Z-z*Y, w*Y-x*Z+y*W+z*X,
            w*Z+x*Y-y*X+z*W, w*W-x*X-y*Y-z*Z)

def axis_x(degrees):
    angle = math.radians(degrees)/2
    return (math.sin(angle), 0, 0, math.cos(angle))

def unity_euler(q):
    x,y,z,w = q
    # Unity Euler applies Z, then X, then Y (Ry * Rx * Rz).
    X = math.asin(max(-1, min(1, 2*(w*x-y*z))))
    Y = math.atan2(2*(x*z+w*y), 1-2*(x*x+y*y))
    Z = math.atan2(2*(x*y+w*z), 1-2*(x*x+z*z))
    return dict(zip(('x','y','z'), (math.degrees(v) for v in (X,Y,Z))))

def unity_quaternion(euler):
    x,y,z = (math.radians(euler[k])/2 for k in ('x','y','z'))
    return mul(mul((0,math.sin(y),0,math.cos(y)),
                   (math.sin(x),0,0,math.cos(x))),
               (0,0,math.sin(z),math.cos(z)))

def donor_to_camper(euler):
    return unity_euler(mul(axis_x(89.24567726861585),unity_quaternion(euler)))

def run():
    reference = json.loads((ROOT/'inputs/runtime_reference/20261011_022337_857_192_seated_e29efdca.json').read_text())
    nodes = {n['path']:n for n in reference['hierarchy']}
    wheel = nodes['$vehicle/M/SteeringWheel']['pose']['localRotation']
    # Captured donor rotation factorizes as Rx(tilt) * Rz(steering).
    # x/w is unchanged by its Z steering, recovering the neutral shaft tilt.
    donor_tilt = math.degrees(2*math.atan2(wheel['x'],wheel['w']))
    camper_tilt = math.degrees(2*math.atan2(-0.58244705,0.8128687))
    conversion = axis_x(donor_tilt-camper_tilt)
    report = {'kind':'CAMPER_HAND_FRAME_CALIBRATION', 'status':'CANDIDATE_REQUIRES_VISUAL_GAME_TEST',
              'donorNeutralTiltX':donor_tilt, 'camperTiltX':camper_tilt,
              'frameConversionX':donor_tilt-camper_tilt, 'rotations':{},
              'note':'Cancel donor steering before transferring wrist orientation; preserve camper seat positions.'}
    for name in ('IKHandL','IKHandR'):
        grip = nodes['$vehicle/M/SteeringWheel/'+name]['pose']['localRotation']
        q = tuple(grip[k] for k in ('x','y','z','w'))
        converted = mul(conversion,q)
        report['rotations'][name] = {'quaternion':dict(zip(('x','y','z','w'),converted)),
                                   'unityEuler':unity_euler(converted)}
    dest = ROOT/'reports/driver_fit/20261011_axis_correction/hand_frame_candidate.json'
    dest.parent.mkdir(parents=True,exist_ok=True)
    dest.write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(report,indent=2))

if __name__ == '__main__':
    run()
