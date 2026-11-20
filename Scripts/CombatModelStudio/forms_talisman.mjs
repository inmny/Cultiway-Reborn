import * as T from 'three';
import {part, structure, write} from './form_export.mjs';

/** 区分折纸厚边、朱砂印块与底下的完整受符面。 */
export function makeTalismanForms() {
  const paper = [[-.36,-.51],[-.16,-.64],[.23,-.61],[.37,-.43],[.34,.53],[.19,.63],[-.32,.59]];
  const fold = [[-.33,.56],[.18,.60],[.35,.49],[.17,.31],[-.08,.36]];
  const stamp = [[-.62,-.37],[-.39,-.59],[.41,-.53],[.62,-.29],[.59,.36],[.33,.58],[-.39,.51],[-.61,.30]];
  const seal = [[-.32,.28],[.27,.28],[.29,.05],[.02,.04],[.03,-.10],[.31,-.10],[.30,-.31],
    [-.29,-.30],[-.28,-.08],[-.10,-.07],[-.11,.07],[-.32,.07]];
  const plate = (outline, depth, front, bevel) =>
    new T.ExtrudeGeometry(new T.Shape(outline.map(([x, y]) => new T.Vector2(x, y))),
      {depth, bevelEnabled:true, bevelThickness:bevel, bevelSize:bevel, bevelSegments:1, steps:1})
      .translate(0, 0, front);
  const model = (id, geometry, surface) => ({...part(id, geometry, surface), UseMatterSurface:false});
  const axes = {Forward:[0,0,1], Up:[0,1,0]};
  const leaf = [model('paper', plate(paper,.067,-.040,.017), 'talisman_paper'),
    model('fold', plate(fold,.04,.03,.01).rotateX(-.13), 'talisman_fold'),
    model('ink', plate(seal.map(([x,y]) => [x*.66,y*.98-.045]),.018,.047,.009), 'talisman_ink')];
  write('talisman_folded_leaf', leaf, [],
    [{Id:'paper',Part:0,A:[0,-.27,0],B:[0,.25,0],Radius:.33}], [], [], false, axes,
    structure('articulated','forms_talisman.mjs','makeTalismanForms',{paper,fold,seal},
      {paper:['talisman.body'],fold:['talisman.fold'],ink:['talisman.symbol']},
      [['paper','fold',[0,.37,0],false],['paper','ink',[0,0,.047],false]]));
  const print = [model('imprint', plate(stamp,.032,-.017,.024), 'talisman_imprint'),
    model('symbol', plate(seal,.04,.03,.019), 'talisman_paper'),
    model('ridge', plate(stamp.map(([x,y]) => [x*.78,y*.09+.33]),.026,.039,.012), 'talisman_fold')];
  write('talisman_contact_seal', print, [], [], [], [], false, axes,
    structure('articulated','forms_talisman.mjs','makeTalismanForms',{stamp,seal},
      {imprint:['talisman.contact'],symbol:['talisman.symbol'],ridge:['talisman.rim']},
      [['imprint','symbol',[0,0,.03],false],['imprint','ridge',[0,.33,.04],false]]));
}
