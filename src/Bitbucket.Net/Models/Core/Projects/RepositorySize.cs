using Newtonsoft.Json;

namespace Bitbucket.Net.Models.Core.Projects
{
    public class RepositorySize
    {
        [JsonProperty("repository")]
        public long SizeBytes { get; set; }
        
        [JsonProperty("attachments")]
        public long Attachments { get; set; }
    }
}
