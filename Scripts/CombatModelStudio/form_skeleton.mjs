/** 连续主体的离线制作工具；运行中仍使用已有共享网格和骨架。 */
import * as T from 'three';
import {MarchingCubes} from 'three/addons/objects/MarchingCubes.js';
import {part, skin} from './form_export.mjs';

/** 全局绑定点只在制作时转为父级局部坐标，层次仍明确保留。 */
export function controlsFromPoints(entries) {
  const points = new Map(entries.map(([id,,p])=>[id,new T.Vector3(...p)]));
  return entries.map(([Id,Parent,p])=>({Id,Parent,Position:new T.Vector3(...p)
    .sub(Parent?points.get(Parent):new T.Vector3()).toArray(),Rotation:[0,0,0,1]}));
}

/** 混合后只保留四项主要影响，再统一归一；所有分区使用同一计算。 */
export function blendWeights(items) {
  const sums = new Map();
  for(const [index,weight] of items) if(weight>1e-7)sums.set(index,(sums.get(index)??0)+weight);
  const selected = [...sums].sort((a,b)=>b[1]-a[1]).slice(0,4);
  const sum=selected.reduce((a,b)=>a+b[1],0);
  return selected.map(([i,w])=>[i,w/sum]);
}

/** 局部姿态的角度转换与已有四元数取样格式一致。 */
export function rotation(x=0,y=0,z=0) {
  return new T.Quaternion().setFromEuler(new T.Euler(x*Math.PI/180,y*Math.PI/180,z*Math.PI/180)).toArray();
}

/** 用平滑关键姿态制作蓄势、接触和收势，不在运行时分支猜动作。 */
export function phaseValue(phase,keys) {
  let index=0;while(index<keys.length-2&&phase>keys[index+1][0])index++;
  const a=keys[index],b=keys[index+1],t=T.MathUtils.smoothstep(phase,a[0],b[0]);
  return a[1].map((value,i)=>T.MathUtils.lerp(value,b[1][i],t));
}

/** 按同一组体积提取连续表面，再按最近制作部位分区，保留共同法向和权重。 */
export function implicitParts(components,groups,minimum,maximum,resolution,weights,smooth=.1) {
  const material=new T.MeshBasicMaterial();
  const surface=new MarchingCubes(resolution,material,false,false,90000);surface.isolation=0;
  const size=new T.Vector3(...maximum).sub(new T.Vector3(...minimum));
  function distance(p,item) {
    const q=[0,1,2].map(i=>p[i]-item.Center[i]);
    const a=Math.hypot(...q.map((v,i)=>v/item.Radius[i]));
    const b=Math.hypot(...q.map((v,i)=>v/(item.Radius[i]*item.Radius[i])));
    return b<1e-9?-Math.min(...item.Radius):a*(a-1)/b;
  }
  for(let z=0;z<resolution;z++)for(let y=0;y<resolution;y++)for(let x=0;x<resolution;x++) {
    const p=[minimum[0]+x/resolution*size.x,minimum[1]+y/resolution*size.y,minimum[2]+z/resolution*size.z];
    let value=Infinity;
    for(const item of components) {
      const d=distance(p,item);
      if(!Number.isFinite(value)){value=d;continue;}
      const h=Math.max(0,smooth-Math.abs(value-d))/smooth;
      value=Math.min(value,d)-h*h*smooth*.25;
    }
    surface.field[x+resolution*y+resolution*resolution*z]=-value;
  }
  surface.update();
  if(surface.count>=90000*3)throw new Error('连续主体超出离线表面预算');
  const position=surface.geometry.getAttribute('position'),normal=surface.geometry.getAttribute('normal');
  const buckets=groups.map(()=>({positions:[],normals:[],uv:[]}));
  for(let triangle=0;triangle<surface.count;triangle+=3) {
    const vertices=[];const normals=[];
    for(let corner=0;corner<3;corner++) {
      const i=triangle+corner;
      vertices.push(new T.Vector3((position.getX(i)+1)*size.x*.5+minimum[0],(position.getY(i)+1)*size.y*.5+minimum[1],(position.getZ(i)+1)*size.z*.5+minimum[2]));
      normals.push(new T.Vector3(normal.getX(i)/size.x,normal.getY(i)/size.y,normal.getZ(i)/size.z).normalize());
    }
    const center=vertices.reduce((a,b)=>a.add(b),new T.Vector3()).multiplyScalar(1/3).toArray();
    let nearest=components[0],best=Infinity;
    for(const item of components){const d=distance(center,item);if(d<best){nearest=item;best=d;}}
    const bucket=buckets[nearest.Group];
    vertices.forEach((p,i)=>{bucket.positions.push(...p.toArray());bucket.normals.push(...normals[i].toArray());
      bucket.uv.push(p.x/size.x+.5,(p.z-minimum[2])/size.z);});
  }
  surface.geometry.dispose();material.dispose();
  return groups.map((group,i)=> {
    const bucket=buckets[i];if(!bucket.positions.length)throw new Error('连续主体缺少实际部位：'+group.Id);
    const geometry=new T.BufferGeometry();geometry.setAttribute('position',new T.Float32BufferAttribute(bucket.positions,3));
    geometry.setAttribute('normal',new T.Float32BufferAttribute(bucket.normals,3));
    geometry.setAttribute('uv',new T.Float32BufferAttribute(bucket.uv,2));geometry.userData.keepNormals=true;
    return part(group.Id,skin(geometry,weights),group.Surface??'bronze');
  });
}

/** 作用端点与实际表面使用同一权重；参数保持为可发布的命名控制。 */
export function influences(point,weights,controls) {
  return weights(new T.Vector3(...point)).map(([i,Weight])=>({Control:controls[i].Id,Weight}));
}
