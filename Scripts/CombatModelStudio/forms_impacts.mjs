/** 投射和坠落来源的实体，以及由真实余力交接形成的六类爆发厚体。 */
import * as T from 'three';
import {write,structure,surfaceData,motion,round,dissolve} from './form_export.mjs';
import {wallPart,column} from './forms_solid_walls.mjs';

/** 封闭的分层长体；局部正Z是前方，尖端与内部接触共同朝向实际运动。 */
function shaft(rows,sides=8) {
  return column(rows.map(([z,r,x=0,y=0])=>Array.from({length:sides},(_,i)=>{
    const a=i*Math.PI*2/sides;return [x+Math.cos(a)*r,y+Math.sin(a)*r,z];
  })),sides);
}

/** 共用制作出口，各资源保留自己的网格和实际接触芯。 */
function publish(id,pieces,contacts,parameters={},ending=false) {
  const controls=[{Id:'root',Position:[0,0,0],Rotation:[0,0,0,1]}];
  const parts=pieces.map(([name,g,s,fixedSurface])=>{
    const part=wallPart(name,g,s,[0,0,0],'root');
    if(fixedSurface)part.UseMatterSurface=false;
    return part;
  });
  const motions=ending?[dissolve(controls,(_index,t)=>({position:[0,.13*t,-.06*t]}))]:[];
  write(id,parts,[{Id:'root',Part:0,Position:[0,0,0],Direction:[0,0,1],Control:'root'}],
    contacts.map(([a,b,r,part=0],i)=>({Id:`core_${i}`,Part:part,A:a,B:b,Radius:r,AControl:'root',BControl:'root'})),controls,motions,false,
    {Forward:[0,0,1],Up:[0,1,0],HideBelowGround:true},structure('rigid','forms_impacts.mjs','makeImpactForms',parameters,
      Object.fromEntries(parts.map(p=>[p.Id,['projectile.body','impact.volume']])),[]));
}

/** 原索引的火核、火舌及交叉束各自保留可辨轮廓，接触芯沿有厚度的部分铺设。 */
function fireball() {
  const pieces=[['heart',new T.SphereGeometry(.49,24,16),'flame_cast']];
  for(let i=0;i<3;i++)pieces.push([`flare_${i}`,shaft([[-.86,.015],[-.44,.21],[-.12,.18]],10).translate(.24,0,0).rotateZ(i*Math.PI*2/3),'flame_cast']);
  publish('fireball',pieces,[[[0,0,0],[0,0,.10],.35]],{radius:.49,backFlares:3},true);

  // 闭合截面的火舌沿曲线变厚再收尖；接触仅覆盖仍有可见厚度的相邻截面。
  function tongue(parts,contacts,name,rows,surface='flame_cast',fixed=false,sides=10) {
    const part=parts.length;
    parts.push([name,shaft(rows,sides),surface,fixed]);
    for(let i=1;i<rows.length;i++) {
      const [z,r,x=0,y=0]=rows[i-1], [nextZ,nextR,nextX=0,nextY=0]=rows[i];
      const core=Math.min(r,nextR)*.84;
      if(core>.04)contacts.push([[x,y,z],[nextX,nextY,nextZ],core,part]);
      else if(Math.max(r,nextR)>.14) {
        const peak=r>nextR?[x,y,z]:[nextX,nextY,nextZ];
        contacts.push([peak,peak,Math.max(r,nextR)*.72,part]);
      }
    }
  }
  function variant(index,profile,build) {
    const parts=[],contacts=[];
    build(parts,contacts);
    publish(`fireball_${index}`,parts,contacts,{legacyVariant:index,profile},index<=4);
  }
  function heart(parts,contacts,radius,surface,geometry,fixed=false) {
    contacts.push([[0,0,0],[0,0,0],radius,parts.length]);
    parts.push(['heart',geometry,surface,fixed]);
  }

  variant(1,'white_dart',(p,c)=>tongue(p,c,'white_dart',
    [[-.80,.014,-.06],[-.53,.085,-.02],[-.14,.14],[.13,.21],[.42,.17],[.62,.008,.03]],'fireball_white'));
  variant(2,'ember_shell',(p,c)=>{
    heart(p,c,.34,'flame_cast',new T.IcosahedronGeometry(.43,2).scale(.94,1.12,.92));
    for(let i=0;i<5;i++) {
      const a=i*Math.PI*2/5;
      const shell=shaft([[-.37,.035,.04],[-.12,.13,.31],[.14,.15,.31],[.37,.017,.13]],7).rotateZ(a);
      p.push([`fissure_${i}`,shell,'fireball_copper',true]);
    }
    tongue(p,c,'crown',[[-.22,.18,0,.28],[-.06,.15,.015,.41],[.13,.09,.08,.58],[.19,.012,.14,.73]],'flame_cast');
  });
  variant(3,'blue_crest',(p,c)=>{
    tongue(p,c,'crest',[[-.36,.10,0,.01],[-.12,.29,0,.08],[.08,.29,.06,.29],[.22,.18,.10,.51],
      [.29,.075,.02,.69],[.32,.009,-.07,.82]],'fireball_blue');
    p.push(['hot_fold',shaft([[-.20,.08],[-.04,.16],[.14,.13,0,.07],[.22,.012,0,.30]]),'fireball_ice',true]);
  });
  variant(4,'solar_disk',(p,c)=>{
    p.push(['heart',new T.SphereGeometry(.46,24,16).scale(1,1,.42),'fireball_white']);
    for(const x of [-.21,0,.21])c.push([[x,-.14,0],[x,.14,0],.14]);
    for(let i=0;i<8;i++) {
      const angle=i*Math.PI/4;
      p.push([`ray_${i}`,shaft([[.05,.06],[.22,.13],[.42+.08*(i%3),.01]],8)
        .rotateY(Math.PI/2).rotateZ(angle).translate(.27*Math.cos(angle),.27*Math.sin(angle),-.06),'solar_kernel',true]);
    }
  });
  variant(5,'ember_seed',(p,c)=>{
    heart(p,c,.22,'flame_cast',new T.IcosahedronGeometry(.29,2).scale(1,.94,1.12));
    p.push(['molten_face',new T.SphereGeometry(.19,16,10).scale(.9,.8,.60).translate(.02,.03,.21),'solar_kernel',true]);
  });
  variant(6,'flat_flame',(p,c)=>{
    const flat=shaft([[-1.01,.013,.13],[-.51,.17,.10],[0,.37,-.02],[.31,.34],[.63,.016,.045]],12).scale(1,.32,1);
    p.push(['flat_fold',flat,'flame_cast']);
    for(const x of [-.19,0,.19])c.push([[x,0,-.21],[x,0,.38],.10]);
    p.push(['hot_lip',shaft([[-.18,.13],[.40,.23],[.63,.01]],10).scale(1,.23,1),'fireball_white',true]);
  });
  variant(7,'thick_flame',(p,c)=>{
    heart(p,c,.48,'flame_cast',new T.SphereGeometry(.62,24,16).scale(.93,1.1,1.03));
    for(let i=0;i<4;i++) {
      const a=i*Math.PI/2;
      p.push([`shoulder_${i}`,shaft([[-1.16,.011,.34],[-.67,.22,.34],[-.19,.28,.34],[.26,.07,.27]],9)
        .rotateZ(a),'flame_cast']);
    }
    p.push(['front_cap',new T.SphereGeometry(.38,20,14).translate(.02,0,.35),'solar_kernel',true]);
  });
  variant(8,'needle_with_long_flare',(p,c)=>{
    tongue(p,c,'needle',[[-1.28,.012,.14],[-.87,.08,.09],[-.43,.16,-.04],[-.10,.24],[.27,.21],[.52,.014,.02]]);
    p.push(['white_front',shaft([[-.05,.13],[.31,.17],[.55,.012]],8),'solar_kernel',true]);
  });
  variant(9,'ember_facets',(p,c)=>{
    heart(p,c,.24,'flame_cast',new T.IcosahedronGeometry(.43,0).scale(1,.88,1.26));
    for(let i=0;i<3;i++)p.push([`facet_${i}`,new T.IcosahedronGeometry(.20,0).scale(.84,.6,1.31)
      .translate(.22*Math.cos(i*2.1),.22*Math.sin(i*2.1),-.27),'fireball_copper',true]);
    p.push(['inner_facet',new T.IcosahedronGeometry(.26,0).translate(.01,.02,.31),'solar_kernel',true]);
  });
  function pair(index,profile,surface,rows) {
    variant(index,profile,(p,c)=>{
      for(const sign of [-1,1])tongue(p,c,`fold_${sign}`,
        rows.map(([z,r,x,y=0])=>[z,r,sign*x,sign*y]),surface,false,9);
    });
  }
  pair(10,'diverging_gold','fireball_gold',[[-.71,.01,.015],[-.38,.07,.22],[.02,.11,.40],[.34,.16,.49],[.63,.02,.60]]);
  pair(11,'crossed_blue','fireball_blue',[[-.75,.01,-.57,-.044],[-.48,.07,-.32,-.045],[-.06,.10,.01,-.07],
    [.30,.16,.35,-.11],[.51,.07,.46,-.06],[.66,.012,.52,-.04]]);
  pair(12,'diverging_green','fireball_green',[[-.69,.01,.025],[-.42,.085,.25,.02],[-.15,.12,.45,.062],
    [.22,.18,.52,.09],[.53,.06,.58,.14],[.63,.01,.69,.20]]);
  pair(13,'braided_rose','fireball_rose',[[-.83,.01,-.47,-.21],[-.52,.067,-.41,-.12],[-.25,.10,-.18,.018],
    [.02,.12,.08,.12],[.28,.15,.33,.18],[.51,.09,.38,.11],[.68,.012,.45,.0]]);
  pair(14,'hooked_red','fireball_red',[[-.56,.012,.56,.04],[-.38,.091,.47,.04],[-.12,.14,.37,-.012],
    [.15,.18,.18,.015],[.40,.14,.055,.025],[.62,.014,.17,.03]]);
  pair(15,'crossed_silver','fireball_ice',[[-.74,.01,-.53],[-.46,.075,-.33,-.14],[-.08,.108,-.036,-.085],
    [.25,.16,.24,-.062],[.51,.14,.44,.01],[.63,.015,.52,.08]]);
  variant(16,'vertical_braid',(p,c)=>{
    for(const sign of [-1,1])tongue(p,c,`lift_${sign}`,[[-.52,.018,sign*.035,sign*-.31],[-.29,.09,sign*.13,sign*-.17],
      [-.01,.16,sign*.08,sign*.07],[.18,.14,sign*-.025,sign*.32],[.25,.08,sign*-.11,sign*.52],
      [.38,.012,sign*.13,sign*.71]],sign>0?'flame_cast':'solar_kernel',sign<0);
  });
  variant(17,'separated_ring',(p,c)=>{
    const radius=.44;
    for(let arc=0;arc<4;arc++) {
      const rings=[], part=p.length;
      for(let i=0;i<=16;i++) {
        const t=i/16,a=arc*Math.PI/2+t*1.10+.17;
        const width=.125*Math.pow(Math.max(.03,Math.sin(Math.PI*t)),.60), z=.10*(.5-t);
        const cos=Math.cos(a),sin=Math.sin(a);
        rings.push(Array.from({length:8},(_,j)=>{
          const b=j*Math.PI/4,r=radius+width*Math.cos(b);
          return [r*cos,r*sin,z+width*Math.sin(b)];
        }));
        if(i>=3&&i<=13&&(i-3)%2===0) {
          const x=radius*cos,y=radius*sin;c.push([[x,y,z],[x,y,z],width*.87,part]);
        }
      }
      p.push([`arc_${arc}`,column(rings,8),arc%2?'fireball_copper':'flame_cast']);
    }
  });
}

/** 雷球各索引保留原来的主轮廓，厚雷瓣围绕实际接触核心。 */
function thunder() {
  const forward=new T.Vector3(0,0,1);
  const pieces=[['heart',new T.IcosahedronGeometry(.46,2),'thunder_core']];
  for(let i=0;i<6;i++) {
    const direction=new T.Vector3(Math.cos(i*Math.PI/3),Math.sin(i*Math.PI/3),i%2?.48:-.48).normalize();
    const fold=shaft([[.26,.13],[.35,.195,.065],[.49,.123,-.065],[.63,.012,-.127]],8)
      .applyQuaternion(new T.Quaternion().setFromUnitVectors(forward,direction));
    pieces.push([`lobe_${i}`,fold,i%2?'thunder_body':'thunder_arc_fork']);
  }
  publish('thunder_orb',pieces,[[[0,0,0],[0,0,0],.39]],{lobes:6},true);

  const drop=shaft([[-.72,.008,-.17],[-.52,.095,-.10],[-.28,.19,.025],[-.10,.32,.037],
    [.22,.356,.018],[.39,.17,-.02],[.45,.009,-.03]],14);
  const wake=shaft([[-.94,.007,.12],[-.63,.065,.155],[-.44,.133,.07],[-.23,.053,-.04]],8);
  publish('thunder_orb_1',[
    ['heart',drop,'thunder_core'],['wake',wake,'thunder_arc_fork']
  ],[[[.024,0,-.08],[.02,0,.24],.233]],{variant:1});

  const paired=[['heart',new T.IcosahedronGeometry(.40,2).scale(.92,.87,1.28),'thunder_core']];
  for(const [i,phase] of [0,Math.PI].entries()) {
    const sides=10,sections=28;
    const winding=column(Array.from({length:sections+1},(_,j)=>{
      const t=j/sections,a=phase+4.55*t;
      const taper=Math.max(.022,.124*Math.pow(Math.sin(Math.PI*t),.66));
      const distance=.312+.098*Math.sin(Math.PI*t);
      return Array.from({length:sides},(_,k)=>{
        const b=k*Math.PI*2/sides,r=distance+taper*Math.cos(b);
        return [r*Math.cos(a),r*Math.sin(a),-.57+1.12*t+taper*Math.sin(b)];
      });
    }),sides);
    paired.push([`winding_${i}`,winding,i?'thunder_arc_fork':'thunder_body']);
  }
  publish('thunder_orb_2',paired,[[[0,0,-.22],[0,0,.22],.31]],{variant:2,folds:2});

  const amber=[['heart',new T.IcosahedronGeometry(.34,1).scale(1.12,.86,.92),'thunder_amber']];
  for(const [i,phase] of [.2,2.27,4.34].entries()) {
    const fragment=shaft([[-.25,.012,.13],[-.16,.107,.27],[.055,.125,.33],[.24,.064,.26],[.31,.012,.17]],7)
      .rotateZ(phase);
    amber.push([`fold_${i}`,fragment,i===1?'thunder_core':'thunder_amber']);
  }
  publish('thunder_orb_3',amber,[[[0,0,-.07],[0,0,.075],.27]],{variant:3,folds:3});
}

/** 爆发先标出实际球形作用范围，厚雷瓣从同一点错时打开，在原地消散。 */
function thunderOrbImpact() {
  const origin=[0,0,0],identity=[0,0,0,1],duration=.22,ease=T.MathUtils.smoothstep;
  const axes=Array.from({length:7},(_,i)=>new T.Vector3(Math.cos(i*2.4),Math.sin(i*2.4),.34*Math.sin(i*1.9)).normalize());
  const controls=[{Id:'root',Position:origin,Rotation:identity},...axes.map((axis,i)=>({
    Id:`lobe_${i}`,Parent:'root',Position:origin,Rotation:identity
  }))];
  const parts=[wallPart('extent',new T.SphereGeometry(.90,24,16),'thunder_corona',origin,'root')];
  for(const [i,axis] of axes.entries()) {
    const facet=shaft([[.08,.03],[.28,.142,.09],[.52,.162,-.045],[.75,.013,-.10]],7)
      .applyQuaternion(new T.Quaternion().setFromUnitVectors(new T.Vector3(0,0,1),axis));
    parts.push(wallPart(`lobe_${i}`,facet,i%3?'thunder_arc_fork':'thunder_arc',origin,controls[i+1].Id));
  }
  const clip=motion('spread',duration,false,controls,24,(index,phase)=>{
    if(!index)return {};
    const time=phase*duration,axis=axes[index-1],delay=(index%4)*.005;
    const open=ease(time,delay,.058+delay),settle=ease(time,.084,.215);
    return {position:axis.clone().multiplyScalar(-.13*(1-open)+.15*open-.031*settle).toArray(),
      rotation:new T.Quaternion().setFromAxisAngle(new T.Vector3(-axis.y,axis.x,0).normalize(),.42*open-.60*settle).toArray()};
  });
  for(const key of clip.Keys)key.Fade=round(.20*(1-ease(key.At,0,.024))+ease(key.At,.052,duration));
  write('thunder_orb_impact',parts,[{Id:'root',Part:0,Position:origin,Direction:[0,0,1],Control:'root'}],
    [{Id:'core_0',Part:0,A:origin,B:origin,Radius:.9,AControl:'root',BControl:'root'}],controls,[clip],
    false,{Forward:[0,0,1],Up:[0,1,0],HideBelowGround:true},
    structure('articulated','forms_impacts.mjs','makeImpactForms',{contactRadius:.9,lobes:axes.length,duration},
      Object.fromEntries(parts.map(p=>[p.Id,['impact.volume']])),[]));
}

/** 混沌球由不共面的厚瓣挤合，中央保留封闭核，表面错位不扩大实际接触。 */
function chaos() {
  const pieces=[['heart',new T.SphereGeometry(.38,24,16),'entropy_depth']];
  for(let i=0;i<3;i++)pieces.push([`fold_${i}`,new T.SphereGeometry(.46,24,12).scale(1,.48,1.12).rotateX(.9*i).rotateZ(1.8*i),'entropy_veil']);
  publish('chaos_orb',pieces,[[[0,0,-.05],[0,0,.05],.30]],{folds:3});
}

/** 五种下落主体有独立轮廓，接触芯随共同下落轨迹及朝向。 */
function falling() {
  publish('flame_meteor',[
    ['body',new T.IcosahedronGeometry(.63,2).scale(1,.91,1.5),'flame'],
    ['back',shaft([[-1.55,.012],[-1.05,.31],[-.5,.43]],10),'flame']],
    [[[0,0,-.28],[0,0,.36],.42]],{profile:'elongated_flame'});
  const rock=new T.IcosahedronGeometry(.80,1),p=rock.getAttribute('position');
  for(let i=0;i<p.count;i++)p.setXYZ(i,p.getX(i)*(1+.13*Math.sin(p.getY(i)*12)),p.getY(i)*.86,p.getZ(i)*1.28);
  publish('falling_rock',[['boulder',rock,'stone']],[[[0,0,-.25],[0,0,.28],.51]],{profile:'faceted_boulder'});
  const trunk=shaft([[-1.05,.30,-.03],[-.63,.37],[.40,.32,.04],[1.18,.20,.02]],12);
  const branch=shaft([[0,.13],[.52,.10],[.86,.025]],8).rotateY(.9).translate(.13,0,-.29);
  publish('falling_timber',[['trunk',trunk,'wood'],['broken_branch',branch,'wood']],[[[0,0,-.75],[.025,0,.82],.19]],{profile:'broken_trunk'});
  lightningStrike();
  const pieces=[],contacts=[];
  for(let i=0;i<4;i++) {
    const a=i*Math.PI/2,x=Math.cos(a)*.23,y=Math.sin(a)*.23,length=1.05+.24*Math.sin(i*2);
    pieces.push([`crystal_${i}`,shaft([[-.67,.15],[.34,.22],[length,.004]],5).translate(x,y,0),'ice']);
    contacts.push([[x,y,-.44],[x,y,.61],.10,i]);
  }
  publish('icefall',pieces,contacts,{profile:'crystal_cluster',crystals:4});
}

/** 前端折面套在原接触芯外，后段才分岔，保留原尺寸与下落基准。 */
function lightningStrike() {
  const bolt=shaft([[-1.45,.07,.15],[-.99,.25,-.16],[-.54,.29,.13],[-.14,.24,-.13],
    [.33,.23,.08],[.71,.145,-.04],[.83,.11,.04],[1.34,.005,.02]],7);
  const pieces=[['channel',bolt,'thunder_arc']];
  for(const [i,angle] of [.25,2.30,4.11].entries()) {
    const start=-1.04+i*.26;
    const fork=shaft([[start,.14],[start+.28,.175,.36],[start+.56,.11,.50],
      [start+.84,.008,.86-i*.11]],6).rotateZ(angle);
    pieces.push([`fork_${i}`,fork,'thunder_arc_fork']);
  }
  publish('lightning_strike',pieces,[[[0,0,-.7],[0,0,.72],.10]],{profile:'forked_channel',forks:3});
  // 旧术保留原芯。新落雷的前芯抵到实体尖端，小体型不会在接触前就判为落地。
  publish('lightning_strike_focused',pieces,[[[0,0,-.7],[0,0,1.23],.13]],{profile:'forked_channel',forks:3});
}

/** 蓄力先收雷瓣；确认接触时余瓣才由击点向外翻，随后在同一动作中消散。 */
function lightningStages() {
  const zero=[0,0,0],identity=[0,0,0,1],ease=T.MathUtils.smoothstep;
  for(const charge of [true,false]) {
    const duration=charge?.58:.40, count=charge?4:5, clipId=charge?'gather':'release';
    const axes=Array.from({length:count},(_,i)=>new T.Vector3(Math.cos(i*2.4),Math.sin(i*2.4),0));
    const controls=[{Id:'root',Position:zero,Rotation:identity},...axes.map((axis,i)=>({
      Id:`lobe_${i}`,Parent:'root',Position:zero,Rotation:identity
    }))];
    const parts=[wallPart('heart',new T.IcosahedronGeometry(charge?.25:.17,1).scale(1,1,charge?1.2:.65),
      'thunder_arc',zero,'root')];
    for(const [i,axis] of axes.entries()) {
      const length=charge?.62+.09*(i%3):.50+.12*(i%3);
      const shard=shaft([[0,.038], [length*.28,.125], [length*.64,.19,-.16],[length,.01,-.095]],6)
        .applyQuaternion(new T.Quaternion().setFromUnitVectors(new T.Vector3(0,0,1),axis));
      parts.push(wallPart(controls[i+1].Id,shard,'thunder_arc_fork',zero,controls[i+1].Id));
    }
    const clip=motion(clipId,duration,false,controls,24,(index,phase)=>{
      if(!index)return {};
      const i=index-1, axis=axes[i], time=phase*duration;
      const start=.017*i;
      const opening=ease(time,start,charge?.48:.14+start);
      const settling=ease(time,.18+start,duration);
      const radius=charge?1.13*(1-opening):.56*opening+.16*settling;
      const position=axis.clone().multiplyScalar(radius);
      position.z=charge?(.23-.12*i)*(1-opening):-(.06+.16*opening)*(1-settling);
      const hinge=new T.Vector3(-axis.y,axis.x,0);
      const amount=charge?(1-opening)*(.75-.14*i):opening*(.50+.06*i)-settling*.49;
      const tilt=new T.Quaternion().setFromAxisAngle(hinge,amount);
      if(charge)tilt.premultiply(new T.Quaternion().setFromAxisAngle(new T.Vector3(0,0,1),(.85-.05*i)*(1-opening)));
      return {position:position.toArray(),rotation:tilt.toArray()};
    });
    for(const key of clip.Keys)key.Fade=round(charge
      ?.72*(1-ease(key.At,0,.17))+ease(key.At,.49,duration)
      :.42*(1-ease(key.At,0,.055))+ease(key.At,.13,duration));
    write(charge?'lightning_charge':'lightning_contact',parts,
      [{Id:'root',Part:0,Position:zero,Direction:[0,0,1],Control:'root'}],[],controls,[clip],false,
      {Forward:[0,0,1],Up:[0,1,0],HideBelowGround:true},structure('articulated','forms_impacts.mjs','makeImpactForms',
        {lobes:count,duration},Object.fromEntries(parts.map(p=>[p.Id,['impact.volume']])),[]));
  }
}

/** 六类爆发保留闭合实体内核；内部球形作用区半径0.9，与运行的原作用半径共同缩放。 */
function bursts() {
  const settings=[['fire','flame_cast'],['lightning','lightning_dense'],['entropy','entropy_veil'],['earth','stone'],['wood','wood'],['ice','ice']];
  for(const [matter,surface] of settings) {
    let core=matter==='earth'||matter==='ice'?new T.IcosahedronGeometry(.97,2):new T.SphereGeometry(.91,28,18);
    const pieces=[['heart',core,surface]];
    for(let i=0;i<6;i++) {
      const direction=new T.Vector3(Math.cos(i*2.4)*.8,Math.sin(i*2.4)*.8,(i-2.5)*.29).normalize();
      let g;
      if(matter==='earth')g=new T.BoxGeometry(.43,.26,.32).translate(0,0,.83);
      else if(matter==='entropy')g=new T.SphereGeometry(.45,20,12).scale(1,.48,1).translate(0,0,.59).rotateY(i*.7);
      else if(matter==='fire')g=shaft([[.65,.27],[.84,.22],[1.10,.006]],10);
      else if(matter==='lightning')g=shaft([[.65,.21,.08],[.88,.16,-.06],[1.08,.006,.02]],6);
      else if(matter==='wood')g=shaft([[.65,.20],[.95,.13],[1.10,.06]],10);
      else g=shaft([[.64,.22],[.88,.18],[1.12,.003]],5);
      g.applyQuaternion(new T.Quaternion().setFromUnitVectors(new T.Vector3(0,0,1),direction));
      pieces.push([`lobe_${i}`,g,surface]);
    }
    publish(matter+'_burst',pieces,[[[0,0,0],[0,0,0],.9]],{profile:matter,contactRadius:.9});
  }
}

/** 炎陨向前撞击，背向坠落方向翻开低厚火肩，落点仍可看穿。 */
function meteorImpact() {
  const floor=new T.LatheGeometry([[0,.006],[.73,.006],[.89,.01],[.90,.018],[.83,.05],[.56,.077],[0,.055]]
    .map(p=>new T.Vector2(...p)),64).rotateX(-Math.PI/2);
  const pieces=[['heat',surfaceData(floor,.11,p=>.47+.23*(1-Math.hypot(p.x,p.y)/.90)),'meteor_heat']];
  const shoulders=[];
  for(const [index,start,span] of [[0,.20,2.03],[1,2.66,1.46],[2,4.57,1.42]]) {
    const sections=30,sides=12;
    const rings=Array.from({length:sections+1},(_,i)=>{
      const t=i/sections,a=start+span*t,taper=Math.max(.018,Math.pow(Math.sin(Math.PI*t),.71));
      const rim=.64+.053*Math.sin(a*5+index),height=.065+taper*(.115+.067*Math.sin(a*4+.4));
      return Array.from({length:sides},(_,j)=>{
        const b=j*2*Math.PI/sides,r=rim+.193*taper*Math.cos(b);
        return [r*Math.cos(a),r*Math.sin(a),-Math.max(.01,height+.150*taper*Math.sin(b))];
      });
    });
    const arc=column(rings,sides);
    pieces.push([`fold_${index}`,surfaceData(arc,.18,p=>T.MathUtils.smoothstep(-p.z,.013,.10)),'meteor_crown']);
    shoulders.push(start+span*.5);
  }
  publish('flame_meteor_impact',pieces,[[[0,0,0],[0,0,0],.9]],{contactRadius:.9,folds:3});
  publishMeteorMotion(pieces,shoulders);
}

/** 落点的低焰标出整块作用面；独立火肩错时翻开再贴地，始终不移动球形作用。 */
function publishMeteorMotion(pieces,shoulders) {
  const identity=[0,0,0,1],origin=[0,0,0],ease=T.MathUtils.smoothstep,duration=.22;
  const directions=shoulders.map(angle=>new T.Vector3(Math.cos(angle),Math.sin(angle),0));
  const controls=[{Id:'root',Position:origin,Rotation:identity},...directions.map((axis,i)=>({
    Id:`fold_${i}`,Parent:'root',Position:axis.clone().multiplyScalar(.62).toArray(),Rotation:identity
  }))];
  const parts=pieces.map(([name,g,s],i)=>wallPart(name,g,s,controls[i].Position,controls[i].Id));
  const clip=motion('spread',duration,false,controls,22,(i,phase,control)=>{
    if(!i)return {};
    const at=phase*duration,axis=directions[i-1];
    const start=(i-1)*.009,open=ease(at,start,start+.052),settle=ease(at,.073+(i-1)*.007,.21);
    const position=new T.Vector3(...control.Position).addScaledVector(axis,-.28*(1-open)+.062*settle);
    position.z=.14*(1-open)+.17*settle;
    const tangent=new T.Vector3(-axis.y,axis.x,0);
    const rotation=new T.Quaternion().setFromAxisAngle(tangent,-.68*(1-open)+.16*open-.62*settle);
    return {position:position.toArray(),rotation:rotation.toArray()};
  },[{Id:'spread',At:.07},{Id:'settle',At:.10}]);
  for(const key of clip.Keys)key.Fade=round(.33*(1-ease(key.At,0,.035))+ease(key.At,.072,duration));
  const anchors=[{Id:'root',Part:0,Position:origin,Direction:[0,0,1],Control:'root'}];
  const contacts=[{Id:'core_0',Part:0,A:origin,B:origin,Radius:.9,AControl:'root',BControl:'root'}];
  write('flame_meteor_impact_motion',parts,anchors,contacts,controls,[clip],false,
    {Forward:[0,0,1],Up:[0,1,0],HideBelowGround:true},
    structure('articulated','forms_impacts.mjs','makeImpactForms',{contactRadius:.9,folds:3,duration},
      Object.fromEntries(parts.map(p=>[p.Id,['projectile.body','impact.volume']])),[]));
}

/** 发布八种来源主体、六类通用爆发及落地、蓄雷形体。 */
export function makeImpactForms() {fireball();thunder();thunderOrbImpact();chaos();falling();lightningStages();bursts();meteorImpact();}
