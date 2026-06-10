using IGoLibrary.Ex.Domain.Helpers;

namespace IGoLibrary.Ex.Tests;

public sealed class CodeLinkParserTests
{
    [Fact]
    public void TryExtractCode_ReturnsCode_WhenLinkContainsValidCode()
    {
        const string expected = "1234567890abcdef1234567890ABCDEF";
        var url = $"https://example.com/callback?foo=1&code={expected}&state=1";

        var succeeded = CodeLinkParser.TryExtractCode(url, out var actual);

        Assert.True(succeeded);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryExtractCode_ReturnsCode_WhenLinkIsTraceIntGraphQlRedirect()
    {
        const string expected = "021WFO0w3lHhd73hok0w3AIC6n3WFO0k";
        const string url = "http://wechat.v2.traceint.com/index.php/graphql/?operationName=index&query=query%7BuserAuth%7BtongJi%7Brank%7D%7D%7D&code=021WFO0w3lHhd73hok0w3AIC6n3WFO0k&state=1";

        var succeeded = CodeLinkParser.TryExtractCode(url, out var actual);

        Assert.True(succeeded);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.com/callback?code=too-short")]
    [InlineData("https://example.com/callback?state=1")]
    public void TryExtractCode_ReturnsFalse_WhenLinkDoesNotContainValidCode(string? url)
    {
        var succeeded = CodeLinkParser.TryExtractCode(url, out var actual);

        Assert.False(succeeded);
        Assert.Equal(string.Empty, actual);
    }
}
