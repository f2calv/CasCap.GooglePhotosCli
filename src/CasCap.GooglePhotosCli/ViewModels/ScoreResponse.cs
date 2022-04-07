using System.Collections.Concurrent;
namespace CasCap.ViewModels;

public class ScoreResponse
{
    /// <summary>
    /// for each unique image id store the matching property combinations, along with a count.
    /// </summary>
    public ConcurrentDictionary<string, MediaItemScore> dScore { get; set; } = new ConcurrentDictionary<string, MediaItemScore>();

    /// <summary>
    /// for each unique property combination store the image ids that count as duplicated.
    /// </summary>
    //public ConcurrentDictionary<GroupByProperty, List<string>> dScore2 { get; set; } = new ConcurrentDictionary<GroupByProperty, List<string>>();
    //public ConcurrentDictionary<GroupByProperty, HashSet<string>> dScore2 { get; set; } = new ConcurrentDictionary<GroupByProperty, HashSet<string>>();

    //record a count of matches per enum
    public ConcurrentDictionary<GroupByProperty, int> dStats { get; set; } = new ConcurrentDictionary<GroupByProperty, int>();
}