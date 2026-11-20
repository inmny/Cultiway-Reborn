/** 巨神的雷甲、云腹与重锤共用现有蒙皮及世界接触。 */
import * as T from 'three';
import {mergeGeometries} from 'three/addons/utils/BufferGeometryUtils.js';
import {formation,forming,motion,part,round,skin,structure,write} from './form_export.mjs';
import {phaseValue,rotation} from './form_skeleton.mjs';

const turn=x=>new T.Quaternion().setFromAxisAngle(new T.Vector3(1,0,0),x*Math.PI/180);
const smooth=(x,a,b)=>T.MathUtils.smoothstep(x,a,b);
const grip=[-.055,-.075,-.12];
const jaw=1.47;
const strikeFace={centre:[0,-.855,jaw],half:[.41,.085,.41]};

/** 封闭护甲用折面收边，外缘与主面接光不同。 */
function plate(points,depth,bevel) {
  return new T.ExtrudeGeometry(new T.Shape(points.map(p=>new T.Vector2(...p))),{
    depth,bevelEnabled:true,bevelSize:bevel,bevelThickness:bevel*.55,bevelSegments:1,steps:1});
}

/** 左右肩各有三层越肩甲片，片层包住真实连续肩部。 */
function pauldron(side) {
  const sign=side==='left'?1:-1;
  return mergeGeometries([0,1,2].map(i=>plate([[-.38,.16],[-.25,.34],[.14,.39],[.45,.25],
    [.54,.02],[.36,-.08],[-.32,-.13]],.52-.07*i,.035)
    .translate(0,0,-.26+.035*i).rotateX(Math.PI/2)
    .rotateY(sign*(-.3-i*.14)).translate(sign*(.92+i*.105),-.065,3.35-i*.12)));
}

/** 云腹采用同一低重心的大团块，少量硬折面只在外围露出。 */
function cloudBed() {
  return mergeGeometries(Array.from({length:13},(_,i)=>{
    const a=i*2.399963, edge=i===0?0:.56+i*.105;
    const w=.60+.19*Math.cos(i*1.71)+.45/(1+i*.3);
    const g=new T.IcosahedronGeometry(1,2);
    const pos=g.getAttribute('position');
    for(let j=0;j<pos.count;j++) {
      const p=new T.Vector3().fromBufferAttribute(pos,j);
      const warp=1+.05*Math.sin(p.x*6+p.z*2+i)+.05*Math.cos(5*p.y-4*p.z+i*.6);
      p.multiplyScalar(warp);
      pos.setXYZ(j,p.x,p.y,p.z);
    }
    return g.scale(w,w*.78,w*.42).rotateZ(a).translate(Math.cos(a)*edge,Math.sin(a)*edge*.72,1.43+.13*Math.sin(i*1.6));
  }));
}

/** 先用大角度回摆蓄势，再由肩肘加速下砸，最后由腕带着原锤回弹。 */
function strikeMotion(controls) {
  const at=[[0,[-5,-18,-6,-12,0,30,-.35]], [.18,[-37,-71,-23,-32,-8,-13,-.12]],
    [.37,[-118,-27,-18,-38,-11,-38,.05]], [.49,[-127,-16,-8,-25,-4,-44,.08]],
    [.65,[-53,21,-16,-31,12,126,-.03]], [.695,[-38,24,-22,-38,15,141,-.055]],
    [.79,[-26,-19,-27,-28,6,109,.02]], [1,[-9,-16,-8,-17,0,42,-.35]]];
  const duration=3.2,lookup=new Map(controls.map((c,i)=>[c.Id,i]));
  let previous=-1,values;
  const clip=motion('hammerfall',duration,false,controls,88,(i,p)=>{
    if(p!==previous) {
      previous=p;
      const [upper,elbow,left,leftElbow,lean,head,lift]=phaseValue(p,at);
      const set={spine:rotation(-lean*.32,0,lean*.12),chest:rotation(-lean*.50,-lean*.12,lean*.19),
        neck:rotation(lean*.28,0,0),head:rotation(lean*.24,0,0),clavicle_right:rotation(-lean*.35,0,lean*.21),
        upper_right:rotation(upper,lean*.12,4),lower_right:rotation(elbow),hand_right:rotation(elbow*-.16),
        clavicle_left:rotation(lean*.16),upper_left:rotation(left,-17,4),lower_left:rotation(leftElbow)};
      const world=[];
      values=controls.map((control,k)=>{
        const parent=control.Parent?world[lookup.get(control.Parent)]:new T.Quaternion();
        const q=control.Id==='maul'?parent.clone().invert().multiply(turn(head)):
          new T.Quaternion().fromArray(set[control.Id]??control.Rotation);
        world[k]=parent.clone().multiply(q);
        const position=control.Id==='pelvis'?control.Position.map((a,n)=>n===2?a+lift:a):control.Position;
        return {rotation:q.toArray(),position};
      });
    }
    return values[i];
  },[{Id:'cloud',At:.15},{Id:'raised',At:1.50},{Id:'strike_start',At:1.81},{Id:'strike_end',At:2.25},
    {Id:'recoil',At:2.48},{Id:'gone',At:duration}]);
  return forming(clip,p=>smooth(p,.005,.29)*(1-smooth(p,.80,1)));
}

/** 装饰保留各自材质；进场和退场用同一批顶点数据。 */
function formed(id,geometry,surface,pivot,control,from,range) {
  formation(geometry,p=>from+range*smooth(p.z,pivot[2]-.38,pivot[2]+.75),p=>
    new T.Vector3(pivot[0]+(p.x-pivot[0])*.20,pivot[1]+(p.y-pivot[1])*.20,pivot[2]));
  return {...part(id,skin(geometry,()=>[[control,1]]),surface,pivot),UseMatterSurface:false};
}

/** 同一锤面坐标驱动显示、人物接触及撞地，不用整个巨神的中心球。 */
export function makeThunderColossus(original,bodyRig) {
  const rig=bodyRig.filter(control=>!/^(thigh|shin|foot)_/.test(control.Id));
  const remap=bodyRig.map(control=>Math.max(0,rig.findIndex(remaining=>remaining.Id===control.Id)));
  const controls=[...rig,{Id:'maul',Parent:'hand_right',Position:grip,Rotation:[0,0,0,1]}];
  const bind=rig.findIndex(x=>x.Id==='hand_right');
  const hand=[0,0,0];
  for(let at=rig[bind];at;at=rig.find(c=>c.Id===at.Parent)) at.Position.forEach((v,i)=>hand[i]+=v);
  const pivot=hand.map((v,i)=>v+grip[i]);
  const parts=original.filter(p=>!p.Id.startsWith('leg_')).map(p=>{
    const bodyPart=p.Id.startsWith('arm')?p.Id.endsWith('left')?.30:.37:p.Id==='torso'?.14:.28;
    const data=[], rigidIndices=[], rigidWeights=[];
    const index=p.Skin ? -1 : rig.findIndex(control=>control.Id===p.Control);
    for(let i=0;i<p.Positions.length;i+=3) {
      const [x,y,z]=p.Positions.slice(i,i+3);
      data.push(round(x*.46),round(y*.40),round(1.54+smooth(z,1.45,4.45)*.14),
        round((bodyPart+.25*smooth(z,1.53,4.45))*.96));
      if(!p.Skin) {
        rigidIndices.push(index,0,0,0);
        rigidWeights.push(1,0,0,0);
      }
    }
    return {...p,HasBuildData:true,BuildData:data,UseMatterSurface:false,
      ...(p.Skin ? {SkinIndices:p.SkinIndices.map(i=>remap[i])} :
        {Control:null,Skin:true,SkinIndices:rigidIndices,SkinWeights:rigidWeights}),
      Surface:p.Id==='faceplate'?'metal':p.Id==='eyes'?'lightning_dense':'bronze'};
  });
  const joint = id=>controls.findIndex(control=>control.Id===id);
  parts.unshift(formed('cloud',cloudBed(),'yin_depth',[0,0,1.42],joint('pelvis'),.035,.12));
  for(const side of ['left','right'])parts.push(formed(`pauldron_${side}`,pauldron(side),'metal',
    [side==='left'?1:-1,0,3.3],joint(`clavicle_${side}`),.31,.16));
  const throat=plate([[-.52,.28],[-.18,.42],[0,.31],[.18,.42],[.52,.28],[.44,-.17],[0,-.39],[-.44,-.17]],.10,.035)
    .rotateX(Math.PI/2).translate(0,-.31,3.06);
  parts.push(formed('breastplate',throat,'metal',[0,0,2.97],joint('chest'),.27,.11));

  const intoGrip=g=>g.translate(...pivot);
  const shaft=intoGrip(new T.CylinderGeometry(.090,.13,2.03,14,2).rotateX(Math.PI/2).translate(0,0,.48));
  const head=intoGrip(plate([[-.50,-.91],[.50,-.91],[.58,-.68],[.43,-.39],[.42,.47],[.27,.72],
    [-.28,.72],[-.45,.45],[-.46,-.37],[-.58,-.66]],.88,.04).translate(0,0,jaw-.44));
  const cap=intoGrip(plate([[-.415,-.94],[.415,-.94],[.48,-.86],[.48,-.77],[-.48,-.77],[-.48,-.86]],
    .90,.022).translate(0,0,jaw-.45));
  const rings=intoGrip(mergeGeometries([-.27,.26,.98].map(z=>new T.TorusGeometry(.132,.034,6,18).translate(0,0,z))));
  const crest=intoGrip(new T.OctahedronGeometry(.29,1).scale(1,1.16,.42).translate(0,.035,jaw-.48));
  parts.push(formed('shaft',shaft,'metal',pivot,joint('maul'),.51,.14));
  parts.push(formed('maul_head',head,'bronze',pivot,joint('maul'),.61,.14));
  const face=parts.length;
  parts.push(formed('hammer_face',cap,'metal',pivot,joint('maul'),.67,.14));
  parts.push(formed('grip_rings',rings,'bronze',pivot,joint('maul'),.59,.12));
  parts.push(formed('hammer_core',crest,'lightning_dense',pivot,joint('maul'),.70,.14));
  const A=pivot.map((v,i)=>v+strikeFace.centre[i]);
  const anchors=[{Id:'hammer.face',Part:face,Position:A,Direction:[0,-1,0],Control:'maul'},
    {Id:'hammer.grip',Part:face-2,Position:pivot,Direction:[0,0,1],Control:'maul'}];
  const contacts=[{Id:'hammer_face',Part:face,A,HalfExtents:strikeFace.half,BuildRange:[.88,1],AControl:'maul'}];
  const roles=Object.fromEntries(parts.map(p=>[p.Id,[p.Id==='hammer_face'?'weapon.hammer.face':p.Id==='cloud'?
    'decoration.cloud':p.Id==='shaft'?'weapon.shaft':'spirit.body']]));
  const links=[['cloud','torso',[0,0,1.55],false],['torso','arm_right',[-.8,0,3.2],false],
    ['torso','arm_left',[.8,0,3.2],false],['arm_right','shaft',pivot,false],['shaft','maul_head',A,false],['maul_head','hammer_face',A,false]];
  write('thunder_colossus',parts,anchors,contacts,controls,[strikeMotion(controls)],false,
    {Forward:[0,-1,0],Up:[0,0,1],HideBelowGround:true},
    structure('skinned','forms_thunder_colossus.mjs','makeThunderColossus',{grip,head:jaw,face:strikeFace.half},roles,links));
}
