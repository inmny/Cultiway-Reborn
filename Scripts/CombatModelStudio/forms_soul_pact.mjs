import * as T from 'three';
import {part, structure, write} from './form_export.mjs';

function carving(outline, height, depth, bevel, hole) {
  const shape = new T.Shape(outline.map(([x, y]) => new T.Vector2(x, y)));
  if (hole) shape.holes.push(new T.Path(hole.map(([x, y]) => new T.Vector2(x, y))));
  return new T.ExtrudeGeometry(shape, { depth, bevelEnabled:true, bevelThickness:bevel, bevelSize:bevel,
    bevelSegments:2, steps:1, curveSegments:1 }).translate(0, 0, height);
}

/** 两瓣夹住一枚会变换归属的玉心；端点、链节与已兑现的负担共用这类实体。 */
export function makeSoulPactSeal() {
  const outline = [[-.88,0],[-.58,-.36],[-.20,-.55],[.20,-.55],[.58,-.36],[.88,0],[.58,.36],[.20,.55],[-.20,.55],[-.58,.36]];
  const inset = outline.map(([x,y]) => [x*.70,y*.65]).reverse();
  const heart = [[-.57,0],[-.24,-.10],[-.09,-.26],[.09,-.26],[.24,-.10],[.57,0],[.24,.10],[.09,.26],[-.09,.26],[-.24,.10]];
  const parts = [part('case', carving(outline, .01, .14, .039, inset), 'soul_seal_rim'),
    part('heart', carving(heart, .13, .13, .038), 'soul_seal_glass')];
  for (const item of parts) item.UseMatterSurface = false;
  write('soul_pact_seal', parts, [{Id:'chain',Part:0,Position:[0,0,0],Direction:[1,0,0]}], [], [], [], false,
    {Forward:[1,0,0],Up:[0,0,1]}, structure('articulated','forms_soul_pact.mjs','makeSoulPactSeal',
      {outline,heart}, {case:['pact.anchor'],heart:['pact.burden']}, [['case','heart',[0,0,.14],false]]));
}
