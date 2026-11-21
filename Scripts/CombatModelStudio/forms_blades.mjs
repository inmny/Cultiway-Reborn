/** 推进刃波的宽面轮廓、封闭厚度与接触芯共同导出。 */
import * as T from 'three';
import {write,structure,dissolve} from './form_export.mjs';
import {wallPart,column} from './forms_solid_walls.mjs';

/** 横向弯曲的封闭刃面，末端连续收尖；宽度与厚度不是贴图或拖尾。 */
function ribbon(point,depth,height,count=40) {
  const rings=[];
  for(let i=0;i<=count;i++) {
    const u=-1+2*i/count,p=point(u),taper=Math.max(.018,Math.pow(1-u*u,.6));
    rings.push(Array.from({length:8},(_,side)=>{
      const a=side*Math.PI/4;
      return [p[0],(p[1]??0)-Math.sin(a)*height*taper,p[2]+Math.cos(a)*depth*taper];
    }));
  }
  return column(rings,8);
}

/** 接触只沿宽面中心逐段连接，不把弯曲主体外的空隙填成隐藏伤害球。 */
function cores(point,radius=.10,count=16) {
  return Array.from({length:count},(_,i)=>{
    const a=-.9+1.8*i/count,b=-.9+1.8*(i+1)/count;
    return [point(a),point(b),radius*Math.pow(1-Math.max(a*a,b*b),.6)];
  });
}

/** 共用前上向、根部和版本8制作结构；每种来源保留自身完整实体。 */
function publish(id,pieces,contacts,parameters,ending=false) {
  const controls=[{Id:'root',Position:[0,0,0],Rotation:[0,0,0,1]}];
  const parts=pieces.map(([name,g,s,fixedSurface])=>{
    const mesh=wallPart(name,g,s,[0,0,0],'root');
    if(fixedSurface)mesh.UseMatterSurface=false;
    return mesh;
  });
  const shapes=contacts.map((contact,i)=>Array.isArray(contact)
    ? {Id:`edge_${i}`,Part:contact[3] ?? 0,A:contact[0],B:contact[1],Radius:contact[2],AControl:'root',BControl:'root'}
    : contact);
  const motions=ending?[dissolve(controls,(_index,p)=>({
      position:[0,.16*p,-.06*p],
      rotation:new T.Quaternion().setFromAxisAngle(new T.Vector3(1,0,0),-.12*p).toArray()
  }))]:[];
  write(id,parts,[{Id:'root',Part:0,Position:[0,0,0],Direction:[0,0,1],Control:'root'}],shapes,controls,motions,false,
    {Forward:[0,0,1],Up:[0,1,0]},structure('rigid','forms_blades.mjs','makeBladeForms',parameters,
      Object.fromEntries(parts.map(p=>[p.Id,['projectile.body','projectile.edge']])),[]));
}

/** 剑气沿实际锋线磨出窄斜面，后侧再加厚成分明的刃背。 */
function sword() {
  const edge=u=>[u*1.85,0,.42-.55*Math.abs(u)];
  // 各站点加厚的是刃后。中锋、肩部和两翼的后缘各有折角，前缘仍沿同一条作用线。
  const wing=[[0,.78,.17],[.18,.59,.16],[.39,.52,.13],[.64,.57,.12],[.83,.30,.08],[.94,.12,.038],[1,.025,.013]];
  const sections=[...wing.slice(1).reverse().map(([u,d,h])=>[-u,d,h]),...wing];
  const rings=sections.map(([u,d,h])=>{
    const [x,,z]=edge(u),bevel=Math.min(.076,d*.37);
    return [[x,0,z+Math.min(.042,d*.52)],[x,-h*.25,z-bevel*.34],[x,-h*.76,z-bevel],
      [x,-h,z-d*.57],[x,-h*.46,z-d],[x,h*.46,z-d],
      [x,h,z-d*.57],[x,h*.76,z-bevel],[x,h*.25,z-bevel*.34]];
  });
  // 厚脊紧接同一刃背，集中在中段与肩部，翼端只留下锋口。
  const backing=sections.filter(([u])=>Math.abs(u)<=.64).map(([u,d,h])=>{
    const [x,,z]=edge(u), shoulder=Math.abs(u)===.64;
    return [[x,0,z-d*.47],[x,-h*1.07,z-d*.61],[x,-h*(shoulder?.34:.76),z-d*.90],
      [x,h*(shoulder?.34:.76),z-d*.90],[x,h*1.07,z-d*.61]];
  });
  // 作用区沿现有折锋。保留两端的收尖余量和前缘两侧的空隙。
  const points=[-.9,0,.9].map(u=>{const [x,,z]=edge(u);return [x,z];});
  const contact={Id:'edge_0',Part:0,A:[0,0,0],AControl:'root',
    Path:{Points:points,HalfWidth:.10,HalfThickness:.05}};
  publish('sword_qi',[['edge',column(rings,9),'metal'],['ridge',column(backing,5),'bronze',true]],
    [contact],{span:3.7,bevel:.076,backDepth:.78},true);

  // 短锋有窄的向前折面，厚度聚在两肩，后缘没有另加一圈弧线。
  const short=u=>[u*1.25,0,.49-.77*u*u+.065*u];
  const back=u=>[u*1.14,-.035,.22-.68*u*u+.063*u];
  publish('sword_qi_1',[
    ['edge',ribbon(short,.095,.077,28),'metal'],
    ['ridge',ribbon(back,.22,.118,28),'gold',true]
  ],cores(short,.080,16),{span:2.50,bevel:.071,backDepth:.22});
}

/** 风刃由三道交错的宽弯流组成，分叉与留白可辨，不使用细线或烟团。 */
function wind() {
  const edge=u=>[u*1.75,0,.50-.72*u*u];
  const parts=[['front',ribbon(edge,.29,.205),'wind_pressure']];
  for(const sign of [-1,1]) {
    const flow=u=>[u*1.50+sign*.18,sign*.08,-.18+.20*Math.sin(u*2.3+sign*.4)];
    parts.push([`flow_${sign}`,ribbon(flow,.20,.14),'wind_open']);
  }
  publish('wind_blade',parts,cores(edge),{span:3.5,layers:3,depth:.29},true);

  // 紧缩的单瓣以较深背脊承接前锋，截面偏在一翼，远看能辨认受力侧。
  const crest=u=>[u*1.36,.08*u,.43-.67*u*u+.22*u*u*u];
  const heel=u=>[u*1.22,-.075+.095*u,.14-.59*u*u+.20*u*u*u];
  publish('wind_blade_1',[
    ['crest',ribbon(crest,.15,.17,28),'wind_pressure'],
    ['heel',ribbon(heel,.23,.226,28),'wind_open']
  ],cores(crest,.12,16),{span:2.72,layers:2,depth:.23});
}

/** 火刃有连续焰锋与五个独立收尖的厚焰舌，不以散乱粒子替代作用主体。 */
function fire() {
  const edge=u=>[u*1.72,0,.37-.42*u*u];
  const pieces=[['front',ribbon(edge,.28,.16),'flame_cast']];
  for(let i=0;i<5;i++) {
    const x=(i-2)*.59,length=.65+.25*Math.cos(i*2),rings=[];
    for(const [t,r] of [[0,.22],[.28,.24],[.64,.14],[1,.008]])
      rings.push(Array.from({length:10},(_,j)=>{
        const a=j*Math.PI/5;return [x+Math.cos(a)*r+.10*Math.sin(t*4+i),-Math.sin(a)*r*.65,-.05-t*length];
      }));
    pieces.push([`tongue_${i}`,column(rings,10),'flame_cast']);
  }
  publish('fire_blade',pieces,cores(edge),{span:3.44,tongues:5,depth:.28},true);

  // 两股焰锋沿相反的局部弧弯折，接触芯也分开，中央没有补一段隐形作用。
  const forkPieces=[],forkCores=[];
  for(const sign of [-1,1]) {
    const fork=u=>[sign*(1.02+.72*u),.08+sign*.08*u,.46-.37*u+.31*(1-u*u)];
    forkCores.push(...cores(fork,.09,10).map(core=>[...core,forkPieces.length]));
    forkPieces.push([`branch_${sign}`,ribbon(fork,.31,.16,28),'flame_cast']);
  }
  publish('fire_blade_fork',forkPieces,forkCores,{span:3.48,gap:.60,branches:2},true);

  // 亮刃的热脊很厚，刃口只露出窄面；两者为同一段封闭的短弧。
  const bright=u=>[u*1.52,0,.40-.66*u*u];
  const ridge=u=>[u*1.48,-.025,.17-.64*u*u];
  publish('fire_blade_bright',[
    ['ridge',ribbon(ridge,.38,.19,32),'flame_cast'],
    ['lip',ribbon(bright,.15,.09,32),'solar_kernel',true]
  ],cores(bright,.08).map(core=>[...core,1]),{span:3.04,ridgeDepth:.38});

  // 回钩把厚面偏到一翼，另一翼沿深弧收尖，接触沿实际前缘并留出弧内空隙。
  const hook=u=>[u*1.70,.07*u,.65-1.10*u*u+.28*u*u*u];
  const back=u=>[u*1.63,.04*u,.38-1.03*u*u+.25*u*u*u];
  publish('fire_blade_hook',[
    ['face',ribbon(back,.34,.16,36),'flame_cast'],
    ['tip',ribbon(hook,.19,.14,36),'flame_cast']
  ],cores(hook,.10).map(core=>[...core,1]),{span:3.40,arcDepth:1.10});
}

/** 阴刃偏斜成厚钩月，冷亮刃缘包住深色刃背，左右收尾明显不同。 */
function yin() {
  const edge=u=>[u*1.72-.10,0,.46-.73*u*u+.24*u*u*u];
  const back=u=>[u*1.63-.10,0,.20-.70*u*u+.24*u*u*u];
  const contact=cores(edge,.09).map(c=>[...c,1]);
  publish('yin_blade',[['back',ribbon(back,.30,.18),'yin_depth'],['edge',ribbon(edge,.14,.13),'yin_moon']],
    contact,{span:3.44,profile:'asymmetric_hook',depth:.30});

  // 不同宽度的背面与刃缘合拢。开口留在环内，侧肩沿同一锋线收尖。
  const sections=[[-1,.023,.019],[-.91,.115,.049],[-.71,.29,.11],[-.44,.32,.15],[-.22,.48,.18],
    [.05,.59,.20],[.31,.45,.18],[.58,.38,.14],[.82,.25,.099],[.96,.098,.044],[1,.025,.021]];
  const faces=sections.map(([u,depth,height])=>{
    const [x,,z]=edge(u);
    return [[x,0,z],[x,-height*.67,z-depth*.14],[x,-height,z-depth*.61],
      [x,-height*.34,z-depth],[x,height*.39,z-depth],[x,height,z-depth*.55],[x,height*.64,z-depth*.12]];
  });
  const bevel=sections.map(([u,depth,height])=>{
    const [x,,z]=edge(u);
    const front=Math.min(.086,depth*.28);
    return [[x,0,z+front],[x,-height*.57,z+front*.27],[x,-height*.66,z-front*.9],
      [x,height*.44,z-front*.9],[x,height*.57,z+front*.21]];
  });
  publish('yin_blade_0',[
    ['hooked_body',column(faces,7),'yin_wrap'],['bevel',column(bevel,5),'yin_moon']
  ],contact,{span:3.44,profile:'asymmetric_facet',depth:.59},true);
}

/** 一次发布原刃波的独立主体。 */
export function makeBladeForms() {sword();wind();fire();yin();}
