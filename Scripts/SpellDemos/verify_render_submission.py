#!/usr/bin/env python3
"""复用已有透明及视角场景，分段核对绘制优化。"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import tempfile

from common import PROJECT, write_json


def compare_fade(before, after):
    """保留旧绝对断言，另核对两版的原版和过渡差异。"""
    def pixels(folder, name):
        return subprocess.check_output(['ffmpeg', '-v', 'error', '-i', str(folder / f'fade-{name}.png'),
                                        '-pix_fmt', 'rgba', '-f', 'rawvideo', '-'])

    a, b, c = zip(*[[pixels(folder, name) for name in ('native', 'worldfall', 'half')]
                   for folder in (before, after)])
    lengths = {len(data) for pair in (a, b, c) for data in pair}
    if lengths != {256 * 144 * 4}:
        raise RuntimeError('需要同一夹具拍出的 256×144 三张过渡图')
    stable_error = max(abs(x - y) for x, y, u, v in zip(*c, *b) if u == v)
    residual_error = max(abs((x - y) - (u - v) * .5) for x, y, u, v in zip(*c, *b))
    result = dict(native_identical=a[0] == a[1], stable_error=stable_error,
                  residual_error=residual_error, pixels=len(a[0]) // 4)
    result['passed'] = result['native_identical'] and stable_error <= 1 and residual_error <= 2
    print(json.dumps(result, ensure_ascii=False))
    if not result['passed']:
        raise SystemExit('前后版本的过渡不一致')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('pixels', 'views', 'detail', 'fade', 'compare'), required=True)
    parser.add_argument('--fixture', type=Path, help='复用原视角测试的世界目录')
    parser.add_argument('--before', type=Path, help='改动前相同夹具的过渡输出目录')
    parser.add_argument('--after', type=Path, help='改动后相同夹具的过渡输出目录')
    args = parser.parse_args()
    if args.section == 'compare':
        if args.before is None or args.after is None:
            parser.error('对照需要 --before 及 --after')
        compare_fade(args.before, args.after)
        return
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请在装有 Worldfall 的独立游戏运行')
    import baseline_scenarios as baseline

    folder = Path(tempfile.mkdtemp(prefix=f'render-submission-{args.section}-',
                                   dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    scripts = Path(__file__).parent
    if args.section == 'pixels':
        code = (scripts / 'verify_form_batch.script').read_text().split('// 实际场景只发布', 1)[0]
        code = code.replace('Path.Combine(root, "artifacts/skill_rebuild/form-batch")', json.dumps(str(folder)))
        point = '    var many ='
        code = code.replace(point, '''    foreach (bool native in new[] { true, false })
    {
        var one = new[] { Item(0, native, 0) };
        var expected = Render(one, native, false, false);
        var actual = Render(one, native, true, false);
        Check(expected.SequenceEqual(actual) && (int)Get(probe, "DrawCount") == 1 &&
            (int)Get(probe, "UploadBytes") == 0, native + " 单个部件不上传实例资料且画面相同");
    }
''' + point)
        code += '\nreturn new { passed = checks.All(c => (bool)Get(c, "pass")), checks };'
    else:
        code = (scripts / 'verify_view_switch.script').read_text()
        code = code.replace('Path.Combine(root, "artifacts/skill_rebuild/view-switch/views")', json.dumps(str(folder)))
        fixture = args.fixture or PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1'
        code = code.replace('Path.Combine(root, "artifacts/skill_rebuild/performance-baseline/cultiway/worlds/none")',
                            json.dumps(str(fixture)))
        code = code.replace('repeat < 2', 'repeat < 1').replace('new[] { "native", "worldfall", "firstperson" }',
                                                                  'new[] { "native", "worldfall" }')
        code = code.replace('new[] { "worldfall", "firstperson" }', 'new[] { "worldfall" }')
        code = code.replace('i < 12; i++) { Shot(mode', 'i < 1; i++) { Shot(mode')
        code = code.replace('if (mode == "worldfall" && repeat == 0) FadeCheck();', '')
        extra = (scripts / 'verify_render_submission.script').read_text()
        detail_only = args.section in ('detail', 'fade')
        extra = '            bool detailOnly = ' + str(detail_only).lower() + ';\n' + extra
        spot = '            Publish(stableInstance); await Enter("native"); await Frames(5);'
        if args.section == 'fade':
            offset = code.index('                Check(maximum < .05f && wrong < 8,')
            end = code.index('\n', offset)
            code = code[:offset] + '''                float Error(Color[] first, Color[] last, Color[] middle)
                {
                    float worst = 0;
                    for (int i = 0; i < first.Length; i++)
                        worst = Mathf.Max(worst, Difference(middle[i], Color.Lerp(first[i], last[i], .5f)));
                    return worst;
                }
                material.color = new Color(color.r, color.g, color.b, 1);
                var integrated = Draw("fade-with-model-full");
                material.color = new Color(color.r, color.g, color.b, 0);
                var nativeZero = Draw("fade-with-model-zero");
                float integratedError = Error(nativeZero, integrated, mixed);
                Publish();
                camera.cullingMask = (int)Get(mod, "_godSavedMask") | (1 << 30);
                var plain = Draw("fade-no-model-zero");
                material.color = new Color(color.r, color.g, color.b, 1);
                var plainFull = Draw("fade-no-model-full");
                material.color = new Color(color.r, color.g, color.b, .5f);
                var plainHalf = Draw("fade-no-model-half");
                float plainError = Error(plain, plainFull, plainHalf);
                var emptyCommands = camera.GetCommandBuffers(CameraEvent.AfterForwardAlpha).Single(c => c.name == "Cultiway.NativeScene");
                Check(maximum < .05f && wrong < 8, "原过渡像素基线与无形体控制对照",
                    new { maximum, wrong, integratedError, plainError, emptyBytes = emptyCommands.sizeInBytes });
                Publish(stableInstance);''' + code[end:]
            start = code.index('            // 先反复切换')
            code = (code[:start] + '            Publish(stableInstance); await Enter("worldfall"); await Frames(5); FadeCheck();\n'
                    + code[code.index(spot, start):])
        elif detail_only:
            start = code.index('            // 先反复切换')
            code = code[:start] + extra + '\n' + code[code.index(spot, start):]
        else:
            code = code.replace(spot, extra + '\n' + spot)
    script = folder / 'input.script'
    script.write_text(code, encoding='utf-8')
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '115000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'绘制复验失败，见 {folder}')
    print(folder)


if __name__ == '__main__':
    main()
