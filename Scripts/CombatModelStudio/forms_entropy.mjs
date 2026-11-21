/** 以有壁厚的错层断片表现解构，碰撞仍由制作资料中的接触体决定。 */
import * as T from 'three';
import {mergeGeometries} from 'three/addons/utils/BufferGeometryUtils.js';
import {motion, dissolve, part, skin, structure, write} from './form_export.mjs';

const facing = {Forward: [0, 1, 0], Up: [0, 0, 1]};

/** 沿破面折弯整块石片，内外壁和斜切端共用实际厚度。 */
function fold(x, y, z, radius, start, span, tilt, width, bend, depth) {
  const face = new T.Shape();
  const weights = [.08, .43, .68, .89, 1];
  const edge = [0, .13, -.05, .05, -.14];
  const floor = [.96, .79, .61, .17];
  const point = (t, r) => [Math.cos(start + span * t) * r, Math.sin(start + span * t) * r];
  weights.forEach((t, i) => {
    const p = point(t, radius * (1 + edge[i]));
    if (i === 0) face.moveTo(...p); else face.lineTo(...p);
  });
  floor.forEach((t, i) => face.lineTo(...point(t, radius * (1 - width + edge[i] * .6))));
  face.closePath();
  const bevel = radius * .035;
  const geometry = new T.ExtrudeGeometry(face, {depth: radius * depth, bevelEnabled: true,
    bevelThickness: bevel, bevelSize: bevel * .65, bevelSegments: 1, steps: 1, curveSegments: 1});
  const position = geometry.getAttribute('position');
  for (let i = 0; i < position.count; i++) {
    const a = position.getX(i), b = position.getY(i);
    const slant = a * Math.cos(start + span * .3) + b * Math.sin(start + span * .3);
    position.setXYZ(i, x + a, y + b, z + position.getZ(i) + tilt * a + bend * slant * Math.abs(slant) / radius);
  }
  return geometry;
}

function panel(id, items, surface, control) {
  const geometry = mergeGeometries(items);
  if (control !== undefined) skin(geometry, () => [[control, 1]]);
  return {...part(id, geometry, surface), UseMatterSurface: false};
}

function makeEntropy() {
  const lobes = [[0, 0, 1.25, 1.65], [-1.1, .4, .85, 1.1], [.95, -.45, 1.05, 1.3], [.35, 1.1, 1.6, 1.05]];
  // 四个旧控制只负责作用面，范围按原接触半径提供。
  const controls = lobes.map(([x, y, z, r], i) => {
    const point = [x, y, z], limit = r * .93;
    return {Id: `fold_${i}`, Position: point, Rotation: [0, 0, 0, 1],
      Minimum: point.map(value => value - limit), Maximum: point.map(value => value + limit)};
  });
  const parts = [], contacts = [];
  lobes.forEach(([x, y, z, r], i) => {
    const turn = i * 1.56;
    const slabs = [], cuts = [];
    for (let j = 0; j < 3; j++) {
      const start = turn + j * 2.1;
      slabs.push(fold(x, y, z + r * [-.43, .15, -.15][j], r * [1.03, .86, 1][j],
        start, [1.7, 1.2, 1.5][j], [.49, -.54, .23][j], [.48, .58, .36][j], [-.23, .28, -.38][j], .19));
      cuts.push(fold(x + r * .1, y - r * .1, z + r * [-.38, .26, -.12][j], r * [.65, .58, .7][j],
        start + .25, [1.17, .70, 1.2][j], [.53, -.57, .17][j], .4, -.12, .15));
    }
    const visual = controls.length;
    controls.push({Id: `skin_${i}`, Position: [x, y, z], Rotation: [0, 0, 0, 1]});
    parts.push(panel(`fold_${i}`, slabs, 'entropy_obsidian', visual));
    parts.push(panel(`depth_${i}`, cuts, 'entropy_section', visual));
    contacts.push({Id: `chaos_${i}`, Part: i * 2, A: [x, y, z], B: [x, y, z], Radius: r * .93,
      AControl: controls[i].Id, BControl: controls[i].Id});
  });
  const run = motion('run', 3.2, true, controls, 48, (index, p, c) => {
    const i = index % lobes.length, t = p * Math.PI * 2;
    const position = [c.Position[0] + .03 * Math.sin(t + i), c.Position[1] + .03 * Math.cos(t + i),
      c.Position[2] + .04 * Math.sin(t + i * 1.4)];
    if (index < lobes.length) return {position};
    const slip = Math.pow(.5 + .5 * Math.sin(t - 1.7 * i), 3);
    position[0] += .18 * Math.cos(i * 1.56) * slip;
    position[1] += .18 * Math.sin(i * 1.56) * slip;
    position[2] += .29 * slip;
    const rotation = new T.Quaternion().setFromEuler(new T.Euler(.11 * slip, -.13 * slip, .14 * Math.sin(t - 1.7 * i)));
    return {position, rotation: rotation.toArray()};
  });
  const release = dissolve(controls, (i, p, c) => ({
    position: [c.Position[0]*(1+.18*p), c.Position[1]*(1+.18*p), c.Position[2]-.31*p],
    rotation: new T.Quaternion().setFromEuler(new T.Euler(p*.17*(i%2?1:-1), p*.11, 0)).toArray()
  }));
  write('entropy_field', parts, [], contacts, controls, [run, release], false, facing,
    structure('volume', 'forms_entropy.mjs', 'makeEntropy', {lobes},
      Object.fromEntries(parts.map(p => [p.Id, ['entropy.volume']])), []));
}

/** 交锋用开口冲面和有明确连接的青铜构件，能按原几何拆下其中一件。 */
function makeClashForms() {
  const wedge = [
    panel('mantle', [fold(0, .13, .77, 1.33, -.15, 1.4, .18, .54, .25, .22),
      fold(0, -.33, .64, 1.12, 1.37, 1.61, -.33, .57, -.19, .25)], 'entropy_obsidian'),
    panel('exposure', [fold(0, .37, .58, 1.12, .23, 2.39, .15, .45, -.18, .14)], 'entropy_section')
  ];
  const wedgeContact = {Id: 'sweeping_face', Part: 0, A: [-.65, .48, .65], B: [.65, .48, .65], Radius: .51};
  write('entropy_wedge', wedge, [], [wedgeContact], [], [], false, facing,
    structure('rigid', 'forms_entropy.mjs', 'makeClashForms', {width: 2.5},
      {mantle: ['entropy.surface'], exposure: ['erosion.mark']}, [['mantle', 'exposure', [0, .6, .8]]]));
  const ward = [
    panel('seat', [fold(0, 0, .32, .94, .19, 2.72, .08, .76, .05, .48)], 'bronze'),
    panel('cheek', [fold(0, 0, .73, 1.06, .08, 1.34, .21, .70, .21, .43)], 'guard_enamel'),
    panel('return', [fold(0, 0, .83, .92, 1.6, 1.43, -.18, .63, .29, .31)], 'guard_enamel')
  ];
  const wardContact = {Id: 'bearing', Part: 0, A: [-.4, .33, .62], B: [.4, .33, .62], Radius: .42};
  write('entropy_guard', ward, [], [wardContact], [], [], false, facing,
    structure('rigid', 'forms_entropy.mjs', 'makeClashForms', {width: 2.1},
      {seat: ['shield.face'], cheek: ['structure.edge'], return: ['structure.edge']},
      [['seat', 'cheek', [.34, .35, .66]], ['seat', 'return', [-.29, .32, .66]]]));
}

export function makeEntropyForms() { makeEntropy(); makeClashForms(); }
