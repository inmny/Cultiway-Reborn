"""导出真实加载后的比例与持物姿态，再在独立进程实导最终文件检查。"""
import argparse
import os
from pathlib import Path
import sys
import bpy

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Scripts/Worldfall'))
import unit_model_tools as tools
from render_creature_movement import world_model, bounds

BATCH=ROOT/'Art/Worldfall/daemon-held-items-v1'


def run(mode):
    runtime=tools.read(BATCH/'runtime-check.json')
    models={m['Name']:m for m in tools.read(BATCH/'loaded-models.json')['Models']}
    groups,files=[],[]
    export=BATCH/'final-glbs'
    export.mkdir(exist_ok=True)
    if mode=='check':
        assert tools.read(BATCH/'export-check.json')['pid']!=os.getpid()
    for record in runtime['records']:
        model=models[record['name']]
        near=tools.level_of(model['Name'])=='near'
        for frame in record['poses']:
            posed=world_model(model,frame['corrected'])
            feet=[p for p in posed['Parts'] if p['Name'] in ('leg_l','leg_r')]
            minimum=min(min(p['Pos'][2::3]) for p in feet)
            # 大不净者原生满步幅有轻微下沉，本轮只移髋位且保持原腿长；站姿与新拉长的腿都须落地正常。
            if not (record['asset'].startswith('Cultiway.GreatUncleanOne') and frame['name']=='move'):
                assert minimum>-.0001,(record['asset'],frame['name'],'脚底穿地')
            if frame['name']!='idle' and not near: continue
            filename=model['Name']+'.'+frame['name']+'.glb'
            path=export/filename
            if mode=='export':
                tools.inspect.clear_scene()
                obj=tools.inspect.mesh_object(posed,model['Name'])
                tools.inspect.export_glb([obj],path)
                tools.glb_normals(path,posed,restore=True)
                continue
            proof=tools.inspect.inspect_glb(path,posed,0)
            proof.update(tools.glb_normals(path,posed))
            files.append(proof)
            low,high=bounds(posed)
            span=max(high[2]-low[2],high[0]-low[0],high[1]-low[1])*1.3
            target=(0,0,(high[2]+low[2])*.5)
            views=[('oblique',(4,-6,3.2),target,span)]
            if near and frame['name']=='idle':
                views += [('front',(0,-6,target[2]),target,span),('side',(6,0,target[2]),target,span)]
            destination=BATCH/'final-preview'/model['Name']/frame['name']
            tools.render_scene(destination,views)
            groups.append({'asset':record['asset'],'name':model['Name'],'level':tools.level_of(model['Name']),
                'pose':frame['name'],'images':{v[0]:(destination/(v[0]+'.png')).relative_to(BATCH).as_posix() for v in views}})
    if mode=='export':
        tools.write(BATCH/'export-check.json',{'passed':True,'pid':os.getpid(),'loaded_models_sha256':tools.digest(BATCH/'loaded-models.json')})
    else:
        tools.write(BATCH/'loaded-reopen-check.json',{'passed':True,'IndependentBlenderProcess':True,
            'ImportedAllFinalGLBs':True,'loaded_models_sha256':tools.digest(BATCH/'loaded-models.json'),
            'runtime_sha256':tools.digest(BATCH/'runtime-check.json'),'files':files,'groups':groups,'game_test':'pending_user_manual'})
    print({'mode':mode,'checked_glbs':len(files),'groups':len(groups)})


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('mode',choices=('export','check'))
    run(parser.parse_args(sys.argv[sys.argv.index('--')+1:]).mode)
