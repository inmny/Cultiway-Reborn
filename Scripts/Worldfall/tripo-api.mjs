/** Tripo 官方接口：提交一次、记录任务、续接下载，保留平台原始模型。 */
import { readFile, writeFile, mkdir, access } from 'node:fs/promises';
import { resolve, join, basename, extname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';
import { parseArgs } from 'node:util';
import { TripoClient, modelExtension } from '../../Tools/Tripo/node_modules/@vastai/tripo-sdk/src/index.js';

const root = fileURLToPath(new URL('../../', import.meta.url));
const { values } = parseArgs({ options: {
    mode: { type: 'string', default: 'plan' },
    asset: { type: 'string', default: 'Dark_tree' },
    input: { type: 'string', default: 'Art/Worldfall/Dark_tree/reference/tripo-v1/tripo-input.png' },
    output: { type: 'string', default: 'Art/Worldfall/Dark_tree/tripo-api-v1' },
    region: { type: 'string', default: 'cn' },
    task: { type: 'string' },
} });
const output = resolve(root, values.output);
const recordPath = join(output, 'generation.json');
const baseUrl = values.region === 'cn' ? 'https://openapi.tripo3d.com/v3' : 'https://openapi.tripo3d.ai/v3';
// 5000 是四边面控制；导出的三角面数另行统计。标准贴图实际是否为 2K 由下载文件检查。
const parameters = {
    model: 'P2-20260801', quad: true, face_limit: 5000,
    texture: true, pbr: false, texture_quality: 'standard',
    texture_version: 'v3.0-20250812', texture_alignment: 'original_image',
    enable_image_autofix: false, auto_size: false, export_uv: true,
    model_seed: 20261013, texture_seed: 20261013,
};
let record;

/** 写入当前阶段；任务编号在任何等待或下载之前保存。 */
async function saveRecord() {
    record.updated_at = new Date().toISOString();
    await writeFile(recordPath, JSON.stringify(record, null, 2) + '\n');
}

/** 下载结果使用普通请求，API 密钥只发送到官方接口，不随模型下载发送。 */
async function download(url, name) {
    const response = await fetch(url, { signal: AbortSignal.timeout(60000) });
    if (!response.ok) throw new Error(`结果下载失败，HTTP ${response.status}`);
    const bytes = Buffer.from(await response.arrayBuffer());
    await writeFile(join(output, name), bytes);
    return { file: name, bytes: bytes.length, sha256: createHash('sha256').update(bytes).digest('hex') };
}

/** 查询同一个任务直到完成；续接不会重新提交生成。 */
async function finish(client) {
    const task = await client.waitForTask(record.task_id, {
        pollingIntervalMs: 10000, timeoutMs: 30 * 60000,
        onProgress: item => console.log(`${item.task_id}: ${item.status} ${item.progress ?? 0}%`),
        throwOnFailure: false,
    });
    await writeFile(join(output, 'task-result.json'), JSON.stringify(task, null, 2) + '\n');
    record.task_status = task.status;
    record.credits_consumed = task.credits_consumed;
    record.stage = 'task_finished';
    await saveRecord();
    if (task.status !== 'success') throw new Error(`Tripo 任务未成功：${task.status}，${task.error_message ?? task.error_msg ?? ''}`);
    const modelUrl = task.output?.model_url ?? task.output?.model ?? task.output?.pbr_model ?? task.output?.base_model;
    if (!modelUrl) throw new Error('任务成功但没有模型链接，请检查 task-result.json。');
    const extension = modelExtension(modelUrl) || 'bin';
    record.files = [await download(modelUrl, `model-original.${extension}`)];
    // 保存所有明确给出的贴图链接，不改变分辨率、材质或网格。
    const textureLinks = [];
    for (const [key, value] of Object.entries(task.output)) {
        if (/texture/i.test(key)) collectUrls(value, textureLinks);
    }
    for (const [index, url] of [...new Set(textureLinks)].entries()) {
        const suffix = modelExtension(url) || 'bin';
        record.files.push(await download(url, `texture-${index + 1}.${suffix}`));
    }
    const previewUrl = task.output.rendered_image_url ?? task.output.rendered_image;
    if (previewUrl) record.files.push(await download(previewUrl, `preview.${modelExtension(previewUrl) || 'png'}`));
    record.stage = 'downloaded';
    record.inspection = 'pending';
    await saveRecord();
    console.log(JSON.stringify({ stage: record.stage, task_id: record.task_id, files: record.files }, null, 2));
}

/** 读取服务返回的贴图列表，不推测或拼接下载地址。 */
function collectUrls(value, result) {
    if (typeof value === 'string' && value.startsWith('https://')) result.push(value);
    else if (Array.isArray(value)) value.forEach(item => collectUrls(item, result));
    else if (value && typeof value === 'object') Object.values(value).forEach(item => collectUrls(item, result));
}

/** 执行命令：先检查本地输入，再鉴权，提交后只续接现有任务。 */
async function main() {
    if (!['plan', 'balance', 'generate', 'resume', 'task'].includes(values.mode)) throw new Error('模式应为 plan、balance、generate、resume 或 task。');
    if (!['cn', 'global'].includes(values.region)) throw new Error('地区应为 cn 或 global。');
    if (values.mode === 'plan') {
        const bytes = await readFile(resolve(root, values.input));
        console.log(JSON.stringify({ asset: values.asset, input: values.input,
            input_sha256: createHash('sha256').update(bytes).digest('hex'),
            output, base_url: baseUrl, request: parameters,
            texture_target: [2048, 2048], postprocessing: 'none', submitted: false }, null, 2));
        return;
    }
    if (!process.env.TRIPO_API_KEY) throw new Error('尚未读取到 TRIPO_API_KEY。请通过 tripo.ps1 读取 Windows 用户环境变量。');
    // 生成不自动重试；不确定服务器是否接受时保留阶段记录，先核对任务。
    const client = new TripoClient({ baseUrl, retries: 0, timeoutMs: 60000 });
    if (values.mode === 'balance') {
        const balance = await client.getBalance();
        console.log(JSON.stringify({ authenticated: true, base_url: baseUrl, balance }, null, 2));
        return;
    }
    if (values.mode === 'task') {
        if (!values.task) throw new Error('查询任务需要 --task。');
        const task = await client.getTask(values.task);
        console.log(JSON.stringify({ task_id: task.task_id, status: task.status,
            progress: task.progress, output_fields: Object.keys(task.output ?? {}),
            error_code: task.error_code, error_message: task.error_message }, null, 2));
        return;
    }
    if (values.mode === 'resume') {
        const saved = JSON.parse(await readFile(recordPath, 'utf8'));
        if (saved.base_url !== baseUrl) throw new Error('续接地区与原任务不一致。');
        if (!saved.task_id) throw new Error('没有已记录的任务编号，请先核对开发者平台任务列表，不要重复提交。');
        record = saved;
        await finish(client);
        return;
    }
    let exists = false;
    try { await access(recordPath); exists = true; }
    catch (error) { if (error.code !== 'ENOENT') throw error; }
    if (exists) throw new Error('该输出目录已有生成记录。请用 resume 续接；新试验须指定新的输出目录。');
    const inputPath = resolve(root, values.input);
    const bytes = await readFile(inputPath);
    if (extname(inputPath).toLowerCase() !== '.png' || !bytes.subarray(0, 8).equals(Buffer.from([137,80,78,71,13,10,26,10]))) throw new Error('当前入口需要真实 PNG 高清参考图。');
    if (bytes.length > 20 * 1024 * 1024) throw new Error('参考图超过官方 20 MB 限制。');
    if (bytes.readUInt32BE(16) < 256 || bytes.readUInt32BE(20) < 256) throw new Error('请先把像素原图制作成至少 256×256 的高清参考图。');
    await mkdir(output, { recursive: true });
    record = { asset_id: values.asset, provider: 'Tripo OpenAPI v3', sdk_version: '0.3.0',
        base_url: baseUrl, source_image: inputPath, input_sha256: createHash('sha256').update(bytes).digest('hex'),
        parameters, texture_target: [2048, 2048], stage: 'uploading',
        postprocessing: 'none', production_replaced: false, game_test: 'pending' };
    await saveRecord();
    await writeFile(join(output, 'input.png'), bytes);
    const upload = await client.uploadFile(bytes, { filename: basename(inputPath), contentType: 'image/png' });
    if (!upload.file_token) throw new Error('上传返回值缺少 file_token，未提交生成任务。');
    record.file_token = upload.file_token;
    record.stage = 'submitting';
    await saveRecord();
    record.task_id = await client.imageToModel({ input: upload.file_token, ...parameters });
    if (!record.task_id) throw new Error('提交返回值缺少任务编号，请先核对平台，避免重复生成。');
    record.stage = 'submitted';
    await saveRecord();
    console.log(`已提交并保存编号：${record.task_id}`);
    await finish(client);
}

main().catch(async error => {
    const secret = process.env.TRIPO_API_KEY;
    const message = secret ? String(error.message).split(secret).join('[已隐藏]') : String(error.message);
    if (record) {
        record.last_error = { name: error.name, message, code: error.code,
            http_status: error.status, submission_indeterminate: error.indeterminate === true };
        await saveRecord();
    }
    console.error(JSON.stringify({ error: message, code: error.code, http_status: error.status,
        submission_indeterminate: error.indeterminate === true }));
    process.exitCode = 1;
});
