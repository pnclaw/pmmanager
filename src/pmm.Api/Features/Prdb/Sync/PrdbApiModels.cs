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

record PrdbApiFavoriteActor(
    Guid Id,
    string Name,
    string Gender,
    string Nationality,
    string Ethnicity,
    string? ProfileImageCdnPath,
    DateTime FavoritedAtUtc);

record PrdbApiActorSummary(
    Guid Id,
    string Name,
    int Gender,
    int Nationality,
    int Ethnicity,
    DateOnly? Birthday,
    string? ProfileImageUrl);

record PrdbApiActorDetail(
    Guid Id,
    string Name,
    int Gender,
    DateOnly? Birthday,
    int? BirthdayType,
    DateOnly? Deathday,
    string? Birthplace,
    int Haircolor,
    int Eyecolor,
    int BreastType,
    int? Height,
    int? BraSize,
    string? BraSizeLabel,
    int? WaistSize,
    int? HipSize,
    int Nationality,
    int Ethnicity,
    int? CareerStart,
    int? CareerEnd,
    string? Tattoos,
    string? Piercings,
    List<PrdbApiActorImageDetail> Images,
    List<PrdbApiActorAliasDetail> Aliases,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

record PrdbApiActorAliasDetail(string Name, Guid? SiteId);

record PrdbApiActorImageDetail(Guid Id, int ImageType, string? Url);

record PrdbApiBatchActorsRequest(List<Guid> Ids);

record PrdbApiVideoDetail(
    Guid Id,
    string Title,
    DateOnly? ReleaseDate,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    PrdbApiVideoDetailSite Site,
    List<PrdbApiVideoDetailImage> Images,
    List<PrdbApiVideoDetailPreName> PreNames,
    List<PrdbApiVideoDetailActor> Actors);

record PrdbApiVideoDetailSite(Guid Id, string Title, string Url);

record PrdbApiVideoDetailImage(Guid Id, string? CdnPath);

record PrdbApiVideoDetailPreName(Guid Id, string Title);

record PrdbApiVideoDetailActor(
    Guid Id,
    string Name,
    int Gender,
    DateOnly? Birthday,
    int Nationality,
    List<PrdbApiVideoDetailActorImage> Images);

record PrdbApiVideoDetailActorImage(Guid Id, string? CdnPath, int ImageType);
