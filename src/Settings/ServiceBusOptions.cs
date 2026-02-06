namespace src.Settings;

public class ServiceBusOptions
{
    public string? Connection { get; set; }
    public string InputTopic { get; set; } = "NO_SESSION";
    public string InputSubscription { get; set; } = "STATE_SUB";
    public string OrderedTopic { get; set; } = "ORDERED_TOPIC";
    public string OrderedSubscription { get; set; } = "SESS_SUB";
    public string StateTopic { get; set; } = "NO_SESSION";
    public string StateSubscription { get; set; } = "STATE_SUB";
    public bool UseTransactions { get; set; } = false;
}
