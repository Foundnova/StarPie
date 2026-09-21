# validate_i18n_residuals.ps1
# 校验 SettingsWindow.xaml、SettingsWindow.xaml.cs 与 I18n.cs 中 Tab4_Ms_* 控件与词条的一致性

$repoRoot = Resolve-Path "$PSScriptRoot\.."
$xamlFile = Join-Path $repoRoot "WinPieGestures\SettingsWindow.xaml"
$csFile   = Join-Path $repoRoot "WinPieGestures\SettingsWindow.xaml.cs"
$i18nFile = Join-Path $repoRoot "WinPieGestures\I18n.cs"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "       StarPie Tab4 里程碑多语言一致性与防残留校验        " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. 提取 XAML 中的 Tab4_Ms_* 元素 Name
$xamlContent = Get-Content $xamlFile -Raw -Encoding UTF8
$xamlMatches = [regex]::Matches($xamlContent, 'Name="(Tab4_Ms_[A-Za-z0-9_]+)"')
$xamlNames = [System.Collections.Generic.HashSet[string]]::new()
foreach ($m in $xamlMatches) {
    [void]$xamlNames.Add($m.Groups[1].Value)
}
Write-Host "1. XAML 中定义的 Tab4_Ms_* 控件总数: " -NoNewline
Write-Host "$($xamlNames.Count)" -ForegroundColor Yellow

# 2. 提取 SettingsWindow.xaml.cs 中绑定的 Tab4_Ms_*
$csContent = Get-Content $csFile -Raw -Encoding UTF8
$csMatches = [regex]::Matches($csContent, 'if \((Tab4_Ms_[A-Za-z0-9_]+) != null\)')
$csNames = [System.Collections.Generic.HashSet[string]]::new()
foreach ($m in $csMatches) {
    [void]$csNames.Add($m.Groups[1].Value)
}
Write-Host "2. SettingsWindow.xaml.cs 中绑定的控件总数: " -NoNewline
Write-Host "$($csNames.Count)" -ForegroundColor Yellow

# 3. 提取 I18n.cs 中注册的 Tab4_Ms_* 词条
$i18nContent = Get-Content $i18nFile -Raw -Encoding UTF8
$i18nMatches = [regex]::Matches($i18nContent, 'Add\("(Tab4_Ms_[A-Za-z0-9_]+)",')
$i18nKeys = [System.Collections.Generic.HashSet[string]]::new()
foreach ($m in $i18nMatches) {
    [void]$i18nKeys.Add($m.Groups[1].Value)
}
Write-Host "3. I18n.cs 中注册的 Tab4_Ms_* 词条总数: " -NoNewline
Write-Host "$($i18nKeys.Count)" -ForegroundColor Yellow
Write-Host "----------------------------------------------------------"

$hasError = $false

# 交叉比对 1: XAML 中有，但 C# 逻辑未绑定
$missingInCs = [System.Collections.Generic.List[string]]::new()
foreach ($name in $xamlNames) {
    if (-not $csNames.Contains($name)) {
        $missingInCs.Add($name)
    }
}
if ($missingInCs.Count -gt 0) {
    $hasError = $true
    Write-Host "[X] 错误：XAML 中存在但 C# 未绑定的控件 ($($missingInCs.Count) 个):" -ForegroundColor Red
    foreach ($item in $missingInCs) {
        Write-Host "    - $item" -ForegroundColor Red
    }
} else {
    Write-Host "[OK] XAML 与 C# 绑定 100% 严格吻合" -ForegroundColor Green
}

# 交叉比对 2: XAML 中有，但 I18n 缺少翻译
$missingInI18n = [System.Collections.Generic.List[string]]::new()
foreach ($name in $xamlNames) {
    if (-not $i18nKeys.Contains($name)) {
        $missingInI18n.Add($name)
    }
}
if ($missingInI18n.Count -gt 0) {
    $hasError = $true
    Write-Host "[X] 错误：XAML 中存在但 I18n 缺少词条 ($($missingInI18n.Count) 个):" -ForegroundColor Red
    foreach ($item in $missingInI18n) {
        Write-Host "    - $item" -ForegroundColor Red
    }
} else {
    Write-Host "[OK] XAML 与 I18n 多语言翻译 100% 覆盖" -ForegroundColor Green
}

# 交叉比对 3: I18n 中存在，但 XAML 已无此控件（即用户担心的孤儿残留词条）
$orphansInI18n = [System.Collections.Generic.List[string]]::new()
foreach ($key in $i18nKeys) {
    if (-not $xamlNames.Contains($key)) {
        $orphansInI18n.Add($key)
    }
}
if ($orphansInI18n.Count -gt 0) {
    $hasError = $true
    Write-Host "[!] 警告：发现孤儿残留词条 (I18n 存在但 XAML 已不存在) ($($orphansInI18n.Count) 个):" -ForegroundColor Yellow
    foreach ($item in $orphansInI18n) {
        Write-Host "    - $item" -ForegroundColor Yellow
    }
} else {
    Write-Host "[OK] 孤儿残留词条数为 0，无任何废弃残留！" -ForegroundColor Green
}

Write-Host "==========================================================" -ForegroundColor Cyan
if (-not $hasError) {
    Write-Host "校验结论：三方一致性校验全部通过！无缺失、无残留、无死链。" -ForegroundColor Green
} else {
    Write-Host "校验结论：存在不一致，请参照上方提示进行修正。" -ForegroundColor Red
}
Write-Host "==========================================================" -ForegroundColor Cyan
