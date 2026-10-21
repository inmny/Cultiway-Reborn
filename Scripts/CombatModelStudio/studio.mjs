/** 同一导出模型的交互检查工具，材质公式与游戏主体材质保持相同方向。 */
import * as T from 'three';
import {OrbitControls} from 'three/addons/controls/OrbitControls.js';
const renderer=new T.WebGLRenderer({antialias:true,preserveDrawingBuffer:true});renderer.setSize(innerWidth,innerHeight);renderer.setPixelRatio(Math.min(devicePixelRatio,2));document.body.append(renderer.domElement);
const scene=new T.Scene();scene.background=new T.Color('#101923');
const camera=new T.PerspectiveCamera(36,innerWidth/innerHeight,.01,100);camera.position.set(3.3,3,5.2);
const controls=new OrbitControls(camera,renderer.domElement);controls.target.set(0,1,0);controls.update();
const floor=new T.Mesh(new T.PlaneGeometry(70,70),new T.MeshBasicMaterial({color:'#17232e'}));floor.rotation.x=-Math.PI/2;floor.position.y=-.06;scene.add(floor);
const models=[['flame','火体','暗焰与热芯',0xff6420],['wave','厚浪','水体、卷唇与泡沫',0x3287cf],['peak','岩峰','断层、侧脊与受光面',0xc79b66],['cloud','云团','体积与疏密',0x91a9bb],['dragon','盘龙','连续躯干、角须与四肢',0x61c9a0],['avatar','真身','面部、肩甲与裙甲',0xe9bb6e]];
const roots=[],labels=[];
const vertex=`varying vec3 p;varying vec3 n;varying vec3 eye;varying vec3 paint;void main(){p=position;n=normalize(normalMatrix*normal);vec4 v=modelViewMatrix*vec4(position,1.0);eye=-v.xyz;paint=color;gl_Position=projectionMatrix*v;}`;
const fragment=`uniform float time;uniform float kind;uniform vec3 tint;uniform vec3 minimum;uniform vec3 extent;varying vec3 p;varying vec3 n;varying vec3 eye;varying vec3 paint;
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}float noise(vec2 p){vec2 i=floor(p),f=fract(p);f=f*f*(3.-2.*f);return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+1.),f.x),f.y);}float billow(vec2 p){return noise(p)*.55+noise(p*2.07+11.3)*.3+noise(p*4.19-7.1)*.15;}
void main(){vec3 q=(p-minimum)/max(extent,vec3(.001));float f=dot(normalize(n),normalize(eye)),light=max(0.,dot(normalize(n),normalize(vec3(-.5,.65,.8))));float v=billow(q.xy*4.+q.z*2.7+vec2(time*.11,-time*.17));vec3 rgb=tint*paint;float a=1.;
if(kind<1.5){v=billow(vec2(q.x*5.+q.y*1.7,q.z*4.-time*1.6));float heat=clamp(v*1.35-q.z*.2,0.,1.);vec3 pigment=rgb/max(.08,max(rgb.r,max(rgb.g,rgb.b)));rgb=mix(pigment*.12,pigment,smoothstep(.18,.65,heat));rgb=mix(rgb,vec3(1,.92,.48),smoothstep(.63,.91,heat)*(1.-q.z*.6));a=.88*smoothstep(.15,.48,v)*smoothstep(-.08,.35,f);}
else if(kind<2.5){float foam=smoothstep(.63,.86,billow(q.xy*18.+q.z*8.+vec2(time*.3,-time*.5)))*smoothstep(.42,.9,q.z);foam=max(foam,smoothstep(.87,1.,q.z)*.7);rgb=mix(rgb*(.32+v*.85),vec3(.8,.94,1.),foam);a=.82;}
else if(kind<3.5){float ridge=abs(noise(q.xy*13.+q.z*8.)-.5)*2.;rgb*=mix(.45,1.15,smoothstep(.002,.035,ridge))*(.82+.18*sin((q.z+q.x*.19)*55.+v*5.))*(.28+light*.72);a=smoothstep(-.12,.1,f);}
else if(kind<6.5){rgb*=.35+v*.7;a=.55*smoothstep(.18,.66,v)*smoothstep(0.,.5,abs(f));}
else{rgb*=.4+v*.65;a=.75*(.55+v*.45)*smoothstep(0.,.35,abs(f));}gl_FragColor=vec4(rgb,a);}`;
for(let index=0;index<models.length;index++){
  const [name,title,desc,color]=models[index],data=await(await fetch('../../Content/CombatModels/'+name+'.json')).json();
  const g=new T.BufferGeometry();g.setAttribute('position',new T.Float32BufferAttribute(data.positions,3));g.setAttribute('normal',new T.Float32BufferAttribute(data.normals,3));
  const colors=new Array(data.positions.length);for(let i=0;i<data.triangles.length;i++){const packed=data.colors[Math.floor(i/3)],at=data.triangles[i]*3;colors[at]=(packed&255)/255;colors[at+1]=((packed>>>8)&255)/255;colors[at+2]=((packed>>>16)&255)/255;}g.setAttribute('color',new T.Float32BufferAttribute(colors,3));g.setIndex(data.triangles);g.computeBoundingBox();
  const box=g.boundingBox,extent=box.getSize(new T.Vector3());
  const material=new T.ShaderMaterial({uniforms:{time:{value:1.2},kind:{value:data.surface},tint:{value:new T.Color(color).convertLinearToSRGB()},minimum:{value:box.min},extent:{value:extent}},vertexShader:vertex,fragmentShader:fragment,vertexColors:true,transparent:true,depthWrite:false,side:T.DoubleSide});
  const object=new T.Mesh(g,material),root=new T.Group();root.add(object);root.rotation.x=-Math.PI/2;root.scale.setScalar(2.8/Math.max(...extent.toArray()));object.position.sub(new T.Vector3((box.min.x+box.max.x)/2,(box.min.y+box.max.y)/2,box.min.z));
  scene.add(root);roots.push(root);
  const label=document.createElement('div');label.className='label';label.innerHTML='<strong>'+title+'</strong><small>'+desc+'</small>';document.body.append(label);labels.push(label);
}
let selected=-1;
for(const [index,title] of [[-1,'全部'],...models.map((m,i)=>[i,m[1]])]){
  const button=document.createElement('button');button.textContent=title;button.setAttribute('aria-pressed',index===-1);button.onclick=()=>{selected=index;document.querySelectorAll('button').forEach(b=>b.setAttribute('aria-pressed',b===button));};document.getElementById('choices').append(button);
}
addEventListener('resize',()=>{camera.aspect=innerWidth/innerHeight;camera.updateProjectionMatrix();renderer.setSize(innerWidth,innerHeight);});
function frame(){
  controls.update();renderer.setScissorTest(false);renderer.setViewport(0,0,innerWidth,innerHeight);renderer.clear();renderer.setScissorTest(true);
  const width=innerWidth/(selected===-1?3:1),height=(innerHeight-140)/(selected===-1?2:1);
  for(let i=0;i<roots.length;i++){
    labels[i].hidden=selected!==-1&&selected!==i;if(labels[i].hidden)continue;
    const col=selected===-1?i%3:0,row=selected===-1?Math.floor(i/3):0,x=col*width,y=innerHeight-100-(row+1)*height;
    roots.forEach((r,n)=>r.visible=n===i);camera.aspect=width/height;camera.updateProjectionMatrix();renderer.setViewport(x,y,width,height);renderer.setScissor(x,y,width,height);renderer.render(scene,camera);
    labels[i].style.left=(x+width/2)+'px';labels[i].style.top=(innerHeight-y-43)+'px';
  }
  requestAnimationFrame(frame);
}frame();window.modelStudioReady=true;
