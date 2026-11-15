/** 原地显现裂隙：厚实裂唇、连续暗腔以及共同控制的张开和闭合。 */
import * as T from 'three';
import {part, write, structure, motion, skin} from './form_export.mjs';

/** 截面逐段偏移，形成非椭圆、不对称的真实裂口。 */
const sections = [[.025, -.02, .04, .03], [.18, -.025, .22, .14], [.55, .035, .41, .27],
  [1.10, -.055, .62, .31], [1.85, .045, .48, .28], [2.4, -.04, .32, .21],
  [2.96, .025, .10, .07], [3.2, 0, .002, .003]];

/** 两侧权重随横坐标变化，整个裂口共同张开；维持期间不漂移实际接触面。 */
function bind(geometry) {
  return skin(geometry, p => {
    const right = T.MathUtils.clamp(.5 + p.x / 1.6, 0, 1);
    return [[0, 1 - right], [1, right]];
  });
}

/** 各截面组成封闭实体，法向与表面流动沿同一连续材料坐标保存。 */
function loft(rows, sides) {
  const positions = [], uv = [], indices = [];
  rows.forEach(([z, x, rx, ry]) => {
    for (let i = 0; i <= sides; i++) {
      const a = i / sides * Math.PI * 2;
      positions.push(x + rx * Math.cos(a), ry * Math.sin(a), z);
      uv.push(i / sides, z / 3.2);
    }
  });
  for (let row = 0; row < rows.length - 1; row++) for (let i = 0; i < sides; i++) {
    const a = row * (sides + 1) + i, b = a + sides + 1;
    indices.push(a, a + 1, b + 1, a, b + 1, b);
  }
  for (const [row, reverse] of [[0, true], [rows.length - 1, false]]) {
    const centre = positions.length / 3, [z, x] = rows[row]; positions.push(x, 0, z); uv.push(.5, row ? 1 : 0);
    for (let i = 0; i < sides; i++) {
      const a = row * (sides + 1) + i;
      indices.push(centre, reverse ? a + 1 : a, reverse ? a : a + 1);
    }
  }
  const geometry = new T.BufferGeometry();
  geometry.setAttribute('position', new T.Float32BufferAttribute(positions, 3));
  geometry.setAttribute('uv', new T.Float32BufferAttribute(uv, 2)); geometry.setIndex(indices);
  return bind(geometry);
}

/** 独立的暗腔和两侧宽裂唇，以普通骨架位置关键帧实现开合。 */
export function makeRift() {
  const controls = [{Id: 'left', Position: [-.8, 0, 0], Rotation: [0, 0, 0, 1]},
    {Id: 'right', Position: [.8, 0, 0], Rotation: [0, 0, 0, 1]},
    {Id: 'axis', Position: [0, 0, 0], Rotation: [0, 0, 0, 1], Minimum: [-.32, -.32, 0], Maximum: [.32, .32, 3.2]}];
  const parts = [part('cavity', loft(sections, 32), 'rift_core')];
  for (const side of [-1, 1]) {
    const lip = sections.map(([z, x, rx, ry], row) => [z, x + side * rx * .96,
      (.035 + rx * .12) * (row === sections.length - 1 ? .08 : 1), ry * .72 + .012]);
    parts.push(part(side < 0 ? 'left_lip' : 'right_lip', loft(lip, 14), 'rift_lip'));
  }
  // 各面保留自己的内部或裂唇表面，不被共同混沌伤害材料统一覆盖。
  parts.forEach(p => { p.UseMatterSurface = false; });
  const contacts = [];
  for (let i = 1; i < sections.length - 1; i++) {
    const [z, x, , depth] = sections[i], [nextZ, nextX, , nextDepth] = sections[i + 1];
    const pieces = Math.ceil((nextZ - z) / .12);
    for (let j = 0; j < pieces; j++) {
      const a = j / pieces, b = (j + 1) / pieces;
      const at = t => [T.MathUtils.lerp(x, nextX, t), 0, T.MathUtils.lerp(z, nextZ, t)];
      contacts.push({Id: `core_${i}_${j}`, Part: 0, A: at(a), B: at(b),
        Radius: Math.max(.001, T.MathUtils.lerp(depth, nextDepth, b) * .88), AControl: 'axis', BControl: 'axis'});
    }
  }
  const duration = 1;
  const open = motion('open', duration, false, controls, 48, (index, phase, control) => {
    if (index === 2) return {};
    const amount = .012 + .988 * T.MathUtils.smoothstep(phase, 0, .14) * (1 - T.MathUtils.smoothstep(phase, .74, 1));
    return {position: [control.Position[0] * amount, 0, 0]};
  }, [{Id: 'closed', At: 0}, {Id: 'opened', At: .14}, {Id: 'closing', At: .74}, {Id: 'closed_end', At: 1}]);
  write('rift_opening', parts, [{Id: 'base', Part: 0, Position: [0, 0, 0], Direction: [0, 0, 1], Control: 'axis'},
    {Id: 'top', Part: 0, Position: [0, 0, 3.2], Direction: [0, 0, 1], Control: 'axis'}], contacts, controls, [open], false,
    {Forward: [0, 1, 0], Up: [0, 0, 1]},
    structure('articulated', 'forms_rift.mjs', 'makeRift', {sections, duration, sides: 32},
      {cavity: ['space.opening'], left_lip: ['space.boundary'], right_lip: ['space.boundary']}, []));
}
