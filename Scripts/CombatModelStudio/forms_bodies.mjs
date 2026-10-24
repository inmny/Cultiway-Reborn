/** 巨神和独立灵臂共用连续表面、骨架、动作与作用权重，不新增运行时播放类型。 */
import * as T from 'three';
import {mergeGeometries} from 'three/addons/utils/BufferGeometryUtils.js';
import {part,write,motion,structure} from './form_export.mjs';
import {controlsFromPoints,blendWeights,rotation,phaseValue,implicitParts,influences} from './form_skeleton.mjs';

/** 连续体积的制作输入，部位归属只用于切分语义，不改变共同表面。 */
const volume=(Group,Center,Radius)=>({Group,Center,Radius});
const smooth=(value,a,b)=>T.MathUtils.smoothstep(value,a,b);

/** 制作装饰仍保留有厚度的几何；不把面甲画成一条发光线。 */
function plate(points,depth,y,holes=[]) {
  const shape=new T.Shape(points.map(p=>new T.Vector2(...p)));
  for(const [x,z,rx,rz] of holes){const hole=new T.Path();hole.absellipse(x,z,rx,rz,0,Math.PI*2,true);shape.holes.push(hole);}
  return new T.ExtrudeGeometry(shape,{depth,bevelEnabled:true,bevelSize:.012,bevelThickness:.012,bevelSegments:2,curveSegments:16})
    .rotateX(Math.PI/2).translate(0,y,0);
}

/** 根据控制点高度在同一躯干内连续混合，不随部件名称更换权重算法。 */
function heightWeights(z,indices,heights) {
  if(z<=heights[0])return [[indices[0],1]];
  for(let i=0;i<heights.length-1;i++)if(z<=heights[i+1]) {
    const t=(z-heights[i])/(heights[i+1]-heights[i]);return [[indices[i],1-t],[indices[i+1],t]];
  }
  return [[indices.at(-1),1]];
}

/** 躯干、四肢和面部拥有明确实际部位；所有接合处读取相同权重。 */
function makeColossus() {
  const entries=[['pelvis',null,[0,0,1.9]],['spine','pelvis',[0,0,2.45]],['chest','spine',[0,0,3.1]],
    ['neck','chest',[0,0,3.7]],['head','neck',[0,0,4.08]]];
  for(const side of ['left','right']) {
    const sign=side==='left'?1:-1;
    entries.push([`clavicle_${side}`,'chest',[sign*.63,0,3.28]],[`upper_${side}`,`clavicle_${side}`,[sign*1.05,0,3.22]],
      [`lower_${side}`,`upper_${side}`,[sign*1.13,0,2.37]],[`hand_${side}`,`lower_${side}`,[sign*1.16,-.01,1.61]]);
  }
  for(const side of ['left','right']) {
    const sign=side==='left'?1:-1;
    entries.push([`thigh_${side}`,'pelvis',[sign*.36,0,1.78]],[`shin_${side}`,`thigh_${side}`,[sign*.38,0,.88]],
      [`foot_${side}`,`shin_${side}`,[sign*.38,-.08,.12]]);
  }
  const controls=controlsFromPoints(entries),index=Object.fromEntries(controls.map((c,i)=>[c.Id,i]));
  const components=[volume(0,[0,0,1.94],[.55,.30,.46]),volume(0,[0,0,2.42],[.48,.29,.48]),
    volume(0,[0,-.025,2.98],[.72,.35,.52]),volume(1,[0,0,3.65],[.20,.22,.35]),volume(1,[0,-.01,4.10],[.31,.28,.42])];
  for(const sign of [1,-1]) {
    const arm=sign>0?2:3,leg=sign>0?4:5;
    components.push(volume(arm,[sign*.82,0,3.18],[.41,.31,.36]),volume(arm,[sign*1.09,0,2.83],[.235,.24,.54]),
      volume(arm,[sign*1.15,-.01,1.98],[.20,.20,.49]),volume(arm,[sign*1.16,-.03,1.47],[.23,.19,.26]),
      volume(leg,[sign*.36,0,1.38],[.255,.28,.58]),volume(leg,[sign*.38,0,.53],[.18,.20,.56]),
      volume(leg,[sign*.38,-.13,.13],[.22,.36,.145]));
    for(let finger=0;finger<4;finger++)components.push(volume(arm,[sign*(1.04+finger*.08),-.10,1.29],[.052,.10,.07]));
  }
  function weights(p) {
    const side=p.x>=0?'left':'right';
    const arm=smooth(Math.abs(p.x),.60,.99)*smooth(p.z,.92,1.3);
    const leg=(1-arm)*(1-smooth(p.z,1.65,2.10));
    const body=heightWeights(p.z,[0,1,2,3,4],[1.9,2.45,3.1,3.7,4.08]).map(([i,w])=>[i,w*(1-arm-leg)]);
    const upper=smooth(p.z,2.15,2.59),lower=smooth(p.z,1.47,1.76),clavicle=smooth(p.z,3.2,3.5)*.5;
    const limb=[[index[`clavicle_${side}`],clavicle],[index[`upper_${side}`],upper*(1-clavicle)],
      [index[`lower_${side}`],(1-upper)*lower*(1-clavicle)],[index[`hand_${side}`],(1-upper)*(1-lower)*(1-clavicle)]];
    const thigh=smooth(p.z,.68,1.08),shin=smooth(p.z,.16,.36);
    return blendWeights([...body,...limb.map(([i,w])=>[i,w*arm]),
      [index[`thigh_${side}`],leg*thigh],[index[`shin_${side}`],leg*(1-thigh)*shin],[index[`foot_${side}`],leg*(1-thigh)*(1-shin)]]);
  }
  const groups=['torso','head','arm_left','arm_right','leg_left','leg_right'].map(Id=>({Id}));
  const parts=implicitParts(components,groups,[-1.7,-.65,-.2],[1.7,.65,4.85],72,weights,.10);
  const mask=plate([[-.27,4.35],[.27,4.35],[.26,4.10],[.16,3.88],[-.16,3.88],[-.26,4.10]],.065,-.235,
    [[-.11,4.2,.068,.032],[.11,4.2,.068,.032]]);
  parts.push(part('faceplate',mask,'metal',[0,0,4.08],'head'));
  const eyes=[-1,1].map(sign=>new T.SphereGeometry(.031,12,8).scale(1.5,.65,.8).translate(sign*.11,-.298,4.2));
  const eye=part('eyes',mergeGeometries(eyes),'metal',[0,0,4.08],'head');eye.UseMatterSurface=false;parts.push(eye);
  parts.push(part('chest_seal',plate([[-.19,3.18],[0,3.35],[.19,3.18],[.12,2.9],[0,2.79],[-.12,2.9]],.04,-.34),'metal',[0,0,3.1],'chest'));
  const bind=p=>influences(p,weights,controls);
  const anchors=[{Id:'palm.left',Part:2,Position:[1.16,-.19,1.48],Direction:[0,-1,0],Influences:bind([1.16,-.19,1.48])},
    {Id:'palm.right',Part:3,Position:[-1.16,-.19,1.48],Direction:[0,-1,0],Influences:bind([-1.16,-.19,1.48])},
    {Id:'crown',Part:1,Position:[0,0,4.51],Direction:[0,0,1],Control:'head'},
    {Id:'chest',Part:0,Position:[0,-.34,3.1],Direction:[0,-1,0],Control:'chest'}];
  const contacts=[];
  function contact(Id,Part,A,B,Radius){contacts.push({Id,Part,A,B,Radius,AInfluences:bind(A),BInfluences:bind(B)});}
  contact('torso',0,[0,0,2.16],[0,0,3.17],.34);contact('head',1,[0,-.01,4.02],[0,-.01,4.18],.27);
  for(const side of ['left','right']) {
    const sign=side==='left'?1:-1,arm=sign>0?2:3,leg=sign>0?4:5;
    contact('upper_'+side,arm,[sign*1.02,0,3.18],[sign*1.13,0,2.37],.20);
    contact('lower_'+side,arm,[sign*1.13,0,2.37],[sign*1.16,-.01,1.61],.17);
    contact('fist_'+side,arm,[sign*1.16,-.03,1.36],[sign*1.16,-.03,1.58],.18);
    contact('thigh_'+side,leg,[sign*.36,0,1.76],[sign*.38,0,.88],.22);
    contact('shin_'+side,leg,[sign*.38,0,.88],[sign*.38,0,.16],.16);
    contact('foot_'+side,leg,[sign*.38,-.30,.13],[sign*.38,.07,.13],.12);
    anchors.push({Id:'foot.'+side,Part:leg,Position:[sign*.38,-.13,.02],Direction:[0,0,-1],Control:'foot_'+side});
  }
  const press=motion('palm_press',2.4,false,controls,60,(i,p,c)=> {
    const [upper,lower,lean,turn]=phaseValue(p,[[0,[0,0,0,0]],[.2,[-30,-105,2,-10]],[.53,[-98,15,-4,8]],[.62,[-96,12,-4,7]],[1,[0,0,0,0]]]);
    let angles=[0,0,0];
    if(c.Id==='spine')angles=[lean*.5,0,turn*.25];if(c.Id==='chest')angles=[lean,0,turn];
    if(c.Id==='head')angles=[-lean*.5,0,-turn*.3];if(c.Id==='upper_right')angles=[upper,0,0];
    if(c.Id==='lower_right')angles=[lower,0,0];if(c.Id==='upper_left')angles=[upper*.18,0,0];
    return {rotation:rotation(...angles)};
  },[{Id:'gather',At:0},{Id:'contact',At:1.272},{Id:'recover',At:1.488},{Id:'ready',At:2.4}]);
  const brace=motion('brace',2.6,false,controls,60,(i,p,c)=> {
    const [lift,fold]=phaseValue(p,[[0,[0,0]],[.3,[-55,-75]],[.67,[-55,-75]],[1,[0,0]]]);
    const side=c.Id.endsWith('left')?1:-1;
    return {rotation:c.Id.startsWith('upper_')?rotation(lift,-lift*.4*side,0):c.Id.startsWith('lower_')?rotation(fold,0,0):[0,0,0,1]};
  },[{Id:'gather',At:0},{Id:'guard',At:.78},{Id:'recover',At:1.742},{Id:'ready',At:2.6}]);
  const roles={torso:['body.trunk'],head:['body.head'],arm_left:['body.arm.left','body.palm'],arm_right:['body.arm.right','body.palm'],
    leg_left:['body.leg.left'],leg_right:['body.leg.right'],faceplate:['body.mask'],eyes:['body.eye'],chest_seal:['decoration.seal']};
  const connections=[['torso','head',[0,0,3.7],false],['torso','arm_left',[.82,0,3.18],false],['torso','arm_right',[-.82,0,3.18],false],
    ['torso','leg_left',[.36,0,1.9],false],['torso','leg_right',[-.36,0,1.9],false],['head','faceplate',[0,-.25,4.1],false],
    ['faceplate','eyes',[0,-.298,4.2],false],['torso','chest_seal',[0,-.34,3.1],false]];
  write('spirit_colossus',parts,anchors,contacts,controls,[press,brace],false,{Forward:[0,-1,0],Up:[0,0,1]},
    structure('skinned','forms_bodies.mjs','makeColossus',{resolution:72,smooth:.10,volumes:components.map(c=>[...c.Center,...c.Radius])},roles,connections),[],
    [['palm_press','palm.right','contact'],['brace','palm.left','guard']].map(([Id,Anchor,From])=>({
      Id,Motion:Id,Scale:[.5,3],Rate:[.5,2],Turn:[-35,35],
      Supports:['left','right'].map(side=>({Anchor:'foot.'+side,From:'gather',Until:'ready',Slack:.01})),
      Contacts:[{Id:'palm',Anchor,PartnerRole:'body.palm',From,Until:'recover',Gap:.08,Angle:20,Opposed:true}]})));
}

/** 有独立指节的连续灵臂，掌、指尖挂点和作用端点都用相同表面权重。 */
function makeArm() {
  const fingers=[['little',-.135,.28,.031],['ring',-.045,.35,.034],['middle',.045,.40,.035],['index',.135,.34,.033],['thumb',.26,.23,.052]];
  const entries=[['upper',null,[0,0,2.05]],['lower','upper',[0,0,1.1]],['wrist','lower',[0,0,.26]]];
  const components=[volume(0,[0,0,1.66],[.18,.20,.56]),volume(1,[0,0,.69],[.135,.14,.52]),volume(2,[0,0,.11],[.205,.09,.19])];
  const segments=[];
  fingers.forEach(([id,x,length,radius],i)=> {
    const a=[x,0,id==='thumb'?.04:-.015],b=[x,0,-length*.5],c=[x,0,-length];
    if(id==='thumb'){b[0]+=.025;c[0]+=.05;}
    entries.push([id+'_base','wrist',a],[id+'_tip',id+'_base',b]);segments.push([a,b,c]);
    components.push(volume(3+i,[(a[0]+c[0])*.5,0,(a[2]+c[2])*.5],[radius,id==='thumb'?.06:.043,(a[2]-c[2])*.5+radius]));
    if(id==='thumb')components.push(volume(3+i,[a[0]-.03,0,a[2]],[.085,.065,.115]));
  });
  const controls=controlsFromPoints(entries);
  function weights(p) {
    if(p.z>.12&&Math.abs(p.x)<.205) return blendWeights(heightWeights(p.z,[2,1,0],[.26,1.1,2.05]));
    let nearest=0,distance=Infinity;
    segments.forEach(([a,,c],i)=>{const d=Math.abs(p.x-(a[0]+c[0])*.5);if(d<distance){distance=d;nearest=i;}});
    const [a,b]=segments[nearest],finger=nearest===4?smooth(p.x,.15,.245):1-smooth(p.z,-.015,.14);
    const second=1-smooth(p.z,b[2]-.05,b[2]+.05);
    return blendWeights([[2,1-finger],[3+nearest*2,finger*(1-second)],[4+nearest*2,finger*second]]);
  }
  const groups=['upper_arm','forearm','palm',...fingers.map(f=>'finger_'+f[0])].map(Id=>({Id}));
  const parts=implicitParts(components,groups,[-.5,-.34,-.55],[.65,.34,2.5],80,weights,.028);
  const bind=p=>influences(p,weights,controls),anchors=[{Id:'shoulder',Part:0,Position:[0,0,2.05],Direction:[0,0,1],Control:'upper'},
    {Id:'wrist',Part:2,Position:[0,0,.26],Direction:[0,0,1],Control:'wrist'},
    {Id:'palm',Part:2,Position:[0,-.09,.11],Direction:[0,-1,0],Influences:bind([0,-.09,.11])},
    {Id:'grip',Part:2,Position:[0,-.12,-.07],Direction:[1,0,0],Control:'wrist'}];
  const contacts=[];
  function contact(Id,Part,A,B,Radius){contacts.push({Id,Part,A,B,Radius,AInfluences:bind(A),BInfluences:bind(B)});}
  contact('upper',0,[0,0,2.05],[0,0,1.1],.16);contact('lower',1,[0,0,1.1],[0,0,.26],.12);contact('palm',2,[0,0,.20],[0,0,0],.13);
  fingers.forEach(([id,,,radius],i)=> {
    const [a,b,c]=segments[i];contact(id+'_base',3+i,a,b,radius);contact(id+'_tip',3+i,b,c,radius);
    anchors.push({Id:'tip.'+id,Part:3+i,Position:c,Direction:[0,0,-1],Influences:bind(c)});
  });
  const grasp=motion('grasp',2,false,controls,60,(i,p,c)=> {
    const [curl]=phaseValue(p,[[0,[0]],[.35,[72]],[.65,[72]],[1,[0]]]);
    return {rotation:c.Id.endsWith('_base')?rotation(-curl*.85,0,c.Id.startsWith('thumb')?curl*.4:0):c.Id.endsWith('_tip')?rotation(-curl,0,0):[0,0,0,1]};
  },[{Id:'open',At:0},{Id:'closed',At:.7},{Id:'release',At:1.3},{Id:'ready',At:2}]);
  const press=motion('palm_press',1.8,false,controls,60,(i,p,c)=> {
    const [upper,lower]=phaseValue(p,[[0,[0,0]],[.25,[-35,-95]],[.56,[-90,12]],[.65,[-90,12]],[1,[0,0]]]);
    return {rotation:c.Id==='upper'?rotation(upper):c.Id==='lower'?rotation(lower):[0,0,0,1]};
  },[{Id:'gather',At:0},{Id:'contact',At:1.008},{Id:'recover',At:1.17},{Id:'ready',At:1.8}]);
  const roles={upper_arm:['body.upper_arm'],forearm:['body.forearm'],palm:['body.palm']};
  const connections=[['upper_arm','forearm',[0,0,1.1],false],['forearm','palm',[0,0,.26],false]];
  fingers.forEach(([id],i)=>{roles['finger_'+id]=['body.finger','body.finger.'+id];connections.push(['palm','finger_'+id,segments[i][0],false]);});
  write('spirit_arm',parts,anchors,contacts,controls,[grasp,press],false,{Forward:[0,-1,0],Up:[0,0,1]},
    structure('skinned','forms_bodies.mjs','makeArm',{resolution:80,smooth:.028,fingers:fingers.map(f=>f.slice(1))},roles,connections),[],[
      {Id:'grasp',Motion:'grasp',Scale:[.5,3],Rate:[.5,2],Turn:[-60,60],Supports:[],Contacts:[
        {Id:'weapon',Anchor:'grip',PartnerRole:'weapon.grip',From:'closed',Until:'release',Gap:.04,Angle:12,Opposed:false},
        {Id:'handle',Anchor:'grip',PartnerRole:'vessel.handle',From:'closed',Until:'release',Gap:.04,Angle:12,Opposed:false}]},
      {Id:'palm_press',Motion:'palm_press',Scale:[.5,3],Rate:[.5,2],Turn:[-60,60],Supports:[],Contacts:[
        {Id:'palm',Anchor:'palm',PartnerRole:'body.palm',From:'contact',Until:'recover',Gap:.06,Angle:20,Opposed:true}]}]);
}

/** 两类主体通过同一发布和运行链路提供不同连续姿态。 */
export function makeBodyForms(){makeColossus();makeArm();}
