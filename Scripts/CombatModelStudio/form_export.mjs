/** 带部件、权重、骨架和动作的统一制作交换，不扁平化为面色。 */
import * as T from 'three';
import {mkdirSync, writeFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import {details} from './form_detail.mjs';

export const output = fileURLToPath(new URL('../../artifacts/skill_rebuild/form_sources/', import.meta.url));
mkdirSync(output, {recursive: true});
export const round = value => Math.round(value * 1e6) / 1e6;

/** 保留标准顶点通道；每个部件有独立表面，可选择刚性控制或顶点权重。 */
export function part(id, geometry, surface, pivot = [0, 0, 0], control = null) {
  if (!geometry.userData.keepNormals) geometry.computeVertexNormals();
  if (geometry.userData.smoothSeams) {
    const position = geometry.getAttribute('position'), normal = geometry.getAttribute('normal'), sums = new Map();
    const key = i => [position.getX(i),position.getY(i),position.getZ(i)].map(v=>Math.round(v*1e5)).join(',');
    for (let i=0;i<position.count;i++) {
      const name=key(i);if(!sums.has(name))sums.set(name,new T.Vector3());
      sums.get(name).add(new T.Vector3().fromBufferAttribute(normal,i));
    }
    for (let i=0;i<position.count;i++) {const value=sums.get(key(i)).clone().normalize();normal.setXYZ(i,value.x,value.y,value.z);}
  }
  const values = name => Array.from(geometry.getAttribute(name).array, round);
  const skin = !!geometry.getAttribute('skinIndex');
  const hasSurface = !!geometry.getAttribute('formSurface');
  const hasBuild = !!geometry.getAttribute('formBuild');
  return {Id: id, Surface: surface, Pivot: pivot, Control: control, Skin: skin, HasSurfaceData: hasSurface, HasBuildData: hasBuild,
    UseMatterSurface: !['leather','foam'].includes(surface),
    Positions: values('position'), Normals: values('normal'), Uv: values('uv'),
    Triangles: geometry.index ? Array.from(geometry.index.array) : Array.from({length: geometry.getAttribute('position').count}, (_, i) => i),
    ...(skin ? {SkinIndices: values('skinIndex'), SkinWeights: values('skinWeight')} : {}),
    ...(hasSurface ? {SurfaceData: values('formSurface')} : {}),
    ...(hasBuild ? {BuildData: values('formBuild')} : {})};
}

/** 局部厚度随顶点保留，泡沫覆盖只改变表面，不暗中增加攻击范围。 */
export function surfaceData(geometry, thickness, foam = () => 0) {
  const values = [], position = geometry.getAttribute('position');
  for(let i=0;i<position.count;i++) {
    const point = new T.Vector3().fromBufferAttribute(position,i);
    values.push(typeof thickness === 'number' ? thickness : thickness(point,i),foam(point,i));
  }
  geometry.setAttribute('formSurface',new T.Float32BufferAttribute(values,2));
  geometry.userData.smoothSeams = true;
  return geometry;
}

/** 按制作资料归集各控制实际影响的顶点，供运行使用保守姿态边界。 */
/** 写出完整说明；facing 声明制作前上向，省略时保留旧资源约定。 */
export function write(id, parts, anchors, contacts, controls = [], motions = [], conform = false, facing = null, structure = null, grips = [], actions = [], held = null) {
  if (!structure) throw new Error(`${id}: 缺少明确的制作结构`);
  parts = parts.map(item => {
    const roles = structure.Roles[item.Id];
    if (!roles?.length) throw new Error(`${id}/${item.Id}: 缺少部位用途`);
    return {...item, Roles: roles};
  });
  const changes = ['transform', ...(structure.Changes ?? [])];
  if (parts.some(p => p.UseMatterSurface)) changes.push('material');
  if (structure.Connections.length && structure.Connections.every(c => c.Detachable)) changes.push('separate');
  if (controls.length && motions.length) changes.push('motion');
  if (conform) changes.push('ground');
  if (parts.some(p => p.HasBuildData)) changes.push('build');
  const lookup = new Map(controls.map((control, i) => [control.Id, i]));
  const boxes = controls.map(() => new T.Box3());
  for (const item of parts) {
    for (let vertex = 0; vertex < item.Positions.length / 3; vertex++) {
      const position = new T.Vector3().fromArray(item.Positions, vertex * 3);
      if (item.Skin) {
        let sum = 0;
        for (let influence = 0; influence < 4; influence++) {
          const offset = vertex * 4 + influence, index = item.SkinIndices[offset], weight = item.SkinWeights[offset];
          if (!Number.isInteger(index) || index < 0 || index >= controls.length || !Number.isFinite(weight) || weight < 0)
            throw new Error(`${id}/${item.Id}: 顶点控制或权重无效`);
          sum += weight;
          if (weight > 0) {
            boxes[index].expandByPoint(position);
            if (item.HasBuildData) boxes[index].expandByPoint(new T.Vector3().fromArray(item.BuildData, vertex * 4));
          }
        }
        if (Math.abs(sum - 1) > .00001) throw new Error(`${id}/${item.Id}: 顶点权重没有归一`);
      } else if (item.Control != null) {
        if (!lookup.has(item.Control)) throw new Error(`${id}/${item.Id}: 刚性控制不存在`);
        boxes[lookup.get(item.Control)].expandByPoint(position);
      }
    }
  }
  controls = controls.map((control, i) => ({...control, ...(!boxes[i].isEmpty() ?
    {Minimum: boxes[i].min.toArray().map(round), Maximum: boxes[i].max.toArray().map(round)} : {})}));
  const {Roles, ...description} = structure;
  motions = motions.map(clip => ({...clip, Keys: clip.Keys.map(key => ({...key, Build: key.Build ?? 1}))}));
  parts=parts.map(({KeepFullDetail,...part},index)=>({...part,Details:KeepFullDetail ? [] : details(part,controls,motions,
    [...anchors.filter(a=>a.Part===index).map(a=>a.Position),...contacts.filter(c=>c.Part===index).flatMap(c=>c.Path
      ? c.Path.Points.map(([x,z])=>[c.A[0]+x,c.A[1],c.A[2]+z])
      : [c.A,c.B].filter(Boolean))])}));
  const value = {Version: 8, Id: id, Structure: {...description, Changes: changes}, Parts: parts, Anchors: anchors, Grips: grips, Held: held ?? {Groups: [], Scale: 0}, Actions: actions, Contacts: contacts, Controls: controls, Motions: motions,
    ...(conform ? {ConformToGround:true} : {}), ...(facing ?? {})};
  writeFileSync(output + id + '.json', JSON.stringify(value));
  console.log(`${id}: ${parts.length} 个部件，${controls.length} 个控制，${motions.length} 个动作，${parts.reduce((sum, item) => sum + item.Triangles.length / 3, 0)} 个三角形`);
  return value;
}

/** 制作时明确提供用途、连接和实际生成输入，导入器只保留资料，不猜模型结构。 */
export function structure(kind, source, entry, parameters, roles, connections = [], changes = []) {
  return {Kind: kind, UnitInTiles: 1, Generation: {Source: `Scripts/CombatModelStudio/${source}`, Entry: entry,
    Parameters: Object.entries(parameters).map(([Id, values]) => ({Id, Values: (Array.isArray(values) ? values.flat(Infinity) : [values]).map(round)}))},
    Roles: roles, Changes: changes, Connections: connections.map(([Parent, Child, Position, Detachable = true]) => ({Id: `${Parent}/${Child}`, Parent, Child, Position, Detachable}))};
}

/** 给几何的每个顶点记录最多四个正权重控制。 */
export function skin(geometry, weights) {
  const indices = [], values = [], position = geometry.getAttribute('position');
  for (let i = 0; i < position.count; i++) {
    const influences = weights(new T.Vector3().fromBufferAttribute(position, i));
    const sum = influences.reduce((value, influence) => value + influence[1], 0);
    for (let slot = 0; slot < 4; slot++) {indices.push(influences[slot]?.[0] ?? 0); values.push((influences[slot]?.[1] ?? 0) / sum);}
  }
  geometry.setAttribute('skinIndex', new T.Uint16BufferAttribute(indices, 4));
  geometry.setAttribute('skinWeight', new T.Float32BufferAttribute(values, 4));
  return geometry;
}

/** 顶点按纵向参数成形，截面收拢中心随原有权重一起变形。 */
export function formation(geometry, along, centre) {
  const data = [], position = geometry.getAttribute('position');
  for (let i = 0; i < position.count; i++) {
    const point = new T.Vector3().fromBufferAttribute(position, i), at = T.MathUtils.clamp(along(point, i), 0, 1);
    data.push(...centre(point, at, i).toArray(), at * .96);
  }
  geometry.setAttribute('formBuild', new T.Float32BufferAttribute(data, 4));
  return geometry;
}

/** 同一动作关键点控制成形进度，无额外运行时钟。 */
export function forming(clip, progress = p => p) {
  return {...clip, Keys: clip.Keys.map(key => ({...key, Build: round(progress(key.At / clip.Duration))}))};
}

/** 将平滑制作函数采样为完整局部姿态，原版及三维使用同一资料。 */
export function motion(id, duration, loop, controls, count, evaluate, markers = []) {
  const keys = [];
  const times = [...new Set([...Array.from({length:count+1},(_,i)=>round(duration*i/count)),...markers.map(marker=>marker.At)])].sort((a,b)=>a-b);
  for (const time of times) {
    const positions = [], rotations = [], phase = time / duration;
    controls.forEach((control, index) => {
      const value = evaluate(index, phase, control);
      positions.push(...(value.position ?? control.Position).map(round));
      rotations.push(...(value.rotation ?? control.Rotation).map(round));
    });
    keys.push({At: time, Positions: positions, Rotations: rotations});
  }
  return {Id: id, Duration: duration, Loop: loop, Keys: keys, Markers: markers};
}
