# AiIme

本地优先的 Windows 拼音输入法：Core 层共享统计型习惯学习（词频 / n-gram / 纠正加权）与拼音召回排序，
对外两条外壳——WPF 辅助工具窗口，以及纯 .NET 的 TSF 全局 COM 文本服务。学习数据只存本机，不上传用户内容。

## 路线定位

| 阶段 | 形态 | 状态 |
|---|---|---|
| V1 原型 | 独立窗口 + 候选 + 本地统计记忆 + 剪贴板粘贴 | WPF 候选窗闭环已跑通 |
| V2 打磨 | 全局热键唤起（Ctrl+空格）、焦点归还、候选窗交互打磨、隐私开关 | 热键已接入 |
| V3（可选） | 本地小模型预测（ONNX / llama.cpp），可插拔 | 接口已预留，未接推理运行时 |
| V4 | TSF 全局输入法外壳（COM 文本服务），复用 Core 引擎 | 真机已跑通激活、按键上屏、候选窗跟随光标；Core 音节切分与前缀召回已落地，见 `docs/TSF联调手册.md` |

## 分层结构

```
src/
├─ AiIme.Core/            # 纯托管、可单测，不依赖任何输入法接口
│   ├─ Models/            # Candidate、InputContext
│   ├─ Learning/          # 词频 + bigram/trigram 学习、衰减、纠正加权
│   ├─ Ranking/           # 候选加权打分排序
│   ├─ Prediction/        # 本地模型预测抽象（ILanguagePredictor + NullPredictor 占位）
│   ├─ Dictionary/        # 拼音词典召回（TrieDictionary + 路径定位）
│   ├─ Store/            # SQLite 记忆存储（词频/bigram/trigram）
│   ├─ Composition/      # 拼音输入状态机（累积/退格/清空 -> 召回排序）
│   └─ EngineFactory.cs   # 引擎组装工厂（供两个外壳复用）
├─ AiIme.Shell/           # WPF 辅助工具外壳（net8.0-windows，自包含单文件）
│   ├─ App.xaml(.cs)     # 依赖组装与启动
│   ├─ MainWindow.xaml(.cs)  # 拼音输入 / 候选列表 / 选词上屏
│   ├─ Hotkey/           # 全局热键接口 + Win32 实现
│   ├─ Clipboard/        # 剪贴板备份/写入/模拟 Ctrl+V
│   └─ Ui/               # 候选窗交互接口
├─ AiIme.Tsf/             # TSF COM 文本服务外壳（net8.0-windows，comhost，框架依赖）
│   ├─ Com/              # TSF 互操作接口声明（ThreadMgr / TextInputProcessor / KeyEventSink / Source / Profiles / Context / Range / EditSession / Composition）
│   ├─ TextService/      # AiImeTextService 入口 + KeyEventSink 分流 + CompositionComposer 编辑会话骨架
│   └─ Registration/     # 文本服务注册/卸载：COM 类项直写注册表 + TSF profile 调用
└─ AiIme.Tsf.RegisterTool/ # 管理员命令行注册工具（register / unregister），引用 Tsf
data/
├─ dict/                  # 词典 TSV（MIT）：hsk-basic.tsv（HSK 教学词）+ common-mit.tsv（常用口语词）
└─ third_party/           # 上游 LICENSE 原文
docs/                     # CLA、设计笔记、TSF 联调手册
tests/                    # Core 层单元测试
```

## 核心模型（第一版）

- **学习**：上屏回写词频；手动选非首项候选做纠正加权；记录 bigram/trigram；定期指数衰减。
- **打分**：基础词频 + 个人词频 + 上下文搭配分 + 会话内上浮 + 本地模型置信度（未接入模型时为 0），权重可配置。
- **隐私**：学习数据只存本机；提供关闭学习与清空数据的开关（待实现）。

## 开源协议

- **代码：AGPL-3.0-or-later**。无论修改后分发为产品，还是部署为网络服务/云端 API，衍生部分均须按 AGPL 开源。
- **数据词典**：按上游协议单独声明，见 `THIRD_PARTY_NOTICES.md`。
- 贡献前阅读 `docs/CLA.md`。

## 开发环境

- .NET 8 SDK（8.0.428，安装在 `%LOCALAPPDATA%\Microsoft\dotnet`，已加入用户 PATH）。
- 解决方案：`AiIme.sln`，含五个项目：
  - `src/AiIme.Core`：类库，核心学习/排序/存储/词典/状态机（被 Shell、TSF 与测试引用）
  - `src/AiIme.Shell`：WPF 可执行外壳（候选窗 + 剪贴板粘贴；热键占位）
  - `src/AiIme.Tsf`：TSF COM 文本服务外壳（comhost，框架依赖）
  - `src/AiIme.Tsf.RegisterTool`：管理员命令行注册工具（register / unregister）
  - `tests/AiIme.Tests`：xUnit 测试
- 常用命令：

```powershell
dotnet build AiIme.sln -c Debug
dotnet test AiIme.sln -c Debug

# 自包含发布（win-x64，单文件，目标机无需安装 .NET 运行时）
dotnet publish src\AiIme.Shell\AiIme.Shell.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true -o dist\win-x64
```

> 自包含单文件 exe 约 156MB（含 WPF 运行时）；词典 TSV 与 LICENSE 放在产物目录 `data/dict/`，随包分发。

- 依赖规划：Microsoft.Data.Sqlite；拼音匹配库待协议核实后引入。

### TSF 外壳注册（需管理员）

TSF 文本服务是框架依赖的 COM 组件（不能自包含单文件），目标机需装 .NET 8 运行时。
构建后以管理员运行注册工具（或 `tools/Install-Tsf.ps1`）：

```powershell
# 构建注册工具（Release）
dotnet build src\AiIme.Tsf.RegisterTool\AiIme.Tsf.RegisterTool.csproj -c Release

# 管理员 PowerShell 中注册 / 卸载
AiIme.Tsf.RegisterTool.exe register
AiIme.Tsf.RegisterTool.exe unregister
```

注册做两件事：① 写 `HKLM\SOFTWARE\Classes\CLSID\{...}\InprocServer32` 指向 `AiIme.Tsf.comhost.dll`；
② `ITfInputProcessorProfiles.Register` + `AddLanguageProfile`（简体中文 0x0804）。
尚未做：类别注册（键盘 TIP 类别 GUID）、语言栏图标、安装器打包。

## 待办

- [x] 安装 .NET 8 SDK，创建解决方案与 Core/Shell/测试项目
- [x] 实现 SqliteProfileStore 建表与读写（词频 / bigram / trigram / 衰减 / 清空）
- [x] 接入 MIT HSK 基础词库 + jieba/pinyin-data 通用口语词库，实现 Trie 全拼/首字母前缀召回（目录多 TSV 合并）
- [x] WPF 候选窗：拼音输入 -> 词典召回 -> 排序 -> 选词上屏；剪贴板备份/写入/模拟 Ctrl+V
- [x] Win32 全局热键（RegisterHotKey/WM_HOTKEY，默认 Ctrl+空格）唤起/聚焦窗口
- [x] 补充 Core 层单元测试（衰减、排序权重、上下文搭配）
- [x] 预留本地小模型预测接口（ILanguagePredictor / NullPredictor），模型分作为一路加权融入排序，未接推理运行时
- [x] 搭建 AiIme.Tsf COM 外壳骨架：TSF 互操作接口声明 + AiImeTextService 入口，复用 Core 引擎
- [x] 挂接 ITfKeyEventSink：按键经 CompositionState 累积拼音并召回排序（字母/退格/Esc 分流）
- [x] TSF 注册/卸载工具：comhost COM 类项注册表直写 + ITfInputProcessorProfiles Register/AddLanguageProfile（管理员）
- [x] TSF 上屏：会话内 GetSelection+SetText 真实写文本、composition 区间起止、类别注册（TFCAT_TIP_KEYBOARD）、自绘候选窗（跟随光标、双缓冲去闪烁）
- [x] Core 拼音音节切分与前缀召回：最长匹配切分、全拼音节序列前缀、首字母兜底、召回封顶
- [ ] TSF 后续：标准 UIElement 候选登记、候选翻页/方向键选择、`OnCompositionTerminated`、跨焦点状态重置、UWP/Chrome 等应用时序复测
- [ ] 核实 PinInCS / ToolGood.Words 协议后再决定是否引入
