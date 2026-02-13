namespace squad_dma
{
    public class DownloadProgress
    {
        public long BytesDownloaded { get; set; }
        public long TotalBytes { get; set; }
        public double SpeedBytesPerSec { get; set; }
        public int PercentComplete => TotalBytes > 0 ? (int)((BytesDownloaded * 100) / TotalBytes) : 0;
        public double MegabytesDownloaded => BytesDownloaded / 1024.0 / 1024.0;
        public double TotalMegabytes => TotalBytes / 1024.0 / 1024.0;
        public double SpeedMBPerSec => SpeedBytesPerSec / 1024.0 / 1024.0;
        public int ETASeconds => SpeedBytesPerSec > 0 ? (int)((TotalBytes - BytesDownloaded) / SpeedBytesPerSec) : 0;
    }
}
