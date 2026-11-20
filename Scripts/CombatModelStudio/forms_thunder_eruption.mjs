/** 撞地之后的压痕、岩块及喷雷各使用自己的实体和出现时点。 */
import * as T from 'three';
import {formation,forming,motion,part,skin,structure,write} from './form_export.mjs';
import {rotation} from './form_skeleton.mjs';

const smooth=(p,a,b)=>T.MathUtils.smoothstep(p,a,b);
const identity=[0,0,0,1];
const rest=(Id,Position)=>({Id,Position,Rotation:identity});

/** 由同一圈不规则层面形成实际有深度的石体。 */
function rings(levels) {
  const points=[], uv=[];
  const n=levels[0].length;
  const tri=(a,b,c)=>{for(const v of [a,b,c]) {points.push(...v); uv.push(.5+v[0]*.37,.5+v[1]*.37+v[2]*.12);}};
  for(let k=0;k<levels.length-1;k++)for(let i=0;i<n;i++){
    const j=(i+1)%n, a=levels[k][i], b=levels[k][j], c=levels[k+1][i], d=levels[k+1][j];
    tri(a,b,c);tri(b,d,c);
  }
  for(let i=1;i<n-1;i++){tri(levels[0][0],levels[0][i+1],levels[0][i]);tri(levels.at(-1)[0],levels.at(-1)[i],levels.at(-1)[i+1]);}
  const geometry=new T.BufferGeometry();
  geometry.setAttribute('position',new T.Float32BufferAttribute(points,3));
  geometry.setAttribute('uv',new T.Float32BufferAttribute(uv,2));
  return geometry;
}

/** 石唇真实起伏，内床下沉；凹痕不会自行改变地图的高度或通行。 */
function makeBed() {
  const n=12, controls=[rest('floor',[0,0,0]),rest('rim',[0,0,0])];
  const rim=Array.from({length:n},(_,i)=>{
    const a=2*Math.PI*i/n,r=1+.10*Math.sin(2.4*i)+.065*Math.cos(4.3*i);
    return [Math.cos(a)*r,Math.sin(a)*r*.74];
  });
  const layer=(size,h,wobble=0)=>rim.map(([x,y],i)=>[x*size,y*size,h+wobble*Math.cos(i*2.4)]);
  const inside=layer(.51,-.025,.01), crest=layer(.92,.21,.043), base=layer(1.07,.06,.015);
  const lip=rings([base,crest,inside]);
  // 内外圈之间只保留环壁，不遮盖实际下陷的中心。
  const keep=(geometry,count)=>{
    const out=new T.BufferGeometry();
    for(const k of ['position','uv']){
      const a=geometry.getAttribute(k);
      out.setAttribute(k,new T.Float32BufferAttribute(a.array.slice(0,count*a.itemSize),a.itemSize));
    }
    return out;
  };
  const bank={...part('rim',skin(keep(lip,2*n*6),()=>[[1,1]]),'stone_strata'),UseMatterSurface:false};
  const floor={...part('floor',skin(rings([layer(.51,-.11),inside]),()=>[[0,1]]),'iron_crust'),UseMatterSurface:false};
  const clip=motion('press',1.18,false,controls,30,(i,p,c)=>{
    const age=1.18*p;
    const z=i===0 ? -.095*smooth(age,0,.13) : -.145*(1-smooth(age,.006,.095))-.09*smooth(age,.58,1.12);
    return {position:[0,0,z]};
  },[{Id:'compressed',At:.15}]);
  const fading={...clip,Keys:clip.Keys.map(key=>({...key,Fade:smooth(key.At,.75,1.18)}))};
  write('thunder_impact_bed',[bank,floor],[],[{Id:'imprint',Part:1,A:[0,0,-.05],HalfExtents:[.60,.59,.25],AControl:'floor'}],controls,[fading],false,
    {Forward:[0,-1,0],Up:[0,0,1]},structure('articulated','forms_thunder_eruption.mjs','makeBed',{rim},
      {rim:['terrain.crater.rim'],floor:['terrain.crater.floor']},[['floor','rim',[.55,0,-.02],false]]));
}

/** 不规则石楔先从裂缝上抬，随后连同姿态及上抬速度交给普通碎裂。 */
function makeStrata() {
  const parts=[],controls=[],contacts=[],links=[];
  const count=7;
  for(let i=0;i<count;i++) {
    const a=i*2*Math.PI/count, outward=new T.Vector3(Math.cos(a),Math.sin(a),0);
    const along=new T.Vector3(-outward.y,outward.x,0), r=.74+.08*Math.sin(i*1.9), h=.31+.14*(.5+.5*Math.sin(i*2.6));
    const pivot=outward.clone().multiplyScalar(r);pivot.z=.20;
    const name=`stratum_${i}`;
    controls.push(rest(name,pivot.toArray()));
    const outline=[[-.43,-.25],[-.24,-.38],[.31,-.33],[.49,-.06],[.28,.31],[-.31,.26]];
    const face=(scale,height,lift)=>outline.map(([x,y],j)=>pivot.clone().addScaledVector(along,x*scale)
      .addScaledVector(outward,y*scale).add(new T.Vector3(0,0,height+lift*Math.sin(i*1.71+j*2.1))).toArray());
    const g=rings([face(.84,-.20,.032),face(1,.02,.06),face(.61,h,.094)]);
    parts.push({...part(name,skin(g,()=>[[i,1]]),i%2===0?'stone_strata':'stone_ridge',pivot.toArray()),UseMatterSurface:false});
    const end=x=>pivot.clone().addScaledVector(along,x).add(new T.Vector3(0,0,.13)).toArray();
    contacts.push({Id:`edge_${i}`,Part:i,A:end(-.19),B:end(.19),Radius:.18,AControl:name,BControl:name});
    if(i>0) links.push([`stratum_${i-1}`,name,pivot.clone().addScaledVector(along,-.27).toArray()]);
  }
  const heave=motion('heave',.37,false,controls,26,(i,p,c)=>{
    const t=smooth(p,.01+i*.031,1), a=i*2*Math.PI/count, advance=.38*t;
    return {position:[c.Position[0]+Math.cos(a)*advance,c.Position[1]+Math.sin(a)*advance,
      c.Position[2]-.22+(.84+.12*Math.cos(i*2.6))*t],
      rotation:rotation(t*Math.sin(a)*22,-t*Math.cos(a)*22,Math.sin(i)*9*t)};
  },[{Id:'let_go',At:.296}]);
  write('thunder_strata',parts,[],contacts,controls,[heave],false,{Forward:[0,-1,0],Up:[0,0,1],HideBelowGround:true},
    structure('articulated','forms_thunder_eruption.mjs','makeStrata',{count},
      Object.fromEntries(parts.map(p=>[p.Id,['terrain.stratum','fracture.piece']])),links));
}

/** 连续雷体有窄芯及宽亮边，接触胶囊收在可见主流内。 */
function makeJet() {
  const channels=[
    [[0,0,.04,.40],[.12,-.03,.72,.50],[-.15,.08,1.53,.38],[.28,.04,2.56,.51],[.06,.12,3.63,.39],[.34,.04,4.51,.29],[.17,.11,5.42,.06]],
    [[1.13,.25,.06,.25],[1.35,.31,.8,.32],[1.28,.43,1.69,.29],[1.62,.39,2.55,.23],[1.58,.48,3.46,.065]],
    [[-1.25,-.39,.06,.27],[-1.42,-.32,.83,.35],[-1.67,-.39,1.73,.29],[-1.92,-.40,2.55,.22],[-2.25,-.53,3.13,.045]]
  ];
  const parts=[],controls=[],contacts=[];
  for(const [index,path] of channels.entries()) {
    const delay=index*.115, name=`stream_${index}`;
    const progress=z=>delay+z/path.at(-1)[2]*(.90-delay);
    controls.push(rest(name,[0,0,0]));
    const base=parts.length;
    for(const [suffix,size,surface] of [['body',1,'thunder_arc_fork'],['core',.47,'thunder_core'],['halo',1.22,'thunder_corona']]) {
      const g=rings(path.map(([x,y,z,r],row)=>Array.from({length:8},(_,j)=>{
        const a=Math.PI*j/4+.17*Math.sin(row*3.41+j*5.12), w=r*size*(.92+.08*Math.cos(j*3.52+row*1.5));
        return [x+Math.cos(a)*w,y+Math.sin(a)*w,z];
      })));
      formation(g,p=>progress(p.z)/.96,(p)=>new T.Vector3(T.MathUtils.lerp(path[0][0],p.x,.24),
        T.MathUtils.lerp(path[0][1],p.y,.24),path[0][2]));
      parts.push({...part(`${name}_${suffix}`,skin(g,()=>[[index,1]]),surface),UseMatterSurface:false});
    }
    for(let i=0;i<path.length-1;i++){
      const r=Math.min(path[i][3],path[i+1][3]);
      if(r<.08) continue;
      const A=path[i].slice(0,3),B=path[i+1].slice(0,3);
      contacts.push({Id:`${name}_${i}`,Part:base,A,B,Radius:r*.78,AControl:name,BControl:name,
        BuildRange:[progress(A[2]),progress(B[2])],BuildAlong:true});
    }
  }
  const burst=motion('burst',1.02,false,controls,36,(i,p,c)=>{
    const age=p*1.02,t=smooth(age,.28,.77), split=(i-1)*.13*t;
    return {position:[split,.05*Math.sin(i*2.4)*t,0],rotation:rotation(t*3,split*21,0)};
  },[{Id:'crown',At:.39}]);
  const clip=forming(burst,p=>smooth(p,0,.42));
  const fading={...clip,Keys:clip.Keys.map(k=>({...k,Fade:smooth(k.At,.52,1.02)}))};
  write('thunder_eruption',parts,[],contacts,controls,[fading],false,{Forward:[0,-1,0],Up:[0,0,1],HideBelowGround:true},
    structure('skinned','forms_thunder_eruption.mjs','makeJet',{channels},
      Object.fromEntries(parts.map(p=>[p.Id,['lightning.body','eruption.stream']]))));
}

export function makeThunderEruption() { makeBed(); makeStrata(); makeJet(); }
