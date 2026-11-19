/** 投射和坠落来源的实体，以及由真实余力交接形成的六类爆发厚体。 */
import * as T from 'three';
import {write,structure,surfaceData,motion,round} from './form_export.mjs';
import {wallPart,column} from './forms_solid_walls.mjs';

/** 封闭的分层长体；局部正Z是前方，尖端与内部接触共同朝向实际运动。 */
function shaft(rows,sides=8) {
  return column(rows.map(([z,r,x=0,y=0])=>Array.from({length:sides},(_,i)=>{
    const a=i*Math.PI*2/sides;return [x+Math.cos(a)*r,y+Math.sin(a)*r,z];
  })),sides);
}

/** 共用制作出口，各资源保留自己的网格和实际接触芯。 */
function publish(id,pieces,contacts,parameters={}) {
  const controls=[{Id:'root',Position:[0,0,0],Rotation:[0,0,0,1]}];
  const parts=pieces.map(([name,g,s])=>wallPart(name,g,s,[0,0,0],'root'));
  write(id,parts,[{Id:'root',Part:0,Position:[0,0,0],Direction:[0,0,1],Control:'root'}],
    contacts.map(([a,b,r,part=0],i)=>({Id:`core_${i}`,Part:part,A:a,B:b,Radius:r,AControl:'root',BControl:'root'})),controls,[],false,
    {Forward:[0,0,1],Up:[0,1,0],HideBelowGround:true},structure('rigid','forms_impacts.mjs','makeImpactForms',parameters,
      Object.fromEntries(parts.map(p=>[p.Id,['projectile.body','impact.volume']])),[]));
}

/** 火球有浑圆前核和三片厚焰背，区别于坠落炎陨的长体。 */
function fireball() {
  const pieces=[['heart',new T.SphereGeometry(.49,24,16),'flame']];
  for(let i=0;i<3;i++)pieces.push([`flare_${i}`,shaft([[-.86,.015],[-.44,.21],[-.12,.18]],10).translate(.24,0,0).rotateZ(i*Math.PI*2/3),'flame']);
  publish('fireball',pieces,[[[0,0,0],[0,0,.10],.35]],{radius:.49,backFlares:3});
}

/** 雷球有折面核与六片宽短雷瓣，不以细线网格代替体积。 */
function thunder() {
  const pieces=[['heart',new T.IcosahedronGeometry(.46,1),'lightning_dense']];
  for(let i=0;i<6;i++) {
    const direction=new T.Vector3(Math.cos(i*Math.PI/3),Math.sin(i*Math.PI/3),i%2?.5:-.5).normalize();
    pieces.push([`lobe_${i}`,shaft([[.25,.18],[.52,.14],[.75,.012]],6).applyQuaternion(new T.Quaternion().setFromUnitVectors(new T.Vector3(0,0,1),direction)),'lightning_dense']);
  }
  publish('thunder_orb',pieces,[[[0,0,0],[0,0,0],.39]],{lobes:6});
}

/** 混沌球由不共面的厚瓣挤合，中央保留封闭核，表面错位不扩大实际接触。 */
function chaos() {
  const pieces=[['heart',new T.SphereGeometry(.38,24,16),'entropy_depth']];
  for(let i=0;i<3;i++)pieces.push([`fold_${i}`,new T.SphereGeometry(.46,24,12).scale(1,.48,1.12).rotateX(.9*i).rotateZ(1.8*i),'entropy_veil']);
  publish('chaos_orb',pieces,[[[0,0,-.05],[0,0,.05],.30]],{folds:3});
}

/** 五种下落主体有独立轮廓，接触芯随共同下落轨迹及朝向。 */
function falling() {
  publish('flame_meteor',[
    ['body',new T.IcosahedronGeometry(.63,2).scale(1,.91,1.5),'flame'],
    ['back',shaft([[-1.55,.012],[-1.05,.31],[-.5,.43]],10),'flame']],
    [[[0,0,-.28],[0,0,.36],.42]],{profile:'elongated_flame'});
  const rock=new T.IcosahedronGeometry(.80,1),p=rock.getAttribute('position');
  for(let i=0;i<p.count;i++)p.setXYZ(i,p.getX(i)*(1+.13*Math.sin(p.getY(i)*12)),p.getY(i)*.86,p.getZ(i)*1.28);
  publish('falling_rock',[['boulder',rock,'stone']],[[[0,0,-.25],[0,0,.28],.51]],{profile:'faceted_boulder'});
  const trunk=shaft([[-1.05,.30,-.03],[-.63,.37],[.40,.32,.04],[1.18,.20,.02]],12);
  const branch=shaft([[0,.13],[.52,.10],[.86,.025]],8).rotateY(.9).translate(.13,0,-.29);
  publish('falling_timber',[['trunk',trunk,'wood'],['broken_branch',branch,'wood']],[[[0,0,-.75],[.025,0,.82],.19]],{profile:'broken_trunk'});
  lightningStrike();
  const pieces=[],contacts=[];
  for(let i=0;i<4;i++) {
    const a=i*Math.PI/2,x=Math.cos(a)*.23,y=Math.sin(a)*.23,length=1.05+.24*Math.sin(i*2);
    pieces.push([`crystal_${i}`,shaft([[-.67,.15],[.34,.22],[length,.004]],5).translate(x,y,0),'ice']);
    contacts.push([[x,y,-.44],[x,y,.61],.10,i]);
  }
  publish('icefall',pieces,contacts,{profile:'crystal_cluster',crystals:4});
}

/** 前端折面套在原接触芯外，后段才分岔，保留原尺寸与下落基准。 */
function lightningStrike() {
  const bolt=shaft([[-1.45,.07,.15],[-.99,.25,-.16],[-.54,.29,.13],[-.14,.24,-.13],
    [.33,.23,.08],[.71,.145,-.04],[.83,.11,.04],[1.34,.005,.02]],7);
  const pieces=[['channel',bolt,'thunder_arc']];
  for(const [i,angle] of [.25,2.30,4.11].entries()) {
    const start=-1.04+i*.26;
    const fork=shaft([[start,.14],[start+.28,.175,.36],[start+.56,.11,.50],
      [start+.84,.008,.86-i*.11]],6).rotateZ(angle);
    pieces.push([`fork_${i}`,fork,'thunder_arc_fork']);
  }
  publish('lightning_strike',pieces,[[[0,0,-.7],[0,0,.72],.10]],{profile:'forked_channel',forks:3});
  // 旧术保留原芯。新落雷的前芯抵到实体尖端，小体型不会在接触前就判为落地。
  publish('lightning_strike_focused',pieces,[[[0,0,-.7],[0,0,1.23],.13]],{profile:'forked_channel',forks:3});
}

/** 蓄力先收雷瓣；确认接触时余瓣才由击点向外翻，随后在同一动作中消散。 */
function lightningStages() {
  const zero=[0,0,0],identity=[0,0,0,1],ease=T.MathUtils.smoothstep;
  for(const charge of [true,false]) {
    const duration=charge?.58:.40, count=charge?4:5, clipId=charge?'gather':'release';
    const axes=Array.from({length:count},(_,i)=>new T.Vector3(Math.cos(i*2.4),Math.sin(i*2.4),0));
    const controls=[{Id:'root',Position:zero,Rotation:identity},...axes.map((axis,i)=>({
      Id:`lobe_${i}`,Parent:'root',Position:zero,Rotation:identity
    }))];
    const parts=[wallPart('heart',new T.IcosahedronGeometry(charge?.25:.17,1).scale(1,1,charge?1.2:.65),
      'thunder_arc',zero,'root')];
    for(const [i,axis] of axes.entries()) {
      const length=charge?.62+.09*(i%3):.50+.12*(i%3);
      const shard=shaft([[0,.038], [length*.28,.125], [length*.64,.19,-.16],[length,.01,-.095]],6)
        .applyQuaternion(new T.Quaternion().setFromUnitVectors(new T.Vector3(0,0,1),axis));
      parts.push(wallPart(controls[i+1].Id,shard,'thunder_arc_fork',zero,controls[i+1].Id));
    }
    const clip=motion(clipId,duration,false,controls,24,(index,phase)=>{
      if(!index)return {};
      const i=index-1, axis=axes[i], time=phase*duration;
      const start=.017*i;
      const opening=ease(time,start,charge?.48:.14+start);
      const settling=ease(time,.18+start,duration);
      const radius=charge?1.13*(1-opening):.56*opening+.16*settling;
      const position=axis.clone().multiplyScalar(radius);
      position.z=charge?(.23-.12*i)*(1-opening):-(.06+.16*opening)*(1-settling);
      const hinge=new T.Vector3(-axis.y,axis.x,0);
      const amount=charge?(1-opening)*(.75-.14*i):opening*(.50+.06*i)-settling*.49;
      const tilt=new T.Quaternion().setFromAxisAngle(hinge,amount);
      if(charge)tilt.premultiply(new T.Quaternion().setFromAxisAngle(new T.Vector3(0,0,1),(.85-.05*i)*(1-opening)));
      return {position:position.toArray(),rotation:tilt.toArray()};
    });
    for(const key of clip.Keys)key.Fade=round(charge
      ?.72*(1-ease(key.At,0,.17))+ease(key.At,.49,duration)
      :.42*(1-ease(key.At,0,.055))+ease(key.At,.13,duration));
    write(charge?'lightning_charge':'lightning_contact',parts,
      [{Id:'root',Part:0,Position:zero,Direction:[0,0,1],Control:'root'}],[],controls,[clip],false,
      {Forward:[0,0,1],Up:[0,1,0],HideBelowGround:true},structure('articulated','forms_impacts.mjs','makeImpactForms',
        {lobes:count,duration},Object.fromEntries(parts.map(p=>[p.Id,['impact.volume']])),[]));
  }
}

/** 六类爆发保留闭合实体内核；内部球形作用区半径0.9，与运行的原作用半径共同缩放。 */
function bursts() {
  const settings=[['fire','flame'],['lightning','lightning_dense'],['entropy','entropy_veil'],['earth','stone'],['wood','wood'],['ice','ice']];
  for(const [matter,surface] of settings) {
    let core=matter==='earth'||matter==='ice'?new T.IcosahedronGeometry(.97,2):new T.SphereGeometry(.91,28,18);
    const pieces=[['heart',core,surface]];
    for(let i=0;i<6;i++) {
      const direction=new T.Vector3(Math.cos(i*2.4)*.8,Math.sin(i*2.4)*.8,(i-2.5)*.29).normalize();
      let g;
      if(matter==='earth')g=new T.BoxGeometry(.43,.26,.32).translate(0,0,.83);
      else if(matter==='entropy')g=new T.SphereGeometry(.45,20,12).scale(1,.48,1).translate(0,0,.59).rotateY(i*.7);
      else if(matter==='fire')g=shaft([[.65,.27],[.84,.22],[1.10,.006]],10);
      else if(matter==='lightning')g=shaft([[.65,.21,.08],[.88,.16,-.06],[1.08,.006,.02]],6);
      else if(matter==='wood')g=shaft([[.65,.20],[.95,.13],[1.10,.06]],10);
      else g=shaft([[.64,.22],[.88,.18],[1.12,.003]],5);
      g.applyQuaternion(new T.Quaternion().setFromUnitVectors(new T.Vector3(0,0,1),direction));
      pieces.push([`lobe_${i}`,g,surface]);
    }
    publish(matter+'_burst',pieces,[[[0,0,0],[0,0,0],.9]],{profile:matter,contactRadius:.9});
  }
}

/** 炎陨向前撞击，背向坠落方向翻开低厚火肩，落点仍可看穿。 */
function meteorImpact() {
  const floor=new T.LatheGeometry([[0,.006],[.73,.006],[.89,.01],[.90,.018],[.83,.05],[.56,.077],[0,.055]]
    .map(p=>new T.Vector2(...p)),64).rotateX(-Math.PI/2);
  const pieces=[['heat',surfaceData(floor,.11,p=>.47+.23*(1-Math.hypot(p.x,p.y)/.90)),'meteor_heat']];
  const shoulders=[];
  for(const [index,start,span] of [[0,.20,2.03],[1,2.66,1.46],[2,4.57,1.42]]) {
    const sections=30,sides=12;
    const rings=Array.from({length:sections+1},(_,i)=>{
      const t=i/sections,a=start+span*t,taper=Math.max(.018,Math.pow(Math.sin(Math.PI*t),.71));
      const rim=.64+.053*Math.sin(a*5+index),height=.065+taper*(.115+.067*Math.sin(a*4+.4));
      return Array.from({length:sides},(_,j)=>{
        const b=j*2*Math.PI/sides,r=rim+.193*taper*Math.cos(b);
        return [r*Math.cos(a),r*Math.sin(a),-Math.max(.01,height+.150*taper*Math.sin(b))];
      });
    });
    const arc=column(rings,sides);
    pieces.push([`fold_${index}`,surfaceData(arc,.18,p=>T.MathUtils.smoothstep(-p.z,.013,.10)),'meteor_crown']);
    shoulders.push(start+span*.5);
  }
  publish('flame_meteor_impact',pieces,[[[0,0,0],[0,0,0],.9]],{contactRadius:.9,folds:3});
  publishMeteorMotion(pieces,shoulders);
}

/** 落点的低焰标出整块作用面；独立火肩错时翻开再贴地，始终不移动球形作用。 */
function publishMeteorMotion(pieces,shoulders) {
  const identity=[0,0,0,1],origin=[0,0,0],ease=T.MathUtils.smoothstep,duration=.22;
  const directions=shoulders.map(angle=>new T.Vector3(Math.cos(angle),Math.sin(angle),0));
  const controls=[{Id:'root',Position:origin,Rotation:identity},...directions.map((axis,i)=>({
    Id:`fold_${i}`,Parent:'root',Position:axis.clone().multiplyScalar(.62).toArray(),Rotation:identity
  }))];
  const parts=pieces.map(([name,g,s],i)=>wallPart(name,g,s,controls[i].Position,controls[i].Id));
  const clip=motion('spread',duration,false,controls,22,(i,phase,control)=>{
    if(!i)return {};
    const at=phase*duration,axis=directions[i-1];
    const start=(i-1)*.009,open=ease(at,start,start+.052),settle=ease(at,.073+(i-1)*.007,.21);
    const position=new T.Vector3(...control.Position).addScaledVector(axis,-.28*(1-open)+.062*settle);
    position.z=.14*(1-open)+.17*settle;
    const tangent=new T.Vector3(-axis.y,axis.x,0);
    const rotation=new T.Quaternion().setFromAxisAngle(tangent,-.68*(1-open)+.16*open-.62*settle);
    return {position:position.toArray(),rotation:rotation.toArray()};
  },[{Id:'spread',At:.07},{Id:'settle',At:.10}]);
  for(const key of clip.Keys)key.Fade=round(.33*(1-ease(key.At,0,.035))+ease(key.At,.072,duration));
  const anchors=[{Id:'root',Part:0,Position:origin,Direction:[0,0,1],Control:'root'}];
  const contacts=[{Id:'core_0',Part:0,A:origin,B:origin,Radius:.9,AControl:'root',BControl:'root'}];
  write('flame_meteor_impact_motion',parts,anchors,contacts,controls,[clip],false,
    {Forward:[0,0,1],Up:[0,1,0],HideBelowGround:true},
    structure('articulated','forms_impacts.mjs','makeImpactForms',{contactRadius:.9,folds:3,duration},
      Object.fromEntries(parts.map(p=>[p.Id,['projectile.body','impact.volume']])),[]));
}

/** 发布八种来源主体、六类通用爆发及落地、蓄雷形体。 */
export function makeImpactForms() {fireball();thunder();chaos();falling();lightningStages();bursts();meteorImpact();}
