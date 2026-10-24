/** 有真实器口、镜面及握持资料的刚性器物；不使用整物球体代替局部结构。 */
import * as T from 'three';
import {mergeGeometries} from 'three/addons/utils/BufferGeometryUtils.js';
import {part, write, structure} from './form_export.mjs';

/** 保留内外壁与底部；截面首尾落在转轴时自然封口，不额外盖住器口。 */
function lathe(profile, segments = 64) {
  const geometry = new T.LatheGeometry(profile.map(p => new T.Vector2(...p)), segments);
  geometry.userData.smoothSeams = true;
  return geometry;
}

/** 沿真实首尾放置有截面的刚性支撑。 */
function strut(a, b, radius, endRadius = radius, sides = 16) {
  const start = new T.Vector3(...a), end = new T.Vector3(...b), delta = end.clone().sub(start);
  return new T.CylinderGeometry(endRadius, radius, delta.length(), sides, 3)
    .applyQuaternion(new T.Quaternion().setFromUnitVectors(new T.Vector3(0,1,0),delta.normalize()))
    .translate(...start.add(end).multiplyScalar(.5).toArray());
}

/** 水平薄环的作用近似保留空心，胶囊沿环周相连，不填满中间。 */
function ringContacts(id, index, radius, z, thickness, count = 16) {
  const point = angle => [Math.cos(angle)*radius,Math.sin(angle)*radius,z];
  return Array.from({length:count},(_,i)=>({Id:`${id}_${i}`,Part:index,
    A:point(i/count*Math.PI*2),B:point((i+1)/count*Math.PI*2),Radius:thickness}));
}

/** 七个真实部件组成的敞口三足炉，口沿和把手可被共同结构选择或拆离。 */
function makeVessel() {
  const profile = [[0,.12],[.35,.12],[.60,.28],[.72,.68],[.63,1.08],[.48,1.25],[.47,1.34],
    [.40,1.34],[.40,1.26],[.52,1.05],[.61,.70],[.50,.37],[.29,.24],[0,.24]];
  const parts = [part('body',lathe(profile).rotateX(Math.PI/2),'bronze'),
    part('mouth_ring',new T.TorusGeometry(.435,.048,12,64).translate(0,0,1.335),'bronze')];
  const connections = [['body','mouth_ring',[0,0,1.335]]];
  for (const side of [-1,1]) {
    const id = side < 0 ? 'handle_left' : 'handle_right';
    parts.push(part(id,new T.TorusGeometry(.18,.036,10,48).rotateY(Math.PI/2).translate(side*.67,0,1.07),'bronze'));
    connections.push(['body',id,[side*.63,0,1.07]]);
  }
  for(let i=0;i<3;i++) {
    const angle = (i/3+.25)*Math.PI*2;
    const root = [Math.cos(angle)*.43,Math.sin(angle)*.43,.35];
    const foot = [Math.cos(angle)*.57,Math.sin(angle)*.57,0];
    parts.push(part(`foot_${i}`,strut(foot,root,.078,.105),'bronze',root));
    connections.push(['body',`foot_${i}`,root]);
  }
  const contacts = [[.43,.33,.10],[.60,.67,.11],[.54,1.01,.09]]
    .flatMap(([r,z,t],i)=>ringContacts(`wall_${i}`,0,r,z,t));
  contacts.push(...ringContacts('rim',1,.435,1.335,.048));
  const anchors = [{Id:'mouth',Part:1,Position:[0,0,1.34],Direction:[0,0,1]},
    {Id:'grip.left',Part:2,Position:[-.67,0,1.25],Direction:[0,1,0]},
    {Id:'grip.right',Part:3,Position:[.67,0,1.25],Direction:[0,1,0]},
    {Id:'base',Part:0,Position:[0,0,.12],Direction:[0,0,-1]}];
  write('ritual_vessel',parts,anchors,contacts,[],[],false,{Forward:[0,1,0],Up:[0,0,1]},
    structure('rigid','forms_rigid.mjs','makeVessel',{profile,segments:64,mouth:[.435,.048,1.335],feet:3},
      {body:['vessel.wall'],mouth_ring:['vessel.mouth'],handle_left:['vessel.handle'],handle_right:['vessel.handle'],
        foot_0:['vessel.support'],foot_1:['vessel.support'],foot_2:['vessel.support']},connections),
    [-1,1].map(side=>({Id:side<0?'left':'right',Anchor:side<0?'grip.left':'grip.right',Up:[0,0,1],Diameter:.072,Scale:[.2,.8]})));
}

/** 镜面面向负纵轴，与边框、背板和握柄分别保存，不靠染色假冒同一表面。 */
function makeMirror() {
  const radius = .56, height = .95;
  const reflector = part('reflector',new T.CylinderGeometry(radius,radius,.03,64,1).translate(0,-.04,height),'mirror');
  // 镜面保留专门的光学表面，主体材料覆盖仍作用于铜框和背板。
  reflector.UseMatterSurface = false;
  const backProfile = [[0,.08],[.20,.075],[.45,.05],[.58,.015],[.61,0],[.61,.04],[.46,.13],[.22,.17],[0,.18]];
  const decorations = [];
  for(let i=0;i<8;i++) {
    const angle = (i/8+.0625)*Math.PI*2;
    decorations.push(new T.SphereGeometry(.029,12,8).scale(1,.55,1)
      .translate(Math.cos(angle)*.61,-.049,height+Math.sin(angle)*.61));
  }
  const parts = [reflector,
    part('frame',new T.TorusGeometry(.61,.065,12,64).rotateX(Math.PI/2).translate(0,0,height),'bronze'),
    part('back',lathe(backProfile).translate(0,0,height),'bronze'),
    part('grip',strut([0,.02,-.23],[0,.02,.46],.066,.083),'bronze'),
    part('inlays',mergeGeometries(decorations),'metal')];
  parts[4].UseMatterSurface = false;
  const contacts = Array.from({length:11},(_,i)=> {
    const x = -.5+i*.1, span = Math.sqrt(.51*.51-x*x);
    return {Id:`face_${i}`,Part:0,A:[x,-.055,height-span],B:[x,-.055,height+span],Radius:.049};
  });
  const anchors = [{Id:'face',Part:0,Position:[0,-.055,height],Direction:[0,-1,0]},
    {Id:'grip',Part:3,Position:[0,.02,.10],Direction:[0,0,1]},
    {Id:'back',Part:2,Position:[0,.18,height],Direction:[0,1,0]}];
  write('ritual_mirror',parts,anchors,contacts,[],[],false,{Forward:[0,-1,0],Up:[0,0,1]},
    structure('rigid','forms_rigid.mjs','makeMirror',{radius,height,backProfile,segments:64,grip:[.066,.083,.69]},
      {reflector:['mirror.face'],frame:['mirror.frame'],back:['mirror.back'],grip:['weapon.grip'],inlays:['decoration.inlay']},
      [['frame','reflector',[0,-.04,height]],['frame','back',[0,.015,height]],['frame','grip',[0,.02,.40]],['frame','inlays',[0,-.049,height]]]),
    [{Id:'grip',Anchor:'grip',Up:[0,-1,0],Diameter:.166,Scale:[.18,.7]}]);
}

/** 两种器物复用相同部件、挂点、材料及发布入口。 */
export function makeRigidForms() { makeVessel(); makeMirror(); }
