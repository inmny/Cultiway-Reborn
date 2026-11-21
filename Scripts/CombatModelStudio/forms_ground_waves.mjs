/** 地面推进使用独立厚体；骨架同时驱动网格和接触，沿既有地面投送移动。 */
import * as T from 'three';
import {mergeGeometries} from 'three/addons/utils/BufferGeometryUtils.js';
import {part,write,structure,motion,formation,forming,skin,dissolve} from './form_export.mjs';
import {wallPart,column} from './forms_solid_walls.mjs';
import {waterCurve,thickSheet,curveInfluences} from './form_curve.mjs';
import {woodSprig} from './forms_wood_detail.mjs';

/** 发布普通动作和地面遮挡，不引入独立播放循环。 */
function publish(id,parts,contacts,controls,clip,parameters,roles={}) {
  write(id,parts,[{Id:'root',Part:0,Position:[0,0,0],Direction:[0,0,1],Control:controls[0].Id}],
    contacts,controls,Array.isArray(clip)?clip:[clip],false,{Forward:[0,0,1],Up:[0,1,0],HideBelowGround:true},
    structure('articulated','forms_ground_waves.mjs','makeGroundWaves',parameters,
      Object.fromEntries(parts.map(p=>[p.Id,roles[p.Id]??['ground.front','projectile.body']])),[]));
}

/** 切出正Y向封闭断面，每一级独立保留折角与受光面。 */
function ridgeStone(rings,sides) {
  const geometry=column(rings,sides),index=geometry.index;
  for(let n=0;n<index.count;n+=3) {
    const v=index.getX(n); index.setX(n,index.getX(n+2)); index.setX(n+2,v);
  }
  return geometry;
}

/** 岩片由埋入地脚绕轴俯转，各自的接触芯、层面和石块始终同姿态。 */
function earth() {
  const controls=[{Id:'bed',Position:[0,0,0],Rotation:[0,0,0,1]}],contacts=[];
  const bed=ridgeStone([[-.37,.86], [-.13,1], [.032,.86]].map(([y,scale],row)=>Array.from({length:10},(_,j)=>{
    const a=j*Math.PI/5;
    return [1.09*Math.cos(a)*scale,y+.021*Math.sin(j*2.07+row),.50*Math.sin(a)*scale*(.90+.10*Math.cos(j*2.8))];
  })),10);
  const parts=[wallPart('bed',bed,'stone_ridge',[0,0,0],'bed')];
  for(let i=0;i<5;i++) {
    const x=(i-2)*.43,height=.78+.20*Math.cos(i*1.9),id=`slab_${i}`;
    const pivot=[x,0,0];
    controls.push({Id:id,Position:pivot,Rotation:[0,0,0,1]});
    const rings=[[-.13,.31],[.10,.34],[height*.47,.3],[height*.69,.245],[height*.86,.215],[height,.132]]
      .map(([y,r],row)=>Array.from({length:8},(_,j)=>{
        const a=j*Math.PI/4+.15*Math.sin(i*2.1),rough=1+.13*Math.sin(j*2.6+i*.9);
        const upper=row/5,breakPoint=Math.cos(j*2.11+i*.7);
        return [x+Math.cos(a)*r*rough+upper*.068*Math.cos(i*1.9),
          y+.034*breakPoint*upper,Math.sin(a)*r*(1.36+.18*Math.sin(i*1.7))+.056*Math.sin(i*2)+.11*upper];
      }));
    parts.push(wallPart(id,ridgeStone(rings,8),'stone_ridge',pivot,id));
    contacts.push({Id:id,Part:i+1,A:[x,.23,-.09],B:[x,height*.68,.09],Radius:.20,AControl:id,BControl:id});
  }
  const axis=new T.Vector3(1,0,0),riseOrder=[.07,.035,0,.052,.097];
  const travel=motion('travel',.9,false,controls,30,(i,p,c)=>{
    if(!i)return {};
    const begin=riseOrder[i-1],at=p*.9;
    const rise=T.MathUtils.smoothstep(at,begin,begin+.124);
    const seat=T.MathUtils.smoothstep(at,begin+.112,begin+.251);
    const lean=-.23*(1-rise)+(.19+.03*Math.sin(i*2.1))*(rise-seat);
    return {position:[c.Position[0],-.09*(1-rise),-.08*(1-rise)],rotation:new T.Quaternion().setFromAxisAngle(axis,lean).toArray()};
  });
  const collapse=motion('collapse',.34,false,controls,24,(i,p,c)=>{
    const wait=i===0?.23:.02+(5-i)%3*.029,at=p*.34;
    const tip=T.MathUtils.smoothstep(at,wait,wait+.188),sink=T.MathUtils.smoothstep(at,wait+.045,.34);
    if(!i)return {position:[0,-.44*sink,0]};
    const fractureAxis=new T.Vector3(.62,0,(i%2?1:-1)*.78).normalize();
    return {position:[c.Position[0]+(i-3)*.055*tip,-1.03*sink,.38*tip],
      rotation:new T.Quaternion().setFromAxisAngle(fractureAxis,(1.15+.15*Math.sin(i*2.1))*tip).toArray()};
  });
  publish('earth_ridge',parts,contacts,controls,[travel,collapse],{width:2.18,slabs:5,duration:.9,collapse:.34});
  publish('earth_ridge_0',parts,contacts,controls,[travel,collapse],{width:2.18,slabs:5,duration:.9,collapse:.34});
}

/** 厚浪只翻落一次：背部托起波峰，卷唇伸出，随后压成向前摊开的水脚。 */
function water() {
  const stages=[
    [[-.52,.12],[-.35,.24],[-.15,.48],[.08,.64],[.27,.64],[.41,.55],[.44,.43]],
    [[-.52,.12],[-.34,.23],[-.18,.55],[.00,.84],[.20,1.02],[.42,.99],[.54,.82]],
    [[-.52,.12],[-.30,.24],[-.06,.51],[.20,.73],[.43,.78],[.66,.58],[.73,.29]],
    [[-.52,.12],[-.29,.14],[.02,.21],[.26,.29],[.52,.26],[.75,.18],[.88,.13]]
  ];
  const stagesAt=[0,.28,.68,1],count=9;
  const rest=waterCurve(stages[1]);
  function curveAt(phase) {
    const i=phase<stagesAt[1]?0:phase<stagesAt[2]?1:2;
    const blend=T.MathUtils.smoothstep(phase,stagesAt[i],stagesAt[i+1]);
    return waterCurve(stages[i].map((point,j)=>point.map((v,axis)=>T.MathUtils.lerp(v,stages[i+1][j][axis],blend))));
  }
  const basis=p=>[-p.x,p.z,p.y];
  const controls=Array.from({length:count},(_,i)=>({Id:`water_${i}`,Position:basis(rest.getPoint(i/(count-1))),Rotation:[0,0,0,1]}));
  const width=t=>(1.14-.09*t)*(.54+.46*Math.pow(Math.sin(Math.PI*(.05+.91*t)),.74));
  const thickness=t=>.014+.23*Math.pow(1-t,.85)*T.MathUtils.smoothstep(t,0,.28)+.033*T.MathUtils.smoothstep(t,.62,.9);
  const edgeThickness=x=>.04+.96*Math.pow(Math.max(0,1-x*x),.55);
  /** 收薄水脚和两侧边缘，把上、中、下游的截面连成一片不规则水体。 */
  function section(point,t,x) {
    const rear=1-T.MathUtils.smoothstep(t,0,.42),lip=T.MathUtils.smoothstep(t,.38,.92);
    const crossWave=Math.sin(8*x-6*t)*Math.cos(3*x+t*2);
    point.y+=rear*(.24*x*x+.052*Math.sin(10*x+.6))+lip*(.062*Math.sin(5*x+.7)-.13*x*x);
    point.z+=lip*(.046*crossWave-.074*x*x)+.038*x*Math.sin(Math.PI*t);
  }
  const geometry=thickSheet(rest,{width,thickness,edgeThickness,controls:count,rows:40,columns:32,section,
    foam:(t,x,face)=>T.MathUtils.smoothstep(t,.72,.97)*(face?.12:.45)*(.55+.45*Math.pow(Math.sin(x*8+t*11),2))});
  // 这是已经形成的水体形变；接触与网格一同移动，不另设出现进度。
  geometry.deleteAttribute('formBuild');
  const orient=mesh=>mesh.rotateX(-Math.PI/2).rotateY(Math.PI);
  const patches=[],up=new T.Vector3(0,1,0);
  const vary=value=>T.MathUtils.euclideanModulo(Math.sin(value*127.1)*43758.5453,1);
  for(let i=0;i<32;i++) {
    const x=(vary(i+1)*2-1)*.92,t=.81+.18*vary(i+45);
    const tangent=rest.getTangent(t),normal=new T.Vector3(0,-tangent.z,tangent.y).normalize();
    const surface=rest.getPoint(t).addScaledVector(normal,thickness(t)*edgeThickness(x)+.006);
    surface.x=x*width(t);section(surface,t,x);
    const patch=new T.SphereGeometry(.015+.022*vary(i+80),7,5).scale(1.7,.23,.73)
      .applyQuaternion(new T.Quaternion().setFromUnitVectors(up,normal)).translate(...surface.toArray());
    skin(patch,()=>curveInfluences(t,count));patches.push(patch);
  }
  const parts=[part('water_body',orient(geometry),'water'),part('surface_foam',orient(mergeGeometries(patches)),'foam')];
  const contacts=[];
  const influences=t=>curveInfluences(t,count).map(([index,Weight])=>({Control:controls[index].Id,Weight}));
  for(let i=0;i<=10;i++) {
    const t=i/10;
    const endpoint=sign=>{
      const point=rest.getPoint(t);point.x=sign*(width(t)-.15);section(point,t,point.x/width(t));return basis(point);
    };
    contacts.push({Id:`front_${i}`,Part:0,A:endpoint(-1),B:endpoint(1),Radius:.12,AInfluences:influences(t),BInfluences:influences(t)});
  }
  function wavePose(i,p) {
    const t=i/(count-1),current=curveAt(p);
    const before=new T.Vector3(...basis(rest.getTangent(t))),now=new T.Vector3(...basis(current.getTangent(t)));
    return {position:basis(current.getPoint(t)),rotation:new T.Quaternion().setFromUnitVectors(before,now).toArray()};
  }
  const travel=motion('travel',.9,false,controls,48,wavePose);
  const oldTravel={...travel,Keys:travel.Keys.map(key=>({...key,Fade:T.MathUtils.smoothstep(key.At,.76,.9)}))};
  const ebb=dissolve(controls,(i,t)=>{
    const end=wavePose(i,1);
    end.position[1]-=.22*T.MathUtils.smoothstep(t,0,1);
    end.position[2]+=.16*t;
    return end;
  });
  const parameters={width:2.28,controls:count,duration:.9,stages,stagesAt};
  const roles={water_body:['ground.front','projectile.body','fluid.body'],surface_foam:['fluid.foam']};
  publish('tidal_wave',parts,contacts,controls,oldTravel,parameters,roles);
  publish('tidal_wave_0',parts,contacts,controls,[travel,ebb],parameters,roles);
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
  const withdraw=forming(dissolve(controls,(_i,t)=>({position:[0,-.19*t,0]})),
    t=>1-T.MathUtils.smoothstep(t,.05,.98));
  const anchors=[{Id:'root',Part:0,Position:[0,0,0],Direction:[0,0,1],Control:controls[0].Id}];
  const framing={Forward:[0,0,1],Up:[0,1,0],HideBelowGround:true};
  const construction=structure('branch','forms_ground_waves.mjs','makeGroundWaves',{roots:3,sharedBase:true},
    Object.fromEntries(parts.map(p=>[p.Id,[p.Id.endsWith('_leaf')?'branch.leaf':p.Id.includes('sprig')?'branch.shoot':p.Id==='root_1'?'branch.trunk':'branch.lateral']])),[]);
  write('creeping_vine',parts,anchors,contacts,controls,[clip],false,framing,construction);
  write('creeping_vine_0',parts,anchors,contacts,controls,[clip,withdraw],false,framing,construction);
}

/** 一次制作三种原地面推进来源。 */
export function makeGroundWaves(){earth();water();wood();}
