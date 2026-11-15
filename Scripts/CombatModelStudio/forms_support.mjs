/** 支持法术：叶芽、承托叶面和宽厚净化波，各自保留独立制作结构与真实接触。 */
import * as T from 'three';
import {part, write, motion, structure} from './form_export.mjs';

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
  parts.forEach(p=>p.UseMatterSurface=false);
  write('healing_seed',parts,[],[{Id:'bud',Part:3,A:[0,0,-.14],B:[0,0,.14],Radius:.17,AControl:'bud',BControl:'bud'}],controls,[run],false,
    {Forward:[0,1,0],Up:[0,0,1]},structure('articulated','forms_support.mjs','makeSeed',{leaves:3,length:.48,width:.16},
      Object.fromEntries(parts.map(p=>[p.Id,['support.carrier']])),[]));
}

/** 低矮叶面限定恢复范围，缓慢起伏不会把地面铺成巨大伤害圆球。 */
function makeBloom() {
  const controls=[],parts=[],contacts=[];
  for(let i=0;i<6;i++) {
    const angle=i*Math.PI/3,centre=[Math.cos(angle)*1.05,Math.sin(angle)*1.05,.24],id=`leaf_${i}`;
    controls.push({Id:id,Position:centre,Rotation:[0,0,0,1]});
    parts.push(supportPart(id,leaf(1.4,.73,.23,angle-Math.PI/2,centre),'restoration_jade',centre,id));
    const a=[Math.cos(angle)*.35,Math.sin(angle)*.35,.24],b=[Math.cos(angle)*2.17,Math.sin(angle)*2.17,.24];
    contacts.push({Id:id,Part:i,A:a,B:b,Radius:.23,AControl:id,BControl:id});
  }
  controls.push({Id:'heart',Position:[0,0,.26],Rotation:[0,0,0,1]});
  parts.push(supportPart('heart',new T.SphereGeometry(.66,28,16).scale(1,1,.45).translate(0,0,.26),'purification_pearl',[0,0,.26],'heart'));
  contacts.push({Id:'heart',Part:6,A:[-.3,0,.26],B:[.3,0,.26],Radius:.24,AControl:'heart',BControl:'heart'});
  const run=motion('run',1,true,controls,40,(i,p,c)=>({position:[c.Position[0],c.Position[1],c.Position[2]+.025*Math.sin(p*Math.PI*2)]}));
  parts.forEach(p=>p.UseMatterSurface=false);
  write('rejuvenation_bloom',parts,[],contacts,controls,[run],false,{Forward:[0,1,0],Up:[0,0,1]},
    structure('articulated','forms_support.mjs','makeBloom',{leaves:6,radius:2.5,height:.5},Object.fromEntries(parts.map(p=>[p.Id,['support.surface']])),[]));
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

/** 三个独立支持来源在共同资源入口发布。 */
export function makeSupport(){makeSeed();makeBloom();makeWave();}
