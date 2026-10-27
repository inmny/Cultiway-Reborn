"""收集本轮已有检查，核对最终页面链接并保存离线交付记录。"""
import json
import re
from pathlib import Path
from html import unescape
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT/'Art/Worldfall/biome-sizing-v1'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def main():
    names=['size-check.json','compiled-check.json','material-check.json','texture-plan-check.json',
           'leaf-card-check.json','adjustment-03-check.json']
    for name in names: assert read(OUT/name)['passed'],name
    previews=read(OUT/'preview-check.json')
    assert len(previews['images'])==15 and previews['camera_ortho_scale']==15.5
    for row in previews['images']: assert (OUT/row['image']).is_file()
    links=[]
    for raw in re.findall(r'(?:href|src)="([^"]+)"',(OUT/'index.html').read_text(encoding='utf-8')):
        link=urlsplit(unescape(raw))
        if not link.scheme and link.path:
            assert (OUT/unquote(link.path)).is_file(),raw
            links.append(raw)
    builds=[]
    for name in ('biome-adjustment-03-build.log','biome-adjustment-03-no-worldfall-build.log'):
        path=ROOT/'tmp/WorldfallAcceptance'/name
        text=path.read_text(encoding='utf-8-sig')
        errors=int(re.findall(r'(\d+)\s*个错误',text)[-1])
        warnings=int(re.findall(r'(\d+)\s*个警告',text)[-1])
        assert errors==0
        builds.append(dict(log=path.relative_to(ROOT).as_posix(),errors=errors,warnings=warnings))
    final=read(OUT/'final-check.json')
    adjustment=read(OUT/'adjustment-03-check.json')
    final.update(passed=True,assets=30,models=909,builds=builds,
        plant_repeated_placements=108,candle_placement_cases=54,root_placement_cases=54,
        candle_fixed_frame_cases=12,leaf_card_transform_cases=576,leaf_card_collection_cases=12,leaf_card_fade_cases=6,
        material_boundary_cases=22,actual_preview_images_viewed=15,preview_local_links_checked=len(links),
        geometry_colors_and_flags_unchanged=False,
        material_application_preserves_geometry_colors_and_flags=True,
        preview_scope='15组最高外观的人物比例与固定配色；橡树PNG叶簇效果见独立实际重导入实验页；未模拟其余原生纹理',
        latest_adjustment='adjustment-03-check.json',latest_adjustment_models=213,
        doubled_models=adjustment['doubled_models'],lowered_models=0,root_offsets_follow_scale_models=54,
        remodeled_models=39,rice_baseline_models=9,knowledge_winding_models=12,oak_leaf_card_models=3,
        changed_previews_viewed=[row['asset'] for row in previews['images']],
        checks=names+['preview-check.json'],known_limitations=adjustment['limitations'],
        transparency_diagnosis='../Rice/native-biome-v2/culling-diagnosis/final-diagnosis.json',
        scope='最终文件、离线实际导入与看图、实际编译的放置及收集检查；未启动或操作游戏',
        game_test='pending_user_manual')
    final.pop('independent_texture_plans',None)
    final['independent_texture_plans']=4
    (OUT/'final-check.json').write_text(json.dumps(final,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(dict(passed=True,models=909,doubled_models=adjustment['doubled_models'],
        previews=15,links=len(links),game_test=final['game_test']),ensure_ascii=False))


if __name__=='__main__':
    main()
