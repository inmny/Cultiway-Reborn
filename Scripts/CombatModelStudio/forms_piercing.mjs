/** 贯穿主体共用封闭截面制作，分别保留针、晶枪、岩锥和木枪的实际结构。 */
import {write, structure, surfaceData} from './form_export.mjs';
import {wallPart, column} from './forms_solid_walls.mjs';

/** 沿制作前向Z连接完整截面；可独立声明厚薄、棱面及不规则岩质。 */
function loft(profile, sides, flat=1, rough=0) {
  const rings=profile.map(([z,r],row)=>Array.from({length:sides},(_,side)=>{
    const a=side*Math.PI*2/sides,shape=1+rough*Math.sin(side*2.13+row*.7);
    return [Math.cos(a)*r*shape,Math.sin(a)*r*flat*shape,z];
  }));
  return column(rings,sides);
}

/** 给每个来源保存自己的几何、实际接触芯及可改造的材料，不保存来源专用运行逻辑。 */
function publish(id, pieces, contacts, parameters) {
  const controls=[{Id:'root',Position:[0,0,0],Rotation:[0,0,0,1]}];
  const parts=pieces.map(([name,geometry,surface,liquid=false])=>wallPart(name,liquid?surfaceData(geometry,.24):geometry,surface,[0,0,0],'root'));
  const tips=parts.map(p=>Math.max(...p.Positions.filter((_,i)=>i%3===2))),last=Math.max(...tips),tipPart=tips.indexOf(last);
  const anchors=[{Id:'root',Part:0,Position:[0,0,0],Direction:[0,0,1],Control:'root'},
    {Id:'tip',Part:tipPart,Position:[0,0,last],Direction:[0,0,1],Control:'root'}];
  const shapes=contacts.map(([a,b,r,part=0],i)=>({Id:`core_${i}`,Part:part,A:[0,0,a],B:[0,0,b],Radius:r,AControl:'root',BControl:'root'}));
  write(id,parts,anchors,shapes,controls,[],false,{Forward:[0,0,1],Up:[0,1,0]},
    structure('rigid','forms_piercing.mjs','makePiercingForms',parameters,
      Object.fromEntries(parts.map(p=>[p.Id,['projectile.body','piercing.core']])),[]));
}

/** 一次发布各独立主体。 */
export function makePiercingForms() {
  const golden=[[-.04,.005],[.10,.075],[.32,.09],[1.45,.07],[1.88,.025],[2.12,.002]];
  publish('golden_needle',[['metal_needle',loft(golden,10),'gold']],[[.15,1.40,.055],[1.40,1.83,.025]],{golden,sides:10});

  const poison=[[-.04,.012],[.18,.08],[.48,.17],[.82,.18],[1.20,.11],[1.67,.045],[1.94,.002]];
  publish('poison_needle',[['venom_spindle',loft(poison,16),'poison_liquid',true]],
    [[.22,.40,.055],[.45,.90,.12],[.90,1.35,.065],[1.35,1.72,.025]],{poison,sides:16});

  const stone=[[-.06,.37],[.16,.48],[.58,.43],[1.10,.30],[1.52,.15],[1.96,.008]];
  publish('stone_cone',[['rock_core',loft(stone,7,1,.12),'stone']],
    [[.20,.52,.30],[.52,.96,.24],[.96,1.30,.14],[1.30,1.68,.055]],{stone,sides:7});

  const ice=[[-.03,.08],[.20,.15],[1.30,.18],[1.68,.27],[1.98,.23],[2.60,.002]];
  publish('ice_lance',[['prismatic_lance',loft(ice,6,.8),'ice']],
    [[.18,1.30,.10],[1.35,1.88,.14],[1.90,2.32,.045]],{ice,sides:6});

  const lightning=[[-.04,.045],[.18,.15],[.90,.13],[1.15,.30],[1.42,.18],[1.65,.36],[1.98,.23],[2.46,.003]];
  publish('lightning_spear',[['charged_lance',loft(lightning,6,.85),'lightning_dense']],
    [[.16,.85,.09],[.85,1.30,.10],[1.35,1.95,.13],[1.98,2.25,.045]],{lightning,sides:6});

  const shaft=[[-.04,.10],[.12,.14],[1.50,.115],[1.82,.10]];
  const head=[[1.40,.08],[1.65,.31],[1.96,.22],[2.44,.002]];
  publish('wood_spear',[['wood_shaft',loft(shaft,12,1,.045),'wood'],['leaf_point',loft(head,8,.40),'wood']],
    [[.13,1.45,.08],[1.58,1.92,.075,1],[1.96,2.26,.025,1]],{shaft,head,sides:12});
}
