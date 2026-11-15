/** 独立岩刺：封闭的不规则岩层、错落石锋和同一控制下的真实涌出。 */
import * as T from 'three';
import {part, write, structure, motion, skin} from './form_export.mjs';

/** 每条石锋保留偏斜岩层及截面切面，接触段收在实际石体内部。 */
function shard(index, centre, radius, height) {
  const sides = 9, positions = [], uv = [], triangles = [];
  const levels = [[-.16, 1.05], [height * .12, 1], [height * .52, .68], [height * .82, .31], [height, .008]];
  const ring = (row, side) => {
    const [z, size] = levels[row], angle = side / sides * Math.PI * 2 + index * .71;
    const uneven = 1 + .10 * Math.sin(side * 2.7 + index * 1.3);
    return [centre[0] + Math.cos(angle) * radius * size * uneven + z * .045 * Math.sin(index),
      centre[1] + Math.sin(angle) * radius * size * uneven + z * .035 * Math.cos(index), z];
  };
  for (let row = 0; row < levels.length - 1; row++) for (let side = 0; side < sides; side++) {
    const start = positions.length / 3;
    for (const [r, s] of [[row, side], [row, side + 1], [row + 1, side + 1], [row + 1, side]]) {
      positions.push(...ring(r, s % sides)); uv.push(s / sides, levels[r][0] / height);
    }
    triangles.push(start, start + 1, start + 2, start, start + 2, start + 3);
  }
  const bottom = positions.length / 3;
  positions.push(centre[0], centre[1], -.16); uv.push(.5, .5);
  for (let side = 0; side < sides; side++) { positions.push(...ring(0, side)); uv.push(side / sides, 0); }
  for (let side = 0; side < sides; side++) triangles.push(bottom, bottom + 1 + (side + 1) % sides, bottom + 1 + side);
  const geometry = new T.BufferGeometry();
  geometry.setAttribute('position', new T.Float32BufferAttribute(positions, 3));
  geometry.setAttribute('uv', new T.Float32BufferAttribute(uv, 2)); geometry.setIndex(triangles);
  const id = `spire_${index}`, pivot = [...centre, 0];
  const point = z => [centre[0] + z * .045 * Math.sin(index), centre[1] + z * .035 * Math.cos(index), z];
  const contacts = [[0, .15, .90], [.15, .35, .80], [.35, .55, .60], [.55, .72, .40], [.72, .86, .22], [.86, .975, .06]]
    .map(([a, b, r], band) => ({Id: `${id}_${band}`, Part: index,
      A: point(a * height), B: point(b * height), Radius: radius * r, AControl: id, BControl: id}));
  return {item: part(id, geometry, 'stone', pivot, id), contacts,
    control: {Id: id, Parent: null, Position: pivot, Rotation: [0, 0, 0, 1]}};
}

/** 中央高锋与六片斜生石锋共享涌出时钟；整石平移，石体不会像软胶一样拉长。 */
export function makeEarthSpikes() {
  const specs = [[[0, 0], .48, 2.4]];
  for (let i = 0; i < 6; i++) {
    const angle = i / 6 * Math.PI * 2 + .19;
    specs.push([[Math.cos(angle) * .88, Math.sin(angle) * .88], .33 + (i % 2) * .055, 1.35 + (i % 3) * .23]);
  }
  const shards = specs.map(([centre, radius, height], i) => shard(i, centre, radius, height));
  const controls = shards.map(s => s.control), duration = .9;
  const erupt = motion('erupt', duration, false, controls, 48, (index, phase, control) => {
    const delay = index * .009, rise = T.MathUtils.smoothstep(phase, delay, .18 + delay);
    const withdraw = T.MathUtils.smoothstep(phase, .73, 1);
    return {position: [control.Position[0], control.Position[1], -(specs[index][2] + .12) * (1 - rise * (1 - withdraw))]};
  }, [{Id: 'buried', At: 0}, {Id: 'strike', At: .03}, {Id: 'raised', At: .22}, {Id: 'withdraw', At: .657}, {Id: 'buried_end', At: duration}]);
  write('earth_spikes', shards.map(s => s.item), [
    {Id: 'base', Part: 0, Position: [0, 0, 0], Direction: [0, 0, 1], Control: controls[0].Id},
    {Id: 'tip', Part: 0, Position: [0, .084, 2.4], Direction: [0, 0, 1], Control: controls[0].Id}
  ], shards.flatMap(s => s.contacts), controls, [erupt], false, {Forward: [0, 1, 0], Up: [0, 0, 1], HideBelowGround: true},
  structure('articulated', 'forms_earth.mjs', 'makeEarthSpikes', {specs, duration, sides: 9},
    Object.fromEntries(shards.map(s => [s.item.Id, ['terrain.spire', 'weapon.point']])), []));
}


/** 连续封闭墙面保留错落顶缘，横向控制共同承托显示、挂点和实际接触。 */
export function makeEarthWall() {
  const count=13,length=5,halfThickness=.3,positions=[],uv=[],triangles=[];
  const crest=x=>2.05+.12*Math.sin(x*3.1+.4)+.08*Math.cos(x*5.3);
  const controls=Array.from({length:count},(_,i)=>({Id:`bed_${i}`,Position:[-length/2+length*i/(count-1),0,0],Rotation:[0,0,0,1]}));
  const weights=p=>{
    const at=T.MathUtils.clamp((p.x/length+.5)*(count-1),0,count-1),i=Math.min(count-2,Math.floor(at)),u=at-i;
    return [[i,1-u],[i+1,u]];
  };
  const bind=p=>weights(p).filter(([,weight])=>weight>0).map(([i,Weight])=>({Control:controls[i].Id,Weight}));
  const ring=i=>{
    const x=controls[i].Position[0],h=crest(x),bend=.025*Math.sin(x*4.3);
    return [[x,-.18,-.03],[x,-halfThickness,.12],[x,-halfThickness+bend,h-.14],[x,-.14,h],
      [x,.13,h+.025],[x,halfThickness+bend,h-.10],[x,.27,.08],[x,.18,-.03]];
  };
  for(let i=0;i<count-1;i++)for(let side=0;side<8;side++){
    const a=ring(i),b=ring(i+1),next=(side+1)%8,start=positions.length/3;
    for(const [p,u,v] of [[a[side],0,side/8],[b[side],1,side/8],[b[next],1,(side+1)/8],[a[next],0,(side+1)/8]]){
      positions.push(...p);uv.push((i+u)/(count-1),v);
    }
    triangles.push(start,start+1,start+2,start,start+2,start+3);
  }
  for(const end of [0,count-1]){
    const edge=ring(end),start=positions.length/3;
    positions.push(controls[end].Position[0],0,crest(controls[end].Position[0])*.5);uv.push(.5,.5);
    for(let side=0;side<8;side++){positions.push(...edge[side]);uv.push((edge[side][1]/halfThickness+1)*.5,edge[side][2]/2.3);}
    for(let side=0;side<8;side++){
      const a=start+1+side,b=start+1+(side+1)%8;
      triangles.push(start,...(end===0?[a,b]:[b,a]));
    }
  }
  const geometry=new T.BufferGeometry();geometry.setAttribute('position',new T.Float32BufferAttribute(positions,3));
  geometry.setAttribute('uv',new T.Float32BufferAttribute(uv,2));geometry.setIndex(triangles);skin(geometry,weights);
  const body=part('wall',geometry,'stone');
  // 本函数直接构造朝外的几何，发布时换为Unity正面顺序。
  for(let i=0;i<body.Triangles.length;i+=3)[body.Triangles[i],body.Triangles[i+2]]=[body.Triangles[i+2],body.Triangles[i]];
  const contacts=Array.from({length:11},(_,i)=>{
    const x=-2.25+i*.45,a=new T.Vector3(x,0,.245),b=new T.Vector3(x,0,crest(x)-.27);
    return {Id:`face_${i}`,Part:0,A:a.toArray(),B:b.toArray(),Radius:.245,AInfluences:bind(a),BInfluences:bind(b)};
  });
  const anchors=[['left',new T.Vector3(-2.5,0,0)],['right',new T.Vector3(2.5,0,0)],['crest',new T.Vector3(0,0,crest(0))]]
    .map(([Id,p])=>({Id,Part:0,Position:p.toArray(),Direction:[0,0,1],Influences:bind(p)}));
  write('earth_wall',[body],anchors,contacts,controls,[],true,{Forward:[0,1,0],Up:[0,0,1],HideBelowGround:true},
    structure('surface','forms_earth.mjs','makeEarthWall',{length,halfThickness,controls:count}, {wall:['terrain.wall','barrier.face']},[]));
}
