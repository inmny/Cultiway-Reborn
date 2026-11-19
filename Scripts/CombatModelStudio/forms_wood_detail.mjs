/** 一根细梗和两片折叶；独立显示部位沿用母枝控制，不登记额外接触。 */
import * as T from 'three';
import {formation, part, skin} from './form_export.mjs';

/** 从根枝上的真实接点起笔，沿一条连续细梗展开两片朝向不同的叶。 */
export function woodSprig(id, control, path, start, end, up) {
  const shoot=new T.CatmullRomCurve3(path);
  const stem=new T.TubeGeometry(shoot,9,.038,6,false);
  const stemPositions=stem.getAttribute('position'), stemUv=stem.getAttribute('uv');
  for(let i=0;i<stemPositions.count;i++) {
    const t=stemUv.getX(i), centre=shoot.getPointAt(t);
    const p=new T.Vector3().fromBufferAttribute(stemPositions,i).sub(centre).multiplyScalar(1-.94*t).add(centre);
    stemPositions.setXYZ(i,...p.toArray());
  }
  formation(stem,(_,i)=>T.MathUtils.lerp(start,end,stemUv.getX(i)),(_,__,i)=>shoot.getPointAt(stemUv.getX(i)));

  const positions=[], uv=[], indices=[], steps=[], bases=[];
  for(const [i,t] of [.57,.86].entries()) {
    const root=shoot.getPointAt(t), tangent=shoot.getTangentAt(t);
    const side=new T.Vector3().crossVectors(up,tangent).normalize();
    const axis=tangent.multiplyScalar(.85).addScaledVector(side,i===0?-.78:.58).addScaledVector(up,.12).normalize();
    const width=new T.Vector3().crossVectors(up,axis).normalize(), normal=new T.Vector3().crossVectors(axis,width);
    const length=i===0?.31:.25, halfWidth=length*.26;
    const middle=root.clone().addScaledVector(axis,length*.47);
    const corners=[root,middle.clone().addScaledVector(width,-halfWidth),root.clone().addScaledVector(axis,length),
      middle.clone().addScaledVector(width,halfWidth),middle.clone().addScaledVector(normal,.038),middle.clone().addScaledVector(normal,-.01)];
    const offset=positions.length/3;
    const open=T.MathUtils.lerp(start,end,t);
    for(const [j,point] of corners.entries()) {
      positions.push(...point.toArray());bases.push(root);
      steps.push(T.MathUtils.lerp(open,Math.min(1,open+.13),j===0?0:j===2?1:.44));
    }
    uv.push(.5,0,0,.47,.5,1,1,.47,.5,.47,.5,.47);
    indices.push(...[0,1,4,1,2,4,2,3,4,3,0,4,1,0,5,2,1,5,3,2,5,0,3,5].map(j=>j+offset));
  }
  const leaves=new T.BufferGeometry();
  leaves.setAttribute('position',new T.Float32BufferAttribute(positions,3));
  leaves.setAttribute('uv',new T.Float32BufferAttribute(uv,2));leaves.setIndex(indices);
  formation(leaves,(_,i)=>steps[i],(_,__,i)=>bases[i]);
  return [part(id,skin(stem,()=>[[control,1]]),'wood',path[0].toArray()),
    part(`${id}_leaf`,skin(leaves,()=>[[control,1]]),'wood_leaf',path[0].toArray())];
}
