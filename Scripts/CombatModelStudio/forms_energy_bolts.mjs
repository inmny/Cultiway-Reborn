/** 四种原术保留辨认轮廓。旧自创法术引用的形体资源继续可用。 */
import * as T from 'three';
import {dissolve, motion, round, structure, write} from './form_export.mjs';
import {column, wallPart} from './forms_solid_walls.mjs';

const origin=[0,0,0], rotation=[0,0,0,1], forward=[0,0,1], up=[0,1,0];
const root={Id:'root',Position:origin,Rotation:rotation};
const anchors=[{Id:'root',Part:0,Position:origin,Direction:forward,Control:'root'}];
const facing={Forward:forward,Up:up};
const pigments=['entropy_cobalt','entropy_violet','entropy_carmine','entropy_ochre'];

/** 曲带沿弧保持封闭截面，中段有厚面，端部收窄。 */
function foldedBand(start,span,radius,thickness,offset) {
  const sections=26,sides=9;
  const rings=Array.from({length:sections},(_,i)=>{
    const t=i/(sections-1), angle=start+span*t;
    const body=.07+.93*Math.pow(Math.sin(Math.PI*t),.62);
    const r=radius+.044*Math.sin(t*4.5);
    return Array.from({length:sides},(_,side)=>{
      const k=side*Math.PI*2/sides, width=thickness*body;
      const rim=r+width*Math.cos(k);
      return [rim*Math.cos(angle)+offset[0],offset[1]+width*.81*Math.sin(k)+.085*(t-.5),
        rim*Math.sin(angle)+offset[2]];
    });
  });
  return column(rings,sides);
}

function capsule(a,b,radius) {
  return {Id:'core_0',Part:0,A:a,B:b,Radius:radius,AControl:'root',BControl:'root'};
}

/** 外层分件在实际结束点由本身动作松开，真实接触始终由中央部位承担。 */
function bolt(id,pieces,shape,appearance) {
  const controls=[root,...pieces.slice(1).map(([Id])=>({Id,Parent:'root',Position:origin,Rotation:rotation}))];
  const parts=pieces.map(([name,mesh,surface],i)=>wallPart(name,mesh,surface,origin,controls[i].Id));
  const exit=dissolve(controls,(i,t)=>{
    if(!i)return {};
    const [, , , drift, turn]=pieces[i];
    return {position:drift.map(axis=>axis*t),
      rotation:new T.Quaternion().setFromAxisAngle(new T.Vector3(0,1,0),turn*t).toArray()};
  });
  write(id,parts,anchors,[shape],controls,[exit],false,facing,structure('articulated',
    'forms_energy_bolts.mjs','makeEnergyBoltForms',appearance,
    Object.fromEntries(parts.map(part=>[part.Id,['projectile.body']]))));
}

function spirit() {
  const pearl=new T.SphereGeometry(.335,24,16).scale(1,.94,1.08);
  const cold=foldedBand(-.76,2.91,.33,.137,[-.055,.023,-.054]);
  const warm=foldedBand(2.09,2.60,.309,.113,[.025,-.033,-.070]);
  bolt('spirit_bolt_0',[
    ['pearl',pearl,'spirit_core'],
    ['cool_fold',cold,'spirit_pearl',[.09,.05,-.24],.48],
    ['warm_fold',warm,'spirit_warm',[-.11,-.02,-.20],-.42]
  ],capsule(origin,origin,.31),{pearl:.335,folds:2});
}

function yin() {
  const core=new T.SphereGeometry(.29,24,16).scale(1,.65,1);
  const near=foldedBand(-1.13,4.55,.303,.158,[.013,.009,-.045]);
  const far=foldedBand(2.83,3.43,.272,.111,[-.045,.073,.034]);
  bolt('yin_bolt_0',[
    ['void',core,'yin_depth'],
    ['wide_outer_fold',near,'yin_wrap',[.17,-.05,-.09],.69],
    ['inner_fold',far,'yin_moon',[-.10,.08,.11],-.46]
  ],capsule(origin,origin,.28),{core:.29,folds:2});
}

/** 四道厚日瓣沿前后左右张开，表面向明亮中心收形。 */
function yang() {
  const core=new T.DodecahedronGeometry(.30).scale(1,.95,1);
  const sides=6;
  const profile=[[.08,.11,.063],[.24,.191,.128],[.40,.134,.083],[.52,.051,.047],[.59,.005,.006]];
  const petal=column(profile.map(([z,x,y])=>Array.from({length:sides},(_,i)=>{
    const phi=i*Math.PI*2/sides;
    return [x*Math.cos(phi),y*Math.sin(phi),z];
  })),sides);
  const pieces=[['seed',core,'aegis_solar']];
  for(let i=0;i<4;i++) {
    const angle=i*Math.PI/2+.14;
    pieces.push([`sun_petal_${i}`,petal.clone().rotateY(angle),i%2===0?'solar_gold':'solar_light',
      [.18*Math.sin(angle),.035,.18*Math.cos(angle)],i%2===0?.35:-.34]);
  }
  bolt('yang_bolt_0',pieces,capsule(origin,origin,.28),{core:.30,petals:4});
}

/** 四块色面像相互咬合的矿物，远看仍是连在一起的弹体。 */
function chaos() {
  const core=new T.IcosahedronGeometry(.325,1).scale(1.05,.91,1.10);
  const angles=[.46,2.03,3.60,5.12];
  const pieces=[['dense_center',core,'entropy_depth']];
  for(const [i,angle] of angles.entries()) {
    const axis=new T.Vector3(Math.cos(angle),0,Math.sin(angle));
    const face=new T.DodecahedronGeometry(.264+.014*(i%3)).scale(1.23,.85,1.03)
      .rotateX(.64*i).rotateY(.51+angle).translate(axis.x*.221,.074*Math.cos(i*2.3),axis.z*.237);
    pieces.push([`region_${i}`,face,pigments[i],[.28*axis.x,.09*Math.cos(i*2.7),.28*axis.z],.43-.22*i]);
  }
  bolt('chaos_orb_0',pieces,capsule([0,0,-.05],[0,0,.05],.30),{regions:4,core:.325});
}

/** 破碎断面向几侧同时翻开，改变入射角时仍占据同一个球形范围。 */
function chaosImpact() {
  const duration=.22, ease=T.MathUtils.smoothstep;
  const fragments=[
    {offset:[.36,-.29,-.24],radius:.27,out:.24,flip:.82},
    {offset:[-.38,.22,.12],radius:.24,out:.29,flip:-.65},
    {offset:[.08,.33,.34],radius:.24,out:.26,flip:.61},
    {offset:[-.19,-.25,.28],radius:.20,out:.28,flip:-.52},
    {offset:[.46,.27,.05],radius:.11,out:.31,flip:.89},
    {offset:[-.40,-.17,-.35],radius:.086,out:.29,flip:-.87},
    {offset:[-.03,.45,-.32],radius:.078,out:.32,flip:.62},
    {offset:[.15,-.43,.39],radius:.079,out:.30,flip:-.93}
  ];
  const controls=[root,...fragments.map(({offset},i)=>({Id:`section_${i}`,Parent:'root',Position:offset,Rotation:rotation}))];
  const pieces=[wallPart('fracture_center',new T.IcosahedronGeometry(.34,1).scale(1.03,.86,.94),
    'entropy_depth',origin,'root')];
  for(const [i,fragment] of fragments.entries()) {
    const {Id:id,Position:pivot}=controls[i+1];
    const g=new T.DodecahedronGeometry(fragment.radius).scale(1.13,.61,.89).rotateY(i*1.16)
      .rotateZ(.52*Math.cos(i*2.7)).rotateX(.63*Math.sin(i*1.61)).translate(...pivot);
    pieces.push(wallPart(id,g,pigments[i%pigments.length],pivot,id));
  }
  const spread=motion('spread',duration,false,controls,24,(index,t,control)=>{
    if(!index)return {};
    const {offset,out,flip}=fragments[index-1], delay=(index%4)*.006;
    const axis=new T.Vector3(...offset).normalize();
    const at=t*duration, open=ease(at,delay,.065+delay), sink=ease(at,.068+delay,.159+delay);
    const position=new T.Vector3(...control.Position).addScaledVector(axis,out*(open-.56));
    position.y+=.084*(open-sink)-.049*sink;
    // 同时朝多向离心时，缩短各片翻转角度，给相加的骨架转速留余量。
    const twist=new T.Quaternion().setFromAxisAngle(new T.Vector3(-axis.z,.23,axis.x).normalize(),.74*flip*(open-.61*sink));
    return {position:position.toArray(),rotation:twist.toArray()};
  });
  for(const frame of spread.Keys)frame.Fade=round(.22*(1-ease(frame.At,0,.021))+ease(frame.At,.073,duration));
  write('chaos_orb_impact',pieces,anchors,[capsule(origin,origin,.9)],controls,[spread],false,facing,
    structure('articulated','forms_energy_bolts.mjs','makeEnergyBoltForms',{radius:.9,duration},
      Object.fromEntries(pieces.map(part=>[part.Id,['impact.volume']]))));
}

export function makeEnergyBoltForms() { spirit(); yin(); yang(); chaos(); chaosImpact(); }
