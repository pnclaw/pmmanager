namespace pmm.Api.Features.Prdb;

public class PrdbVideoResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateOnly? ReleaseDate { get; set; }
    public int ActorCount { get; set; }
    public List<PrdbPreNameResponse> PreNames { get; set; } = [];
}

public class PrdbPreNameResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
}
