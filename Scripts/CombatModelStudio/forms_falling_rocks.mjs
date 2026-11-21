/** 三种落石砸到实际地面后，由同一组岩层碎片表现作用范围。 */
import * as T from 'three';
import {dissolve, motion, round, structure, write} from './form_export.mjs';
import {wallPart} from './forms_solid_walls.mjs';

const origin=[0,0,0], identity=[0,0,0,1];
const root={Id:'root',Position:origin,Rotation:identity};
const anchors=[{Id:'root',Part:0,Position:origin,Direction:[0,0,1],Control:'root'}];
const facing={Forward:[0,0,1],Up:[0,1,0],HideBelowGround:true};

/** 岩体的最低点为制作原点，附属层都位于它上方。 */
function fallingRock(id, pieces, radius, ends=false) {
  const parts=pieces.map(([name,geometry,surface])=>wallPart(name,geometry,surface,origin,'root'));
  const contact={Id:'core_0',Part:0,A:[0,0,-radius],B:[0,0,-radius*1.7],Radius:radius,
    AControl:'root',BControl:'root'};
  const clips=ends?[dissolve([root],(_index,t)=>({position:[0,0,.16*t]}))]:[];
  write(id,parts,anchors,[contact],[root],clips,false,facing,structure('rigid','forms_falling_rocks.mjs',
    'makeFallingRockForms',{radius},Object.fromEntries(parts.map(part=>[part.Id,['projectile.body']]))));
}

/** 压碎后的厚岩板留在中心，小碎石从断层两侧错时抛落。 */
function impact() {
  const duration=.22, ease=T.MathUtils.smoothstep;
  const fragments=[
    {x:-.25,y:-.04,radius:.31,depth:.24,drift:.14,loft:.095,turn:.92},
    {x:.15,y:.29,radius:.26,depth:.28,drift:.11,loft:.075,turn:-.68},
    {x:.26,y:-.19,radius:.20,depth:.36,drift:.14,loft:.12,turn:.21},
    {x:-.42,y:.28,radius:.12,depth:.43,drift:.19,loft:.18,turn:1.44},
    {x:-.57,y:.07,radius:.079,depth:.58,drift:.20,loft:.24,turn:.66},
    {x:.55,y:-.18,radius:.092,depth:.47,drift:.19,loft:.22,turn:-.45},
    {x:.28,y:-.43,radius:.115,depth:.58,drift:.22,loft:.16,turn:1.13},
    {x:.14,y:-.62,radius:.067,depth:.60,drift:.13,loft:.25,turn:-1.51},
    {x:.37,y:.42,radius:.078,depth:.47,drift:.16,loft:.14,turn:.78},
  ];
  const controls=[root,...fragments.map(({x,y},i)=>({Id:`fragment_${i}`,Parent:'root',Position:[x,y,-.025],Rotation:identity}))];
  const base=new T.DodecahedronGeometry(.40).scale(1.30,.74,.26).rotateZ(-.39).translate(-.04,.02,-.069);
  const parts=[wallPart('crushed_core',base,'stone_strata',origin,'root')];
  for(const [i,fragment] of fragments.entries()) {
    const {Id:id,Position:pivot}=controls[i+1];
    const plate=new T.DodecahedronGeometry(fragment.radius).scale(1.18,.87,fragment.depth)
      .rotateX(i%2===0?.19:-.12).rotateZ(fragment.turn).translate(...pivot);
    parts.push(wallPart(id,plate,i%3===0?'stone_ridge':i%3===1?'stone':'stone_strata',pivot,id));
  }
  const clip=motion('spread',duration,false,controls,24,(index,phase,control)=>{
    if(index===0)return {};
    const fragment=fragments[index-1], delay=(index%4)*.005;
    const axis=new T.Vector3(fragment.x+.12,fragment.y-.08,0).normalize();
    const at=phase*duration, lift=ease(at,delay,.042+delay), settle=ease(at,.055+delay,.149+delay);
    const position=new T.Vector3(...control.Position).addScaledVector(axis,fragment.drift*(ease(at,delay,.103+delay)-.35));
    position.z-=fragment.loft*(lift-settle)-.026*settle;
    const tilt=new T.Quaternion().setFromAxisAngle(new T.Vector3(-axis.y,axis.x,.2).normalize(),fragment.turn*(.38*lift-.30*settle));
    return {position:position.toArray(),rotation:tilt.toArray()};
  });
  for(const key of clip.Keys)key.Fade=round(.23*(1-ease(key.At,0,.026))+ease(key.At,.089,duration));
  const contact={Id:'core_0',Part:0,A:origin,B:origin,Radius:.9,AControl:'root',BControl:'root'};
  write('falling_rock_impact',parts,anchors,[contact],controls,[clip],false,facing,structure('articulated',
    'forms_falling_rocks.mjs','makeFallingRockForms',{contactRadius:.9,duration},
    Object.fromEntries(parts.map(part=>[part.Id,['impact.volume']]))));
}

export function makeFallingRockForms() {
  fallingRock('falling_rock_0',[
    ['body',new T.DodecahedronGeometry(.67).scale(.87,.79,1).rotateY(.24).translate(0,0,-.69),'stone'],
    ['lower_layer',new T.IcosahedronGeometry(.45).scale(1.22,.89,.40).rotateZ(.47).translate(-.1,-.1,-.44),'stone_ridge'],
    ['upper_layer',new T.DodecahedronGeometry(.43).scale(1.20,1.04,.55).rotateZ(-.38).translate(.11,.15,-.99),'stone_strata']
  ],.46,true);
  fallingRock('falling_rock_1',[
    ['dark_face',new T.DodecahedronGeometry(.5).scale(.88,.64,1.0).rotateX(-.41).rotateZ(.74).translate(0,0,-.51),'stone_strata'],
    ['split_face',new T.IcosahedronGeometry(.29).scale(.72,.58,1.07).rotateY(.49).translate(.19,.10,-.48),'stone']
  ],.30);
  fallingRock('falling_rock_2',[
    ['body',new T.IcosahedronGeometry(.57).scale(.86,.82,1.0).rotateY(.37).translate(0,0,-.58),'stone'],
    ['outer_left',new T.DodecahedronGeometry(.33).scale(.85,.66,1.24).rotateX(.36).translate(-.35,.11,-.73),'stone_strata'],
    ['outer_right',new T.IcosahedronGeometry(.38).scale(.76,.85,1.13).rotateZ(.78).translate(.41,-.07,-.62),'stone_ridge'],
    ['outer_cap',new T.DodecahedronGeometry(.28).scale(.89,.93,1.20).rotateY(.46).translate(-.06,-.37,-.91),'stone']
  ],.41);
  impact();
}
