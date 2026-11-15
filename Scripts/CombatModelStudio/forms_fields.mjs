/** 持续领域：贴地积液、层次毒雾和不规则混沌体积，各自保留真实作用面。 */
import * as T from 'three';
import {part, write, skin, surfaceData, motion, structure} from './form_export.mjs';

/** 低矮积液的九点控制，显示和接触使用同一组地表承托。 */
function poolWeights(p) {
  const x=T.MathUtils.clamp((p.x/2.5+1),0,2),y=T.MathUtils.clamp((p.y/2.2+1),0,2);
  const a=Math.min(1,Math.floor(x)),b=Math.min(1,Math.floor(y)),u=x-a,v=y-b;
  return [[b*3+a,(1-u)*(1-v)],[b*3+a+1,u*(1-v)],[(b+1)*3+a,(1-u)*v],[(b+1)*3+a+1,u*v]];
}

/** 不规则积液具有封闭厚度，波纹只在宽面明暗上表达，不堆叠线圈。 */
function makePool() {
  const controls=[];
  for(let y=0;y<3;y++)for(let x=0;x<3;x++)controls.push({Id:`bed_${y}_${x}`,Position:[(x-1)*2.5,(y-1)*2.2,0],Rotation:[0,0,0,1]});
  const geometry=new T.SphereGeometry(1,48,16).rotateX(Math.PI/2);
  const position=geometry.getAttribute('position');
  for(let i=0;i<position.count;i++) {
    const p=new T.Vector3().fromBufferAttribute(position,i),a=Math.atan2(p.y,p.x);
    const edge=1+.055*Math.sin(a*3+.3)+.035*Math.cos(a*5);
    position.setXYZ(i,p.x*2.5*edge,p.y*2.2*edge,.15+p.z*.145);
  }
  skin(geometry,poolWeights);surfaceData(geometry,p=>Math.max(.035,p.z*2));
  const contacts=[],bind=p=>poolWeights(p).map(([i,Weight])=>({Control:controls[i].Id,Weight}));
  for(let row=-12;row<=12;row++) {
    const y=row*.16,width=2.5*Math.sqrt(Math.max(0,1-y*y/(2.2*2.2)))*.95-.16;
    const a=new T.Vector3(-width,y,.15),b=new T.Vector3(width,y,.15);
    contacts.push({Id:`liquid_${row+12}`,Part:0,A:a.toArray(),B:b.toArray(),Radius:.145,AInfluences:bind(a),BInfluences:bind(b)});
  }
  const run=motion('run',1.6,true,controls,32,(i,p,c)=>({position:[...c.Position.slice(0,2),.009*Math.sin(p*Math.PI*2+i*1.2)]}));
  const body=part('liquid',geometry,'poison_liquid');body.UseMatterSurface=false;
  write('poison_pool',[body],[],contacts,controls,[run],true,{Forward:[0,1,0],Up:[0,0,1]},
    structure('surface','forms_fields.mjs','makePool',{radius:[2.5,2.2],height:.295},{liquid:['fluid.body']},[]));
}

/** 具有真实体积的分层雾团，边缘透明由表面规则控制；实体和接触一起缓慢起伏。 */
function makeMist() {
  const lobes=[[-1.12,-.12,1.05,1.34],[.65,-.5,1.12,1.5],[.2,1.05,.92,1.25]];
  const controls=lobes.map(([x,y,z],i)=>({Id:`cloud_${i}`,Position:[x,y,z],Rotation:[0,0,0,1]}));
  const parts=[],contacts=[];
  lobes.forEach(([x,y,z,r],i)=>{
    const g=new T.SphereGeometry(r,28,18).translate(x,y,z);
    skin(g,()=>[[i,1]]);const outer=part(`cloud_${i}`,g,'poison_vapor');outer.UseMatterSurface=false;parts.push(outer);
    const inner=new T.SphereGeometry(r*.72,22,14).translate(x+.08,y-.08,z);
    skin(inner,()=>[[i,1]]);const core=part(`core_${i}`,inner,'poison_core');core.UseMatterSurface=false;parts.push(core);
    contacts.push({Id:`vapor_${i}`,Part:i*2,A:[x,y,z],B:[x,y,z],Radius:r*.96,AControl:controls[i].Id,BControl:controls[i].Id});
  });
  const run=motion('run',2.4,true,controls,48,(i,p,c)=>({position:[c.Position[0]+.035*Math.sin(p*Math.PI*2+i),c.Position[1],c.Position[2]+.055*Math.sin(p*Math.PI*2+i*1.8)]}));
  write('poison_mist',parts,[],contacts,controls,[run],false,{Forward:[0,1,0],Up:[0,0,1]},
    structure('volume','forms_fields.mjs','makeMist',{lobes},Object.fromEntries(parts.map(p=>[p.Id,['vapor.body']])),[]));
}

/** 混沌场用不规则暗腔和宽面层次，不复用裂隙、刀刃或围绕目标的细线。 */
function makeEntropy() {
  const lobes=[[0,0,1.25,1.65],[-1.1,.4,.85,1.1],[.95,-.45,1.05,1.3],[.35,1.1,1.6,1.05]];
  const controls=lobes.map(([x,y,z],i)=>({Id:`fold_${i}`,Position:[x,y,z],Rotation:[0,0,0,1]}));
  const parts=[],contacts=[];
  lobes.forEach(([x,y,z,r],i)=>{
    const geometry=new T.SphereGeometry(r,30,20),position=geometry.getAttribute('position');
    for(let v=0;v<position.count;v++) {
      const q=new T.Vector3().fromBufferAttribute(position,v);
      q.multiplyScalar(1+.06*Math.sin(q.x*2.3+.5)*Math.cos(q.z*2.8)*Math.sin(q.y*3.1));
      position.setXYZ(v,q.x+x,q.y+y,q.z+z);
    }
    skin(geometry,()=>[[i,1]]);
    const outer=part(`fold_${i}`,geometry,'entropy_veil');outer.UseMatterSurface=false;parts.push(outer);
    const core=new T.SphereGeometry(r*.66,24,16).translate(x,y,z);skin(core,()=>[[i,1]]);
    const inside=part(`depth_${i}`,core,'entropy_depth');inside.UseMatterSurface=false;parts.push(inside);
    contacts.push({Id:`chaos_${i}`,Part:i*2,A:[x,y,z],B:[x,y,z],Radius:r*.93,AControl:controls[i].Id,BControl:controls[i].Id});
  });
  const run=motion('run',3.2,true,controls,48,(i,p,c)=>({position:[c.Position[0]+.03*Math.sin(p*Math.PI*2+i),c.Position[1]+.03*Math.cos(p*Math.PI*2+i),c.Position[2]+.04*Math.sin(p*Math.PI*2+i*1.4)]}));
  write('entropy_field',parts,[],contacts,controls,[run],false,{Forward:[0,1,0],Up:[0,0,1]},
    structure('volume','forms_fields.mjs','makeEntropy',{lobes},Object.fromEntries(parts.map(p=>[p.Id,['entropy.volume']])),[]));
}

/** 独立的三种领域资源由共同资源发布入口构建。 */
export function makeFields(){makePool();makeMist();makeEntropy();}
