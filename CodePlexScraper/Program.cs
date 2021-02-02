using System;
using System.Buffers.Text;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CodePlexScraper
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Contains("--help") || args.Contains("-h") || args.Contains("/?"))
            {
                Console.WriteLine("--dont-save-zips: skip project ZIP download.");
                Console.WriteLine("--dont-save-meta: skip metadata archivization.\n");
                Console.WriteLine("--skip-duplicates: if duplicate project is encountered, skip it.");
                Console.WriteLine("--save-duplicates: if duplicate project is encountered, save them both.");
                Console.WriteLine(
                    "--overwrite-duplicates: if duplicate project is encountered, overwrite the existing.\n");
                Console.WriteLine("--quit-on-api-failure: Immediately stop on API failures.\n");
                Console.WriteLine("--skip <uint>: Start archivization at index <uint>.");
                Console.WriteLine("--items-per-query <uint>: Request <uint> items per API call.");
                Console.WriteLine("--search-query <string>: Search all entries containing <string>.\n");
                Console.WriteLine("--meta-catachup: catch-up on metadata files. You probably don't need this.");
                return;
            }

            if (args.Contains("--meta-catchup"))
            {
                CatchupMetadata();
                return;
            }

            var options = new Archiver();

            if (args.Contains("--dont-save-zips"))
                options.SaveZips = false;
            else if (args.Contains("--dont-save-meta"))
                options.SaveMetadata = false;

            if (args.Contains("--skip-duplicates"))
                options.DuplicateResolutionBehavior = Archiver.ResolutionBehavior.Skip;
            else if (args.Contains("--save-duplicates"))
                options.DuplicateResolutionBehavior = Archiver.ResolutionBehavior.Save;
            else if (args.Contains("--overwrite-duplicates"))
                options.DuplicateResolutionBehavior = Archiver.ResolutionBehavior.Overwrite;

            if (args.Contains("--quit-on-api-failure"))
                options.QuitOnApiFailure = true;

            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == "--skip")
                {
                    if (++i < args.Length)
                    {
                        var skip = 0u;
                        if (!uint.TryParse(args[i], out skip))
                        {
                            Quit("Invalid skip parameter provided.");
                        }
                        options.StartAtIndex = skip;
                    }
                    else
                    {
                        Quit("--skip requires an uint paramter.");
                    }
                }

                if (args[i] == "--items-per-query")
                {
                    if (++i < args.Length)
                    {
                        var top = 0u;
                        if (!uint.TryParse(args[i], out top))
                        {
                            Quit("Invalid items-per-query parameter provided.");
                        }

                        options.ItemsPerQuery = top;
                    }
                    else
                    {
                        Quit("--items-per-query requires an uint parameter.");
                    }
                }

                if (args[i] == "--search-query")
                {
                    if (++i < args.Length)
                    {
                        options.SearchQuery = args[i];
                    }
                    else
                    {
                        Quit("--search-query requires a string.");
                    }
                }
            }

            var archivizer = new ProjectArchiver(options);
            archivizer.Start();
        }

        private static void Quit(string why)
        {
            Console.WriteLine(why);
            Environment.Exit(1);
        }

        private static void CatchupMetadata()
        {
            var wc = new WebClient();
            var files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "archivedProjects"), "*.json");

            foreach (var f in files)
            {
                Console.WriteLine(f);

                using (var sr = new StreamReader(f))
                {
                    var project = JsonConvert.DeserializeObject<Project>(sr.ReadToEnd());

                    var link =
                        $"https://codeplexarchive.blob.core.windows.net/archive/metadata/{project.Name.ToLower()}.json";

                    __retry:
                    try
                    {
                        Console.Write(link);
                        wc.DownloadFile(link,
                            Path.Combine(AppContext.BaseDirectory, "archivedProjects", $"{project.Name}.meta.json"));
                        Console.WriteLine(" OK!");
                    }
                    catch (Exception e)
                    {
                        if (link.EndsWith("json5"))
                        {
                            Console.WriteLine(" ERR");

                            link = link.Substring(0, link.Length - 1);
                            goto __retry;
                        }

                        Console.WriteLine($"\nFailed to download {link}: {e.Message}");
                    }
                }
            }
        }
    }
}