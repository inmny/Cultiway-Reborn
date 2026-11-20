/** 连续水鲸：脊柱、胸鳍和水平尾鳍共用骨架，表面坐标与真实控制一并导出。 */
import * as T from 'three';
import {part, write, skin, motion, surfaceData, structure} from './form_export.mjs';
import {phaseValue} from './form_skeleton.mjs';

const spineY = [4.5, 2.5, .5, -1.5, -3.3, -5.1, -6.2];
const spineZ = [2.65, 2.6, 2.5, 2.3, 2.15, 2.05, 2.05];
const names = ['head', 'neck', 'chest', 'waist', 'tail_base', 'tail', 'fluke'];

/** 体表沿脊柱平滑混合相邻两个控制。 */
function spineWeights(point) {
  if (point.y >= spineY[0]) return [[0, 1]];
  for (let i = 0; i < spineY.length - 1; i++) if (point.y >= spineY[i + 1]) {
    const blend = (spineY[i] - point.y) / (spineY[i] - spineY[i + 1]);
    return [[i, 1 - blend], [i + 1, blend]];
  }
  return [[6, 1]];
}

/** 由有厚度的横截面生成连续表面，环向坐标在接缝处重复。 */
function surface(rows, sides, evaluate) {
  const positions = [], uv = [], indices = [];
  for (let row = 0; row <= rows; row++) for (let side = 0; side <= sides; side++) {
    positions.push(...evaluate(row / rows, side / sides));uv.push(side / sides, row / rows);
  }
  for (let row = 0; row < rows; row++) for (let side = 0; side < sides; side++) {
    const a = row * (sides + 1) + side, b = a + sides + 1;
    indices.push(a, b, a + 1, a + 1, b, b + 1);
  }
  const geometry = new T.BufferGeometry();geometry.setAttribute('position', new T.Float32BufferAttribute(positions, 3));
  geometry.setAttribute('uv', new T.Float32BufferAttribute(uv, 2));geometry.setIndex(indices);return geometry;
}

export function makeWhale() {
  const controls = names.map((Id, i) => ({Id, Parent: i ? names[i - 1] : null,
    Position: [0, i ? spineY[i] - spineY[i - 1] : spineY[i], i ? spineZ[i] - spineZ[i - 1] : spineZ[i]], Rotation: [0, 0, 0, 1]}));
  controls.push({Id: 'fin_left', Parent: 'chest', Position: [1.25, .2, -.2], Rotation: [0, 0, 0, 1]},
    {Id: 'fin_right', Parent: 'chest', Position: [-1.25, .2, -.2], Rotation: [0, 0, 0, 1]});
  const profile = new T.CatmullRomCurve3([
    [-6.5, .12, .14], [-5.5, .27, .24], [-4.5, .45, .38], [-3, .9, .72], [-1, 1.57, 1.23], [1, 2.05, 1.68],
    [3, 2, 1.73], [4.65, 1.42, 1.28], [5.55, .65, .65], [5.86, .015, .015]
  ].map(p => new T.Vector3(...p)), false, 'catmullrom', .35);
  const body = surface(88, 48, (along, around) => {
    const p = profile.getPoint(along), angle = around * Math.PI * 2;
    const centre = 2.15 + .45 * T.MathUtils.smoothstep(p.x, -3, 4);
    return [p.y * Math.sin(angle), p.x, centre + p.z * Math.cos(angle)];
  });
  // 环向与纵向的外法线对应，避免把主体内侧当作水面。
  const bodyIndex = body.index.array;for (let i = 0; i < bodyIndex.length; i += 3) [bodyIndex[i + 1], bodyIndex[i + 2]] = [bodyIndex[i + 2], bodyIndex[i + 1]];
  const parts = [part('body', skin(surfaceData(body, point => {
    let lower=0,upper=1;
    for(let i=0;i<16;i++) {const middle=(lower+upper)*.5;if(profile.getPoint(middle).x<point.y)lower=middle;else upper=middle;}
    const p=profile.getPoint((lower+upper)*.5);return 2*Math.min(p.y,p.z);
  }), spineWeights), 'water')];
  for (let side = 0; side < 2; side++) {
    const sign = side ? -1 : 1, control = 7 + side;
    const fin = surface(30, 16, (along, around) => {
      const chord = .035 + .75 * Math.sin(Math.PI * Math.pow(along, .72));
      const angle = around * Math.PI * 2;
      return [sign * (1.05 + 3.65 * along), .8 - 2.8 * along + chord * Math.cos(angle),
        2.3 - .7 * along + .12 * Math.sin(along * Math.PI) + (.04 + .13 * (1 - along)) * Math.sin(angle)];
    });
    if (sign > 0) {const a = fin.index.array;for (let i = 0; i < a.length; i += 3) [a[i + 1], a[i + 2]] = [a[i + 2], a[i + 1]];}
    parts.push(part(side ? 'fin_right' : 'fin_left', skin(surfaceData(fin,.22), point => {
      const blend = T.MathUtils.smoothstep(Math.abs(point.x), 1.05, 2.4);return [[2, 1 - blend], [control, blend]];
    }), 'water'));
  }
  const tail = surface(54, 16, (along, around) => {
    const x = (along * 2 - 1) * 3.3, edge = Math.abs(x) / 3.3, angle = around * Math.PI * 2;
    // 前缘连接尾柄，后缘中间收出缺口，两侧展开为有厚度的尾叶。
    const front = -6.1 - .28 * edge + .1 * edge * edge;
    const back = -7.05 - .8 * Math.sin(edge * Math.PI) + .5 * edge;
    const taper = Math.pow(Math.sin(Math.PI * along), .4);
    const centre = (front + back) * .5, chord = (front - back) * .5 * taper;
    return [x, centre + chord * Math.cos(angle), 2.05 + edge * edge * .25 + (.025 + .12 * taper) * Math.sin(angle)];
  });
  const tailIndex = tail.index.array;for (let i = 0; i < tailIndex.length; i += 3) [tailIndex[i + 1], tailIndex[i + 2]] = [tailIndex[i + 2], tailIndex[i + 1]];
  parts.push(part('flukes', skin(surfaceData(tail,.24), () => [[6, 1]]), 'water'));
  const dorsal = new T.Shape();dorsal.moveTo(-1.3, 0);dorsal.quadraticCurveTo(-.45, 1.1, .25, 1.25);
  dorsal.quadraticCurveTo(-.2, .4, .9, 0);dorsal.closePath();
  const back = new T.ExtrudeGeometry(dorsal, {depth: .24, bevelEnabled: true, bevelSegments: 3, bevelSize: .09, bevelThickness: .07, curveSegments: 16});
  back.rotateY(Math.PI / 2);back.rotateX(Math.PI / 2);back.translate(-.12, -1.2, 3.5);
  parts.push(part('dorsal', skin(surfaceData(back,.24), spineWeights), 'water'));
  for (const sign of [-1, 1]) parts.push(part(sign < 0 ? 'eye_right' : 'eye_left',
    new T.SphereGeometry(.105, 16, 10).scale(.6, 1, 1).translate(sign * 1.38, 4.65, 2.85), 'leather', [0, 0, 0], 'head'));
  const contacts = [
    {Id: 'head', Part: 0, A: [0, 4.9, 2.65], B: [0, 3, 2.6], Radius: 1.1, AControl: 'head', BControl: 'neck'},
    {Id: 'torso', Part: 0, A: [0, 2.6, 2.6], B: [0, -1.3, 2.3], Radius: 1.65, AControl: 'neck', BControl: 'waist'},
    {Id: 'tail_body', Part: 0, A: [0, -1.3, 2.3], B: [0, -4.6, 2.05], Radius: .65, AControl: 'waist', BControl: 'tail'},
    {Id: 'tail_stem', Part: 0, A: [0, -4.6, 2.05], B: [0, -6.4, 2.05], Radius: .25, AControl: 'tail', BControl: 'fluke'},
    {Id: 'tail_tip', Part: 3, A: [-2.8, -6.8, 2.2], B: [2.8, -6.8, 2.2], Radius: .2, AControl: 'fluke', BControl: 'fluke'}];
  const anchors = [{Id: 'front', Part: 0, Position: [0, 5.8, 2.65], Direction: [0, 1, 0], Control: 'head'},
    {Id: 'tail_tip', Part: 3, Position: [0, -7.5, 2.05], Direction: [0, -1, 0], Control: 'fluke'},
    {Id: 'fin_left', Part: 1, Position: [4.6, -2, 1.6], Direction: [1, 0, 0], Control: 'fin_left'},
    {Id: 'fin_right', Part: 2, Position: [-4.6, -2, 1.6], Direction: [-1, 0, 0], Control: 'fin_right'},
    {Id: 'belly_front', Part: 0, Position: [0, 2.1, .91], Direction: [0, 0, -1], Control: 'neck'},
    {Id: 'belly_back', Part: 0, Position: [0, -.8, 1.26], Direction: [0, 0, -1], Control: 'waist'}];
  const swim = motion('swim', 2.4, true, controls, 48, (index, phase) => {
    const angle = index < 7 ? [1.5, 2, 3, 5, 12, 18, 12][index] * Math.sin(phase * Math.PI * 2 - index * .58) :
      18 * (index === 7 ? 1 : -1) * Math.sin(phase * Math.PI * 2 - .8);
    return {rotation: new T.Quaternion().setFromAxisAngle(index < 7 ? new T.Vector3(1, 0, 0) : new T.Vector3(0, 1, 0), angle * Math.PI / 180).toArray()};
  });
  const tailSweep = motion('tail_sweep', 2.4, false, controls, 60, (index, phase) => {
    const keys = [[0,0],[.3,-1],[.46,1],[.63,.8],[1,0]];
    let k=0;while(k<keys.length-2 && phase>keys[k+1][0])k++;
    const t=T.MathUtils.smoothstep(phase,keys[k][0],keys[k+1][0]);
    const bend=T.MathUtils.lerp(keys[k][1],keys[k+1][1],t);
    const angle = index<7 ? [0,0,2,6,16,24,18][index]*bend : (index===7?1:-1)*10*bend;
    return {rotation:new T.Quaternion().setFromAxisAngle(new T.Vector3(0,0,1),angle*Math.PI/180).toArray()};
  },[{Id:'gather',At:0},{Id:'contact',At:1.104},{Id:'recover',At:1.512},{Id:'ready',At:2.4}]);
  // 仰身和翻转都绕胸腔。挂点、接触和表面共同读取这个骨架。
  const chest = new T.Vector3(0,1.1,2.4);
  const breachPoses = [[0,[-6,16,18,0]],[.31,[14,-13,-13,0]],[.54,[28,-7,-10,0]],[.84,[19,13,8,0]],
    [1.06,[4,-6,17,0]],[1.38,[-11,-16,-7,0]],[1.78,[-9,4,8,0]]];
  const overturnPoses = [...breachPoses.slice(0,3),[.82,[20,9,5,25]],[1.04,[9,14,14,105]],
    [1.24,[3,-8,-14,180]],[1.42,[-2,-16,-21,245]],[1.62,[-8,-3,-7,309]],
    [1.83,[-13,10,9,352]],[2.10,[-17,5,15,360]]];
  function rise(id,poses,markers) {
    const duration=poses.at(-1)[0];
    const keys=poses.map(([at,value])=>[at/duration,value]);
    return motion(id,duration,false,controls,(id==='breach'?58:68),(index,phase,control)=>{
      const [pitch,tail,fin,roll]=phaseValue(phase,keys);
      const turn=(axis,degrees)=>new T.Quaternion().setFromAxisAngle(axis,T.MathUtils.degToRad(degrees));
      if(index===0) {
        const root=turn(new T.Vector3(1,0,0),pitch).multiply(turn(new T.Vector3(0,1,0),roll));
        const position=new T.Vector3(...control.Position).sub(chest).applyQuaternion(root).add(chest);
        return {position:position.toArray(),rotation:root.toArray()};
      }
      if(index<7) return {rotation:turn(new T.Vector3(1,0,0),tail*[0,-.08,0,.16,.34,.68,.72][index]).toArray()};
      return {rotation:turn(new T.Vector3(0,1,0),fin*(index===7?1:-1)).toArray()};
    },markers.map(([Id,At])=>({Id,At})));
  }
  const breach=rise('breach',breachPoses,[['breach',.22],['crest',.88],['settle',1.38]]);
  const overturn=rise('overturn',overturnPoses,[['breach',.22],['crest',.88],['inverted',1.24],['press',1.83]]);
  write('water_whale', parts, anchors, contacts, controls, [swim,tailSweep,breach,overturn], false, {Forward:[0,1,0],Up:[0,0,1],HideBelowGround:true},
    structure('skinned','forms_whale.mjs','makeWhale',{spineY,spineZ,bodySampling:[88,48],breachPoses,overturnPoses},
      {body:['body.trunk'],fin_left:['body.fin'],fin_right:['body.fin'],flukes:['body.tail'],dorsal:['body.fin.dorsal'],eye_left:['body.eye'],eye_right:['body.eye']},
      [['body','fin_left',[1.05,.8,2.3]],['body','fin_right',[-1.05,.8,2.3]],['body','flukes',[0,-6.2,2.05]],
        ['body','dorsal',[0,-1.2,3.5]],['body','eye_left',[1.38,4.65,2.85]],['body','eye_right',[-1.38,4.65,2.85]]]),[],[
      {Id:'tail_sweep',Motion:'tail_sweep',Scale:[.1,3],Rate:[.5,2],Turn:[-60,60],Supports:[],Contacts:[
        {Id:'tail',Anchor:'tail_tip',PartnerRole:'body.trunk',From:'contact',Until:'recover',Gap:.2,Angle:30,Opposed:true}]}]);
  bellySpill();
}

/** 腹部落水为连续有厚度的水帘，接触面就在同一段湿体上。 */
function bellySpill() {
  const profile=[[0,.06,.04],[.22,.27,.11],[.78,.51,.17],[1.43,.60,.24],[2.1,.51,.27],[2.46,.28,.18],[2.57,.015,.012]];
  const curve=new T.CatmullRomCurve3(profile.map(p=>new T.Vector3(...p)),false,'catmullrom',.28);
  const mesh=surface(38,28,(along,around)=>{
    const p=curve.getPoint(along), a=-around*2*Math.PI;
    const x=p.y*Math.cos(a)*(1+.10*Math.sin(a*3+along*9));
    const y=p.z*Math.sin(a)*(1+.17*Math.sin(a*5-along*8));
    return [x,y+Math.sin(along*Math.PI)*.13,p.x];
  });
  const contact=(Id,z0,z1,Radius)=>({Id,Part:0,A:[0,.09,z0],B:[0,.09,z1],Radius});
  write('water_whale_spill',[part('sheet',surfaceData(mesh,point=>.12+.30*Math.sin(point.z/2.57*Math.PI)),'water')],[],[
    contact('neck',.20,.60,.24),contact('body',.82,1.53,.47),contact('lip',1.78,2.20,.45)],[],[],false,
    {Forward:[0,0,1],Up:[0,1,0],HideBelowGround:true},
    structure('surface','forms_whale.mjs','makeWhale',{profile},{sheet:['fluid.body']}));
}
