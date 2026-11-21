/** 下落晶簇的尖端与实际落点共用原点。 */
import * as T from 'three';
import {dissolve, motion, round, structure, write} from './form_export.mjs';
import {column, wallPart} from './forms_solid_walls.mjs';

const origin=[0,0,0], identity=[0,0,0,1];
const root={Id:'root',Position:origin,Rotation:identity};
const anchors=[{Id:'root',Part:0,Position:origin,Direction:[0,0,1],Control:'root'}];
const facing={Forward:[0,0,1],Up:[0,1,0],HideBelowGround:true};

function crystal(length,width) {
  const profile=[[-length,.04,0],[-length*.68,width,-.04],[-length*.39,width*.9,.02],[-.04,width*.12,0],[0,.004,0]];
  return column(profile.map(([z,r,shift])=>Array.from({length:6},(_,i)=>{
    const angle=i*Math.PI/3;
    return [shift+r*Math.cos(angle),r*Math.sin(angle),z];
  })),6);
}

/** 位于中心的冰芯负责接触，四片不等长的冰棱在上方包合。 */
function rain() {
  const parts=[wallPart('core',crystal(2.22,.25),'ice_core',origin,'root')];
  for(const [i,angle] of [.15,1.85,3.3,4.9].entries()) {
    const piece=crystal(1.41+.21*(i%3),.18+.026*(i%3)).rotateY(-.18-.1*(i%2))
      .translate(.20,0,-.20-.25*(i%3)).rotateZ(angle);
    parts.push(wallPart(`shard_${i}`,piece,i%2?'ice':'ice_rime',origin,'root'));
  }
  const contacts=[{Id:'core_0',Part:0,A:[0,0,-.16],B:[0,0,-.63],Radius:.16,
    AControl:'root',BControl:'root'}];
  const clips=[dissolve([root],(_i,t)=>({position:[0,0,-.18*t]}))];
  write('icefall_rain',parts,anchors,contacts,[root],clips,false,facing,
    structure('rigid','forms_icefall.mjs','makeIcefallForms',{length:2.22,shards:4},
      Object.fromEntries(parts.map(part=>[part.Id,['projectile.body','projectile.core']]))));
}

/** 着地晶片各自翻开，覆盖范围由中央球形作用区决定。 */
function impact() {
  const angles=[.18,1.11,2.03,3.12,4.37,5.29],duration=.22,ease=T.MathUtils.smoothstep;
  const axes=angles.map(angle=>new T.Vector3(Math.cos(angle),Math.sin(angle),0));
  const controls=[root,...axes.map((_axis,i)=>({Id:`shard_${i}`,Parent:'root',Position:origin,Rotation:identity}))];
  const parts=[wallPart('fracture_base',new T.IcosahedronGeometry(.87,2).scale(1,1,.37).translate(0,0,-.073),
    'ice',origin,'root')];
  for(const [i,angle] of angles.entries()) {
    const ridge=crystal(.56+.065*(i%3),.15).scale(1,1,.86).rotateY(1.25).translate(.68,0,-.12).rotateZ(angle);
    parts.push(wallPart(controls[i+1].Id,ridge,i%3===0?'ice_core':'ice_rime',origin,controls[i+1].Id));
  }
  const clip=motion('spread',duration,false,controls,24,(index,phase)=>{
    if(!index)return {};
    const at=phase*duration,axis=axes[index-1],delay=(index%3)*.006;
    const open=ease(at,delay,.063+delay),settle=ease(at,.091,duration);
    const position=axis.clone().multiplyScalar(-.24*(1-open)+.18*open+.024*settle);
    position.z=-.12*open+.093*settle;
    const tilt=new T.Quaternion().setFromAxisAngle(new T.Vector3(-axis.y,axis.x,0),.29*open-.46*settle);
    return {position:position.toArray(),rotation:tilt.toArray()};
  });
  for(const key of clip.Keys)key.Fade=round(.26*(1-ease(key.At,0,.027))+ease(key.At,.071,duration));
  write('icefall_impact',parts,anchors,[
    {Id:'core_0',Part:0,A:origin,B:origin,Radius:.9,AControl:'root',BControl:'root'}
  ],controls,[clip],false,facing,structure('articulated','forms_icefall.mjs','makeIcefallForms',
    {contactRadius:.9,shards:axes.length,duration},Object.fromEntries(parts.map(part=>[part.Id,['impact.volume']]))));
}

export function makeIcefallForms() { rain(); impact(); }
