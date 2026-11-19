# AiIme TSF 文本服务真机联调手册

> 阶段：上屏闭环已打通并真机复测通过（Core xUnit **45 项全绿**，Release 构建 0 警告 0 错误），当前为 beta.3 预览。
> 本手册记录注册加载、按键分流、候选交互、上屏写入的现状，以及联调阶段逐一二分定位的根因与修复。

## 1. 前置条件（目标机）

- Windows 10/11 x64。
- 已安装 **.NET 8 共享运行时（Microsoft.NETCore.App 8.0.x）**。
  - 本 comhost 进程不跑 WPF，理论上只需核心运行时，**不需要** Desktop 运行时；
  - 若加载时报 `Microsoft.WindowsDesktop.App` 缺失，再安装 .NET 8 Desktop Runtime。
  - 检查命令：`dotnet --list-runtimes`，应看到 `Microsoft.NETCore.App 8.0.x`。
- 发布目录整体拷到目标机（路径含中文/空格也可，但**注册后不要移动目录**——注册表 InprocServer32 记录的是绝对路径，移动后需重新注册）。

## 2. 发布目录产物（每轮全新 `dist\tsf-vNN\`）

框架依赖部署，目录内必须齐备：

| 文件 | 作用 |
|---|---|
| `AiIme.Tsf.comhost.dll` | 原生 COM 主机，系统 CoCreateInstance 加载入口 |
| `AiIme.Tsf.dll` | 文本服务托管实现 |
| `AiIme.Tsf.deps.json` | Tsf 程序集依赖清单（comhost 加载时按此名查找） |
| `AiIme.Tsf.runtimeconfig.json` | Tsf 运行时配置（框架/rollForward） |
| `AiIme.Core.dll` | 共享 Core（词典/学习/排序/状态机） |
| `Microsoft.Data.Sqlite.dll`、`SQLitePCLRaw.*.dll`、`e_sqlite3.dll` | 本地学习存储依赖 |
| `data\dict\*.tsv` + 3 个 LICENSE | 基础词库 |
| `AiIme.Tsf.RegisterTool.exe/.dll` | 注册/卸载命令行工具 |

> 注意：comhost 加载 `AiIme.Tsf.dll` 时，运行时按**同名** `AiIme.Tsf.deps.json` / `AiIme.Tsf.runtimeconfig.json` 解析；
> 这两份已在 RegisterTool 项目里固化为发布复制项，`dotnet publish` 会自动带出，无需手动补。
>
> 若 `publish` 报 `AiIme.Tsf.dll` 被另一进程占用（宿主进程仍加载着旧 comhost），不要覆盖旧目录；改发到全新 `dist\tsf-vNN` 再 `register`，注册表 InprocServer32 指向新目录即可。

## 3. 注册（管理员）

### 方式 A：一键脚本

在**管理员 PowerShell** 中，仓库根目录下（真机为 Windows PowerShell 5.1，**不要加 `pwsh` 前缀**）：

```powershell
& .\tools\Install-Tsf.ps1 -Action Register
```

### 方式 B：直接运行注册工具

```powershell
cd dist\tsf-vNN
.\AiIme.Tsf.RegisterTool.exe register
```

成功输出：`注册完成。可在 控制面板\时钟和区域\语言\键盘 和语言栏中查看 ai-input 拼音。`

注册做了三件事：
1. 直写 `HKLM\SOFTWARE\Classes\CLSID\{8f3c2a10-7d44-4c9b-9e2a-1b6d5a7c8f90}\InprocServer32` → comhost.dll 绝对路径，`ThreadingModel=Both`；
2. COM 调用 `ITfInputProcessorProfiles.Register` + `AddLanguageProfile`（简体中文 0x0804，profile GUID `9a1c7d42-3e6b-4f0a-8c5d-2b7e1f0a9c34`）；
3. 直写 profile 节点 `Enable=1`（DWORD）。`AddLanguageProfile` 本身不写启用标志；本机正常 profile 节点都带 `Enable`（1 启用 / 0 禁用），缺该值时语言列表可能不把本输入法列为可用，故注册后补写。

### 3.1 注册后确认注册表

```powershell
Get-ItemProperty "HKLM:\SOFTWARE\Classes\CLSID\{8f3c2a10-7d44-4c9b-9e2a-1b6d5a7c8f90}\InprocServer32"
```

应看到 `(默认)` = comhost.dll 绝对路径，`ThreadingModel` = `Both`。

再确认语言 profile 节点已落地（`0x00000804` 即简体中文）：

```powershell
Get-ChildItem "HKLM:\SOFTWARE\Microsoft\CTF\TIP\{8f3c2a10-7d44-4c9b-9e2a-1b6d5a7c8f90}\LanguageProfile" -Recurse
# 末级 profile 键内 Description 应为 "ai-input 拼音"
```

## 4. 加载与按键验证

### 4.0 调试日志

日志**默认关闭**；设环境变量 `AIIME_TSF_DEBUG=1` 才开启。每条日志都是一次同步文件写入（lock + AppendAllText），开启后打字会有明显卡顿，仅排查时临时打开。

日志位置：`%LOCALAPPDATA%\AiIme\logs\tsf-debug.log`（UTF-8 追加，带时间戳与线程号），不在发布目录内。

埋点位置与含义：

| 日志 tag | 出现时机 | 排查看点 |
|---|---|---|
| `Activate` | 文本服务被系统激活 | 含 threadMgr/clientId；若整段无记录 → 服务根本没被创建/加载 |
| `AdviseSink` | 挂接按键 sink | 成功给 cookie；失败会记录 QueryInterface 失败 HRESULT |
| `Deactivate` | 服务停用 | 对照 Activate 看生命周期 |
| `OnTestKeyDown` | TSF 先做按键探测 | 有记录说明 sink 已挂、按键被探测到；`eat=1` 表示本输入法声称接管 |
| `OnKeyDown` | 实际按键回调 | 含 VK、是否消费、当前拼音串、候选数 |
| `Commit` | 提交编辑会话 | 含 RequestEditSession HRESULT、待上屏文本、候选数；非 S_OK 看 HEX |
| `EditSession` | 会话回调进入 | 含编辑 cookie、文本、StartComposition/EndComposition/SetText 的 HRESULT |
| `CandidateWnd` | 自绘候选窗创建 | 含 hwnd；窗口创建失败会在此体现 |

> 联调稳定后这些埋点可整体移除（`TsfDebugLog` + 各处 `TsfDebugLog.Write`）。

> 当前上屏链路：`OnKeyDown` → `CompositionState` 累积（区间保持为空，不写拼音）→ 选词提交时 `RequestEditSession` → 会话内 `GetSelection` 取选区 → 空区间插入候选词 → 置光标到词尾 → `EndComposition`。
> 验证点：服务被系统激活、按键进入 sink、候选窗弹出列候选、↑/↓ 移动与 ←/→ 翻页、回车/空格/数字选词上屏、上屏后退格可删正文。

1. 打开记事本（或任意标准编辑框）。
2. 语言栏切到"ai-input 拼音"（若未出现，注销/重登或重启 `ctfmon` 后再看；已通过 `Set-WinUserLanguageList` 加入中文输入方法列表）。
3. 敲字母、退格、↑/↓、←/→、回车、空格、数字、Esc，观察：
   - 服务是否被激活（`Activate` 被调用）；
   - 按键是否进入 `KeyEventSink.OnKeyDown`；
   - 候选窗是否在编辑框附近弹出、列出候选，高亮随方向键移动、左右键翻页；
   - 回车/空格/数字选词是否上屏，上屏后退格能否删掉正文。

## 5. 卸载（管理员）

```powershell
& .\tools\Install-Tsf.ps1 -Action Unregister
# 或
.\AiIme.Tsf.RegisterTool.exe unregister
```

卸载对称清理：先移除 TSF profile，再删 COM CLSID 项。

## 6. 排查清单

| 现象 | 可能原因 | 排查/处理 |
|---|---|---|
| 注册工具提示"未找到 comhost" | 发布目录缺 `AiIme.Tsf.comhost.dll` | 重新 `dotnet publish`，核对第 2 节目录清单 |
| 写 HKLM / profile 失败（HRESULT 非 0） | 未以管理员运行 | 用管理员 PowerShell 重跑注册 |
| 语言栏看不到 ai-input 拼音 | profile 未注册成功 / 缓存未刷新 | 复核注册表 CLSID 项；注销重登或重启 `ctfmon.exe` |
| 切到该输入法后敲键无反应 | comhost 加载失败 / 运行时缺失 | 见下两条；事件查看器→应用程序日志查 .NET / COM 报错 |
| 报缺 `Microsoft.NETCore.App` | 目标机未装 .NET 8 共享运行时 | 安装 .NET 8 Desktop/Shared Runtime x64 |
| 报缺 `Microsoft.WindowsDesktop.App` | 被判定需桌面框架 | 安装 .NET 8 Desktop Runtime x64；或复查 runtimeconfig |
| comhost 进程崩溃/退出 | vtable 错位、会话时序异常、原生库缺失 | 事件查看器查崩溃模块；核对 `e_sqlite3.dll` 在位 |
| `%LOCALAPPDATA%\AiIme\logs\tsf-debug.log` 未生成 | 日志默认关闭（未设 `AIIME_TSF_DEBUG=1`），或写入被宿主进程权限拦截 | 排查时先设 `AIIME_TSF_DEBUG=1` 并重启加载 comhost 的进程；检查日志目录可写；日志内部异常被吞掉，不影响主流程 |
| 词典未召回候选 | `data\dict` 未随目录拷到目标机 | 核对 `data\dict\*.tsv` 在位；可用环境变量 `AIIME_DICT_DIR` 指定目录 |
| 注册后移动了目录 | 注册表记录绝对路径，已失效 | 重新 `unregister` → 拷到新位置 → `register` |
| `AddLanguageProfile` 报 `0x800706F4`（空引用指针）或直调返回随机 TSF 错误码 | 注册逻辑跑在 CLR 默认 **MTA** 线程，而 TSF profiles 对象为 Apartment 模型，跨套间编组把引用指针转发给代理 | 注册/卸载必须在显式 **STA 线程**上执行（`TsfRegistrar.RunOnStaThread`）；切勿在 MTA 主线程直接调 profiles 接口 |
| 宿主进程在 `coreclr.dll` 固定偏移访问冲突（0xc0000005），无托管堆栈、无转储 | 实现并暴露给原生回调的 COM 接口方法缺 `[PreserveSig]`，运行时按"最后一个 out 参数当返回值"推断本机签名，造成 vtable 签名错位踩内存 | `ITfTextInputProcessor`/`ITfKeyEventSink`/`ITfEditSession` 每个返回 HRESULT 的方法都必须标 `[PreserveSig]`（已修复） |
| 引擎初始化抛"Format of the initialization string … index 0" | 无参 `SqliteProfileStore()` 把数据源**路径**直接传给私有"连接串"构造函数，绕过了 `SqliteConnectionStringBuilder` 生成合法连接串 | 无参构造必须走 `NewConnectionString(DefaultDataSource())`（已修复）；勿把路径直接当连接串传 |
| 词典目录解析抛"The path is empty" | .NET COM Hosting（comhost）在部分宿主进程里 `AppContext.BaseDirectory` 返回空串 | `DictPaths.ResolveDefaultDirectory` 增加程序集目录锚点参数；服务端用 `typeof(...).Assembly.Location` 目录作锚点传入（已修复） |

### 快速命令

```powershell
# 查看已装运行时
dotnet --list-runtimes

# 重启输入框架（刷新输入法列表）
Stop-Process -Name ctfmon -Force; Start-Process ctfmon

# 事件查看器最近应用错误
Get-WinEvent -FilterHashtable @{LogName='Application'; Level=2} -MaxEvents 10 |
  Select-Object TimeCreated, ProviderName, Message
```

## 7. 尚未实现 / 留待真机联调补全

- **标准 TSF 候选元素登记**：`ITfCandidateListUIElement` + `ITfUIElementMgr.Begin/Update/EndUIElement` 尚未接入；当前候选窗为自绘原生 Win32 置顶窗，未纳入标准 UIElement 协议（不支持 UILess 宿主读取）。
- **编辑器内拼音 inline 预览**：累积阶段刻意不把拼音写进 composition 区间（见 9.7），拼音与候选只在候选窗可见；待长生命周期区间模型与显示属性稳定后再补 preedit 写入。
- **composition 显示属性**：`TF_ATTR_INPUT` / `GUID_PROP_COMPOSING` / guid atom / `ITfDisplayAttributeProvider` 未实现，宿主暂无输入态渲染。
- **会话模型对齐 Weasel**：提交与结束区间仍在同一 SYNC 会话；未拆独立 `CInsertTextEditSession` / `CEndCompositionEditSession`，文本服务自身聚合 `ITfCompositionSink`、长生命周期 composition 模型尚未落地。
- **上下文跨焦点重置**：`_committedWords` 上下文目前只在 sink 内存内维护，切换焦点 / 服务停用后是否重置未处理。
- **跨应用稳定性**：不同编辑框（UWP/Chrome/记事本）的会话时序需现场逐一验证。

> 编辑会话真实写入已落地：`EditSession.DoEditSession` 内 `GetSelection` 取范围 → 空区间插入候选词。
> composition 区间生命周期已接入：首次输入 `StartComposition`，清空/结束时 `EndComposition`，引用由 `CompositionComposer` 跨会话持有；`CompositionSink.OnCompositionTerminated` 已实现最小回调，仅当被终止的就是当前持有区间时才清理本地引用。
> 选词交互已接入：回车/空格提交高亮项、数字 1..9 按当前页选词、Esc 清空；候选上屏后调 `RecordCommit` 回写学习（选非首项记纠正权重）。
> 自绘候选窗已接入：原生 Win32 置顶窗（GDI 绘制，不依赖 WPF/桌面运行时），列出候选、高亮当前项，随按键更新；无候选或上屏后隐藏。
> 类别注册 `TFCAT_TIP_KEYBOARD` 已落地（`ITfCategoryMgr.RegisterCategory`，经 `TF_CreateCategoryMgr` 创建对象）。

### 7.1 当前按键交互（TSF 外壳）

| 按键 | 行为 |
|---|---|
| 字母 A–Z | 累积拼音；新输入重置高亮序号与页码 |
| 退格 | 删一个拼音字母；无 composition 时放行，由宿主做原生删除 |
| ↑ / ↓ | 当前页内上下移动高亮；越过末行向下翻页、退到首行之外向上翻页，翻页后高亮落邻页首/末行 |
| ← / →、PgUp / PgDn | 上一页 / 下一页（每页 9 条），翻页后高亮落新页首行；单页候选时放行宿主 |
| Home / End | 高亮跳到首个 / 末个候选，末项自动对齐到所在页首行 |
| Shift（独立敲击） | 切换中文拼音 / 英文直输；英文模式下字母、数字、空格、回车放行宿主原生输入；Shift+字母组合不触发切换 |
| 回车 / 空格 | 提交当前高亮候选；无候选则上屏拼音串 |
| 数字 1–9 | 按当前页行号选词上屏（行号映射到跨页绝对序号；选非首项记纠正） |
| Esc | 清空 composition 与状态机，隐藏候选窗 |
| Ctrl/Alt + 按键 | 组合快捷键透传宿主（Ctrl+A 全选 / C 复制 / V 粘贴 / Z 撤销等），不被当作拼音 |

候选词上屏后经 `HabitLearner.RecordCommit(word, context, wasCorrection)` 回写词频与 n-gram；`PreviousWords` 取最近已上屏词序列。新输入、退格、Esc、上屏、失焦都会把高亮序号与页码重置回首项/首页。

## 8. 关键 GUID / 常量（核对过，勿凭记忆改）

- CLSID_AiImeTextService = `8f3c2a10-7d44-4c9b-9e2a-1b6d5a7c8f90`
- GUID_AiImeProfile = `9a1c7d42-3e6b-4f0a-8c5d-2b7e1f0a9c34`
- LangIdZhCn = `0x0804`
- CLSID_TF_ThreadMgr = `529a9e6b-6587-4f23-ab9e-9c7d683e3c50`
- CLSID_TF_InputProcessorProfiles = `33c53a50-f456-4884-b049-85fd643ecfed`
- IID_ITfCategoryMgr = `c3acefb5-f69d-4905-938f-fcadcf4be830`
- GUID_TFCAT_TIP_KEYBOARD = `34745c63-b2f0-4784-8b67-5e12c8701a31`
- IID_ITfContextComposition = `d40c8aae-ac92-4fc7-9a11-0ee0e23aa39b`
- IID_ITfComposition = `20168d64-5a8f-4a5a-b7bd-cfa29f4d0fd9`

### 类别节点确认

```powershell
Get-ChildItem "HKLM:\SOFTWARE\Microsoft\CTF\TIP\{8f3c2a10-7d44-4c9b-9e2a-1b6d5a7c8f90}\Category" -Recurse
# 应见 Category\Category\{34745C63-…}（键盘类别）与 Category\Item\{8F3C2A10-…}（本服务）
```

## 9. 后期联调根因记录（定位 / 闪烁 / 召回）

> 本节记录真机联调阶段逐一二分定位的根因与对应修复，便于回查。对应产物：`TextService/CandidateWindow.cs`、`CaretRectSession.cs`，Core 层 `Composition/PinyinSegmenter.cs`、`Dictionary/TrieDictionary.cs`。

### 9.1 候选窗定位：折叠光标零高矩形被误判为空、退化角落矩形被当成有效位置

**真机症状**

- 第一键定位基本正常，第二键起候选窗跳到屏幕右下角/边缘角落；
- 大屏、小屏分别复现：首行高亮被上沿裁切，或窗口贴到侧边。

**根因链**

1. **折叠光标矩形高为 0**：选区为空（折叠）时 `ITfContextView.GetTextExt` 返回的矩形 `Height=0`（宽也常为 0），但仍携带有效的 X/Y 落点。旧判定按「宽>0 且高>0」才算有效，把折叠光标误判为空矩形。
2. **空矩形走兜底**：判空后落到「宿主客户区右下」兜底路径；空文档/退化坐标进一步退化成屏幕右下角角落矩形。
3. **自指句柄混入宿主判断**：父 HWND 未排除候选窗自身句柄，窗口句柄被误当成宿主。
4. 早期窗口还以宿主为 owner，宿主焦点/销毁波动会连带弹窗位置异常。

**修复（v25，`CandidateWindow.PositionNear` / `CaretRectSession`）**

- `CaretRectSession`：折叠光标高 0 但 X/Y 有效即保留；仅全零矩形判为无效。
- `PositionNear`：
  - 排除自指句柄（`parent == _hwnd` → 按 `IntPtr.Zero` 处理）；
  - 拒绝角落退化矩形：`Left ≥ 屏宽-4` 或 `Top ≥ 屏高-4`，或全零，均判无效；
  - 沿用最近一次有效光标矩形与宿主句柄（`_lastGoodCaret` / `_lastGoodHost`），单次取失败不回退到角落；
  - 折叠光标高按默认行高 `RowHeight` 估算；主路径贴光标下方，下方放不下翻到光标上方；
  - 窗口不再以宿主为 owner（`CreateWindowEx` 的 `hWndParent=NULL`）。

### 9.2 候选窗闪烁：类背景擦除 + 屏幕 DC 分块绘制 + 无效重绘

**真机症状**：每次按键候选窗整体白闪；位置/内容未变时也闪。

**根因链**

1. **系统先擦背景**：窗口类挂了非空 `hbrBackground`，每帧 `WM_ERASEBKGND` 先整块擦成白背景，随后 `WM_PAINT` 才画内容，两层交替产生白闪。
2. **未双缓冲**：直接在屏幕 DC 上逐行 `FillRect` / `DrawText`，分块可见。
3. **无效刷新**：候选文本、选中项、位置尺寸都没变时，仍每按键 `InvalidateRect` + `SetWindowPos`，触发无谓重排/重绘。

**修复（`CandidateWindow`）**

- 注册类 `hbrBackground = IntPtr.Zero`；`WM_ERASEBKGND` 直接返回 1（不擦除），背景交给 `WM_PAINT` 统一填充。
- `WM_PAINT` 双缓冲：内存 DC + 兼容位图，整窗背景/高亮行/文字画到内存位图，最后 `BitBlt` 一次贴屏。
- 内容签名（选中序号 + 前 9 条候选文本）不变则不 `InvalidateRect`；位置/尺寸不变则不重复 `SetWindowPos`。

### 9.3 召回：无音节切分导致连续拼音命中 0，裸前缀带出整棵子树

**真机/探针症状**：单字母召回泛滥（`n≈737`、`h≈1950`、`s≈3312`）；连续拼音 `nih`、`nihao` 召回 0。

**根因链**

1. 全拼索引键以 `\0` 分隔音节（如 `ni\0hao`）；原 `Recall` 把整串当裸字母前缀在全拼树走，走到节点后 `Collect` 整棵子树，单字母前缀即带出该音节下全部词条。
2. **无音节切分**：连续拼音（`nihao`、`nih`）未切成音节序列；不成音节的字母在全拼路径上失败，首字母索引也匹配不上，故 `nih=0`。

**修复（Core 层）**

- 新增 `Composition/PinyinSegmenter.cs`：最长匹配切分；合法音节集合由词典加载时喂入；含空格直接分段；拼到一半的残片保留做前缀兜底。
- `TrieDictionary.Recall`：
  - 先切音节，`CollectFull` 按音节序列导航——非末音节后必须跟 `\0` 分隔符，末音节允许未拼完前缀并收集其子树；
  - 全拼无命中才退回首字母索引（如 `sj`→世界/时间）；
  - 结果按文本去重、按基础词频降序，`MaxRecall=300` 封顶。
- 真实词典探针实测：`nihao`→你好（1）；`nih`→你好/泥灰岩（2）；`ni`→227；`wo`→42；单字母被封顶到 300。

### 9.4 早期观察项（已闭环）

- ~~首行高亮背景在、高亮文字未见~~：后定位为内存 DC 未选中中文 stock 字体 + GDI 背景模式问题（见 9.5 前的候选窗文字修复），v38 复测首行蓝底白字正常。
- ~~编辑会话 `composing=False`、区间引用跨按键保留存疑~~：随 9.6 区间生命周期排查与 9.7 空区间提交方案闭环；现区间归属按 `ITfContext` 绑定，跨宿主/失焦丢弃陈旧区间。

### 9.5 候选窗 WM_PAINT 无限重绘循环（v45 修复）

**真机症状**：候选窗持续白闪/重绘；日志在 1 秒内刷出上百条 `Paint` 行；刷屏抢占宿主消息线程，打乱编辑会话与光标处理顺序，叠加加剧上屏间歇失败。

**根因链**

1. `WM_PAINT` 处理器用 `GetDC` 直接绘制，结束后**没有校验/清空更新区**（缺 `ValidateRect` 或 BeginPaint/EndPaint）。
2. 窗口更新区持续为脏，系统不断重投 `WM_PAINT`，形成自激重绘循环。

**修复（`CandidateWindow.OnWndProc`）**

- `WM_PAINT` 绘制后调用 `ValidateRect(hwnd, NULL)` 清空更新区；复测确认候选窗不再疯狂闪烁/重绘。

### 9.6 composition 区间生命周期：本地指针复用陈旧区间（未闭环）

**真机症状（v43→v45 基线，不闪退）**

- 拼音 inline 预览在编辑器内不可见；
- 回车 / 数字 / 空格上屏间歇成功：隔 4–5 次、甚至 7–12 次才成功一次；
- 上屏后退格删不掉刚提交的字；Home/End/Delete 正常。

**日志观测**

- `StartComposition` 每次返回 `S_OK`，`Composition.GetRange` / `SetText` / `readback` 均 `S_OK` 且回读非空，但文本未必固化进文档；
- 建区间数量明显多于 `OnCompositionTerminated` 回调数量：大量区间未收到终止通知，本地 `_composition` 指针在「存活/已死」之间复用；写进陈旧/脱锚区间的文本在 `EndComposition` 后被宿主丢弃；
- 光标矩形在连续失败后停在同一位置不再前进，选区/落点状态随失败累积失同步。

**v46 复测新增观测（Edge 搜索栏 + 记事本，交替输入）**

- 所有编辑会话 `RequestEditSession` 返回 `session=0`（TF_SYNC，同步跑完），**未被降级为异步**；会话内无托管异常，返回 HRESULT 全 `S_OK`。排除「会话 CCW 在回调前被回收」「SYNC 被 comhost 降级」两条假设。
- Edge 搜索栏同样间歇（1 成、3–6 败），成功率略高于记事本；**排除 Store 记事本宿主兼容方向**，问题在我方外壳。
- 成功 / 失败与「提交时复用的区间是否已含 preedit 文本」强相关：
  - **成功**：`Ensure` 刚建的是空区间 → `Commit` 直接把候选词写进这个空区间（插入 1 字符），固化进正文；
  - **失败**：`Ensure` → `Preedit` 已把 `ni` 写进同一区间 → `Commit` 再用 `SetText` 把 2 字符 preedit 替换成 1 字符候选词；会话内回读非空、`EndComposition S_OK`，但文本在结束区间后被宿主丢弃。
- 失败块在 preedit 与 commit 之间**未收到** `OnCompositionTerminated`，区间指针仍被本地持有；不是外部提前终止，而是「对已含 interim 文本的区间做替换写入」未被宿主固化。
- 后置光标矩形在提交后取到的是另一个宿主的 `hwnd`（记事本/Edge 视图串扰），选区/落点状态跨宿主失同步；协调器未按 `ITfContext` 区分持有区间归属（对照 Weasel 保留 `_pEditSessionContext`）。

**已核对排除**

- `ITfContext` / `ITfRange` / `ITfComposition` / `ITfContextComposition` / `ITfInsertAtSelection` / `ITfCompositionSink` 的 IID 与 vtable 槽位顺序均与 TLB 镜像、windows-rs 元数据一致，不存在调度错位；
- `TF_SELECTION { range, ase, fInterimChar }` 结构体布局与原生一致；
- 所有返回 HRESULT 的回调方法均已标 `[PreserveSig]`；编辑会话内无托管异常抛出。

**后续方向（待真机验证）**

- 对齐 Weasel 长生命周期模型：一次输入 burst 内只建一条 composition 并保留所属 `ITfContext`，后续按键复用同一区间更新 preedit；提交与结束区间拆成独立会话（`CInsertTextEditSession` 只写+置光标，`CEndCompositionEditSession` 再 `EndComposition`）；结束前先 `_FinalizeComposition` 丢本地所有权，避免同步终止回调误清新区间；
- 提交前校验持有区间仍属于当前上下文；跨宿主/上下文切换时丢弃陈旧区间，不复用其 GetRange 写入；
- 补 composition 显示属性（`TF_ATTR_INPUT` / `GUID_PROP_COMPOSING` / guid atom），让宿主按输入态渲染 preedit；
- 复核替换写入路径：preedit 已含文本时 `SetText` 的区间锚点是否随替换收缩，必要时对照 Weasel 的 range 折叠/ShiftStart 处理。

### 9.7 v47 突破：保持区间为空直到提交，上屏与退格恢复稳定

**真机复测（记事本 + Edge 搜索栏）**：连测十几次，候选词上屏**基本每次成功**；上屏后退格**可以删除**。此前提的「间歇成功、退格删不掉」主线缺陷闭环。

**结论（对照 v46 收窄规律）**

- 根因确认：累积阶段把拼音 interim 文本写进 composition 区间后，提交时对这个已含文本的区间做替换写入，结束区间后文本被宿主丢弃；本地回读非空、`EndComposition S_OK` 是表象。
- 处置：累积阶段不再向 composition 区间写拼音（inline 预览暂时不进编辑器，候选窗照常显示拼音与候选）；区间保持为空，直到选词提交才一次性写入候选词，走已被证明可靠的「空区间插入」路径。
- 配套加固：持有区间绑定所属 `ITfContext`，跨宿主/失焦切换时丢弃陈旧区间，不复用其 GetRange 写入。

**本轮新增修复：Ctrl/Alt 组合快捷键透传**

- 现象：Ctrl+A 全选无效——字母 A（0x41）被 `Consumes` 判为可接管，`OnKeyDown` 当作拼音累积并吃掉，宿主收不到快捷键。
- 处置：`OnTestKeyDown` / `OnKeyDown` 先用 `GetKeyState(VK_CONTROL/VK_MENU)` 判断修饰键；Ctrl/Alt 按下时 `pfEaten=0` 直接透传宿主，不进入拼音/选词分流。覆盖 Ctrl+A/C/V/Z 等。

**退格放行修复（v49）**

- 现象：刚上屏后按退格删不掉正文。根因：`OnTestKeyDown` 对 0x08 无条件 `eat=1` 认领，`OnKeyDown` 本地无拼音可删又返回未吃掉；按键既已被我方认领，宿主不再执行原生退格。
- 处置：测试阶段是否接管改为与实际能否消费一致——退格/Esc/回车/空格仅在 `IsComposing` 时认领，数字仅在有对应候选时认领；本地无 composition 时退格直接 `pfEaten=0` 放行，由宿主做原生删除。

**遗留 / 待办**

- 编辑器内拼音 inline 预览仍未显示（当前刻意不写区间，避免再次扰动 composition）；后续在长生命周期区间模型稳定后再补 preedit 写入与 composition 显示属性。
- 粘贴（Ctrl+V）已随快捷键透传，真机确认正常；其余修饰组合（Shift+字母、Win 组合）未专门处理。
- 提交与结束区间仍在同一 SYNC 会话；尚未对齐 Weasel 的独立结束会话、文本服务自身聚合 `ITfCompositionSink`、显示属性提供器。

### 9.8 候选导航、分页与候选窗视觉（beta.3）

**真机复测（记事本 + Edge 搜索栏）**：↑/↓ 行内移动高亮、←/→ 翻页、数字选词、回车/空格提交高亮项、上屏后退格删正文、Ctrl+A / Ctrl+V 透传均通过；无闪退。Core xUnit 45 项全绿，Release 构建 0 警告 0 错误。

**改动要点（`KeyEventSink.cs` / `CandidateWindow.cs`）**

- sink 新增 `_selectedIndex`（跨页高亮序号）与 `_pageOffset`（当前页起始偏移）：
  - ↑/↓ 在当前页内上下移动；高亮越过页首/页末时联动翻页；
  - ←/→ 与 PgUp/PgDn 按每页 9 条翻页，翻页后高亮落在新页首行；单页候选时左右键/翻页键放行宿主；
  - 回车/空格提交当前高亮项（不再固定首选）；数字 1–9 按当前页行号映射到跨页绝对序号；
  - `OnTestKeyDown` 接管判定与 `OnKeyDown` 实际消费保持一致；新输入、退格、Esc、上屏、失焦均重置高亮序号与页码。
- 候选窗视觉：
  - 改用雅黑逻辑字体（`CreateFontW`，字符高 −16），行距 22 → 28px，左右内边距加宽；
  - 窗宽由 `GetTextExtentPoint32W` 按真实字形测量当前页最宽一行，替代原先按字符数 × 估算；4 字及以上长短语不再被裁；宽度上限为屏宽 −16，避免溢出右边界；
  - 候选切片 `Skip(_pageOffset).Take(MaxRows)`，序号标签显示绝对序号；重绘签名含页码 + 高亮 + 文本，三者不变则不刷新。

**遗留**

- PgUp / PgDn 已接翻页（与 ←/→ 共用同一套页偏移逻辑，左右箭头保留）；标准 UIElement 候选登记未做；inline 预览与 composition 显示属性见第 7 节。

### 9.9 Home/End 跳转、Shift 中英切换与按键卡顿（v54）

**新增交互**

- `Home(0x24)`：高亮回到首项，页码回首页；`End(0x23)`：高亮跳到末项，页码对齐到末项所在页首行。
- `Shift` 独立敲击切换中文拼音 / 英文直输：英文模式下字母、数字、空格、回车、退格一律放行宿主原生输入；用 `OnKeyDown` 标记 `_shiftTap`，若期间按下其它键则清除，`OnKeyUp` 仅在独立敲击时翻转 `_englishMode`，避免 Shift+字母组合误切换。切换时丢弃未上屏的中文输入并结束残留区间。

**卡顿优化（第一轮）**

- 日志默认关闭：旧实现「不等于 0 即开启」，未设变量时也默认写日志，每个按键十余次同步 `AppendAllText`（lock + 磁盘写入），是输入卡顿的重要原因；改为只有 `AIIME_TSF_DEBUG=1` 才开启。
- 旧逻辑累积阶段每按一个字母都开一次 `Preedit` 同步编辑会话（区间保持为空、会话内不写文本，属空跑）；改为已有区间后累积阶段不再开会话，只有最终上屏才开 `Commit` 会话。每个字母少一次同步 TSF 会话 + 一次 `GetSelection/InWriteSession`。
- 剩余待查卡顿点：`CandidateRanker.Rank` 对每个候选逐条查 SQLite（词频 / bigram / trigram），候选最多 300 条时按键会触发多次数据库往返；后续可改为一次批量读出词频表再内存打分。



