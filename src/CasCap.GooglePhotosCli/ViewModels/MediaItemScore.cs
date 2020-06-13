namespace CasCap.ViewModels
{
    public struct MediaItemScore
    {
        public int count { get; set; }

        public GroupByProperty propertyMatches { get; set; }

        public override string ToString() => $"{count} - {propertyMatches}";
    }
}