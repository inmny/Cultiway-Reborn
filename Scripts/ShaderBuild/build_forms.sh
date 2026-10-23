#!/usr/bin/env bash
# 将共用形体导入指定 Unity 资源工程，并构建平台资源。
set -euo pipefail
task_root=$(cd "$(dirname "$0")/../.." && pwd)
task_project=${1:?请提供 Unity 资源工程路径}
task_editor=${2:?请提供 Unity 编辑器路径}
node "$task_root/Scripts/CombatModelStudio/forms.mjs"
mkdir -p "$task_project/Assets/Cultiway/Shaders" "$task_project/Assets/Editor"
cp "$task_root/Content/ShaderSources/FormSurface.shader" "$task_root/Content/ShaderSources/NativeObject.shader" "$task_project/Assets/Cultiway/Shaders/"
cp "$task_root/Scripts/ShaderBuild/BuildFormBundles.cs.txt" "$task_project/Assets/Editor/BuildFormBundles.cs"
"$task_editor" -batchmode -nographics -quit -projectPath "$task_project" \
  -executeMethod CultiwayAssets.Editor.BuildFormBundles.BuildFromCommandLine \
  -cultiwayRoot "$task_root" -logFile /tmp/cultiway-form-build.log
