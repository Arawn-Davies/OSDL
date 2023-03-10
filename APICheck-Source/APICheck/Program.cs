using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;
using System.IO;
using static System.Net.WebRequestMethods;
using System.Security.Cryptography;

namespace APICheck
{
    public class Program
    {
        // File lists for comparing remote and local files
        private static List<string> ExpectedFiles;
        private static List<string> ActualFiles;
        private static List<string> MissingFiles;

        // Operation mode booleans (am I running in debug mode, or as part of a script?)
        private static bool is_live = true;
        private static bool download = true;

        // Whether or not to obtain the data package version ID
        private static bool get_order_id;

        // Strings storing the OS API info (package ID, data download location, API auth key)
        private static string package;
        private static string dataPath;
        private static string apikey;

        // Dictionary, stores the URLs for any missing files
        private static Dictionary<string, string> RedownloadLinks;

        static void Main(string[] args)
        {
            ExpectedFiles = new List<string>();
            ActualFiles = new List<string>();
            MissingFiles = new List<string>();
            RedownloadLinks = new Dictionary<string, string>();
            // If running as part of an automation script, check for valid runtime arguments
            if (is_live == true)
            {
                if (args.Length == 0 || args.Length > 4)
                {
                    Console.WriteLine("Insufficient arguments provided.");
                    Console.WriteLine("Usage: <Package ID> <API Key> <Data Path> Optional: <order_num/download>");
                    Console.WriteLine("Exitting.");
                    //Environment.Exit(1);
                }
            }
            // If running in debug mode, not as part of an automation script
            if (is_live == false)
            {
                package = "0040169726";
				// Z mapped to \\lCC80597\e$\
                dataPath = @"Z:\ABC\SetMe\";
                apikey = "ItsASecretSshhh";
                download = true;
                get_order_id = false;
            }
            // Running in live mode as part of automation script -
            // Obtain the API key, data package ID and script path download location
            else
            {
                package = args[0];
                apikey = args[1];
                dataPath = args[2];
                if (args[3] == "order_num")
                {
                    get_order_id = true;
                    download = false;
                }
                else if (args[3] == "download")
                {

                    get_order_id = false;
                    download = true;
                }
                else
                {
                    download = false;
                    get_order_id = false;
                }
            }




            api_objects.Order result;

            // Creates an HttpClient, communicating with the OS API to analyse the data package for what files it has and the version ID of the latest update
            using (HttpClient apiClient = new HttpClient())
            {
                apiClient.DefaultRequestHeaders.Add("key", apikey);
                apiClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue(@"*/*"));

                HttpResponseMessage resp = apiClient.GetAsync("https://api.os.uk/downloads/v1/dataPackages/" + package + "/versions/latest?key=" + apikey).GetAwaiter().GetResult();
                // If HTTP Response status code is not 200 (HTTP OK)
                if (resp.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    Console.WriteLine("Error communicating with Ordnance Survey API. Status code = " + resp.StatusCode.ToString());
                    //Environment.Exit(1);
                }
                // Retry until status code returns 200
                try
                {
                    resp.EnsureSuccessStatusCode();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error communicating with OS API. " + ex.Message);
                    Environment.Exit(1);
                }
                var respBody = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                // Deserialize the JSON object into a C# object
                result = JsonConvert.DeserializeObject<api_objects.Order>(respBody);
            }



            // Obtain the order ID from the datapackage, used for generating the download URL
            if (get_order_id == true)
            {
                Console.WriteLine(result.id);
            }
            // Download the files that are part of the package
            else if (download == true)
            {
                FileDL.setup(apikey, package, dataPath);
                int i = 0;
                // Foreach downloadble file in the data package
                foreach (var item in result.downloads)
                {
                    ++i;
                    Console.WriteLine(item.fileName + " Size: " + item.size);
                    // If file already exists
                    if (System.IO.File.Exists(dataPath + @"\" + item.fileName))
                    {
                        // MD5SUM exists
                        if (item.md5 != null)
                        {
                            // MD5SUM matches
                            if (FileDL.MD5Sum(dataPath + @"\" + item.fileName, item.md5))
                            {
                                Console.WriteLine("File " + i + " out of " + result.downloads.Count() + " already latest version");
                            }
                            // MD5SUM doesn't match
                            else
                            {
                                Console.WriteLine("Downloading " + item.fileName + "...");
                                FileDL.DownloadFile(item);
                                // After downloading the latest file update, check MD5 again
                                if (FileDL.MD5Sum(dataPath + @"\" + item.fileName, item.md5))
                                {
                                    Console.WriteLine("File " + i + " out of " + result.downloads.Count() + " passed!");
                                }
                                else
                                {
                                    Console.WriteLine("MD5 check failed for file " + item.fileName + ". Download will proceed but verification will not be possible.");
                                    FileDL.DownloadFile(item);
                                }
                            }


                        }
                        // MD5SUM doesn't exist
                        else
                        {
                            Console.WriteLine("File " + i + " out of " + result.downloads.Count() + " (" + item.fileName + ") is missing a MD5 checksum value. Download will proceed but verification will not be possible.");
                            FileDL.DownloadFile(item);
                        }
                    }
                    // File doesn't exist locally
                    else
                    {
                        Console.WriteLine("Downloading " + item.fileName + "...");
                        FileDL.DownloadFile(item);
                        // MD5SUM exists
                        if (item.md5 != null)
                        {
                            // MD5SUM matches
                            if (FileDL.MD5Sum(dataPath + @"\" + item.fileName, item.md5))
                            {
                                Console.WriteLine("File " + i + " out of " + result.downloads.Count() + " passed!");
                            }
                            // MD5SUM doesn't match
                            else
                            {
                                Console.WriteLine("MD5 check failed for file " + item.fileName + ". Download will proceed but verification will not be possible");
                                FileDL.DownloadFile(item);
                            } 
                        }
                        // MD5SUM doesn't exist
                        else
                        {
                            Console.WriteLine("File " + i + " out of " + result.downloads.Count() + " (" + item.fileName + ") is missing a MD5 checksum value. Download will proceed but verification will not be possible.");
                        }
                    }
                }
            }
            else
            {
                check(result);
            }
        }

        /// <summary>
        /// Checkt the OS data package for the files it contains, and check each one for a corresponding file stored locally
        /// </summary>
        /// <param name="result"></param>
        private static void check(api_objects.Order result)
        {
            // Get each file in the data package, and store the filename and its URL for re-downloading
            foreach (var item in result.downloads)
            {
                RedownloadLinks.Add(item.fileName, item.url);
                //Console.WriteLine("File: " + item.fileName + "\n");
                ExpectedFiles.Add(item.fileName);
            }

            // Get each file in the local directory, add its filename
            foreach (string file in Directory.GetFiles(dataPath))
            {
                ActualFiles.Add(Path.GetFileName(file));
            }

            // Compare the remote files and local files
            foreach (string file in ExpectedFiles)
            {
                if (!ActualFiles.Contains(file))
                {
                    Console.WriteLine("Missing file: " + file);
                    MissingFiles.Add(file);
                }
            }

            // Not applicable with Change-Only-Updates as local directories will store more than the remote data package
            if (ActualFiles.Count == ExpectedFiles.Count)
            {
                Console.WriteLine("Download completed successfully");
            }
            else
            {
                Console.WriteLine("Mismatch count");

                Console.WriteLine("Actual files: " + ActualFiles.Count + "\n\r");
                Console.WriteLine("Expected files: " + ExpectedFiles.Count + "\n\r");
            }

            // If any missing files, get the URL of each file
            if (MissingFiles.Count >= 1)
            {
                foreach (string f in ExpectedFiles)
                {
                    if (RedownloadLinks.ContainsKey(f))
                    {
                        string url = "";
                        RedownloadLinks.TryGetValue(f, out url);
                        Console.WriteLine(url);
                    }
                }
            }
        }

    }

}