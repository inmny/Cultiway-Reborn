import * as T from 'three';
import {part, structure, write} from './form_export.mjs';

// 横向接缝错开，山脊在中间最高；每块的棱柱与可见断面来自同一圈边界。
const outlines = [
  [[-2.42, .1], [-2.12, 0], [-1.16, 0], [-1.03, 1.54], [-1.85, 1.4], [-2.46, .64]],
  [[-1.13, 0], [.11, 0], [.34, 1.94], [.16, 2.19], [-.52, 2.02], [-1, 1.54]],
  [[.14, 0], [1.37, 0], [1.19, 1.45], [.37, 1.93]],
  [[1.4, 0], [2.27, 0], [2.48, .45], [2.31, 1.1], [1.68, 1.38], [1.22, 1.45]]
];
const depths = [.168, .215, .184, .151];

/** 切平的正面接上斜断层，覆盖形状的完整厚度留给触地和碎块落稳。 */
function plate(outline, depth, pivot) {
  const levels = [[-1, .83], [-.44, 1], [.42, 1], [1, .86]];
  const rings = levels.map(([y, size]) => outline.map(([x, z]) =>
    [pivot[0] + (x - pivot[0]) * size, y * depth, pivot[2] + (z - pivot[2]) * size]));
  const point = [], uv = [];
  function triangle(a, b, c, cut = false) {
    for (const v of [a, b, c]) {
      point.push(...v);
      uv.push(v[0] * .9 + (cut ? v[1] * 1.3 : 0), v[2] * .9 + v[0] * .12 + v[1] * 1.3);
    }
  }
  const last = rings.at(-1);
  for (let i = 1; i < outline.length - 1; i++) {
    triangle(rings[0][0], rings[0][i], rings[0][i + 1]);
    triangle(last[0], last[i + 1], last[i]);
  }
  for (let k = 1; k < rings.length; k++) {
    for (let i = 0; i < outline.length; i++) {
      const j = (i + 1) % outline.length;
      const a = rings[k - 1][i], b = rings[k - 1][j], c = rings[k][i], d = rings[k][j];
      triangle(a, d, b, true); triangle(a, c, d, true);
    }
  }
  const mesh = new T.BufferGeometry();
  mesh.setAttribute('position', new T.Float32BufferAttribute(point, 3));
  mesh.setAttribute('uv', new T.Float32BufferAttribute(uv, 2));
  return mesh;
}

/** 输出刚性整片及真实可拆接缝；底边挂点在抬升、转动时不改变。 */
export function makeBowingMountains() {
  const ids = outlines.map((_, i) => `stratum_${i}`);
  const pieces = outlines.map((outline, i) => {
    const pivot = [0, 0, 0];
    for (const [x, z] of outline) { pivot[0] += x / outline.length; pivot[2] += z / outline.length; }
    return part(ids[i], plate(outline, depths[i], pivot), 'stone_strata', pivot);
  });
  const anchors = [['left', [-2.42, 0, 0], 0], ['right', [2.27, 0, 0], 3], ['crest', [.16, 0, 2.19], 1]];
  const footprints = outlines.map((Points, i) => ({Id: ids[i], Part: i, A: [0, 0, 0],
    Footprint: {Points, HalfThickness: depths[i]}}));
  const joins = [-1.07, .25, 1.31].map((x, i) => [ids[i], ids[i + 1], [x, 0, .68]]);
  write('bowing_mountain_slab', pieces,
    anchors.map(([Id, Position, Part]) => ({Id, Position, Part, Direction: [0, 1, 0]})), footprints,
    [], [], false, {Forward: [0, 1, 0], Up: [0, 0, 1], HideBelowGround: true},
    structure('rigid', 'forms_bowing_mountains.mjs', 'makeBowingMountains', {outlines, depths},
      Object.fromEntries(ids.map(id => [id, ['terrain.slab', 'terrain.stratum']])), joins));
}
