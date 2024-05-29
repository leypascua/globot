namespace Globot.Web;

public record PushGlobRequest
{
    public PushGlobRequest(string input, DateTimeOffset? requestDate = null)
    {
        KnownSources = [input];
        RequestDateUtc = requestDate.GetValueOrDefault(DateTimeOffset.UtcNow);
    }

    public PushGlobRequest(params string[] inputs)
    {
        KnownSources = inputs;
        RequestDateUtc = DateTimeOffset.UtcNow;
    }

    public DateTimeOffset RequestDateUtc {get; set;}
    public string[] KnownSources {get;set;} = [];
}

public class PushGlobRequestContext(PushGlobRequest request)
{
    public PushGlobRequest Request { get; set; } = request;

    public PushGlobRequestStatus Status { get; set; }

    public enum PushGlobRequestStatus
    {
        Submitted = 0,
        Running = 1,
        FinishedWithErrors = 2,
        Finished = 3
    }
}
