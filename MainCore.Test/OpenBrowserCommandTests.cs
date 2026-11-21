using System.Net;
using MainCore.Infrasturecture.Entities;
using MainCore.Shared.Commands;

namespace MainCore.Test;

public class OpenBrowserCommandTests
{
    private static AccessDto Access(string host, string user, string password) => new()
    {
        Username = "player",
        Password = "secret",
        ProxyHost = host,
        ProxyPort = 8080,
        ProxyUsername = user,
        ProxyPassword = password,
    };

    [Fact]
    public void ProxyCheck_UsesSeparateProxyAndCredentialsPerAccess()
    {
        using var first = OpenBrowserCommand.CreateHttpClientHandler(Access("proxy-a.example", "alice", "pa"));
        using var second = OpenBrowserCommand.CreateHttpClientHandler(Access("proxy-b.example", "bob", "pb"));

        var target = new Uri("https://www.travian.com/international");
        first.Proxy.ShouldNotBeSameAs(second.Proxy);
        first.Proxy!.GetProxy(target)!.Host.ShouldBe("proxy-a.example");
        second.Proxy!.GetProxy(target)!.Host.ShouldBe("proxy-b.example");

        var firstCredential = (NetworkCredential)first.Proxy.Credentials!;
        firstCredential.UserName.ShouldBe("alice");
        firstCredential.Password.ShouldBe("pa");
    }

    [Fact]
    public void ProxyCheck_WithoutProxyHost_ConnectsDirectly()
    {
        using var handler = OpenBrowserCommand.CreateHttpClientHandler(Access("", "", ""));

        handler.UseProxy.ShouldBeFalse();
    }

    [Fact]
    public void ProxyCheck_WithoutProxyUsername_SendsNoCredentials()
    {
        using var handler = OpenBrowserCommand.CreateHttpClientHandler(Access("proxy-a.example", "", ""));

        handler.Proxy!.Credentials.ShouldBeNull();
    }
}
