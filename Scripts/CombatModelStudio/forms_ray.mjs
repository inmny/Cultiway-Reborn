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
