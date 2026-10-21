/** 带部件、权重、骨架和动作的统一制作交换，不扁平化为面色。 */
import * as T from 'three';
import {mkdirSync, writeFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';

export const output = fileURLToPath(new URL('../../artifacts/skill_rebuild/form_sources/', import.meta.url));
mkdirSync(output, {recursive: true});
export const round = value => Math.round(value * 1e6) / 1e6;

/** 保留标准顶点通道；每个部件有独立表面，可选择刚性控制或顶点权重。 */
export function part(id, geometry, surface, pivot = [0, 0, 0], control = null) {
  geometry.computeVertexNormals();
  const values = name => Array.from(geometry.getAttribute(name).array, round);
  const skin = !!geometry.getAttribute('skinIndex');
  return {Id: id, Surface: surface, Pivot: pivot, Control: control, Skin: skin,
    Positions: values('position'), Normals: values('normal'), Uv: values('uv'),
    Triangles: geometry.index ? Array.from(geometry.index.array) : Array.from({length: geometry.getAttribute('position').count}, (_, i) => i),
    ...(skin ? {SkinIndices: values('skinIndex'), SkinWeights: values('skinWeight')} : {})};
}

/** 按制作资料归集各控制实际影响的顶点，供运行使用保守姿态边界。 */
export function write(id, parts, anchors, contacts, controls = [], motions = []) {
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
          sum += weight; if (weight > 0) boxes[index].expandByPoint(position);
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
  const value = {Version: 2, Id: id, Parts: parts, Anchors: anchors, Contacts: contacts, Controls: controls, Motions: motions};
  writeFileSync(output + id + '.json', JSON.stringify(value));
  console.log(`${id}: ${parts.length} 个部件，${controls.length} 个控制，${motions.length} 个动作，${parts.reduce((sum, item) => sum + item.Triangles.length / 3, 0)} 个三角形`);
  return value;
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

/** 将平滑制作函数采样为完整局部姿态，原版及三维使用同一资料。 */
export function motion(id, duration, loop, controls, count, evaluate) {
  const keys = [];
  for (let i = 0; i <= count; i++) {
    const positions = [], rotations = [], phase = i / count;
    controls.forEach((control, index) => {
      const value = evaluate(index, phase, control);
      positions.push(...(value.position ?? control.Position).map(round));
      rotations.push(...(value.rotation ?? control.Rotation).map(round));
    });
    keys.push({At: round(duration * phase), Positions: positions, Rotations: rotations});
  }
  return {Id: id, Duration: duration, Loop: loop, Keys: keys};
}
