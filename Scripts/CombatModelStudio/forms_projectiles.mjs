/** 普通投射各自制作完整主体：灵核、旋风、岩块、阴月、阳瓣和冰片。 */
import * as T from 'three';
import {write, structure, dissolve} from './form_export.mjs';
import {wallPart, column} from './forms_solid_walls.mjs';

/** 生成各自的表面、实际核心和共同控制，所有运动继续由普通投射构造提供。 */
function publish(id, pieces, contacts, parameters, ending=false) {
  const controls=[{Id:'root',Position:[0,0,0],Rotation:[0,0,0,1]}];
  const parts=pieces.map(([name,geometry,surface])=>wallPart(name,geometry,surface,[0,0,0],'root'));
  const shapes=contacts.map(([a,b,r,part=0],i)=>({Id:`core_${i}`,Part:part,A:a,B:b,Radius:r,AControl:'root',BControl:'root'}));
  const anchors=[{Id:'root',Part:0,Position:[0,0,0],Direction:[0,0,1],Control:'root'}];
  const motions=ending?[dissolve(controls,(_index,t)=>({
    position:[0,.12*t,-.07*t],
    rotation:new T.Quaternion().setFromAxisAngle(new T.Vector3(0,0,1),.41*t).toArray()
  }))]:[];
  write(id,parts,anchors,shapes,controls,motions,false,{Forward:[0,0,1],Up:[0,1,0]},
    structure('rigid','forms_projectiles.mjs','makeProjectileForms',parameters,
      Object.fromEntries(parts.map(p=>[p.Id,['projectile.body','projectile.core']])),[]));
}

/** 灵弹以实心玉珠和四片包覆厚瓣构成，接触芯位于可见珠体内。 */
function spirit() {
  const parts=[['pearl',new T.SphereGeometry(.36,24,16),'spirit_core']];
  for(let i=0;i<4;i++) {
    const a=i*Math.PI/2,g=new T.SphereGeometry(.26,16,12).scale(.48,.76,1.55);
    g.translate(0,.24,-.12).rotateZ(a);parts.push([`lobe_${i}`,g,'spirit_pearl']);
  }
  publish('spirit_bolt',parts,[[[0,0,0],[0,0,0],.31]],{radius:.36,lobes:4});
}

/** 风弹有卷入的厚风核和紧实的团流两种主体。 */
function wind() {
  const pieces=[['heart',new T.SphereGeometry(.29,22,16).scale(.92,.92,1.52),'wind_pressure']];
  for(let i=0;i<2;i++) {
    const rings=Array.from({length:37},(_,j)=>{
      const t=j/36,a=i*Math.PI+t*Math.PI*2.24;
      const radius=.25+.055*Math.sin(Math.PI*t);
      const width=.009+.102*Math.pow(Math.sin(Math.PI*t),.65);
      return Array.from({length:10},(_,side)=>{
        const theta=side*Math.PI*2/10,r=radius+width*Math.cos(theta);
        return [r*Math.cos(a),r*Math.sin(a),-.56+t*.87+.102*Math.sin(theta)*Math.sin(t*Math.PI)];
      });
    });
    pieces.push([`flow_${i}`,column(rings,10),'wind_open']);
  }
  publish('wind_bullet',pieces,[[[0,0,-.12],[0,0,.12],.21]],{radius:.405,rolls:1.12},true);

  const core=new T.SphereGeometry(.385,24,16),v=core.getAttribute('position');
  for(let i=0;i<v.count;i++) {
    const x=v.getX(i),y=v.getY(i),z=v.getZ(i),pressure=1+.115*Math.cos(11*x-7*y+6*z);
    v.setXYZ(i,x*pressure,y*pressure*.91,z*pressure*1.12);
  }
  const rolls=[['heart',core,'wind_pressure']];
  for(let i=0;i<3;i++)
    rolls.push([`fold_${i}`,new T.SphereGeometry(.35,20,14).scale(.49,.6,1)
      .translate(.24,0,-.07).rotateZ(i*2*Math.PI/3),'wind_open']);
  publish('wind_bullet_1',rolls,[[[0,0,-.12],[0,0,.15],.27]],{radius:.415,folds:3});
}

/** 飞石保持不规则分面与厚重体积，外轮廓不做成另一根岩锥。 */
function rock() {
  const g=new T.DodecahedronGeometry(.52).scale(1.06,.82,1.14).rotateZ(.29).rotateY(.33);
  const shoulder=new T.IcosahedronGeometry(.31).scale(1.14,.54,1.27).rotateZ(.24).translate(.07,.27,-.045);
  const underside=new T.DodecahedronGeometry(.28).scale(.81,.66,1.45).rotateY(-.48).translate(-.16,-.24,.09);
  publish('rock_projectile',[
    ['boulder',g,'stone'],['shoulder',shoulder,'stone_ridge'],['underside',underside,'stone_strata']
  ],[[[0,0,-.08],[0,0,.08],.34]],{radius:.52,slabs:3},true);
}

/** 阴弹以深色实心内核与偏置月缘区分轮廓；月缘两端由厚圆头封闭。 */
function yin() {
  const radius=.43,tube=.10,arc=Math.PI*1.45;
  const pieces=[['heart',new T.SphereGeometry(.32,24,16),'yin_depth'],
    ['moon',new T.TorusGeometry(radius,tube,10,40,arc).rotateZ(-arc/2).rotateX(Math.PI/2).translate(.08,0,0),'yin_moon']];
  for(const sign of [-1,1])pieces.push([`tip_${sign}`,new T.SphereGeometry(tube,12,8).translate(.08+radius*Math.cos(arc/2),0,sign*radius*Math.sin(arc/2)),'yin_moon']);
  publish('yin_bolt',pieces,[[[0,0,0],[0,0,0],.28]],{radius,arc});
}

/** 阳弹由中心光核与六片宽厚日瓣围合，不以细线放射或火焰烟团充当主体。 */
function yang() {
  const pieces=[['heart',new T.SphereGeometry(.32,24,16),'aegis_solar']];
  for(let i=0;i<6;i++) {
    const a=i*Math.PI/3,g=new T.SphereGeometry(.24,16,12).scale(.52,1.13,.60).translate(0,.29,0).rotateZ(a).rotateX(Math.PI/2);
    pieces.push([`petal_${i}`,g,'solar_light']);
  }
  publish('yang_bolt',pieces,[[[0,0,0],[0,0,0],.28]],{petals:6,radius:.56});
}

/** 冰片为偏斜三棱晶体，有宽中段和不等长尖端，与六棱长冰枪保留明显区别。 */
function ice() {
  const profile=[[-.48,.04,-.10],[-.18,.34,0],[.27,.31,.07],[.94,.004,.17]];
  const rings=profile.map(([z,r,shift])=>Array.from({length:3},(_,i)=>{
    const a=i*Math.PI*2/3;return [Math.cos(a)*r+shift,Math.sin(a)*r*.72,z];
  }));
  const enamel=column([
    [[-.09,-.06,-.35],[.04,-.15,-.35],[.12,-.06,-.35]],
    [[-.18,-.11,-.12],[.10,-.23,-.12],[.20,-.065,-.12]],
    [[-.13,-.09,.27],[.16,-.20,.27],[.22,-.05,.27]],
    [[.10,-.035,.74],[.16,-.08,.74],[.17,-.026,.74]]
  ],3);
  publish('ice_shard',[['shard',column(rings,3),'ice_core'],['ice_edge',enamel,'ice_rime']],
    [[[0,0,-.12],[.05,0,.22],.10],[[.08,0,.25],[.13,0,.62],.04]],{profile,sides:3},true);
}

/** 一次发布本组独立普通投射。 */
export function makeProjectileForms() { spirit();wind();rock();yin();yang();ice(); }
