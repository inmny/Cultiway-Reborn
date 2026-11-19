import * as T from 'three';
import {mergeGeometries} from 'three/addons/utils/BufferGeometryUtils.js';
import {part, structure, write} from './form_export.mjs';

/** 共用地表实体：后沿为原点，闭合石体的外包面也是人物实际接触面。 */
export function makeFoldingGround() {
  const perimeter=[[-.36,.015],[.26,0],[.48,.16],[.46,.71],[.29,1.04],[-.19,1.02],[-.47,.83],[-.49,.29]];
  const thickness=.38;
  const bands=[perimeter.map(()=>-thickness),[-.24,-.25,-.27,-.21,-.17,-.225,-.30,-.29],
    [-.093,-.135,-.155,-.103,-.067,-.103,-.16,-.136],perimeter.map(()=>0)];
  const faces=points=>{
    const g=new T.BufferGeometry();
    g.setAttribute('position',new T.Float32BufferAttribute(points.flat(),3));
    g.setAttribute('uv',new T.Float32BufferAttribute(points.flatMap(([x,y,z])=>[x+.49,z-y]),2));
    return g;
  };
  const parts=['stone','stone_strata','stone_ridge'].map((surface,band)=>{
    const levels=bands.slice(band,band+2), triangles=[];
    const point=(index,level)=>[perimeter[index][0],levels[level][index],perimeter[index][1]];
    for(let i=0;i<perimeter.length;i++) {
      const j=(i+1)%perimeter.length;
      triangles.push(point(i,0),point(i,1),point(j,0),point(j,0),point(i,1),point(j,1));
    }
    for(let i=1;i+1<perimeter.length;i++) {
      if(band===0) triangles.push(point(0,0),point(i,0),point(i+1,0));
      if(band===2) triangles.push(point(0,1),point(i+1,1),point(i,1));
    }
    return part(`layer_${band}`,faces(triangles),surface);
  });
  for(let group=0;group<2;group++) {
    const chips=Array.from({length:4},(_,i)=>{
      const radius=.034+.021*((i*7+group*3)%4);
      return new T.IcosahedronGeometry(radius,0).scale(1.43,.56,.96).rotateY(i*2.17+group)
        .translate((group===0?-1:1)*(.26+.035*i),-.095,.39+.13*i);
    });
    parts.push(part(`chips_${group}`,mergeGeometries(chips),group===0?'stone_strata':'stone_ridge'));
  }
  write('folding_ground_slab',parts,[],[{Id:'bed',Part:0,A:[0,-thickness*.5,0],
    Footprint:{Points:perimeter,HalfThickness:thickness*.5}} ],[],[],false,{Forward:[0,0,1],Up:[0,1,0],HideBelowGround:true},
    structure('rigid','forms_folding_ground.mjs','makeFoldingGround',{perimeter,thickness},
      Object.fromEntries(parts.map(p=>[p.Id,[p.Id.startsWith('chips')?'terrain.scree':'terrain.slab']]))));
}
