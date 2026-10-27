"""对照实际原生姿态与修正姿态，聚焦躯干下面的髋腿位置。"""
import copy
from pathlib import Path
import sys
import bpy

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Scripts/Worldfall'))
import unit_model_tools as tools
from render_creature_movement import world_model, bounds

BATCH=ROOT/'Art/Worldfall/daemon-held-items-v1'
runtime=tools.read(BATCH/'runtime-check.json')
loaded={m['Name']:m for m in tools.read(BATCH/'loaded-models.json')['Models']}
groups=[]
for record in runtime['records']:
    if record['name'].endswith(('_m','_f')):
        continue
    source=tools.read(ROOT/'Art/Worldfall'/record['asset']/'native-unit-v1/models.candidate.json')
    models={'native':copy.deepcopy(source['Models'][0]),'corrected':copy.deepcopy(loaded[record['name']])}
    # 只为髋腿检查临时隐去头、臂、羽翼和手持物；不修改任何正式模型。
    for model in models.values():
        for part in model['Parts']:
            if part['Name'] not in ('body','leg_l','leg_r'):
                part.update(Pos=[],Nrm=[],Tri=[],Slot=[],Axis=[])
    model=models['corrected']
    leg_top=max(p['Pos'][2::3] and max(p['Pos'][2::3]) or 0 for p in model['Parts'] if p['Name'].startswith('leg_'))
    low,high=bounds(model)
    framing=max(leg_top*2.25,(high[0]-low[0])*1.35,.3)
    target=(0,0,leg_top*.65)
    images={}
    frame=record['poses'][0]
    for stage in ('native','corrected'):
        posed=world_model(models[stage],frame[stage])
        assert abs(bounds(posed)[0][2])<.0001,(record['asset'],'站立脚底离地')
        tools.inspect.clear_scene()
        tools.inspect.mesh_object(posed,'髋腿对照')
        bpy.ops.mesh.primitive_plane_add(size=8,location=(0,0,-.001))
        bpy.context.object.data.materials.append(tools.inspect.solid_material('地面零高',(91,108,114)))
        destination=BATCH/'leg-preview'/record['asset']
        tools.render_scene(destination,[(stage+'_front',(0,-6,1.8),target,framing),(stage+'_side',(6,0,1.8),target,framing)])
        for view in ('front','side'):
            images[stage+'_'+view]=f'leg-preview/{record["asset"]}/{stage}_{view}.png'
    groups.append({'asset':record['asset'],'images':images})
tools.write(BATCH/'leg-render-check.json',{'passed':True,'groups':groups,'backface_culled':True,
    'method':'原始身体与实际加载后拉长的身体分别采用对应姿态；仅为检查隐藏头臂与翼，全部站立脚底仍为地面零高',
    'game_test':'pending_user_manual'})
print({'passed':True,'groups':len(groups)})
