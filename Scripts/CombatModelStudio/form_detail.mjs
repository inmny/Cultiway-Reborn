/** 离线近中远网格：只更新索引及压紧顶点，保留真实控制、表面和成形资料。 */
import * as T from 'three';
import {MeshoptSimplifier} from 'meshoptimizer';

await MeshoptSimplifier.ready;
const streams = {Positions:3,Normals:3,Uv:2,SkinIndices:4,SkinWeights:4,SurfaceData:2,BuildData:4};

/** 完全相同的顶点合并；不同权重、法线和接缝坐标仍独立。 */
function weld(part) {
  const packed={},mapping=[],lookup=new Map();
  for(const name in streams)if(part[name])packed[name]=[];
  for(let vertex=0;vertex<part.Positions.length/3;vertex++) {
    const key=Object.keys(packed).map(name=>part[name].slice(vertex*streams[name],(vertex+1)*streams[name]).join(',')).join(';');
    let index=lookup.get(key);
    if(index===undefined){index=lookup.size;lookup.set(key,index);for(const name in packed)packed[name].push(...part[name].slice(vertex*streams[name],(vertex+1)*streams[name]));}
    mapping.push(index);
  }
  return {...part,...packed,Triangles:part.Triangles.map(index=>mapping[index])};
}

/** 按发布资料构造少量实际姿态，减面同时约束变形后的表面。 */
function poses(controls,motions) {
  const names=new Map(controls.map((c,i)=>[c.Id,i]));
  function matrices(positions,rotations) {
    const world=[];
    for(let i=0;i<controls.length;i++) {
      const c=controls[i],local=new T.Matrix4().compose(new T.Vector3().fromArray(positions,i*3),new T.Quaternion().fromArray(rotations,i*4),new T.Vector3(1,1,1));
      world.push(c.Parent?world[names.get(c.Parent)].clone().multiply(local):local);
    }
    return world;
  }
  const bind=matrices(controls.flatMap(c=>c.Position),controls.flatMap(c=>c.Rotation)).map(m=>m.clone().invert());
  return motions.slice(0,2).flatMap(motion=>[.25,.75].map(p=>{
    const key=motion.Keys[Math.round((motion.Keys.length-1)*p)];
    return matrices(key.Positions,key.Rotations).map((m,i)=>m.multiply(bind[i]));
  }));
}

/** 所有等级保留原极值、挂点附近及实际接触端附近的顶点。 */
function locks(part,points) {
  const count=part.Positions.length/3,chosen=new Set(),position=i=>part.Positions.slice(i*3,i*3+3).join(',');
  for(let axis=0;axis<3;axis++)for(const sign of [-1,1]) {
    let best=0;for(let i=1;i<count;i++)if(part.Positions[i*3+axis]*sign>part.Positions[best*3+axis]*sign)best=i;
    chosen.add(position(best));
  }
  for(const point of points) {
    let best=0,distance=Infinity;
    for(let i=0;i<count;i++) {let d=0;for(let a=0;a<3;a++)d+=(part.Positions[i*3+a]-point[a])**2;if(d<distance){best=i;distance=d;}}
    chosen.add(position(best));
  }
  return Uint8Array.from({length:count},(_,i)=>chosen.has(position(i))?1:0);
}

/** 简化后的每项属性仍来自原顶点，不重新估算骨架和材料参数。 */
function compact(part,indices,suffix,error) {
  const used=[],mapping=new Map(),triangles=Array.from(indices,index=>{
    if(!mapping.has(index)){mapping.set(index,used.length);used.push(index);}return mapping.get(index);
  });
  const result={...part,Id:part.Id+suffix,Triangles:triangles,DetailError:error};
  for(const name in streams)if(part[name])result[name]=used.flatMap(index=>part[name].slice(index*streams[name],(index+1)*streams[name]));
  return result;
}

/** 近景保持原件，中远景锁住边界并限制属性及姿态偏差；低面数物件不强行减面。 */
export function details(part,controls,motions,points) {
  if(part.Triangles.length<=192)return [];
  const source=weld(part),positions=new Float32Array(source.Positions),scale=Math.max(.00001,MeshoptSimplifier.getScale(positions,3));
  const channels=[['Normals',[.4,.4,.4]],['Uv',[.05,.05]]];
  if(part.HasSurfaceData)channels.push(['SurfaceData',[.2,.1]]);
  if(part.HasBuildData)channels.push(['BuildData',[.1,.1,.1,2]]);
  if(part.Skin)channels.push(['SkinIndices',[1.5,1.5,1.5,1.5]],['SkinWeights',[3,3,3,3]]);
  const samples=part.Skin?poses(controls,motions):[];
  const weights=channels.flatMap(c=>c[1]).concat(samples.flatMap(()=>[2,2,2])),attributes=[];
  for(let vertex=0;vertex<positions.length/3;vertex++) {
    for(const [name] of channels)for(let axis=0;axis<streams[name];axis++) {
      let value=source[name][vertex*streams[name]+axis];
      if(name==='SkinIndices')value/=32;
      if(name==='BuildData'&&axis<3)value/=scale;
      attributes.push(value);
    }
    for(const pose of samples) {
      const point=new T.Vector3().fromArray(source.Positions,vertex*3),posed=new T.Vector3();
      for(let slot=0;slot<4;slot++)posed.addScaledVector(point.clone().applyMatrix4(pose[source.SkinIndices[vertex*4+slot]]),source.SkinWeights[vertex*4+slot]);
      attributes.push(posed.x/scale,posed.y/scale,posed.z/scale);
    }
  }
  const indices=new Uint32Array(source.Triangles),locked=locks(source,points),data=new Float32Array(attributes);
  const levels=[[.5,.004,'_mid'],[.18,.012,'_far']].map(([ratio,error,suffix])=>{
    const [result,actualError]=MeshoptSimplifier.simplifyWithAttributes(indices,positions,3,data,weights.length,weights,locked,Math.max(12,Math.floor(indices.length*ratio/3)*3),error,['LockBorder','RegularizeLight']);
    return compact(source,result,suffix,actualError);
  });
  return levels.every(level=>level.Triangles.length===part.Triangles.length)?[]:levels;
}
