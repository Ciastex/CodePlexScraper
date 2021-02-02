using System;
using System.Buffers.Text;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CodePlexScraper
{
    public class ProjectArchiver
    {
        private readonly WebClient _webClient = new();
        private readonly Archiver _options = new();

        private string _lastCallResult = string.Empty;

        private StreamWriter _hardFailureLogWriter = new("hard_fails.log") {AutoFlush = true};
        private StreamWriter _lastCallResultWriter = new("last_call_result.log") {AutoFlush = true};
        private StreamWriter _exceptionLogWriter = new("exceptions.log") {AutoFlush = true};
        private StreamWriter _lastIndexWriter = new("last_index.txt") {AutoFlush = true};

        public ProjectArchiver(Archiver options = null)
        {
            if (options != null)
            {
                _options = options;
            }
        }

        public void Start()
        {
            var skip = _options.StartAtIndex;

            while (true)
            {
                JObject jObject;

                try
                {
                    jObject = SearchApiCall(skip, _options.ItemsPerQuery, _options.SearchQuery);
                }
                catch (Exception e)
                {
                    Console.WriteLine("API call failure. Check the logs for details.");
                    _lastCallResultWriter.WriteLine(_lastCallResult);
                    _exceptionLogWriter.WriteLine($"----\n{e}");

                    if (_options.QuitOnApiFailure)
                        break;

                    continue;
                }

                var projects = jObject["value"].Value<JArray>();
                foreach (var projectObject in projects)
                {
                    var retries = 3;

                    __download:
                    var projectName = projectObject["ProjectName"].Value<string>();
                    Console.WriteLine($"({skip}) Now archiving: {projectName}...");

                    try
                    {
                        ArchiveProject(projectObject.ToObject<JObject>());
                        skip++;
                    }
                    catch
                    {
                        if (retries > 0)
                        {
                            Console.WriteLine($"Failure. Retrying. ({retries--} more time(s)...).");
                            goto __download;
                        }
                        else
                        {
                            _hardFailureLogWriter.WriteLine($"{skip} | {projectName}");
                            skip++;
                        }
                    }

                    _lastIndexWriter.BaseStream.Seek(0, SeekOrigin.Begin);
                    _lastIndexWriter.WriteLine(skip);
                }
            }
        }

        private JObject SearchApiCall(uint skip = 0, uint top = 200, string search = "*")
        {
            var url =
                "https://codeplexarchive-search.search.windows.net/indexes/codeplexarchive-index/docs" +
                $"?api-version=2016-09-01&api-key=44C8CF90A6561D9EC9A1BBA09250FEA1&$top={top}&$skip={skip}&search={HttpUtility.UrlEncode(search)}";

            _lastCallResult = _webClient.DownloadString(url);
            return JsonConvert.DeserializeObject<JObject>(_lastCallResult);
        }

        private void ArchiveProject(JObject prj, string targetDirectory = "archivedProjects")
        {
            var project = prj.ToObject<Project>();
            var targetDir = Path.Combine(AppContext.BaseDirectory, targetDirectory);

            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            if (_options.SaveMetadata)
                SaveProjectMetadata(targetDir, project);

            if (_options.SaveZips)
                DownloadProjectZip(targetDir, project);
        }

        private void DownloadProjectZip(string targetDir, Project project)
        {
            var targetFilePath = Path.Combine(targetDir, $"{project.Name}.zip");

            if (File.Exists(targetFilePath))
            {
                Console.Write($"'{project.Name}' exists. ");
                if (_options.DuplicateResolutionBehavior == Archiver.ResolutionBehavior.Save)
                {
                    var i = 0;
                    var name = string.Empty;

                    while (File.Exists(targetFilePath))
                    {
                        name = $"{project.Name} ({++i}).zip";
                        targetFilePath = Path.Combine(targetDir, name);
                    }

                    Console.WriteLine($"Saved to '{name}' instead.");
                }
                else if (_options.DuplicateResolutionBehavior == Archiver.ResolutionBehavior.Skip)
                {
                    Console.WriteLine("Skipped.");
                    return;
                }
                else
                {
                    Console.WriteLine("Overwriting.");
                }
            }

            _webClient.DownloadFile(
                $"https://codeplexarchive.blob.core.windows.net/archive/projects/{project.Name}/{project.Name}.zip",
                targetFilePath
            );
        }

        private void SaveProjectMetadata(string targetDir, Project project)
        {
            var targetFilePath = Path.Combine(targetDir, $"{project.Name}.json");

            using (var sw = new StreamWriter(targetFilePath))
                sw.Write(JsonConvert.SerializeObject(project, Formatting.Indented));


            var link = $"https://codeplexarchive.blob.core.windows.net/archive/metadata/{project.Name.ToLower()}.json";

            __retry:
            try
            {
                _webClient.DownloadFile(link,
                    Path.Combine(targetDir, $"{project.Name}.meta.json"));
            }
            catch (Exception e)
            {
                if (link.EndsWith("json5"))
                {
                    link = link.Substring(0, link.Length - 1);
                    goto __retry;
                }

                Console.WriteLine($"META: {project.Name} -- {e.Message}");
                _exceptionLogWriter.WriteLine($"---\nFailed to download metafile for {project.Name}: {e}");
            }
        }
    }
}