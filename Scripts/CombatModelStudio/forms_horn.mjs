/** 雷角保留封闭截面、渐细弯曲和独立分叉，根和出口均为几何上的实际点。 */
import * as T from 'three';
import {part, write, structure, round} from './form_export.mjs';

/** 沿连续轴建立有厚度的角体，两端封口，尖端使用单个顶点。 */
function horn(points, radius, rows = 28, sides = 16) {
  const curve = new T.CatmullRomCurve3(points.map(p => new T.Vector3(...p)));
  const frames = curve.computeFrenetFrames(rows, false), positions = [], uv = [], indices = [];
  for (let row = 0; row < rows; row++) {
    const t = row / rows, centre = curve.getPointAt(t);
    const r = radius * Math.pow(1 - t, .72) * (1 + .045 * Math.cos(t * Math.PI * 20));
    for (let side = 0; side <= sides; side++) {
      const angle = side / sides * Math.PI * 2;
      const point = centre.clone().addScaledVector(frames.normals[row], Math.cos(angle) * r)
        .addScaledVector(frames.binormals[row], Math.sin(angle) * r * .86);
      positions.push(...point.toArray()); uv.push(side / sides, t);
    }
  }
  for (let row = 0; row < rows - 1; row++) for (let side = 0; side < sides; side++) {
    const a = row * (sides + 1) + side, b = a + sides + 1;
    indices.push(a, a + 1, b, a + 1, b + 1, b);
  }
  const tip = positions.length / 3;
  positions.push(...points.at(-1)); uv.push(.5, 1);
  const base = positions.length / 3;
  positions.push(...points[0]); uv.push(.5, 0);
  for (let side = 0; side < sides; side++) {
    const end = (rows - 1) * (sides + 1) + side;
    indices.push(end, end + 1, tip, side + 1, side, base);
  }
  // 每两圈合为一段，半径直接包住该段实际顶点；细分曲线与三维外观使用同一输入。
  const contacts = [];
  for (let first = 0; first < rows; first += 2) {
    const last = Math.min(rows, first + 2), a = curve.getPointAt(first / rows), b = curve.getPointAt(last / rows);
    const line = new T.Line3(a, b), closest = new T.Vector3();
    let bound = 0;
    for (let row = first; row <= last; row++) {
      if (row === rows) {
        const point = new T.Vector3(...points.at(-1));
        line.closestPointToPoint(point, true, closest);
        bound = Math.max(bound, point.distanceTo(closest));
      } else for (let side = 0; side <= sides; side++) {
        const offset = (row * (sides + 1) + side) * 3;
        const point = new T.Vector3(...positions.slice(offset, offset + 3).map(round));
        line.closestPointToPoint(point, true, closest);
        bound = Math.max(bound, point.distanceTo(closest));
      }
    }
    contacts.push({A: a.toArray().map(round), B: b.toArray().map(round), Radius: Math.ceil(bound * 1e6) / 1e6 + .000002});
  }
  const geometry = new T.BufferGeometry();
  geometry.setAttribute('position', new T.Float32BufferAttribute(positions, 3));
  geometry.setAttribute('uv', new T.Float32BufferAttribute(uv, 2));
  geometry.setIndex(indices); geometry.userData.smoothSeams = true;
  geometry.userData.contacts = contacts;
  return geometry;
}

/** 所有分叉都从主角内部接出；表面坐标沿角长，不使用光线代替角体。 */
export function makeHorn() {
  const stem = horn([[0, 0, 0], [.08, -.025, .27], [.03, -.045, .57], [-.045, -.025, .82], [0, 0, 1]], .16);
  const left = horn([[.035, -.025, .38], [-.17, -.015, .48], [-.30, .005, .65], [-.32, .015, .76]], .085, 18, 12);
  const right = horn([[.03, -.035, .56], [.20, -.015, .65], [.27, .015, .80], [.23, .035, .91]], .065, 16, 12);
  const shapes = [stem, left, right].flatMap((geometry, index) => geometry.userData.contacts.map((contact, segment) =>
    ({Id: ['crown', 'fork_left', 'fork_right'][index] + '.' + segment, Part: index, ...contact})));
  return write('horn_thunder', [part('crown', stem, 'horn'), part('fork_left', left, 'horn'), part('fork_right', right, 'horn')],
    [{Id: 'mount', Part: 0, Position: [0, 0, 0], Direction: [0, 0, 1]},
      {Id: 'cast', Part: 0, Position: [0, 0, 1], Direction: [0, 0, 1]}], shapes, [], [], false, null,
    structure('rigid','forms_horn.mjs','makeHorn',{length:1,radii:[.16,.085,.065],rows:[28,18,16],sides:[16,12,12]},
      {crown:['organ.horn'],fork_left:['organ.horn.branch'],fork_right:['organ.horn.branch']},
      [['crown','fork_left',[.035,-.025,.38]],['crown','fork_right',[.03,-.035,.56]]]));
}
