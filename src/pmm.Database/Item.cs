using pmm.Database.Common;

namespace Pmm.Database;

public class Item : BaseEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
