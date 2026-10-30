# 对比真实运行读取器与原编辑文件，检查转换是否保留全部网格和动作数据，并统计读取开销。
param([string]$Assembly = 'tmp/StartupDiagnosis-20261028/build/Cultiway.dll',
    [string]$Output = 'tmp/StartupDiagnosis-20261028/model-files-check.json')
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskGame = [IO.Path]::GetFullPath((Join-Path $taskRoot '../..'))
[Reflection.Assembly]::LoadFrom((Join-Path $taskGame 'worldbox_Data/Managed/UnityEngine.CoreModule.dll')) | Out-Null
$taskWorldfall = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath((Join-Path $taskRoot '../Worldfall/Assemblies/Worldfall.dll')))
$taskCultiway = [Reflection.Assembly]::LoadFrom((Join-Path $taskRoot $Assembly))
$taskReferences = @((Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll').FullName) + @(
    $taskWorldfall.Location, [Newtonsoft.Json.Linq.JObject].Assembly.Location)
Add-Type -ReferencedAssemblies $taskReferences -CompilerOptions '/nowarn:1701' -TypeDefinition @'
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using FirstPerson.Core;
using Newtonsoft.Json.Linq;
public static class WorldfallFileCheck {
    static void Same(bool condition, string label) { if (!condition) throw new Exception(label); }
    static bool Vector(Vec3 a, Vec3 b) { return a.X == b.X && a.Y == b.Y && a.Z == b.Z; }
    public static object Run(Assembly assembly, string directory) {
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        var method = assembly.GetType("Cultiway.Content.Worldfall.WorldfallModelFiles").GetMethod("ReadAll", flags);
        var watch = Stopwatch.StartNew();
        long before = GC.GetTotalAllocatedBytes(true);
        var collections = new[] {GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)};
        var assets = (Array)method.Invoke(null, new object[] {directory});
        double milliseconds = watch.Elapsed.TotalMilliseconds;
        long allocations = GC.GetTotalAllocatedBytes(true) - before;
        long peak = Process.GetCurrentProcess().PeakWorkingSet64;
        int[] gc = Enumerable.Range(0,3).Select(i => GC.CollectionCount(i)-collections[i]).ToArray();
        long retained = GC.GetTotalMemory(true);
        int models = 0; long vertices = 0, triangles = 0, sourceBytes = 0, binaryBytes = 0;
        watch.Restart();
        foreach (object asset in assets) {
            Type type = asset.GetType();
            string id = (string)type.GetField("Id", flags).GetValue(asset);
            string path = Path.Combine(directory, id, "models.json");
            var document = JObject.Parse(File.ReadAllText(path));
            var loaded = (MeshModel[])type.GetField("Models", flags).GetValue(asset);
            Same(id == (string)document["Id"], id+" 资产");
            Same((string)type.GetField("Category", flags).GetValue(asset) == (string)document["Category"], id+" 分类");
            Same(loaded.Length == document["Models"].Count(), id+" 网格数");
            sourceBytes += new FileInfo(path).Length; binaryBytes += new FileInfo(Path.ChangeExtension(path,".bin")).Length;
            for (int m=0; m<loaded.Length; m++) {
                var entry = (JObject)document["Models"][m];
                MeshModel expected = entry.ToObject<MeshModel>(), actual = loaded[m];
                string name = expected.Name;
                Same(name == actual.Name && Vector(expected.Min,actual.Min) && Vector(expected.Max,actual.Max), name+" 名称或边界");
                Same(expected.SlotNames.SequenceEqual(actual.SlotNames) && expected.SlotKind.SequenceEqual(actual.SlotKind) &&
                    expected.SlotFlags.SequenceEqual(actual.SlotFlags) && expected.SlotColor.SequenceEqual(actual.SlotColor), name+" 材质与颜色");
                typeof(MeshModel).GetMethod("ParseInfo", flags).Invoke(expected, new object[] {
                    string.Join(";", ((JObject)entry["Info"]).Properties().Select(p => p.Name+"="+p.Value))});
                var expectedInfo = (Dictionary<string,string>)typeof(MeshModel).GetField("_info",flags).GetValue(expected);
                var actualInfo = (Dictionary<string,string>)typeof(MeshModel).GetField("_info",flags).GetValue(actual);
                Same(expectedInfo.Count == actualInfo.Count, name+" 标记数");
                foreach (var property in expectedInfo)
                    Same(actual.Info(property.Key) == property.Value, name+" 标记 "+property.Key);
                Same(expected.Parts.Length == actual.Parts.Length, name+" 部件数");
                for(int p=0;p<expected.Parts.Length;p++) {
                    var a=expected.Parts[p]; var b=actual.Parts[p];
                    Same(a.Name==b.Name && a.Parent==b.Parent && a.Version==b.Version && Vector(a.Pivot,b.Pivot), name+" 动作部件 "+a.Name);
                    Same(a.Pos.SequenceEqual(b.Pos) && a.Nrm.SequenceEqual(b.Nrm) && a.Tri.SequenceEqual(b.Tri) &&
                        a.Slot.SequenceEqual(b.Slot) && a.Axis.SequenceEqual(b.Axis), name+" 几何 "+a.Name);
                    vertices+=b.VertexCount; triangles+=b.TriangleCount;
                }
                models++;
            }
        }
        return new {passed=true, runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            assets=assets.Length, models, vertices, triangles, source_bytes=sourceBytes, binary_bytes=binaryBytes,
            read_ms=milliseconds, allocated_bytes=allocations, retained_bytes=retained, collections=gc,
            peak_process_bytes=peak, equivalence_ms=watch.Elapsed.TotalMilliseconds};
    }
}
'@
$taskResult = [WorldfallFileCheck]::Run($taskCultiway, (Join-Path $taskRoot 'Content/Worldfall/Models'))
$taskResult | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskRoot $Output) -Encoding utf8
$taskResult | ConvertTo-Json -Depth 8
