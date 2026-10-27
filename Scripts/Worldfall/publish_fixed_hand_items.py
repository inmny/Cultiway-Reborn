"""发布按各自原图补好的固定手持物，旧身体逐部件保留。"""
import argparse
import shutil
from native_unit_report import ROOT, REGISTRY, read, write, sha


def publish(asset):
    folder = ROOT/'Art/Worldfall'/asset/'held-items-v1'
    production = ROOT/'Content/Worldfall/Models'/asset/'models.json'
    doc = read(folder/'models.candidate.json')
    original = read(folder.parent/'native-unit-v1/models.candidate.json')
    review, reopen = read(folder/'root-review.json'), read(folder/'reopen-check.json')
    digest = sha(folder/'models.candidate.json')
    assert digest == review['candidate_sha256'] == reopen['candidate_sha256']
    assert review['reviewer'] == 'root' and review['actual_image_view']
    assert all(reopen[k] for k in ('Passed','IndependentBlenderProcess','ReopenedEditableSource','ImportedAllFinalGLBs'))
    assert len(doc['Models']) == len(original['Models']) == 3
    counts = {}
    for model, previous in zip(doc['Models'],original['Models']):
        assert model['Name'] == previous['Name']
        assert model['Parts'][:len(previous['Parts'])] == previous['Parts']
        assert all(model['Info'][k] == v for k,v in previous['Info'].items())
        assert model['Info']['cultiway_fixed_hand_items'] == '1'
        for key in ('SlotNames','SlotColor','SlotFlags','SlotKind'):
            assert model[key][:len(previous[key])] == previous[key]
        added = model['Parts'][len(previous['Parts']):]
        assert added or model['Info'].get('cultiway_fixed_existing_hand_items') == 'wing_l'
        assert len(model['SlotColor']) <= 64
        for part in added:
            assert part['Name'] in ('held_item_r','held_item_l')
            arm = 'wing_l' if part['Name'] == 'held_item_r' else 'wing_r'
            assert model['Parts'][part['Parent']]['Name'] == arm
            assert part['Tri']
        counts[model['Name']] = sum(len(p['Tri'])//3 for p in model['Parts'])
    glbs = sorted((folder/'glbs').glob('*.glb'))
    assert len(glbs) == 3
    shutil.copyfile(folder/'models.candidate.json',production)
    manifest = {'asset':asset,'version':'held-items-v1','publication':'published_offline',
        'production_resource':production.relative_to(ROOT).as_posix(),'production_sha256':digest,
        'models':3,'triangles':counts,'all_previous_body_parts_exactly_preserved':True,
        'source_glbs':['held-items-v1/'+p.relative_to(folder).as_posix() for p in glbs],
        'editable_sources':['held-items-v1/model.blend','held-items-v1/build.py'],
        'review':review,'game_test':'pending_user_manual'}
    write(folder/'manifest.json',manifest)
    registry = read(REGISTRY)
    row = next(r for r in registry if r['Id'] == asset)
    row.update(RuntimeSha256=digest,Models=3,ExportedModels=3,Triangles=counts,NativeVersion='held-items-v1',
        source_glbs=manifest['source_glbs'],EditableSources=manifest['editable_sources'],
        PublicationRecord=(folder/'manifest.json').relative_to(ROOT).as_posix(),GameTest='pending_user_manual',
        Status='按自身原图补好专属手持物，隐藏随机装备外观；游戏效果待查看')
    row['inspection_files']['held_items_contact']='held-items-v1/contact.png'
    write(REGISTRY,registry)
    print({'published':asset,'models':3,'sha256':digest})


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('assets',nargs='+')
    for asset in parser.parse_args().assets:
        publish(asset)
