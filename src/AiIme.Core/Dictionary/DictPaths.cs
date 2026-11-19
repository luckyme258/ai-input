namespace AiIme.Core.Dictionary;

/// <summary>
/// 定位随程序分发的词典数据目录（目录下所有 *.tsv 合并加载）。
/// 查找顺序：环境变量 AIIME_DICT_DIR -> 程序目录下 data/dict -> 向上回溯到解决方案 data/dict。
/// </summary>
public static class DictPaths
{
    public const string DictDirectoryName = "dict";

    public static string ResolveDefaultDirectory() => ResolveDefaultDirectory(null);

    /// <summary>
    /// 定位词典数据目录。<paramref name="anchorDirectory"/> 为外壳提供的程序集目录锚点；
    /// 在 .NET COM Hosting（comhost）里 AppContext.BaseDirectory 可能为空，此时以锚点目录为准。
    /// </summary>
    public static string ResolveDefaultDirectory(string? anchorDirectory)
    {
        var env = Environment.GetEnvironmentVariable("AIIME_DICT_DIR");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
        {
            return env;
        }

        // .NET COM Hosting（comhost）在部分宿主进程里 AppContext.BaseDirectory 为空。
        // 解析顺序：锚点目录 -> BaseDirectory -> 进程模块目录 -> 当前工作目录。
        var baseDir = anchorDirectory;
        if (string.IsNullOrWhiteSpace(baseDir)) baseDir = AppContext.BaseDirectory;
        if (string.IsNullOrWhiteSpace(baseDir))
        {
            var modulePath = Environment.ProcessPath;
            baseDir = string.IsNullOrWhiteSpace(modulePath)
                ? Directory.GetCurrentDirectory()
                : Path.GetDirectoryName(modulePath)!;
        }

        var current = new DirectoryInfo(baseDir);
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "data", DictDirectoryName);
            if (Directory.Exists(candidate)) return candidate;
            current = current.Parent;
        }

        // 兜底：锚点目录下的 data/dict。
        return Path.Combine(baseDir, "data", DictDirectoryName);
    }
}
