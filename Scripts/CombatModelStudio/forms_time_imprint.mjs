import * as T from 'three';
import {part, structure, write} from './form_export.mjs';

/** 压印片的平面轮廓与多层切边，不依靠线框表示时序。 */
function slab(outline, base, bevel, depth) {
  const shape = new T.Shape(outline.map(([x, y]) => new T.Vector2(x, y)));
  return new T.ExtrudeGeometry(shape, {steps: 1, depth, bevelEnabled: true, bevelSize: bevel,
    bevelThickness: bevel, bevelSegments: 2, curveSegments: 1}).translate(0, 0, base);
}

export function makeTimeImprint() {
  const outline = [[-.61,-.17],[-.43,-.47],[.42,-.47],[.61,-.17],[.48,.26],[.22,.53],[-.21,.53],[-.48,.26]];
  const crest = [[-.33,-.29],[.33,-.29],[.24,-.13],[.10,0],[.23,.20],[.15,.30],[-.15,.30],[-.23,.20],[-.10,0],[-.24,-.13]];
  const parts = [part('slate', slab(outline, .025, .055, .14), 'time_stamp_slate'),
    part('crest', slab(crest, .212, .023, .11), 'time_stamp_brass')];
  for (const side of [-1, 1]) parts.push(part(side < 0 ? 'left' : 'right',
    new T.CylinderGeometry(.071,.087,.073,6).rotateX(Math.PI / 2).translate(.41 * side, -.04, .24), 'time_stamp_brass'));
  for (const item of parts) item.UseMatterSurface = false;
  write('time_fold_imprint', parts, [{Id:'clasp', Part:0, Position:[0,0,0], Direction:[0,0,1]}], [], [], [], false,
    {Forward:[0,1,0], Up:[0,0,1]}, structure('articulated', 'forms_time_imprint.mjs', 'makeTimeImprint',
      {outline, crest}, {slate:['time.anchor'], crest:['time.imprint'], left:['time.fastening'], right:['time.fastening']},
      [['slate','crest',[0,0,.18],false],['slate','left',[-.41,-.04,.2],false],['slate','right',[.41,-.04,.2],false]]));
}
