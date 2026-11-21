/** 支持法术：叶芽、承托叶面和宽厚净化波，各自保留独立制作结构与真实接触。 */
import * as T from 'three';
import {part, write, motion, dissolve, surfaceData, structure} from './form_export.mjs';

/** 本组直接使用 Three.js 几何，输出时换为 Unity 正面顺序，保留朝外法线。 */
function supportPart(...args) {
  const result=part(...args);
  for(let i=0;i<result.Triangles.length;i+=3)
    [result.Triangles[i],result.Triangles[i+2]]=[result.Triangles[i+2],result.Triangles[i]];
  return result;
}

/** 厚叶从中脊到边缘连续收薄，不使用刀锋、细线或烟团。 */
function leaf(length,width,height,angle,centre) {
  const g=new T.SphereGeometry(1,28,16),position=g.getAttribute('position');
  for(let i=0;i<position.count;i++) {
    const p=new T.Vector3().fromBufferAttribute(position,i),u=p.y;
    const x=p.x*width*(.82+.18*u),y=u*length,z=p.z*height+.09*(1-u*u);
    position.setXYZ(i,x*Math.cos(angle)-y*Math.sin(angle)+centre[0],x*Math.sin(angle)+y*Math.cos(angle)+centre[1],z+centre[2]);
  }
  return g;
}

/** 单体恢复载体是三片短叶包住玉芽，完整主体追踪实际友军。 */
function makeSeed() {
  const controls=[{Id:'bud',Position:[0,0,0],Rotation:[0,0,0,1]}],parts=[];
  for(let i=0;i<3;i++)parts.push(supportPart(`leaf_${i}`,leaf(.48,.16,.13,i*Math.PI*2/3,[0,0,0]),'restoration_jade',[0,0,0],'bud'));
  parts.push(supportPart('seed',new T.SphereGeometry(.20,24,16).scale(1,1,1.35),'purification_pearl',[0,0,0],'bud'));
  const run=motion('run',1.2,true,controls,32,(i,p)=>({position:[0,0,.02*Math.sin(p*Math.PI*2)]}));
  const release=dissolve(controls,(_,p)=>({position:[0,0,.12*p]}));
  parts.forEach(p=>p.UseMatterSurface=false);
  write('healing_seed',parts,[],[{Id:'bud',Part:3,A:[0,0,-.14],B:[0,0,.14],Radius:.17,AControl:'bud',BControl:'bud'}],controls,[run,release],false,
    {Forward:[0,1,0],Up:[0,0,1]},structure('articulated','forms_support.mjs','makeSeed',{leaves:3,length:.48,width:.16},
      Object.fromEntries(parts.map(p=>[p.Id,['support.carrier']])),[]));
}

/** 六支贴地叶脉各自承托既有接触区。叶尖和根茎逐渐收薄，中央留出受术者的轮廓。 */
function makeBloom(whole=false) {
  const controls=[],parts=[],contacts=[],veins=[];
  for(let i=0;i<6;i++) {
    const angle=i*Math.PI/3,dir=[Math.cos(angle),Math.sin(angle)],centre=[dir[0]*1.05,dir[1]*1.05,.24],id=`leaf_${i}`;
    controls.push({Id:id,Position:centre,Rotation:[0,0,0,1]});
    const g=new T.SphereGeometry(1,34,18),p=g.getAttribute('position');
    for(let k=0;k<p.count;k++) {
      const u=p.getY(k),t=.5+.5*u,v=p.getX(k),w=p.getZ(k);
      const r=.35+2.04*t,arc=.10*Math.sin(Math.PI*t*1.2);
      const width=v*(.18+.12*Math.sin(Math.PI*t))*(1-.16*Math.pow(Math.cos(t*5*Math.PI),2));
      p.setXYZ(k,dir[0]*r-dir[1]*(width+arc),dir[1]*r+dir[0]*(width+arc),.125+w*.044+(1-t)*.037);
    }
    g.computeVertexNormals();
    parts.push(supportPart(id,g,'living_leaf',centre,id));
    const a=[dir[0]*.35,dir[1]*.35,.24],b=[dir[0]*2.17,dir[1]*2.17,.24];
    contacts.push({Id:id,Part:i,A:a,B:b,Radius:.23,AControl:id,BControl:id});
    // 将暖色叶心贴在每片厚叶的中部，连接而不向合拢面外画线。
    const sap=leaf(.52,.10,.025,angle-Math.PI/2,[dir[0]*1.31-dir[1]*.07,dir[1]*1.31+dir[0]*.07,.105]);
    veins.push(supportPart(`sap_${i}`,sap,'living_sap',centre,id));
  }
  controls.push({Id:'heart',Position:[0,0,.26],Rotation:[0,0,0,1]});
  parts.push(supportPart('heart',new T.SphereGeometry(.28,24,16).scale(1,1,.46).translate(0,0,.16),'living_sap',[0,0,.26],'heart'));
  contacts.push({Id:'heart',Part:6,A:[-.3,0,.26],B:[.3,0,.26],Radius:.24,AControl:'heart',BControl:'heart'});
  parts.push(...veins);
  if(whole) {
    const profile=[[0,.035],[1.15,.03],[2.3,.035],[2.5,.075],[2.43,.11],[1.9,.095],[.75,.13],[0,.13]];
    const growth=new T.LatheGeometry(profile.map(row=>new T.Vector2(...row)),80).rotateX(Math.PI/2);
    surfaceData(growth,.38,p=>{
      const a=Math.atan2(p.y,p.x),r=Math.hypot(p.x,p.y);
      return .39+.24*Math.cos(a*6-r*2)**2+.19*T.MathUtils.smoothstep(r,1.30,2.49);
    });
    controls.push({Id:'ground',Position:[0,0,0],Rotation:[0,0,0,1]});
    contacts.splice(0,contacts.length,{Id:'ground',Part:parts.length,A:[0,0,.11],AControl:'ground',
      Sector:{Radius:2.5,Angle:360,Direction:0,HalfThickness:.14}});
    parts.push(supportPart('ground',growth,'restoration_jade',[0,0,0],'ground'));
  }
  const run=motion('run',1,true,controls,40,(i,p,c)=>({position:[c.Position[0],c.Position[1],c.Position[2]+.025*Math.sin(p*Math.PI*2)]}));
  const release=dissolve(controls,(_,p,c)=>({position:[c.Position[0]*(1-.065*p),c.Position[1]*(1-.065*p),c.Position[2]-.065*p]}));
  parts.forEach(p=>p.UseMatterSurface=false);
  write(whole?'rejuvenation_ground':'rejuvenation_bloom',parts,[],contacts,controls,whole?[run,release]:[run],whole,
    {Forward:[0,1,0],Up:[0,0,1]},
    structure('articulated','forms_support.mjs','makeBloom',{leaves:6,radius:2.5,height:.5},Object.fromEntries(parts.map(p=>[p.Id,['support.surface']])),[]));
}

/** 旧净化在一个时点铺满整个圆面，宽厚的浅色波肩随余势向上翻起。 */
function makePurificationSurge() {
  const id='crest',controls=[{Id:id,Position:[0,0,0],Rotation:[0,0,0,1]}];
  const profile=[[0,.025],[.8,.025],[2.25,.026],[2.5,.055],[2.5,.135],[2.38,.25],
    [2.2,.41],[1.98,.38],[1.75,.235],[1.48,.153],[1.12,.125],[.48,.145],[0,.17]];
  const surface=new T.LatheGeometry(profile.map(row=>new T.Vector2(...row)),96).rotateX(Math.PI/2);
  const points=surface.getAttribute('position');
  for(let i=0;i<points.count;i++) {
    const x=points.getX(i),y=points.getY(i),z=points.getZ(i),r=Math.hypot(x,y);
    const a=Math.atan2(y,x),crest=T.MathUtils.smoothstep(z,.12,.40);
    points.setZ(i,z+crest*(.11*Math.sin(a*5+r*3)+.065*Math.cos(a*8)));
  }
  surfaceData(surface,p=>.23+.43*T.MathUtils.smoothstep(p.z,.1,.42),p=>{
    const margin=T.MathUtils.smoothstep(Math.hypot(p.x,p.y),1.62,2.32);
    return .32+.45*margin+.12*Math.sin(Math.atan2(p.y,p.x)*5)**2;
  });
  const mesh=supportPart(id,surface,'purification_pearl',[0,0,0],id);mesh.UseMatterSurface=false;
  const release=dissolve(controls,(_,p)=>({position:[0,0,.12*p]}));
  write('purification_surge',[mesh],[],[{Id:'wash',Part:0,A:[0,0,.10],AControl:id,
    Sector:{Radius:2.5,Angle:360,Direction:0,HalfThickness:.14}}],controls,[release],true,
    {Forward:[0,1,0],Up:[0,0,1],HideBelowGround:true},
    structure('surface','forms_support.mjs','makePurificationSurge',{radius:2.5,profile},{crest:['support.front']},[]));
}

/** 净化波有厚度；来源的普通生长步骤使宽面与接触一起展开，骨架只控制缓慢抬起。 */
function makeWave() {
  const id='wave',controls=[{Id:id,Position:[0,0,.32],Rotation:[0,0,0,1]}];
  const g=new T.TorusGeometry(2.08,.42,16,64).scale(1,1,.55).translate(0,0,.32);
  const body=supportPart(id,g,'purification_pearl',[0,0,.32],id);body.UseMatterSurface=false;
  const contacts=[];
  for(let i=0;i<48;i++) {
    const a=i*Math.PI*2/48,b=(i+1)*Math.PI*2/48;
    contacts.push({Id:`front_${i}`,Part:0,A:[Math.cos(a)*2.08,Math.sin(a)*2.08,.32],B:[Math.cos(b)*2.08,Math.sin(b)*2.08,.32],Radius:.225,AControl:id,BControl:id});
  }
  const run=motion('run',.9,false,controls,48,(i,p,c)=>{
    const progress=T.MathUtils.smoothstep(p,0,1);
    return {position:[0,0,.14+.18*progress]};
  });
  write('purification_wave',[body],[],contacts,controls,[run],false,{Forward:[0,1,0],Up:[0,0,1]},
    structure('surface','forms_support.mjs','makeWave',{radius:2.5,width:.84,duration:.9},{wave:['support.front']},[]));
}

/** 战意以宽厚相扣的赤铜结蓄力，主体及核心共用一个控制。 */
function makeBattle() {
  const controls=[{Id:'knot',Position:[0,0,0],Rotation:[0,0,0,1]}],parts=[];
  for(let i=0;i<2;i++) {
    const g=new T.TorusGeometry(.24,.095,12,32).rotateX((i===0?.28:-.28)*Math.PI).rotateZ(i*Math.PI/2);
    parts.push(supportPart(`loop_${i}`,g,'battle_copper',[0,0,0],'knot'));
  }
  parts.push(supportPart('heart',new T.IcosahedronGeometry(.18,2),'purification_pearl',[0,0,0],'knot'));
  const run=motion('run',.8,true,controls,32,(i,p)=>({position:[0,0,.018*Math.sin(p*Math.PI*2)],
    rotation:new T.Quaternion().setFromAxisAngle(new T.Vector3(0,0,1),.18*Math.sin(p*Math.PI*2)).toArray()}));
  const release=dissolve(controls,(_,p)=>({rotation:new T.Quaternion().setFromAxisAngle(new T.Vector3(0,0,1),.76*p).toArray()}));
  parts.forEach(p=>p.UseMatterSurface=false);
  write('battle_knot',parts,[],[{Id:'heart',Part:2,A:[0,0,0],B:[0,0,0],Radius:.15,AControl:'knot',BControl:'knot'}],controls,[run,release],false,
    {Forward:[0,1,0],Up:[0,0,1]},structure('articulated','forms_support.mjs','makeBattle',{radius:.335,loops:2},
      Object.fromEntries(parts.map(p=>[p.Id,['support.carrier']])),[]));
}

/** 守护印是厚实六角小印，不借用实体格挡盾的结构或行为。 */
function makeGuard() {
  const controls=[{Id:'seal',Position:[0,0,0],Rotation:[0,0,0,1]}],parts=[];
  parts.push(supportPart('body',new T.CylinderGeometry(.36,.36,.18,6).rotateX(Math.PI/2),'guard_enamel',[0,0,0],'seal'));
  parts.push(supportPart('rim',new T.TorusGeometry(.28,.038,8,6).translate(0,0,.09),'purification_pearl',[0,0,0],'seal'));
  parts.push(supportPart('heart',new T.SphereGeometry(.125,20,12).scale(1,1,.6).translate(0,0,.09),'guard_enamel',[0,0,0],'seal'));
  const contacts=[{Id:'centre',Part:0,A:[-.22,0,0],B:[.22,0,0],Radius:.08,AControl:'seal',BControl:'seal'}];
  for(const y of [-.14,.14])contacts.push({Id:`side_${y}`,Part:0,A:[-.13,y,0],B:[.13,y,0],Radius:.08,AControl:'seal',BControl:'seal'});
  const run=motion('run',1.2,true,controls,32,(i,p)=>({position:[0,0,.012*Math.sin(p*Math.PI*2)]}));
  const release=dissolve(controls,(_,p)=>({position:[0,0,-.095*p]}));
  parts.forEach(p=>p.UseMatterSurface=false);
  write('guard_seal',parts,[],contacts,controls,[run,release],false,{Forward:[0,1,0],Up:[0,0,1]},
    structure('articulated','forms_support.mjs','makeGuard',{radius:.36,depth:.18},Object.fromEntries(parts.map(p=>[p.Id,['support.carrier']])),[]));
}

/** 迅捷的宽翼片使用封闭圆弧截面和圆钝端部，不制作刃口或细线拖尾。 */
function makeHaste() {
  const controls=[{Id:'core',Position:[0,0,0],Rotation:[0,0,0,1]}],parts=[];
  parts.push(supportPart('core',new T.CylinderGeometry(.13,.13,.20,6).rotateX(Math.PI/2),'purification_pearl',[0,0,0],'core'));
  const outline=new T.Shape();outline.moveTo(.04,-.10);
  outline.bezierCurveTo(.20,-.18,.46,-.08,.49,.08);
  outline.bezierCurveTo(.53,.24,.40,.37,.28,.31);
  outline.bezierCurveTo(.16,.25,.18,.10,.04,.10);
  outline.quadraticCurveTo(-.02,0,.04,-.10);
  for(let i=0;i<3;i++) {
    const id=`vane_${i}`,g=new T.ExtrudeGeometry(outline,{depth:.12,steps:1,bevelEnabled:true,bevelThickness:.035,bevelSize:.035,bevelSegments:3,curveSegments:16});
    g.translate(0,0,-.06).rotateZ(i*Math.PI*2/3);
    controls.push({Id:id,Parent:'core',Position:[0,0,0],Rotation:[0,0,0,1]});
    parts.push(supportPart(id,g,'haste_gold',[0,0,0],id));
  }
  const run=motion('run',.65,true,controls,32,(i,p,c)=>({position:c.Position,
    rotation:new T.Quaternion().setFromAxisAngle(new T.Vector3(0,0,1),(i===0?.18:.12)*Math.sin(p*Math.PI*2+i*.7)).toArray()}));
  const release=dissolve(controls,(i,p,c)=>({position:[c.Position[0],c.Position[1],p*.085*i]}));
  parts.forEach(p=>p.UseMatterSurface=false);
  write('haste_vanes',parts,[],[{Id:'core',Part:0,A:[0,0,-.02],B:[0,0,.02],Radius:.075,AControl:'core',BControl:'core'}],controls,[run,release],false,
    {Forward:[0,1,0],Up:[0,0,1]},structure('articulated','forms_support.mjs','makeHaste',{vanes:3,depth:.19},
      Object.fromEntries(parts.map(p=>[p.Id,['support.carrier']])),[]));
}

/** 柔软接合面由几个宽厚、相扣的组织片围住实际人物，不描发光细线。 */
function makeLifeTissue() {
  const parts=[];
  for(let i=0;i<5;i++) {
    const angle=i*Math.PI*2/5;
    const g=new T.SphereGeometry(1,22,12),p=g.getAttribute('position');
    for(let k=0;k<p.count;k++) {
      const u=p.getY(k),v=p.getX(k),w=p.getZ(k),a=angle+.78*u+.39*v;
      const radius=.47+.051*w+.13*(1-u*u);
      p.setXYZ(k,radius*Math.cos(a),radius*Math.sin(a),.53+.29*u+.055*v);
    }
    g.computeVertexNormals();
    parts.push(supportPart(`fibre_${i}`,g,i%2?'living_tissue':'living_sap',[0,0,0]));
  }
  parts.push(supportPart('stitch',new T.SphereGeometry(.15,20,12).scale(1,.70,1.65).translate(.31,0,.58),'living_sap',[0,0,0]));
  parts.forEach(p=>p.UseMatterSurface=false);
  write('life_tissue_join',parts,[],[],[],[],false,{Forward:[0,1,0],Up:[0,0,1]},
    structure('volume','forms_support.mjs','makeLifeTissue',{height:.85},Object.fromEntries(parts.map(p=>[p.Id,['support.surface']])),[]));
}

/** 活木保留可见的木芯、枝节与成对嫩叶，用不同受光把它和组织分开。 */
function makeLifeWood() {
  const parts=[];
  const root=new T.SphereGeometry(.36,22,14).scale(1,1,.63).translate(0,0,.23);
  parts.push(supportPart('stock',root,'living_bark',[0,0,0]));
  for(let i=0;i<4;i++) {
    const a=i*Math.PI*.5+.30, d=new T.Vector3(Math.cos(a),Math.sin(a),0);
    const from=d.clone().multiplyScalar(.15).setZ(.15),to=d.clone().multiplyScalar(.54).setZ(.75+.09*(i%2));
    const stem=to.clone().sub(from), mid=from.clone().addScaledVector(stem,.5);
    const branch=new T.CylinderGeometry(.043,.10,stem.length(),10,3);
    branch.applyQuaternion(new T.Quaternion().setFromUnitVectors(new T.Vector3(0,1,0),stem.normalize())).translate(...mid.toArray());
    parts.push(supportPart(`branch_${i}`,branch,'living_bark',[0,0,0]));
    for(let j=0;j<2;j++) {
      const theta=a+(j===0?-.52:.64),centre=to.clone().multiplyScalar(.81+j*.18).setZ(to.z-.083+j*.13);
      const g=leaf(.27,.115,.049,theta-Math.PI/2,[0,0,0]);
      g.rotateY(.35*Math.sin(a)).rotateX(-.30*Math.cos(a)).translate(...centre.toArray());
      parts.push(supportPart(`leaf_${i}_${j}`,g,j===0?'restoration_jade':'living_sap',[0,0,0]));
    }
  }
  parts.forEach(p=>p.UseMatterSurface=false);
  write('life_wood_graft',parts,[],[],[],[],false,{Forward:[0,1,0],Up:[0,0,1]},
    structure('volume','forms_support.mjs','makeLifeWood',{branches:4,height:1.1},Object.fromEntries(parts.map(p=>[p.Id,['support.surface']])),[]));
}

/** 六个独立支持来源在共同资源入口发布。 */
export function makeSupport(){makeSeed();makeBloom();makeBloom(true);makeWave();makePurificationSurge();makeBattle();makeGuard();makeHaste();makeLifeTissue();makeLifeWood();}
