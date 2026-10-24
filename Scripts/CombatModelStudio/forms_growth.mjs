/** 根与藤的连续截面、分枝关系及逐段成形资料；运行复用共同动作系统。 */
import * as T from 'three';
import {part, write, structure, skin, motion, formation, forming} from './form_export.mjs';
import {curveInfluences} from './form_curve.mjs';

const count = 9, rows = 72, sides = 14;
const curve = points => new T.CatmullRomCurve3(points.map(p => new T.Vector3(...p)), false, 'catmullrom', .35);

/** 保留渐细封口和稳定纵向参数，避免末端留下等宽管口。 */
function tube(path, radius) {
  const geometry = new T.TubeGeometry(path, rows, radius, sides, false), positions = geometry.getAttribute('position');
  const uv = geometry.getAttribute('uv');
  for (let i = 0; i < positions.count; i++) {
    const at = uv.getX(i), centre = path.getPointAt(at);
    const p = new T.Vector3().fromBufferAttribute(positions, i).sub(centre);
    const taper = Math.pow(Math.max(0, 1-at), .48) * (1+.055*Math.sin(at*34));
    p.multiplyScalar(taper).add(centre); positions.setXYZ(i,p.x,p.y,p.z);
  }
  // 起点加封口；终点半径归零，所有末端顶点重合于真实尖端。
  const data = Array.from(positions.array), coords = Array.from(uv.array), indices = Array.from(geometry.index.array);
  const centreIndex = positions.count;
  data.push(...path.getPointAt(0).toArray()); coords.push(0,.5);
  for (let side = 0; side < sides; side++) indices.push(centreIndex,side+1,side);
  geometry.setAttribute('position',new T.Float32BufferAttribute(data,3));
  // 新封口增加了顶点，旧 TubeGeometry 法线长度不足；交给共同导出重算完整通道。
  geometry.deleteAttribute('normal');
  geometry.setAttribute('uv',new T.Float32BufferAttribute(coords,2)); geometry.setIndex(indices);
  geometry.userData.smoothSeams = true;
  return geometry;
}

/** 一份实际分枝结构同时导出网格、动作、作用区及末端挂点。 */
function make(id, paths, intervals, radii, target) {
  const bind = paths.map(curve), controls = [], parts = [], anchors = [], contacts = [];
  const at = (path,t) => path.getPointAt(t), tangent = (path,t) => path.getTangentAt(t);
  bind.forEach((path, branch) => {
    const offset = controls.length, name = branch ? `lateral_${branch}` : 'trunk';
    for (let i=0;i<count;i++) controls.push({Id:`${name}_${i}`,Parent:null,Position:at(path,i/(count-1)).toArray(),Rotation:[0,0,0,1]});
    const geometry = tube(path,radii[branch]), uv = geometry.getAttribute('uv');
    // 接缝两端和封口沿同一纵向参数取权重。
    const controlIndices=[],weights=[];
    for (let i=0;i<uv.count;i++) {
      const blend=curveInfluences(uv.getX(i),count);
      controlIndices.push(offset+blend[0][0],offset+blend[1][0],0,0);weights.push(blend[0][1],blend[1][1],0,0);
    }
    geometry.setAttribute('skinIndex',new T.Uint16BufferAttribute(controlIndices,4));
    geometry.setAttribute('skinWeight',new T.Float32BufferAttribute(weights,4));
    const [start,end] = intervals[branch];
    formation(geometry,(_,i)=>T.MathUtils.lerp(start,end,uv.getX(i)),(_,global) => at(path,(global-start)/(end-start)));
    parts.push(part(name,geometry,'wood',paths[branch][0]));
    const influence = t => curveInfluences(t,count).map(([i,Weight])=>({Control:controls[offset+i].Id,Weight}));
    anchors.push({Id:`tip_${branch}`,Part:branch,Position:at(path,1).toArray(),Direction:tangent(path,1).toArray(),Influences:influence(1),BuildAt:end*.96+.04});
    for (let i=0;i<count-1;i++) {
      const a=i/(count-1),b=(i+1)/(count-1);
      contacts.push({Id:`${name}_${i}`,Part:branch,A:at(path,a).toArray(),B:at(path,b).toArray(),Radius:radii[branch]*Math.pow(1-a,.48),
        AInfluences:influence(a),BInfluences:influence(b),BuildRange:[T.MathUtils.lerp(start,end,a)*.96,T.MathUtils.lerp(start,end,b)*.96+.04],BuildAlong:true});
    }
  });
  const evaluate = (i,p) => {
    const branch=Math.floor(i/count),t=i%count/(count-1),path=target(bind,p)[branch];
    return {position:at(path,t).toArray(),rotation:new T.Quaternion().setFromUnitVectors(tangent(bind[branch],t),tangent(path,t)).toArray()};
  };
  const grow=forming(motion('form',2.4,false,controls,64,evaluate,[{Id:'start',At:0},{Id:'branch',At:.96},{Id:'ready',At:2.4}]));
  const bend=motion('bend',2.4,false,controls,64,evaluate,[{Id:'start',At:0},{Id:'turn',At:1.2},{Id:'ready',At:2.4}]);
  write(id,parts,anchors,contacts,controls,[grow,bend],false,{Forward:[0,1,0],Up:[0,0,1]},
    structure('branch','forms_growth.mjs','makeGrowingForms',{paths,intervals,radii,controlsPerBranch:count,rows,sides},
      Object.fromEntries(parts.map((p,i)=>[p.Id,[i?'branch.lateral':'branch.trunk']])),
      parts.slice(1).map(p=>['trunk',p.Id,p.Pivot,false])));
}

/** 主根先到达分叉位置，侧根随后成形；藤使用相同路径并盘卷。 */
export function makeGrowingForms() {
  const trunk=[[0,0,.3],[.25,1.8,.24],[-.2,3.8,.32],[.1,6,.2]], base=curve(trunk);
  const branch=(t,offsets)=>offsets.map(p=>new T.Vector3(...p).add(base.getPointAt(t)).toArray());
  const paths=[trunk,branch(.4,[[0,0,0],[-1,.5,.1],[-2.3,1.4,.05],[-3.1,2.5,-.02]]),branch(.7,[[0,0,0],[1,.5,.2],[2.1,1.6,.15],[2.8,3,.04]])];
  make('root_network',paths,[[0,.6],[.32,.87],[.49,1]],[.34,.21,.18],(bind,p)=>{
    const sway=Math.sin(p*Math.PI)*.65;
    const moved=bind.map((path,index)=>curve(path.points.map((v,i)=>[v.x+sway*i*.16*(index===1?-1:1),v.y,v.z+.12*Math.sin(p*Math.PI)*i])));
    // 分枝基部随主根的同一位置，不依赖各自摆动的近似值。
    for(let i=1;i<moved.length;i++) {
      const delta=moved[0].getPointAt(i===1?.4:.7).sub(moved[i].points[0]);
      moved[i]=curve(moved[i].points.map(p=>p.clone().add(delta).toArray()));
    }
    return moved;
  });
  const vine=[[0,0,.15],[.2,1.2,.45],[.65,2.5,1.25],[.4,3.6,2.4],[-.65,4.3,3.3],[-1.7,3.9,3.8]];
  make('spirit_vine',[vine],[[0,1]],[.23],(bind,p)=>[curve(bind[0].points.map((v,i)=>{
    const turn=T.MathUtils.smoothstep(p,.15,1)*i/(bind[0].points.length-1)*1.65;
    return [v.x*Math.cos(turn)-v.y*Math.sin(turn),v.x*Math.sin(turn)+v.y*Math.cos(turn),v.z];
  }))]);
}
