using System.Text.Json.Serialization;

namespace CivicPlusCalendar.Models;

public sealed class AuthRequest
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
}

public sealed class AuthResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessTokenSnake { get; set; }

    [JsonPropertyName("accessToken")]
    public string? AccessTokenCamel { get; set; }

    [JsonPropertyName("token")]
    public string? Token { get; set; }

    public string? GetToken()
    {
        return AccessTokenSnake ?? AccessTokenCamel ?? Token;
    }
}
