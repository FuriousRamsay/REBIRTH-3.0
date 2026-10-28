"""Bake fitted source shader channels to 2K maps, then save a map-only scene."""
import bpy,json,time,sys
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
group=sys.argv[sys.argv.index('--group')+1] if '--group' in sys.argv else None
assert group in (None,'masonry','metal','organic')
out=root/('GroupedBaked2K_v3/'+group if group else 'FittedBaked2K_v2');out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/('group-atlas-source.blend' if group else 'fitted-atlas-source.blend')))
sc=bpy.context.scene;empty=[o.name for o in sc.objects if o.type=='MESH' and not o.hide_render and not o.data.polygons]
objects=[o for o in sc.objects if o.type=='MESH' and not o.hide_render and o.name!='Inspection ground' and len(o.data.polygons)>0]
if group:
    names=set(json.loads((root/'group-atlas-plan.json').read_text())['groups'][group]);objects=[o for o in objects if o.name in names]
materials={m for o in objects for m in o.data.materials if m};originals={}
for m in materials:
    output=next(n for n in m.node_tree.nodes if n.type=='OUTPUT_MATERIAL' and n.is_active_output)
    originals[m]=(output,output.inputs['Surface'].links[0].from_socket,m.node_tree.nodes.get('Principled BSDF'))
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
sc.render.engine='CYCLES';sc.cycles.samples=1;sc.render.bake.margin=2 if group else 8;sc.render.bake.use_clear=True
images={};completed=[];started=time.time()
def state(channel):
    (out/'bake-state.json').write_text(json.dumps({'status':'RUNNING','activeChannel':channel,'completed':completed,'resolution':[2048,2048],'excludedEmptyObjects':empty,'elapsedSeconds':time.time()-started},indent=2))
def bind_or_default(links,socket,target):
    if socket.is_linked:links.new(socket.links[0].from_socket,target)
    else:
        value=socket.default_value
        if target.type=='VECTOR' and hasattr(value,'__len__'):value=value[:3]
        target.default_value=value
for channel in ['normal','basecolor','roughness','metallic','emission']:
    state(channel)
    image=bpy.data.images.new('Forge_'+channel+'_2K',width=2048,height=2048,alpha=False,float_buffer=True)
    image.colorspace_settings.name='sRGB' if channel in ('basecolor','emission') else 'Non-Color';images[channel]=image
    for m in materials:
        nodes=m.node_tree.nodes;links=m.node_tree.links;output,original,bs=originals[m]
        links.new(original,output.inputs['Surface'])
        if channel!='normal':
            emit=nodes.new('ShaderNodeEmission');emit.inputs['Strength'].default_value=1
            if channel=='emission':
                scale=nodes.new('ShaderNodeVectorMath');scale.operation='SCALE'
                bind_or_default(links,bs.inputs['Emission Color'],scale.inputs[0])
                factor=nodes.new('ShaderNodeMath');factor.operation='DIVIDE';factor.inputs[1].default_value=2.8
                bind_or_default(links,bs.inputs['Emission Strength'],factor.inputs[0]);links.new(factor.outputs[0],scale.inputs[3]);links.new(scale.outputs[0],emit.inputs['Color'])
            else:
                socket=bs.inputs[{'basecolor':'Base Color','roughness':'Roughness','metallic':'Metallic'}[channel]]
                if socket.is_linked:links.new(socket.links[0].from_socket,emit.inputs['Color'])
                else:
                    value=socket.default_value;emit.inputs['Color'].default_value=tuple(value) if channel=='basecolor' else (value,value,value,1)
            links.new(emit.outputs[0],output.inputs['Surface'])
        target=nodes.new('ShaderNodeTexImage');target.image=image;target.name='BAKE_TARGET_'+channel;nodes.active=target
    bpy.ops.object.bake(type='NORMAL' if channel=='normal' else 'EMIT')
    image.filepath_raw=str(out/f'forge-{channel}-2k.png');image.file_format='PNG';sc.render.image_settings.color_depth='16';image.save();completed.append(channel)
for m,(output,original,bs) in originals.items():m.node_tree.links.new(original,output.inputs['Surface'])
bpy.ops.wm.save_as_mainfile(filepath=str(out/'source-with-bakes.blend'))
mat=bpy.data.materials.new('Forge_Baked2K_'+(group or 'shared'));mat.use_nodes=True;nodes=mat.node_tree.nodes;links=mat.node_tree.links;bs=nodes.get('Principled BSDF')
for channel,image in images.items():
    tex=nodes.new('ShaderNodeTexImage');tex.image=image;tex.interpolation='Linear'
    if channel=='normal':
        normal=nodes.new('ShaderNodeNormalMap');links.new(tex.outputs['Color'],normal.inputs['Color']);links.new(normal.outputs[0],bs.inputs['Normal'])
    else:links.new(tex.outputs['Color'],bs.inputs[{'basecolor':'Base Color','roughness':'Roughness','metallic':'Metallic','emission':'Emission Color'}[channel]])
bs.inputs['Emission Strength'].default_value=2.8
for o in objects:o.data.materials.clear();o.data.materials.append(mat)
sc.cycles.samples=48;sc.render.filepath=str(out/'baked-reference.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out/('group-material-only.blend' if group else 'baked-only.blend')))
if not group:bpy.ops.render.render(write_still=True)
(out/'bake-state.json').write_text(json.dumps({'status':'MAPS_SAVED_REVIEW_PENDING' if group else 'MAPS_AND_RENDER_SAVED_REVIEW_PENDING','group':group,'groupRenderProduced':not bool(group),'completed':completed,'resolution':[2048,2048],'objects':len(objects),'emissionReconstructionStrength':2.8,'elapsedSeconds':time.time()-started,'remaining':['reopen/inspect maps and scene','source-versus-baked appearance comparison','fidelity review','Unity verification']},indent=2));print('FITTED_2K_MAPS_SAVED')
