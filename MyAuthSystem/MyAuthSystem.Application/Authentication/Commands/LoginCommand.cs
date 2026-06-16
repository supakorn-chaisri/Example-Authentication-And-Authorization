namespace MyAuthSystem.Application.Authentication.Commands;

public record LoginCommand(
    string Username,
    string Password
);