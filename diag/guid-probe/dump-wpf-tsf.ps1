$asmPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll"
$asm = [Reflection.Assembly]::LoadFrom($asmPath)
$targets = @(
  "d40c8aae-ac92-4fc7-9a11-0ee0e23aa39b",
  "20168d64-5a8f-4a5a-b7bd-cfa29f4d0fd9",
  "aa80e7fd-2021-11d2-93e0-0060b067b86e",
  "aa80e7ff-2021-11d2-93e0-0060b067b86e",
  "2433bf8e-0f9b-435c-ba2c-180611978c30"
)
foreach ($t in $asm.GetTypes()) {
  $attr = $t.GetCustomAttributes([Runtime.InteropServices.GuidAttribute], $false)
  if (-not $attr -or $attr.Count -eq 0) { continue }
  $g = $attr[0].Value
  $hit = $targets -contains $g
  if ($hit) {
    Write-Output ("=== {0}  GUID={1}  IsInterface={2}" -f $t.FullName, $g, $t.IsInterface)
    if ($t.IsInterface) {
      $i = 0
      foreach ($m in $t.GetMethods()) {
        Write-Output ("   [{0}] {1}" -f $i, $m.Name)
        $i++
      }
    }
  }
}
