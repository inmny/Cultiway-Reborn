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
    const solar=id==='solar_ray_luminous';
    function addLayer(name,x,width,surface,inside=false) {
      // 圆端与实际接触相合。阳光内部的长流改变截面厚度，不把多条笔直光柱并排。
      const geometry=new T.CapsuleGeometry(width,length-radius*2,solar?8:6,solar?24:20).rotateX(Math.PI/2).translate(x,0,length*.5);
      if(solar&&inside) {
        const p=geometry.getAttribute('position');
        for(let k=0;k<p.count;k++) {
          const z=p.getZ(k),u=T.MathUtils.clamp((z-radius)/(length-2*radius),0,1);
          const tide=.59+.32*Math.pow(Math.sin(Math.PI*(1.55*u+.13)),2);
          p.setXYZ(k,(p.getX(k)-x)*tide+x+.023*Math.sin(u*Math.PI*2),p.getY(k)*tide,z);
        }
        geometry.computeVertexNormals();
      }
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
    addLayer('luminous_core',0,radius*(solar?.68:.43),core,solar);
    if(solar) {
      const seed=part('solar_seed',new T.IcosahedronGeometry(.132,2).scale(1,1,1.03).translate(0,0,.16),'solar_kernel',[0,0,0],'origin');
      for(let k=0;k<seed.Triangles.length;k+=3)
        [seed.Triangles[k],seed.Triangles[k+2]]=[seed.Triangles[k+2],seed.Triangles[k]];
      seed.UseMatterSurface=false;parts.push(seed);roles[seed.Id]=['channel.body'];
    }
    write(id,parts,[{Id:'start',Part:0,Position:[0,0,0],Direction:[0,0,-1],Control:'origin'},
      {Id:'end',Part:0,Position:[0,0,length],Direction:[0,0,1],Control:'extent'}],
      contacts,controls,[],false,{Forward:[0,0,1],Up:[0,1,0]},
      structure('volume','forms_ray.mjs','makeLuminousRays',{'span.length':length,'span.radius':radius},roles,[],['span']));
  }
}

/** 连锁段保留原接触胶囊，电弧折芯和分梢始终收在同一外缘内。 */
export function makeThunderChannel() {
  const radius=.12,length=2,span=length-radius*2,parts=[],roles={};
  const controls=['origin','extent'].map(Id=>({Id,Parent:null,Position:[0,0,0],Rotation:[0,0,0,1]}));
  function addLayer(name,geometry,surface) {
    const mesh=skin(geometry,p=>{
      const t=T.MathUtils.clamp((p.z-radius)/span,0,1);
      return [[0,1-t],[1,t]];
    });
    const item=part(name,mesh,surface);item.UseMatterSurface=false;
    for(let k=0;k<item.Triangles.length;k+=3)
      [item.Triangles[k],item.Triangles[k+2]]=[item.Triangles[k+2],item.Triangles[k]];
    parts.push(item);roles[name]=['channel.body','channel.surface'];
  }
  const envelope=new T.CapsuleGeometry(radius,span,6,16).rotateX(Math.PI/2).translate(0,0,length*.5);
  envelope.userData.keepNormals=true;
  addLayer('body',envelope,'thunder_corona');

  // 折芯是连续的多面体。宽段和窄段错开，三个方向的凸起在不同镜头仍能读到。
  function arc(sections) {
    const sides=8,points=[],uv=[],faces=[];
    sections.forEach(([z,x,y,width],level)=>{
      const cap=Math.max(radius-z,z-(length-radius),0);
      const limit=Math.sqrt(Math.max(0,radius*radius-cap*cap))-Math.hypot(x,y);
      for(let side=0;side<sides;side++) {
        const angle=2*Math.PI*side/sides;
        const r=Math.min(width,Math.max(0,limit))*(.86+.14*Math.cos(angle*3+z*3.9));
        points.push(x+r*Math.cos(angle),y+r*Math.sin(angle),z);
        uv.push(side/sides,z/length);
        if(level===0) continue;
        const a=(level-1)*sides+side,b=(level-1)*sides+(side+1)%sides,c=level*sides+side,d=level*sides+(side+1)%sides;
        if(sections[level-1][3]>0) faces.push(a,b,d);
        if(width>0) faces.push(a,d,c);
      }
    });
    const geometry=new T.BufferGeometry();
    geometry.setAttribute('position',new T.Float32BufferAttribute(points,3));
    geometry.setAttribute('uv',new T.Float32BufferAttribute(uv,2));
    geometry.setIndex(faces);
    return geometry;
  }
  addLayer('core',arc([
    [0,0,0,0],[.12,0,0,.054],[.29,-.015,.010,.045],
    [.48,.038,.017,.073],[.65,-.041,-.014,.033],[.90,.026,-.040,.072],
    [1.12,.041,.011,.038],[1.34,-.038,.038,.060],[1.55,.016,-.020,.030],
    [1.77,.023,.007,.057],[1.88,0,0,.046],[2,0,0,0]
  ]),'thunder_arc');
  addLayer('fork_left',arc([
    [.29,-.015,.010,0],[.41,-.050,.027,.033],[.56,-.077,.025,.025],
    [.72,-.099,-.021,.010],[.78,-.107,-.021,0]
  ]),'thunder_arc_fork');
  addLayer('fork_right',arc([
    [1.12,.041,.011,0],[1.25,.064,-.035,.024],[1.39,.093,-.035,.016],[1.51,.096,.014,0]
  ]),'thunder_arc_fork');
  write('thunder_channel',parts,
    [{Id:'start',Part:0,Position:[0,0,0],Direction:[0,0,-1],Control:'origin'},
      {Id:'end',Part:0,Position:[0,0,length],Direction:[0,0,1],Control:'extent'}],
    [{Id:'channel',Part:0,A:[0,0,radius],B:[0,0,length-radius],Radius:radius,AControl:'origin',BControl:'extent'}],
    controls,[],false,{Forward:[0,0,1],Up:[0,1,0]},
    structure('volume','forms_ray.mjs','makeThunderChannel',{'span.length':length,'span.radius':radius},roles,[],['span']));
}
