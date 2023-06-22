namespace Bitbucket.Net.Models.Core.Projects
{
    public class FromToRef
    {
        public string Id { get; set; }
        public string DisplayId { get; set; }
        public string LatestCommit { get; set; }
        public RepositoryRef Repository { get; set; }
    }
}