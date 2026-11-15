/** 五种推进刃波：宽面轮廓、封闭厚度与沿实际主体分布的接触芯共同导出。 */
import * as T from 'three';
import {write,structure,surfaceData} from './form_export.mjs';
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
function publish(id,pieces,contacts,parameters) {
  const controls=[{Id:'root',Position:[0,0,0],Rotation:[0,0,0,1]}];
  const parts=pieces.map(([name,g,s])=>wallPart(name,g,s,[0,0,0],'root'));
  const shapes=contacts.map(([a,b,r,part=0],i)=>({Id:`edge_${i}`,Part:part,A:a,B:b,Radius:r,AControl:'root',BControl:'root'}));
  write(id,parts,[{Id:'root',Part:0,Position:[0,0,0],Direction:[0,0,1],Control:'root'}],shapes,controls,[],false,
    {Forward:[0,0,1],Up:[0,1,0]},structure('rigid','forms_blades.mjs','makeBladeForms',parameters,
      Object.fromEntries(parts.map(p=>[p.Id,['projectile.body','projectile.edge']])),[]));
}

/** 剑气用折锋与宽脊形成有尖端的金属弧面，保留明确刃背。 */
function sword() {
  const edge=u=>[u*1.85,0,.42-.55*Math.abs(u)];
  const ridge=u=>[u*1.72,.07,.24-.49*Math.abs(u)];
  publish('sword_qi',[['edge',ribbon(edge,.34,.14,32),'metal'],['ridge',ribbon(ridge,.10,.14,32),'gold']],
    cores(edge),{span:3.7,profile:'angular',depth:.34});
}

/** 风刃由三道交错的宽弯流组成，分叉与留白可辨，不使用细线或烟团。 */
function wind() {
  const edge=u=>[u*1.75,0,.50-.72*u*u];
  const parts=[['front',ribbon(edge,.24,.15),'wind']];
  for(const sign of [-1,1]) {
    const flow=u=>[u*1.50+sign*.18,sign*.08,-.18+.20*Math.sin(u*2.3+sign*.4)];
    parts.push([`flow_${sign}`,ribbon(flow,.16,.12),'wind']);
  }
  publish('wind_blade',parts,cores(edge),{span:3.5,layers:3,depth:.24});
}

/** 水刃为有水量的弯面，后缘翻卷成连续水唇；泡沫通道只改变表面。 */
function water() {
  const edge=u=>[u*1.80,0,.43-.58*u*u+.035*Math.sin(u*9)];
  const sheet=surfaceData(ribbon(edge,.35,.17),p=>.27+.08*Math.cos(p.x*2),p=>Math.max(0,.35-p.z)*.7);
  const lip=u=>[u*1.66,.08,.15-.56*u*u];
  publish('water_blade',[['sheet',sheet,'water'],['rolled_lip',surfaceData(ribbon(lip,.13,.20),.25,()=>.22),'water']],
    cores(edge,.12),{span:3.6,depth:.35,rolledLip:true});
}

/** 火刃有连续焰锋与五个独立收尖的厚焰舌，不以散乱粒子替代作用主体。 */
function fire() {
  const edge=u=>[u*1.72,0,.37-.42*u*u];
  const pieces=[['front',ribbon(edge,.28,.16),'flame']];
  for(let i=0;i<5;i++) {
    const x=(i-2)*.59,length=.65+.25*Math.cos(i*2),rings=[];
    for(const [t,r] of [[0,.22],[.28,.24],[.64,.14],[1,.008]])
      rings.push(Array.from({length:10},(_,j)=>{
        const a=j*Math.PI/5;return [x+Math.cos(a)*r+.10*Math.sin(t*4+i),-Math.sin(a)*r*.65,-.05-t*length];
      }));
    pieces.push([`tongue_${i}`,column(rings,10),'flame']);
  }
  publish('fire_blade',pieces,cores(edge),{span:3.44,tongues:5,depth:.28});
}

/** 阴刃偏斜成厚钩月，冷亮刃缘包住深色刃背，左右收尾明显不同。 */
function yin() {
  const edge=u=>[u*1.72-.10,0,.46-.73*u*u+.24*u*u*u];
  const back=u=>[u*1.63-.10,0,.20-.70*u*u+.24*u*u*u];
  publish('yin_blade',[['back',ribbon(back,.30,.18),'yin_depth'],['edge',ribbon(edge,.14,.13),'yin_moon']],
    cores(edge,.09).map(c=>[...c,1]),{span:3.44,profile:'asymmetric_hook',depth:.30});
}

/** 一次发布五种原刃波的独立主体。 */
export function makeBladeForms() {sword();wind();water();fire();yin();}
