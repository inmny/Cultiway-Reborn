/** 贯穿主体共用封闭截面制作，分别保留针、晶枪、岩锥和木枪的实际结构。 */
import {write, structure, surfaceData, dissolve} from './form_export.mjs';
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
function publish(id, pieces, contacts, parameters, ending=false) {
  const controls=[{Id:'root',Position:[0,0,0],Rotation:[0,0,0,1]}];
  const parts=pieces.map(([name,geometry,surface,liquid=false])=>wallPart(name,liquid?surfaceData(geometry,.24):geometry,surface,[0,0,0],'root'));
  const tips=parts.map(p=>Math.max(...p.Positions.filter((_,i)=>i%3===2))),last=Math.max(...tips),tipPart=tips.indexOf(last);
  const anchors=[{Id:'root',Part:0,Position:[0,0,0],Direction:[0,0,1],Control:'root'},
    {Id:'tip',Part:tipPart,Position:[0,0,last],Direction:[0,0,1],Control:'root'}];
  const shapes=contacts.map(([a,b,r,part=0],i)=>({Id:`core_${i}`,Part:part,A:[0,0,a],B:[0,0,b],Radius:r,AControl:'root',BControl:'root'}));
  const motions=ending?[dissolve(controls,(_index,t)=>({position:[0,-.13*t,-.05*t]}))]:[];
  write(id,parts,anchors,shapes,controls,motions,false,{Forward:[0,0,1],Up:[0,1,0]},
    structure('rigid','forms_piercing.mjs','makePiercingForms',parameters,
      Object.fromEntries(parts.map(p=>[p.Id,['projectile.body','piercing.core']])),[]));
}

/** 一次发布各独立主体。 */
export function makePiercingForms() {
  const golden=[[-.04,.005],[.10,.075],[.32,.09],[1.45,.07],[1.88,.025],[2.12,.002]];
  const weight=[[-.32,.006],[-.28,.062],[-.17,.11],[-.06,.087],[.14,.068]];
  const collars=[[.15,.069],[.18,.105],[.24,.105],[.28,.079]];
  publish('golden_needle',[
    ['metal_needle',loft(golden,10),'gold'],
    ['counterweight',loft(weight,12),'gold'],
    ['collar',loft(collars,10,.9),'gold']
  ],[[.15,1.40,.055],[1.40,1.83,.025]],{golden,weight,sides:10},true);

  const poison=[[-.04,.012],[.18,.08],[.48,.17],[.82,.18],[1.20,.11],[1.67,.045],[1.94,.002]];
  publish('poison_needle',[['venom_spindle',loft(poison,16),'poison_liquid',true]],
    [[.22,.40,.055],[.45,.90,.12],[.90,1.35,.065],[1.35,1.72,.025]],{poison,sides:16});

  const stone=[[-.06,.37],[.16,.48],[.58,.43],[1.10,.30],[1.52,.15],[1.96,.008]];
  const shoulder=loft([[.04,.056],[.26,.25],[.56,.21],[.88,.11],[1.26,.01]],5,.58,.16)
    .rotateY(-.35).translate(.27,.05,.027);
  const split=loft([[.08,.025],[.30,.14],[.66,.16],[1.04,.075],[1.38,.01]],5,.59,.13)
    .rotateY(-.27).translate(.22,0,.02).rotateZ(2.47);
  publish('stone_cone',[
    ['rock_core',loft(stone,7,1,.12),'stone'],['shoulder',shoulder,'stone_strata'],['split',split,'stone_ridge']
  ],[[.20,.52,.30],[.52,.96,.24],[.96,1.30,.14],[1.30,1.68,.055]],{stone,sides:7},true);

  const ice=[[-.03,.08],[.20,.15],[1.30,.18],[1.68,.27],[1.98,.23],[2.60,.002]];
  const ridge=loft([[.12,.016],[.41,.061],[1.20,.077],[1.72,.073],[2.16,.062],[2.46,.004]],5,.82)
    .translate(.089,0,0);
  publish('ice_lance',[
    ['prismatic_lance',loft(ice,6,.8),'ice_core'],
    ['white_ridge',ridge,'ice_rime'],
    ['ice_ridge',ridge.clone().scale(.82,.9,.94).rotateZ(2.64),'ice']
  ],[[.18,1.30,.10],[1.35,1.88,.14],[1.90,2.32,.045]],{ice,sides:6},true);

  const lightning=[[-.04,.045],[.18,.15],[.90,.13],[1.15,.30],[1.42,.18],[1.65,.36],[1.98,.23],[2.46,.003]];
  const lightningSpine=[[-.11,.012],[.11,.11],[.42,.14],[.93,.12],[1.24,.145],[1.71,.19],[2.24,.025]];
  const fin=loft(lightningSpine,6,.42).translate(.044,0,0);
  publish('lightning_spear',[
    ['charged_lance',loft(lightning,8,.85),'thunder_body'],
    ['bright_spine',fin,'thunder_arc'],
    ['return_spine',fin.clone().rotateZ(Math.PI),'thunder_arc_fork']
  ],[[.16,.85,.09],[.85,1.30,.10],[1.35,1.95,.13],[1.98,2.25,.045]],{lightning,lightningSpine,sides:8},true);

  const shaft=[[-.04,.10],[.12,.14],[1.50,.115],[1.82,.10]];
  const head=[[1.40,.08],[1.65,.31],[1.96,.22],[2.44,.002]];
  const bind=loft([[1.36,.09],[1.43,.145],[1.53,.164],[1.61,.19]],9,.76,.11);
  const leaf=loft([[0,.019],[.10,.059],[.21,.044],[.28,.002]],6,.38).rotateY(1.28).translate(.06,0,1.44);
  publish('wood_spear',[
    ['wood_shaft',loft(shaft,12,1,.045),'wood'],['leaf_point',loft(head,8,.40),'wood_heartwood'],
    ['growth_collar',bind,'wood_bark'],['fresh_leaf',leaf,'wood_leaf']
  ],[[.13,1.45,.08],[1.58,1.92,.075,1],[1.96,2.26,.025,1]],{shaft,head,sides:12},true);
}
