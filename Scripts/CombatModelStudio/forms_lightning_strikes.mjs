/** 落雷的尖端位于本地原点，雷身向上展开，命中与落地沿同一尖端。 */
import * as T from 'three';
import {dissolve, motion, round, structure, write} from './form_export.mjs';
import {column, wallPart} from './forms_solid_walls.mjs';

const origin=[0,0,0], identity=[0,0,0,1];
const root={Id:'root',Position:origin,Rotation:identity};
const facing={Forward:[0,0,1],Up:[0,1,0],HideBelowGround:true};
const source='forms_lightning_strikes.mjs',entry='makeLightningStrikeForms';
const anchors=[{Id:'root',Part:0,Position:origin,Direction:[0,0,1],Control:'root'}];

/** 以带厚度的折面接通各截面。 */
function tube(rows,sides=8) {
  return column(rows.map(([z,r,x=0,y=0])=>Array.from({length:sides},(_,i)=>{
    const angle=i*2*Math.PI/sides;
    return [x+r*Math.cos(angle),y+r*Math.sin(angle),z];
  })),sides);
}

/** 每个旧索引有具体线形、分岔和颜色。坐标以真实尖端为零。 */
const strikes=[
  {height:3.62,width:.20,color:'thunder_amber',edge:'thunder_arc',forks:3,phase:.17,bends:[.04,-.16,.34,-.21,.18,.07,0]},
  {height:4.68,width:.127,color:'thunder_core',edge:'thunder_arc_fork',forks:4,phase:.67,bends:[-.16,.09,.33,-.29,.35,-.04,0]},
  {height:2.48,width:.194,color:'thunder_cyan',edge:'thunder_core',forks:2,phase:1.16,bends:[.02,.23,-.06,-.27,.19,.02,0]},
  {height:3.06,width:.214,color:'thunder_core',edge:'thunder_cyan',forks:4,phase:1.66,bends:[.23,.06,.07,.32,-.38,-.14,0]},
  {height:3.53,width:.156,color:'thunder_cyan',edge:'thunder_body',forks:3,phase:2.21,bends:[-.15,.21,.34,-.17,.26,-.07,0]},
  {height:3.72,width:.169,color:'thunder_arc',edge:'thunder_body',forks:3,phase:2.47,bends:[.17,-.20,.31,-.14,.26,-.06,0]},
  {height:3.43,width:.18,color:'thunder_green',edge:'thunder_core',forks:3,phase:2.74,bends:[.03,.19,-.19,.31,-.19,.10,0]},
  {height:3.67,width:.186,color:'thunder_rose',edge:'thunder_arc_fork',forks:4,phase:3.17,bends:[.14,-.17,.23,-.24,.21,.05,0]},
  {height:3.42,width:.18,color:'thunder_amber',edge:'thunder_core',forks:3,phase:3.51,bends:[-.08,.17,-.10,.27,-.24,.09,0]},
  {height:4.27,width:.254,color:'lightning_dense',edge:'thunder_arc',forks:4,phase:4.12,bends:[.16,-.14,.45,-.23,.26,-.11,0]},
  {height:4.03,width:.29,color:'thunder_body',edge:'thunder_cyan',forks:5,phase:5.14,bends:[-.27,.36,-.28,.44,-.41,.06,0]}
];

function strike(index,{height,width,color,edge,forks,phase,bends}) {
  const offsets=[0,.15,.34,.52,.69,.86,1];
  const widths=[.20,.73,1.09,.87,1.18,.54,.008];
  const path=bends.map((x,i)=>[-height*(1-offsets[i]),width*widths[i],x,
    Math.sin(i*1.65+phase)*width*(1-offsets[i])]);
  const parts=[wallPart('channel',tube(path,8).rotateZ(phase),color,origin,'root')];
  for(let i=0;i<forks;i++) {
    const base=path[2+i%4],z=base[0],angle=phase+i*2.4;
    const length=.56+.13*(i%3)+width;
    const side=Math.cos(angle),away=Math.sin(angle);
    const branch=[
      [z,width*.65,base[2],base[3]],
      [z*.89,width*.76,base[2]+side*length*.29,base[3]+away*length*.29],
      [z*.64,width*.51,base[2]+side*length*.58,base[3]+away*length*.58],
      [z*.42,.01,base[2]+side*length,base[3]+away*length]
    ];
    parts.push(wallPart(`fork_${i}`,tube(branch,6).rotateZ(phase),edge,origin,'root'));
  }
  const tip={Id:'core_0',Part:0,A:[0,0,-.12],B:[0,0,-.16],Radius:.12,AControl:'root',BControl:'root'};
  const motions=index===0?[dissolve([root],(_i,t)=>({position:[0,0,-.42*t]}))]:[];
  write(`lightning_strike_${index}`,parts,[
    ...anchors,{Id:'head',Part:0,Position:[0,0,-height],Direction:[0,0,-1],Control:'root'}
  ],[tip],[root],motions,false,facing,structure('rigid',source,entry,{index,height,width,forks,phase,bends},
    Object.fromEntries(parts.map(part=>[part.Id,[part.Id==='channel'?'projectile.body':'impact.volume']]))));
}

/** 裂开的雷冠围住真实球形作用区，分岔在落点向上翻开并消散。 */
function strikeImpact() {
  const duration=.22,ease=T.MathUtils.smoothstep;
  const axes=Array.from({length:8},(_,i)=>{
    const angle=i*Math.PI/4+.19*Math.sin(i*2.3);
    return new T.Vector3(Math.cos(angle),Math.sin(angle),0);
  });
  const controls=[root,...axes.map((axis,i)=>({Id:`fork_${i}`,Parent:'root',Position:origin,Rotation:identity}))];
  const parts=[wallPart('extent',new T.SphereGeometry(.9,24,16),'thunder_corona',origin,'root')];
  for(const [i,axis] of axes.entries()) {
    const shard=tube([[-.22,.016,.65],[-.15,.09,.54],[-.09,.127,.38],[-.035,.09,.18],[-.012,.02,.11]],7)
      .rotateZ(Math.atan2(axis.y,axis.x));
    parts.push(wallPart(controls[i+1].Id,shard,i%2?'thunder_arc_fork':'thunder_arc',origin,controls[i+1].Id));
  }
  const clip=motion('spread',duration,false,controls,22,(index,phase)=>{
    if(index===0)return {};
    const time=phase*duration,axis=axes[index-1],delay=(index%3)*.006;
    const open=ease(time,delay,.055+delay),settle=ease(time,.083,duration);
    const position=axis.clone().multiplyScalar(-.16*(1-open)+.19*open+.055*settle);
    position.z=-.055*open+.054*settle;
    return {position:position.toArray(),rotation:new T.Quaternion().setFromAxisAngle(
      new T.Vector3(-axis.y,axis.x,0),.34*open-.57*settle).toArray()};
  });
  for(const key of clip.Keys)key.Fade=round(.13*(1-ease(key.At,0,.023))+ease(key.At,.066,duration));
  write('lightning_strike_impact',parts,anchors,[
    {Id:'core_0',Part:0,A:origin,B:origin,Radius:.9,AControl:'root',BControl:'root'}
  ],controls,[clip],false,facing,structure('articulated',source,entry,{contactRadius:.9,forks:axes.length,duration},
    Object.fromEntries(parts.map(part=>[part.Id,['impact.volume']]))));
}

/** 发布十一个独立三维索引及其共用的实际落点表现。 */
export function makeLightningStrikeForms() {
  strikes.forEach((profile,index)=>strike(index,profile));
  strikeImpact();
}
