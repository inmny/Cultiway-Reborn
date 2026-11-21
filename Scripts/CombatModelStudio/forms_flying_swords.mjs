/** 已学飞剑单独发布。原握持兵器继续使用原剑形。 */
import * as T from 'three';
import {part,write,structure,dissolve} from './form_export.mjs';

/** 两种剑身的挂点、刃口和尖端沿用各自实体的尺寸。 */
export function makeFlyingSwords(original, blade, guard, grip, pommel) {
  const controls=[{Id:'root',Position:[0,0,0],Rotation:[0,0,0,1]}];
  const roles={blade:['blade.edge','blade.spine'],guard:['weapon.guard'],
    grip:['weapon.grip'],pommel:['weapon.pommel']};
  const facing={Forward:[0,0,1],Up:[0,1,0]};
  const longParts=[
    part('blade',blade.clone(),'metal',[0,0,0],'root'),
    {...part('guard',guard.clone().scale(1.12,1.25,1.28),'bronze',[0,0,0],'root'),UseMatterSurface:false},
    part('grip',grip.clone(),'leather',[0,0,0],'root'),
    {...part('pommel',pommel.clone(),'gold',[0,0,0],'root'),UseMatterSurface:false}
  ];
  const anchors=original.Anchors.map(a=>({...a,Control:'root'}));
  const contacts=original.Contacts.map(c=>({...c,AControl:'root',BControl:'root'}));
  write('flying_sword',longParts,anchors,contacts,controls,[
    dissolve(controls,(_index,t)=>({
      position:[0,.092*t,-.13*t],
      rotation:new T.Quaternion().setFromAxisAngle(new T.Vector3(1,0,0),-.09*t).toArray()
    }))
  ],false,facing,structure('rigid','forms_flying_swords.mjs','makeFlyingSwords',
    {variant:0,bladeLength:2.5,guardSpan:.96},roles));

  const width=1.19,length=.60,gripLength=.63;
  const collar=new T.BoxGeometry(.35,.133,.137).translate(0,0,-.024);
  const shortParts=[
    part('blade',blade.clone().scale(width,1.25,length),'metal',[0,0,0],'root'),
    {...part('guard',collar,'gold',[0,0,0],'root'),UseMatterSurface:false},
    part('grip',grip.clone().scale(.78,.86,gripLength),'leather',[0,0,0],'root'),
    part('pommel',pommel.clone().scale(.85,.85,gripLength),'metal',[0,0,0],'root')
  ];
  const shortAnchors=anchors.map(a=>({...a,
    Position:[a.Position[0]*width,a.Position[1],a.Position[2]*(a.Part===0?length:gripLength)]}));
  const shortContacts=contacts.map(c=>({...c,A:[c.A[0]*width,c.A[1],c.A[2]*length],
    B:[c.B[0]*width,c.B[1],c.B[2]*length]}));
  write('flying_sword_1',shortParts,shortAnchors,shortContacts,controls,[],false,facing,
    structure('rigid','forms_flying_swords.mjs','makeFlyingSwords',{variant:1,bladeLength:1.5,guardSpan:.35},roles));
}
