# UI 约定检查（M5）。
# 用法: pwsh -File scripts/check-ui-conventions.ps1
# 退出码: 0 = 通过; 1 = 存在违规。
#
# 检查两类回归:
#   1) 页面/面板 code-behind 重新用 C# 构造控件（应改为 .axaml + VM）。
#      自绘/动态层（组件工厂 M3、Toast、批注 Ink、独立自绘窗口）在允许清单里。
#   2) XAML 里给前景/背景/描边/填充写死颜色（应走 ThemeResources 的 DynamicResource）。
#      资源字典里的首帧兜底（Color=）与 BoxShadow 阴影不在此列。

param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$uiRoot = Join-Path $Root 'src/SmartSideBAR.Avalonia'
if (-not (Test-Path $uiRoot)) {
    Write-Error "找不到 UI 目录: $uiRoot"
    exit 1
}

# 允许用代码构造控件的文件（自绘 / 动态层 / M2 过渡态）。
$codeAllowlist = @(
    'Ui/M3.cs',                 # MD3 组件工厂（唯一的通用控件工厂）
    'Ui/ToastService.cs',       # 动态 Toast 浮层
    'Ui/DesignSpecPage.cs',     # Debug 设计规范陈列页
    'Ui/AutomationPage.axaml.cs', # M2 过渡态: 规则编辑器仍为视图内编辑
    'Ui/Ink/*.cs',              # 批注引擎/工具条（动态绘制）
    'Ui/*Window.cs'             # 独立自绘窗口（OOBE/悬浮球/批注/小画板等）
)

$violations = [System.Collections.Generic.List[string]]::new()

function Test-Allowlisted {
    param([string]$RelativePath, [string[]]$Patterns)
    foreach ($pattern in $Patterns) {
        if ($RelativePath -like $pattern) { return $true }
    }
    return $false
}

# ---- 1. 代码构造控件 ----
$controlPattern = '\bnew\s+(Button|TextBlock|Border|StackPanel|Grid|Image|CheckBox|ComboBox|TextBox|Slider|NumericUpDown|ScrollViewer|WrapPanel|UniformGrid|Ellipse|Canvas)\b'
$codeRoots = @(
    (Join-Path $uiRoot 'Ui'),
    (Join-Path $uiRoot 'Views')
)
foreach ($codeRoot in $codeRoots) {
    if (-not (Test-Path $codeRoot)) { continue }
    Get-ChildItem -Path $codeRoot -Recurse -Filter *.cs | ForEach-Object {
        $relative = [System.IO.Path]::GetRelativePath($uiRoot, $_.FullName).Replace('\', '/')
        if (Test-Allowlisted -RelativePath $relative -Patterns $codeAllowlist) { return }
        $lineNo = 0
        Get-Content -LiteralPath $_.FullName | ForEach-Object {
            $lineNo++
            if ($_ -match $controlPattern) {
                $violations.Add("$relative`:$lineNo  [代码构造控件] $($_.Trim())")
            }
        }
    }
}

# ---- 2. XAML 硬编码颜色 ----
$attributePattern = '(Foreground|Background|BorderBrush|Fill|Stroke)="(#[0-9A-Fa-f]{3,8}|White|Black)"'
$setterPattern = 'Property="(Foreground|Background|BorderBrush|Fill|Stroke)"\s+Value="(#[0-9A-Fa-f]{3,8}|White|Black)"'
Get-ChildItem -Path $uiRoot -Recurse -Filter *.axaml | ForEach-Object {
    $relative = [System.IO.Path]::GetRelativePath($uiRoot, $_.FullName).Replace('\', '/')
    $lineNo = 0
    Get-Content -LiteralPath $_.FullName | ForEach-Object {
        $lineNo++
        if ($_ -match $attributePattern -or $_ -match $setterPattern) {
            $violations.Add("$relative`:$lineNo  [硬编码颜色] $($_.Trim())")
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Host "UI 约定检查失败: $($violations.Count) 处违规" -ForegroundColor Red
    $violations | ForEach-Object { Write-Host "  $_" }
    exit 1
}

Write-Host 'UI 约定检查通过: 无代码构造控件 / XAML 硬编码颜色违规。' -ForegroundColor Green
exit 0
