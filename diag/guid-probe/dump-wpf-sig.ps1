$asmPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll"
$asm = [Reflection.Assembly]::LoadFrom($asmPath)

function Dump-Interface($namePart) {
  foreach ($t in $asm.GetTypes()) {
    if (-not $t.IsInterface) { continue }
    if ($t.Name -notlike "*$namePart*") { continue }
    $attr = $t.GetCustomAttributes([Runtime.InteropServices.GuidAttribute], $false)
    $g = if ($attr.Count) { $attr[0].Value } else { "(none)" }
    Write-Output ("=== {0}  GUID={1}" -f $t.FullName, $g)
    foreach ($m in $t.GetMethods()) {
      $ps = ($m.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ", "
      Write-Output ("   {0} ({1}) -> {2}" -f $m.Name, $ps, $m.ReturnType.Name)
    }
  }
}

Dump-Interface "ContextComposition"
Dump-Interface "Composition"
Dump-Interface "InsertAtSelection"
Dump-Interface "EditSession"
Dump-Interface "ContextView"
