"""从实际程序集导出的姿态回放飞行、狼跑与三档，检查整只身体离地。"""
import copy
import argparse
import hashlib
import json
import sys
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
import unit_model_tools as tools

ROOT=Path(__file__).resolve().parents[2]
BATCH=ROOT/'Art/Worldfall/movement-v1'


def world_model(model, pose):
    result=tools.posed_model(model,pose)
    for part in result['Parts']:
        for index in range(len(part['Pos'])//3):
            for axis in range(3):
                part['Pos'][index*3+axis]+=pose['position'][axis]
    return result


def bounds(model):
    vertices=[p['Pos'][axis::3] for p in model['Parts'] if p['Pos'] for axis in range(3)]
    return [min(min(v) for v in vertices[axis::3]) for axis in range(3)], [max(max(v) for v in vertices[axis::3]) for axis in range(3)]


def render(model, destination, name, target, framing):
    tools.inspect.clear_scene()
    tools.inspect.mesh_object(model,model['Name'])
    bpy.ops.mesh.primitive_plane_add(size=7,location=(0,0,0))
    plane=bpy.context.object
    material=tools.inspect.solid_material('地面高度零',(91,108,114))
    plane.data.materials.append(material)
    tools.render_scene(destination,[(name,(4,-6,4.6),target,framing)])


def group_pose_sha(name, records):
    group=[r for r in records if r['name'] in (name,name+'_m',name+'_f')]
    return hashlib.sha256(json.dumps(group,sort_keys=True,separators=(',',':')).encode()).hexdigest()


def main(assets=None):
    runtime=tools.read(BATCH/'runtime-check.json')
    pose_digest=hashlib.sha256(json.dumps([runtime['flight'],runtime['wolves']],sort_keys=True,separators=(',',':')).encode()).hexdigest()
    models={}
    records=runtime['flight']+runtime['wolves']
    previous={r['name']:r for r in tools.read(BATCH/'render-check.json')['groups']} if assets else {}
    for asset in {r['asset'] for r in records}:
        for model in tools.read(ROOT/'Content/Worldfall/Models'/asset/'models.json')['Models']:
            models[model['Name']]=model
    checks=[]
    for record in records:
        model=models[record['name']]
        minima=[]
        for pose in record['poses'][1:]:
            low,high=bounds(world_model(model,pose))
            if record in runtime['flight']:
                assert low[2]>0.01,(record['name'],pose['name'],'飞行身体触地',low[2])
            minima.append(low[2])
        checks.append({'name':record['name'],'minimum_moving_z':min(minima),'flying':record in runtime['flight']})
    tools.write(BATCH/'geometry-check.json',{'passed':True,'runtime_check_sha256':tools.digest(BATCH/'runtime-check.json'),'pose_data_sha256':pose_digest,
        'method':'实际导出姿态逐部件继承父轴，再加入本次离地高度；检查全部三档所有移动采样的最低顶点',
        'game_test':'pending_user_manual','models':checks})
    groups=[]
    for record in records:
        if record['name'].endswith(('_m','_f')):continue
        name=record['name']
        group_digest=group_pose_sha(name,records)
        production_digest=tools.digest(ROOT/'Content/Worldfall/Models'/record['asset']/'models.json')
        if assets and record['asset'] not in assets:
            reused=previous[name]
            assert reused['pose_data_sha256']==group_digest and reused['production_sha256']==production_digest,name
            assert all((BATCH/path).is_file() for path in reused['images'].values()),name
            groups.append(reused)
            continue
        model=models[name]
        poses=record['poses'][1:]
        if 'wing' in record:
            index=next(i for i,p in enumerate(model['Parts']) if p['Name']==record['wing'])
            score=lambda pose:pose['parts'][index]['columns'][0][2]+pose['parts'][index]['columns'][1][2]
            chosen=[min(poses,key=score),max(poses,key=score)]
        else:chosen=poses[:2]
        all_bounds=[bounds(world_model(model,pose)) for pose in chosen]
        high=max(b[1][2] for b in all_bounds)
        across=max(max(b[1][0]-b[0][0],b[1][1]-b[0][1]) for b in all_bounds)
        framing=max(high,across,1.3)*1.45
        target=(0,0,high*0.45)
        destination=BATCH/'preview'/name
        for label,pose in zip(('move_a','move_b'),chosen):
            render(world_model(model,pose),destination,label,target,framing)
        for suffix,label in (('_m','middle'),('_f','far')):
            variant=next(r for r in records if r['name']==name+suffix)
            pose=next(p for p in variant['poses'] if p['name']==chosen[0]['name'])
            render(world_model(models[name+suffix],pose),destination,label,target,framing)
        groups.append({'asset':record['asset'],'name':name,'pose_data_sha256':group_digest,'production_sha256':production_digest,
            'images':{label:f'preview/{name}/{label}.png' for label in ('move_a','move_b','middle','far')}})
        tools.write(BATCH/'render-progress.json',{'done':len(groups),'expected':25})
    tools.write(BATCH/'render-check.json',{'passed':True,'runtime_check_sha256':tools.digest(BATCH/'runtime-check.json'),'pose_data_sha256':pose_digest,
        'backface_culled':True,'source':'正式模型几何加实际原生姿态矩阵离线回放；灰平面为地面高度零',
        'game_test':'pending_user_manual','groups':groups})
    print(json.dumps({'passed':True,'moving_models':len(checks),'rendered_groups':len(groups)},ensure_ascii=False))


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--asset',action='append')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    main(set(args.asset) if args.asset else None)
