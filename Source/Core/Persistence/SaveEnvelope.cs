using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cultiway.Core.Persistence;

internal sealed class SaveEnvelope
{
    public const int CurrentFormatVersion = 1;

    [JsonProperty("formatVersion")]
    public int FormatVersion = CurrentFormatVersion;

    [JsonProperty("documentId")]
    public string DocumentId;

    [JsonProperty("dataVersion")]
    public int DataVersion;

    [JsonProperty("data")]
    public JObject Data;
}
