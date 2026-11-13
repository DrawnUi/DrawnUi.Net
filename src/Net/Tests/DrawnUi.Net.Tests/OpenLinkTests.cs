using DrawnUi.Draw;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>The desktop .NET heads open links as the MAUI and web heads do; an empty link opens nothing.</summary>
public class OpenLinkTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EmptyLink_DoesNothing(string link) => Super.OpenLink(link);
}
