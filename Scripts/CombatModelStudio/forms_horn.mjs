/** 雷角保留封闭截面、渐细弯曲和独立分叉，根和出口均为几何上的实际点。 */
import * as T from 'three';
import {part, write} from './form_export.mjs';

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
  const geometry = new T.BufferGeometry();
  geometry.setAttribute('position', new T.Float32BufferAttribute(positions, 3));
  geometry.setAttribute('uv', new T.Float32BufferAttribute(uv, 2));
  geometry.setIndex(indices); geometry.userData.smoothSeams = true;
  return geometry;
}

/** 所有分叉都从主角内部接出；表面坐标沿角长，不使用光线代替角体。 */
export function makeHorn() {
  const stem = horn([[0, 0, 0], [.08, -.025, .27], [.03, -.045, .57], [-.045, -.025, .82], [0, 0, 1]], .16);
  const left = horn([[.035, -.025, .38], [-.17, -.015, .48], [-.30, .005, .65], [-.32, .015, .76]], .085, 18, 12);
  const right = horn([[.03, -.035, .56], [.20, -.015, .65], [.27, .015, .80], [.23, .035, .91]], .065, 16, 12);
  write('horn_thunder', [part('crown', stem, 'horn'), part('fork_left', left, 'horn'), part('fork_right', right, 'horn')],
    [{Id: 'mount', Part: 0, Position: [0, 0, 0], Direction: [0, 0, 1]},
      {Id: 'cast', Part: 0, Position: [0, 0, 1], Direction: [0, 0, 1]}], []);
}
