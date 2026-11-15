/** 竖立水幕保持连续厚面、弯曲上沿与向下流动的表面，显示和防御取同一列控制。 */
import * as T from 'three';
import {write, structure, skin, motion} from './form_export.mjs';
import {wallPart, column} from './forms_solid_walls.mjs';

/** 生成有内外表面、两端封口和收拢边缘的液态幕面，不借用冰晶柱或水平方向的海浪。 */
export function makeWaterCurtain() {
  const count=17, spacing=.28, half=(count-1)*spacing/2;
  const controls=Array.from({length:count},(_,i)=>({Id:`water_${i}`,Position:[i*spacing-half,0,0],Rotation:[0,0,0,1]}));
  // 截面从厚水脚向上延伸，上沿稍向迎敌侧翻出；背面连回地面，形成封闭厚水层。
  const section=[[-.40,-.07],[-.32,.17],[-.24,.55],[-.20,1.0],[-.22,1.45],[-.33,1.78],[-.41,1.98],
    [-.36,2.13],[-.21,2.20],[.01,2.16],[.14,2.04],[.20,1.78],[.25,1.45],[.30,1.0],[.36,.55],[.44,.17],[.46,-.07]];
  const bend=(x,z)=>.045*Math.sin(x*2.6+z*1.9)*Math.max(0,z)/2.2;
  const rings=controls.map((c,i)=>section.map(([y,z])=>{
    const x=c.Position[0], edge=i===0||i===count-1?.63:1;
    return [x,y*edge+bend(x,z),z+.045*Math.sin(x*2.2)*Math.max(0,z)/2.2];
  }));
  const geometry=column(rings,section.length), position=geometry.getAttribute('position');
  const uv=[], surface=[];
  for(let i=0;i<position.count;i++) {
    const x=position.getX(i),z=position.getZ(i);
    // 水面的表面坐标向下展开，沿用通用水材质即可表现下流；厚度单独交给透光着色。
    uv.push((x+half)/(half*2),1-z/2.2);
    const crest=T.MathUtils.smoothstep(z,1.9,2.15),broken=.45+.25*Math.sin(x*4.7)+.16*Math.sin(x*9.3);
    surface.push(.5,Math.max(0,crest*broken));
  }
  geometry.setAttribute('uv',new T.Float32BufferAttribute(uv,2));
  geometry.setAttribute('formSurface',new T.Float32BufferAttribute(surface,2));
  geometry.userData.smoothSeams=true;
  skin(geometry,p=>{
    const at=T.MathUtils.clamp((p.x+half)/spacing,0,count-1),lower=Math.min(count-2,Math.floor(at)),u=at-lower;
    return [[lower,1-u],[lower+1,u]];
  });
  const parts=[wallPart('water_sheet',geometry,'water')],contacts=[];
  // 接触芯保留在可见水层内；列之间相交，端部以外和真实顶沿之上仍可通过。
  for(let i=0;i<count;i++)for(let j=0;j<4;j++) {
    const x=controls[i].Position[0],a=.19+j*.43,b=a+.43;
    const edge=i===0||i===count-1?.63:1;
    const centre=z=>[x,(.055-.19*Math.pow(z/2.1,4))*edge+bend(x,z),z+.045*Math.sin(x*2.2)*z/2.2];
    contacts.push({Id:`water_${i}_${j}`,Part:0,A:centre(a),B:centre(b),Radius:i===0||i===count-1?.13:.17,
      AControl:controls[i].Id,BControl:controls[i].Id});
  }
  const run=motion('run',1.45,true,controls,48,(i,p,c)=>({rotation:new T.Quaternion().setFromAxisAngle(new T.Vector3(1,0,0),
    .018*Math.sin(p*Math.PI*2-i*.48)).toArray()}));
  const anchors=[{Id:'left',Part:0,Position:[-half,0,0],Direction:[0,0,1],Control:controls[0].Id},
    {Id:'right',Part:0,Position:[half,0,0],Direction:[0,0,1],Control:controls[count-1].Id},
    {Id:'crest',Part:0,Position:[0,-.21,2.2],Direction:[0,0,1],Control:controls[(count-1)/2].Id}];
  write('water_curtain',parts,anchors,contacts,controls,[run],true,{Forward:[0,1,0],Up:[0,0,1],HideBelowGround:true},
    structure('surface','forms_water_curtain.mjs','makeWaterCurtain',{count,spacing,section},
      {water_sheet:['terrain.wall','barrier.face','fluid.body','fluid.crest']},[]));
}
