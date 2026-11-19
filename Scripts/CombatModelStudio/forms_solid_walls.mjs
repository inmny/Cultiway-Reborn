/** 独立冰晶墙和盘根木壁：每块实体与自身接触共用地表控制，保留封闭体积。 */
import * as T from 'three';
import {part, write, structure, skin, motion, forming, round} from './form_export.mjs';
import {woodSprig} from './forms_wood_detail.mjs';

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

/** 粗根从地面抬起，交错横枝扎入根干；实际接触只取会承力的根芯。 */
export function makeWoodWall() {
  const count=11,spacing=.44,controls=[],parts=[],contacts=[],heights=[],connections=[];
  const up=new T.Vector3(0,0,1),axis=new T.Vector3(1,0,0);
  const point=(i,z)=>new T.Vector3((i-5)*spacing+.063*Math.sin(z*2.1+i)*z,
    .115*Math.sin(z*2.4+i*.71),z);
  for(let i=0;i<count;i++) {
    const x=(i-5)*spacing,height=1.94+.18*Math.sin(i*1.8+.5),id=`root_${i}`;heights.push(height);
    controls.push({Id:id,Position:[x,0,0],Rotation:[0,0,0,1]});
    const rows=[[-.12,.41],[.10,.36],[height*.34,.317],[height*.64,.306],
      [height-.21,.289],[height-.085,.244],[height,.15]];
    const rings=rows.map(([z,r],row)=>Array.from({length:12},(_,side)=>{
      const angle=side*Math.PI/6+.12*Math.sin(row*.7+i),p=point(i,z);
      const ridge=1+.092*Math.sin(side*2.5+i)+.026*Math.cos(side*5+row);
      return [p.x+Math.cos(angle)*r*ridge,p.y+Math.sin(angle)*r*.97,
        z+(row===6?.062*Math.sin(side*1.7+i):0)];
    }));
    parts.push(wallPart(id,column(rings,12),'wood',[x,0,0],id));
    for(let band=0;band<3;band++) {
      const a=.23+band*(height-.53)/3,b=.23+(band+1)*(height-.53)/3;
      contacts.push({Id:`${id}_${band}`,Part:i,A:point(i,a).toArray(),B:point(i,b).toArray(),Radius:.245,AControl:id,BControl:id});
    }
    if(i>0)connections.push([`root_${i-1}`,id,[x-spacing*.5,0,.26]]);
  }
  const weights=p=>{
    const at=T.MathUtils.clamp(p.x/spacing+5,0,10),index=Math.min(9,Math.floor(at)),u=at-index;
    return [[index,1-u],[index+1,u]];
  };
  for(let side=0;side<2;side++) {
    const points=Array.from({length:27},(_,i)=>{
      const t=i/26,x=T.MathUtils.lerp(-2.30,2.30,t),sign=side===0?1:-1;
      // 两根横枝来回扎进根干，在相邻正背面露出弧面，不是浮在墙前的横线。
      const y=sign*(.20+.18*Math.cos(t*Math.PI*5))*Math.sin(Math.PI*t);
      return new T.Vector3(x,y,.69+sign*(t-.5)*.96+.13*Math.sin(t*Math.PI*4+side*.7));
    });
    const braid=new T.CatmullRomCurve3(points),geometry=new T.TubeGeometry(braid,42,.19,10,false);
    const positions=geometry.getAttribute('position'),uv=geometry.getAttribute('uv');
    for(let i=0;i<positions.count;i++) {
      const t=uv.getX(i),centre=braid.getPointAt(t),radius=.45+.55*Math.pow(Math.sin(t*Math.PI),.35);
      const p=new T.Vector3().fromBufferAttribute(positions,i).lerp(centre,1-radius);
      positions.setXYZ(i,...p.toArray());
    }
    const id=`binding_${side}`;
    parts.push(wallPart(id,skin(geometry,weights),'wood',points[13].toArray()));
    connections.push(['root_5',id,points[13].toArray()]);
  }
  for(const i of [1,4,7,9]) {
    const side=i%2?1:-1,base=point(i,heights[i]-.40),id=`crown_${i}`;
    const path=[base,base.clone().add(new T.Vector3(.10*side,.16*side,.26)),
      base.clone().add(new T.Vector3(.26*side,.25*side,.46))];
    const [shoot,leaf]=woodSprig(id,i,path,0,.7,up);
    parts.push(shoot,leaf);
    connections.push([`root_${i}`,shoot.Id,base.toArray()],[shoot.Id,leaf.Id,path[1].toArray()]);
  }
  const settle=forming(motion('settle',.36,false,controls,25,(i,p,c)=>{
    const start=Math.abs(i-5)*.042,rise=T.MathUtils.smoothstep(p,start,.85+start*.45);
    const seat=(1-rise)*(1-rise),tilt=.10*Math.sin(i*1.48)*seat;
    return {position:[c.Position[0],-.10*seat,-(.51+.13*Math.cos(i*1.9))*seat],
      rotation:new T.Quaternion().setFromAxisAngle(axis,tilt).toArray()};
  }),p=>T.MathUtils.smoothstep(p,.10,.96));
  const wither=motion('wither',.25,false,controls,20,(i,p,c)=>{
    const lower=T.MathUtils.smoothstep(p,.12+Math.abs(i-5)*.045,1);
    return {position:[c.Position[0],-.13*lower,-.32*lower],
      rotation:new T.Quaternion().setFromAxisAngle(axis,.12*lower).toArray()};
  });
  // 同一条时间轴持住落稳姿态，再沿原墙芯收势。
  const wallMotion={...settle,Id:'wall_lifetime',Duration:4,Keys:[...settle.Keys,
    ...wither.Keys.map(key=>({...key,At:round(3.75+key.At),Fade:T.MathUtils.smoothstep(key.At,.08,.25)*.92}))]};
  const anchors=[{Id:'left',Part:0,Position:[-2.2,0,0],Direction:[0,0,1],Control:'root_0'},
    {Id:'right',Part:10,Position:[2.2,0,0],Direction:[0,0,1],Control:'root_10'},
    {Id:'crest',Part:5,Position:point(5,heights[5]).toArray(),Direction:[0,0,1],Control:'root_5'}];
  write('wood_wall',parts,anchors,contacts,controls,[wallMotion],true,{Forward:[0,1,0],Up:[0,0,1],HideBelowGround:true},
    structure('branch','forms_solid_walls.mjs','makeWoodWall',{count,spacing,sides:12,bindings:2},
      Object.fromEntries(parts.map(p=>[p.Id,p.Id.startsWith('root_')?['terrain.wall','barrier.face','root.body']:
        [p.Id.startsWith('binding_')?'branch.binding':p.Id.endsWith('_leaf')?'branch.leaf':'branch.shoot']])),connections));
}
