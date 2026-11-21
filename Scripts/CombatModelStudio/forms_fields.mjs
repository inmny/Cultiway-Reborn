/** 持续领域：贴地积液、层次毒雾和不规则混沌体积，各自保留真实作用面。 */
import * as T from 'three';
import {makeEntropyForms} from './forms_entropy.mjs';
import {part, write, skin, surfaceData, motion, dissolve, structure} from './form_export.mjs';

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
    const edge=1+.045*Math.sin(a*3+.3)+.038*Math.cos(a*7)+.022*Math.sin(a*11);
    const shelf=.72+.28*Math.sin(a*2.1+3*p.x)*Math.cos(4*p.y-.5);
    position.setXYZ(i,p.x*2.5*edge,p.y*2.2*edge,.15+p.z*.145*shelf);
  }
  skin(geometry,poolWeights);
  surfaceData(geometry,p=>Math.max(.035,p.z*2),p=>Math.min(1,Math.hypot(p.x/2.5,p.y/2.2)));
  const contacts=[],bind=p=>poolWeights(p).map(([i,Weight])=>({Control:controls[i].Id,Weight}));
  for(let row=-12;row<=12;row++) {
    const y=row*.16,width=2.5*Math.sqrt(Math.max(0,1-y*y/(2.2*2.2)))*.95-.16;
    const a=new T.Vector3(-width,y,.15),b=new T.Vector3(width,y,.15);
    contacts.push({Id:`liquid_${row+12}`,Part:0,A:a.toArray(),B:b.toArray(),Radius:.145,AInfluences:bind(a),BInfluences:bind(b)});
  }
  const run=motion('run',1.6,true,controls,32,(i,p,c)=>({position:[...c.Position.slice(0,2),.009*Math.sin(p*Math.PI*2+i*1.2)]}));
  const settle=motion('settle',2.1,false,controls,40,(i,p,c)=>({
    position:[...c.Position.slice(0,2),.03*Math.exp(-4*p)*Math.sin(10*p-i*.6)]}));
  settle.Keys.forEach(k=>{k.Fade=.65*T.MathUtils.smoothstep(k.At,1.72,2.1);});
  const release=dissolve(controls,(_,p,c)=>({position:[c.Position[0]*(1-.09*p),c.Position[1]*(1-.09*p),-.105*p]}));
  const body=part('liquid',geometry,'poison_film');body.UseMatterSurface=false;
  write('poison_pool',[body],[],contacts,controls,[run,settle,release],true,{Forward:[0,1,0],Up:[0,0,1]},
    structure('surface','forms_fields.mjs','makePool',{radius:[2.5,2.2],height:.295},{liquid:['fluid.body']},[]));
}

/** 一股宽雾托底，左右分出去的是低薄的余流。每层蜿蜒程度和宽厚都沿推进方向变化。 */
function mistTongue(index,inner) {
  const geometry=new T.SphereGeometry(1,40,32),position=geometry.getAttribute('position');
  // 顺序保持原挂点。主体跨过原三个接触区，两侧只填偏薄的边缘。
  const [x,y,length,width,low,opacity]=[
    [-1.23,-.19,1.88,.96,.41,.44],[-.13,-.08,2.29,2.06,.25,.91],[1.13,.45,1.81,1.09,.35,.48]
  ][index];
  for(let i=0;i<position.count;i++) {
    const p=new T.Vector3().fromBufferAttribute(position,i),t=(p.y+1)*.5;
    const crest=Math.exp(-Math.pow((t-.78)*4.1,2));
    const slant=.21*Math.sin(4.7*t+index*.72)+.15*(t-.45);
    const breadth=(.57+.48*Math.pow(t,.67))*(1+.09*Math.sin(17*t+index*.9));
    const taper=inner?.73:1;
    const side=p.x*width*breadth*taper;
    const rim=.13*Math.sin(14*t+4.2*p.x)+.075*Math.sin(23*t-3*p.x);
    const lift=inner?.51:1;
    position.setXYZ(i,x+side+slant,y+length*(p.y+.16*Math.abs(p.x)*crest+rim*.38),
      low+(.35+.68*crest+.42*p.z*(.86+.14*Math.cos(12*t+3*p.x))+rim)*lift);
  }
  skin(geometry,()=>[[index,1]]);
  surfaceData(geometry,inner?.69:.36,p=>{
    const t=T.MathUtils.clamp((p.y-y)/length*.5+.5,0,1),edge=Math.abs((p.x-x)/width);
    const tail=.22+.78*T.MathUtils.smoothstep(t,.015,.46);
    const margin=1-.79*T.MathUtils.smoothstep(edge,.51,1.06);
    return opacity*tail*margin*(1-.63*T.MathUtils.smoothstep(p.z,.81,1.63));
  });
  return geometry;
}

/** 保留旧雾的三个接触体，在同样的运动上换用薄层与毒舌。 */
function makeMist() {
  const lobes=[[-1.12,-.12,1.05,1.34],[.65,-.5,1.12,1.5],[.2,1.05,.92,1.25]];
  const controls=lobes.map(([x,y,z],i)=>({Id:`cloud_${i}`,Position:[x,y,z],Rotation:[0,0,0,1]}));
  const parts=[],contacts=[];
  lobes.forEach(([x,y,z,r],i)=>{
    const outer=part(`cloud_${i}`,mistTongue(i,false),'poison_vapor');outer.UseMatterSurface=false;parts.push(outer);
    const core=part(`core_${i}`,mistTongue(i,true),'poison_core');core.UseMatterSurface=false;parts.push(core);
    contacts.push({Id:`vapor_${i}`,Part:i*2,A:[x,y,z],B:[x,y,z],Radius:r*.96,AControl:controls[i].Id,BControl:controls[i].Id});
  });
  const run=motion('run',2.4,true,controls,48,(i,p,c)=>({position:[c.Position[0]+.035*Math.sin(p*Math.PI*2+i),c.Position[1],c.Position[2]+.055*Math.sin(p*Math.PI*2+i*1.8)]}));
  const breath=motion('breath',1.85,false,controls,44,(i,p,c)=>({position:[
    c.Position[0]+.064*Math.sin(10*p+i),c.Position[1]+.12*Math.sin(8*p-i*.8),
    c.Position[2]+.115*Math.sin(12*p-i*.95)*Math.sin(p*Math.PI)]}));
  breath.Keys.forEach(k=>{k.Fade=.68*T.MathUtils.smoothstep(k.At,1.22,1.85);});
  const release=dissolve(controls,(_,p,c)=>({position:[c.Position[0]*(1+.11*p),c.Position[1]-.16*p,c.Position[2]+.09*p]}));
  write('poison_mist',parts,[],contacts,controls,[run,breath,release],false,{Forward:[0,1,0],Up:[0,0,1]},
    structure('volume','forms_fields.mjs','makeMist',{lobes},Object.fromEntries(parts.map(p=>[p.Id,['vapor.body']])),[]));
}

/** 局部金属受损处的浅蚀孔，内部暗、边缘残留黏膜；不含接触体。 */
function makePits() {
  const pits=[[-.21,.20,.21],[.14,.01,.29],[.035,-.33,.14]];
  const parts=pits.map(([x,z,r],i)=>{
    const geometry=new T.SphereGeometry(1,16,8).rotateX(Math.PI/2);
    const positions=geometry.getAttribute('position');
    for(let j=0;j<positions.count;j++) {
      const p=new T.Vector3().fromBufferAttribute(positions,j),a=Math.atan2(p.y,p.x);
      const radial=1+.15*Math.sin(a*5+i)+.08*Math.cos(a*3);
      positions.setXYZ(j,x+r*p.x*radial,.03*p.z,z+r*p.y*radial*1.32);
    }
    surfaceData(geometry,.16,p=>Math.min(1,Math.hypot(p.x-x,(p.z-z)/1.32)/r));
    const body=part(`pit_${i}`,geometry,'poison_etch');body.UseMatterSurface=false;return body;
  });
  write('poison_pit',parts,[],[],[],[],false,null,
    structure('surface','forms_fields.mjs','makePits',{pits},Object.fromEntries(parts.map(p=>[p.Id,['erosion.mark']])),[]));
}

/** 领域资源由共同发布入口构建。 */
export function makeFields(){makePool();makeMist();makePits();makeEntropyForms();}
