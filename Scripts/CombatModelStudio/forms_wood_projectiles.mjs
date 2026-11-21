/** 木桩与断木用有厚度的木芯、树皮及端面表现。 */
import * as T from 'three';
import {dissolve, motion, round, structure, write} from './form_export.mjs';
import {column, wallPart} from './forms_solid_walls.mjs';

const origin=[0,0,0], identity=[0,0,0,1], forward=[0,0,1], up=[0,1,0];
const root={Id:'root',Position:origin,Rotation:identity};
const rootAnchor={Id:'root',Part:0,Position:origin,Direction:forward,Control:'root'};
const falling={Forward:forward,Up:up,HideBelowGround:true};

/** 枝干截面沿制作前向排列，凹凸保持连续，以便读出整段树皮。 */
function grain(sections,sides,phase=0) {
  const rings=sections.map(([z,rx,ry,x=0,y=0],row)=>Array.from({length:sides},(_,index)=>{
    const angle=index*Math.PI*2/sides;
    const edge=1+.13*Math.cos(3*angle+phase)+.071*Math.sin(5*angle+phase*.7+row*.21);
    return [x+rx*Math.cos(angle)*edge,y+ry*Math.sin(angle)*edge,z];
  }));
  return column(rings,sides);
}

function capsule(id,a,b,radius,part=0) {
  return {Id:id,Part:part,A:a,B:b,Radius:radius,AControl:'root',BControl:'root'};
}

function roles(parts,role) {
  return Object.fromEntries(parts.map(part=>[part.Id,[role]]));
}

/** 木桩直接飞行，表层木纤维沿已有木芯束拢，接触区由中央实木决定。 */
function stake() {
  const fibers={Id:'fibers',Parent:'root',Position:[0,0,.49],Rotation:identity};
  const controls=[root,fibers];
  const body=grain([[-.17,.15,.12],[.08,.20,.15],[.50,.157,.135],[1.47,.128,.113],[1.79,.094,.078],[2.07,.004,.004]],8,.3);
  const point=grain([[1.46,.095,.079],[1.62,.13,.11],[1.83,.083,.065],[2.07,.004,.004]],6,1.4);
  const strip=grain([[.034,.02,.014],[.16,.061,.035],[.64,.057,.04],[1.22,.028,.017],[1.51,.006,.006]],6,1.8)
    .rotateY(-.065).translate(.154,.015,0);
  const parts=[
    wallPart('stake',body,'wood_bark',origin,'root'), wallPart('cut_point',point,'wood_heartwood',origin,'root'),
    wallPart('near_fiber',strip,'wood',fibers.Position,'fibers'),
    wallPart('far_fiber',strip.clone().rotateZ(2.8).translate(0,0,-.14),'wood_heartwood',fibers.Position,'fibers')
  ];
  const gather=motion('gather_fibers',.40,false,controls,16,(index,t)=>{
    if(!index)return {};
    const remaining=1-T.MathUtils.smoothstep(t,0,1);
    return {position:[.036*remaining,-.016*remaining,.49-.24*remaining],
      rotation:new T.Quaternion().setFromAxisAngle(new T.Vector3(0,0,1),-.38*remaining).toArray()};
  });
  const tip={Id:'tip',Part:1,Position:[0,0,2.07],Direction:forward,Control:'root'};
  const contacts=[capsule('core_0',[0,0,.11],[0,0,1.38],.10),capsule('core_1',[0,0,1.45],[0,0,1.91],.069,1)];
  write('wood_spear_1',parts,[rootAnchor,tip],contacts,controls,[gather],false,{Forward:forward,Up:up},
    structure('articulated','forms_wood_projectiles.mjs','makeWoodProjectileForms',{length:2.24},roles(parts,'piercing.core')));
}

/** 老树干露出切面，其后是错开的残枝。短断木则保留原来较钝的轮廓。 */
function timber(id,long) {
  const body=long
    ? grain([[-1.92,.22,.21,.08],[-1.49,.30,.31,.04],[-.92,.37,.34],[-.33,.31,.30,-.01],[-.02,.32,.29]],13,.77)
    : grain([[-1.12,.27,.24],[-.82,.32,.25],[-.16,.31,.28,.02],[-.025,.27,.26]],11,2.1);
  const tip=long?.30:.25;
  const cap=new T.CylinderGeometry(tip*.94,tip,.047,13).rotateX(Math.PI/2).translate(0,0,-.024);
  const growth=new T.CylinderGeometry(tip*.53,tip*.55,.014,11).rotateX(Math.PI/2).translate(-.038,.011,-.001);
  const branch=grain([[0,.13,.12],[.32,.105,.106],[.63,.059,.051],[.72,.025,.030]],8,.2)
    .rotateY(long?1.16:1.34).translate(.196,0,long?-1.16:-.67);
  const pieces=[['trunk',body,'wood_bark'],['cut',cap,'wood_heartwood'],['end_grain',growth,'wood'],['bough',branch,'wood_bark']];
  if(long) {
    const fork=grain([[0,.13,.11],[.24,.084,.073],[.48,.034,.032],[.60,.009,.015]],8,1.43)
      .rotateY(1.64).rotateZ(2.6).translate(-.23,.066,-.62);
    const ridge=grain([[-1.85,.052,.045],[-1.28,.07,.054],[-.69,.078,.047],[-.3,.054,.037]],7,2)
      .translate(-.185,-.185,0);
    pieces.push(['branch_fork',fork,'wood'],['raised_bark',ridge,'wood']);
  }
  const parts=pieces.map(([name,mesh,surface])=>wallPart(name,mesh,surface,origin,'root'));
  const radius=long?.295:.242, contacts=[capsule('core_0',[0,0,-radius],[0,0,long?-1.56:-.84],radius)];
  const clips=long?[dissolve([root],(_i,t)=>({position:[.11*t,0,.21*t],
    rotation:new T.Quaternion().setFromAxisAngle(new T.Vector3(0,1,0),.41*t).toArray()}))]:[];
  write(id,parts,[rootAnchor],contacts,[root],clips,false,falling,structure('rigid','forms_wood_projectiles.mjs',
    'makeWoodProjectileForms',{length:long?1.92:1.12},roles(parts,'projectile.body')));
}

/** 木板沿同一方向劈开，两侧木楔以不同速度掀起，散片回落在作用范围内。 */
function splitter() {
  const duration=.22, ease=T.MathUtils.smoothstep;
  const halves=[
    {x:-.13,y:.15,length:.64,width:.23,angle:.25,slide:.15,lift:.095,turn:.74},
    {x:.14,y:-.14,length:.54,width:.21,angle:.21,slide:-.15,lift:.084,turn:-.69},
    {x:-.47,y:.29,length:.25,width:.10,angle:.46,slide:.12,lift:.153,turn:1.12},
    {x:.35,y:-.41,length:.21,width:.074,angle:-.32,slide:-.19,lift:.17,turn:-1.19},
    {x:.52,y:.19,length:.20,width:.068,angle:.32,slide:.126,lift:.092,turn:.94},
    {x:-.28,y:-.34,length:.16,width:.059,angle:.66,slide:-.12,lift:.178,turn:.8}
  ];
  const controls=[root,...halves.map(({x,y},i)=>({Id:`split_${i}`,Parent:'root',Position:[x,y,-.063],Rotation:identity}))];
  const core=grain([[-.49,.009,.012],[-.35,.22,.074],[.22,.20,.105],[.48,.033,.022]],8,.7)
    .rotateY(Math.PI/2).rotateZ(.21).translate(0,0,-.059);
  const parts=[wallPart('crushed_heartwood',core,'wood_heartwood',origin,'root')];
  for(const [i,half] of halves.entries()) {
    const {Id:id,Position:pivot}=controls[i+1];
    const {length:l,width:w}=half;
    const layout=[[-l*.6,w*.13,w*.09],[-l*.34,w,w*.43],[l*.25,w*.92,w*.40],[l*.54,w*.10,w*.09]];
    const split=grain(layout,7,.75+i).rotateY(Math.PI/2).rotateZ(half.angle).translate(...pivot);
    parts.push(wallPart(id,split,i%3===0?'wood_bark':'wood_heartwood',pivot,id));
  }
  const spread=motion('spread',duration,false,controls,24,(index,t,control)=>{
    if(!index)return {};
    const half=halves[index-1], at=t*duration, delay=(index%3)*.007;
    const kick=ease(at,delay,.041+delay), drop=ease(at,.057+delay,.165+delay);
    const [x,y,z]=control.Position;
    return {position:[x+Math.sign(x)*.074*kick,y+half.slide*(kick-.3),z-half.lift*(kick-drop)+.032*drop],
      rotation:new T.Quaternion().setFromEuler(new T.Euler(.13*kick,half.turn*(.51*kick-.39*drop),.17*half.turn*kick)).toArray()};
  });
  for(const key of spread.Keys)key.Fade=round(.22*(1-ease(key.At,0,.026))+ease(key.At,.079,duration));
  write('falling_timber_impact',parts,[rootAnchor],[capsule('core_0',origin,origin,.9)],controls,[spread],false,falling,
    structure('articulated','forms_wood_projectiles.mjs','makeWoodProjectileForms',{contactRadius:.9,duration},roles(parts,'impact.volume')));
}

export function makeWoodProjectileForms() { stake(); timber('falling_timber_0',true); timber('falling_timber_1',false); splitter(); }
