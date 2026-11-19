/** 有实际厚度的可伸展主体；只平移末端，避免纵向伸展放大横向半径。 */
import * as T from 'three';
import {part, write, structure, skin} from './form_export.mjs';

/** 封闭胶囊的两端保持原形，中段沿制作前向伸展。 */
export function makeRay() {
  const radius=.12, length=1;
  const geometry=new T.CapsuleGeometry(radius,length-radius*2,8,24).rotateX(Math.PI/2).translate(0,0,length*.5);
  const mesh=skin(geometry,p=>{
    const t=T.MathUtils.clamp((p.z-radius)/(length-radius*2),0,1);
    return [[0,1-t],[1,t]];
  });
  const controls=['origin','extent'].map(Id=>({Id,Parent:null,Position:[0,0,0],Rotation:[0,0,0,1]}));
  write('energy_ray',[part('channel',mesh,'metal')],
    [{Id:'start',Part:0,Position:[0,0,0],Direction:[0,0,-1],Control:'origin'},
      {Id:'end',Part:0,Position:[0,0,length],Direction:[0,0,1],Control:'extent'}],
    [{Id:'channel',Part:0,A:[0,0,radius],B:[0,0,length-radius],Radius:radius,AControl:'origin',BControl:'extent'}],
    controls,[],false,{Forward:[0,0,1],Up:[0,1,0]},
    structure('volume','forms_ray.mjs','makeRay',{'span.length':length,'span.radius':radius},
      {channel:['channel.body','channel.surface']},[],['span']));
}

/** 两类原光束保留独立宽面，末端只平移，不拉粗横截面。 */
export function makeSourceRays() {
  const controls=['origin','extent'].map(Id=>({Id,Parent:null,Position:[0,0,0],Rotation:[0,0,0,1]}));
  for(const [id,radius,lanes] of [
    ['spirit_ray',.6,[[0,0,'spirit_pearl']]],
    ['solar_ray',.25,[[0,0,'solar_light'],[-.2,0,'solar_gold'],[.2,0,'solar_gold']]]
  ]) {
    const length=2, parts=[], contacts=[], roles={};
    lanes.forEach(([x,y,surface],i)=>{
      const geometry=new T.CapsuleGeometry(radius,length-radius*2,8,24).rotateX(Math.PI/2).translate(x,y,length*.5);
      const mesh=skin(geometry,p=>{
        const t=T.MathUtils.clamp((p.z-radius)/(length-radius*2),0,1);
        return [[0,1-t],[1,t]];
      });
      const item=part(`channel${i}`,mesh,surface);item.UseMatterSurface=false;
      for(let k=0;k<item.Triangles.length;k+=3)
        [item.Triangles[k],item.Triangles[k+2]]=[item.Triangles[k+2],item.Triangles[k]];
      parts.push(item);roles[item.Id]=['channel.body','channel.surface'];
      contacts.push({Id:`channel${i}`,Part:i,A:[x,y,radius],B:[x,y,length-radius],Radius:radius,AControl:'origin',BControl:'extent'});
    });
    write(id,parts,[{Id:'start',Part:0,Position:[0,0,0],Direction:[0,0,-1],Control:'origin'},
      {Id:'end',Part:0,Position:[0,0,length],Direction:[0,0,1],Control:'extent'}],
      contacts,controls,[],false,{Forward:[0,0,1],Up:[0,1,0]},
      structure('volume','forms_ray.mjs','makeSourceRays',{'span.length':length,'span.radius':radius},roles,[],['span']));
  }
}

/** 发光外缘与可接触体使用同一截面，内部亮芯只负责显示。旧形体仍可从正式构造加载。 */
export function makeLuminousRays() {
  const length=2;
  const controls=['origin','extent'].map(Id=>({Id,Parent:null,Position:[0,0,0],Rotation:[0,0,0,1]}));
  for(const [id,radius,lanes,core] of [
    ['spirit_ray_luminous',.30,[[0,'spirit_beam_edge']],'spirit_beam_core'],
    ['solar_ray_luminous',.15,[[0,'solar_beam_edge'],[-.12,'solar_beam_flank'],[.12,'solar_beam_flank']],'solar_beam_core']
  ]) {
    const parts=[], contacts=[], roles={};
    function addLayer(name,x,width,surface) {
      // 亮芯与外缘共用圆端中心；缩短射线时不会有一层伸出实际末端。
      const geometry=new T.CapsuleGeometry(width,length-radius*2,6,20).rotateX(Math.PI/2).translate(x,0,length*.5);
      geometry.userData.keepNormals=true;
      const mesh=skin(geometry,p=>{
        const t=T.MathUtils.clamp((p.z-radius)/(length-radius*2),0,1);
        return [[0,1-t],[1,t]];
      });
      const item=part(name,mesh,surface);item.UseMatterSurface=false;
      for(let k=0;k<item.Triangles.length;k+=3)
        [item.Triangles[k],item.Triangles[k+2]]=[item.Triangles[k+2],item.Triangles[k]];
      parts.push(item);
      roles[name]=['channel.body','channel.surface'];
    }
    lanes.forEach(([x,surface],i)=>{
      const name=`channel${i}`;
      addLayer(name,x,radius,surface);
      contacts.push({Id:name,Part:i,A:[x,0,radius],B:[x,0,length-radius],Radius:radius,AControl:'origin',BControl:'extent'});
    });
    addLayer('luminous_core',0,radius*.43,core);
    write(id,parts,[{Id:'start',Part:0,Position:[0,0,0],Direction:[0,0,-1],Control:'origin'},
      {Id:'end',Part:0,Position:[0,0,length],Direction:[0,0,1],Control:'extent'}],
      contacts,controls,[],false,{Forward:[0,0,1],Up:[0,1,0]},
      structure('volume','forms_ray.mjs','makeLuminousRays',{'span.length':length,'span.radius':radius},roles,[],['span']));
  }
}

/** 连锁使用有厚度的扭转雷体；亮芯位于外层内部，不额外增加作用次数。 */
export function makeThunderChannel() {
  const radius=.12,length=2,parts=[],roles={};
  const controls=['origin','extent'].map(Id=>({Id,Parent:null,Position:[0,0,0],Rotation:[0,0,0,1]}));
  for(const [name,r,surface] of [['body',radius,'thunder_body'],['core',radius,'thunder_core']]) {
    const geometry=new T.CapsuleGeometry(r,length-r*2,8,18).rotateX(Math.PI/2).translate(0,0,length*.5);
    const position=geometry.getAttribute('position');
    for(let i=0;i<position.count;i++) {
      const x=position.getX(i),y=position.getY(i),z=position.getZ(i),angle=Math.atan2(y,x);
      const fold=.87+.13*Math.cos(angle*3+z*4.2),turn=z*.8;
      position.setXYZ(i,(x*Math.cos(turn)-y*Math.sin(turn))*fold,(x*Math.sin(turn)+y*Math.cos(turn))*fold,z);
    }
    const selected=[],indices=geometry.index.array;
    for(let i=0;i<indices.length;i+=3) {
      const middle=new T.Vector3();
      for(let j=0;j<3;j++)middle.add(new T.Vector3().fromBufferAttribute(position,indices[i+j]));
      middle.multiplyScalar(1/3);
      const bright=Math.cos(Math.atan2(middle.y,middle.x)*3-middle.z*.2)>.2;
      if(bright===(name==='core'))selected.push(indices[i],indices[i+1],indices[i+2]);
    }
    geometry.setIndex(selected);
    const mesh=skin(geometry,p=>{const t=T.MathUtils.clamp((p.z-radius)/(length-radius*2),0,1);return [[0,1-t],[1,t]];});
    const item=part(name,mesh,surface);item.UseMatterSurface=false;
    for(let k=0;k<item.Triangles.length;k+=3)
      [item.Triangles[k],item.Triangles[k+2]]=[item.Triangles[k+2],item.Triangles[k]];
    parts.push(item);roles[name]=['channel.body','channel.surface'];
  }
  write('thunder_channel',parts,
    [{Id:'start',Part:0,Position:[0,0,0],Direction:[0,0,-1],Control:'origin'},
      {Id:'end',Part:0,Position:[0,0,length],Direction:[0,0,1],Control:'extent'}],
    [{Id:'channel',Part:0,A:[0,0,radius],B:[0,0,length-radius],Radius:radius,AControl:'origin',BControl:'extent'}],
    controls,[],false,{Forward:[0,0,1],Up:[0,1,0]},
    structure('volume','forms_ray.mjs','makeThunderChannel',{'span.length':length,'span.radius':radius},roles,[],['span']));
}
