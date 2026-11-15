/** 独立冰晶墙和盘根木壁：每块实体与自身接触共用地表控制，保留封闭体积。 */
import * as T from 'three';
import {part, write, structure, skin} from './form_export.mjs';

/** Three.js 的朝外几何转为 Unity 正面顺序。 */
export function wallPart(...args) {
  const body=part(...args);
  for(let i=0;i<body.Triangles.length;i+=3)
    [body.Triangles[i],body.Triangles[i+2]]=[body.Triangles[i+2],body.Triangles[i]];
  return body;
}

/** 封闭多层截面，不用平面贴图代替冰晶或树根的厚度。 */
export function column(rings,sides) {
  const positions=[],uv=[],triangles=[];
  for(let row=0;row<rings.length-1;row++)for(let side=0;side<sides;side++) {
    const start=positions.length/3;
    for(const [r,s] of [[row,side],[row,side+1],[row+1,side+1],[row+1,side]]) {
      positions.push(...rings[r][s%sides]);uv.push(s/sides,r/(rings.length-1));
    }
    triangles.push(start,start+1,start+2,start,start+2,start+3);
  }
  for(const row of [0,rings.length-1]) {
    const edge=rings[row],centre=edge.reduce((sum,p)=>sum.add(new T.Vector3(...p)),new T.Vector3()).multiplyScalar(1/sides);
    const at=positions.length/3;positions.push(...centre.toArray());uv.push(.5,.5);
    for(let side=0;side<sides;side++){positions.push(...edge[side]);uv.push(side/sides,row/(rings.length-1));}
    for(let side=0;side<sides;side++) {
      const a=at+1+side,b=at+1+(side+1)%sides;
      triangles.push(at,...(row===0?[b,a]:[a,b]));
    }
  }
  const geometry=new T.BufferGeometry();geometry.setAttribute('position',new T.Float32BufferAttribute(positions,3));
  geometry.setAttribute('uv',new T.Float32BufferAttribute(uv,2));geometry.setIndex(triangles);return geometry;
}

/** 冰晶有宽厚基座、连续受力芯和错落尖冠，晶面与木根使用完全不同的截面。 */
export function makeIceWall() {
  const count=11,spacing=.44,controls=[],parts=[],contacts=[];
  for(let i=0;i<count;i++) {
    const x=(i-(count-1)/2)*spacing,height=1.92+.26*Math.sin(i*2.1+.3),id=`crystal_${i}`;
    controls.push({Id:id,Position:[x,0,0],Rotation:[0,0,0,1]});
    const rows=[[-.06,.36],[.18,.34],[height*.67,.30],[height*.87,.23],[height,.012]];
    const rings=rows.map(([z,r],row)=>Array.from({length:8},(_,side)=>{
      const angle=side*Math.PI/4+.13*Math.sin(i),lean=Math.max(0,z)*.045*Math.sin(i*1.7);
      return [x+Math.cos(angle)*r+lean,Math.sin(angle)*r*.95,z+(row===3?.045*Math.cos(side*2.3+i):0)];
    }));
    const body=wallPart(id,column(rings,8),'ice',[x,0,0],id);parts.push(body);
    // 接触留在晶体受力芯中；尖冠上方保留实际可通过空间。
    contacts.push({Id:id,Part:i,A:[x+.01,0,.25],B:[x+.045*height*Math.sin(i*1.7),0,height*.66],Radius:.265,AControl:id,BControl:id});
  }
  const anchors=[{Id:'left',Part:0,Position:[-2.2,0,0],Direction:[0,0,1],Control:controls[0].Id},
    {Id:'right',Part:count-1,Position:[2.2,0,0],Direction:[0,0,1],Control:controls[count-1].Id},
    {Id:'crest',Part:5,Position:[0,0,2.1],Direction:[0,0,1],Control:controls[5].Id}];
  write('ice_wall',parts,anchors,contacts,controls,[],true,{Forward:[0,1,0],Up:[0,0,1],HideBelowGround:true},
    structure('surface','forms_solid_walls.mjs','makeIceWall',{count,spacing,sides:8},
      Object.fromEntries(parts.map(p=>[p.Id,['terrain.wall','barrier.face','crystal.body']])),[]));
}

/** 木壁由粗根干构成，前后盘根连结；每根的真实曲芯决定作用位置。 */
export function makeWoodWall() {
  const count=11,spacing=.44,controls=[],parts=[],contacts=[],heights=[];
  const point=(i,z)=>new T.Vector3((i-5)*spacing+.055*Math.sin(z*1.8+i)*z,
    .07*Math.sin(z*2+i*.7),z);
  for(let i=0;i<count;i++) {
    const x=(i-5)*spacing,height=1.94+.18*Math.sin(i*1.8+.5),id=`root_${i}`;heights.push(height);
    controls.push({Id:id,Position:[x,0,0],Rotation:[0,0,0,1]});
    const rows=[[-.07,.36],[.15,.33],[height*.35,.305],[height*.7,.29],[height,.255]];
    const rings=rows.map(([z,r],row)=>Array.from({length:12},(_,side)=>{
      const angle=side*Math.PI/6,p=point(i,z),ridge=1+.055*Math.sin(side*2.5+i);
      return [p.x+Math.cos(angle)*r*ridge,p.y+Math.sin(angle)*r*.94,z+(row===4?.07*Math.sin(side*1.7+i):0)];
    }));
    const body=wallPart(id,column(rings,12),'wood',[x,0,0],id);parts.push(body);
    for(let band=0;band<3;band++) {
      const a=.23+band*(height-.53)/3,b=.23+(band+1)*(height-.53)/3;
      contacts.push({Id:`${id}_${band}`,Part:i,A:point(i,a).toArray(),B:point(i,b).toArray(),Radius:.245,AControl:id,BControl:id});
    }
  }
  const weights=p=>{
    const at=T.MathUtils.clamp(p.x/spacing+5,0,10),index=Math.min(9,Math.floor(at)),u=at-index;
    return [[index,1-u],[index+1,u]];
  };
  for(let side=0;side<2;side++) {
    const points=Array.from({length:25},(_,i)=>{
      const x=-2.35+4.7*i/24;return new T.Vector3(x,(side===0?-1:1)*(.24+.03*Math.sin(x*3)),
        .36+.92*(side===0?(x+2.35)/4.7:(2.35-x)/4.7)+.10*Math.sin(x*3));
    });
    const geometry=new T.TubeGeometry(new T.CatmullRomCurve3(points),48,.145,10,false);skin(geometry,weights);
    parts.push(wallPart(`binding_${side}`,geometry,'wood'));
  }
  const anchors=[{Id:'left',Part:0,Position:[-2.2,0,0],Direction:[0,0,1],Control:'root_0'},
    {Id:'right',Part:10,Position:[2.2,0,0],Direction:[0,0,1],Control:'root_10'},
    {Id:'crest',Part:5,Position:point(5,heights[5]).toArray(),Direction:[0,0,1],Control:'root_5'}];
  write('wood_wall',parts,anchors,contacts,controls,[],true,{Forward:[0,1,0],Up:[0,0,1],HideBelowGround:true},
    structure('surface','forms_solid_walls.mjs','makeWoodWall',{count,spacing,sides:12,bindings:2},
      Object.fromEntries(parts.map(p=>[p.Id,['terrain.wall','barrier.face','root.body']])),[]));
}
