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
