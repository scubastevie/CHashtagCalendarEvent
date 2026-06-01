using CivicPlusCalendar.Models;

namespace CivicPlusCalendar.Tests;

public sealed class AuthResponseTests
{
    [Fact]
    public void GetToken_UsesSnakeCaseTokenFirst()
    {
        var response = new AuthResponse
        {
            AccessTokenSnake = "snake",
            AccessTokenCamel = "camel",
            Token = "plain"
        };

        Assert.Equal("snake", response.GetToken());
    }

    [Fact]
    public void GetToken_FallsBackThroughKnownTokenNames()
    {
        Assert.Equal("camel", new AuthResponse { AccessTokenCamel = "camel", Token = "plain" }.GetToken());
        Assert.Equal("plain", new AuthResponse { Token = "plain" }.GetToken());
        Assert.Null(new AuthResponse().GetToken());
    }
}
