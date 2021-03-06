﻿using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using VGMToolbox.format;

namespace UsmToolkit
{
    [Command(Description = "Extract audio and video")]
    public class ExtractCommand
    {
        [Required]
        [FileOrDirectoryExists]
        [Argument(0, Description = "File or folder containing usm files")]
        public string InputPath { get; set; }

        protected int OnExecute(CommandLineApplication app)
        {
            FileAttributes attr = File.GetAttributes(InputPath);
            if (attr.HasFlag(FileAttributes.Directory))
            {
                foreach (var file in Directory.GetFiles(InputPath, "*.usm"))
                    Process(file);
            }
            else
                Process(InputPath);

            return 0;
        }

        private void Process(string fileName)
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
        }
    }
    
    [Command(Description = "Convert according to the parameters in config.json")]
    public class ConvertCommand
    {
        [Required]
        [FileOrDirectoryExists]
        [Argument(0, Description = "File or folder containing usm files")]
        public string InputPath { get; set; }

        [Option(CommandOptionType.SingleValue, Description = "Specify output directory", ShortName = "o", LongName = "output-dir")]
        public string OutputDir { get; set; }

        [Option(CommandOptionType.NoValue, Description = "Remove temporary m2v and audio after converting", ShortName = "c", LongName = "clean")]
        public bool CleanTempFiles { get; set; }

        protected int OnExecute(CommandLineApplication app)
        {
            FileAttributes attr = File.GetAttributes(InputPath);
            if (attr.HasFlag(FileAttributes.Directory))
            {
                foreach (var file in Directory.GetFiles(InputPath, "*.usm"))
                    Process(file);
            }
            else
                Process(InputPath);

            return 0;
        }

        private void Process(string fileName)
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

            if (!string.IsNullOrEmpty(OutputDir) && !Directory.Exists(OutputDir))
                Directory.CreateDirectory(OutputDir);

            JoinOutputFile(usmStream);
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

            Helpers.ExecuteProcess("ffmpeg", Helpers.CreateFFmpegParameters(usmStream, pureFileName, OutputDir));

            if (CleanTempFiles)
            {
                Console.WriteLine($"Cleaning up temporary files from {pureFileName}");

                File.Delete(Path.ChangeExtension(usmStream.FilePath, "wav"));
                File.Delete(Path.ChangeExtension(usmStream.FilePath, "adx"));
                File.Delete(Path.ChangeExtension(usmStream.FilePath, "hca"));
                File.Delete(Path.ChangeExtension(usmStream.FilePath, "m2v"));
            }
        }
    }

    [Command(Description = "Setup ffmpeg and vgmstream needed for conversion")]
    public class GetDependenciesCommand
    {
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
