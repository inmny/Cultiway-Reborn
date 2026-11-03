/** 刚性兵器的真实结构、握点和分段表面，所有坐标由显示与接触共同读取。 */
import * as T from 'three';
import {part, write, structure} from './form_export.mjs';

/** 沿正纵轴制作有脊线与尖端的四棱尖叶，不用椭圆代替刃面。 */
function leaf(sections) {
  const vertices=[], uv=[], indices=[];
  for(let side=0;side<4;side++) {
    for(const [z,w,d] of sections) {
      const ring=[[-w,0],[0,-d],[w,0],[0,d]];
      for(const corner of [side,(side+1)%4]) {vertices.push(...ring[corner],z);uv.push(corner/4,z);}
    }
    const offset=side*sections.length*2;
    for(let i=0;i<sections.length-1;i++) {const a=offset+i*2;indices.push(a,a+1,a+3,a,a+3,a+2);}
  }
  const geometry=new T.BufferGeometry();
  geometry.setAttribute('position',new T.Float32BufferAttribute(vertices,3));
  geometry.setAttribute('uv',new T.Float32BufferAttribute(uv,2));geometry.setIndex(indices);
  return geometry;
}

/** 圆柄沿正纵轴，保留各自长度和所在高度。 */
function shaft(radius,length,z) {return new T.CylinderGeometry(radius,radius,length,16,8).rotateX(Math.PI/2).translate(0,0,z);}

/** 锤头采用带圆角的平面轮廓和真实倒角，锤面保持可辨的平直区域。 */
function hammerHead() {
  const w=.575,h=.29,r=.065, shape=new T.Shape();
  shape.moveTo(-w+r,-h);shape.lineTo(w-r,-h);shape.quadraticCurveTo(w,-h,w,-h+r);
  shape.lineTo(w,h-r);shape.quadraticCurveTo(w,h,w-r,h);shape.lineTo(-w+r,h);
  shape.quadraticCurveTo(-w,h,-w,h-r);shape.lineTo(-w,-h+r);shape.quadraticCurveTo(-w,-h,-w+r,-h);
  return new T.ExtrudeGeometry(shape,{depth:.40,steps:1,bevelEnabled:true,bevelSegments:3,
    bevelSize:.025,bevelThickness:.025,curveSegments:4}).translate(0,0,-.20).rotateX(Math.PI/2).translate(0,0,1.72);
}

/** 盾面前后分别成形；边缘与背面闭合，前面保留连续曲率。 */
function shieldPlate() {
  const p=[],uv=[],indices=[],rings=12,sides=48;
  for(const front of [true,false]) for(let ring=0;ring<=rings;ring++) for(let i=0;i<=sides;i++) {
    const r=ring/rings,angle=i/sides*Math.PI*2,x=Math.cos(angle)*r*.88,y=Math.sin(angle)*r*1.16;
    p.push(x,y,(front?.14:-.06)*(1-.55*r*r));uv.push(x/1.76+.5,y/2.32+.5);
  }
  const count=(rings+1)*(sides+1);
  for(let face=0;face<2;face++) for(let r=0;r<rings;r++) for(let i=0;i<sides;i++) {
    const a=face*count+r*(sides+1)+i,b=a+sides+1;
    indices.push(...(face?[a,a+1,b+1,a,b+1,b]:[a,b+1,a+1,a,b,b+1]));
  }
  for(let i=0;i<sides;i++) {const a=rings*(sides+1)+i,b=a+count;indices.push(a,b,a+1,a+1,b,b+1);}
  const geometry=new T.BufferGeometry();geometry.setAttribute('position',new T.Float32BufferAttribute(p,3));
  geometry.setAttribute('uv',new T.Float32BufferAttribute(uv,2));geometry.setIndex(indices);
  geometry.userData.smoothSeams=true;return geometry;
}

/** 只输出资源及其实际结构；取得装备、格挡和传导仍由相应玩法负责。 */
export function makeWeaponForms() {
  const spearParts=[part('shaft',shaft(.055,4.35,.575),'wood'),
    part('socket',new T.CylinderGeometry(.09,.07,.40,16,3).rotateX(Math.PI/2).translate(0,0,2.95),'metal'),
    part('point',leaf([[3.10,.08,.035],[3.28,.19,.045],[3.58,.15,.035],[4.15,.002,.002]]),'metal'),
    part('butt',new T.ConeGeometry(.075,.24,16).rotateX(-Math.PI/2).translate(0,0,-1.69),'metal')];
  const spearAnchors=[{Id:'grip',Part:0,Position:[0,0,0],Direction:[0,0,1]},
    {Id:'forward',Part:0,Position:[0,0,.8],Direction:[0,0,1]},
    {Id:'tip',Part:2,Position:[0,0,4.15],Direction:[0,0,1]},
    {Id:'shaft',Part:0,Position:[0,0,1.2],Direction:[1,0,0]}];
  write('spear',spearParts,spearAnchors,
    [{Id:'tip',Part:2,A:[0,0,4.04],B:[0,0,4.13],Radius:.018},
      {Id:'shaft',Part:0,A:[0,0,-1.54],B:[0,0,2.69],Radius:.055}],[],[],false,null,
    structure('rigid','forms_weapons.mjs','spear',{length:5.96,shaftRadius:.055},
      {shaft:['weapon.shaft','weapon.grip'],socket:['weapon.socket'],point:['weapon.tip','blade.edge'],butt:['weapon.pommel']},
      [['shaft','socket',[0,0,2.75]],['socket','point',[0,0,3.10]],['shaft','butt',[0,0,-1.60]]]),
    [{Id:'grip',Anchor:'grip',Up:[0,1,0],Diameter:.11,Scale:[.2,.6]},
      {Id:'forward',Anchor:'forward',Up:[0,1,0],Diameter:.11,Scale:[.2,.6]}],[],{Groups:['spear'],Scale:.42});

  const hammerParts=[part('handle',shaft(.06,2.4,.48),'wood'),part('head',hammerHead(),'metal'),
    part('collar',new T.TorusGeometry(.085,.025,8,20).translate(0,0,1.35),'metal'),
    part('pommel',new T.SphereGeometry(.095,16,8).scale(1,1,1.35).translate(0,0,-.71),'metal')];
  const hammerContacts=[];
  for(const side of [-1,1]) for(let row=-3;row<=3;row++) hammerContacts.push({Id:`face/${side>0?'right':'left'}/${row+3}`,
    Part:1,A:[side*.56,row*.06,1.49],B:[side*.56,row*.06,1.95],Radius:.035});
  hammerContacts.push({Id:'shaft',Part:0,A:[0,0,-.65],B:[0,0,1.30],Radius:.06});
  write('warhammer',hammerParts,[{Id:'grip',Part:0,Position:[0,0,-.35],Direction:[0,0,1]},
    {Id:'short',Part:0,Position:[0,0,.65],Direction:[0,0,1]},
    {Id:'face/right',Part:1,Position:[.575,0,1.72],Direction:[1,0,0]},
    {Id:'face/left',Part:1,Position:[-.575,0,1.72],Direction:[-1,0,0]},
    {Id:'face',Part:1,Position:[.575,0,1.72],Direction:[1,0,0]}],hammerContacts,[],[],false,
    {Forward:[1,0,0],Up:[0,0,1]},
    structure('rigid','forms_weapons.mjs','warhammer',{head:[1.2,.45,.63],handle:[.12,2.4]},
      {handle:['weapon.shaft','weapon.grip'],head:['weapon.hammer.face'],collar:['weapon.socket'],pommel:['weapon.pommel']},
      [['handle','head',[0,0,1.4]],['handle','collar',[0,0,1.35]],['handle','pommel',[0,0,-.6]]]),
    [{Id:'grip',Anchor:'grip',Up:[0,1,0],Diameter:.12,Scale:[.2,.6]},
      {Id:'short',Anchor:'short',Up:[0,1,0],Diameter:.12,Scale:[.2,.6]}],[],{Groups:['hammer'],Scale:.42});

  const rim=new T.TorusGeometry(1,.045,8,48).scale(.88,1.16,1).translate(0,0,.05);
  const boss=new T.SphereGeometry(.19,24,12,0,Math.PI*2,0,Math.PI/2).rotateX(Math.PI/2).translate(0,0,.12);
  const shieldParts=[part('plate',shieldPlate(),'metal'),part('rim',rim,'metal'),part('boss',boss,'metal'),
    part('grip',shaft(.055,.50,0).rotateY(Math.PI/2).translate(0,0,-.2),'leather')];
  const face=[];
  for(let row=-7;row<=7;row++) {
    const y=row*.145,xmax=.88*Math.sqrt(1-y*y/(1.16*1.16));
    const front=x=>.14*(1-.55*(x*x/(.88*.88)+y*y/(1.16*1.16)));
    for(let section=0;section<4;section++) {
      const x0=-xmax+2*xmax*section/4,x1=-xmax+2*xmax*(section+1)/4;
      face.push({Id:`face/${row+7}/${section}`,Part:0,A:[x0,y,front(x0)],B:[x1,y,front(x1)],Radius:.075});
    }
  }
  face.push({Id:'face/boss',Part:2,A:[0,0,.20],B:[0,0,.20],Radius:.11});
  for(let i=0;i<24;i++) {const point=t=>[Math.cos(t)*.88,Math.sin(t)*1.16,.05];
    face.push({Id:`rim/${i}`,Part:1,A:point(i/24*Math.PI*2),B:point((i+1)/24*Math.PI*2),Radius:.045});}
  write('shield',shieldParts,[{Id:'grip',Part:3,Position:[0,0,-.2],Direction:[1,0,0]},
    {Id:'face',Part:0,Position:[0,0,.14],Direction:[0,0,1]},
    {Id:'rim',Part:1,Position:[.88,0,.05],Direction:[1,0,0]}],face,[],[],false,null,
    structure('rigid','forms_weapons.mjs','shield',{size:[1.85,2.43,.40],faceRows:15,faceSections:4},
      {plate:['weapon.shield.face'],rim:['weapon.shield.rim'],boss:['weapon.shield.boss'],grip:['weapon.grip']},
      [['plate','rim',[0,0,.05]],['plate','boss',[0,0,.12]],['plate','grip',[0,0,-.2]]]),
    [{Id:'grip',Anchor:'grip',Up:[0,1,0],Diameter:.11,Scale:[.2,.6]}],[],{Groups:['shield'],Scale:.42});
}
