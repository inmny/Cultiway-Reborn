/** 导出可寻址部位和完整表面坐标，供 Unity 离线构建及共同形体描述使用。 */
import * as T from 'three';
import {part, write, structure} from './form_export.mjs';
import {makeWhale} from './forms_whale.mjs';
import {makeWave} from './forms_wave.mjs';
import {makeFlow} from './forms_flow.mjs';
import {makeHorn} from './forms_horn.mjs';
import {makeOrganForms} from './forms_organs.mjs';
import {makeRigidForms} from './forms_rigid.mjs';
import {makeBodyForms} from './forms_bodies.mjs';
import {makeGrowingForms} from './forms_growth.mjs';
import {makeFractureSlab} from './forms_changes.mjs';
import {makeWeaponForms} from './forms_weapons.mjs';
import {makeRay} from './forms_ray.mjs';
import {makeEarthSpikes} from './forms_earth.mjs';

makeHorn();
makeOrganForms();
makeRigidForms();
makeBodyForms();
makeGrowingForms();
makeFractureSlab();
makeWeaponForms();
makeRay();
makeEarthSpikes();

// 剑刃由脊、刃面、窄边和逐段收尖构成；各面保留自己的表面方向。
const positions = [], uv = [], triangles = [];
const levels = [[0, .135, .035], [.13, .155, .038], [1.7, .13, .032], [2.15, .105, .028], [2.5, .003, .003]];
for (let face = 0; face < 8; face++) {
  for (const [z, width, depth] of levels) {
    const ring = [[-width, 0], [-width * .8, -depth * .35], [0, -depth], [width * .8, -depth * .35],
      [width, 0], [width * .8, depth * .35], [0, depth], [-width * .8, depth * .35]];
    for (const corner of [face, (face + 1) % 8]) {
      positions.push(...ring[corner], z);
      uv.push(corner / 8, z / 2.5);
    }
  }
  const offset = face * levels.length * 2;
  for (let row = 0; row < levels.length - 1; row++) {
    const a = offset + row * 2;
    triangles.push(a, a + 1, a + 3, a, a + 3, a + 2);
  }
}
const blade = new T.BufferGeometry();
blade.setAttribute('position', new T.Float32BufferAttribute(positions, 3));
blade.setAttribute('uv', new T.Float32BufferAttribute(uv, 2));
blade.setIndex(triangles);

const guardShape = new T.Shape();
guardShape.moveTo(-.41, -.085); guardShape.quadraticCurveTo(-.28, -.01, -.13, -.015);
guardShape.lineTo(.13, -.015); guardShape.quadraticCurveTo(.28, -.01, .41, -.085);
guardShape.lineTo(.43, -.025); guardShape.quadraticCurveTo(.27, .10, .13, .065);
guardShape.lineTo(-.13, .065); guardShape.quadraticCurveTo(-.27, .10, -.43, -.025); guardShape.closePath();
const guard = new T.ExtrudeGeometry(guardShape, {depth: .08, bevelEnabled: true, bevelSegments: 2, bevelSize: .012, bevelThickness: .012, steps: 1, curveSegments: 8});
guard.rotateX(Math.PI / 2); guard.translate(0, .04, -.035);
const grip = new T.CylinderGeometry(.055, .065, .55, 12, 6).rotateX(Math.PI / 2).translate(0, 0, -.35);
const pommel = new T.LatheGeometry([[0, -.78], [.045, -.775], [.08, -.73], [.075, -.675], [.04, -.64]].map(point => new T.Vector2(...point)), 16).rotateX(Math.PI / 2);
write('sword', [part('blade', blade, 'metal'), part('guard', guard, 'metal'), part('grip', grip, 'leather'), part('pommel', pommel, 'metal')],
  [{Id: 'grip', Part: 2, Position: [0, 0, -.34], Direction: [0, 0, 1]},
    {Id: 'choke', Part: 2, Position: [0, 0, -.14], Direction: [0, 0, 1]},
    {Id: 'edge', Part: 0, Position: [.13, 0, 1.3], Direction: [1, 0, 0]},
    {Id: 'tip', Part: 0, Position: [0, 0, 2.5], Direction: [0, 0, 1]}],
  [{Id: 'edge', Part: 0, A: [0, 0, .05], B: [0, 0, 2.35], Radius: .035},
    ...[-1, 1].flatMap(side => levels.slice(0, -1).map(([z,w],i) => ({Id:`edge/${side<0?'left':'right'}/${i}`,Part:0,
      A:[side*w,0,z+.012],B:[side*levels[i+1][1],0,levels[i+1][0]-.012],Radius:.012}))),
    {Id:'tip',Part:0,A:[0,0,2.43],B:[0,0,2.49],Radius:.01}], [], [], false, null,
  structure('rigid','forms.mjs','sword',{bladeSections:levels,grip:[.055,.065,.55],guardDepth:.08},
    {blade:['blade.edge','blade.spine'],guard:['weapon.guard'],grip:['weapon.grip'],pommel:['weapon.pommel']},
    [['grip','guard',[0,0,-.035]],['guard','blade',[0,0,0]],['grip','pommel',[0,0,-.64]]]),
  [{Id:'grip',Anchor:'grip',Up:[0,1,0],Diameter:.13,Scale:[.2,.6]},
    {Id:'choke',Anchor:'choke',Up:[0,1,0],Diameter:.12,Scale:[.2,.6]}], [], {Groups:['sword'],Scale:.42});

// 分枝以独立部件输出，保留连接枢轴与末梢；生长及附着使用相同部位。
const branchParts = [], tips = [], roots = [];
const paths = [
  [[0, 0, 0], [.03, -.02, .55], [-.08, .03, 1.1], [.06, .02, 1.75]],
  [[-.035, .01, .65], [-.26, .04, .93], [-.5, -.04, 1.23], [-.63, .02, 1.46]],
  [[-.04, .025, 1.05], [.22, .07, 1.27], [.42, .12, 1.55], [.48, .14, 1.72]]
];
paths.forEach((points, index) => {
  const curve = new T.CatmullRomCurve3(points.map(point => new T.Vector3(...point)));
  const geometry = new T.TubeGeometry(curve, 28, index ? .045 : .095, 10, false);
  const attribute = geometry.getAttribute('position');
  for (let i = 0; i < attribute.count; i++) {
    const progress = Math.floor(i / 11) / 28;
    const centre = curve.getPointAt(progress);
    const value = new T.Vector3().fromBufferAttribute(attribute, i).sub(centre).multiplyScalar(1 - .86 * progress).add(centre);
    attribute.setXYZ(i, value.x, value.y, value.z);
  }
  branchParts.push(part(index ? `branch_${index}` : 'trunk', geometry, 'wood', points[0]));
  tips.push({Id: `tip_${index}`, Part: index, Position: points.at(-1), Direction: curve.getTangentAt(1).toArray()});
  roots.push({Id: `body_${index}`, Part: index, A: points[0], B: points.at(-1), Radius: index ? .055 : .1});
});
write('branch', branchParts, tips, roots, [], [], false, null,
  structure('branch','forms.mjs','branch',{paths,rows:28,sides:10,radii:[.095,.045]},
    {trunk:['branch.trunk'],branch_1:['branch.lateral'],branch_2:['branch.lateral']},
    [['trunk','branch_1',paths[1][0]],['trunk','branch_2',paths[2][0]]]));

// 可反复使用的圆润肢段；长轴为z，关节中心分别在0和1。
const limb = new T.LatheGeometry([[0, 0], [.56, .025], [.62, .13], [.57, .4], [.48, .78], [.44, .96], [0, 1]]
  .map(point => new T.Vector2(...point)), 16).rotateX(Math.PI / 2);
write('limb', [part('segment', limb, 'skin')],
  [{Id: 'start', Part: 0, Position: [0, 0, 0], Direction: [0, 0, 1]},
    {Id: 'end', Part: 0, Position: [0, 0, 1], Direction: [0, 0, 1]}], [], [], [], false, null,
  structure('rigid','forms.mjs','limb',{length:1,sides:16},{segment:['body.limb']}));

// 手掌保留柔和轮廓及独立指节，握持时可以逐节弯曲，末端不是一团亮球。
const outline = new T.Shape();
outline.moveTo(-.035, -.04); outline.bezierCurveTo(-.046, -.01, -.05, .025, -.04, .045);
outline.quadraticCurveTo(0, .065, .039, .043); outline.bezierCurveTo(.046, .02, .036, -.015, .03, -.04); outline.closePath();
const palm = new T.ExtrudeGeometry(outline, {depth: .025, bevelEnabled: true, bevelSize: .006, bevelThickness: .005, bevelSegments: 3, steps: 1, curveSegments: 10}).translate(0, 0, -.0125);
const handParts = [part('palm', palm, 'skin')], handAnchors = [];
for(let finger = 0; finger < 5; finger++) {
  const thumb = finger === 4, x = thumb ? -.052 : -.031 + finger * .021;
  const root = thumb ? -.005 : .041, length = thumb ? .05 : [.066, .081, .074, .055][finger];
  const radius = thumb ? .012 : .009;
  for(let joint = 0; joint < 2; joint++) {
    const start = root + joint * length * .55, size = length * (joint ? .45 : .55);
    const geometry = new T.CapsuleGeometry(radius * (joint ? .83 : 1), size - radius * 2, 4, 10).translate(x, start + size * .5, 0);
    handParts.push(part(`finger_${finger}_${joint}`, geometry, 'skin', [x, start, 0]));
  }
  handAnchors.push({Id: `finger_${finger}`, Part: 2 + finger * 2, Position: [x, root + length, 0], Direction: [0, 1, 0]});
}
write('hand', handParts, handAnchors.concat([
  {Id: 'wrist', Part: 0, Position: [0, -.04, 0], Direction: [0, 1, 0]},
  {Id: 'grip', Part: 0, Position: [0, .02, -.025], Direction: [1, 0, 0]}]),
  [{Id: 'palm', Part: 0, A: [0, -.015, .01], B: [0, .067, .01], Radius: .042}], [], [], false, null,
  structure('articulated','forms.mjs','hand',{fingers:5,palmDepth:.025,joints:handParts.slice(1).map(p=>p.Pivot)},
    Object.fromEntries(handParts.map((p,i)=>[p.Id,[i ? 'body.finger' : 'body.palm']])),
    handParts.slice(1).map((p,i)=>[i%2 ? handParts[i].Id : 'palm',p.Id,p.Pivot])));

makeWhale();
makeWave();
makeFlow();
