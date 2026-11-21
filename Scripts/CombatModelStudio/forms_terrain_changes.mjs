/** 抬地和降地后的岩层。每件只占一个已改变的地块。 */
import * as T from 'three';
import {part, structure, write} from './form_export.mjs';

function section(id, geometry, material) {
  const mesh = part(id, geometry, material);
  mesh.UseMatterSurface = false;
  for (let i = 0; i < mesh.Triangles.length; i += 3)
    [mesh.Triangles[i], mesh.Triangles[i + 2]] = [mesh.Triangles[i + 2], mesh.Triangles[i]];
  return mesh;
}

function publish(id, parts, parameters) {
  write(id, parts, [], [], [], [], false, {Forward:[0,1,0], Up:[0,0,1]},
    structure('volume', 'forms_terrain_changes.mjs', 'makeTerrainChanges', parameters,
      Object.fromEntries(parts.map(mesh => [mesh.Id, ['ground.surface']])), []));
}

/** 侧面错开的岩床与顶岩是两块完整的体积，有可见厚度和凹凸。 */
function plate(radius, depth, height, turn) {
  const shape = new T.Shape();
  const edge = [[-.91,-.54],[-.62,-.99],[.21,-.90],[.86,-.67],[.94,.24],
    [.65,.93],[-.12,.91],[-.91,.64]];
  edge.forEach(([x,y], i) => shape[i === 0 ? 'moveTo' : 'lineTo'](x * radius, y * radius));
  shape.closePath();
  const rock = new T.ExtrudeGeometry(shape, {depth, bevelEnabled:true, bevelThickness:.043,
    bevelSize:.043, bevelSegments:1, steps:1});
  const positions = rock.getAttribute('position');
  for (let i = 0; i < positions.count; i++) {
    const x = positions.getX(i), y = positions.getY(i), z = positions.getZ(i);
    positions.setZ(i, z + .034 * Math.sin(x * 13 + y * 5) + .023 * Math.cos(x * 10 - y * 12));
  }
  return rock.rotateZ(turn).translate(0, 0, height);
}

export function makeTerrainChanges() {
  publish('terrain_raised_slab', [
    section('bed', plate(.44, .19, -.045, .01), 'stone_ridge'),
    section('crown', plate(.34, .135, .105, -.17), 'stone')
  ], {width:.96, layers:2, height:.32});

  const steps = [[-.19,.14,.15], [.13,-.18,.05], [.19,.16,.1]];
  publish('terrain_lowered_steps', steps.map(([x,y,z], i) =>
    section(`ledge_${i}`, plate(.218, .14, z - .12, i * .47).translate(x, y, 0), 'stone_ridge')),
    {width:.95, layers:3, height:.30});

}
