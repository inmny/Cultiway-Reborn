/** 地面推进使用独立厚体；骨架同时驱动网格和接触，沿既有地面投送移动。 */
import * as T from 'three';
import {part,write,structure,motion} from './form_export.mjs';
import {wallPart,column} from './forms_solid_walls.mjs';
import {waterCurve,thickSheet,curveInfluences} from './form_curve.mjs';

/** 发布普通动作和地面遮挡，不引入独立播放循环。 */
function publish(id,parts,contacts,controls,clip,parameters) {
  write(id,parts,[{Id:'root',Part:0,Position:[0,0,0],Direction:[0,0,1],Control:controls[0].Id}],
    contacts,controls,[clip],false,{Forward:[0,0,1],Up:[0,1,0],HideBelowGround:true},
    structure('articulated','forms_ground_waves.mjs','makeGroundWaves',parameters,
      Object.fromEntries(parts.map(p=>[p.Id,['ground.front','projectile.body']])),[]));
}

/** 土脊由错落封闭石锋叠在连续石床上，局部石锋错时承接，避免整座山上下抽动。 */
function earth() {
  const controls=[{Id:'bed',Position:[0,0,0],Rotation:[0,0,0,1]}];
  const parts=[wallPart('bed',new T.BoxGeometry(2.18,.14,.92).translate(0,.07,0),'stone',[0,0,0],'bed')],contacts=[];
  for(let i=0;i<5;i++) {
    const x=(i-2)*.43,height=.78+.20*Math.cos(i*1.9),id=`slab_${i}`;
    controls.push({Id:id,Position:[x,0,0],Rotation:[0,0,0,1]});
    const rings=[[0,.30],[.16,.32],[height*.72,.26],[height,.025]].map(([y,r],row)=>Array.from({length:8},(_,j)=>{
      const a=j*Math.PI/4;return [x+Math.cos(a)*r,y+.035*Math.sin(i+j)*row/3,Math.sin(a)*r*1.42+.07*Math.sin(i*2)];
    }));
    // 截面按局部正Y上行，顺序与正Z长体相反。
    const geometry=column(rings,8);const index=geometry.index;
    for(let n=0;n<index.count;n+=3){const v=index.getX(n);index.setX(n,index.getX(n+2));index.setX(n+2,v);}
    parts.push(wallPart(id,geometry,'stone',[x,0,0],id));
    contacts.push({Id:id,Part:i+1,A:[x,.23,-.09],B:[x,height*.68,.09],Radius:.20,AControl:id,BControl:id});
  }
  const clip=motion('travel',.9,false,controls,30,(i,p,c)=>({position:[c.Position[0],i?.035*Math.sin(p*Math.PI*2+i*.8):0,0]}));
  publish('earth_ridge',parts,contacts,controls,clip,{width:2.18,slabs:5,duration:.9});
}

/** 潮浪保留连续厚水层；卷唇前翻而浪脚稳定，实际作用随同一曲线弯曲。 */
function water() {
  const count=7;
  const curve=p=>waterCurve([[-.48,.11],[-.33,.19],[-.15,.48],[.04,.83],[.24,.98],[.43,.85-.19*p],[.48+.19*p,.62-.28*p]]);
  const rest=curve(0),basis=p=>[-p.x,p.z,p.y],controls=Array.from({length:count},(_,i)=>({Id:`water_${i}`,Position:basis(rest.getPoint(i/(count-1))),Rotation:[0,0,0,1]}));
  const width=t=>1.14-.09*t,thickness=t=>.16-.06*t;
  const geometry=thickSheet(rest,{width,thickness,controls:count,rows:32,columns:24,foam:(t,x,face)=>T.MathUtils.smoothstep(t,.72,.98)*(face?.28:.75)});
  // 整片浪体只做骨架前翻，不声明逐步生成。
  geometry.deleteAttribute('formBuild');
  geometry.rotateX(-Math.PI/2).rotateY(Math.PI);
  const body=part('water_body',geometry,'water'),contacts=[];
  const influences=t=>curveInfluences(t,count).map(([index,Weight])=>({Control:controls[index].Id,Weight}));
  for(let i=0;i<=10;i++) {
    const t=i/10,point=rest.getPoint(t),a=basis(new T.Vector3(-width(t)+.15,point.y,point.z)),b=basis(new T.Vector3(width(t)-.15,point.y,point.z));
    contacts.push({Id:`front_${i}`,Part:0,A:a,B:b,Radius:.12,AInfluences:influences(t),BInfluences:influences(t)});
  }
  const clip=motion('travel',.9,false,controls,36,(i,p)=>{
    const t=i/(count-1),current=curve(Math.sin(p*Math.PI));
    const oldDirection=new T.Vector3(...basis(rest.getTangent(t))),newDirection=new T.Vector3(...basis(current.getTangent(t)));
    return {position:basis(current.getPoint(t)),rotation:new T.Quaternion().setFromUnitVectors(oldDirection,newDirection).toArray()};
  });
  // 末段沿原卷唇姿态散去，只改变透明度，不缩小真实接触或提前失效。
  clip.Keys = clip.Keys.map(key => ({...key, Fade:T.MathUtils.smoothstep(key.At,.72,.9)}));
  publish('tidal_wave',[body],contacts,controls,clip,{width:2.28,controls:count,duration:.9,rolledLip:true});
}

/** 灵藤是共根分开的三条低伏厚根；各条曲芯既是可见主体，也是实际接触。 */
function wood() {
  const controls=[],parts=[],contacts=[];
  for(let branch=0;branch<3;branch++) {
    const id=`root_${branch}`,side=branch-1;controls.push({Id:id,Position:[0,0,0],Rotation:[0,0,0,1]});
    const point=t=>new T.Vector3(side*.83*t+.06*Math.sin(t*8+branch)*Math.sin(t*Math.PI),.18+.17*Math.sin(t*Math.PI),-.54+1.13*t);
    const rings=Array.from({length:25},(_,i)=>{
      const t=i/24,p=point(t),r=.19*Math.pow(1-t,.35)+.012;
      return Array.from({length:10},(_,j)=>{const a=j*Math.PI/5;return [p.x+Math.cos(a)*r,p.y+Math.sin(a)*r,p.z];});
    });
    parts.push(wallPart(id,column(rings,10),'wood',[0,0,0],id));
    for(let i=0;i<8;i++)contacts.push({Id:`${id}_${i}`,Part:branch,A:point(i/9).toArray(),B:point((i+1)/9).toArray(),Radius:.13,AControl:id,BControl:id});
  }
  const clip=motion('travel',.9,false,controls,30,(i,p)=>({rotation:new T.Quaternion().setFromAxisAngle(new T.Vector3(0,1,0),.055*Math.sin(p*Math.PI*2+i*1.5)).toArray()}));
  publish('creeping_vine',parts,contacts,controls,clip,{roots:3,duration:.9,sharedBase:true});
}

/** 一次制作三种原地面推进来源。 */
export function makeGroundWaves(){earth();water();wood();}
