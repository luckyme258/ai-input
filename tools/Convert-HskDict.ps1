# 将 complete-hsk-vocabulary (MIT) 的 complete.json 转为引擎使用的 TSV。
# 输出列：word<TAB>pinyin(无声调全拼,空格分隔)<TAB>initials(首字母串)<TAB>baseFreq
# 用法： pwsh tools/Convert-HskDict.ps1 -Source <complete.json> -Out <dict.tsv> [-Blocklist <blocklist.txt>]

param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$Out,
    [string]$Blocklist
)

$block = New-Object System.Collections.Generic.HashSet[string]
if ($Blocklist -and (Test-Path $Blocklist)) {
    foreach ($b in Get-Content $Blocklist) {
        $t = $b.Trim()
        if ($t -and -not $t.StartsWith('#')) { [void]$block.Add($t) }
    }
}

function Strip-Tone([string]$numeric) {
    if ([string]::IsNullOrWhiteSpace($numeric)) { return $null }
    $parts = $numeric.Trim() -split '\s+'
    $clean = New-Object System.Collections.Generic.List[string]
    $init  = New-Object System.Collections.Generic.List[string]
    foreach ($p in $parts) {
        # 允许大小写、音节内含带调字母；去掉尾部声调数字，ü 记为 v。
        $m = [regex]::Match($p, '^([a-zA-Züāáǎàēéěèīíǐìōóǒòūúǔùǖǘǚǜ]+?)([1-5])$')
        if (-not $m.Success) { return $null }
        $syl = $m.Groups[1].Value.ToLowerInvariant() -replace 'ü', 'v'
        $syl = $syl -replace '[āáǎà]', 'a' -replace '[ēéěè]', 'e' -replace '[īíǐì]', 'i' -replace '[ōóǒò]', 'o' -replace '[ūúǔù]', 'u' -replace '[ǖǘǚǜ]', 'v'
        $clean.Add($syl)
        $init.Add($syl.Substring(0, 1))
    }
    return [pscustomobject]@{ Pinyin = ($clean -join ' '); Initials = ($init -join '') }
}

$json = Get-Content $Source -Raw | ConvertFrom-Json
$lines = New-Object System.Collections.Generic.List[string]
$skipped = 0
foreach ($entry in $json) {
    $word = $entry.simplified
    if ([string]::IsNullOrWhiteSpace($word)) { $skipped++; continue }
    if ($block.Contains($word)) { $skipped++; continue }
    $freq = if ($entry.frequency) { [double]$entry.frequency } else { 1.0 }
    foreach ($form in $entry.forms) {
        $r = Strip-Tone $form.transcriptions.numeric
        if ($null -eq $r) { $skipped++; continue }
        $lines.Add(($word, $r.Pinyin, $r.Initials, $freq.ToString('0.###')) -join "`t")
    }
}

$lines | Set-Content -Path $Out -Encoding UTF8
Write-Output "written: $($lines.Count) rows; skipped: $skipped -> $Out"
