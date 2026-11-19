/** 龙卷与玄阴领域各自保留连续厚体，共用原领域执行，不建立专用播放循环。 */
import * as T from 'three';
import {part,write,skin,motion,structure,surfaceData,round} from './form_export.mjs';

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
  const controls=['vortex','inlet','inner','outer'].map(Id=>({Id,Position:[0,0,0],Rotation:[0,0,0,1]}));
  const pressure=body([[0,.01],[2.27,.01],[2.50,.10],[2.51,.20],[2.34,.29],[1.34,.34],
    [.86,.62],[.80,1.25],[.81,2.50],[.78,3.0],[.62,3.35],[.29,3.64],[0,3.70]],64);
  const pieces=[part('wind_body',surfaceData(pressure,.28,p=>.66-.30*T.MathUtils.smoothstep(p.z,.25,1.1)),'wind_pressure',[0,0,0],'vortex'),
    ...Array.from({length:3},(_,i)=>part(`fold_${i}`,windFold(i),'wind_open',[0,0,0],controls[i+1].Id))];
  const rates=[2,1,3,2];
  const pose=(i,p)=>({rotation:new T.Quaternion().setFromAxisAngle(new T.Vector3(0,0,1),p*2*Math.PI*rates[i]).toArray()});
  const run=motion('run',2.4,true,controls,96,pose);
  const pressureRun=motion('pressure',2.45,false,controls,98,(i,p)=>{
    const time=p*2.45, open=T.MathUtils.smoothstep(time,2.03,2.45);
    return {...pose(i,time/2.4),position:[.12*open*Math.cos(i*2.09),.12*open*Math.sin(i*2.09),i*.08*open]};
  });
  for(const key of pressureRun.Keys)key.Fade=round(Math.max(1-T.MathUtils.smoothstep(key.At,0,.21),T.MathUtils.smoothstep(key.At,2.02,2.45)));
  write('wind_vortex_open',pieces,[],windContacts(),controls,[run,pressureRun],false,
    {Forward:[0,1,0],Up:[0,0,1],HideBelowGround:true},
    structure('volume','forms_remaining_fields.mjs','windOpen',{radius:2.5,height:3.7,cycle:2.4,folds:3},
      {wind_body:['wind.flow','field.body'],fold_0:['wind.flow'],fold_1:['wind.flow'],fold_2:['wind.flow']},[]));
}

/** 折叠的短剪切面：前段压紧，后段逐片错开，截面纯暗而不画发光边线。 */
function cuttingLayer(index, count, size, column) {
  const positions=[],uv=[],triangles=[],sides=8,segments=22;
  for(let s=0;s<=segments;s++) {
    const t=s/segments, edge=Math.max(.022,Math.sin(Math.PI*t)**.64), angle=index*2*Math.PI/count+t*(1.8+index*.13);
    const r=size*(.94-.53*Math.sin(t*Math.PI*.72)+.11*Math.cos(index*2)),
          height=size*((column?.19:.095) + (.64+.10*index)*t);
    for(let j=0;j<=sides;j++) {
      const a=j*2*Math.PI/sides, spread=Math.cos(a)*(.23+.15*t)*edge, z=Math.sin(a)*(.085-.03*t)*edge;
      const radial=r+size*spread;
      positions.push(radial*Math.cos(angle),radial*Math.sin(angle),Math.max(.007,height+size*(.27*spread+z)));
      uv.push(t,j/sides);
      if(s<segments&&j<sides) {
        const k=s*(sides+1)+j,m=k+sides+1;
        triangles.push(k,m,m+1,k,m+1,k+1);
      }
    }
  }
  for(let j=1;j<sides-1;j++) {
    const last=segments*(sides+1);
    triangles.push(0,j,j+1,last,last+j+1,last+j);
  }
  const geometry=new T.BufferGeometry();
  geometry.setAttribute('position',new T.Float32BufferAttribute(positions,3));
  geometry.setAttribute('uv',new T.Float32BufferAttribute(uv,2));
  geometry.setIndex(triangles);
  return surfaceData(geometry,.20,(_,i)=>Math.sin(Math.PI*uv[i*2])**.65);
}

/** 来袭风股用原旋转作用芯，原中央风柱无伤；余痕仅供真实结算后绘制。 */
function baleful(id, column=false, mark=false) {
  const count=column?5:3,size=mark?.61:2.5, contacts=column||mark?[]:windContacts();
  const controls=Array.from({length:count},(_,i)=>({
    Id:i===0&&contacts.length?'vortex':`cut_${i}`,Position:[0,0,0],Rotation:[0,0,0,1]
  }));
  const layers=Array.from({length:count},(_,i)=>part(`cut_${i}`,cuttingLayer(i,count,size,column),
    i%2===0?'wind_shear':'wind_fold_dark',[0,0,0],controls[i].Id));
  // 原索引零的作用芯仍绑向 vortex。新增控制只带动外部各片。
  const clip=motion('run',1.2,true,controls,48,(i,p)=>({
    rotation:new T.Quaternion().setFromAxisAngle(new T.Vector3(0,0,1),p*2*Math.PI*(i===0?1:i%2?2:1)).toArray()
  }));
  write(id,layers,[],contacts,controls,[clip],false,{Forward:[0,1,0],Up:[0,0,1],HideBelowGround:true},
    structure('volume','forms_remaining_fields.mjs','baleful',{size,count,column,mark},
      Object.fromEntries(layers.map(layer=>[layer.Id,['wind.flow']])),[]));
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

/** 阴质从低凹面滑入，三处厚肩错开包向内侧，留出明确的缺口。 */
function yinBasin() {
  const geometry=body([[0,.016],[2.24,.016],[2.44,.041],[2.50,.10],[2.47,.20],
    [2.33,.34],[2.11,.39],[1.84,.31],[1.68,.22],[1.26,.13],[.76,.145],[0,.17]],96);
  const points=geometry.getAttribute('position');
  for(let i=0;i<points.count;i++) {
    const p=new T.Vector3().fromBufferAttribute(points,i), a=Math.atan2(p.y,p.x), r=Math.hypot(p.x,p.y);
    const rim=T.MathUtils.smoothstep(r,1.38,2.23)*T.MathUtils.smoothstep(p.z,.09,.34);
    const shoulder=T.MathUtils.smoothstep(Math.cos(a*2.65 + .46*Math.sin(a*2) - r*.84),-.50,.94);
    const gap=T.MathUtils.smoothstep(Math.cos(a-.55),.52,.96);
    const lift=(.22+.73*shoulder)*(1-.96*gap);
    const curl=a+.17*rim*lift;
    const fold=.043*Math.sin(a*4-r*6.8)*T.MathUtils.smoothstep(r,.32,2)*(1-rim);
    const edge=T.MathUtils.smoothstep(r,.78,2.48);
    const radius=r+edge*(.13*Math.sin(a*3-.29)+.057*Math.sin(a*7+.38))-.21*rim*lift;
    points.setXYZ(i, Math.cos(curl)*radius, Math.sin(curl)*radius,
      p.z+rim*(lift-.07)+.10*rim*Math.cos(a*4+.9)+fold*T.MathUtils.smoothstep(p.z,.08,.17));
  }
  publishYin('yin_field_basin','yinBasin',geometry,'yin_basin',1.24);
}

/** 裹身为向内弯合的厚膜，流束有连续起伏和闭合截面。二者只由实际转移发布。 */
function yinTransfer() {
  const wrap=body([[0,-.41],[.43,-.42],[.78,-.37],[.93,-.18],[1,.08],
    [.91,.31],[.72,.51],[.59,.58],[.49,.52],[.54,.38],[.68,.24],
    [.73,.04],[.68,-.19],[.41,-.32],[0,-.29]],64);
  const points=wrap.getAttribute('position');
  for(let i=0;i<points.count;i++) {
    const x=points.getX(i), y=points.getY(i), z=points.getZ(i), angle=Math.atan2(y,x);
    const upper=T.MathUtils.smoothstep(z,-.28,.48)*Math.hypot(x,y);
    const notch=T.MathUtils.smoothstep(Math.cos(angle+.65-z*1.9),.10,.98);
    const squeeze=1-.25*upper*Math.cos(angle*2+z*4);
    points.setXYZ(i,x*squeeze,y*squeeze,z-upper*(.42*notch+.10*Math.sin(angle*3)));
  }
  const flow=new T.LatheGeometry([[0,0],[.23,.06],[.63,.19],[1,.34],
    [.88,.53],[.60,.72],[.27,.90],[0,1]].map(p=>new T.Vector2(...p)),32);
  const flowPoints=flow.getAttribute('position');
  for(let i=0;i<flowPoints.count;i++) {
    const x=flowPoints.getX(i), t=flowPoints.getY(i), z=flowPoints.getZ(i), bend=Math.sin(t*Math.PI);
    flowPoints.setXYZ(i,x+.70*bend,t,z*(.66+.21*Math.sin(t*11))+.73*bend);
  }
  const face={Forward:[0,1,0],Up:[0,0,1],HideBelowGround:true};
  for (const [type, geometry, surface] of [['wrap',wrap,'yin_wrap'],['flow',flow,'yin_flow']])
    write(`yin_transfer_${type}`,[part('yin_body',geometry,surface)],[],[],[],[],false,face,
      structure('volume','forms_remaining_fields.mjs','yinTransfer',{cycle:.68}, {yin_body:[`yin.${type}`]},[]));
}

function publishYin(id,entry,geometry,surface,height) {
  const controls=[{Id:'centre',Position:[0,0,0],Rotation:[0,0,0,1]},...Array.from({length:4},(_,i)=>
    ({Id:`rim_${i}`,Position:[Math.cos(i*Math.PI/2)*2,Math.sin(i*Math.PI/2)*2,0],Rotation:[0,0,0,1]}))];
  skin(geometry,yinWeights);
  const bind=p=>yinWeights(new T.Vector3(...p)).map(([i,Weight])=>({Control:controls[i].Id,Weight}));
  const contacts=floorContacts(0,(a,b)=>({AInfluences:bind(a),BInfluences:bind(b)}));
  const run=motion('run',2.4,true,controls,48,(i,p,c)=>({position:[c.Position[0],c.Position[1],i?.015*Math.sin(p*Math.PI*2+i):0]}));
  const clips=[run];
  if(surface==='yin_basin') {
    const gather=motion('gather',2.65,false,controls,64,(i,p,c)=>{
      const time=p*2.65, edge=i!==0;
      const fold=T.MathUtils.smoothstep(time,0,.31)*(1-T.MathUtils.smoothstep(time,2.01,2.56));
      const turn=edge ? .025*Math.sin(time*6.2 + i*.9)*fold : 0;
      return {position:[c.Position[0]-c.Position[1]*turn,c.Position[1]+c.Position[0]*turn,
        edge ? -.26*(1-fold)+.033*Math.sin(time*4+i*1.5)*fold : -.12*(1-fold)]};
    });
    for(const key of gather.Keys) key.Fade=round(Math.max(1-T.MathUtils.smoothstep(key.At,.025,.32),
      T.MathUtils.smoothstep(key.At,2.03,2.60)));
    clips.push(gather);
  }
  write(id,[part('yin_surface',geometry,surface)],[],contacts,controls,clips,false,
    {Forward:[0,1,0],Up:[0,0,1],HideBelowGround:true},
    structure('surface','forms_remaining_fields.mjs',entry,{radius:2.5,height,cycle:2.4},
      {yin_surface:['yin.surface','field.body']},[]));
}

/** 一次导出两种剩余的原攻击领域。 */
export function makeRemainingFields(){
  wind();yin();windOpen();yinBasin();yinTransfer();
  baleful('baleful_wind_shear');baleful('baleful_wind_column',true);baleful('baleful_wind_mark',false,true);
}
