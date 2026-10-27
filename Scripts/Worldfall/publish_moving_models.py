"""发布主代理已看过的移动变身候选，站立原形逐项核对。"""
import argparse
from pathlib import Path
import shutil
from native_unit_report import ROOT, REGISTRY, read, write, sha


def publish(asset):
    folder = ROOT/'Art/Worldfall'/asset/'movement-v1'
    production = ROOT/'Content/Worldfall/Models'/asset/'models.json'
    doc, strategy, reopen, review = [read(folder/name) for name in
        ('models.candidate.json', 'strategy.json', 'reopen-check.json', 'root-review.json')]
    digest = sha(folder/'models.candidate.json')
    assert digest == strategy['candidate_sha256'] == reopen['candidate_sha256'] == review['candidate_sha256']
    assert review['actual_image_view'] and review['reviewer'] == 'root'
    assert all(reopen[k] for k in ('Passed', 'IndependentBlenderProcess', 'ReopenedEditableSource', 'ImportedAllFinalGLBs'))
    old = read(ROOT/'Art/Worldfall'/asset/'native-unit-v1/models.candidate.json')
    assert len(doc['Models']) == 6 and doc['Models'][:3] == old['Models']
    assert all(m['Name'] == doc['Family']+'_moving'+suffix for m, suffix in zip(doc['Models'][3:], ('','_m','_f')))
    original_budgets = read(ROOT/'Art/Worldfall'/asset/'native-unit-v1/strategy.json')['models'][:3]
    triangles = {}
    for i, model in enumerate(doc['Models']):
        count = sum(len(p['Tri'])//3 for p in model['Parts'])
        assert count <= original_budgets[i % 3]['budget']
        assert len(model['SlotColor']) <= 64
        if i >= 3:
            assert all(model['Info'][k] == old['Models'][i % 3]['Info'][k] for k in ('height','length'))
        triangles[model['Name']] = count
    source_glbs = ['movement-v1/'+p.relative_to(folder).as_posix() for p in sorted((folder/'glbs').glob('*.glb'))]
    assert len(source_glbs) == len(doc['Models'])
    shutil.copyfile(folder/'models.candidate.json', production)
    manifest = {'asset':asset,'version':'movement-v1','publication':'published_offline',
        'production_resource':production.relative_to(ROOT).as_posix(),'production_sha256':digest,
        'models':len(doc['Models']),'triangles':triangles,'standing_models_preserved':True,
        'source_glbs':source_glbs,'editable_sources':['movement-v1/model.blend','movement-v1/build.py'],
        'review':review,'game_test':'pending_user_manual'}
    write(folder/'manifest.json',manifest)
    registry = read(REGISTRY)
    row = next(r for r in registry if r['Id']==asset)
    row.update(RuntimeSha256=digest,Models=len(doc['Models']),ExportedModels=len(doc['Models']),Triangles=triangles,
        NativeVersion='movement-v1',EditableSources=manifest['editable_sources'],source_glbs=source_glbs,
        PublicationRecord=(folder/'manifest.json').relative_to(ROOT).as_posix(),GameTest='pending_user_manual',
        Status='移动变身与原形已离线发布；游戏动作待查看')
    row['inspection_files']['movement_contact']='movement-v1/contact.png'
    row['inspection_files']['movement_reopen']='movement-v1/reopen-check.json'
    if asset == 'Cultiway.BloodBeast': row['Shape']=old['Models'][0]['Info']['cultiway_body']
    write(REGISTRY,registry)
    print({'published':asset,'models':len(doc['Models']),'sha256':digest})


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('assets',nargs='+')
    for asset_id in parser.parse_args().assets:
        publish(asset_id)
