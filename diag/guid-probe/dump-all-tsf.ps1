$asmPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll"
$asm = [Reflection.Assembly]::LoadFrom($asmPath)
foreach ($t in $asm.GetTypes()) {
  if (-not $t.IsInterface) { continue }
  $attr = $t.GetCustomAttributes([Runtime.InteropServices.GuidAttribute], $false)
  if ($attr.Count -eq 0) { continue }
  $g = $attr[0].Value
  if ($t.Name -match "Insert|CompositionSink|Composition|EditSession|Context") {
    Write-Output ("=== {0}  GUID={1}" -f $t.Name, $g)
    foreach ($m in $t.GetMethods()) {
      $ps = ($m.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ", "
      Write-Output ("   {0} ({1}) -> {2}" -f $m.Name, $ps, $m.ReturnType.Name)
    }
  }
}
