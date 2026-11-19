import * as T from 'three';
import {part, structure, write} from './form_export.mjs';

function tablet(outline, top, width, depth, bevel) {
  const shape = new T.Shape(outline.map(([x, y]) => new T.Vector2(x * width, y)));
  return new T.ExtrudeGeometry(shape, { depth, bevelEnabled:true, bevelThickness:bevel, bevelSize:bevel,
    bevelSegments:2, steps:1, curveSegments:1 }).translate(0, 0, top);
}

/** 青瓷底片上的方位箭与刻度，实体提示记录所在方向。 */
export function makeObservedSignal() {
  const outline = [[-.62,-.37],[-.30,-.58],[.28,-.58],[.62,-.36],[.62,.44],[.39,.63],[-.39,.63],[-.62,.44]];
  const compass = [[0,.49],[.41,-.09],[.15,-.10],[.08,-.41],[-.08,-.41],[-.15,-.10],[-.41,-.09]];
  const parts = [part('ceramic', tablet(outline, 0, 1, .14, .045), 'signal_ceramic'),
    part('arrow', tablet(compass, .17, 1, .11, .024), 'signal_gold')];
  for (const item of parts) item.UseMatterSurface = false;
  write('signal_sight_tablet', parts, [{Id:'bearing',Part:0,Position:[0,0,0],Direction:[0,1,0]}], [], [], [], false,
    {Forward:[0,1,0],Up:[0,0,1]}, structure('articulated','forms_observed_signal.mjs','makeObservedSignal',
      {outline,compass}, {ceramic:['signal.record'],arrow:['signal.bearing']}, [['ceramic','arrow',[0,0,.17],false]]));
}
