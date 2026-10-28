using System.Text.Json;

namespace RebirthProfiler;

// Local single-viewer launch request. Parsing has no UI/process side effects.
sealed class LaunchRequest
{
    public string ArgsFile { get; set; }
    public bool? FreshSave { get; set; }
    public bool? PlayMode { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string GameArgs { get; private set; }

    public static LaunchRequest Parse(string json, string requestDirectory)
    {
        if (string.IsNullOrWhiteSpace(json)) return new LaunchRequest();
        var request = JsonSerializer.Deserialize<LaunchRequest>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true,
                UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow });
        if (request == null) throw new InvalidDataException("Launch request must be a JSON object.");
        if (request.ArgsFile != null)
        {
            if (string.IsNullOrWhiteSpace(request.ArgsFile)) throw new InvalidDataException("argsFile is empty.");
            string path = Path.GetFullPath(request.ArgsFile, requestDirectory);
            var file = new FileInfo(path);
            if (!file.Exists || file.Length > 65536) throw new InvalidDataException("argsFile is missing or exceeds 64 KiB.");
            request.GameArgs = File.ReadAllText(path).Trim();
            if (request.GameArgs.Length == 0) throw new InvalidDataException("argsFile contains no arguments.");
        }
        return request;
    }
}
