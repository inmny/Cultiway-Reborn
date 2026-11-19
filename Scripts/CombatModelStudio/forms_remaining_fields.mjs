/** 龙卷与玄阴领域各自保留连续厚体，共用原领域执行，不建立专用播放循环。 */
import * as T from 'three';
import {part,write,skin,motion,structure,surfaceData} from './form_export.mjs';

/** 按连续横截面构成封闭旋体，制作上向为Z。 */
function body(profile,segments=64) {
  return new T.LatheGeometry(profile.map(p=>new T.Vector2(...p)),segments).rotateX(Math.PI/2);
}

/** 低层实际作用区按主体圆面布置，芯位于可见厚体内。 */
function floorContacts(partIndex,bind) {
  const contacts=[];
  for(let row=-14;row<=14;row++) {
    const y=row*.16,width=Math.sqrt(2.5*2.5-y*y)-.18;
    const a=[-width,y,.17],b=[width,y,.17];
    contacts.push({Id:`floor_${row+14}`,Part:partIndex,A:a,B:b,Radius:.12,...bind(a,b)});
  }
  return contacts;
}

/** 有宽风底的连续旋卷风体，三条宽曲脊融在主体中，不使用线圈或独立拖尾。 */
function wind() {
  const geometry=body([[0,.02],[2.28,.02],[2.5,.12],[2.5,.35],[1.72,.55],[.95,1.2],
    [1.25,2.1],[1.85,2.9],[2.12,3.25],[1.9,3.45],[0,3.45]],72);
  const points=geometry.getAttribute('position');
  for(let i=0;i<points.count;i++) {
    const p=new T.Vector3().fromBufferAttribute(points,i),angle=Math.atan2(p.y,p.x);
    const sway=.09*T.MathUtils.smoothstep(p.z,.4,1)*Math.sin(angle*3-p.z*2.4);
    points.setXYZ(i,p.x*(1+sway),p.y*(1+sway),p.z);
  }
  const controls=[{Id:'vortex',Position:[0,0,0],Rotation:[0,0,0,1]}];
  const contacts=windContacts();
  const run=windRun(controls);
  write('wind_vortex',[part('wind_body',geometry,'wind',[0,0,0],'vortex')],[],contacts,controls,[run],false,
    {Forward:[0,1,0],Up:[0,0,1],HideBelowGround:true},
    structure('volume','forms_remaining_fields.mjs','wind',{radius:2.5,height:3.45,cycle:1.2},
      {wind_body:['wind.flow','field.body']},[]));
}

function windContacts() {
  const contacts=floorContacts(0,()=>({AControl:'vortex',BControl:'vortex'}));
  contacts.push({Id:'wind_core',Part:0,A:[0,0,.3],B:[0,0,2.9],Radius:.80,AControl:'vortex',BControl:'vortex'});
  return contacts;
}

function windRun(controls) {
  return motion('run',1.2,true,controls,48,(_,p)=>({rotation:new T.Quaternion().setFromAxisAngle(new T.Vector3(0,0,1),p*Math.PI*2).toArray()}));
}

/** 每股宽流面为椭圆厚截面，沿地面卷入，越过腰部后向外散开。 */
function windFold(index) {
  const points=[],uv=[],faces=[],rings=44,sides=10;
  for(let i=0;i<=rings;i++) {
    const t=i/rings, waist=Math.exp(-Math.pow((t-.47)/.23,2));
    const angle=index*2.09 + t*(5.1+.34*index), z=.13 + (3.2+.21*index)*Math.pow(t,1.22);
    const r=1.91-.88*waist+.13*Math.sin(t*6+index);
    const fade=Math.max(.025,Math.pow(Math.sin(t*Math.PI),.65));
    const width=fade*(.20+.21*(1-waist)), depth=fade*(.22+.19*t);
    for(let j=0;j<=sides;j++) {
      const v=j*Math.PI*2/sides,c=Math.cos(v),h=Math.sin(v);
      const radial=r+width*c;
      points.push(radial*Math.cos(angle),radial*Math.sin(angle),Math.max(.015,z+depth*(.8*h+.23*c)));
      uv.push(t,j/sides);
      if(i<rings&&j<sides) {
        const a=i*(sides+1)+j,b=a+sides+1;
        faces.push(a,b,b+1,a,b+1,a+1);
      }
    }
  }
  for(let j=1;j<sides-1;j++) {
    const end=rings*(sides+1);
    faces.push(0,j,j+1,end,end+j+1,end+j);
  }
  const geometry=new T.BufferGeometry();
  geometry.setAttribute('position',new T.Float32BufferAttribute(points,3));
  geometry.setAttribute('uv',new T.Float32BufferAttribute(uv,2));
  geometry.setIndex(faces);
  // 单股起尾由厚度和透光共同淡出，宽面之间留出看穿空隙。
  return surfaceData(geometry,.3,(_,i)=>Math.sin(geometry.getAttribute('uv').getX(i)*Math.PI));
}

function windOpen() {
  const controls=[{Id:'vortex',Position:[0,0,0],Rotation:[0,0,0,1]}];
  const pressure=body([[0,.01],[2.27,.01],[2.50,.10],[2.51,.20],[2.34,.29],[1.34,.34],
    [.86,.62],[.80,1.25],[.81,2.50],[.78,3.0],[.62,3.35],[.29,3.64],[0,3.70]],64);
  const pieces=[part('wind_body',surfaceData(pressure,.28,()=>.64),'wind_pressure',[0,0,0],'vortex'),
    ...Array.from({length:3},(_,i)=>part(`fold_${i}`,windFold(i),'wind_open',[0,0,0],'vortex'))];
  write('wind_vortex_open',pieces,[],windContacts(),controls,[windRun(controls)],false,
    {Forward:[0,1,0],Up:[0,0,1],HideBelowGround:true},
    structure('volume','forms_remaining_fields.mjs','windOpen',{radius:2.5,height:3.7,cycle:1.2,folds:3},
      {wind_body:['wind.flow','field.body'],fold_0:['wind.flow'],fold_1:['wind.flow'],fold_2:['wind.flow']},[]));
}

/** 五项控制中的中心与相邻两个边缘共同承托连续暗面。 */
function yinWeights(p) {
  const radius=Math.min(1,Math.hypot(p.x,p.y)/2.5),angle=(Math.atan2(p.y,p.x)+Math.PI*2)%(Math.PI*2);
  const sector=angle/(Math.PI/2),a=Math.floor(sector),t=sector-a,edge=radius*radius;
  return [[0,1-edge],[a+1,edge*(1-t)],[(a+1)%4+1,edge*t]];
}

/** 玄阴以低伏连续暗面向内收束，起伏来自自身控制，不堆叠刀刃或换色毒雾。 */
function yin() {
  const geometry=body([[0,.02],[2.25,.02],[2.5,.12],[2.5,.28],[2.25,.40],[0,.30]]),points=geometry.getAttribute('position');
  for(let i=0;i<points.count;i++) {
    const p=new T.Vector3().fromBufferAttribute(points,i),r=Math.min(1,Math.hypot(p.x,p.y)/2.5);
    points.setZ(i,p.z+.055*r*r*T.MathUtils.smoothstep(p.z,.20,.35)*Math.cos(Math.atan2(p.y,p.x)*3));
  }
  publishYin('yin_field','yin',geometry,'yin_depth',.455);
}

/** 低处有连续作用面，偏离全圆的外缘有向内折的高肩和可见开口。 */
function yinBasin() {
  const geometry=body([[0,.015],[2.22,.015],[2.47,.075],[2.50,.15],[2.47,.24],
    [2.35,.34],[2.16,.33],[1.84,.24],[1.36,.19],[.70,.165],[0,.16]],72);
  const points=geometry.getAttribute('position');
  for(let i=0;i<points.count;i++) {
    const p=new T.Vector3().fromBufferAttribute(points,i), a=Math.atan2(p.y,p.x), r=Math.hypot(p.x,p.y);
    const rim=T.MathUtils.smoothstep(r,1.38,2.36)*T.MathUtils.smoothstep(p.z,.13,.32);
    const lift=.51*T.MathUtils.smoothstep(Math.sin(a*2-.6+Math.sin(a)*.56)+.24*Math.cos(a*3+1.1),-.53,1);
    const depth=.055*Math.sin(a*3-r*3.4)*T.MathUtils.smoothstep(r,.25,1.9)*(1-rim);
    points.setZ(i,p.z+rim*(lift-.08)+depth*T.MathUtils.smoothstep(p.z,.08,.18));
  }
  publishYin('yin_field_basin','yinBasin',geometry,'yin_basin',.85);
}

function publishYin(id,entry,geometry,surface,height) {
  const controls=[{Id:'centre',Position:[0,0,0],Rotation:[0,0,0,1]},...Array.from({length:4},(_,i)=>
    ({Id:`rim_${i}`,Position:[Math.cos(i*Math.PI/2)*2,Math.sin(i*Math.PI/2)*2,0],Rotation:[0,0,0,1]}))];
  skin(geometry,yinWeights);
  const bind=p=>yinWeights(new T.Vector3(...p)).map(([i,Weight])=>({Control:controls[i].Id,Weight}));
  const contacts=floorContacts(0,(a,b)=>({AInfluences:bind(a),BInfluences:bind(b)}));
  const run=motion('run',2.4,true,controls,48,(i,p,c)=>({position:[c.Position[0],c.Position[1],i?.015*Math.sin(p*Math.PI*2+i):0]}));
  write(id,[part('yin_surface',geometry,surface)],[],contacts,controls,[run],false,
    {Forward:[0,1,0],Up:[0,0,1],HideBelowGround:true},
    structure('surface','forms_remaining_fields.mjs',entry,{radius:2.5,height,cycle:2.4},
      {yin_surface:['yin.surface','field.body']},[]));
}

/** 一次导出两种剩余的原攻击领域。 */
export function makeRemainingFields(){wind();yin();windOpen();yinBasin();}
