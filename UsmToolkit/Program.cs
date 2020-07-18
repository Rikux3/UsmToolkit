using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using VGMToolbox.format;

namespace UsmToolkit
{
    [Command("UsmToolkit")]
    [VersionOptionFromMember("--version", MemberName = nameof(GetVersion))]
    [Subcommand(typeof(ExtractCommand), typeof(GetDependenciesCommand))]
    class Program
    {

        static int Main(string[] args)
        {
            try
            {
                return CommandLineApplication.Execute<Program>(args);
            }
            catch (FileNotFoundException e)
            {
                Console.WriteLine($"The file {e.FileName} cannot be found. The program will now exit.");
                return 2;
            }
            catch (Exception e)
            {
                Console.WriteLine($"FATAL ERROR: {e.Message}\n{e.StackTrace}");
                return -1;
            }
        }

        protected int OnExecute(CommandLineApplication app)
        {
            app.ShowHelp();
            return 1;
        }

        private static string GetVersion()
            => typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

        private class ExtractCommand
        {
            private class JoinConfig
            {
                public string VideoParameter { get; set; }
                public string AudioParameter { get; set; }
                public string OutputFormat { get; set; }
            }

            [Required]
            [FileOrDirectoryExists]
            [Argument(0, Description = "File or folder containing usm files")]
            public string InputPath { get; set; }

            [Option(CommandOptionType.NoValue, Description = "Join files after extraction.", ShortName = "j", LongName = "join")]
            public bool Join { get; set; }

            [Option(CommandOptionType.SingleValue, Description = "Specify output directory.", ShortName = "o", LongName = "output-dir")]
            public string OutputDir { get; set; }

            [Option(CommandOptionType.NoValue, Description = "Remove temporary m2v and audio after joining.", ShortName = "c", LongName = "clean")]
            public bool CleanTempFiles { get; set; }

            protected int OnExecute(CommandLineApplication app)
            {
                FileAttributes attr = File.GetAttributes(InputPath);
                if (attr.HasFlag(FileAttributes.Directory))
                {
                    foreach (var file in Directory.GetFiles(InputPath, "*.usm"))
                    {
                        Convert(file);
                    }
                }
                else
                    Convert(InputPath);

                return 0;
            }

            private void Convert(string fileName)
            {
                Console.WriteLine($"File: {fileName}");
                var usmStream = new CriUsmStream(fileName);

                Console.WriteLine("Demuxing...");
                usmStream.DemultiplexStreams(new MpegStream.DemuxOptionsStruct()
                {
                    AddHeader = false,
                    AddPlaybackHacks = false,
                    ExtractAudio = true,
                    ExtractVideo = true,
                    SplitAudioStreams = false
                });

                if (Join)
                {
                    if (!string.IsNullOrEmpty(OutputDir) && !Directory.Exists(OutputDir))
                        Directory.CreateDirectory(OutputDir);

                    JoinOutputFile(usmStream);
                }
            }

            private void JoinOutputFile(CriUsmStream usmStream)
            {
                if (!File.Exists("config.json"))
                {
                    Console.WriteLine("ERROR: config.json not found!");
                    return;
                }

                var audioFormat = usmStream.FinalAudioExtension;
                var pureFileName = Path.GetFileNameWithoutExtension(usmStream.FilePath);

                if (audioFormat == ".adx")
                {
                    //ffmpeg can not handle .adx from 0.2 for whatever reason
                    //need vgmstream to format that to wav
                    if (!Directory.Exists("vgmstream"))
                    {
                        Console.WriteLine("ERROR: vgmstream folder not found!");
                        return;
                    }

                    Console.WriteLine("adx audio detected, convert to wav...");
                    Helpers.ExecuteProcess("vgmstream/test.exe", $"\"{Path.ChangeExtension(usmStream.FilePath, usmStream.FinalAudioExtension)}\" -o \"{Path.ChangeExtension(usmStream.FilePath, "wav")}\"");

                    usmStream.FinalAudioExtension = ".wav";
                }


                Helpers.ExecuteProcess("ffmpeg", CreateFFmpegParameters(usmStream, pureFileName));

                if (CleanTempFiles)
                {
                    Console.WriteLine($"Cleaning up temporary files from {pureFileName}");

                    File.Delete(Path.ChangeExtension(usmStream.FilePath, "wav"));
                    File.Delete(Path.ChangeExtension(usmStream.FilePath, "adx"));
                    File.Delete(Path.ChangeExtension(usmStream.FilePath, "hca"));
                    File.Delete(Path.ChangeExtension(usmStream.FilePath, "m2v"));
                }
            }

            private string CreateFFmpegParameters(CriUsmStream usmStream, string pureFileName)
            {
                JoinConfig conf = JsonConvert.DeserializeObject<JoinConfig>(File.ReadAllText("config.json"));

                StringBuilder sb = new StringBuilder();
                sb.Append($"-i \"{Path.ChangeExtension(usmStream.FilePath, usmStream.FileExtensionVideo)}\" ");
                
                if (usmStream.HasAudio)
                    sb.Append($"-i \"{Path.ChangeExtension(usmStream.FilePath, usmStream.FinalAudioExtension)}\" ");

                sb.Append($"{conf.VideoParameter} ");

                if (usmStream.HasAudio)
                    sb.Append($"{conf.AudioParameter} ");

                sb.Append($"\"{Path.Combine(OutputDir ?? string.Empty, $"{pureFileName}.{conf.OutputFormat}")}\"");

                return sb.ToString();
            }
        }

        private class GetDependenciesCommand
        {
            private class DepsConfig
            {
                public string Vgmstream { get; set; }
                public string FFmpeg { get; set; }
            }

            protected int OnExecute(CommandLineApplication app)
            {
                DepsConfig conf = JsonConvert.DeserializeObject<DepsConfig>(File.ReadAllText("deps.json"));
                WebClient client = new WebClient();

                Console.WriteLine($"Downloading ffmpeg from {conf.FFmpeg}");
                client.DownloadFile(conf.FFmpeg, "ffmpeg.zip");

                Console.WriteLine($"Extracting ffmpeg...");
                using (ZipArchive archive = ZipFile.OpenRead("ffmpeg.zip"))
                {
                    var ent = archive.Entries.FirstOrDefault(x => x.Name == "ffmpeg.exe");
                    if (ent != null)
                    {
                        ent.ExtractToFile("ffmpeg.exe", true);
                    }
                }
                File.Delete("ffmpeg.zip");

                Console.WriteLine($"Downloading vgmstream from {conf.Vgmstream}");
                client.DownloadFile(conf.Vgmstream, "vgmstream.zip");

                Console.WriteLine("Extracting vgmstream...");
                ZipFile.ExtractToDirectory("vgmstream.zip", "vgmstream", true);
                File.Delete("vgmstream.zip");

                return 0;
            }
        }
    }
}
