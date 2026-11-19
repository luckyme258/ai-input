# 合成宽松协议通用词库：
#   词表：jieba dict.txt（MIT，词 + 词频）
#   拼音底座：mozillazg/pinyin-data pinyin.txt（MIT，字 -> 带调号拼音，逗号多音）
# 输出 TSV 列：word<TAB>pinyin(无声调全拼,空格分隔)<TAB>initials(首字母串)<TAB>baseFreq
# 多音字取首读音；任一汉字查不到拼音则跳过该词。
# 裁剪规则：仅保留词频 >= MinFreq、长度 <= MaxLen 的中文常用词（默认 100、4 字）。
# 用法：
#   pwsh tools/Convert-CommonDict.ps1 -JiebaDict <dict.txt> -PinyinData <pinyin.txt> -Out <common-mit.tsv> [-MinFreq 100] [-MaxLen 4]

param(
    [Parameter(Mandatory = $true)][string]$JiebaDict,
    [Parameter(Mandatory = $true)][string]$PinyinData,
    [Parameter(Mandatory = $true)][string]$Out,
    [double]$MinFreq = 100.0,
    [int]$MaxLen = 4
)

# 带调号韵母 -> 无声调
function Strip-ToneMark([string]$syl) {
    $syl = $syl -replace 'ü', 'v'
    $syl = $syl -replace '[āáǎà]', 'a' -replace '[ēéěè]', 'e' -replace '[īíǐì]', 'i' `
                -replace '[ōóǒò]', 'o' -replace '[ūúǔù]', 'u' -replace '[ǖǘǚǜ]', 'v'
    return $syl.ToLowerInvariant()
}

# 1) 载入字 -> 首读音（无声调）映射
$charPinyin = new-object 'System.Collections.Generic.Dictionary[string,string]'
foreach ($raw in [System.IO.File]::ReadLines($PinyinData)) {
    if ($raw.Length -eq 0 -or $raw[0] -eq '#') { continue }
    # 形如：U+4F60: nǐ  # 你
    $m = [regex]::Match($raw, '^U\+([0-9A-Fa-f]+):\s*([^\s#]+)')
    if (-not $m.Success) { continue }
    $code = [Convert]::ToInt32($m.Groups[1].Value, 16)
    if ($code -gt 0xFFFF) { continue }   # 跳过 BMP 外辅助平面字符
    $ch = [char]$code
    $first = ($m.Groups[2].Value -split ',')[0]
    $charPinyin[[string]$ch] = Strip-ToneMark $first
}

# 2) 遍历 jieba 词表，逐字注音
$lines = New-Object System.Collections.Generic.List[string]
$skipped = 0
foreach ($raw in [System.IO.File]::ReadLines($JiebaDict)) {
    if ([string]::IsNullOrWhiteSpace($raw)) { continue }
    $parts = $raw -split '\s+'
    if ($parts.Count -lt 2) { continue }
    $word = $parts[0].Trim()
    if ($word.Length -lt 1 -or $word.Length -gt $MaxLen) { continue }
    if ($word -notmatch '[\u4e00-\u9fff]') { continue }   # 跳过 AT&T / C# 等非中文
    if (-not [double]::TryParse($parts[1], [ref]([double]0))) { continue }
    $freq = [double]$parts[1]
    if ($freq -lt $MinFreq) { continue }

    $syls = New-Object System.Collections.Generic.List[string]
    $inits = New-Object System.Collections.Generic.List[string]
    $ok = $true
    foreach ($ch in $word.ToCharArray()) {
        $key = [string]$ch
        $py = $null
        if (-not $charPinyin.TryGetValue($key, [ref]$py)) { $ok = $false; break }
        $syls.Add($py)
        $inits.Add($py.Substring(0, 1))
    }
    if (-not $ok) { $skipped++; continue }

    $lines.Add(($word, ($syls -join ' '), ($inits -join ''), $freq.ToString('0.###')) -join "`t")
}

[System.IO.File]::WriteAllLines($Out, $lines, (New-Object System.Text.UTF8Encoding($false)))
Write-Output "written: $($lines.Count) rows; skipped(no pinyin): $skipped -> $Out"
