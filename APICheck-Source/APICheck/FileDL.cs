using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using System.Net;
using System.Security.Cryptography;

namespace APICheck
{
    public class FileDL
    {
        private const int MillisecondsTimeout = 100;
        private static string _apikey;
        private static string _package;
        private static string _dataPath;

        private static int _retries = 0;
        private static int _max_retries = 4;
        private static HttpClient _apiClient = new HttpClient();
        private static HttpStatusCode _status;
        private static Stream _fileStream;

        public static void setup(string key, string pkg, string path)
        {
            _apikey = key;
            _package = pkg;
            _dataPath = path;
        }

        public static void DownloadFile(api_objects.downloads file)
        {

            // Create HttpClient object, used to communicate with OS API and retrieve files
            //using (HttpClient apiClient = new HttpClient())
            //{
                // Adds the OS API key to the HTTP header
                _apiClient.DefaultRequestHeaders.Add("key", _apikey);
                _apiClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue(@"*/*"));
                HttpResponseMessage resp;
                List<byte> content = new List<byte>();
                // Retry loop
                for (_retries = 0; _retries < _max_retries; _retries++)
                {
                    _fileStream = null;
                    try
                    {
                        // Get the HTTP response, and the associated content
                        resp = _apiClient.GetAsync(file.url).GetAwaiter().GetResult();
                        _fileStream = _apiClient.GetStreamAsync(file.url).GetAwaiter().GetResult();
                        _status = resp.StatusCode;
                        resp.EnsureSuccessStatusCode();
                        var respBody = resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();

                        

                        // If 0 bytes downloaded
                        if (respBody.Count() != file.size || respBody.Count() == 0)
                        {
                            System.Threading.Thread.Sleep(1500);
                            throw new InvalidDataException("OS API returned incorrect number of bytes, file is invalid");
                        }

#if DEBUG
                        Console.WriteLine("original file size: " + file.size);
                        Console.WriteLine("incoming file size: " + respBody.Count());
#endif




                        // Add each byte from the response into the file array
                        foreach (byte b in respBody)
                        {
                            content.Add(b);
                        }
                        break;
                    }
                    catch (Exception ex)
                    {
                        _retries++;
                        // Increment retries, and check if its reached maximum number of retries
                        if (_retries == _max_retries)
                        {
                            Console.WriteLine("Retry " + _retries + " of " + _max_retries);
                            Console.WriteLine("Error communicating with Ordnance Survey API. Status code: " + _status.ToString()); ;
                            Console.WriteLine(ex.ToString());
                            Console.WriteLine(ex.Message);
                            Console.ReadKey(true);
                            Environment.Exit(1);
                        }
                        
                    }
                    System.Threading.Thread.Sleep(MillisecondsTimeout);

                }


                // Write the content to the file
                //File.WriteAllBytes(_dataPath + @"\" + file.fileName, content.ToArray());
                SaveFileStream(_dataPath + @"\" + file.fileName, _fileStream);
                // Redeclare the byte array, ready for the next file
                content = new List<byte>();
                _fileStream = new MemoryStream();
                // Sleep for 0.1 secs to clear buffers
                System.Threading.Thread.Sleep(100);

            //}
        }

        /// <summary>
        /// Write OS Order file stream to local file
        /// </summary>
        /// <param name="path"></param>
        /// <param name="stream"></param>
        private static void SaveFileStream(string path, Stream stream)
        {
            var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
            stream.CopyTo(fileStream);
            fileStream.Dispose();
        }


        /// <summary>
        /// Compares the MD5SUM generated from the specified file name and the expected MD5 value returned from the API
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="expected_md5"></param>
        /// <returns></returns>
        public static bool MD5Sum(string filename, string expected_md5)
        {
            using (var md5 = MD5.Create())
            {
                try
                {
                    using (var stream = File.OpenRead(filename))
                    {
                        //Console.WriteLine("Expected MD5: " + expected_md5.ToLower());
                        string actual = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
                        //Console.WriteLine("Actual MD5: " + actual);

                        // If MD5SUM values match
                        if (actual.ToLower().Equals(expected_md5.ToLower()))
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
                catch
                {
                    Console.WriteLine("Failed to obtain MD5SUM of downloaded file. Please check your connection and the Ordnance Survey API status.");
                    Environment.Exit(1);
                }
                return false;
            }
        }
    }
}
