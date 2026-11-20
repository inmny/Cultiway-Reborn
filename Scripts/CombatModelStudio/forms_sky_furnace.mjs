/** 青铜炉的真实杯口和熔融金属的闭合截面，共用实际姿态及接触资料。 */
import * as T from 'three';
import {mergeGeometries} from 'three/addons/utils/BufferGeometryUtils.js';
import {motion, part, structure, write} from './form_export.mjs';
import {phaseValue, rotation} from './form_skeleton.mjs';

const source = 'forms_sky_furnace.mjs';
const facing = {Forward:[0,0,1], Up:[0,1,0]};
const upright = [0,0,0,1];
const at = (r,t,y) => [r*Math.sin(t),y,r*Math.cos(t)];

/** 真实截面绕竖直轴组成内外壁，轮廓回到轴心封底，顶面始终敞开。 */
function furnace() {
  const profile = [[0,.15],[.40,.15],[.63,.31],[.83,.62],[.87,.89],[.79,1.12],[.64,1.26],[.62,1.41],
    [.55,1.41],[.54,1.32],[.63,1.18],[.71,.94],[.70,.67],[.59,.39],[.36,.28],[0,.28]];
  const swing = 'vessel';
  const controls = [{Id:swing, Position:[0,0,0], Rotation:upright}];
  const bronze = (id, geometry, surface = 'furnace_bronze') => ({...part(id,geometry,surface,[0,0,0],swing),UseMatterSurface:false});
  const wall = new T.LatheGeometry(profile.map(p=>new T.Vector2(...p)),48);
  wall.userData.smoothSeams = true;
  const collar = (r,y,thick) => new T.TorusGeometry(r,thick,8,48).rotateX(Math.PI/2).translate(0,y,0);
  const foot = a => new T.CylinderGeometry(.115,.177,.45,8).rotateZ(-.3)
    .rotateY(a).translate(...at(.57,a,.18));
  const handles = [-1,1].map(side=>new T.TorusGeometry(.19,.058,8,24,Math.PI*1.87).rotateY(Math.PI/2)
    .translate(side*.82,1.07,0));
  const lobes = Array.from({length:10},(_,i)=>{
    const a = i*Math.PI*.2;
    return new T.SphereGeometry(.10,10,7).scale(.78,1.85,.49).rotateY(a).translate(...at(.789,a,.72));
  });
  const pieces = [bronze('body',wall),bronze('rim',collar(.585,1.41,.061),'furnace_rim'),
    bronze('bands',mergeGeometries([collar(.817,.98,.034),collar(.714,.44,.042),...lobes]),'furnace_rim'),
    bronze('feet',mergeGeometries([.81,2.905,5].map(foot))),bronze('ears',mergeGeometries(handles))];
  const outlets = [[0,1.41,.59],[-.285,1.41,.518],[.285,1.41,.518]];
  const anchors = ['mouth','mouth.left','mouth.right'].map((id,i)=>({Id:id,Part:1,Control:swing,
    Position:outlets[i],Direction:[outlets[i][0]*.78,.36,1]}));
  const contacts = [[.59,1.41,.066],[.79,.86,.13],[.61,.39,.12]].flatMap(([radius,height,thickness],ring)=>
    Array.from({length:12},(_,i)=>({Id:`wall_${ring}_${i}`,Part:ring===0?1:0,A:at(radius,i*Math.PI/6,height),
      B:at(radius,(i+1)*Math.PI/6,height),Radius:thickness,AControl:swing,BControl:swing})));
  const duration = 3.72;
  const poses = [[0,[0,-.50,0,0]],[.21,[0,-.23,0,0]],[.69,[0,1.54,.12,0]],[1.03,[0,1.59,.13,9]],
    [1.45,[0,1.79,.33,57]],[1.83,[0,1.73,.50,69]],[2.33,[0,1.72,.42,65]],[2.91,[0,1.52,.20,9]],
    [3.31,[0,1.33,.15,0]],[duration,[0,-.49,.15,0]]].map(([t,p])=>[t/duration,p]);
  const marks = [{Id:'raised',At:.69},{Id:'tilt',At:1.03},{Id:'return',At:2.33},{Id:'upright',At:3.31}];
  const action = (id,tilt) => motion(id,duration,false,controls,76,(_i,p)=>{
    const [x,y,z,pitch] = phaseValue(p,poses);
    return {position:[x,y,z],rotation:rotation(tilt?pitch:0,0,0)};
  },marks);
  write('sky_furnace',pieces,anchors,contacts,controls,[action('lift_and_pour',true),action('hold_upright',false)],false,facing,
    structure('rigid',source,'makeSkyFurnace',{profile,outlets,timeline:poses},{body:['vessel.wall'],rim:['vessel.mouth'],
      bands:['vessel.band'],feet:['vessel.support'],ears:['vessel.handle']},[
        ['body','rim',[0,1.31,0]],['body','bands',[0,.98,0]],['body','feet',[0,.31,0]],['body','ears',[0,1.07,0]] ]));
}

/** 稠流从窄尾拉到厚头，各段接触沿同一截面的中心及半径。 */
function metalStream() {
  const profile = [[0,-.03],[.038,.03],[.071,.16],[.105,.29],[.152,.48],[.174,.65],[.204,.82],[.238,1.02],[.244,1.18],[.188,1.33],[.095,1.43],[0,1.47]];
  const geometry = new T.LatheGeometry(profile.map(p=>new T.Vector2(...p)),24).rotateX(Math.PI/2);
  const points = geometry.getAttribute('position');
  for(let i=0;i<points.count;i++) {
    const z = points.getZ(i), t = z/1.49, twist = Math.sin(t*5.4)*.05;
    const angle = Math.atan2(points.getY(i),points.getX(i));
    const swell = 1 + .065*Math.sin(angle*3-t*8) + .035*Math.cos(angle*5+t*10);
    points.setXY(i,points.getX(i)*swell + twist,points.getY(i)*swell*.82);
  }
  geometry.userData.smoothSeams = true;
  const contact = (id,z0,z1,r) => ({Id:id,Part:0,A:[Math.sin(z0/1.49*5.4)*.05,0,z0],
    B:[Math.sin(z1/1.49*5.4)*.05,0,z1],Radius:r});
  write('sky_furnace_stream',[part('metal',geometry,'furnace_melt')],[],[
    contact('tail',.14,.68,.19),contact('front',.73,1.22,.26)],[],[],false,facing,
    structure('surface',source,'makeSkyFurnace',{profile},{metal:['fluid.body']}));
}

/** 同一构造的炉身、口沿和有限稠流。 */
export function makeSkyFurnace() {
  furnace();
  metalStream();
}
