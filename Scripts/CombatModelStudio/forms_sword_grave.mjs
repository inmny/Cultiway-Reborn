import * as T from 'three';
import {motion, part, structure, write} from './form_export.mjs';
import {phaseValue, rotation} from './form_skeleton.mjs';

const origin = [0, 0, 0];
const facing = {Forward:[0,0,1], Up:[0,1,0], HideBelowGround:true};
const source = 'forms_sword_grave.mjs';

/** 凸边、实厚和明暗切面在一份局部资料中制作。斜刃向内收，碰撞取仍有厚度的实体边。 */
function plate(outline, depth, bevel = 0) {
  const shape = new T.Shape();
  outline.forEach(([x,z], i) => i ? shape.lineTo(x,-z) : shape.moveTo(x,-z));
  shape.closePath();
  return new T.ExtrudeGeometry(shape, {depth:2*depth, steps:1, curveSegments:1,
    bevelEnabled:bevel>0, bevelThickness:bevel*.75, bevelSize:bevel, bevelSegments:1})
    .translate(0,0,-depth).rotateX(-Math.PI/2);
}

/** 刃面向中间抬高；斜切侧面从同一实体外沿开始，让俯视也能读出厚度。 */
function facetedBlade(outline, depth) {
  const centre = outline.reduce(([x,z],[a,b])=>[x+a/outline.length,z+b/outline.length],[0,0]);
  const section = (y, width, length) => outline.map(([x,z])=>[
    centre[0]+(x-centre[0])*width, y, centre[1]+(z-centre[1])*length]);
  const rings = [section(-depth,.48,.95),section(-depth*.28,1,1),
    section(depth*.28,1,1),section(depth,.48,.95)];
  const vertices = [];
  const face = (a,b,c)=>vertices.push(...a,...b,...c);
  const last = rings.length-1;
  for (let i=1;i<outline.length-1;i++) {
    face(rings[0][0],rings[0][i],rings[0][i+1]);
    face(rings[last][0],rings[last][i+1],rings[last][i]);
  }
  for (let ring=0;ring<last;ring++) {
    const lo=rings[ring],hi=rings[ring+1];
    for (let i=0;i<outline.length;i++) {
      const j=(i+1)%outline.length;
      face(lo[i],hi[j],lo[j]); face(lo[i],hi[i],hi[j]);
    }
  }
  const mesh = new T.BufferGeometry();
  mesh.setAttribute('position',new T.Float32BufferAttribute(vertices,3));
  mesh.setAttribute('uv',new T.Float32BufferAttribute(vertices.flatMap((x,i)=>i%3===0?[x,vertices[i+2]]:[]),2));
  return mesh;
}

/** 铜饰属于构件自身的工艺，换剑身材料时继续保留。 */
function bronze(id, geometry, pivot, control = null) {
  return {...part(id,geometry,'bronze',pivot,control),UseMatterSurface:false};
}

/** 将接触棱柱原样发布，避免外侧空缺仍按整个矩形结算。 */
function edge(id, index, points, thickness, control) {
  // 开工检查和运行共享逆时针凸边合同，坏轮廓不能形成更大的无形碰撞。
  points.forEach(([x,z], i) => {
    const [u,v] = points[(i+1)%points.length];
    for(let j=2;j<points.length;j++) {
      const [a,b] = points[(i+j)%points.length];
      if((u-x)*(b-z)-(v-z)*(a-x)<=0) throw Error(`${id} 的实体边需要凸形`);
    }
  });
  return {Id:id, Part:index, A:origin, ...(control?{AControl:control}:{}), Footprint:{Points:points, HalfThickness:thickness}};
}

/** 一根真实厚脊先离地，落斩动作也同时驱动接缝上的子部件。 */
function spine() {
  const track='sweep';
  const controls=[{Id:track,Position:origin,Rotation:[0,0,0,1]}];
  const profile=[[-.43,.28],[.43,.28],[.51,1.72],[.43,5.46],[-.025,7.6],[-.46,5.42],[-.52,1.66]];
  const inlay=[[-.13,.55],[.14,.55],[.208,2.08],[.165,5.34],[-.03,7.38],[-.165,5.30],[-.204,2.02]];
  const crown=[[-1.05,.15],[-.80,-.15],[.78,-.16],[1.21,.14],[.70,.42],[-.65,.43]];
  const pieces=[part('spine',facetedBlade(profile,.23),'metal',origin,track),
    bronze('raised_ridge',plate(inlay,.30),origin,track),
    bronze('crossguard',plate(crown,.30,.045),origin,track),
    part('tang',new T.CylinderGeometry(.16,.19,1.47,10,1).rotateX(Math.PI/2).translate(0,0,-.73),'leather',origin,track),
    part('pommel',new T.IcosahedronGeometry(.31,1).scale(1,.69,1.12).translate(0,0,-1.48),'metal',origin,track)];
  // 一道接触由蓄势转为加速，末段原地承接，随后整柄落下而非缩放消失。
  const duration=2.70;
  const key=[
    [0,[0,-.32,0,0,0,0]],[.10,[0,-.26,.03,-2,-4,0]],[.47,[0,.93,.08,-10,-7,3]],
    [1.09,[0,1.22,.10,-10,-7,3]],[1.46,[0,1.73,.02,-51,-15,-14]],
    [1.57,[0,1.77,-.10,-53,-16,-15]],[1.84,[0,.88,.48,-1,0,4]],
    [1.99,[0,.72,.67,3,8,7]],[2.17,[0,.62,.69,2,12,1]],[2.58,[0,-.53,.66,1,12,0]],
    [duration,[0,-.74,.65,0,12,0]]
  ].map(([t,v])=>[t/duration,v]);
  const stages=[{Id:'raise',At:.1},{Id:'seated',At:1.10},{Id:'cleave',At:1.70},{Id:'brake',At:2.14}];
  const cut=motion('falling_cleave',duration,false,controls,102,(_i,p)=>{
    const [x,y,z,pitch,yaw,roll]=phaseValue(p,key);
    return {position:[x,y,z],rotation:rotation(pitch,yaw,roll)};
  },stages);
  write('sword_grave_spine',pieces,[{Id:'seam',Part:0,Position:origin,Direction:[0,0,1],Control:track}],
    [edge('body_cut',0,profile,.23,track)],controls,[cut],false,facing,
    structure('rigid',source,'makeSwordGrave',{profile,coreDepth:.46,stages:stages.map(mark=>mark.At)},
      {spine:['weapon.spine'],raised_ridge:['weapon.ridge'],crossguard:['weapon.guard'],tang:['weapon.grip'],pommel:['weapon.pommel']},
      [['spine','raised_ridge',[0,.23,2.2]],['spine','crossguard',[0,0,.25]],['crossguard','tang',origin],['tang','pommel',[0,0,-1.37]]]));
}

/** 两片不同断口的是真实、可重组的部件；每边有一块只负责衔接外观的扣件。 */
function flank(sign) {
  const handed=sign<0?'left':'right';
  const pattern=sign<0 ? [[.39,.53],[.94,.74],[1.03,2.17],[.87,4.71],[.43,6.39],[.34,5.48]] :
    [[.39,.35],[1.08,.87],[1.15,2.68],[.89,5.38],[.40,6.71],[.34,5.44]];
  function mirror(points) { const list=points.map(([x,z])=>[sign*x,z]); return sign<0 ? list.reverse() : list; }
  const shape=mirror(pattern);
  const inlay=mirror([[.44,.79],[.69,.99],[.73,2.73],[.58,4.86],[.43,4.97]]);
  const parts=[part('edge',facetedBlade(shape,.148),'metal',[sign*.7,0,3.12]),
    bronze('clasp',plate(inlay,.187,.021),[sign*.54,0,2.1])];
  // 侧翼同属剑刃，但之后能独立沿实际斩势剥落；锁扣不提供攻击棱柱。
  write(`sword_grave_${handed}`,parts,[],[edge('running_edge',0,shape,.148)],[],[],false,facing,
    structure('rigid',source,'makeSwordGrave',{shape,side:sign,thickness:.296},
      {edge:['blade.edge'],clasp:['weapon.clasp']},[['edge','clasp',[sign*.52,0,2.1]]]));
}

export function makeSwordGrave() { spine(); flank(-1); flank(1); }
