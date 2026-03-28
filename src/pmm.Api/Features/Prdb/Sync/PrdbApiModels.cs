namespace pmm.Api.Features.Prdb.Sync;

// Internal models for deserializing prdb.net API responses

record PrdbApiPagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);

record PrdbApiSite(
    Guid Id,
    string Title,
    string Url,
    Guid? NetworkId,
    string? NetworkTitle);

record PrdbApiVideo(
    Guid Id,
    string Title,
    Guid SiteId,
    string SiteTitle,
    DateOnly? ReleaseDate);

record PrdbApiFavoriteSite(
    Guid Id,
    string Title,
    string Url,
    Guid? NetworkId,
    string? NetworkTitle,
    DateTime FavoritedAtUtc);
