#!/usr/bin/env bash
# 在指定 Unity 2022.3.60f1 资源工程中构建三平台材质，源码保存在模组内。
set -euo pipefail
task_root=$(cd "$(dirname "$0")/../.." && pwd)
task_project=${1:?请提供 Unity 资源工程路径}
task_editor=${2:?请提供 Unity 编辑器可执行文件路径}
cp "$task_root"/Content/ShaderSources/*.shader "$task_project/Assets/Cultiway/Shaders/"
cp "$task_root/Scripts/ShaderBuild/BuildCultiwayBundles.cs.txt" "$task_project/Assets/Editor/BuildCultiwayBundles.cs"
"$task_editor" -batchmode -nographics -quit -projectPath "$task_project" \
    -executeMethod CultiwayAssets.Editor.BuildCultiwayBundles.BuildFromCommandLine \
    -bundleOutput "$task_root/Content/AssetBundles" -logFile /tmp/cultiway-shader-build.log
