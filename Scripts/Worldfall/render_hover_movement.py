"""从真实程序集回放水面背包与月亮单位悬浮，使用正式几何和背面剔除。"""
from pathlib import Path
import sys
import bpy

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT/'Scripts/Worldfall'))
import unit_model_tools as tools
from render_creature_movement import world_model, bounds

BATCH = ROOT/'Art/Worldfall/water-float-v1'
runtime = tools.read(BATCH/'runtime-check.json')
records = runtime['records']
models = {m['Name']: m for asset in {r['asset'] for r in records}
          for m in tools.read(ROOT/'Content/Worldfall/Models'/asset/'models.json')['Models']}
checks, groups = [], []
for record in records:
    for frame in record['poses']:
        low, high = bounds(world_model(models[record['name']], frame['body']))
        assert low[2] > .01, (record['name'], '身体仍触地', low[2])
        checks.append({'model': record['name'], 'body_min_z': low[2]})
        if frame['extra_model']:
            low, high = bounds(world_model(frame['extra_model'], frame['extra_pose']))
            assert low[2] > 0, (record['name'], '尾焰伸入水面', low[2])
    if record['name'].endswith(('_m','_f')):
        continue
    name = record['name']
    frame = record['poses'][0]
    body = world_model(models[name], frame['body'])
    low, high = bounds(body)
    framing = max(high[2], high[0]-low[0], high[1]-low[1], 1.2)*1.5
    target = (0,0,high[2]*.48)
    images = {}
    for suffix, caption, camera in (('', 'front', (4,-6,3.7)), ('', 'back', (-4,6,3.7)),
                                    ('', 'side', (6,0,3)), ('_m', 'middle', (4,-6,3.7)),
                                    ('_f', 'far', (4,-6,3.7))):
        current = next(r for r in records if r['name'] == name+suffix)
        pose = current['poses'][0]
        tools.inspect.clear_scene()
        tools.inspect.mesh_object(world_model(models[name+suffix],pose['body']), name+suffix)
        if pose['extra_model']:
            tools.inspect.mesh_object(world_model(pose['extra_model'],pose['extra_pose']), '双喷口尾焰')
        bpy.ops.mesh.primitive_plane_add(size=8,location=(0,0,0))
        bpy.context.object.data.materials.append(tools.inspect.solid_material('水面' if frame['extra_model'] else '地面',
            (62,112,136) if frame['extra_model'] else (91,108,114)))
        tools.render_scene(BATCH/'preview'/name, [(caption,camera,target,framing)])
        images[caption] = f'preview/{name}/{caption}.png'
    groups.append({'asset':record['asset'],'name':name,'images':images})
tools.write(BATCH/'render-check.json',{'passed':True,'backface_culled':True,'moving_samples':checks,'groups':groups,
    'method':'正式几何与实际程序集姿态；全部15距离模型两相最低顶点检查；水面零高；背面剔除CPU预览',
    'game_test':'pending_user_manual'})
print({'passed':True,'groups':len(groups),'samples':len(checks)})
