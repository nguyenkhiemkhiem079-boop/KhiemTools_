using System.IO;
namespace KhimTools.DoorSchedule.Services;
internal static class LocalLog
{
    public static string? Write(Exception exception)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KhimTools", "DoorSchedule", "Logs");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"error-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.log");
            File.WriteAllText(path, $"UTC {DateTime.UtcNow:O}\n{exception}", new System.Text.UTF8Encoding(false));
            return path;
        }
        catch (Exception) { return null; }
    }
}
