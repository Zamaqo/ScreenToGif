using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ScreenToGif.Cloud;

[DataContract]
public class YandexResource
{
    [DataMember(Name = "name")]
    public string Name { get; set; }

    [DataMember(Name = "type")]
    public string Type { get; set; }

    [DataMember(Name = "path")]
    public string Path { get; set; }

    [DataMember(Name = "size")]
    public long Size { get; set; }

    [DataMember(Name = "modified")]
    public string Modified { get; set; }

    [DataMember(Name = "_embedded")]
    public YandexEmbedded Embedded { get; set; }
}

[DataContract]
public class YandexEmbedded
{
    [DataMember(Name = "items")]
    public List<YandexResource> Items { get; set; }
}
