namespace CasCap.Models;

public class AppConfig
{
    public DateTime lastCheck { get; set; } = DateTime.MinValue;
    public DateTime latestMediaItemCreation { get; set; } = DateTime.MinValue;
}