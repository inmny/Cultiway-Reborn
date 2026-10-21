/** 导出可寻址部位和完整表面坐标，供 Unity 离线构建及共同形体描述使用。 */
import * as T from 'three';
import {mkdirSync, writeFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';

const output = fileURLToPath(new URL('../../artifacts/skill_rebuild/form_sources/', import.meta.url));
mkdirSync(output, {recursive: true});

function part(id, geometry, surface, pivot = [0, 0, 0]) {
  geometry.computeVertexNormals();
  const values = name => Array.from(geometry.getAttribute(name).array, value => Math.round(value * 1e6) / 1e6);
  return {Id: id, Surface: surface, Pivot: pivot, Positions: values('position'), Normals: values('normal'),
    Uv: values('uv'), Triangles: geometry.index ? Array.from(geometry.index.array) :
      Array.from({length: geometry.getAttribute('position').count}, (_, index) => index)};
}

function write(id, parts, anchors, contacts) {
  const value = {Version: 1, Id: id, Parts: parts, Anchors: anchors, Contacts: contacts};
  writeFileSync(output + id + '.json', JSON.stringify(value));
  console.log(`${id}: ${parts.length} 个部件，${parts.reduce((sum, item) => sum + item.Triangles.length / 3, 0)} 个三角形`);
}

// 剑刃由脊、刃面、窄边和逐段收尖构成；各面保留自己的表面方向。
const positions = [], uv = [], triangles = [];
const levels = [[0, .135, .035], [.13, .155, .038], [1.7, .13, .032], [2.15, .105, .028], [2.5, .003, .003]];
for (let face = 0; face < 8; face++) {
  for (const [z, width, depth] of levels) {
    const ring = [[-width, 0], [-width * .8, -depth * .35], [0, -depth], [width * .8, -depth * .35],
      [width, 0], [width * .8, depth * .35], [0, depth], [-width * .8, depth * .35]];
    for (const corner of [face, (face + 1) % 8]) {
      positions.push(...ring[corner], z);
      uv.push(corner / 8, z / 2.5);
    }
  }
  const offset = face * levels.length * 2;
  for (let row = 0; row < levels.length - 1; row++) {
    const a = offset + row * 2;
    triangles.push(a, a + 1, a + 3, a, a + 3, a + 2);
  }
}
const blade = new T.BufferGeometry();
blade.setAttribute('position', new T.Float32BufferAttribute(positions, 3));
blade.setAttribute('uv', new T.Float32BufferAttribute(uv, 2));
blade.setIndex(triangles);

const guardShape = new T.Shape();
guardShape.moveTo(-.41, -.085); guardShape.quadraticCurveTo(-.28, -.01, -.13, -.015);
guardShape.lineTo(.13, -.015); guardShape.quadraticCurveTo(.28, -.01, .41, -.085);
guardShape.lineTo(.43, -.025); guardShape.quadraticCurveTo(.27, .10, .13, .065);
guardShape.lineTo(-.13, .065); guardShape.quadraticCurveTo(-.27, .10, -.43, -.025); guardShape.closePath();
const guard = new T.ExtrudeGeometry(guardShape, {depth: .08, bevelEnabled: true, bevelSegments: 2, bevelSize: .012, bevelThickness: .012, steps: 1, curveSegments: 8});
guard.rotateX(Math.PI / 2); guard.translate(0, .04, -.035);
const grip = new T.CylinderGeometry(.055, .065, .55, 12, 6).rotateX(Math.PI / 2).translate(0, 0, -.35);
const pommel = new T.LatheGeometry([[0, -.78], [.045, -.775], [.08, -.73], [.075, -.675], [.04, -.64]].map(point => new T.Vector2(...point)), 16).rotateX(Math.PI / 2);
write('sword', [part('blade', blade, 'metal'), part('guard', guard, 'metal'), part('grip', grip, 'leather'), part('pommel', pommel, 'metal')],
  [{Id: 'grip', Part: 2, Position: [0, 0, -.34], Direction: [0, 0, 1]},
    {Id: 'tip', Part: 0, Position: [0, 0, 2.5], Direction: [0, 0, 1]}],
  [{Id: 'edge', Part: 0, A: [0, 0, .05], B: [0, 0, 2.48], Radius: .14}]);

// 分枝以独立部件输出，保留连接枢轴与末梢；生长及附着使用相同部位。
const branchParts = [], tips = [], roots = [];
const paths = [
  [[0, 0, 0], [.03, -.02, .55], [-.08, .03, 1.1], [.06, .02, 1.75]],
  [[-.035, .01, .65], [-.26, .04, .93], [-.5, -.04, 1.23], [-.63, .02, 1.46]],
  [[-.04, .025, 1.05], [.22, .07, 1.27], [.42, .12, 1.55], [.48, .14, 1.72]]
];
paths.forEach((points, index) => {
  const curve = new T.CatmullRomCurve3(points.map(point => new T.Vector3(...point)));
  const geometry = new T.TubeGeometry(curve, 28, index ? .045 : .095, 10, false);
  const attribute = geometry.getAttribute('position');
  for (let i = 0; i < attribute.count; i++) {
    const progress = Math.floor(i / 11) / 28;
    const centre = curve.getPointAt(progress);
    const value = new T.Vector3().fromBufferAttribute(attribute, i).sub(centre).multiplyScalar(1 - .86 * progress).add(centre);
    attribute.setXYZ(i, value.x, value.y, value.z);
  }
  branchParts.push(part(index ? `branch_${index}` : 'trunk', geometry, 'wood', points[0]));
  tips.push({Id: `tip_${index}`, Part: index, Position: points.at(-1), Direction: curve.getTangentAt(1).toArray()});
  roots.push({Id: `body_${index}`, Part: index, A: points[0], B: points.at(-1), Radius: index ? .055 : .1});
});
write('branch', branchParts, tips, roots);
