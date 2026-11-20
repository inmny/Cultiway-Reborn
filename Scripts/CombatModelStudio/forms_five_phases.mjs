/** 五相的独立实体。水相继续使用共用浅层水格，由真正的水量和地形画出前沿。 */
import * as T from 'three';
import {mergeGeometries} from 'three/addons/utils/BufferGeometryUtils.js';
import {motion, part, structure, write} from './form_export.mjs';

const source = 'forms_five_phases.mjs';
const facing = {Forward: [0, 1, 0], Up: [0, 0, 1]};
const shell = (id, meshes, surface, control = null) => ({
  ...part(id, mergeGeometries(meshes), surface, [0, 0, 0], control), UseMatterSurface: false
});

function slab(points, depth, bevel) {
  const shape = new T.Shape();
  shape.moveTo(...points[0]);
  for (const point of points.slice(1)) shape.lineTo(...point);
  shape.closePath();
  return new T.ExtrudeGeometry(shape, {depth, bevelEnabled: true, bevelSegments: 1,
    bevelThickness: bevel, bevelSize: bevel * .68, steps: 1, curveSegments: 1});
}

function capsule(Id, Part, A, B, Radius) { return {Id, Part, A, B, Radius}; }

/** 下层铺开的踏石托住厚岩板，多处断面遮挡来向。 */
function makeMantle() {
  const cols = [-1.04, 0, 1.04];
  const lower = cols.map((x, i) => slab([[-.56,-.68],[.49,-.56],[.57,.33],[.30,.63],[-.55,.46]], .54, .075)
    .rotateY((i - 1) * .12).translate(x, .07 + .16 * (i % 2), .19));
  const crowns = cols.map((x, i) => slab([[-.46,-.53],[-.2,-.65],[.52,-.35],[.42,.53],[-.36,.36]], 1.07 + (i % 2) * .25, .085)
    .rotateY((1 - i) * .19).rotateX(.19).translate(x, .04, .59));
  const lime = cols.map((x, i) => slab([[-.54,-.20],[.49,-.22],[.50,.085],[.24,.27],[-.46,.15]], .16, .055)
    .rotateY((i - 1) * .12).translate(x, .46, .63 + .1 * (i % 2)));
  const parts = [shell('footing', lower, 'stone_strata'), shell('crown', crowns, 'five_shale'), shell('seams', lime, 'five_silt')];
  const contacts = [.48, 1.1, 1.62].map((z, i) => capsule(`bearing_${i}`, i === 0 ? 0 : 1, [-1.25,.26,z], [1.25,.26,z], .52));
  write('five_phase_mantle', parts, [], contacts, [], [], false, facing,
    structure('rigid', source, 'makeMantle', {cols}, {footing: ['structure.base'], crown: ['shield.face'], seams: ['surface.mark']},
      [['footing','crown',[0,0,.62]],['footing','seams',[0,.4,.6]]]));
}

function bough(path, base, tip) {
  const pieces = [];
  for (let i = 1; i < path.length; i++) {
    const a = new T.Vector3(...path[i-1]), b = new T.Vector3(...path[i]), dir = b.clone().sub(a);
    const width = t => T.MathUtils.lerp(base, tip, t / (path.length - 1));
    const form = new T.CylinderGeometry(width(i), width(i-1), dir.length() + .08, 7, 1);
    form.applyQuaternion(new T.Quaternion().setFromUnitVectors(new T.Vector3(0,1,0), dir.normalize()));
    pieces.push(form.translate(...a.lerp(b,.5).toArray()).toNonIndexed());
  }
  return pieces;
}

/** 两肢先从地表伸出，随后聚出有宽面的木质叶冠。 */
function makeRootFork() {
  const controls = [], parts = [], contacts = [], connections = [];
  for (let side = 0; side < 2; side++) {
    const sign = side * 2 - 1, id = `shoot_${side}`, tip = [sign * 1.0, 1.15 + side * .34, 1.34];
    const path = [[sign * .26,-.64,.12],[sign * .49,-.1,.64],[sign * .87,.59,1.14],tip];
    const spurs = [...bough(path,.32,.13), ...bough([path[2],[sign * 1.4,.95,1.34],[sign * 1.51,1.43,1.65]],.17,.06)];
    const leaves = Array.from({length: 5}, (_, i) => {
      const mesh = slab([[0,-.40],[.24,-.07],[.27,.32],[.06,.69],[-.23,.39],[-.24,.03]], .075,.023);
      return mesh.rotateZ((i-2)*.48).rotateY(sign * (.18+i*.14))
        .translate(tip[0]+sign*(i-2)*.12,tip[1]-.11+(i%2)*.13,tip[2]-.09+(i%3)*.14);
    });
    controls.push({Id: id, Position: path[0], Rotation: [0,0,0,1]});
    parts.push(shell(id, spurs, 'five_root', id));
    parts.push(shell(`leaves_${side}`, leaves, 'five_crown', id));
    connections.push([id, `leaves_${side}`, tip]);
    contacts.push({...capsule(`snare_${side}`, side * 2, path[1], tip, .48), AControl: id, BControl: id});
  }
  const rise = motion('rise', .66, false, controls, 18, (i,p,c) => {
    const t = T.MathUtils.smoothstep(p, .03 + i*.09, .77 + i*.12);
    const q = new T.Quaternion().setFromEuler(new T.Euler(-.84*(1-t),0,(i-.5)*.39*(1-t)));
    return {position:[c.Position[0],c.Position[1],c.Position[2]-.12*(1-t)], rotation:q.toArray()};
  });
  write('five_phase_roots', parts, [], contacts, controls, [rise], false, facing,
    structure('articulated', source, 'makeRootFork', {spread:2.9},
      Object.fromEntries(parts.map((p,i)=>[p.Id,[i%2 ? 'leaf.crown' : 'wood.branch']])), connections));
}

/** 三束宽焰从底层热胆向前翻，不以长线充当热源。 */
function makeKiln() {
  const lobes = [], cores = [], controls = [], parts = [], contacts = [];
  const shape = [[-.51,-.46],[.36,-.45],[.54,-.15],[.43,.44],[.13,1.01],[-.015,.64],[-.38,.50],[-.54,.14]];
  for (let i = 0; i < 3; i++) {
    const x = (i-1)*.66, y = (i%2)*.27, id = `jet_${i}`;
    controls.push({Id:id, Position:[x,y,.16], Rotation:[0,0,0,1]});
    lobes[i] = slab(shape, .48, .051).rotateX(.84 + i*.14).rotateZ((i-1)*-.38).translate(x,y,.26);
    cores[i] = slab(shape.map(([u,v])=>[u*.62,v*.58]), .34, .03).rotateX(.79).translate(x,y+.02,.23);
    parts.push(shell(id, [lobes[i]], 'solar_gold', id), shell(`coal_${i}`, [cores[i]], 'solar_kernel', id));
    contacts.push({...capsule(`heat_${i}`, i*2,[x,y-.1,.48],[x+(i-1)*.13,y+.54,.98],.49), AControl:id,BControl:id});
  }
  const roll = motion('roll',.74,true,controls,20,(i,p,c)=>{
    const t = p*Math.PI*2 + i*1.75;
    return {position:[c.Position[0],c.Position[1]+.09*Math.sin(t),c.Position[2]+.16*Math.sin(t)],
      rotation:new T.Quaternion().setFromEuler(new T.Euler(.23*Math.cos(t),.19*Math.sin(t),0)).toArray()};
  });
  write('five_phase_kiln',parts,[],contacts,controls,[roll],false,facing,
    structure('articulated',source,'makeKiln',{jets:3},Object.fromEntries(parts.map(p=>[p.Id,['fire.body']])),[]));
}

/** 沉重的锻压块托起横切面，短促金属主体和飞剑剪影不同。 */
function makePress() {
  const jaw = slab([[-1.34,-.48],[1.32,-.48],[1.47,.12],[.72,1.05],[.24,1.27],[-.23,1.04],[-.79,1.32],[-1.45,.28]], .27,.052)
    .rotateX(-.12).translate(0,0,.82);
  const bevel = slab([[-1.42,.18],[-.78,1.32],[-.23,1.07],[.24,1.27],[1.45,.18],[.62,.54],[.17,.73],[-.71,.68]], .12,.036)
    .rotateX(-.12).translate(0,.08,.87);
  const mounts = [-.97,.06,.87].map((x,i)=>slab([[-.32,-.43],[.42,-.41],[.46,.26],[-.28,.56]], .55 + .15*(i%2),.06)
    .rotateX(-.12).translate(x,-.23,.15));
  const parts=[shell('jaw',[jaw],'blade_steel'),shell('edge',[bevel],'silver'),shell('mounts',mounts,'bronze')];
  const contacts = [capsule('cut',0,[-.94,.72,.89],[.94,.72,.89],.44)];
  write('five_phase_press',parts,[],contacts,[],[],false,facing,
    structure('rigid',source,'makePress',{width:2.94},{jaw:['blade.body'],edge:['blade.edge'],mounts:['structure.base']},
      [['jaw','edge',[0,.70,.88]],['jaw','mounts',[0,-.1,.74]]]));
}

export function makeFivePhases() { makeMantle(); makeRootFork(); makeKiln(); makePress(); }
