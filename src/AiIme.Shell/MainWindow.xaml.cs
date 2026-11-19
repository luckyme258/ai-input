using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using AiIme.Core.Dictionary;
using AiIme.Core.Learning;
using AiIme.Core.Models;
using AiIme.Core.Ranking;
using AiIme.Shell.Clipboard;

namespace AiIme.Shell;

/// <summary>
/// 候选窗主窗口：拼音输入 -> 词典召回 -> 排序 -> 选词上屏；整句可粘贴到剪贴板。
/// 第一版不注册全局热键、不拦截全局键盘，交互都在本窗口内完成。
/// </summary>
public partial class MainWindow : Window
{
    private readonly IPinyinDictionary _dictionary;
    private readonly HabitLearner _learner;
    private readonly CandidateRanker _ranker;
    private readonly IClipboardPoster _poster;
    private readonly IUserProfileStore _store;

    private readonly ObservableModel _items = new();
    private readonly List<string> _committedWords = new();
    private readonly List<string> _sessionWords = new();

    public MainWindow(IPinyinDictionary dictionary, HabitLearner learner, CandidateRanker ranker,
        IClipboardPoster poster, IUserProfileStore store)
    {
        _dictionary = dictionary;
        _learner = learner;
        _ranker = ranker;
        _poster = poster;
        _store = store;
        InitializeComponent();
        CandidateList.ItemsSource = _items;
    }

    private InputContext BuildContext() => new()
    {
        PreviousWords = _committedWords,
        RecentSessionWords = _sessionWords,
    };

    /// <summary>
    /// 全局热键唤起：从最小化/隐藏还原窗口，置于前台并聚焦拼音输入框。
    /// </summary>
    public void ActivateForInput()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Show();
        Activate();
        Topmost = true;
        Focus();
        PinyinInput.Focus();
    }

    private void PinyinInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        RefreshCandidates();
    }

    private void RefreshCandidates()
    {
        var query = PinyinInput.Text.Trim().ToLowerInvariant();
        _items.Clear();
        if (string.IsNullOrEmpty(query)) return;

        var recalled = _dictionary.Recall(query);
        var ranked = _ranker.Rank(recalled, BuildContext());

        var index = 1;
        foreach (var c in ranked.Take(20))
        {
            _items.Add(new CandidateItem(index++, c));
        }

        if (_items.Count > 0) CandidateList.SelectedIndex = 0;
    }

    private void PinyinInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                ClearInput();
                e.Handled = true;
                break;
            case Key.Down:
                MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    PasteComposed();
                }
                else if (CandidateList.SelectedItem is CandidateItem item)
                {
                    CommitCandidate(item);
                }
                e.Handled = true;
                break;
            default:
                // 数字键 1..9 直接选词。
                if (e.Key >= Key.D1 && e.Key <= Key.D9)
                {
                    var n = (int)(e.Key - Key.D1);
                    if (n < _items.Count)
                    {
                        CommitCandidate(_items[n]);
                        e.Handled = true;
                    }
                }
                break;
        }
    }

    private void CandidateList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && CandidateList.SelectedItem is CandidateItem item)
        {
            CommitCandidate(item);
            e.Handled = true;
        }
    }

    private void CandidateList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (CandidateList.SelectedItem is CandidateItem item) CommitCandidate(item);
    }

    private void MoveSelection(int delta)
    {
        if (_items.Count == 0) return;
        var next = CandidateList.SelectedIndex + delta;
        next = Math.Clamp(next, 0, _items.Count - 1);
        CandidateList.SelectedIndex = next;
    }

    private void CommitCandidate(CandidateItem item)
    {
        // 是否为纠正：选了非首项。
        var wasCorrection = CandidateList.SelectedIndex > 0;
        var word = item.Candidate.Text;

        _learner.RecordCommit(word, BuildContext(), wasCorrection);
        _committedWords.Add(word);
        if (!_sessionWords.Contains(word)) _sessionWords.Add(word);

        ComposedText.Text = string.Join(string.Empty, _committedWords);
        ClearInput();
        RefreshCandidates();
    }

    private void PasteComposed()
    {
        var text = ComposedText.Text;
        if (string.IsNullOrEmpty(text)) return;

        var backup = _poster.Backup();
        _poster.SetText(text);
        _poster.PasteToForeground();
        // 保留备份以便需要时恢复；第一版暂不自动回写覆盖用户原有剪贴板。
        _ = backup;

        // 粘贴后清空本次会话，等待下一轮输入。
        _committedWords.Clear();
        ComposedText.Text = string.Empty;
        ClearInput();
    }

    private void ClearInput()
    {
        PinyinInput.Text = string.Empty;
        PinyinInput.Focus();
    }

    /// <summary>候选列表展示项（带序号供界面绑定）。</summary>
    private sealed class CandidateItem
    {
        public CandidateItem(int index, Candidate candidate)
        {
            Index = index;
            Candidate = candidate;
        }

        public int Index { get; }
        public Candidate Candidate { get; }
        public string Text => Candidate.Text;
        public string Pinyin => Candidate.Pinyin;
    }

    private sealed class ObservableModel : ObservableCollection<CandidateItem>
    {
    }
}
