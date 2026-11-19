/** 从霜缘收拢成厚冰面，真实碎件使用同一组闭合切面。 */
import * as T from 'three';
import {formation, forming, motion, part, skin, structure, surfaceData, write} from './form_export.mjs';
import {fractureCell} from './forms_changes.mjs';

/** 贴地薄边、倾斜的厚晶甲、可脱落的内部分片共用同一份断面。 */
export function makeIceShell() {
  // 裁切区域保持凸形；顶沿的不规则轮廓由所有切片共同使用的映射产生。
  const outline = [[-2.6,-.28],[2.62,-.28],[2.7,.41],[2.32,1.48],[1.72,2.44],
    [.08,2.50],[-1.5,2.55],[-2.25,1.81],[-2.61,1.09]];
  const seeds = [[-2.02,.37],[-.95,.27],[.10,.44],[1.34,.23],[2.11,.72],[-1.62,1.64],
    [-.44,1.85],[.62,1.45],[1.57,1.85]];
  const polygons = seeds.map(seed => fractureCell(outline,seed,seeds));
  const centre = polygon => polygon.reduce((sum,p) => sum.add(p),new T.Vector2()).multiplyScalar(1 / polygon.length);
  const depth = (x,z) => .22 + .22 * T.MathUtils.smoothstep(z,.04,.94) * (1 - .25 * Math.abs(x) / 2.7);
  const point = (x,z,layer=.5) => {
    const upper = T.MathUtils.smoothstep(z,1.32,2.48);
    const crown = upper * (-.112 + .118*Math.sin(x*4.4) + .05*Math.cos(x*9));
    const lean = .46*z + .145*x*x + .036*Math.sin(x*3.3 + z*2);
    return [x,lean + (layer-.5)*depth(x,z),z+crown];
  };
  const parts=[],controls=[],anchors=[],contacts=[],connections=[];
  for (const [index,polygon] of polygons.entries()) {
    const c=centre(polygon), id=`ice_plate_${index}`, pivot=point(c.x,c.y);
    controls.push({Id:id,Position:pivot,Rotation:[0,0,0,1]});
    const geometry=new T.ExtrudeGeometry(new T.Shape(polygon),{depth:1,bevelEnabled:false,steps:1,curveSegments:1});
    const p=geometry.getAttribute('position');
    for(let j=0;j<p.count;j++)p.setXYZ(j,...point(p.getX(j),p.getY(j),1-p.getZ(j)));
    surfaceData(geometry, p=>depth(p.x,p.z), p=>
      .75*(1-T.MathUtils.smoothstep(p.z,.00,.56))+.18*T.MathUtils.smoothstep(Math.abs(p.x),1.55,2.70));
    geometry.userData.smoothSeams=false;
    formation(geometry,p=> T.MathUtils.smoothstep(p.z,-.28,2.5)*.82 + .14*(1-Math.abs(p.x)/2.7),
      p=>new T.Vector3(p.x,point(p.x,p.z)[1],p.z));
    parts.push(part(id,skin(geometry,()=>[[index,1]]),'ice',pivot));
    anchors.push({Id:`plate_${index}`,Part:index,Position:point(c.x,c.y,1),Direction:[0,1,0],Control:id});

    // 水平短段只走封闭冰片的内部，不越过松边和尖冠。
    const lo=Math.min(...polygon.map(p=>p.y)), hi=Math.max(...polygon.map(p=>p.y));
    for(let z=lo+.14,row=0;z<hi-.10;z+=.20) {
      const cuts=[];
      polygon.forEach((a,i)=>{const b=polygon[(i+1)%polygon.length];
        if((a.y<=z&&b.y>z)||(b.y<=z&&a.y>z))cuts.push(T.MathUtils.lerp(a.x,b.x,(z-a.y)/(b.y-a.y)));
      });
      if(cuts.length!==2)continue;
      const left=Math.min(...cuts)+.085,right=Math.max(...cuts)-.085;
      if(right<=left)continue;
      const count=Math.ceil((right-left)/.42);
      for(let i=0;i<count;i++) {
        const a=T.MathUtils.lerp(left,right,i/count), b=T.MathUtils.lerp(left,right,(i+1)/count);
        const radius=Math.min(.128,depth((a+b)/2,z)*.47);
        contacts.push({Id:`${id}_${row}_${i}`,Part:index,A:point(a,z),B:point(b,z),Radius:radius,AControl:id,BControl:id});
      }
      row++;
    }
    polygons.slice(0,index).forEach((other,parent)=>{
      const common=polygon.filter(p=>other.some(q=>p.distanceTo(q)<.00001));
      if(common.length!==2)return;
      const at=common[0].clone().add(common[1]).multiplyScalar(.5);
      connections.push([`ice_plate_${parent}`,id,point(at.x,at.y)]);
    });
  }
  const joined=.84;
  const grow=forming(motion('join',joined,false,controls,32,(i,p,control)=>{
    const t=1-T.MathUtils.smoothstep(p,.18,1);
    const [x,y,z]=control.Position;
    const side=Math.sign(seeds[i][0]);
    const turn=side*(.20+.05*Math.sin(i));
    return {position:[x+side*t*(.45+.32*(1-Math.abs(x)/2.7)),y+t*(.12+.3*z),z-t*.17*z],
      rotation:new T.Quaternion().setFromAxisAngle(new T.Vector3(0,0,1),turn*t).toArray()};
  },[{Id:'frost',At:.14},{Id:'joined',At:joined}]),p => T.MathUtils.smoothstep(p,.015,1));
  write('ice_shell',parts,anchors,contacts,controls,[grow],false,
    {Forward:[0,1,0],Up:[0,0,1],HideBelowGround:true},
    structure('articulated','forms_ice_shell.mjs','makeIceShell',{outline,seeds,contactStep:.2,joinTime:joined},
      Object.fromEntries(parts.map(p=>[p.Id,['ice.shell','barrier.face','fracture.piece']])),connections));
}
