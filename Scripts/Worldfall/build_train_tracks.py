"""制作与黑红蒸汽列车共用轮距的立体轨道；在 Blender 后台运行。"""
import itertools
import json
import math
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'Scripts/Worldfall'))
from blender_worldfall import Asset, packed_color

ASSET = 'Cultiway.TrainTrack'
FAMILY = 'cultiway_train_track_0'
OUTPUT = ROOT / 'Art/Worldfall' / ASSET / 'steam-v1'
PALETTE = [('ballast', (73, 70, 66), 0), ('sleeper', (77, 50, 33), 3),
           ('rail_steel', (151, 160, 166), 10), ('rail_web', (76, 84, 91), 10),
           ('fastening', (51, 54, 58), 10), ('buffer_red', (146, 44, 49), 10)]
GAUGE = .92
RAIL_TOP = .14


class TrackGeometry:
    """先构造封闭的轨条与枕木，再交给现有 Blender 导出器。"""
    def __init__(self):
        self.vertices = []
        self.triangles = []
        self.slots = []

    def face(self, points, slot):
        start = len(self.vertices) // 3
        self.vertices.extend(v for point in points for v in point)
        for i in range(1, len(points) - 1):
            self.triangles.extend((start, start + i, start + i + 1))
            self.slots.append(slot)

    def box(self, center, half, angle, slot):
        x, y, z = center
        hx, hy, hz = half
        c, s = math.cos(angle), math.sin(angle)
        points = [(x + dx*c-dy*s, y + dx*s+dy*c, z+dz)
                  for dx, dy, dz in itertools.product((-hx, hx), (-hy, hy), (-hz, hz))]
        for corners in [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1),
                        (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]:
            self.face([points[i] for i in corners], slot)

    def ribbon(self, points, offset, half, bottom, top, slot):
        """同一截面的连续封闭轨条，端点严格对齐相邻格。"""
        sections = []
        for x, y, tx, ty in points:
            nx, ny = -ty, tx
            sections.append([(x+nx*(offset-half), y+ny*(offset-half), bottom),
                             (x+nx*(offset+half), y+ny*(offset+half), bottom),
                             (x+nx*(offset+half), y+ny*(offset+half), top),
                             (x+nx*(offset-half), y+ny*(offset-half), top)])
        self.face(list(reversed(sections[0])), slot)
        self.face(sections[-1], slot)
        for a, b in zip(sections, sections[1:]):
            for i in range(4):
                j = (i+1) % 4
                self.face([a[i], a[j], b[j], b[i]], slot)

    def path(self, points, detail, sleepers=True, rise=0):
        count = max(2, round(sum(math.dist(a[:2], b[:2]) for a, b in zip(points, points[1:])) / .29))
        # 按真实曲线弧长放枕木；交叉口的重合枕木随后按同位置去重。
        for step in range(count if sleepers else 0):
            q = (step+.5) / count * (len(points)-1)
            at = min(len(points)-2, int(q)); f = q-at
            a, b = points[at], points[at+1]
            x, y, tx, ty = [a[i]*(1-f)+b[i]*f for i in range(4)]
            angle = math.atan2(ty, tx)
            self.box((x, y, .035), (.067, .65, .023), angle, 1)
            if detail < 2 and len(points) == 2:
                for sign in (-1, 1):
                    self.box((x-ty*.46*sign, y+tx*.46*sign, .064), (.073, .078, .009), angle, 4)
        for side in (-1, 1):
            offset = side*GAUGE/2
            if detail < 2:
                self.ribbon(points, offset, .034 if len(points) > 2 else .064, .058+rise, .077+rise, 3)
                self.ribbon(points, offset, .016, .077+rise, .118+rise, 3)
            else:
                self.ribbon(points, offset, .028, .058+rise, .118+rise, 3)
            self.ribbon(points, offset, .037, .118+rise, RAIL_TOP+rise, 2)


def route(a, b, detail):
    """位一、二、四、八分别表示上、右、下、左。"""
    directions = [(0, 1), (1, 0), (0, -1), (-1, 0)]
    ax, ay = directions[a]; bx, by = directions[b]
    if (a-b) % 2 == 0:
        return [(ax*.5, ay*.5, -ax, -ay), (bx*.5, by*.5, -ax, -ay)]
    cx, cy = (ax+bx)*.5, (ay+by)*.5
    start = math.atan2(ay*.5-cy, ax*.5-cx)
    end = math.atan2(by*.5-cy, bx*.5-cx)
    delta = (end-start+math.pi) % (2*math.pi)-math.pi
    segments = (8, 5, 3)[detail]
    return [(cx+.5*math.cos(t), cy+.5*math.sin(t),
             -math.sin(t)*math.copysign(1, delta), math.cos(t)*math.copysign(1, delta))
            for t in (start+delta*i/segments for i in range(segments+1))]


def make(mask, detail):
    geometry = TrackGeometry()
    geometry.box((0, 0, .002), (.5, .5, .01), 0, 0)
    directions = [i for i in range(4) if mask & (1 << i)]
    if len(directions) == 0:
        geometry.path([(0, -.38, 0, 1), (0, .38, 0, 1)], detail)
    elif len(directions) == 1:
        x, y = [(0, 1), (1, 0), (0, -1), (-1, 0)][directions[0]]
        geometry.path([(-x*.18, -y*.18, x, y), (x*.5, y*.5, x, y)], detail)
        geometry.box((-x*.17, -y*.17, .20), (.045, .57, .06), math.atan2(y, x), 5)
    elif len(directions) == 2:
        geometry.path(route(*directions, detail), detail)
    else:
        # 岔口共用一组枕木，三向岔轨保留贯通轨和两条转向轨。
        for step in (-.375, -.125, .125, .375):
            geometry.box((0, step, .035), (.65, .065, .023), 0, 1)
        pairs = [(0, 2), (1, 3)] if len(directions) == 4 else list(itertools.combinations(directions, 2))
        for a, b in pairs:
            geometry.path(route(a, b, detail), detail, sleepers=False)
    suffix = ('', '_m', '_f')[detail]
    return {'Name': f'cultiway_train_track_{mask}{suffix}',
            'SlotNames': [p[0] for p in PALETTE], 'SlotColor': [packed_color(p[1]) for p in PALETTE],
            'SlotKind': [p[2] for p in PALETTE], 'SlotFlags': [1]*len(PALETTE),
            'Info': {'kingdom_slots': 'none', 'rail_top': str(RAIL_TOP), 'gauge': str(GAUGE)},
            'Parts': [{'Name': 'track', 'Parent': -1, 'Pivot': {'X': 0, 'Y': 0, 'Z': 0},
                       'Pos': geometry.vertices, 'Nrm': [], 'Tri': geometry.triangles,
                       'Slot': geometry.slots, 'Axis': [2]*len(geometry.slots)}]}


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    data = {'Id': ASSET, 'Category': 'rail', 'Family': FAMILY,
            'Models': [make(mask, detail) for mask in range(16) for detail in range(3)]}
    seed = ROOT / 'Art/Worldfall/Seeds' / f'{ASSET}.json'
    seed.write_text(json.dumps(data, ensure_ascii=False), encoding='utf-8')
    asset = Asset(ASSET)
    # 用Blender已有的实体合并清理交叉的轨条与枕木，去掉重合内面和自遮挡黑斑。
    for model in asset.models.values():
        mask = int(model['source']['Name'].split('track_')[1].split('_')[0])
        if mask in (0, 1, 2, 4, 5, 8, 10): continue
        model['collection'].hide_viewport = False
        for obj in model['collection'].objects:
            bpy.ops.object.select_all(action='DESELECT')
            obj.select_set(True); bpy.context.view_layer.objects.active = obj
            bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
            bpy.ops.mesh.intersect_boolean(operation='UNION', use_self=True, solver='EXACT')
            bpy.ops.mesh.normals_make_consistent(inside=False)
            bpy.ops.object.mode_set(mode='OBJECT')
            bm = bmesh.new(); bm.from_mesh(obj.data)
            bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
            bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=1e-6)
            bmesh.ops.dissolve_limit(bm, angle_limit=.002, use_dissolve_boundaries=False,
                                     verts=list(bm.verts), edges=list(bm.edges), delimit={'MATERIAL'})
            bmesh.ops.triangulate(bm, faces=list(bm.faces))
            degenerate = [face for face in bm.faces if face.calc_area() < 1e-9]
            bmesh.ops.delete(bm, geom=degenerate, context='FACES_ONLY')
            bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
            bm.to_mesh(obj.data); bm.free(); obj.data.update()
    asset.export('按0.92轮距构造钢轨、木枕、压板和封闭端点；16种邻接形态与三档。Blender直接构形，无平台生成初稿。',
                 output_path=str((OUTPUT/'models.candidate.json').relative_to(ROOT)))
    # 网格颜色仍保留原始RGB；Blender材质使用线性输入，避免黑色被显示成浅灰。
    for material in bpy.data.materials:
        color = tuple(c/12.92 if c <= .04045 else ((c+.055)/1.055)**2.4
                      for c in material.diffuse_color[:3]) + (1,)
        material.diffuse_color = color
        if material.use_nodes:
            shader = next(n for n in material.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
            shader.inputs['Base Color'].default_value = color
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT/'model.blend'), compress=True)
    # 最终 GLB 供新进程重开检查，并保留可编辑三档源文件。
    for name, model in asset.models.items():
        asset.show(name, viewport=True)
        bpy.ops.object.select_all(action='DESELECT')
        for obj in model['collection'].objects:
            obj.select_set(True)
        bpy.ops.export_scene.gltf(filepath=str(OUTPUT/f'{name}.glb'), use_selection=True, export_format='GLB')
    print('TRACK_MODELS_READY', len(asset.models))


if __name__ == '__main__':
    main()
