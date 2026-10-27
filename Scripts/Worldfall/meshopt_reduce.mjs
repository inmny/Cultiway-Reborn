// 实验入口：原始表面输入，每档独立减面，结果仍要按固定高面来源验形。
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { MeshoptSimplifier } from '../../Tools/Worldfall/meshopt-js/meshopt_simplifier.mjs';
const [folder, method, ...counts] = process.argv.slice(2);
if (!folder || !['update', 'geometry', 'sloppy', 'sloppy-budget'].includes(method) || !counts.length) throw Error('输入目录 update|geometry|sloppy|sloppy-budget 目标面数...');
await MeshoptSimplifier.ready;
const float = name => { const b=fs.readFileSync(path.join(folder,name)); return new Float32Array(b.buffer.slice(b.byteOffset,b.byteOffset+b.byteLength)); };
const b=fs.readFileSync(path.join(folder,'faces.u32'));
const originalIndices=new Uint32Array(b.buffer.slice(b.byteOffset,b.byteOffset+b.byteLength));
const originalPositions=float('vertices.f32');
const locks=new Uint8Array(fs.readFileSync(path.join(folder,'locks.u8')));
const normals=fs.existsSync(path.join(folder,'normals.f32')) ? float('normals.f32') : null;
const rows=[];
for (const count of counts.map(Number)) {
  const indices=originalIndices.slice(), positions=originalPositions.slice(), attrs=method==='update'?normals?.slice():null;
  let reduced,error;
  const begin=performance.now();
  if (method.startsWith('sloppy')) [reduced,error]=MeshoptSimplifier.simplifySloppy(indices,positions,3,locks,count*3,method==='sloppy'?.01:1);
  else {
    const [n,e]=MeshoptSimplifier.simplifyWithUpdate(indices,positions,3,attrs || new Float32Array(),attrs?3:0,attrs?[.5,.5,.5]:[],locks,count*3,method==='update'?.01:1,['Permissive']);
    reduced=indices.slice(0,n); error=e;
  }
  const prefix=path.join(folder,method+'-'+count);
  if (fs.existsSync(prefix+'.u32')) throw Error('拒绝覆盖已有输出 '+prefix);
  fs.writeFileSync(prefix+'.u32',Buffer.from(reduced.buffer,reduced.byteOffset,reduced.byteLength));
  fs.writeFileSync(prefix+'.f32',Buffer.from(positions.buffer));
  const row={target:count,triangles:reduced.length/3,error,seconds:(performance.now()-begin)/1000};
  rows.push(row); console.log(JSON.stringify(row));
}
const module=new URL('../../Tools/Worldfall/meshopt-js/meshopt_simplifier.mjs',import.meta.url);
fs.writeFileSync(path.join(folder,method+'-record.json'),JSON.stringify({
  method, source:'https://raw.githubusercontent.com/zeux/meshoptimizer/master/js/meshopt_simplifier.js',
  module_sha256:crypto.createHash('sha256').update(fs.readFileSync(module)).digest('hex'),
  input_triangles:originalIndices.length/3,input_vertices:originalPositions.length/3,
  locked_vertices:locks.reduce((n,x)=>n+(x===1),0),error_limit:method==='update'||method==='sloppy'?.01:1,
  normals_weight:method==='update'?.5:null,outputs:rows
},null,2)+'\n');
