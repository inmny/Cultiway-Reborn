/** 水箭、水球和水刃沿真实水量制作用于两种视角的封闭截面。 */
import * as T from 'three';
import {write,structure,surfaceData,motion,round} from './form_export.mjs';
import {wallPart,column} from './forms_solid_walls.mjs';

const up=new T.Vector3(0,1,0);

/** 每个截面保存中心、横宽与厚度，截面随输运方向转过弯处。 */
function waterPart(name, rows, foam, sides=16, normal=up) {
  const centres=rows.map(row=>new T.Vector3(...row.slice(0,3)));
  const rings=rows.map(([, , ,width,depth],i)=>{
    const tangent=centres[Math.min(i+1,rows.length-1)].clone().sub(centres[Math.max(0,i-1)]).normalize();
    const across=new T.Vector3().crossVectors(normal,tangent).normalize();
    const above=new T.Vector3().crossVectors(tangent,across);
    return Array.from({length:sides},(_,j)=>{
      const a=j*2*Math.PI/sides;
      return centres[i].clone().addScaledVector(across,width*Math.cos(a))
        .addScaledVector(above,depth*Math.sin(a)).toArray();
    });
  });
  const geometry=column(rings,sides);
  const count=geometry.getAttribute('position').count;
  return [name,surfaceData(geometry,(_point,i)=>{
    // 同一条水流的薄唇、厚核和薄尾使用各自光程。
    const quad=Math.min(i,(rows.length-1)*sides*4-1);
    const row=Math.min(rows.length-1,Math.floor(quad/(sides*4))+((quad%4)>=2?1:0));
    return Math.max(.025,2*rows[row][4]);
  },(point,i)=>i<count-2*(sides+1)?foam(point):0)];
}

/** 实际接触沿主体连续截面取样，封闭薄尾和两股水之间的留白不填接触球。 */
function spine(rows,part=0) {
  return rows.slice(1).flatMap((b,i)=>{
    const a=rows[i],r=Math.min(a[3],a[4],b[3],b[4])*.84;
    return r<.035?[]:[[a.slice(0,3),b.slice(0,3),r,part]];
  });
}

/** 水体出手后的散落只调整同一控制的显示姿态和透明度。 */
function publish(family,index,pieces,contacts,dimensions) {
  const id=index===0?family:`${family}_${index}`;
  const controls=[{Id:'root',Position:[0,0,0],Rotation:[0,0,0,1]}];
  const parts=pieces.map(([name,body])=>wallPart(name,body,'water',[0,0,0],'root'));
  const anchors=[{Id:'root',Part:0,Position:[0,0,0],Direction:[0,0,1],Control:'root'}];
  if(family==='water_arrow') {
    let tip=[0,0,-Infinity],tipPart=0;
    for(const [part,mesh] of parts.entries())
      for(let i=0;i<mesh.Positions.length;i+=3)
        if(mesh.Positions[i+2]>tip[2]) {
          tip=mesh.Positions.slice(i,i+3);
          tipPart=part;
        }
    anchors.push({Id:'tip',Part:tipPart,Position:index===0?[0,0,tip[2]]:tip,Direction:[0,0,1],Control:'root'});
  }
  const prefix=family==='water_blade'?'edge':'core';
  const shapes=contacts.map(([a,b,r,part=0],i)=>({Id:`${prefix}_${i}`,Part:part,A:a,B:b,Radius:r,AControl:'root',BControl:'root'}));
  const motions=[];
  if(index<2) {
    const clip=motion('dissolve',.25,false,controls,12,(_index,t)=>({
      position:[0,-.20*t,-.06*t],
      rotation:new T.Quaternion().setFromAxisAngle(new T.Vector3(1,0,0),.17*t).toArray()
    }));
    for(const key of clip.Keys)key.Fade=round(T.MathUtils.smoothstep(key.At,0,clip.Duration));
    motions.push(clip);
  }
  const role=family==='water_blade'?'projectile.edge':family==='water_arrow'?'piercing.core':'projectile.core';
  write(id,parts,anchors,shapes,controls,motions,false,{Forward:[0,0,1],Up:[0,1,0]},
    structure('rigid','forms_water_projectiles.mjs','makeWaterProjectileForms',{variant:index,...dimensions},
      Object.fromEntries(parts.map(p=>[p.Id,['projectile.body',role]])),[]));
}

function arrow() {
  const rows=(profile)=>profile.map(([z,width,depth,x=0,y=0])=>[x,y,z,width,depth]);
  const mouth=rows([[-.27,.02,.022],[-.05,.103,.085],[.43,.145,.10],[1.29,.15,.104],
    [1.52,.38,.162],[1.82,.275,.15],[2.14,.14,.082],[2.44,.007,.006]]);
  const foil=rows([[-.42,.006,.008],[-.17,.23,.055],[.11,.35,.067],[.43,.19,.047],[.69,.008,.008]]);
  const arrowContacts=[
    [[0,0,.18],[0,0,1.23],.07], [[0,0,1.43],[0,0,1.86],.11],
    [[0,0,1.90],[0,0,2.20],.04]
  ];
  publish('water_arrow',0,[
    waterPart('water_shaft',mouth,p=>p.z>1.52?(.12+.25*Math.abs(p.x)):.04,20),
    waterPart('flow_vanes',foil,p=>.07+.21*Math.abs(p.x))
  ],arrowContacts,{length:2.71,span:.76});

  // 水锥的两股水绕真实中轴合拢，水层前缘较紧，近尾处间距宽。
  const jet=rows([[-.51,.01,.01],[-.27,.285,.22],[.16,.255,.20],[.64,.19,.17],
    [1.10,.14,.13],[1.74,.075,.076],[2.08,.007,.007]]);
  const pieces=[waterPart('cone',jet,p=>.05+.07*Math.max(0,1-p.z))], sh=spine(jet);
  for(const sign of [-1,1]) {
    const wrap=Array.from({length:32},(_,i)=>{
      const t=i/31,r=.33*Math.pow(1-t,.67),a=t*2*Math.PI*1.3+(sign>0?0:Math.PI);
      const thickness=.10*Math.pow(Math.sin(Math.PI*t),.56)+.007;
      return [r*Math.cos(a),r*Math.sin(a),-.49+2.34*t,thickness,thickness*.76];
    });
    pieces.push(waterPart(sign>0?'left_flow':'right_flow',wrap,p=>.17+.17*Math.max(0,1-p.z)));
  }
  publish('water_arrow',1,pieces,sh,{length:2.59,streams:2});

  const flat=rows([[-.46,.009,.01],[-.32,.25,.091,-.035],[-.04,.395,.13,-.075],[.56,.34,.137,-.02],
    [1.19,.29,.128],[1.62,.21,.106,.05],[1.91,.07,.051,.07],[2.03,.007,.006,.08]]);
  const flatContacts=spine(flat);
  for(const x of [-.18,.17])flatContacts.push([[x,0,-.13],[x,0,1.19],.093]);
  publish('water_arrow',2,[waterPart('pressed_shuttle',flat,p=>.04+.21*Math.abs(p.x)/.4,12)],flatContacts,
    {length:2.49,flatness:.37});

  const dart=rows([[-.74,.009,.008],[-.54,.115,.085],[-.22,.195,.14,0,-.058],[.22,.225,.17,.035,-.025],
    [.79,.164,.127,.035],[1.31,.055,.06],[1.55,.008,.008]]);
  const gates=[waterPart('short_dart',dart,p=>.08+.2*Math.max(0,p.z),18)]; const gateContacts=spine(dart);
  for(const sign of [-1,1]) {
    const shoulder=rows([[-.38,.008,.008,.21*sign,.004],[-.16,.08,.085,.28*sign,.06],
      [.22,.113,.095,.28*sign,.075],[.56,.063,.064,.26*sign,.045],[.79,.008,.008,.20*sign]]);
    gateContacts.push(...spine(shoulder,gates.length));
    gates.push(waterPart(sign>0?'first_fold':'second_fold',shoulder,p=>.18+.15*Math.abs(p.x)));
  }
  publish('water_arrow',3,gates,gateContacts,{length:2.29,folds:2});
}

function waterOrb() {
  /** 类球主体有完整外壳，可独立拉长、压扁或改成不规则水滴。 */
  function body(name,[width,height,length],skew=0,lobes=0) {
    const sphere=new T.SphereGeometry(1,32,24),vertices=sphere.getAttribute('position');
    for(let i=0;i<vertices.count;i++) {
      const [x,y,z]=[vertices.getX(i),vertices.getY(i),vertices.getZ(i)];
      const fold=1+lobes*Math.cos(3*Math.atan2(y,x))*(1-z*z);
      vertices.setXYZ(i,(x*width+skew*z*z)*fold,y*height*fold,z*length);
    }
    return [name,surfaceData(sphere,height*1.5,p=>.03+.10*Math.max(0,p.z))];
  }
  function spiral(start,turns,radius,zShift,thickness) {
    return Array.from({length:46},(_,i)=>{
      const t=i/45,a=start+t*Math.PI*2*turns;
      const r=radius*(.76+.24*t),width=.006+thickness*Math.pow(Math.sin(t*Math.PI),.61);
      return [r*Math.cos(a),r*Math.sin(a),zShift*(t-.5),width,width*.82];
    });
  }
  const defaultRows=spiral(.05,1.06,.49,.70,.113);
  const pool=[body('drop',[.45,.42,.47],0,.09),
    waterPart('wrapped_lip',defaultRows,p=>.24+.26*Math.max(0,p.y),14,new T.Vector3(0,0,1))];
  publish('water_orb',0,pool,[[[0,0,-.10],[0,0,.13],.305],...spine(defaultRows,1)],{radius:.49,rolls:1.06});
  const rim=spiral(-.30,.87,.52,.37,.148);
  const core=[body('inner_cistern',[.34,.33,.38]),
    waterPart('rolled_ring',rim,p=>.21+.37*Math.max(0,p.z),18,new T.Vector3(0,0,1))];
  publish('water_orb',1,core,[[[0,0,-.03],[0,0,.11],.26],...spine(rim,1)],{radius:.52,rolls:.87});
  publish('water_orb',2,[body('rounded_droplet',[.49,.325,.391],.024,.07)],
    [[[-.155,0,0],[.155,0,0],.23]],{radius:.49,depth:.391});
  const drop=[
    [.12,.015,-.73,.008,.008],[.06,.045,-.47,.17,.12],[-.02,.02,-.13,.36,.28],
    [0,-.02,.14,.43,.36],[.03,-.025,.39,.31,.24],[.05,-.02,.56,.009,.007]
  ];
  publish('water_orb',3,[waterPart('heavy_front',drop,p=>.10+.14*Math.max(0,p.z),22)],spine(drop),
    {length:1.29,span:.86});

  // 四股水沿各自回旋的水量接触，中心洞在旋转中始终空着。
  const flows=[],surfaces=[];
  for(let i=0;i<4;i++) {
    const rows=Array.from({length:18},(_,j)=>{
      const t=j/17,a=i*Math.PI/2+.16+t*1.12,r=.38+.09*Math.sin(t*Math.PI);
      const width=.01+.117*Math.pow(Math.sin(t*Math.PI),.56);
      return [r*Math.cos(a),r*Math.sin(a),.18*Math.cos(a+.8)-.15*t,width,width*.93];
    });
    surfaces.push(...spine(rows,flows.length));
    flows.push(waterPart(`current_${i}`,rows,p=>.18+.24*Math.max(0,p.y),12,new T.Vector3(0,0,1)));
  }
  publish('water_orb',4,flows,surfaces,{outerRadius:.59,streams:4});
}

function blade() {
  /** 水刃从实际锋口往后展成厚面。泡沫集中在卷唇和很薄的边缘。 */
  function sheet(index,edge,span,depth,thickness,lip) {
    function sample(curve,width,height) {
      return Array.from({length:33},(_,i)=>{
        const u=-1+i/16,t=Math.max(.022,Math.pow(1-u*u,.6));
        const w=width*t,h=height*t;
        const [x,y,z]=curve(u);
        return [x,y,z,w,h];
      });
    }
    const rows=sample(edge,depth,thickness);
    const parts=[waterPart(index===0?'sheet':'cutting_water',rows,p=>.06+.34*Math.max(0,p.y)/thickness,18)];
    if(lip)parts.push(waterPart('rolled_lip',sample(lip,depth*.53,thickness*.87),
      p=>.29+.32*Math.max(0,p.y)/thickness,18));
    const contacts=index===0?Array.from({length:16},(_,i)=>{
      const a=-.9+1.8*i/16,b=-.9+1.8*(i+1)/16;
      return [edge(a),edge(b),.12*Math.pow(1-Math.max(a*a,b*b),.6)];
    }):spine(rows);
    publish('water_blade',index,parts,contacts,{span,depth,thickness});
  }
  sheet(0,u=>[u*1.8,0,.43-.58*u*u+.035*Math.sin(u*9)],3.60,.35,.17,
    u=>[u*1.69,.066,.13-.51*u*u]);
  sheet(1,u=>[u*1.79,.10+.17*u+.06*Math.sin(u*3),.22+.52*u+.29*u*u-.43*u*u*u],3.58,.31,.16,
    u=>[u*1.67,.15+.12*u,.02+.48*u+.25*u*u-.42*u*u*u]);
  sheet(2,u=>[u*1.25,.025,.34-.54*u*u],2.50,.20,.12,null);
  sheet(3,u=>[u*1.63,.032*Math.cos(u*4),.60-.76*Math.abs(u)-.09*u*u],3.26,.31,.19,
    u=>[u*1.48,-.04,.33-.69*Math.abs(u)]);
}

/** 每个旧索引一一对应，共用发射规则的同时允许更换实际水体材料。 */
export function makeWaterProjectileForms() { arrow();waterOrb();blade(); }
