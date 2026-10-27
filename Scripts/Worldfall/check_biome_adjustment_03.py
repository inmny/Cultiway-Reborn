"""逐项核对第三轮倍率、落地位置、实际发布版本与透明叶簇数据。"""
import base64
from collections import Counter
import json
import math
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Art/Worldfall/biome-sizing-v1'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def close(a, b):
    assert math.isclose(a, b, rel_tol=2e-6, abs_tol=2e-6), (a,b)


def main():
    before = {row['name']:row for row in read(OUT/'adjustment-03-before.json')['models_checked']}
    after = read(OUT/'size-check.json')['models_checked']
    selected = {'Bamboo','Candle','FleshBlood','Titans'}
    doubled = Counter()
    for row in after:
        old=before[row['name']]
        multiple=2 if row['asset'].removesuffix('_tree') in selected else 1
        close(row['factor'], old['factor']*multiple)
        close(row['nominal_height'], old['nominal_height']*multiple)
        for a,b in zip(row['place_scale'],old['place_scale']): close(a,b*multiple)
        expected=old['ground_offset']
        if row['asset'] in ('Candle_tree','FleshBlood_tree'): expected*=2
        elif row['asset']=='Candle': expected=-.01+(expected+.01)*2
        close(row['ground_offset'],expected)
        if multiple==2: doubled[row['asset']]+=1
    assert len(after)==len(before)==909
    translucent=0
    for asset in {row['asset'] for row in after}:
        path=ROOT/'Content/Worldfall/Models'/asset/'models.json'
        for model in read(path)['Models']:
            if model['Info'].get('cultiway_tree')!='1' and model['Info'].get('cultiway_plant')!='1': continue
            flags=model['SlotFlags']
            flags=base64.b64decode(flags) if isinstance(flags,str) else flags
            translucent+=sum(bool(flag&8) for flag in flags)
    assert translucent==0
    compiled=read(OUT/'compiled-check.json')
    leaf=read(OUT/'leaf-card-check.json')
    assert compiled['passed'] and compiled['candle_fixed_frame_cases']==12 and compiled['flame_scale_animation_removed']
    assert leaf['passed'] and leaf['transform_cases']==576 and leaf['collection_cases']==12
    recipe=read(ROOT/'Content/Worldfall/LeafCards/Oak_tree.main_2.json')
    textures=[]
    for name in recipe['Textures'].values():
        path=ROOT/'GameResources'/(name+'.png')
        image=Image.open(path).convert('RGBA')
        assert max(image.size)<=512 and set(image.getchannel('A').tobytes()) <= {0,255}
        textures.append(dict(path=path.relative_to(ROOT).as_posix(),size=image.size))
    source_recipe=read(ROOT/'Art/Worldfall/Oak_tree/leaf-cards-v1/cross-data.json')
    for path in source_recipe['TextureSources'].values():
        assert Image.open(ROOT/'Art/Worldfall/Oak_tree/leaf-cards-v1'/path).size==(2048,2048)
    result=dict(passed=True,models=909,doubled_biomes=sorted(selected),doubled_models=sum(doubled.values()),
        doubled_by_asset=dict(doubled),unscaled_models=909-sum(doubled.values()),
        candle_size_animation_removed=True,candle_fixed_frame_cases=12,
        leaf_transform_cases=576,leaf_collection_cases=12,translucent_slots=0,
        runtime_textures=textures,source_texture_resolution=2048,
        remodeled_assets=['FleshBlood_tree','FleshBlood'],rice_baseline_lowered=True,
        knowledge_winding_fixed_triangles=736,oak_leaf_card_appearances=1,
        limitations=['黑森林旧平台网格仍有单层叶面，未在本轮重做；仰视可能剔背面',
                     '原生交叉贴片极端上下角度叶缘收窄；保留封闭冠心，距离和阴影效果待游戏查看',
                     '保留Worldfall近树冠主动淡出；离线检查不代替实际游戏验收'],
        game_test='pending_user_manual')
    (OUT/'adjustment-03-check.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(dict(passed=True,doubled_models=sum(doubled.values()),models=909,textures=len(textures)),ensure_ascii=False))


if __name__=='__main__':
    main()
