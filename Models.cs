// Models.cs — data models matching the Pico JSON API

using System.Text.Json.Serialization;

namespace BoatTronClient;

public class AlarmInfo
{
    [JsonPropertyName("name")]   public string Name   { get; set; } = "";
    [JsonPropertyName("hours")]  public int    Hours  { get; set; }
    [JsonPropertyName("base_s")] public double BaseS  { get; set; }
    [JsonPropertyName("active")] public bool   Active { get; set; }
    [JsonPropertyName("fired")]  public bool   Fired  { get; set; }

    public AlarmInfo Clone() => new()
    {
        Name   = Name,
        Hours  = Hours,
        BaseS  = BaseS,
        Active = Active,
        Fired  = Fired,
    };
}

public class DeviceInfo
{
    [JsonPropertyName("brand")]         public string           Brand        { get; set; } = "BoatTron";
    [JsonPropertyName("name")]          public string           Name         { get; set; } = "";
    [JsonPropertyName("serial")]        public string           Serial       { get; set; } = "";
    [JsonPropertyName("version")]       public string           Version      { get; set; } = "";
    [JsonPropertyName("device_num")]    public int              DeviceNum    { get; set; }
    [JsonPropertyName("device_tag")]    public string           DeviceTag    { get; set; } = "01";
    [JsonPropertyName("counter_s")]     public double           CounterS     { get; set; }
    [JsonPropertyName("wifi_ssid")]     public string           WifiSsid     { get; set; } = "";
    [JsonPropertyName("wifi_pass")]     public string           WifiPass     { get; set; } = "";
    [JsonPropertyName("ap_pass")]       public string           ApPass       { get; set; } = "";
    [JsonPropertyName("ap_ssid")]       public string           ApSsid       { get; set; } = "";
    [JsonPropertyName("udp_port")]      public int              UdpPort      { get; set; } = 5757;
    [JsonPropertyName("standalone")]    public bool             Standalone   { get; set; }
    [JsonPropertyName("batch")]         public int              Batch        { get; set; }
    [JsonPropertyName("network_name")]  public string           NetworkName  { get; set; } = "";
    [JsonPropertyName("mdns_hostname")] public string           MdnsHostname { get; set; } = "";
    [JsonPropertyName("alarms")]        public List<AlarmInfo>? Alarms       { get; set; }

    // Set by client after discovery — not sent to device
    [JsonIgnore] public string IP { get; set; } = "";

    [JsonIgnore]
    public string HoursDisplay
    {
        get
        {
            int total = (int)CounterS;
            int h = total / 3600;
            int t = (total % 3600) / 360;
            return $"{h}.{t}h";
        }
    }

    [JsonIgnore]
    public bool HasFiredAlarm => Alarms?.Any(a => a.Fired) ?? false;

    [JsonIgnore]
    public bool IsOnLan => !string.IsNullOrEmpty(WifiSsid) && !Standalone;
}
