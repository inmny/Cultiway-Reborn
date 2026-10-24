/** 浪背、厚水层及向前翻下的卷唇；共用曲面制作和已有连续骨架执行。 */
import * as T from 'three';
import {mergeGeometries} from 'three/addons/utils/BufferGeometryUtils.js';
import {part, write, skin, structure} from './form_export.mjs';
import {curveInfluences, waterCurve, thickSheet, curveMotion} from './form_curve.mjs';

const names = ['foot','lower_back','back','shoulder','crest','lip','fall'];
const initial = [[-4,.86],[-2.2,.78],[-1,1.15],[-.4,1.8],[0,2.2],[.7,1.9],[1,1.2]];
const raised = [[-4,.86],[-2.1,.9],[-.75,1.6],[-.25,3.65],[.45,5.2],[1.9,5.3],[2.85,4.3]];
const falling = [[-4,.86],[-1.8,.85],[-.3,1.5],[.7,3.2],[2.5,3.6],[4,2.5],[4.3,.5]];
const spread = [[-4,.86],[-1.5,.78],[.6,.65],[2.6,1.05],[4.4,.9],[5.5,.45],[6.3,.23]];

/** 不硬切姿态；浪背先抬起，卷唇前倾后落到低处。 */
function phaseCurve(phase) {
  const sequence = [initial,raised,falling,spread], times = [0,.35,.72,1];
  let interval = times.findIndex((time, i) => i < times.length - 1 && phase <= times[i + 1]);
  if (interval < 0) interval = 2;
  const blend = T.MathUtils.smoothstep(phase, times[interval], times[interval + 1]);
  return waterCurve(sequence[interval].map((point, i) => point.map((v, axis) => T.MathUtils.lerp(v, sequence[interval+1][i][axis], blend))));
}

export function makeWave() {
  const curve = waterCurve(raised);
  const controls = names.map((Id, i) => {
    const position = curve.getPoint(i / (names.length - 1));
    if (i) position.sub(curve.getPoint((i - 1) / (names.length - 1)));
    return {Id, Parent: i ? names[i-1] : null, Position: position.toArray(), Rotation: [0,0,0,1]};
  });
  const width = along => 6.8 * (1 - .11 * along);
  const thickness = along => .12 + .72 * Math.pow(1 - along, 1.4);
  const sheet = thickSheet(curve, {width, thickness, controls: names.length,
    foam: (along, across, face) => T.MathUtils.smoothstep(along, .72, .96) * (face ? .42 : .82) * (.75+.25*Math.sin(across*11+along*13))});

  // 少量真实泡沫体积位于卷唇表面，随同一曲线控制，不用烟团盖住整片水。
  const foam = [];
  for (let i = 0; i < 91; i++) {
    const random = value => T.MathUtils.euclideanModulo(Math.sin(value*127.1)*43758.5453,1);
    const along = .84 + random(i+1) * .15, point = curve.getPoint(along), tangent = curve.getTangent(along);
    const normal = new T.Vector3(0,-tangent.z,tangent.y).normalize();
    point.x = (i / 90 * 2 - 1) * width(along) * .96;
    const edge = .15+.85*Math.pow(1-Math.pow(point.x/width(along),2),.35);
    point.addScaledVector(normal, thickness(along)*edge+.015);
    point.z += Math.sin(point.x*.8+along*4)*.11*T.MathUtils.smoothstep(along,.35,.85);
    const geometry = new T.SphereGeometry(.055+random(i+19)*.09, 8, 6).scale(1.7,.65,1).translate(...point.toArray());
    foam.push(skin(geometry, () => curveInfluences(along,names.length)));
  }
  const parts = [part('water_body',sheet,'water'),part('aerated_lip',mergeGeometries(foam),'foam')];
  const influences = along => curveInfluences(along,names.length).map(([control,Weight]) => ({Control:names[control],Weight}));
  const contacts = [];
  for (let row = 0; row <= 28; row++) {
    const along = row / 28, point = curve.getPoint(along);
    contacts.push({Id:'water_'+row,Part:0,A:[-width(along)+.2,point.y,point.z],B:[width(along)-.2,point.y,point.z],
      Radius:Math.max(.23,thickness(along)),AInfluences:influences(along),BInfluences:influences(along)});
  }
  const anchors = [.25,.5,.75,1].map((along,i) => ({Id:'surface_'+i,Part:0,Position:curve.getPoint(along).toArray(),
    Direction:curve.getTangent(along).toArray(),Influences:influences(along)}));
  const markers=[{Id:'rise',At:0},{Id:'crest',At:.98},{Id:'fall',At:2.016},{Id:'spread',At:2.8}];
  write('water_wave',parts,anchors,contacts,controls,[curveMotion('break',2.8,controls,curve,phaseCurve,64,markers),
    curveMotion('recede',1.8,controls,curve,phase => phaseCurve(1-phase),64,
      [{Id:'rise',At:0},{Id:'crest',At:.65},{Id:'fall',At:1.2},{Id:'spread',At:1.8}])],false,{Forward:[0,1,0],Up:[0,0,1]},
    structure('surface','forms_wave.mjs','makeWave',{initial,raised,falling,spread,controls:names.length},
      {water_body:['fluid.body'],aerated_lip:['fluid.foam','fluid.crest']},[['water_body','aerated_lip',curve.getPoint(.85).toArray()]]));
}
