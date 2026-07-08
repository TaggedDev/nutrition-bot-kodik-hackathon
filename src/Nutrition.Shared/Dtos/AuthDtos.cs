namespace Nutrition.Shared.Dtos;

public sealed record RegisterRequestDto(string Email, string FirstName, string SecondName, string Password);

public sealed record LoginRequestDto(string Email, string Password);

public sealed record CurrentUserResponseDto(Guid UserId, string Email, string FirstName, string SecondName);

public sealed record AuthErrorResponseDto(IReadOnlyCollection<string> Errors);