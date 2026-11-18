/** 原样发布有厚度的作用形状；Unity 的网格导入器不处理这些几何声明。 */
import {readFileSync, readdirSync, writeFileSync} from 'node:fs';
import {join} from 'node:path';

const [root, selected] = process.argv.slice(2);
const source = join(root, 'artifacts/skill_rebuild/form_sources');
const destination = join(root, 'Content/Forms');
const files = selected ? [`${selected}.json`] : readdirSync(source).filter(file => file.endsWith('.json')).sort();
for (const file of files) {
  const authored = JSON.parse(readFileSync(join(source, file), 'utf8'));
  if (!authored.Contacts.some(contact => contact.HalfExtents || contact.Sector || contact.Path)) continue;
  const path = join(destination, file);
  const published = JSON.parse(readFileSync(path, 'utf8'));
  if (published.Id !== authored.Id || published.Version !== authored.Version ||
      published.Parts.length !== authored.Parts.length || published.Parts.some((part, i) => part.Id !== authored.Parts[i].Id)) {
    throw new Error(`${file}：已发布的网格部位和制作资料不匹配`);
  }
  published.Contacts = authored.Contacts;
  writeFileSync(path, JSON.stringify(published, null, 4));
  console.log(`发布作用形状：${authored.Id}`);
}
