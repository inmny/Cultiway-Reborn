/** 龙卷与玄阴领域各自保留连续厚体，共用原领域执行，不建立专用播放循环。 */
import * as T from 'three';
import {part,write,skin,motion,structure} from './form_export.mjs';

/** 按连续横截面构成封闭旋体，制作上向为Z。 */
function body(profile,segments=64) {
  return new T.LatheGeometry(profile.map(p=>new T.Vector2(...p)),segments).rotateX(Math.PI/2);
}

/** 低层实际作用区按主体圆面布置，芯位于可见厚体内。 */
function floorContacts(partIndex,bind) {
  const contacts=[];
  for(let row=-14;row<=14;row++) {
    const y=row*.16,width=Math.sqrt(2.5*2.5-y*y)-.18;
    const a=[-width,y,.17],b=[width,y,.17];
    contacts.push({Id:`floor_${row+14}`,Part:partIndex,A:a,B:b,Radius:.12,...bind(a,b)});
  }
  return contacts;
}

/** 有宽风底的连续旋卷风体，三条宽曲脊融在主体中，不使用线圈或独立拖尾。 */
function wind() {
  const geometry=body([[0,.02],[2.28,.02],[2.5,.12],[2.5,.35],[1.72,.55],[.95,1.2],
    [1.25,2.1],[1.85,2.9],[2.12,3.25],[1.9,3.45],[0,3.45]],72);
  const points=geometry.getAttribute('position');
  for(let i=0;i<points.count;i++) {
    const p=new T.Vector3().fromBufferAttribute(points,i),angle=Math.atan2(p.y,p.x);
    const sway=.09*T.MathUtils.smoothstep(p.z,.4,1)*Math.sin(angle*3-p.z*2.4);
    points.setXYZ(i,p.x*(1+sway),p.y*(1+sway),p.z);
  }
  const controls=[{Id:'vortex',Position:[0,0,0],Rotation:[0,0,0,1]}];
  const contacts=floorContacts(0,()=>({AControl:'vortex',BControl:'vortex'}));
  contacts.push({Id:'wind_core',Part:0,A:[0,0,.3],B:[0,0,2.9],Radius:.80,AControl:'vortex',BControl:'vortex'});
  const run=motion('run',1.2,true,controls,48,(_,p)=>({rotation:new T.Quaternion().setFromAxisAngle(new T.Vector3(0,0,1),p*Math.PI*2).toArray()}));
  write('wind_vortex',[part('wind_body',geometry,'wind',[0,0,0],'vortex')],[],contacts,controls,[run],false,
    {Forward:[0,1,0],Up:[0,0,1],HideBelowGround:true},
    structure('volume','forms_remaining_fields.mjs','wind',{radius:2.5,height:3.45,cycle:1.2},
      {wind_body:['wind.flow','field.body']},[]));
}

/** 五项控制中的中心与相邻两个边缘共同承托连续暗面。 */
function yinWeights(p) {
  const radius=Math.min(1,Math.hypot(p.x,p.y)/2.5),angle=(Math.atan2(p.y,p.x)+Math.PI*2)%(Math.PI*2);
  const sector=angle/(Math.PI/2),a=Math.floor(sector),t=sector-a,edge=radius*radius;
  return [[0,1-edge],[a+1,edge*(1-t)],[(a+1)%4+1,edge*t]];
}

/** 玄阴以低伏连续暗面向内收束，起伏来自自身控制，不堆叠刀刃或换色毒雾。 */
function yin() {
  const geometry=body([[0,.02],[2.25,.02],[2.5,.12],[2.5,.28],[2.25,.40],[0,.30]]),points=geometry.getAttribute('position');
  for(let i=0;i<points.count;i++) {
    const p=new T.Vector3().fromBufferAttribute(points,i),r=Math.min(1,Math.hypot(p.x,p.y)/2.5);
    points.setZ(i,p.z+.055*r*r*T.MathUtils.smoothstep(p.z,.20,.35)*Math.cos(Math.atan2(p.y,p.x)*3));
  }
  const controls=[{Id:'centre',Position:[0,0,0],Rotation:[0,0,0,1]},...Array.from({length:4},(_,i)=>
    ({Id:`rim_${i}`,Position:[Math.cos(i*Math.PI/2)*2,Math.sin(i*Math.PI/2)*2,0],Rotation:[0,0,0,1]}))];
  skin(geometry,yinWeights);
  const bind=p=>yinWeights(new T.Vector3(...p)).map(([i,Weight])=>({Control:controls[i].Id,Weight}));
  const contacts=floorContacts(0,(a,b)=>({AInfluences:bind(a),BInfluences:bind(b)}));
  const run=motion('run',2.4,true,controls,48,(i,p,c)=>({position:[c.Position[0],c.Position[1],i?.015*Math.sin(p*Math.PI*2+i):0]}));
  write('yin_field',[part('yin_surface',geometry,'yin_depth')],[],contacts,controls,[run],false,
    {Forward:[0,1,0],Up:[0,0,1],HideBelowGround:true},
    structure('surface','forms_remaining_fields.mjs','yin',{radius:2.5,height:.455,cycle:2.4},
      {yin_surface:['yin.surface','field.body']},[]));
}

/** 一次导出两种剩余的原攻击领域。 */
export function makeRemainingFields(){wind();yin();}
