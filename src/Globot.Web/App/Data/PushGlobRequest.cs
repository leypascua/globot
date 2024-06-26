namespace Globot.Web;

public record PushGlobRequest
{
    public PushGlobRequest(string input, DateTimeOffset? requestDate = null) 
    {
        KnownSource = input;
        RequestDateUtc = requestDate.GetValueOrDefault(DateTimeOffset.UtcNow);
    }

    public DateTimeOffset RequestDateUtc {get; set;}
    public string KnownSource {get; set;}
}

public class PushGlobRequestContext(PushGlobRequest request)
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    
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
