using Newtonsoft.Json;

namespace CodePlexScraper
{
    public class Project
    {
        [JsonProperty("@search.score")]
        public float SearchScore { get; set; }

        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("ProjectName")]
        public string Name { get; set; }
        
        [JsonProperty("Title")]
        public string Title { get; set; }

        [JsonProperty("Description")]
        public string Description { get; set; }

        [JsonProperty("TagList")]
        public string Tags { get; set; }
    }
}