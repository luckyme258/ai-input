using AiIme.Core.Dictionary;

var dictDir = args.Length > 0 ? args[0] : DictPaths.ResolveDefaultDirectory(AppContext.BaseDirectory);
Console.WriteLine($"dict dir: {dictDir}");

var dict = new TrieDictionary(dictDir);
dict.Load();

string[] queries = ["n", "ni", "nih", "nihao", "h", "s", "sf", "shi", "ta", "wo", "nihaoma"];
foreach (var q in queries)
{
    var results = dict.Recall(q);
    var top = string.Join("、", results.Take(5).Select(c => c.Text));
    Console.WriteLine($"{q,-9} count={results.Count,-5} top=[{top}]");
}
