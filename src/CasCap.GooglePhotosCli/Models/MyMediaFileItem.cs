using System.IO;
namespace CasCap.Models;

public class MyMediaFileItem
{
    public FileInfo fileInfo { get; set; }
    public string mimeType { get; set; }
    public string relPath { get; set; }
    public string[] albums { get; set; }
    public string uploadToken { get; set; }
    public MediaItem mediaItem { get; set; }
}