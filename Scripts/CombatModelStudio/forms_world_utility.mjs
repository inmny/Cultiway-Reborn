/** 三种地块结果各用独立模型，幅宽限制在单个实际生效的地块内。 */
import * as T from 'three';
import {part, structure, surfaceData, write} from './form_export.mjs';

function volume(id, geometry, material) {
  const result = part(id, geometry, material);
  result.UseMatterSurface = false;
  for (let i = 0; i < result.Triangles.length; i += 3)
    [result.Triangles[i], result.Triangles[i + 2]] = [result.Triangles[i + 2], result.Triangles[i]];
  return result;
}

function publish(id, parts, parameters) {
  write(id, parts, [], [], [], [], false, {Forward:[0,1,0], Up:[0,0,1], HideBelowGround:true},
    structure('volume', 'forms_world_utility.mjs', 'makeWorldUtility', parameters,
      Object.fromEntries(parts.map(mesh => [mesh.Id, ['ground.surface']])), []));
}

/** 三株不等高的新枝从两片低芽中长出，嫩叶由实体叶面受光。 */
function growth() {
  const parts = [];
  for (let i = 0; i < 3; i++) {
    const angle = i * 2.4, d = new T.Vector3(Math.cos(angle), Math.sin(angle), 0);
    const start = d.clone().multiplyScalar(.07).setZ(.03);
    const crown = d.clone().multiplyScalar(.20).setZ(.45 + .11 * (i % 2));
    const middle = start.clone().lerp(crown, .5).addScaledVector(d, -.045);
    const branch = new T.QuadraticBezierCurve3(start, middle, crown);
    parts.push(volume(`shoot_${i}`, new T.TubeGeometry(branch, 10, .026, 7, false), 'living_bark'));

    for (let j = 0; j < 2; j++) {
      const turn = angle + (j === 0 ? -.7 : .8);
      const where = branch.getPoint(.65 + .29 * j);
      const leaf = new T.SphereGeometry(1, 14, 9).scale(.17 + .03 * j, .068, .029);
      leaf.translate(.075, 0, 0).rotateY(-.29).rotateZ(turn).translate(...where.toArray());
      parts.push(volume(`leaf_${i}_${j}`, leaf, j === 0 ? 'living_leaf' : 'living_sap'));
    }
  }
  publish('utility_young_growth', parts, {shoots:3, width:.83, height:.64});
}

/** 真正成云时，几片厚水膜环着一个饱满水核聚拢。 */
function condensate() {
  const parts = [];
  const core = new T.SphereGeometry(1, 24, 16).scale(.25,.20,.31).translate(0,0,.43);
  parts.push(volume('heart', surfaceData(core, .49, p => .22 * T.MathUtils.smoothstep(p.z,.5,.74)), 'water'));
  for (let i = 0; i < 4; i++) {
    const a = i * Math.PI * .5 + .22;
    const vapour = new T.SphereGeometry(1, 18, 12).scale(.095,.19,.105)
      .rotateX(.43 * (i % 2 === 0 ? 1 : -1)).translate(.31, 0, .29 + .08 * (i % 3)).rotateZ(a);
    parts.push(volume(`condensate_${i}`, vapour, 'purification_pearl'));
  }
  publish('utility_rain_condensate', parts, {lobes:4, width:.84, height:.76});
}

/** 小麦成熟时三根弯茎托起不同倾斜的饱满麦穗。 */
function grain() {
  const parts = [];
  for (let i = 0; i < 3; i++) {
    const angle = .2 + i * 2.37, direction = new T.Vector3(Math.cos(angle), Math.sin(angle), 0);
    const base = direction.clone().multiplyScalar(.065);
    const tip = direction.clone().multiplyScalar(.24).setZ(.53 + .10 * i);
    const line = new T.QuadraticBezierCurve3(base, base.clone().lerp(tip,.49).setZ(tip.z * .65), tip);
    parts.push(volume(`stem_${i}`, new T.TubeGeometry(line, 12, .019, 7, false), 'living_sap'));
    const side = new T.Vector3(-direction.y, direction.x, 0);
    for (let row = 0; row < 3; row++) {
      const center = tip.clone().addScaledVector(direction, (row-2) * .021).setZ(tip.z + row * .079);
      for (const sign of [-1,1]) {
        const position = center.clone().addScaledVector(side, sign * (.05 - row * .008));
        const seed = new T.SphereGeometry(1, 10, 7).scale(.039, .032, .065).rotateY(.30 * sign)
          .rotateZ(angle).translate(...position.toArray());
        parts.push(volume(`grain_${i}_${row}_${sign > 0 ? 'right' : 'left'}`, seed, row === 2 ? 'purification_pearl' : 'haste_gold'));
      }
    }
  }
  publish('utility_ripe_grain', parts, {stems:3, width:.62, height:.96});
}

export function makeWorldUtility() {
  growth();
  condensate();
  grain();
}
