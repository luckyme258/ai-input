using AiIme.Core.Models;

namespace AiIme.Core.Dictionary;

/// <summary>
/// 拼音词典召回接口。实现负责把拼音前缀映射到基础候选词，
/// 数据可来自开源词库（见 data/dict）。学习得到的用户词由存储层补充。
/// </summary>
public interface IPinyinDictionary
{
    /// <summary>按拼音前缀召回基础候选（全拼或首字母）。</summary>
    IReadOnlyList<Candidate> Recall(string pinyinPrefix);

    /// <summary>加载/重载词典数据。</summary>
    void Load();
}
