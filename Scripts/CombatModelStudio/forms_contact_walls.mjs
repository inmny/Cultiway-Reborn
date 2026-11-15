/** 带明确人物作用的三类墙；真实表面、接触与动画共用地表控制。 */
import * as T from 'three';
import {write, structure, skin, motion, round} from './form_export.mjs';
import {wallPart, column} from './forms_solid_walls.mjs';

/** 连续厚火面由相互衔接的火舌构成，短幅摆动同时带动真实接触。 */
export function makeFireWall() {
  const count=11,spacing=.44,parts=[],controls=[],contacts=[];
  for(let i=0;i<count;i++) {
    const x=(i-5)*spacing,id=`tongue_${i}`,height=1.94+.28*Math.sin(i*1.73+.4);
    controls.push({Id:id,Position:[x,0,0],Rotation:[0,0,0,1]});
    const rings=Array.from({length:17},(_,j)=>{
      const t=j/16,z=-.045+(height+.045)*t;
      const width=.34*Math.pow(1-t,.38)+.004,depth=.29*Math.pow(1-t,.48)+.004;
      return Array.from({length:12},(_,s)=>{
        const a=s*Math.PI/6,curve=.13*Math.sin(t*3+i)*t;
        return [x+curve+Math.cos(a)*width,.045*Math.sin(t*5+i)*t+Math.sin(a)*depth,z];
      });
    });
    const body=wallPart(id,column(rings,12),'flame',[x,0,0],id);parts.push(body);
    for(let j=0;j<3;j++) {
      const a=.23+j*(height*.77-.23)/3,b=.23+(j+1)*(height*.77-.23)/3;
      const centre=z=>[x+.13*Math.sin((z+.045)/(height+.045)*3+i)*(z+.045)/(height+.045),0,z];
      contacts.push({Id:`${id}_${j}`,Part:i,A:centre(a),B:centre(b),Radius:.215,AControl:id,BControl:id});
    }
  }
  const run=motion('run',.72,true,controls,36,(i,p,c)=>({
    rotation:new T.Quaternion().setFromAxisAngle(new T.Vector3(0,1,0),.035*Math.sin(p*Math.PI*2+i*.77)).toArray()}));
  write('fire_wall',parts,wallAnchors(controls),contacts,controls,[run],true,{Forward:[0,1,0],Up:[0,0,1],HideBelowGround:true},
    structure('surface','forms_contact_walls.mjs','makeFireWall',{count,spacing,rows:17,sides:12},
      Object.fromEntries(parts.map(p=>[p.Id,['terrain.wall','barrier.face','fire.body']])),[]));
}

/** 宽厚流带交叠成风障；各环间的实际胶囊跟随相同控制，不用烟团或细线。 */
export function makeWindWall() {
  const controls=Array.from({length:11},(_,i)=>({Id:`flow_${i}`,Position:[(i-5)*.46,0,0],Rotation:[0,0,0,1]}));
  const parts=[],contacts=[];
  const centre=(x,band)=>[x,.065*Math.sin(x*1.6+band),.28+band*.45+.10*Math.sin(x*1.4+band*.6)];
  for(let band=0;band<5;band++) {
    const rings=controls.map((c,i)=>Array.from({length:12},(_,s)=>{
      const a=s*Math.PI/6,p=centre(c.Position[0],band),taper=i===0||i===10?.68:1;
      return [p[0],p[1]+Math.cos(a)*.23*taper,p[2]+Math.sin(a)*.26*taper];
    }));
    const geometry=column(rings,12);
    skin(geometry,p=>{
      const v=T.MathUtils.clamp(p.x/.46+5,0,10),i=Math.min(9,Math.floor(v)),u=v-i;
      return [[i,1-u],[i+1,u]];
    });
    parts.push(wallPart(`stream_${band}`,geometry,'wind'));
    for(let i=0;i<10;i++) contacts.push({Id:`stream_${band}_${i}`,Part:band,
      A:centre(controls[i].Position[0],band),B:centre(controls[i+1].Position[0],band),Radius:.17,
      AControl:controls[i].Id,BControl:controls[i+1].Id});
  }
  const run=motion('run',1.1,true,controls,44,(i,p,c)=>({position:[c.Position[0],
    .04*Math.sin(p*Math.PI*2-i*.43),.025*Math.sin(p*Math.PI*2-i*.43)]}));
  write('wind_wall',parts,wallAnchors(controls),contacts,controls,[run],true,{Forward:[0,1,0],Up:[0,0,1],HideBelowGround:true},
    structure('surface','forms_contact_walls.mjs','makeWindWall',{bands:5,count:11,spacing:.46,sides:12},
      Object.fromEntries(parts.map(p=>[p.Id,['terrain.wall','barrier.face','wind.flow']])),[]));
}

/** 原剑的刃脊、护手、柄与尾首完整保留，剑锋朝下形成可辨认的悬剑屏障。 */
export function makeSwordCurtain(sword) {
  const count=15,spacing=.34,scale=.72,parts=[],controls=[],contacts=[];
  for(let i=0;i<count;i++) {
    const id=`sword_${i}`,x=(i-7)*spacing,origin=new T.Vector3(x,0,1.98+.045*Math.sin(i));
    const rotation=new T.Quaternion().setFromEuler(new T.Euler(Math.PI, .035*Math.sin(i*1.7),0));
    controls.push({Id:id,Position:[x,0,0],Rotation:[0,0,0,1]});
    const transform=a=>new T.Vector3(...a).multiplyScalar(scale).applyQuaternion(rotation).add(origin).toArray().map(round);
    const offset=parts.length;
    for(const source of sword.Parts) {
      const positions=[],normals=[];
      for(let j=0;j<source.Positions.length;j+=3) {
        positions.push(...transform(source.Positions.slice(j,j+3)));
        normals.push(...new T.Vector3(...source.Normals.slice(j,j+3)).applyQuaternion(rotation).toArray().map(round));
      }
      parts.push({...source,Id:`${id}_${source.Id}`,Positions:positions,Normals:normals,Triangles:[...source.Triangles],
        Pivot:[x,0,0],Control:id,Roles:undefined,Details:undefined});
    }
    for(const source of sword.Contacts) contacts.push({...source,Id:`${id}_${source.Id}`,Part:offset+source.Part,
      A:transform(source.A),B:transform(source.B),Radius:source.Radius*scale,AControl:id,BControl:id});
  }
  const run=motion('run',1.4,true,controls,40,(i,p,c)=>({position:[c.Position[0],0,.018*Math.sin(p*Math.PI*2+i*.6)]}));
  write('sword_curtain',parts,wallAnchors(controls),contacts,controls,[run],true,{Forward:[0,1,0],Up:[0,0,1],HideBelowGround:true},
    structure('articulated','forms_contact_walls.mjs','makeSwordCurtain',{count,spacing,scale},
      Object.fromEntries(parts.map(p=>[p.Id,p.Id.endsWith('_blade')?['terrain.wall','barrier.face','blade.edge','blade.spine']:
        p.Id.endsWith('_grip')?['weapon.grip']:p.Id.endsWith('_guard')?['weapon.guard']:['weapon.pommel']])),[]));
}

/** 左右边缘和上缘挂点明确引用墙体已有控制。 */
function wallAnchors(controls) {
  const middle=Math.floor(controls.length/2),last=controls.length-1;
  return [{Id:'left',Part:0,Position:[controls[0].Position[0],0,0],Direction:[0,0,1],Control:controls[0].Id},
    {Id:'right',Part:0,Position:[controls[last].Position[0],0,0],Direction:[0,0,1],Control:controls[last].Id},
    {Id:'crest',Part:0,Position:[0,0,2.1],Direction:[0,0,1],Control:controls[middle].Id}];
}
