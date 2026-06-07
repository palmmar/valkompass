namespace Valkompass.Application.Dtos;

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthUserDto(string Email, IReadOnlyList<string> Roles);
