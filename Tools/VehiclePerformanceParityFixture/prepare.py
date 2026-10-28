from pathlib import Path
import re
r=Path(__file__).resolve().parents[2]
s=(r/'Scripts/Vehicles/Restoration/RebirthVehicleRestorationSystem.cs').read_text()
s=s[s.index('public static class RebirthVehicleRuntimePerformance'):]
methods=[]
for name in ['WheelTorqueRatio','DirectAcceleration','DirectBrake','DirectHill','SelectedVelocity','HasPhysicsModifiers']:
 m=re.search(r'    (?:internal|private) static (?:float|bool) '+name+r'\(',s); start=m.start(); tail=s[start:]
 if '=>' in tail[:tail.index('\n')]: end=start+tail.index(';')+1
 else:
  b=s.index('{',start);depth=1;end=b+1
  while depth:
   depth += (s[end]=='{')-(s[end]=='}');end+=1
 methods.append(s[start:end].replace('private static','internal static'))
(r/'Tools/VehiclePerformanceParityFixture/ActualSource.generated.cs').write_text('public static class ActualSource {\n'+'\n'.join(methods)+'\n}')
assert 'original[0] * ratio' in s and 'original[1] * ratio' in s
assert 'wheel.sideStiffnessBase = side.stiffness' in s
assert 'state.CanOperate' in s and 'entity.GetWheelsOnGround() > 0' in s and 'entity.IsEngineRunning' in s
assert 'extraRate * Time.deltaTime' in s
print('Prepared exact production method slices; source gate/idempotent adapter contracts PASS')

# Full runtime block API check uses actual installed game/Unity assemblies, not Unity doubles.
full=s[:s.index("\npublic enum RebirthVehicleRequestOutcome")]
(r/"Tools/VehiclePerformanceParityFixture/NativeApi.generated.cs").write_text("using System;using System.Collections.Generic;using UnityEngine;\n"+full)

central=(r/"Scripts/Vehicles/Restoration/RebirthVehicleRestorationSystem.cs").read_text()
(r/"Tools/VehiclePerformanceParityFixture/CentralDefinitions.generated.cs").write_text(central[:central.index("public static class RebirthVehicleAssemblySerializer")])
