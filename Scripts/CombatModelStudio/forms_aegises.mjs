/** 随身护盾采用中空厚壳；形体与实际接触共同运动，不复用手持兵器盾。 */
import * as T from 'three';
import {write, structure, motion} from './form_export.mjs';
import {wallPart} from './forms_solid_walls.mjs';

/** 五种原来源各自有明确主体；这里只共享封闭壳面的制作与接触规则。 */
export function makeAegises() {
  makeShield('spirit_aegis','aegis_jade','petals');
  makeShield('flame_aegis','flame','flame');
  makeShield('wind_aegis','wind','flow');
  makeShield('lightning_aegis','aegis_lightning','facets');
  makeShield('solar_aegis','aegis_solar','solar');
}

/** 球面点使用地图竖直轴；焰瓣起伏和风带扭转同时进入网格与接触。 */
function point(radius,theta,phi,kind) {
  if(kind==='flow')theta+=.10*Math.sin(phi*2)*Math.sin(theta);
  if(kind==='flame')radius-=.05-.05*Math.sin(phi*8+theta*1.4)*Math.sin(theta);
  return [Math.sin(theta)*Math.cos(phi)*radius,Math.sin(theta)*Math.sin(phi)*radius,.65+Math.cos(theta)*radius*.88];
}

/** 封闭厚壳片，包含内外表面与四侧收口，内部不被填成伤害球。 */
function shell(kind,thetaStart,thetaEnd,phiStart,phiEnd,rows,columns) {
  const positions=[],uv=[],triangles=[];
  const vertex=(r,t,p)=>point(r,thetaStart+(thetaEnd-thetaStart)*t,phiStart+(phiEnd-phiStart)*p,kind);
  const quad=(points,coords)=>{
    const at=positions.length/3;
    for(let i=0;i<4;i++){positions.push(...points[i]);uv.push(...coords[i]);}
    triangles.push(at,at+1,at+2,at,at+2,at+3);
  };
  for(let i=0;i<rows;i++)for(let j=0;j<columns;j++) {
    const a=i/rows,b=(i+1)/rows,c=j/columns,d=(j+1)/columns;
    // 球面纬向再经向为外法线；内表面反向。
    quad([vertex(1.90,a,c),vertex(1.90,b,c),vertex(1.90,b,d),vertex(1.90,a,d)],[[c,a],[c,b],[d,b],[d,a]]);
    quad([vertex(1.40,a,c),vertex(1.40,a,d),vertex(1.40,b,d),vertex(1.40,b,c)],[[c,a],[d,a],[d,b],[c,b]]);
  }
  for(let i=0;i<rows;i++) {
    const a=i/rows,b=(i+1)/rows;
    quad([vertex(1.4,a,0),vertex(1.4,b,0),vertex(1.9,b,0),vertex(1.9,a,0)],[[0,a],[0,b],[1,b],[1,a]]);
    quad([vertex(1.4,a,1),vertex(1.9,a,1),vertex(1.9,b,1),vertex(1.4,b,1)],[[0,a],[1,a],[1,b],[0,b]]);
  }
  for(let j=0;j<columns;j++) {
    const c=j/columns,d=(j+1)/columns;
    quad([vertex(1.4,0,c),vertex(1.9,0,c),vertex(1.9,0,d),vertex(1.4,0,d)],[[c,0],[c,1],[d,1],[d,0]]);
    quad([vertex(1.4,1,c),vertex(1.4,1,d),vertex(1.9,1,d),vertex(1.9,1,c)],[[c,0],[d,0],[d,1],[c,1]]);
  }
  const geometry=new T.BufferGeometry();geometry.setAttribute('position',new T.Float32BufferAttribute(positions,3));
  geometry.setAttribute('uv',new T.Float32BufferAttribute(uv,2));geometry.setIndex(triangles);
  return geometry;
}

/** 弧片、焰瓣、风带和雷晶面分别围合；接触留在实际厚面内部。 */
function makeShield(id,surface,kind) {
  const parts=[],controls=[],contacts=[];
  const flow=kind==='flow',facets=kind==='facets',panels=flow?5:facets?1:8;
  for(let panel=0;panel<panels;panel++) {
    const control=`panel_${panel}`,a=flow?panel*Math.PI/panels:0,b=flow?(panel+1)*Math.PI/panels:Math.PI;
    const c=flow||facets?0:panel*Math.PI*2/panels,d=flow||facets?Math.PI*2:(panel+1)*Math.PI*2/panels;
    const rows=flow?2:10,columns=flow||facets?24:4;
    controls.push({Id:control,Position:[0,0,.65],Rotation:[0,0,0,1]});
    parts.push(wallPart(control,shell(kind,a,b,c,d,rows,columns),surface,[0,0,.65],control));
    const longitudes=flow||facets?24:3;
    for(let j=0;j<longitudes;j++)for(let i=0;i<rows;i++) {
      const phi=c+(d-c)*(j+.5)/longitudes;
      contacts.push({Id:`${control}_${j}_${i}`,Part:panel,A:point(1.65,a+(b-a)*i/rows,phi,kind),
        B:point(1.65,a+(b-a)*(i+1)/rows,phi,kind),Radius:.23,AControl:control,BControl:control});
    }
  }
  if(kind==='solar') {
    const control='sun_wheel',radius=1.73,width=.17,index=parts.length;
    controls.push({Id:control,Position:[0,0,.65],Rotation:[0,0,0,1]});
    parts.push(wallPart(control,new T.TorusGeometry(radius,width,16,64).translate(0,0,.65),surface,[0,0,.65],control));
    for(let i=0;i<48;i++) {
      const a=i*Math.PI*2/48,b=(i+1)*Math.PI*2/48;
      contacts.push({Id:`wheel_${i}`,Part:index,A:[radius*Math.cos(a),radius*Math.sin(a),.65],
        B:[radius*Math.cos(b),radius*Math.sin(b),.65],Radius:.15,AControl:control,BControl:control});
    }
  }
  const run=motion('run',kind==='flame'?.8:1.6,true,controls,40,(i,p)=>({
    rotation:new T.Quaternion().setFromAxisAngle(new T.Vector3(0,0,1),
      (kind==='flow'?.025:kind==='flame'?.012:.006)*Math.sin(p*Math.PI*2+i*.7)).toArray()}));
  write(id,parts,[{Id:'crown',Part:0,Position:[0,0,2.32],Direction:[0,0,1],Control:controls[0].Id},
    {Id:'centre',Part:0,Position:[0,0,.65],Direction:[0,1,0],Control:controls[0].Id}],contacts,controls,[run],false,
    {Forward:[0,1,0],Up:[0,0,1],HideBelowGround:true},
    structure('surface','forms_aegises.mjs','makeAegises',{outer:1.90,inner:1.40,centre:.65,vertical:.88,panels},
      Object.fromEntries(parts.map(p=>[p.Id,['barrier.face',kind==='flow'?'wind.flow':kind==='flame'?'fire.body':kind==='solar'?'sun.wheel':facets?'energy.facet':'energy.panel']])),[]));
}
