import * as T from 'three';
import {part, structure, write} from './form_export.mjs';

/** 连续的窄喉、翻卷外焰及更短的内焰，共用厚度逐段增加的轮廓。 */
export function makeFoxBreath() {
  const envelope = [[0,.003],[.15,.105],[.54,.22],[1.06,.35],[1.64,.50],[2.28,.65],
    [2.94,.76],[3.40,.74],[3.74,.52],[4.02,.29],[4.20,.003]];
  const inner = envelope.slice(0,9).map(([z,r],i) => [z * .94, i === 8 ? .004 : r * .47]);
  const build = (id, profile, surfaceId, wave) => {
    const mesh = new T.LatheGeometry(profile.map(([z,r]) => new T.Vector2(r,z)), 32).rotateX(Math.PI / 2);
    const points = mesh.getAttribute('position');
    for (let i = 0; i < points.count; i++) {
      const z = points.getZ(i), angle = Math.atan2(points.getY(i),points.getX(i));
      const turn = 1 + wave * Math.sin(angle * 4 - z * 4) * Math.sin(z * .65);
      points.setXY(i, points.getX(i) * turn, points.getY(i) * turn * .91 + .075 * (1 - Math.cos(z * 1.3)));
    }
    mesh.userData.smoothSeams = true;
    return {...part(id, mesh, surfaceId), UseMatterSurface:false};
  };
  const parts = [build('envelope',envelope,'flame_body',.16), build('core',inner,'meteor_crown',.12)];
  const contacts = [[.28,.59,.16],[.69,1.14,.27],[1.26,1.75,.40],[1.93,2.40,.53],
    [2.52,3.12,.64],[3.22,3.60,.63],[3.67,3.92,.25]].map(([a,b,Radius],i) =>
    ({Id:`breath_${i}`,Part:0,A:[0,.075*(1-Math.cos(a*1.3)),a],
      B:[0,.075*(1-Math.cos(b*1.3)),b],Radius}));
  write('fox_breath',parts,[{Id:'mouth',Part:0,Position:[0,0,0],Direction:[0,0,1]}],
    contacts,[],[],false,{Forward:[0,0,1],Up:[0,1,0]},
    structure('volume','forms_fox_breath.mjs','makeFoxBreath',{envelope,inner},
      {envelope:['breath.body','breath.contact'],core:['breath.heat']}));
}
