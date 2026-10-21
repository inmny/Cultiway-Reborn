# 战斗主体制作与检查

从模组目录启动 `python3 -m http.server 9048 --bind 127.0.0.1`，在浏览器打开 `http://127.0.0.1:9048/Scripts/CombatModelStudio/`。预览所需的 Three.js 0.180.0 和旋转控制已放在 `vendor/`，无需外网。

模型源程序是 `generate.mjs`。在本目录运行 `npm ci` 和 `npm run generate`，会重新生成 `Content/CombatModels/` 中六个共同模型。游戏不需要安装这些制作工具。

导出坐标依次为横向、纵深、高度。模型包含完整位置、法向、三角形和基础颜色，表面材料可由招式组合覆盖。真身右臂在游戏内按真实掌击动作添加；制作预览中的身体因此只包含左臂。模型文件是发布资源，不属于长期世界存档。

预览用于检查形体与质感，实机视频用于确认最终战斗画面。预览材质与游戏材质分别运行，不能用预览截图代替实机验证。

Three.js 与旋转控制按 MIT 许可使用，见 `Licenses/threejs.txt`。官方资料：[模型几何](https://threejs.org/docs/#api/en/core/BufferGeometry)、[旋转轮廓](https://threejs.org/docs/#api/en/geometries/LatheGeometry)、[曲线路径](https://threejs.org/docs/#api/en/geometries/TubeGeometry)。
