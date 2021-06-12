namespace SSRPClient
{
    public record SqlInstance
    {
        public string ServerName { get; init; }
        public string InstanceName { get; init; }
        public bool IsClustered { get; init; }
        public string Version { get; init; }
    }
}
