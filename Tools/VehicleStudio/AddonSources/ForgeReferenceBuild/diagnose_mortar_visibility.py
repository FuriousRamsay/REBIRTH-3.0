"""Unlit diagnostic ID material reveals actual mortar surface, not final appearance."""
import bpy,json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
out=root/'MortarVisibilityDiagnostic_v28';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'VisibleMortarIronwork_v27/visible-mortar-ironwork.blend'))
core=bpy.data.objects['furnace_core'];original=core.data.materials[0]
report={'source':'VisibleMortarIronwork_v27/visible-mortar-ironwork.blend','object':core.name,'hideRender':core.hide_render,'material':original.name,'vertices':len(core.data.vertices),'polygons':len(core.data.polygons),'diagnostic':'Unlit magenta mortar ID, all other materials unchanged. Not a product render.'}
assert not core.hide_render
material=bpy.data.materials.new('Diagnostic mortar ID only');material.use_nodes=True
nodes=material.node_tree.nodes;nodes.clear();output=nodes.new('ShaderNodeOutputMaterial');emission=nodes.new('ShaderNodeEmission');emission.inputs['Color'].default_value=(1,0,1,1);emission.inputs['Strength'].default_value=1;material.node_tree.links.new(emission.outputs[0],output.inputs['Surface'])
core.data.materials.clear();core.data.materials.append(material)
sc=bpy.context.scene;sc.cycles.device='CPU';sc.cycles.samples=16;sc.render.filepath=str(out/'mortar-id-reference.png');bpy.ops.render.render(write_still=True)
(out/'diagnostic-receipt.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
