using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using KhimTools.Tools.Updater.Models;
using KhimTools.Tools.Updater.Services;

internal static class Program
{
    private static int _checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
    }
    private sealed class ResponseHandler : HttpMessageHandler
    {
        private readonly string _content;
        public ResponseHandler(string content) { _content = content; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_content == null) throw new HttpRequestException("SYNTHETIC offline response");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_content) });
        }
    }
    private static async Task<UpdateInfo> Discover(string content)
    {
        using (var client = new HttpClient(new ResponseHandler(content)))
            return await new UpdateService(client).CheckForUpdatesAsync();
    }
    private static async Task Main()
    {
        var empty = new UpdateInfo();
        Check(!empty.DiscoverySucceeded && empty.LatestVersion == "" && empty.ReleaseDate == "", "No fabricated release defaults");
        UpdateInfo valid = await Discover("{\"latest_version\":\"v9.8.7\",\"release_date\":\"SYNTHETIC TEST ONLY\",\"changelog\":[\"SYNTHETIC\"]}");
        Check(valid.DiscoverySucceeded && valid.LatestVersion == "v9.8.7" && valid.ReleaseDate == "SYNTHETIC TEST ONLY", "Valid metadata is preserved exactly");
        UpdateInfo offline = await Discover(null);
        Check(!offline.DiscoverySucceeded && offline.LatestVersion == "" && offline.ReleaseDate == "" && !string.IsNullOrWhiteSpace(offline.DiscoveryError), "Offline failure stays unavailable");
        UpdateInfo malformed = await Discover("{bad-json");
        Check(!malformed.DiscoverySucceeded && malformed.LatestVersion == "", "Malformed JSON cannot become a release");
        UpdateInfo missing = await Discover("{}");
        Check(!missing.DiscoverySucceeded && missing.ReleaseDate == "", "Missing release identity is blocked");
        Check(UpdateService.IsOfficialReleaseUrl("https://github.com/nguyenkhiemkhiem079-boop/KhiemTools_/releases/latest") &&
              !UpdateService.IsOfficialReleaseUrl("https://github.com.evil.invalid/nguyenkhiemkhiem079-boop/KhiemTools_/releases/latest") &&
              !UpdateService.IsOfficialReleaseUrl("file:///C:/updater.exe"), "Download route stays on the official HTTPS repository");
        Console.WriteLine("UPDATER_CONTRACT=PASS (" + _checks + " checks); deterministic fake HTTP, no live network or installation.");
    }
}
