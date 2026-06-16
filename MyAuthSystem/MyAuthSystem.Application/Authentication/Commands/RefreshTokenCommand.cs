namespace MyAuthSystem.Application.Authentication.Commands;

public record RefreshTokenCommand(
    string ExpiredAccessToken,
    string RefreshToken
);