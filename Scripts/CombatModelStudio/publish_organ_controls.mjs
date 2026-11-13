/** 只发布真实器官的命名绑定资料；网格、材质及三平台资源包都复用原件。 */
import {readFileSync, writeFileSync} from 'node:fs';
import {makeOrganForms} from './forms_organs.mjs';

for (const source of makeOrganForms().filter(form => form.Controls.length)) {
  const path = new URL(`../../Content/Forms/${source.Id}.json`, import.meta.url);
  let text = readFileSync(path, 'utf8');
  const published = JSON.parse(text);
  if (published.Parts.length !== source.Parts.length || published.Parts.some((part, index) => part.Id !== source.Parts[index].Id))
    throw new Error(`${source.Id}: 部件结构已改变，请走完整资源构建`);
  // 正式资源由共同导入器按固定字段顺序输出；只替换控制字段，保留原几何数值的完整文本。
  let index = 0;
  text = text.replace(/("MeshAsset"\s*:\s*"[^"]+",\s*"Control"\s*:\s*)"[^"]*"/g,
    (_, prefix) => prefix + JSON.stringify(source.Parts[index++].Control));
  if (index !== source.Parts.length) throw new Error(`${source.Id}: 正式资源字段布局已改变，请走完整资源构建`);
  const controls = JSON.stringify(source.Controls, null, 4).replace(/\n/g, '\n    ');
  let found = false;
  text = text.replace(/("Controls"\s*:\s*)\[(?:\]|[\s\S]*?\n    \])/, (_, prefix) => {found = true; return prefix + controls;});
  if (!found) throw new Error(`${source.Id}: 正式资源缺少控制字段`);
  writeFileSync(path, text);
  console.log(`${source.Id}: 已发布 ${source.Controls.length} 个真实部件控制`);
}
