/** 独立岩刺：封闭的不规则岩层、错落石锋和同一控制下的真实涌出。 */
import * as T from 'three';
import {mergeGeometries} from 'three/addons/utils/BufferGeometryUtils.js';
import {part, write, structure, motion, skin, round} from './form_export.mjs';
import {column, wallPart} from './forms_solid_walls.mjs';

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
  makeGroundSpikeVariants();
}

/** 四个原索引用同一片露出地面的起伏岩床承接原范围，石锋只改变形态。 */
function makeGroundSpikeVariants() {
  const origin = [0, 0, 0], identity = [0, 0, 0, 1], radius = 1.4;
  const types = [
    [[0, .08, .46, 2.34, .03, -.07], [.61, .20, .25, 1.31, .12, .17], [-.62, .18, .29, 1.46, -.14, -.11],
      [-.28, -.58, .25, 1.05, -.09, -.15], [.38, -.47, .23, .99, .16, -.03]],
    [[-.10, 0, .32, 1.93, .35, .10], [.38, -.12, .20, 1.12, .11, -.21]],
    [[-.70, -.08, .26, .64, .37, -.12], [-.24, -.03, .30, .83, .35, -.16],
      [.26, .06, .34, 1.06, .27, -.19], [.68, .14, .293, .84, .19, -.21]],
    [[-.21, .11, .36, 2.21, .52, .17], [.30, -.13, .23, 1.43, .46, .27], [-.48, -.28, .22, .96, -.13, -.25]]
  ];
  const palette = ['stone_ridge', 'stone', 'stone_strata', 'stone_ridge'];
  for (const [variant, specs] of types.entries()) {
    const geometry = column([[-.28,1.23],[-.07,1.38],[.10,radius],[.16,1.22]].map(([z,radius],row)=>
      Array.from({length:56},(_,i)=>{
        const angle=i*Math.PI/28, edge=row===2?1:1+.039*Math.sin(i*.93+variant);
        return [radius*edge*Math.cos(angle),radius*edge*Math.sin(angle),z+.025*Math.sin(i*1.07+row)];
      })),56).rotateX(-Math.PI/2);
    const controls=[{Id:'bed',Position:origin,Rotation:identity}];
    const parts=[wallPart('bed',geometry,palette[variant],origin,'bed')];
    for(const [index,[x,z,width,height,leanX,leanZ]] of specs.entries()) {
      const id=`spire_${index}`, pivot=[x,0,z];
      controls.push({Id:id,Position:pivot,Rotation:identity});
      const rings=[[-.12,1],[.24,1],[height*.45,.83],[height*.71,.56],[height*.91,.27],[height,.014]]
        .map(([h,r],row)=>Array.from({length:8},(_,i)=>{
          const angle=i*Math.PI/4+.48*index, rough=1+.10*Math.sin(i*2.25+variant*.7);
          const offset=h/height;
          return [x+leanX*offset+Math.cos(angle)*r*width*rough,-z-leanZ*offset+Math.sin(angle)*r*width*.89,
            h+(row>=2?.025*Math.sin(i*2.5+variant):0)];
        }));
      const spire=column(rings,8).rotateX(-Math.PI/2);
      parts.push(wallPart(id,spire,index%2===0?palette[variant]:'stone_strata',pivot,id));
    }
    const duration=variant===0?round(4/18+4/20):.24;
    const motionData=motion('erupt',duration,false,controls,32,(index,phase,control)=>{
      const at=phase*duration, delay=variant===0?.007*index:0;
      const rise=variant===0?T.MathUtils.smoothstep(at,delay,(index===0?.156:.186)+delay):1;
      const withdraw=T.MathUtils.smoothstep(at,variant===0?duration*.59:.058,duration);
      const depth=index===0?.31:specs[index-1][3]+.11;
      return {position:[control.Position[0],-depth*(1-rise*(1-withdraw)),control.Position[2]]};
    });
    const formId=`earth_spikes_${variant}`;
    const anchors=[{Id:'base',Part:0,Position:origin,Direction:[0,1,0],Control:'bed'}];
    const contacts=[{Id:'bed',Part:0,A:[0,.04,0],AControl:'bed',Sector:{Radius:radius,Angle:360,Direction:0,HalfThickness:.15}}];
    write(formId,parts,anchors,contacts,controls,[motionData],false,{Forward:[0,0,1],Up:[0,1,0],HideBelowGround:true},
      structure('articulated','forms_earth.mjs','makeEarthSpikes',{specs,radius,duration},
        Object.fromEntries(parts.map(p=>[p.Id,[p.Id==='bed'?'terrain.floor':'terrain.spire']]))));
  }
}


/** 连续封闭墙面保留错落顶缘，横向控制共同承托显示、挂点和实际接触。 */
function makeLegacyEarthWall() {
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

/** 岩墙在原阻挡体内刻出岩层，骨架仍共同承托墙面、挂点和实际接触。 */
function makeEarthWallStrata() {
  const count = 13, length = 5, radius = .245;
  const crest = x => 2.05 + .12 * Math.sin(x * 3.1 + .4) + .08 * Math.cos(x * 5.3);
  const controls = Array.from({length: count}, (_, i) => ({Id: `bed_${i}`,
    Position: [-length / 2 + length * i / (count - 1), 0, 0], Rotation: [0, 0, 0, 1]}));
  const weights = p => {
    const at = T.MathUtils.clamp((p.x / length + .5) * (count - 1), 0, count - 1);
    const i = Math.min(count - 2, Math.floor(at)), u = at - i;
    return [[i, 1 - u], [i + 1, u]];
  };
  const bind = p => weights(p).filter(([, weight]) => weight > 0).map(([i, Weight]) => ({Control: controls[i].Id, Weight}));
  const columns = Array.from({length: 11}, (_, i) => {
    const x = -2.25 + i * .45;
    return {x, bottom: .245, top: crest(x) - .27};
  });
  const contacts = columns.map(({x, bottom, top}, i) => {
    const a = new T.Vector3(x, 0, bottom), b = new T.Vector3(x, 0, top);
    return {Id: `face_${i}`, Part: 0, A: a.toArray(), B: b.toArray(), Radius: radius, AInfluences: bind(a), BInfluences: bind(b)};
  });
  const span = (column, x, z) => Math.sqrt(Math.max(0, radius ** 2 - (x - column.x) ** 2 -
    (z - T.MathUtils.clamp(z, column.bottom, column.top)) ** 2));
  const ring = (x, shell) => {
    const margin = Math.min(...shell.map(c => span(c, x, c.bottom)));
    const end = T.MathUtils.smoothstep(Math.abs(x), 2.24, 2.49);
    const base = radius - margin + .012;
    const top = Math.min(crest(x) - end * .26, ...shell.map(c => c.top + margin - .022));
    const profile = (side, z, width) => [x, side * width * Math.min(...shell.map(c => span(c, x, z))), z];
    const sideProfile = side => {
      const seam = .41 + .043 * Math.sin(x * 3.4 + side) + .019 * Math.cos(x * 8.5 - side);
      const shoulder = .72 + .055 * Math.cos(x * 4.4 - side);
      const broken = .5 + .5 * Math.sin(x * 5.7 + 1.8 * side + .6 * Math.cos(x * 2.9));
      return [[0, .33], [.07, .82], [.20, .99 - .10 * broken], [seam - .08, .91],
        [seam, .58 + .18 * broken], [seam + .045, .94], [shoulder, .98 - .14 * broken],
        [.88, .79], [1, .40 - .11 * broken]].map(([level, width]) => profile(side, T.MathUtils.lerp(base, top, level), width));
    };
    return [...sideProfile(-1), ...sideProfile(1).reverse()];
  };
  // 每小段归属同一阻挡体，交界截面归属两侧。面的三角形留在原封闭范围内。
  const rings = [];
  for (let i = 0; i < columns.length; i++) {
    const column = columns[i], next = columns[i + 1];
    if (i === 0) for (const offset of [-.237, -.17, -.1]) rings.push(ring(column.x + offset, [column]));
    rings.push(ring(column.x, [column]));
    if (next) {
      rings.push(ring(column.x + .11, [column]));
      rings.push(ring((column.x + next.x) * .5, [column, next]));
      rings.push(ring(next.x - .10, [next]));
    } else for (const offset of [.1, .17, .237]) rings.push(ring(column.x + offset, [column]));
  }
  const geometry = earthWallShell(rings, length);
  skin(geometry, weights);
  const body = wallPart('wall', geometry, 'stone_strata');
  const anchors = [['left', new T.Vector3(-2.5, 0, 0)], ['right', new T.Vector3(2.5, 0, 0)], ['crest', new T.Vector3(0, 0, crest(0))]]
    .map(([Id, p]) => ({Id, Part: 0, Position: p.toArray(), Direction: [0, 0, 1], Influences: bind(p)}));
  write('earth_wall_strata', [body], anchors, contacts, controls, [], true, {Forward: [0, 1, 0], Up: [0, 0, 1], HideBelowGround: true},
    structure('surface', 'forms_earth.mjs', 'makeEarthWall', {length, radius, controls: count}, {wall: ['terrain.wall', 'barrier.face']}, []));
  publishEarthWallMotion(rings, length, radius, controls, anchors, contacts);
}

/** 墙和接地后分开的石块使用同一断面；每段都封口。 */
function earthWallShell(rings, length) {
  const positions = [], uv = [], triangles = [];
  const sides = rings[0].length;
  const append = p => { positions.push(...p); uv.push(p[0] / length + .5 + .045 * p[1], p[2] / 2.3); };
  for (let i = 0; i < rings.length - 1; i++) for (let side = 0; side < sides; side++) {
    const a = rings[i], b = rings[i + 1], next = (side + 1) % sides, start = positions.length / 3;
    for (const p of [a[side], b[side], b[next], a[next]]) append(p);
    triangles.push(start, start + 1, start + 2, start, start + 2, start + 3);
  }
  for (const end of [0, rings.length - 1]) {
    const edge = rings[end], start = positions.length / 3;
    append([edge[0][0], 0, (edge[0][2] + edge[sides / 2][2]) * .5]);
    edge.forEach(append);
    for (let side = 0; side < sides; side++) {
      const a = start + 1 + side, b = start + 1 + (side + 1) % sides;
      triangles.push(start, ...(end === 0 ? [a, b] : [b, a]));
    }
  }
  const geometry = new T.BufferGeometry();
  geometry.setAttribute('position', new T.Float32BufferAttribute(positions, 3));
  geometry.setAttribute('uv', new T.Float32BufferAttribute(uv, 2));
  geometry.setIndex(triangles);
  return geometry;
}

/** 地脚始终承力。上层沿现成断面分成七块，每块整块落位。 */
function publishEarthWallMotion(rings, length, radius, base, anchors, contacts) {
  const count = base.length, identity = [0, 0, 0, 1], sections = 7;
  const facing = {Forward: [0, 1, 0], Up: [0, 0, 1], HideBelowGround: true};
  const spans = Array.from({length: sections}, (_, i) => {
    const from = Math.round(i * (rings.length - 1) / sections);
    const to = Math.round((i + 1) * (rings.length - 1) / sections);
    return rings.slice(from, to + 1);
  });
  const nearest = x => Math.round(T.MathUtils.clamp((x / length + .5) * (count - 1), 0, count - 1));
  const centre = span => (span[0][0][0] + span.at(-1)[0][0]) * .5;
  const controls = [...base, ...spans.map(span => {
    const control = base[nearest(centre(span))];
    return {...control, Id: control.Id.replace('bed_', 'crest_')};
  })];
  const rigid = x => [{Control: controls[count + spans.findIndex(span => x <= span.at(-1)[0][0])].Id, Weight: 1}];
  const footage = skin(earthWallShell(rings.map(ring => [...ring.slice(0, 4), ...ring.slice(-4)]), length), p => {
    const at = T.MathUtils.clamp((p.x / length + .5) * (count - 1), 0, count - 1);
    const left = Math.min(count - 2, Math.floor(at)), offset = at - left;
    return [[left, 1 - offset], [left + 1, offset]];
  });
  const crowns = spans.map((span, i) => {
    const bone = count + i;
    // 接缝盖住岩层下缘。收紧只有横截面交界的一小片，露出削断的棱。
    const cut = span.map((ring, j) => ring.slice(3, -3).map(([x, y, z]) => {
      const end = j === 0 || j === span.length - 1, terminal = Math.abs(x) > 2.47;
      return [x, y * (end && !terminal ? .90 : 1), z];
    }));
    return skin(earthWallShell(cut, length), () => [[bone, 1]]);
  });
  const stone = mergeGeometries([footage, ...crowns]);
  const axis = new T.Vector3(1, 0, 0);
  const settle = motion('settle', .27, false, controls, 24, (i, phase, control) => {
    if (i < count) return {};
    const distance = Math.abs(control.Position[0]) / (length / 2);
    const arrival = .021 + distance * .076;
    const lift = T.MathUtils.smoothstep(phase * .27, arrival, arrival + .152);
    const rest = 1 - lift;
    return {position: [control.Position[0], 0, -(.15 + .035 * Math.cos(i * .83)) * rest],
      rotation: new T.Quaternion().setFromAxisAngle(axis, .058 * Math.sin(i * 1.67) * rest).toArray()};
  });
  write('earth_wall_strata_motion', [wallPart('wall', stone, 'stone_strata')],
    anchors.map(anchor => anchor.Id === 'crest' ? {...anchor, Influences: rigid(anchor.Position[0])} : anchor),
    contacts.map(contact => ({...contact, BInfluences: rigid(contact.B[0])})),
    controls, [settle], true, facing,
    structure('surface', 'forms_earth.mjs', 'makeEarthWall', {length, radius, controls: controls.length, sections},
      {wall: ['terrain.wall', 'barrier.face']}));

  const blocks = spans.map((span, i) => ({Id: `block_${i}`, Rotation: identity,
    Position: [(span[0][0][0] + span.at(-1)[0][0]) / 2, 0, .28]}));
  const parts = spans.map((span, i) => {
    const block = blocks[i];
    return wallPart(block.Id, earthWallShell(span, length), 'stone_strata', block.Position, block.Id);
  });
  const collapse = motion('collapse', .42, false, blocks, 28, (i, phase, control) => {
    const wait = (2 - i % 3) * .033, at = phase * .42;
    const t = T.MathUtils.clamp((at - wait) / (.42 - wait), 0, 1);
    const sign = i % 2 ? -1 : 1, lean = T.MathUtils.smoothstep(t, 0, .76);
    const down = T.MathUtils.smoothstep(t, .34, 1);
    const position = [control.Position[0] + (i - 3) * .041 * lean,
      sign * (.12 + i * .028) * lean, .28 - (1.46 + (i % 3) * .11) * down];
    const rotation = new T.Quaternion().setFromEuler(new T.Euler(sign * (1.23 + i * .054) * lean,
      (i - 3) * .115 * lean, sign * .19 * lean));
    return {position, rotation: rotation.toArray()};
  });
  write('earth_wall_strata_rubble', parts, [], [], blocks, [collapse], true, facing,
    structure('articulated', 'forms_earth.mjs', 'makeEarthWall', {length, sections},
      Object.fromEntries(parts.map(piece => [piece.Id, ['terrain.wall', 'debris']]))));
}

/** 旧自创资料仍引用原网格及其贴地基准。 */
export function makeEarthWall() {
  makeLegacyEarthWall();
  makeEarthWallStrata();
}
