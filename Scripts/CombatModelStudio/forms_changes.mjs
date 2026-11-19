/** 按真实分区切面制作可聚合、开裂及碎解的薄板，材质由构造决定。 */
import * as T from 'three';
import {part,write,structure,motion,surfaceData} from './form_export.mjs';

/** 对凸多边形按种子中垂线裁切，所有片共享相同的真实分区边。 */
export function fractureCell(outline,seed,seeds) {
  let polygon=outline.map(p=>new T.Vector2(...p));
  for(const other of seeds) {
    if(other===seed)continue;
    const normal=new T.Vector2(other[0]-seed[0],other[1]-seed[1]);
    const middle=new T.Vector2((other[0]+seed[0])*.5,(other[1]+seed[1])*.5);
    const output=[];
    for(let i=0;i<polygon.length;i++) {
      const a=polygon[i],b=polygon[(i+1)%polygon.length],da=a.clone().sub(middle).dot(normal),db=b.clone().sub(middle).dot(normal);
      if(da<=0)output.push(a);
      if((da<=0)!==(db<=0))output.push(a.clone().lerp(b,da/(da-db)));
    }
    polygon=output;
  }
  return polygon;
}

/** 每片是封闭体积，几何缝隙、动作控制和作用段采用同一片坐标。 */
export function makeFractureSlab() {
  const outline=[[-2.4,-1.4],[-1.6,-2.1],[1.7,-1.9],[2.6,-.5],[2.2,1.4],[.8,2.1],[-1.7,1.8],[-2.6,.4]];
  const seeds=[[-1.55,-.8],[-.2,-1.1],[1.4,-.9],[-1.6,.5],[0,.1],[1.8,.45],[-.65,1.2],[.9,1.3]];
  const thickness=.42,polygons=seeds.map(seed=>fractureCell(outline,seed,seeds));
  const controls=[],parts=[],anchors=[],contacts=[],centres=[];
  polygons.forEach((polygon,index)=>{
    const centre=polygon.reduce((sum,p)=>sum.add(p),new T.Vector2()).multiplyScalar(1/polygon.length);centres.push(centre);
    const id=`plate_${index}`;controls.push({Id:id,Parent:null,Position:[centre.x,centre.y,thickness*.5],Rotation:[0,0,0,1]});
    const shape=new T.Shape(polygon);const geometry=new T.ExtrudeGeometry(shape,{depth:thickness,bevelEnabled:false,steps:1,curveSegments:1});
    surfaceData(geometry,thickness);geometry.userData.smoothSeams=false;
    parts.push(part(id,geometry,'ice',[centre.x,centre.y,thickness*.5],id));
    anchors.push({Id:`surface_${index}`,Part:index,Position:[centre.x,centre.y,thickness],Direction:[0,0,1],Control:id});
    const ys=polygon.map(p=>p.y),min=Math.min(...ys),max=Math.max(...ys);
    for(let y=min+.12,row=0;y<max-.1;y+=.2,row++) {
      const intersections=[];
      polygon.forEach((a,j)=>{const b=polygon[(j+1)%polygon.length];if((a.y<=y&&b.y>y)||(b.y<=y&&a.y>y))intersections.push(T.MathUtils.lerp(a.x,b.x,(y-a.y)/(b.y-a.y)));});
      if(intersections.length!==2)continue;
      const left=Math.min(...intersections)+.1,right=Math.max(...intersections)-.1;
      if(right<=left)continue;
      contacts.push({Id:`${id}_${row}`,Part:index,A:[left,y,thickness*.5],B:[right,y,thickness*.5],Radius:.12,AControl:id,BControl:id});
    }
  });
  const connections=[];
  polygons.forEach((left,i)=>polygons.slice(i+1).forEach((right,offset)=>{
    const shared=left.filter(a=>right.some(b=>a.distanceTo(b)<.00001));
    if(shared.length<2)return;
    const p=shared[0].clone().add(shared[1]).multiplyScalar(.5);
    connections.push([parts[i].Id,parts[i+1+offset].Id,[p.x,p.y,thickness*.5]]);
  }));
  const pose=(index,amount,turn)=>{
    const c=centres[index],out=c.clone().normalize(),bind=controls[index].Position;
    return {position:[bind[0]+out.x*amount,bind[1]+out.y*amount,bind[2]+amount*.08*(index%3)],
      rotation:new T.Quaternion().setFromAxisAngle(new T.Vector3(0,0,1),turn*(index%2?1:-1)).toArray()};
  };
  const assemble=motion('assemble',2,false,controls,48,(i,p)=>{const t=1-T.MathUtils.smoothstep(p,0,1);return pose(i,t*2.2,t*.22);},[{Id:'scattered',At:0},{Id:'joined',At:2}]);
  const crack=motion('crack',1.8,false,controls,48,(i,p)=>{const t=T.MathUtils.smoothstep(p,.12,.95);return pose(i,t*.6,t*.055);},[{Id:'intact',At:0},{Id:'opening',At:.6},{Id:'open',At:1.8}]);
  write('fracture_slab',parts,anchors,contacts,controls,[assemble,crack],false,{Forward:[0,1,0],Up:[0,0,1]},
    structure('articulated','forms_changes.mjs','makeFractureSlab',{outline,seeds,thickness,contactStep:.2},
      Object.fromEntries(parts.map(p=>[p.Id,['surface.plate','fracture.piece']])),connections,['crack']));
}
