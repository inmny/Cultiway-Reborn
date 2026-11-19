import * as T from 'three';
import {part, structure, write} from './form_export.mjs';

const cut = (w, bottom, top, bevel) => [
  [-w + bevel, bottom], [w - bevel, bottom], [w, bottom + bevel], [w, top - bevel],
  [w - bevel, top], [-w + bevel, top], [-w, top - bevel], [-w, bottom + bevel],
];
const tilt = z => (z - 1.71) * (1.06 / 1.94);

/** 多个封闭截面压出厚门柱和内沿；中央留空，供游戏填入真实对端地貌。 */
function shell(profiles) {
  const vertices = [], uv = [];
  const point = (profile, index) => {
    const [w, bottom, top, bevel, depth] = profile;
    const [x, z] = cut(w, bottom, top, bevel)[index];
    vertices.push(x, tilt(z) + depth, z);
    uv.push(index / 8, z / 3.2);
  };
  for (let r = 0; r < profiles.length; r++) {
    const next = (r + 1) % profiles.length;
    for (let i = 0; i < 8; i++) {
      const j = (i + 1) % 8;
      for (const [p, k] of [[r, i], [next, j], [r, j], [r, i], [next, i], [next, j]]) point(profiles[p], k);
    }
  }
  const model = new T.BufferGeometry();
  model.setAttribute('position', new T.Float32BufferAttribute(vertices, 3));
  model.setAttribute('uv', new T.Float32BufferAttribute(uv, 2));
  return model;
}

export function makeSpatialGate() {
  const stone = [[1.29, .32, 3.03, .19, -.30], [1.46, .22, 3.17, .25, -.14],
    [1.46, .22, 3.17, .25, .14], [1.29, .32, 3.03, .19, .30], [1.10, .63, 2.78, .075, .30],
    [1.10, .63, 2.78, .075, -.30]];
  const lip = [[1.18, .55, 2.88, .09, -.272], [1.18, .55, 2.88, .09, .272],
    [1.08, .65, 2.77, .055, .22], [1.05, .69, 2.73, .035, .12],
    [1.05, .69, 2.73, .035, -.12], [1.08, .65, 2.77, .055, -.22]];
  const box = (x, w, h, y, depth, z) => new T.BoxGeometry(w, depth, h).translate(x, y, z);
  const parts = [part('stone', shell(stone), 'space_gate_stone'), part('inset', shell(lip), 'space_gate_inlay'),
    part('step', box(0, 2.96, .24, -.53, 1.34, .12), 'space_gate_stone')];
  for (const sign of [-1, 1]) parts.push(part(sign < 0 ? 'left_socket' : 'right_socket',
    box(sign * 1.24, .36, .47, -.49, .87, .29), 'space_gate_stone'));
  for (const item of parts) item.UseMatterSurface = false;
  write('space_fold_gate', parts,
    [{Id: 'threshold', Part: 2, Position: [0, 0, 0], Direction: [0, 1, 0]}],
    [{Id: 'threshold', Part: 2, A: [-.78, 0, .13], B: [.78, 0, .13], Radius: .33}], [], [], false,
    {Forward: [0, 1, 0], Up: [0, 0, 1]},
    structure('articulated', 'forms_spatial_gate.mjs', 'makeSpatialGate', {stone, lip},
      {stone: ['space.boundary'], inset: ['space.opening'], step: ['space.threshold'],
        left_socket: ['space.support'], right_socket: ['space.support']},
      [['stone', 'inset', [0, 0, 1.72], false], ['stone', 'step', [0, -.53, .25], false],
        ['stone', 'left_socket', [-1.24, -.49, .29], false], ['stone', 'right_socket', [1.24, -.49, .29], false]]));
}
