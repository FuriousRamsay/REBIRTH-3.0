"""Export actual current prefab skin/anchor inputs for the production C# fixture.

Reads existing Unity text prefab plus the native mesh survey. Never edits assets.
"""
import json
import re
import sys
from pathlib import Path

project = Path("E:/_Haven/shared/REBIRTHVehicleStudio_Camper")
prefab = project / "Assets/REBIRTHApprovedPickupProduction_20261020/rebirthCamper80s.prefab"
survey = Path("E:/_Haven/shared/REBIRTHStaged/VehicleStudio/RobotTurret_20261019/Output/camper-lod-input.json")
output = Path(sys.argv[1])
docs = {int(i): (int(t), s) for t, i, s in re.findall(
    r"--- !u!(\d+) &(\d+)\s*\n(.*?)(?=\n--- !u!|\Z)", prefab.read_text(encoding="utf-8-sig"), re.S)}


def value(s, key):
    return re.search(r"^  " + re.escape(key) + r":\s*([^\r\n]+)", s, re.M).group(1).strip()


def ref(s, key):
    return int(re.search(r"fileID:\s*(-?\d+)", value(s, key)).group(1))


def coords(s, key):
    return {k: float(v) for k, v in re.findall(r"(\w+):\s*([-+\deE.]+)", value(s, key))}


names = {i: value(s, "m_Name") for i, (t, s) in docs.items() if t == 1}
transforms = {ref(s, "m_GameObject"): (i, s) for i, (t, s) in docs.items() if t == 4}
skin_id = next(i for i, n in names.items() if n == "camper_body")
battery_id = next(i for i, n in names.items() if n == "REBIRTHInteraction_battery_bank")
skin = next(s for t, s in docs.values() if t == 33 and ref(s, "m_GameObject") == skin_id)
mesh_ref = value(skin, "m_Mesh")
guid = re.search(r"guid: (\w+)", mesh_ref).group(1)
local_id = int(re.search(r"fileID: (-?\d+)", mesh_ref).group(1))
mesh = next(m for m in json.loads(survey.read_text())["meshes"] if m["guid"] == guid and m["localId"] == local_id)


def cross(a, b):
    return [a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]]


def transform_point(transform_id, point):
    if not transform_id:
        return point
    _, s = docs[transform_id]
    scale, q, p = (coords(s, k) for k in ["m_LocalScale", "m_LocalRotation", "m_LocalPosition"])
    v = [point[i] * scale[a] for i, a in enumerate("xyz")]
    qv = [q[a] for a in "xyz"]
    uv = cross(qv, v)
    uuv = cross(qv, uv)
    v = [v[i] + 2 * (uv[i]*q["w"] + uuv[i]) + p[a] for i, a in enumerate("xyz")]
    return transform_point(ref(s, "m_Father"), v)


battery = next(s for t, s in docs.values() if t == 65 and ref(s, "m_GameObject") == battery_id)
bc, bs = coords(battery, "m_Center"), coords(battery, "m_Size")
center = transform_point(transforms[battery_id][0], [bc[a] for a in "xyz"])
top = max(transform_point(transforms[battery_id][0], [bc[a] + bs[a]*(.5 if i & (1 << j) else -.5)
    for j, a in enumerate("xyz")])[1] for i in range(8))
vertices = [dict(zip("xyz", transform_point(transforms[skin_id][0], [v[a] for a in "xyz"]))) for v in mesh["vertices"]]
result = {"sourcePrefab": str(prefab), "sourceMesh": mesh["asset"], "mountY": (2.23 + 2.46) * .5, "batteryTop": top, "mountZ": center[2],
          "vertices": vertices, "triangles": [i for sub in mesh["submeshes"] for i in sub["triangles"]]}
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps(result, separators=(",", ":")))
print(json.dumps({k: v for k, v in result.items() if k not in ["vertices", "triangles"]}, indent=2))
