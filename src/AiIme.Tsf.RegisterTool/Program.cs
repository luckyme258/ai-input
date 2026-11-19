using System.Security.Principal;
using AiIme.Tsf.Registration;

// 用法：
//   AiIme.Tsf.RegisterTool.exe register      注册 TSF 文本服务（需管理员）
//   AiIme.Tsf.RegisterTool.exe unregister   卸载 TSF 文本服务（需管理员）

if (args.Length == 0)
{
    Console.WriteLine("用法: AiIme.Tsf.RegisterTool.exe <register|unregister>");
    return 1;
}

var command = args[0].Trim().ToLowerInvariant();
if (command is not ("register" or "unregister"))
{
    Console.WriteLine($"未知命令: {args[0]}（应为 register 或 unregister）");
    return 1;
}

if (!IsAdministrator())
{
    Console.WriteLine("警告: 当前未以管理员身份运行，写 HKLM 注册表与 TSF profile 可能失败。");
}

// comhost.dll 与本工具输出在同一发布目录。
var baseDir = AppContext.BaseDirectory;
var comhost = Path.Combine(baseDir, "AiIme.Tsf.comhost.dll");
if (!File.Exists(comhost))
{
    Console.WriteLine($"未找到 comhost: {comhost}");
    return 2;
}

var registrar = new TsfRegistrar(comhost, "ai-input 拼音");

try
{
    if (command == "register")
    {
        registrar.Register();
        Console.WriteLine("注册完成。可在 控制面板\\时钟和区域\\语言\\键盘 和语言栏中查看 AiIme 拼音。");
    }
    else
    {
        registrar.Unregister();
        Console.WriteLine("卸载完成。");
    }
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"{command} 失败: {ex.Message}");
    return 3;
}

static bool IsAdministrator()
{
    using var identity = WindowsIdentity.GetCurrent();
    var principal = new WindowsPrincipal(identity);
    return principal.IsInRole(WindowsBuiltInRole.Administrator);
}
