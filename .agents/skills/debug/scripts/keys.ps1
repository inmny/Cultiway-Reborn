param([string]$keys)
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class K {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
'@ -ErrorAction SilentlyContinue
[K]::SetProcessDPIAware() | Out-Null
Add-Type -AssemblyName System.Windows.Forms
if ($keys -eq 'COPY') {
  [System.Windows.Forms.SendKeys]::SendWait('^a')
  Start-Sleep -Milliseconds 100
  [System.Windows.Forms.SendKeys]::SendWait('^v')
} elseif ($keys -eq 'ENTER') {
  [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
}
Start-Sleep -Milliseconds 200
Write-Output "sent $keys"
