using AiIme.Core.Dictionary;
using AiIme.Core.Learning;
using AiIme.Core.Ranking;
using AiIme.Core.Store;

namespace AiIme.Core;

/// <summary>
/// Core 引擎组装结果：词典、学习器、排序器与记忆存储。
/// 外壳（WPF 辅助工具 / TSF 文本服务）统一从 <see cref="Create"/> 取得依赖，
/// 避免在两处重复装配。
/// </summary>
public sealed record EngineBundle(
    IPinyinDictionary Dictionary,
    HabitLearner Learner,
    CandidateRanker Ranker,
    IUserProfileStore Store);

/// <summary>
/// 引擎组装工厂：加载本地词典、打开本机记忆库，创建学习与排序组件。
/// </summary>
public static class EngineFactory
{
    /// <summary>
    /// 按默认路径组装引擎。词典目录由 <see cref="DictPaths.ResolveDefaultDirectory(string?)"/> 定位；
    /// <paramref name="anchorDirectory"/> 为外壳提供的程序集目录锚点（comhost 下 BaseDirectory 可能为空）。
    /// </summary>
    public static EngineBundle Create(string? anchorDirectory = null)
    {
        var store = new SqliteProfileStore();

        var dictionary = new TrieDictionary(DictPaths.ResolveDefaultDirectory(anchorDirectory));
        dictionary.Load();

        var learner = new HabitLearner(store);
        var ranker = new CandidateRanker(store);

        return new EngineBundle(dictionary, learner, ranker, store);
    }
}
