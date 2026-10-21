using System.Text;
using AquaMap.Helpers;

namespace AquaMap.Tecnico.Tests;

// The reservoir form shows "Excluir" only to administrators; the role comes from the stored JWT.
public class JwtRoleTests
{
    private static string Token(string payloadJson)
    {
        static string Encode(string json) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        return $"{Encode("{\"alg\":\"HS256\",\"typ\":\"JWT\"}")}.{Encode(payloadJson)}.assinatura";
    }

    [Fact]
    public void Administrator_role_is_recognized()
    {
        Assert.True(JwtRole.IsAdministrator(Token("{\"nameid\":\"1\",\"unique_name\":\"Ana Cláudia\",\"role\":\"Administrator\"}")));
    }

    [Fact]
    public void Administrator_inside_a_role_list_is_recognized()
    {
        Assert.True(JwtRole.IsAdministrator(Token("{\"role\":[\"Citizen\",\"Administrator\"]}")));
    }

    [Fact]
    public void Technician_is_not_administrator()
    {
        Assert.False(JwtRole.IsAdministrator(Token("{\"role\":\"Citizen\"}")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nao-e-um-jwt")]
    [InlineData("a.%%%.c")]
    public void Missing_or_malformed_token_is_not_administrator(string? token)
    {
        Assert.False(JwtRole.IsAdministrator(token));
    }

    [Fact]
    public void Token_without_role_is_not_administrator()
    {
        Assert.False(JwtRole.IsAdministrator(Token("{\"nameid\":\"1\"}")));
    }
}
