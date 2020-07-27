namespace UsmToolkit
{
    public class JoinConfig
    {
        public string VideoParameter { get; set; }
        public string AudioParameter { get; set; }
        public string OutputFormat { get; set; }
    }

    public class DepsConfig
    {
        public string Vgmstream { get; set; }
        public string FFmpeg { get; set; }
    }
}
