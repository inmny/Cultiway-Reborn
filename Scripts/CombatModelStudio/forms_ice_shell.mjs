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

/** 从同一种厚冰和成形资料制作合围：每片的可见厚度、骨架和作用面共同倾转。 */
export function makeFrostFlower() {
  const count=8, radius=2.7, duration=.98, baseHeight=.06;
  const rows=[.42,.89,1.37];
  const parts=[],controls=[],contacts=[],anchors=[],connections=[],directions=[];
  const apothem=z=>radius-.36*z-.08*z*z;
  const depth=(x,z)=>.47+.14*T.MathUtils.smoothstep(z,.25,.9)-.08*Math.abs(x);
  const ringWidth=z=>apothem(z)*Math.tan(Math.PI/count);
  for(let i=0;i<count;i++) {
    const angle=2*Math.PI*i/count, out=new T.Vector3(Math.cos(angle),Math.sin(angle),0);
    const across=new T.Vector3(-out.y,out.x,0), pivot=out.clone().multiplyScalar(radius);
    pivot.z=baseHeight;
    const id=`petal_${i}`, lean=(i%2===0?1:-1)*.23, top=2.55+.16*Math.cos(i*2.62);
    const outline=[[-.92,.02],[.88,.02],[ringWidth(.42)+.31,.42],[ringWidth(.89)+.29,.89],
      [ringWidth(1.37)+.26,1.37],[.76,1.83],[lean+.42,2.23],[lean,top],[-.40+lean,2.16],
      [-.78,1.78],[-ringWidth(1.37)-.26,1.37],[-ringWidth(.89)-.29,.89],[-ringWidth(.42)-.31,.42]];
    const point=(x,z,layer=.5)=>{
      // 腰部连续、顶端变薄并向心合拢；外层保留同一原材质的微弱晶面起伏。
      const edge=.025*Math.cos(x*5+i*.7)*T.MathUtils.smoothstep(z,1.4,top);
      const r=apothem(z)+edge+(layer-.5)*depth(x,z)*(1-.72*T.MathUtils.smoothstep(z,1.9,top));
      const p=out.clone().multiplyScalar(r).addScaledVector(across,x);
      p.z=z+baseHeight;
      return p;
    };
    controls.push({Id:id,Position:pivot.toArray(),Rotation:[0,0,0,1]});
    directions.push({out,across});
    // 裂缝共用同一条切边：完整时严丝合缝，脱落后是真正封闭的两块冰。
    const cut=(x,z)=>z-1.59-.18*x*(i%2===0?1:-1);
    const section=side=>outline.flatMap((a,j)=>{
      const b=outline[(j+1)%outline.length], lo=side*cut(...a), hi=side*cut(...b);
      const vertices=lo>=0?[new T.Vector2(...a)]:[];
      if ((lo>=0)!==(hi>=0)) vertices.push(new T.Vector2(...a).lerp(new T.Vector2(...b),lo/(lo-hi)));
      return vertices;
    });
    const body=parts.length, tip=`tip_${i}`;
    for(const [name,side,centre] of [[id,-1,point(0,.71)],[tip,1,point(lean,1.96)]]) {
      const geometry=new T.ExtrudeGeometry(new T.Shape(section(side)),
        {depth:1,bevelEnabled:false,steps:1,curveSegments:1});
      const vertices=geometry.getAttribute('position');
      for(let j=0;j<vertices.count;j++)vertices.setXYZ(j,...point(vertices.getX(j),vertices.getY(j),vertices.getZ(j)).toArray());
      surfaceData(geometry,p=>.59-.32*T.MathUtils.smoothstep(p.z,1.6,top),
        p=>.62*(1-T.MathUtils.smoothstep(p.z,baseHeight,.7))+.13*T.MathUtils.smoothstep(p.z,1.5,top));
      geometry.userData.smoothSeams=false;
      formation(geometry,p=>.12+.80*T.MathUtils.smoothstep(p.z,baseHeight,top),p=>{
        const foot=p.clone(); foot.z=baseHeight; return foot;
      });
      parts.push(part(name,skin(geometry,()=>[[i,1]]),'ice',centre.toArray()));
    }
    anchors.push({Id:id,Part:body+1,Position:point(lean,top).toArray(),Direction:[0,0,1],Control:id});
    connections.push([id,tip,point(0,1.59).toArray()]);
    for(const [row,z] of rows.entries()) {
      // 同一高度的胶囊沿真实冰瓣腰部取样，厚度留在冰体之内；两个邻瓣在接合后才连通。
      const w=ringWidth(z)-.033;
      contacts.push({Id:`${id}_wall_${row}`,Part:body,A:point(-w,z).toArray(),B:point(w,z).toArray(),
        Radius:.24+(row===1?.016:0),AControl:id,BControl:id,BuildRange:[.24+row*.12,.46+row*.13],BuildAlong:false});
    }
    if(i>0)connections.push([`petal_${i-1}`,id,point(-ringWidth(.42),.42).toArray()]);
  }
  connections.push([`petal_${count-1}`,'petal_0',anchors[0].Position]);
  const close=forming(motion('close',duration,false,controls,42,(i,p,control)=>{
    const {out,across}=directions[i];
    const opened=1-T.MathUtils.smoothstep(p,.19+(i%2)*.11,1);
    const lift=.15*Math.sin(Math.PI*p)*(i%2===0?1:.7);
    const position=new T.Vector3().fromArray(control.Position).addScaledVector(out,opened*(.82+.11*Math.cos(i*2.3)));
    position.z+=lift;
    const rotation=new T.Quaternion().setFromAxisAngle(across,(.59+.05*Math.sin(i))*opened);
    return {position:position.toArray(),rotation:rotation.toArray()};
  },[{Id:'lifted',At:.24},{Id:'closed',At:duration}]),p=>T.MathUtils.smoothstep(p,.025,.83));
  const bloomDuration=.58;
  const bloom=motion('bloom',bloomDuration,false,controls,35,(i,p,control)=>{
    const {out,across}=directions[i];
    const delay=(i%2)*.09+.025*(i%3);
    const t=T.MathUtils.smoothstep(p,delay,.82+delay);
    const windup=Math.sin(Math.PI*T.MathUtils.clamp(p/.22,0,1))*(1-t);
    const position=new T.Vector3().fromArray(control.Position).addScaledVector(out,
      t*(.48+.12*Math.cos(i*2.5))-.09*windup);
    position.z+=t*(.63+.095*Math.sin(i*1.8));
    const rotation=new T.Quaternion().setFromAxisAngle(across,t*(1.09+.08*Math.sin(i*2.4))-.10*windup);
    return {position:position.toArray(),rotation:rotation.toArray()};
  },[{Id:'bloom_open',At:.48},{Id:'bloom_edge',At:bloomDuration}]);
  write('frost_flower',parts,anchors,contacts,controls,[close,bloom],false,
    {Forward:[0,1,0],Up:[0,0,1],HideBelowGround:true},
    structure('articulated','forms_ice_shell.mjs','makeFrostFlower',{count,radius,duration,bloomDuration,rows},
      Object.fromEntries(parts.map(p=>[p.Id,['ice.shell','flower.petal','fracture.piece',
        p.Id.startsWith('tip_')?'flower.tip':'barrier.face']])),connections));
}
