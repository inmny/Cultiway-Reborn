/** 两仪的暗端、明端与渡流各有实体部件；入账的方向由游戏给出。 */
import * as T from 'three';
import {mergeGeometries} from 'three/addons/utils/BufferGeometryUtils.js';
import {part, structure, surfaceData, write} from './form_export.mjs';

const facing = {Forward: [0, 1, 0], Up: [0, 0, 1]};
const origin = [0, 0, 0];
const shell = (id, items, surface, thickness) => ({
  ...part(id, surfaceData(mergeGeometries(items), thickness), surface), UseMatterSurface: false
});

/** 有内壁、翻边和横向弧面的扇片；弯曲整张实体以保留真实壁厚。 */
function vane(width, length, bend, twist) {
  const shape = new T.Shape();
  shape.moveTo(-width * .24, -.12 * length);
  shape.bezierCurveTo(-width * .73, .12 * length, -width * 1.04, .51 * length, -width * .55, .82 * length);
  shape.quadraticCurveTo(-width * .11, 1.03 * length, width * .62, length);
  shape.bezierCurveTo(width * .43, .91 * length, width * .84, .49 * length, width * .38, .27 * length);
  shape.quadraticCurveTo(width * .1, .07 * length, width * .27, -.08 * length);
  shape.closePath();
  const depth = width * .24;
  const mesh = new T.ExtrudeGeometry(shape, {depth, steps: 1, curveSegments: 5, bevelEnabled: true,
    bevelSegments: 1, bevelThickness: depth * .28, bevelSize: depth * .28});
  const positions = mesh.getAttribute('position');
  for (let i = 0; i < positions.count; i++) {
    const x = positions.getX(i), y = positions.getY(i), z = positions.getZ(i);
    const t = Math.max(0, Math.min(1, y / length));
    positions.setXYZ(i, x + twist * t * t, y, z + bend * t * t - x * x / width * .23);
  }
  return mesh;
}

/** 送出端是不对称的沉色卷片，收受端是有内瓤的翻开的明瓣。 */
function endpoint(receive) {
  const leaves = [], inners = [], seeds = [];
  const angles = (receive ? [26, 126, 220, 314] : [45, 132, 216, 282]).map(a => a * Math.PI / 180);
  angles.forEach((angle, i) => {
    const size = (receive ? [.79, .63, .84, .66] : [.67, .84, .59, .73])[i];
    const width = (receive ? .36 : .42) * (.95 + i * .035);
    const twist = (receive ? -.12 : .23) * (1 - i * .10);
    const curve = receive ? .45 - .08 * i : -.34 + .04 * i;
    const lift = receive ? .08 : .34;
    leaves.push(vane(width, size, curve, twist).translate(0, .04, lift).rotateZ(angle));
    inners.push(vane(width * .53, size * .79, curve * .92, twist * .75)
      .translate(-.025, .046, lift + .085).rotateZ(angle));
    seeds.push(new T.IcosahedronGeometry(.12, 1).scale(.68, .97, .55)
      .translate(.08 * Math.cos(angle), .09 * Math.sin(angle), .165 + i * .024));
  });
  const pieces = [shell('outer_wall', leaves, receive ? 'polarity_pearl' : 'polarity_ink', .68),
    shell('folded_lining', inners, receive ? 'polarity_gold' : 'polarity_depth', .52),
    shell('settled_seed', seeds, receive ? 'polarity_core' : 'polarity_depth', .7)];
  return {pieces, angles};
}

export function makePolarity() {
  const source = 'forms_polarity.mjs', entry = 'makePolarity';
  for (const receive of [false, true]) {
    const {pieces, angles} = endpoint(receive);
    write(receive ? 'polarity_receiver' : 'polarity_sender', pieces, [], [], [], [], false, facing,
      structure('volume', source, entry, {angles},
        {outer_wall: ['aura.body'], folded_lining: ['aura.fold'], settled_seed: ['aura.core']},
        [['outer_wall', 'folded_lining', origin], ['folded_lining', 'settled_seed', origin]]));
  }

  // 渡流前端更饱满，后端分出两片短鳞；两种材质在同一个移动体上交错。
  const fins = [vane(.21, .68, .20, .11).translate(-.17, -.36, -.07),
    vane(.18, .60, .22, -.10).translate(.18, -.34, -.10).rotateY(.81),
    vane(.21, .54, -.13, -.12).translate(.065, -.26, .13).rotateY(1.15)];
  const seed = new T.IcosahedronGeometry(.25, 2).scale(.79, 1.65, .78).translate(0, .20, .032);
  const echo = new T.IcosahedronGeometry(.14, 1).scale(.76, 1.14, .76).translate(.055, -.26, .045);
  write('polarity_packet', [shell('wrapped_fins', fins, 'polarity_ink', .65),
    shell('leading_kernel', [seed], 'polarity_core', .83), shell('trailing_kernel', [echo], 'polarity_depth', .56)], [], [], [], [], false, facing,
    structure('rigid', source, entry, {length: 1},
      {wrapped_fins: ['carrier.body'], leading_kernel: ['carrier.core'], trailing_kernel: ['carrier.tail']},
      [['wrapped_fins', 'leading_kernel', [0, .20, .032]], ['wrapped_fins', 'trailing_kernel', [.055, -.26, .045]]]));

  // 接触底印只标出实际触碰的小片区域，入账后才会生成两端和渡流。
  const rim = [], metal = [];
  for (const side of [-1, 1]) {
    const turn = side === -1 ? Math.PI : 0;
    rim.push(vane(.22, .47, .034, .09).rotateZ(turn));
    metal.push(vane(.14, .37, .03, .08).translate(0, .013, .036).rotateZ(turn));
  }
  const contacts = [{Id: 'junction', Part: 0, A: [0, -.26, .04], B: [0, .26, .04], Radius: .24}];
  write('polarity_seal', [shell('junction_wall', rim, 'polarity_pearl', .42),
    shell('junction_inlay', metal, 'polarity_depth', .45)], [], contacts, [], [], false, facing,
    structure('surface', source, entry, {radius: .24, length: .52},
      {junction_wall: ['junction.contact'], junction_inlay: ['junction.inlay']}, [['junction_wall', 'junction_inlay', origin]]));
}
