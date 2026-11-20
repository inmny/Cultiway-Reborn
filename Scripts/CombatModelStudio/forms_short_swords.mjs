/** 原版铁剑与石剑的短兵形体。刃口和实际尖端由同一组截面导出。 */
import * as T from 'three';
import {part, structure, write} from './form_export.mjs';

/** 每一截面依次给出长度位置、两侧刃口和中央折脊厚度。 */
function blade(stations) {
  const rings = stations.map(([z, left, right, depth]) => {
    const spine = left * .44 + right * .56;
    return [[left, 0, z], [spine, -depth, z], [right, 0, z], [spine, depth, z]];
  });
  const vertices = [], uv = [], triangles = [];
  for (let face = 0; face < 4; face++) {
    const base = vertices.length / 3;
    for (const ring of rings) {
      const next = (face + 1) % 4;
      vertices.push(...ring[face], ...ring[next]);
      const length = ring[face][2] / stations.at(-1)[0];
      uv.push(face / 4, length, (face + 1) / 4, length);
    }
    for (let segment = 0; segment < rings.length - 1; segment++) {
      const point = base + segment * 2;
      triangles.push(point, point + 1, point + 3, point, point + 3, point + 2);
    }
  }
  for (const end of [0, rings.length - 1]) {
    const cap = vertices.length / 3;
    const ring = rings[end];
    vertices.push((ring[0][0] + ring[2][0]) * .5, 0, ring[0][2], ...ring.flat());
    uv.push(.5, .5, 0, .5, .5, 0, 1, .5, .5, 1);
    for (let corner = 0; corner < 4; corner++) {
      const a = cap + 1 + corner, b = cap + 1 + (corner + 1) % 4;
      triangles.push(cap, ...(end === 0 ? [b, a] : [a, b]));
    }
  }
  const mesh = new T.BufferGeometry();
  mesh.setAttribute('position', new T.Float32BufferAttribute(vertices, 3));
  mesh.setAttribute('uv', new T.Float32BufferAttribute(uv, 2));
  mesh.setIndex(triangles);
  return mesh;
}

/** 铁剑用压扁的缠柄和横护手，石剑用较厚的绑颈和短柄。 */
function shortSword({id, item, material, stations, handle, grips, scale}) {
  const [diameter, length, guardWidth] = handle;
  const profile = [[0, -length], [diameter * .42, -length], [diameter * .48, -length + .02]];
  for (let wrap = 0; wrap < 6; wrap++) {
    const start = -.05 - (length - .07) * (1 - wrap / 6);
    profile.push([diameter * .47, start], [diameter * .5, start + .014]);
  }
  profile.push([diameter * .46, -.04], [0, -.04]);
  const hilt = new T.LatheGeometry(profile.map(value => new T.Vector2(...value)), 16).rotateX(Math.PI / 2).scale(.88, 1, 1);
  const guard = guardWidth > .2
    ? new T.BoxGeometry(guardWidth, .105, .065).rotateZ(.06).translate(0, 0, -.021)
    : new T.CylinderGeometry(guardWidth * .5, guardWidth * .55, .13, 10).rotateX(Math.PI / 2).translate(0, 0, -.034);
  const parts = [part('blade', blade(stations), material), part('guard', guard, guardWidth > .2 ? material : 'leather'),
    part('grip', hilt, 'leather')];
  const [tipLength, tipLeft, tipRight] = stations.at(-1);
  const tip = [(tipLeft + tipRight) * .5, 0, tipLength];
  const anchors = [{Id: 'tip', Part: 0, Position: tip, Direction: [0, 0, 1]},
    ...grips.map(([Id, z]) => ({Id, Part: 2, Position: [0, 0, z], Direction: [0, 0, 1]}))];
  const contacts = [{Id: 'tip', Part: 0, A: [tip[0], 0, tipLength - .03], B: [tip[0], 0, tipLength - .008], Radius: .009}];
  // 斜切选外侧前刃，横扫选两侧中刃。各段沿网格实际刃口取得端点。
  function edge(group, number, side, from, to) {
    contacts.push({Id: `edge/${group}/${number}`, Part: 0,
      A: [stations[from][side], 0, stations[from][0]], B: [stations[to][side], 0, stations[to][0]], Radius: .016});
  }
  edge('cut', 0, 2, 2, 3);
  edge('cut', 1, 2, 3, 4);
  edge('sweep', 0, 1, 1, 2);
  edge('sweep', 1, 2, 1, 2);
  write(id, parts, anchors, contacts, [], [], false, null,
    structure('rigid', 'forms_short_swords.mjs', 'shortSword', {stations, handle},
      {blade: ['blade.edge', 'blade.spine'], guard: ['weapon.guard'], grip: ['weapon.grip']},
      [['grip', 'guard', [0, 0, -.04]], ['guard', 'blade', [0, 0, 0]]]),
    grips.map(([Id, , Diameter]) => ({Id, Anchor: Id, Up: [0, 1, 0], Diameter, Scale: [.25, .55]})), [],
    {Groups: ['sword'], EquipmentIds: [item], Scale: scale});
}

/** 两种器形保留稳定的握点和动作接口，各用实际长短与截面。 */
export function makeShortSwords() {
  shortSword({id: 'iron_short_sword', item: 'sword_iron', material: 'metal', handle: [.128, .46, .32], scale: .42,
    grips: [['grip', -.255, .126], ['choke', -.13, .12]], stations: [
      [0, -.094, .111, .033], [.22, -.115, .136, .044], [.68, -.079, .15, .045],
      [1.05, -.056, .158, .037], [1.44, .019, .105, .021], [1.68, .055, .057, .0015]
    ]});
  shortSword({id: 'stone_short_sword', item: 'sword_stone', material: 'stone', handle: [.147, .39, .176], scale: .38,
    grips: [['grip', -.205, .147], ['choke', -.103, .142]], stations: [
      [0, -.088, .086, .048], [.16, -.139, .117, .058], [.52, -.111, .146, .067],
      [.8, -.114, .107, .043], [1.02, -.035, .058, .023], [1.19, .021, .026, .0018]
    ]});
}
