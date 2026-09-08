param([int]$x, [int]$y)
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class MH {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
}
'@ -ErrorAction SilentlyContinue
[MH]::SetProcessDPIAware() | Out-Null
[MH]::SetCursorPos($x, $y) | Out-Null
Start-Sleep -Milliseconds 400
Write-Output "hover $x,$y"
