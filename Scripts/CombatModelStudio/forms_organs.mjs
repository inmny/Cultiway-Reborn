/** 已有实体器官的三维制作源；真实封闭体积和作用形状一起发布。 */
import * as T from 'three';
import {part, write, structure, round} from './form_export.mjs';
import {taperedBody} from './forms_horn.mjs';

/** 按实际顶点求一段包络，保留部件身份；薄面不扩大成整只身体。 */
function bound(geometry, a, b) {
  const line = new T.Line3(new T.Vector3(...a), new T.Vector3(...b)), closest = new T.Vector3();
  const positions = geometry.getAttribute('position');
  let radius = 0;
  for (let i = 0; i < positions.count; i++) {
    const point = new T.Vector3().fromBufferAttribute(positions, i);
    line.closestPointToPoint(point, true, closest);
    radius = Math.max(radius, point.distanceTo(closest));
  }
  return {A: a, B: b, Radius: Math.ceil(radius * 1e6) / 1e6 + .000002};
}

/** 同一个几何变换同时应用到作用段，不另写一套形状坐标。 */
function transform(geometry, matrix) {
  geometry.applyMatrix4(matrix);
  geometry.userData.contacts = geometry.userData.contacts.map(c => ({
    A: new T.Vector3(...c.A).applyMatrix4(matrix).toArray().map(round),
    B: new T.Vector3(...c.B).applyMatrix4(matrix).toArray().map(round),
    Radius: c.Radius * matrix.getMaxScaleOnAxis()
  }));
  return geometry;
}

/** 小块翼膜保留四角真实高度，两层与侧壁封闭，不用悬空平面盖住指骨。 */
function membranePatch(points) {
  const positions = [], uv = [], triangles = [0, 1, 2, 0, 2, 3, 6, 5, 4, 7, 6, 4];
  for (const height of [.0125, -.0125]) for (const p of points) {
    positions.push(p.x, p.y, p.z + height); uv.push(p.x, p.y);
  }
  for (let i = 0; i < 4; i++) {
    const j = (i + 1) % 4;
    triangles.push(i, j, j + 4, i, j + 4, i + 4);
  }
  const g = new T.BufferGeometry();
  g.setAttribute('position', new T.Float32BufferAttribute(positions, 3));
  g.setAttribute('uv', new T.Float32BufferAttribute(uv, 2));
  g.setIndex(triangles);
  return g;
}

/** 合并制作资料，所有部件和出口仍可按稳定名称选择。 */
function publish(id, pieces, parameters) {
  const parts = pieces.map(([name, geometry, surface]) => part(name, geometry, surface));
  const contacts = pieces.flatMap(([name, geometry], index) => geometry.userData.contacts
    .map((c, segment) => ({Id: `${name}.${segment}`, Part: index, ...c})));
  return write(id, parts, [{Id: 'mount', Part: 0, Position: [0, 0, 0], Direction: [0, 0, 1]}], contacts,
    [], [], false, {Forward: [0, -1, 0], Up: [0, 0, 1]},
    structure('rigid', 'forms_organs.mjs', 'makeOrganForms', parameters,
      Object.fromEntries(parts.map(p => [p.Id, [`organ.${id}`, 'organ.contact']])),
      parts.slice(1).map(p => [parts[0].Id, p.Id, [0, 0, 0], false])));
}

/** 羽翼有双侧翼骨和独立扁厚飞羽；凶翼有指骨与真正封闭的翼膜。 */
function wing(membrane) {
  const pieces = [];
  for (const side of [-1, 1]) {
    const label = side < 0 ? 'left' : 'right';
    const points = [[0, 0, 0], [side * .35, -.04, .13], [side * .8, -.18, .18], [side * 1.2, .02, .1]];
    pieces.push([label + '_arm', taperedBody(points, .075, 14, 10), membrane ? 'ivory' : 'feather']);
    const count = membrane ? 4 : 9;
    for (let i = 0; i < count; i++) {
      const at = i / (count - 1), x = .4 + at * .8, y = -.1 + at * .08;
      const end = [side * (x + .28 + .18 * at), y + .35 + .25 * Math.sin(at * Math.PI), .05];
      const root = [side * x, y, .13];
      if (!membrane) {
        const feather = taperedBody([root, [side * (x + .12), y + .25, .12], end], .105, 12, 8,
          t => Math.pow(Math.sin(Math.PI * (.12 + t * .88)), .75));
        pieces.push([`${label}_feather_${i}`, transform(feather,
          new T.Matrix4().makeScale(1, 1, .28).setPosition(0, 0, .13 * .72)), 'feather']);
      } else {
        pieces.push([`${label}_finger_${i}`, taperedBody([root, end], .025, 8, 8), 'ivory']);
        if (i === 0) continue;
        const prior = pieces.filter(p => p[0] === `${label}_finger_${i - 1}`)[0][1];
        const previous = prior.userData.contacts.at(-1).B;
        const previousRoot = prior.userData.contacts[0].A;
        // 膜按纵向小片封闭；各片取自身顶点包络，避免一段作用填满整个翼间空隙。
        for (let strip = 0; strip < 4; strip++) {
          const v = (from, to, t) => new T.Vector3(...from).lerp(new T.Vector3(...to), t);
          const a = v(previousRoot, previous, strip / 4), b = v(root, end, strip / 4);
          const c = v(root, end, (strip + 1) / 4), d = v(previousRoot, previous, (strip + 1) / 4);
          const g = membranePatch([a, b, c, d]);
          g.userData.contacts = [bound(g, a.clone().add(d).multiplyScalar(.5).toArray(),
            b.clone().add(c).multiplyScalar(.5).toArray())];
          pieces.push([`${label}_membrane_${i}_${strip}`, g, 'membrane']);
        }
      }
    }
  }
  return publish(membrane ? 'wing_fierce' : 'wing_fupeng', pieces, {membrane: membrane ? 1 : 0, feathers: membrane ? 0 : 18});
}

/** 尾冠按已有的单尾、三尾、六尾分别发布；曲线有体积和收尖，各尾保留独立部件。 */
function tails(count) {
  const pieces = [];
  for (let i = 0; i < count; i++) {
    const angle = count === 1 ? 0 : (i / (count - 1) - .5) * 1.9;
    const path = [[0, 0, 0], [Math.sin(angle) * .16, -.04, .25],
      [Math.sin(angle) * .8 + .08, -.24, .9], [Math.sin(angle) * 1.12 + .12, -.42, 1.1 + Math.cos(angle) * .4]];
    pieces.push([`tail_${i}`, taperedBody(path, .23, 20, 12,
      t => (.22 + .78 * Math.pow(Math.sin(Math.PI * t), .65)) * Math.pow(1 - t, .35)), 'fur']);
  }
  return publish('tails' + count, pieces, {tails: count, spread: 1.9, rows: 20, sides: 12});
}

/** 背甲由有厚度的曲面甲片组成，根部在背顶，边缘沿背部弯下。 */
function shell() {
  const pieces = [];
  for (let row = -2; row <= 2; row++) for (let column = -2; column <= 2; column++) {
    const x = column * .31 + (Math.abs(row) % 2) * .155, y = row * .29;
    if (x * x / .55 + y * y / .9 > .72) continue;
    const shape = new T.Shape();
    for (let corner = 0; corner < 6; corner++) {
      const angle = corner / 6 * Math.PI * 2;
      const p = [x + Math.cos(angle) * .185, y + Math.sin(angle) * .185];
      if (corner === 0) shape.moveTo(...p); else shape.lineTo(...p);
    }
    shape.closePath();
    const g = new T.ExtrudeGeometry(shape, {depth: .085, bevelEnabled: true, bevelSize: .008, bevelThickness: .008, bevelSegments: 1});
    const positions = g.getAttribute('position');
    for (let i = 0; i < positions.count; i++) positions.setZ(i, positions.getZ(i) -
      .26 * (positions.getX(i) ** 2 / .55 + positions.getY(i) ** 2 / .9));
    const z = -.26 * (x * x / .55 + y * y / .9);
    g.userData.contacts = [bound(g, [x - .12, y, z + .04], [x + .12, y, z + .04])];
    pieces.push([`plate_${row + 2}_${column + 2}`, g, 'shell']);
  }
  pieces.unshift(...pieces.splice(pieces.findIndex(p => p[0] === 'plate_2_2'), 1));
  return publish('shell_black', pieces, {rows: 5, columns: 5, spacing: [.31, .29], thickness: .085});
}

/** 由统一入口生成所有已实现独立外观，未登记的爪牙仍不凭空补造。 */
export function makeOrganForms() {
  const horn = taperedBody([[0, 0, 0], [.02, -.04, .3], [.08, -.12, .6], [.02, -.25, .83], [-.08, -.3, .9]], .18);
  return [publish('horn_evilspotter', [['horn', horn, 'ivory']], {length: .9, radius: .18}),
    wing(false), wing(true), tails(1), tails(3), tails(6), shell()];
}
