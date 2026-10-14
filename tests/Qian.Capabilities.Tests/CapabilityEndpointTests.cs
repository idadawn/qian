using Microsoft.AspNetCore.Http;
using Qian.Capabilities.AspNetCore;

namespace Qian.Capabilities.Tests;

// S1-RT-019：别名的 successor-version 链接带宿主 PathBase，查询串按 RFC 3986 转义。
public sealed class CapabilityEndpointTests
{
    [Fact]
    public void 别名后继链接带PathBase_保留合法字符与百分号编码_转义尖括号引号空格非ASCII与残缺百分号()
    {
        Assert.Equal("</api/capabilities>; rel=\"successor-version\"", Link("", ""));
        Assert.Equal("</qian/api/capabilities?kind=query&q=a%2Fb:@!$'()*+,;=/?-._~>; rel=\"successor-version\"", Link("/qian", "?kind=query&q=a%2Fb:@!$'()*+,;=/?-._~"));
        Assert.Equal("</qian/api/capabilities?q=%3Cx%3E%22%20%E5%AE%A2%7C%5B%5D%25zz%25>; rel=\"successor-version\"", Link("/qian", "?q=<x>\" 客|[]%zz%"));
    }

    private static string Link(string pathBase, string query)
    {
        var http = new DefaultHttpContext();
        http.Request.PathBase = new PathString(pathBase);
        http.Request.QueryString = new QueryString(query);
        return CapabilityEndpoints.SuccessorLink(http.Request, "/api/capabilities");
    }
}
