
public record CreateSpecialtyRequest(
    string Name,
    string? Code,
    string? Description
);

public record UpdateSpecialtyRequest(
    string? Name,
    string? Code,
    string? Description,
    bool? IsActive
);