namespace CodePlexScraper
{
    public class Archiver
    {
        public enum ResolutionBehavior
        {
            Overwrite,
            Save,
            Skip
        }

        public ResolutionBehavior DuplicateResolutionBehavior { get; set; } = ResolutionBehavior.Save;

        public uint StartAtIndex { get; set; } = 0;
        public uint ItemsPerQuery { get; set; } = 200;

        public bool SaveZips { get; set; } = true;
        public bool SaveMetadata { get; set; } = true;

        public string SearchQuery { get; set; } = "*";

        public bool QuitOnApiFailure { get; set; } = false;
    }
}