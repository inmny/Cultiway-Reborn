/** 低矮闭合后流：横纵控制共同适应地表，前沿与薄沫来自真实水层表面。 */
import * as T from 'three';
import {mergeGeometries} from 'three/addons/utils/BufferGeometryUtils.js';
import {part,write,skin,surfaceData,motion} from './form_export.mjs';

const across=5,along=6,halfWidth=3,start=-2,length=8;
/** 沿水层位置取得四项双线性控制，不按整个浪体做刚性抬升。 */
function influences(x,y){
  const u=T.MathUtils.clamp((x/halfWidth+1)*.5*(across-1),0,across-1),v=T.MathUtils.clamp((y-start)/length*(along-1),0,along-1);
  const a=Math.min(across-2,Math.floor(u)),b=Math.min(along-2,Math.floor(v)),s=u-a,t=v-b;
  return [[b*across+a,(1-s)*(1-t)],[b*across+a+1,s*(1-t)],[(b+1)*across+a,(1-s)*t],[(b+1)*across+a+1,s*t]];
}
/** 后缘收薄，前沿略有堆水，岸边不做直尺般的长硬边。 */
function point(u,v,lower=false){
  const edge=Math.pow(Math.max(0,1-u*u),.28),front=Math.exp(-Math.pow((v-.84)/.14,2));
  const width=halfWidth*(.72+.28*Math.sin(Math.PI*(.16+v*.76)))*(1+.035*Math.sin(v*15));
  const x=u*width,y=start+length*v+.12*Math.sin(u*4+v*6)*Math.pow(Math.abs(u),2);
  return new T.Vector3(x,y,lower?.025:.08+edge*(.07+.16*front)+.015*Math.sin(v*13+u*6)*edge);
}
export function makeFlow(){
  const columns=40,rows=64,stride=columns+1,faceCount=(rows+1)*stride;
  const positions=[],uv=[],triangles=[];
  for(let face=0;face<2;face++)for(let row=0;row<=rows;row++)for(let col=0;col<=columns;col++){
    const u=col/columns*2-1,v=row/rows,p=point(u,v,!!face);positions.push(...p.toArray());uv.push(col/columns,v);
  }
  for(let face=0;face<2;face++)for(let r=0;r<rows;r++)for(let c=0;c<columns;c++){
    const a=face*faceCount+r*stride+c,b=a+stride;triangles.push(...(face?[a,b,a+1,a+1,b,b+1]:[a,a+1,b,a+1,b+1,b]));
  }
  for(let r=0;r<rows;r++)for(const c of [0,columns]){const a=r*stride+c,b=a+stride,d=a+faceCount,e=b+faceCount;triangles.push(...(c?[a,d,b,d,e,b]:[a,b,d,d,b,e]));}
  for(let c=0;c<columns;c++)for(const r of [0,rows]){const a=r*stride+c,b=a+1,d=a+faceCount,e=b+faceCount;triangles.push(...(r?[a,b,d,d,b,e]:[a,d,b,d,e,b]));}
  const body=new T.BufferGeometry();body.setAttribute('position',new T.Float32BufferAttribute(positions,3));body.setAttribute('uv',new T.Float32BufferAttribute(uv,2));body.setIndex(triangles);
  skin(body,p=>influences(p.x,p.y));surfaceData(body,p=>Math.max(.035,p.z-.025),p=>Math.max(0,1-Math.abs((p.y-start)/length-.84)/.12)*.5);
  const controls=[];for(let y=0;y<along;y++)for(let x=0;x<across;x++)controls.push({Id:`flow_${y}_${x}`,Parent:null,Position:[(x/(across-1)*2-1)*halfWidth,start+y/(along-1)*length,0],Rotation:[0,0,0,1]});
  const parts=[part('water_layer',body,'water')],foam=[];
  for(let i=0;i<84;i++){
    const u=Math.sin(i*2.399963)*.97,v=.72+(Math.sin(i*8.31)*.5+.5)*.2,p=point(u,v);p.z+=.015;
    const g=new T.SphereGeometry(.035+(Math.sin(i*4.19)*.5+.5)*.045,7,5).scale(1.8,2.2,.2).translate(...p.toArray());
    foam.push(skin(g,q=>influences(q.x,q.y)));
  }
  parts.push(part('broken_foam',mergeGeometries(foam),'foam'));
  const bind=(x,y)=>influences(x,y).map(([i,Weight])=>({Control:controls[i].Id,Weight}));
  const contacts=[];for(let row=0;row<=16;row++)for(let col=0;col<across-1;col++){
    const v=row/16,a=point(col/(across-1)*2-1,v),b=point((col+1)/(across-1)*2-1,v);a.z=b.z=.145;
    contacts.push({Id:`water_${row}_${col}`,Part:0,A:a.toArray(),B:b.toArray(),Radius:.13,AInfluences:bind(a.x,a.y),BInfluences:bind(b.x,b.y)});
  }
  const anchors=controls.map((control,i)=>({Id:`surface_${i}`,Part:0,Position:[...control.Position.slice(0,2),.15],Direction:[0,1,0],Control:control.Id}));
  const animation=motion('run',1.6,true,controls,48,(i,p,c)=>({position:[c.Position[0],c.Position[1],.012*Math.sin(p*Math.PI*2+c.Position[1]*1.8)*Math.sin(Math.PI*(c.Position[1]-start)/length)]}));
  write('water_flow',parts,anchors,contacts,controls,[animation],true);
}
