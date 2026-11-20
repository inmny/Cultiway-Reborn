/** 倒悬木冠和独立承力根；木纹体积、挂点和接触面来自同一条曲线。 */
import * as T from 'three';
import {part, write, structure, motion} from './form_export.mjs';

const v = p => new T.Vector3(...p);
const pathOf = points => new T.CatmullRomCurve3(points.map(v), false, 'centripetal');
const basis = {Forward: [0, 1, 0], Up: [0, 0, 1]};

/** 在真实截面上挖浅木沟，根末闭合，粗细随弧长逐渐改变。 */
function bark(path, radius, taper, phase, rows = 48, sides = 18) {
  const geo = new T.TubeGeometry(path, rows, radius, sides, false);
  const pp = geo.getAttribute('position'), uv = geo.getAttribute('uv');
  for (let i = 0; i < pp.count; i++) {
    const t = uv.getX(i), a = uv.getY(i) * Math.PI * 2, centre = path.getPointAt(t);
    const ribs = 1 + .12 * Math.cos(a * 5 + t * 4 + phase) + .075 * Math.sin(a * 9 - t * 5);
    const neck = 1 + .12 * Math.sin(t * Math.PI) + .045 * Math.sin(t * 21 + phase);
    const point = new T.Vector3().fromBufferAttribute(pp, i).sub(centre)
      .multiplyScalar(taper(t) * ribs * neck).add(centre);
    pp.setXYZ(i, ...point.toArray());
  }
  const positions = Array.from(pp.array), coords = Array.from(uv.array), indices = Array.from(geo.index.array);
  positions.push(...path.getPointAt(0).toArray()); coords.push(0, .5);
  for (let side = 0; side < sides; side++) indices.push(pp.count, side + 1, side);
  geo.setAttribute('position', new T.Float32BufferAttribute(positions, 3));
  geo.setAttribute('uv', new T.Float32BufferAttribute(coords, 2));
  geo.deleteAttribute('normal'); geo.setIndex(indices);
  geo.userData.smoothSeams = true;
  return geo;
}

/** 冠层由有厚度的偏心叶片群组成，叶群位置和外形随真实枝干分层。 */
function foliage(centre, size, phase) {
  const geometry = new T.IcosahedronGeometry(1, 2), positions = geometry.getAttribute('position');
  for (let i = 0; i < positions.count; i++) {
    const p = new T.Vector3().fromBufferAttribute(positions, i);
    const a = Math.atan2(p.y, p.x), bias = p.z;
    const lump = 1 + .12 * Math.sin(5 * a + phase + bias * 3) + .08 * Math.cos(9 * a - bias * 5 + phase);
    p.multiplyScalar(lump).multiply(v(size));
    p.z += .13 * Math.sin(3 * a + phase) * (1 - bias * bias);
    p.add(v(centre)); positions.setXYZ(i, ...p.toArray());
  }
  geometry.deleteAttribute('normal');
  return geometry;
}

function makeCrown() {
  const id = 'hanging_tree', control = 'crown';
  const controls = [{Id: control, Parent: null, Position: [0, 0, 0], Rotation: [0, 0, 0, 1]}];
  const role = {}, parts = [], links = [];
  const add = (name, geo, surface, pivot, usage, parent = null) => {
    parts.push(part(name, geo, surface, pivot, control)); role[name] = [usage];
    if (parent) links.push([parent, name, pivot, false]);
  };
  const taper = t => Math.pow(1 - t, .45);
  const trunk = [[0,0,-.16],[.10,.06,.65],[-.21,.22,1.60],[-.12,.47,2.6],[.28,.66,3.03]];
  add('trunk', bark(pathOf(trunk), .74, taper, .2), 'wood', trunk[0], 'branch.trunk');
  const branches = [
    [[-.07,.14,1.15],[-.87,.35,1.70],[-1.9,.49,2.33],[-3.04,.76,2.58]],
    [[-.08,.26,1.62],[.91,.57,2.09],[1.85,1.45,2.57],[2.91,2.04,2.78]],
    [[-.09,.18,1.38],[.32,-.63,1.91],[.67,-1.38,2.34],[.92,-2.64,2.62]],
    [[-.19,.30,1.85],[-.7,.96,2.58],[-.91,1.8,3.05],[-1.37,2.64,3.26]],
    [[-.02,.37,2.21],[.66,.40,2.83],[1.6,.26,3.34],[2.34,-.04,3.47]]
  ];
  branches.forEach((points, i) => {
    const name = `bough_${i}`;
    add(name, bark(pathOf(points), .34 - i * .024, taper, i * 1.43, 30, 13), 'wood', points[0], 'branch.lateral', 'trunk');
    const end = v(points.at(-1));
    const shoulder = v(points[2]);
    for (let j = 0; j < 3; j++) {
      const centre = shoulder.clone().lerp(end, .31 + .29 * j);
      centre.x += Math.cos(i * 2.48 + j * 2.16) * .59;
      centre.y += Math.sin(i * 2.48 + j * 2.16) * .63;
      centre.z += .16 + .24 * (j % 2);
      const size = j === 1 ? [1.47, 1.1, .52] : [1.18, .90, .42];
      add(`${name}_crown_${j}`, foliage(centre.toArray(), size, i + j * 2),
        j === 1 ? 'wood_leaf' : 'wood_leaf_shadow', centre.toArray(), 'branch.foliage', name);
    }
  });
  const sockets = [[-.25,.03,.17],[-.60,.16,.35],[.52,-.12,.29]];
  const anchors = sockets.map((Position, i) => ({Id: ['main','west','east'][i], Part: 0, Position,
    Direction: [0, 0, 1], Control: control}));
  const settle = motion('settle', .56, false, controls, 26, (_, t) => ({
    position: [0, 0, 1.15 * (1 - t) ** 3 - .18 * Math.sin(t * Math.PI * 2) * (1 - t)],
    rotation: new T.Quaternion().setFromEuler(new T.Euler(.08 * (1 - t) * Math.cos(t * 9), -.045 * (1 - t), .11 * (1 - t))).toArray()
  }), [{Id:'stable', At:.56}]);
  write(id, parts, anchors, [], controls, [settle], false, basis,
    structure('branch', 'forms_hanging_tree.mjs', 'makeHangingTree', {trunk, branches, sockets}, role, links));
}

/** 每条根是独立部位；专用根尖面和其余根身共用实际缩放及材质。 */
function makeRoot(id, points, radius, twigs) {
  const path = pathOf(points), role = {root: ['branch.trunk']};
  const taper = t => Math.pow(Math.max(0, 1 - t), .62);
  const parts = [part('root', bark(path, radius, taper, 1.1), 'wood', points[0])], links = [];
  twigs.forEach(([at, delta], i) => {
    const base = path.getPointAt(at), end = v(delta);
    const route = pathOf([base.toArray(), base.clone().addScaledVector(end, .46).toArray(), base.clone().add(end).toArray()]);
    const name = `fibres_${i}`;
    parts.push(part(name, bark(route, .084 - .012 * i, taper, i, 20, 9), 'wood', base.toArray()));
    role[name] = ['branch.lateral']; links.push(['root', name, base.toArray(), false]);
  });
  const contact = [];
  const section = [0,.14,.30,.46,.60,.75,.86,.942];
  for (let i = 0; i < section.length - 1; i++)
    contact.push({Id:`shaft_${i + 1}`, Part:0, A:path.getPointAt(section[i]).toArray(),
      B:path.getPointAt(section[i + 1]).toArray(), Radius:radius * taper(section[i + 1]) * .94});
  contact.push({Id:'tip', Part:0, A:path.getPointAt(.967).toArray(), B:path.getPointAt(.992).toArray(), Radius:.055});
  const anchors = [{Id:'root_tip',Part:0,Position:points.at(-1),Direction:[0,0,1]}];
  write(id, parts, anchors, contact, [], [], false, basis,
    structure('branch', 'forms_hanging_tree.mjs', 'makeHangingTree', {path:points,radius,twigs:twigs.map(([at,point])=>[at,...point])}, role, links));
}

export function makeHangingTree() {
  makeCrown();
  makeRoot('hanging_root', [[0,0,0],[.20,.16,-1.55],[.55,-.20,-3.33],[-.05,-.41,-5.45],[.21,-.23,-7.68]], .67,
    [[.37,[.52,-.66,-.99]],[.53,[-.65,.33,-1.04]],[.72,[.49,.40,-.86]]]);
  makeRoot('hanging_lateral_root', [[0,0,0],[-.64,.26,-1.21],[-1.69,.40,-2.88],[-2.77,.25,-4.96],[-3.20,.51,-7.34]], .46,
    [[.4,[-.41,.51,-1.15]],[.52,[.15,-.75,-.98]],[.62,[-.65,.20,-.88]]]);
}
