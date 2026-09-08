param([int]$x, [int]$y, [switch]$Double, [int]$Wheel = 0)
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class M {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, int data, UIntPtr extra);
}
'@ -ErrorAction SilentlyContinue
[M]::SetProcessDPIAware() | Out-Null
[M]::SetCursorPos($x, $y) | Out-Null
Start-Sleep -Milliseconds 80
if ($Wheel -ne 0) {
  [M]::mouse_event(0x0800, 0, 0, $Wheel, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 80
  exit
}
[M]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
[M]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
if ($Double) {
  Start-Sleep -Milliseconds 60
  [M]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
  [M]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
}
Start-Sleep -Milliseconds 60
Write-Output "clicked $x,$y"
