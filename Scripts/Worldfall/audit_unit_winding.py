"""核查生物的封闭片段朝向，报告可疑处供实际剔背图判断，不自动改几何。"""
import argparse
from collections import defaultdict
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BATCH = ROOT / 'Art/Worldfall/creature-native-v1'


def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def inspect_part(part):
    """按共同边找片段；封闭片段的负体积或相邻面同向值得核对。"""
    positions = part['Pos']
    points, unique, indices = [], {}, []
    for i in range(0, len(positions), 3):
        point = tuple(round(v, 6) for v in positions[i:i+3])
        if point not in unique:
            unique[point] = len(points)
            points.append(point)
        indices.append(unique[point])
    faces = [tuple(indices[v] for v in part['Tri'][i:i+3]) for i in range(0, len(part['Tri']), 3)]
    parent = list(range(len(faces)))
    edges = defaultdict(list)

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    for i, face in enumerate(faces):
        for a, b in zip(face, face[1:]+face[:1]):
            key = (min(a, b), max(a, b))
            if edges[key]:
                parent[find(i)] = find(edges[key][0][0])
            edges[key].append((i, 1 if a < b else -1))
    groups = defaultdict(list)
    for i in range(len(faces)):
        groups[find(i)].append(i)
    boundaries, conflicts = defaultdict(int), defaultdict(int)
    for records in edges.values():
        group = find(records[0][0])
        if len(records) != 2:
            boundaries[group] += 1
        elif sum(sign for _, sign in records) != 0:
            conflicts[group] += 1
    issues = []
    for group, members in groups.items():
        volume = 0
        for i in members:
            a, b, c = (points[v] for v in faces[i])
            volume += (a[0]*(b[1]*c[2]-b[2]*c[1]) + a[1]*(b[2]*c[0]-b[0]*c[2]) + a[2]*(b[0]*c[1]-b[1]*c[0]))/6
        closed = boundaries[group] == 0
        if closed and (volume < -1e-9 or conflicts[group]):
            vertices = {v for i in members for v in faces[i]}
            issues.append({'part':part['Name'], 'triangles':len(members),
                'signed_volume':volume, 'orientation_conflicts':conflicts[group],
                'min':[min(points[v][a] for v in vertices) for a in range(3)],
                'max':[max(points[v][a] for v in vertices) for a in range(3)]})
    return issues


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--asset')
    args = parser.parse_args()
    assets = [args.asset] if args.asset else [a['id'] for a in read(BATCH/'progress.json')['assets'] if a['published']]
    result = []
    for asset in assets:
        path = ROOT/'Art/Worldfall'/asset/'native-unit-v1/models.candidate.json' if args.asset else ROOT/'Content/Worldfall/Models'/asset/'models.json'
        doc = read(path)
        models = [{'name':m['Name'], 'issues':[issue for part in m['Parts'] for issue in inspect_part(part)]} for m in doc['Models']]
        result.append({'asset':asset,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'models':models})
    output = ROOT/'Art/Worldfall'/args.asset/'native-unit-v1/winding-audit.json' if args.asset else BATCH/'winding-audit.json'
    output.write_text(json.dumps({'method':'封闭片段签名体积和共同边朝向；可疑处须结合实际剔背图，不自动翻面','assets':result},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    for row in result:
        count = sum(len(m['issues']) for m in row['models'])
        if count:
            largest = sorted((dict(model=m['name'],**issue) for m in row['models'] for issue in m['issues']),key=lambda v:abs(v['signed_volume']),reverse=True)[:4]
            print(json.dumps({'asset':row['asset'],'suspicious_closed_components':count,'largest':largest},ensure_ascii=False))
    print(json.dumps({'assets':len(result),'models_with_suspicions':sum(bool(m['issues']) for a in result for m in a['models']),'output':str(output)},ensure_ascii=False))


if __name__ == '__main__':
    main()
