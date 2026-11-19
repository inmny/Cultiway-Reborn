/** 地面推进使用独立厚体；骨架同时驱动网格和接触，沿既有地面投送移动。 */
import * as T from 'three';
import {part,write,structure,motion,formation,forming,skin} from './form_export.mjs';
import {wallPart,column} from './forms_solid_walls.mjs';
import {waterCurve,thickSheet,curveInfluences} from './form_curve.mjs';
import {woodSprig} from './forms_wood_detail.mjs';

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

/** 灵藤主根先探、侧根后张，细梗及叶跟随原枝；根芯只在实际长到的位置接触。 */
function wood() {
  const controls=[],parts=[],contacts=[],sprigs=[];
  const up=new T.Vector3(0,1,0);
  for(let branch=0;branch<3;branch++) {
    const id=`root_${branch}`,side=branch-1,main=side===0;
    controls.push({Id:id,Position:[0,0,0],Rotation:[0,0,0,1]});
    const point=t=>new T.Vector3(side*.83*t+.06*Math.sin(t*8+branch)*Math.sin(t*Math.PI),.18+.17*Math.sin(t*Math.PI),-.54+1.13*t);
    const start=main?0:.16+branch*.035, end=main?.70:.89;
    const progress=t=>T.MathUtils.lerp(start,end,t);
    const rings=Array.from({length:25},(_,i)=>{
      const t=i/24,p=point(t),cap=T.MathUtils.smoothstep(t,8/9,1);
      const r=T.MathUtils.lerp(.16+(main?.11:.06)*Math.pow(1-t,.7),.015,cap);
      return Array.from({length:10},(_,j)=>{
        const a=j*Math.PI/5,d=r*(1+.08*Math.sin(j*2.7+branch+t*16));
        return [p.x+Math.cos(a)*d,p.y+Math.sin(a)*d*.9,p.z];
      });
    });
    const root=column(rings,10), along=p=>T.MathUtils.clamp((p.z+.54)/1.13,0,1);
    formation(root,p=>progress(along(p)),p=>point(along(p)));
    parts.push(wallPart(id,skin(root,()=>[[branch,1]]),'wood'));
    for(let i=0;i<8;i++) {
      const a=i/9,b=(i+1)/9;
      contacts.push({Id:`${id}_${i}`,Part:branch,A:point(a).toArray(),B:point(b).toArray(),Radius:.13,AControl:id,BControl:id,
        BuildRange:[.96*progress(a),.96*progress(b)+.04],BuildAlong:true});
    }
    for(const [n,t] of (main?[.65]:[.38,.68]).entries()) {
      const base=point(t),direction=side||1;
      const path=[base,base.clone().add(new T.Vector3(direction*.11,.11,.13)),
        base.clone().add(new T.Vector3(direction*(n===0?.27:.20),.20,.23))];
      const begin=progress(t)+.05;
      sprigs.push(...woodSprig(`${id}_sprig_${n}`,branch,path,begin,Math.min(.85,begin+.20),up));
    }
  }
  const clip=forming(motion('travel',.9,false,controls,36,(i,p)=>{
    // 主根稳住中心；侧根像从同一根颈掰开，离地的叶梢随根轻摆。
    const opened=T.MathUtils.smoothstep(p,.02+i*.025,.38),release=Math.sin(Math.PI*p);
    const yaw=(i-1)*(-.33*(1-opened)+.10*release);
    return {rotation:new T.Quaternion().setFromAxisAngle(up,yaw).toArray()};
  }), p=>T.MathUtils.smoothstep(p,0,.44));
  parts.push(...sprigs);
  write('creeping_vine',parts,[{Id:'root',Part:0,Position:[0,0,0],Direction:[0,0,1],Control:controls[0].Id}],
    contacts,controls,[clip],false,{Forward:[0,0,1],Up:[0,1,0],HideBelowGround:true},
    structure('branch','forms_ground_waves.mjs','makeGroundWaves',{roots:3,sharedBase:true},
      Object.fromEntries(parts.map(p=>[p.Id,[p.Id.endsWith('_leaf')?'branch.leaf':p.Id.includes('sprig')?'branch.shoot':p.Id==='root_1'?'branch.trunk':'branch.lateral']])),[]));
}

/** 一次制作三种原地面推进来源。 */
export function makeGroundWaves(){earth();water();wood();}
