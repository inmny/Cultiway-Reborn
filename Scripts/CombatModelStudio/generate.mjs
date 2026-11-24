/** 用 Three.js 制作有体积的战斗主体，导出到游戏共同模型资料。坐标为横向、纵深、高度。 */
import * as T from 'three';
import {mergeGeometries,mergeVertices} from 'three/addons/utils/BufferGeometryUtils.js';
import {writeFileSync,mkdirSync} from 'node:fs';

const output=new URL('../../Content/CombatModels/',import.meta.url);
const previews=new URL('../../artifacts/combat-model-studio/',import.meta.url);
mkdirSync(output,{recursive:true});
mkdirSync(previews,{recursive:true});
const V=(x,y,z)=>new T.Vector3(x,y,z),white=0xffffff;
function paint(g,color=white){
  const c=new T.Color(color),a=new Float32Array(g.attributes.position.count*3);
  for(let i=0;i<a.length;i+=3){a[i]=c.r;a[i+1]=c.g;a[i+2]=c.b;}
  g.setAttribute('color',new T.BufferAttribute(a,3));
  for(const key of Object.keys(g.attributes))if(!['position','normal','color'].includes(key))g.deleteAttribute(key);
  return g;
}
function sphere(p,s,color=white,segments=14,rings=8){return paint(new T.SphereGeometry(1,segments,rings).scale(...s).translate(...p),color);}
function limb(a,b,r,color=white){
  a=V(...a);b=V(...b);const delta=b.clone().sub(a);
  const g=new T.CylinderGeometry(r*.85,r,delta.length(),8,1);
  g.applyQuaternion(new T.Quaternion().setFromUnitVectors(V(0,1,0),delta.normalize())).translate(...a.add(b).multiplyScalar(.5).toArray());
  return paint(g,color);
}
function tube(points,r,segments=24,color=white,sides=6){return paint(new T.TubeGeometry(new T.CatmullRomCurve3(points.map(p=>V(...p))),segments,r,sides,false),color);}
function lathe(points,segments=18,color=white){return paint(new T.LatheGeometry(points.map(p=>new T.Vector2(...p)),segments).rotateX(Math.PI/2),color);}
function mesh(positions,triangles,color=white){
  const g=new T.BufferGeometry();g.setAttribute('position',new T.Float32BufferAttribute(positions,3));g.setIndex(triangles);g.computeVertexNormals();return paint(g,color);
}
function face(points,color=white){return mesh(points.flat(),[0,1,2,...(points.length===4?[0,2,3]:[])],color);}
function exportModel(name,parts,surface,destination=output){
  const g=mergeVertices(mergeGeometries(parts.map(p=>p.index?p.toNonIndexed():p),false),1e-6),pos=Array.from(g.attributes.position.array),norm=Array.from(g.attributes.normal.array),color=g.attributes.color.array,triangles=Array.from(g.index.array);
  if(pos.some(x=>!Number.isFinite(x))||norm.some(x=>!Number.isFinite(x)))throw Error(name+' 包含无效坐标');
  const colors=[];
  for(let i=0;i<triangles.length;i+=3){const a=triangles[i]*3,b=triangles[i+1]*3,c=triangles[i+2]*3,r=Math.round((color[a]+color[b]+color[c])/3*255),blue=Math.round((color[a+2]+color[b+2]+color[c+2])/3*255),green=Math.round((color[a+1]+color[b+1]+color[c+1])/3*255);colors.push((r|(green<<8)|(blue<<16)|0xff000000)>>>0);}
  const round=v=>Math.round(v*1e6)/1e6;
  const data={name,surface,positions:pos.map(round),normals:norm.map(round),triangles,colors};
  writeFileSync(new URL(name+'.json',destination),JSON.stringify(data));
  g.computeBoundingBox();console.log(name,colors.length+' 个三角形',g.boundingBox.min.toArray(),g.boundingBox.max.toArray());
}

// 连续火体用多段旋转轮廓建立，外轮廓沿高度翻卷，不依靠细线撑起形状。
{
  const points=[[0,0],[.42,.08],[.73,.23],[.8,.43],[.69,.67],[.58,.93],[.5,1.18],[.36,1.43],[.26,1.67],[.14,1.9],[.035,2.13],[0,2.25]];
  const g=lathe(points,20),p=g.attributes.position;
  for(let i=0;i<p.count;i++){const z=p.getZ(i),x=p.getX(i),y=p.getY(i),a=Math.atan2(y,x),r=1+.13*Math.sin(a*3+z*4)+.07*Math.sin(a*5-z*3);p.setXYZ(i,x*r+.1*Math.sin(z*2.8)*z,y*r+.06*Math.sin(z*4),z);}
  g.computeVertexNormals();exportModel('flame',[g],1);
}

// 浪头具有封闭的厚截面：前沿、卷起的浪唇、内侧水体和后坡都是真实曲面。
{
  const profile=[[-.48,0],[-.4,.2],[-.25,.5],[-.08,.74],[.1,.83],[.24,.76],[.25,.61],[.15,.52],[.07,.59],[.08,.67],[-.01,.67],[-.07,.49],[.02,.24],[.18,0]];
  const pos=[],tri=[],width=36,rows=profile.length;
  for(let i=0;i<=width;i++)for(let j=0;j<rows;j++){
    const x=i/width*2-1,arch=Math.pow(Math.max(0,1-x*x),.62),[y,z]=profile[j];
    pos.push(x,y*arch+.06*Math.cos(x*7),z*arch*(1+.05*Math.sin(x*11)));
  }
  for(let i=0;i<width;i++)for(let j=0;j<rows;j++){const a=i*rows+j,b=i*rows+(j+1)%rows,c=(i+1)*rows+(j+1)%rows,d=(i+1)*rows+j;tri.push(a,b,c,a,c,d);}
  exportModel('wave',[mesh(pos,tri)],2);
}

// 岩峰的断层、偏斜峰顶与侧脊组成完整体量，保留石材需要的硬边。
{
  const pos=[],tri=[],sides=12,levels=[0,.12,.32,.53,.7,.85,1];
  for(let j=0;j<levels.length;j++)for(let i=0;i<sides;i++){
    const z=levels[j],a=i/sides*Math.PI*2,r=Math.pow(1-z,.8)*(1+.16*Math.sin(i*3.1+j*.9));
    pos.push(Math.cos(a)*r+z*.2,Math.sin(a)*r-z*.12,z*2);
  }
  for(let j=0;j<levels.length-1;j++)for(let i=0;i<sides;i++){const a=j*sides+i,b=j*sides+(i+1)%sides,c=(j+1)*sides+(i+1)%sides,d=(j+1)*sides+i;tri.push(a,b,c,a,c,d);}
  const g=mesh(pos,tri).toNonIndexed();g.computeVertexNormals();
  exportModel('peak',[g],3,previews);
}

// 同一封闭云团有大小不同的隆起，雾气材质进一步控制疏密和软边。
{
  const g=paint(new T.SphereGeometry(1,24,16)),p=g.attributes.position;
  for(let i=0;i<p.count;i++){const x=p.getX(i),y=p.getY(i),z=p.getZ(i),r=1+.13*Math.sin(x*6+y*3)*Math.cos(z*5)+.08*Math.sin(y*9-z*4);p.setXYZ(i,x*r,y*r,z*r);}
  g.computeVertexNormals();exportModel('cloud',[g],6);
}

// 盘龙由连续躯干、面部、鹿角、须、背鳍和四肢构成，尾部逐渐收尖。
{
  const parts=[],path=new T.CatmullRomCurve3([V(-.7,.1,.08),V(-.4,.52,.2),V(.22,.56,.4),V(.57,.1,.62),V(.18,-.42,.78),V(-.42,-.26,.98),V(-.18,.1,1.18),V(.27,.12,1.25)]);
  const body=paint(new T.TubeGeometry(path,64,.085,8,false),0xb1e3ca),p=body.attributes.position;
  for(let i=0;i<p.count;i++){const t=Math.floor(i/9)/64,center=path.getPointAt(t),v=V(p.getX(i),p.getY(i),p.getZ(i));v.sub(center).multiplyScalar(.2+.8*Math.sin(t*Math.PI*.5)).add(center);p.setXYZ(i,...v.toArray());}
  body.computeVertexNormals();parts.push(body);
  const head=path.getPointAt(1);
  const at=(x,y,z)=>head.clone().add(V(x,y,z)).toArray();
  parts.push(sphere(at(.035,0,.01),[.145,.105,.095]),sphere(at(.155,0,-.035),[.13,.07,.045],0xe4efd0));
  parts.push(sphere(at(.2,-.01,-.068),[.105,.054,.018],0x263a31));
  for(const s of [-1,1]){
    parts.push(sphere(at(.072,s*.087,.04),[.035,.018,.026],0xfce8a1,10,6));
    parts.push(sphere(at(.085,s*.1,.043),[.014,.008,.016],0x122b22,8,5));
    parts.push(tube([at(-.04,s*.06,.08),at(-.13,s*.11,.22),at(-.08,s*.19,.31)],.018,12,0xf3dcaa));
    parts.push(tube([at(-.12,s*.1,.2),at(-.23,s*.16,.25),at(-.24,s*.21,.29)],.011,8,0xf3dcaa));
    parts.push(tube([at(.21,s*.05,-.02),at(.36,s*.15,-.05),at(.31,s*.28,.03)],.007,16,0xe7f6df,5));
  }
  for(let i=9;i<59;i+=4){const a=path.getPointAt(i/64),b=path.getPointAt((i+3)/64);parts.push(face([a.toArray(),b.toArray(),a.clone().add(V(-.035,0,.16)).toArray()],0xf0d495));}
  for(const t of [.4,.75])for(const s of [-1,1]){
    const a=path.getPointAt(t),b=a.clone().add(V(-.05,s*.17,-.11)),c=b.clone().add(V(.1,s*.09,-.09));
    parts.push(limb(a.toArray(),b.toArray(),.04,0xb3d7ba),limb(b.toArray(),c.toArray(),.027,0xe5d4a1));
    for(let claw=0;claw<3;claw++)parts.push(limb(c.toArray(),c.clone().add(V(.09,s*(claw-1)*.04,-.04)).toArray(),.009,0xf5e8c0));
  }
  exportModel('dragon',parts,7);
}

// 真身有面部、额饰、肩甲、束腰、裙甲与足部；右臂留给游戏中的真实掌击动作。
{
  const parts=[];
  const torso=lathe([[.16,.27],[.195,.4],[.2,.48],[.255,.61],[.26,.66],[.18,.71]],20).scale(1,.57,1);parts.push(torso);
  parts.push(sphere([0,0,.825],[.105,.09,.13]),limb([0,0,.68],[0,0,.76],.065));
  parts.push(sphere([0,-.09,.83],[.025,.035,.046],0xc4c0aa));
  for(const s of [-1,1]){
    parts.push(sphere([s*.047,-.083,.858],[.037,.012,.012],0x5a4c32,10,5));
    parts.push(sphere([s*.044,-.091,.847],[.022,.005,.006],0x24202a,10,5));
    parts.push(sphere([s*.107,-.005,.82],[.022,.025,.047],0xd8c69b,10,6));
    parts.push(sphere([s*.285,0,.65],[.105,.08,.075],0xd1bb87));
    parts.push(limb([s*.13,0,.29],[s*.17,0,.15],.08),limb([s*.17,0,.15],[s*.19,-.02,.04],.06));
    parts.push(sphere([s*.19,-.055,.035],[.075,.1,.037],0xc6b98d));
    for(let i=0;i<3;i++)parts.push(face([[s*(.04+i*.065),-.105,.4],[s*(.1+i*.065),-.1,.4],[s*(.16+i*.06),-.12,.23],[s*(.08+i*.065),-.14,.23]],i%2?0xada98d:0xe1d4b1));
    parts.push(face([[s*.04,-.035,.93],[s*.11,.015,.97],[s*.14,.02,1.065],[s*.06,-.01,1.02]],0xe5d8ab));
  }
  parts.push(sphere([0,-.087,.89],[.013,.006,.022],0xffedac,10,6));
  parts.push(limb([-.285,0,.65],[-.4,-.02,.48],.064),limb([-.4,-.02,.48],[-.36,-.11,.36],.055));
  parts.push(sphere([-.36,-.11,.35],[.055,.035,.065]));
  for(let i=0;i<5;i++)parts.push(limb([-.36+(i-2)*.018,-.11,.32],[-.36+(i-2)*.02,-.105,.26],.009));
  const belt=lathe([[.192,.385],[.205,.39],[.205,.415],[.195,.42]],20,0x877850).scale(1,.65,1);parts.push(belt);
  exportModel('avatar',parts,7);
}
