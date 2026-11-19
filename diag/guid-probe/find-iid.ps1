$dirs = @(
  "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF",
  "C:\Windows\Microsoft.NET\Framework\v4.0.30319\WPF",
  "C:\Windows\Microsoft.NET\assembly\GAC_MSIL"
)
$pat = "InsertAtSelection|CompositionSink"
foreach ($d in $dirs) {
  if (-not (Test-Path $d)) { continue }
  Get-ChildItem $d -Recurse -Include *.dll -ErrorAction SilentlyContinue | ForEach-Object {
    try {
      $asm = [Reflection.Assembly]::LoadFrom($_.FullName)
      foreach ($t in $asm.GetTypes()) {
        if (-not $t.IsInterface) { continue }
        if ($t.Name -notmatch $pat) { continue }
        $attr = $t.GetCustomAttributes([Runtime.InteropServices.GuidAttribute], $false)
        $g = if ($attr.Count) { $attr[0].Value } else { "(none)" }
        Write-Output ("{0} :: {1}  GUID={2}" -f $_.Name, $t.Name, $g)
      }
    } catch {}
  }
}
