/** 连续厚曲面的制作工具；表面、控制和作用取样保留相同的纵向参数。 */
import * as T from 'three';
import {motion, formation} from './form_export.mjs';

/** 沿曲面参数混合相邻两个控制，不依赖翻卷后可能重复的空间坐标。 */
export function curveInfluences(along, count) {
  const at = Math.min(count - 1, Math.max(0, along * (count - 1)));
  const lower = Math.min(count - 2, Math.floor(at)), blend = at - lower;
  return [[lower, 1 - blend], [lower + 1, blend]];
}

/** 制作纵向曲线及其法向，横向统一朝模型x轴。 */
export function waterCurve(points) {
  return new T.CatmullRomCurve3(points.map(([y, z]) => new T.Vector3(0, y, z)), false, 'catmullrom', .35);
}

/** 两面、两端和两侧均封口，厚度和表面坐标独立保留。 */
export function thickSheet(curve, {width, thickness, controls, rows = 80, columns = 56, foam = () => 0}) {
  const positions = [], uv = [], indices = [], weights = [], controlIndices = [], surface = [];
  const stride = columns + 1, faceCount = (rows + 1) * stride;
  for (let face = 0; face < 2; face++) for (let row = 0; row <= rows; row++) {
    const along = row / rows, centre = curve.getPoint(along), tangent = curve.getTangent(along);
    const normal = new T.Vector3(0, -tangent.z, tangent.y).normalize(), blend = curveInfluences(along, controls);
    for (let column = 0; column <= columns; column++) {
      const across = column / columns * 2 - 1;
      const half = thickness(along) * (.15 + .85 * Math.pow(1 - across * across, .35));
      const p = centre.clone().addScaledVector(normal, (face ? -1 : 1) * half);
      p.x = across * width(along);
      p.z += Math.sin(p.x * .8 + along * 4) * .11 * T.MathUtils.smoothstep(along, .35, .85);
      positions.push(...p.toArray()); uv.push(column / columns, along);
      surface.push(half * 2, foam(along, across, face));
      controlIndices.push(blend[0][0], blend[1][0], 0, 0); weights.push(blend[0][1], blend[1][1], 0, 0);
    }
  }
  for (let face = 0; face < 2; face++) for (let row = 0; row < rows; row++) for (let column = 0; column < columns; column++) {
    const a = face * faceCount + row * stride + column, b = a + stride;
    indices.push(...(face ? [a,b,a+1,a+1,b,b+1] : [a,a+1,b,a+1,b+1,b]));
  }
  // 封口沿原有边缘顶点连接，保留连续权重，不另加跟不上的静态端盖。
  for (let row = 0; row < rows; row++) for (const column of [0, columns]) {
    const a = row * stride + column, b = a + stride, c = a + faceCount, d = b + faceCount;
    indices.push(...(column ? [a,c,b,c,d,b] : [a,b,c,c,b,d]));
  }
  for (let column = 0; column < columns; column++) for (const row of [0, rows]) {
    const a = row * stride + column, b = a + 1, c = a + faceCount, d = b + faceCount;
    indices.push(...(row ? [a,b,c,c,b,d] : [a,c,b,c,d,b]));
  }
  const geometry = new T.BufferGeometry();
  geometry.setAttribute('position', new T.Float32BufferAttribute(positions, 3));
  geometry.setAttribute('uv', new T.Float32BufferAttribute(uv, 2));
  geometry.setAttribute('skinIndex', new T.Uint16BufferAttribute(controlIndices, 4));
  geometry.setAttribute('skinWeight', new T.Float32BufferAttribute(weights, 4));
  geometry.setAttribute('formSurface', new T.Float32BufferAttribute(surface, 2));
  geometry.setIndex(indices); geometry.userData.smoothSeams = true;
  formation(geometry, (_, i) => Math.floor(i % faceCount / stride) / rows,
    (point, at) => {const p = curve.getPoint(at); p.x = point.x; return p;});
  return geometry;
}

/** 将目标曲线的世界控制姿态换为已有格式的父级局部动作。 */
export function curveMotion(id, duration, controls, bindingCurve, targetCurve, samples = 64, markers = []) {
  return motion(id, duration, false, controls, samples, (index, phase) => {
    const curve = targetCurve(phase), at = index / (controls.length - 1);
    const world = new T.Matrix4().compose(curve.getPoint(at), new T.Quaternion().setFromUnitVectors(
      bindingCurve.getTangent(at), curve.getTangent(at)), new T.Vector3(1, 1, 1));
    if (index) {
      const parentAt = (index - 1) / (controls.length - 1);
      const parent = new T.Matrix4().compose(curve.getPoint(parentAt), new T.Quaternion().setFromUnitVectors(
        bindingCurve.getTangent(parentAt), curve.getTangent(parentAt)), new T.Vector3(1, 1, 1));
      world.premultiply(parent.invert());
    }
    const position = new T.Vector3(), rotation = new T.Quaternion();
    world.decompose(position, rotation, new T.Vector3());
    return {position: position.toArray(), rotation: rotation.toArray()};
  }, markers);
}
