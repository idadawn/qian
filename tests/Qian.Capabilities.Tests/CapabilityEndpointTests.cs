using Microsoft.AspNetCore.Http;
using Qian.Capabilities.AspNetCore;

namespace Qian.Capabilities.Tests;

// S1-RT-019 / S1-RT-020：别名的 successor-version 链接带宿主 PathBase，查询串按 RFC 3986 转义，孤立代理项按替换字符编码。
public sealed class CapabilityEndpointTests
{
    [Fact]
    public void 别名后继链接带PathBase_保留合法字符与百分号编码_转义尖括号引号空格非ASCII与残缺百分号()
    {
        Assert.Equal("</api/capabilities>; rel=\"successor-version\"", Link("", ""));
        Assert.Equal("</qian/api/capabilities?kind=query&q=a%2Fb:@!$'()*+,;=/?-._~>; rel=\"successor-version\"", Link("/qian", "?kind=query&q=a%2Fb:@!$'()*+,;=/?-._~"));
        Assert.Equal("</qian/api/capabilities?q=%3Cx%3E%22%20%E5%AE%A2%7C%5B%5D%25zz%25>; rel=\"successor-version\"", Link("/qian", "?q=<x>\" 客|[]%zz%"));
    }

    [Fact]
    public void 别名后继链接里孤立的UTF16代理项按替换字符编码_成对代理项按UTF8编码()
    {
        // 代理项用码元值拼出：高位后跟非低位、单独的低位、结尾的高位各一处，另有一个成对的 U+1F600。
        var query = "?q=a" + (char)0xD800 + "b" + (char)0xDC00 + "c" + char.ConvertFromUtf32(0x1F600) + "d" + (char)0xDBFF;
        Assert.Equal("</qian/api/capabilities?q=a%EF%BF%BDb%EF%BF%BDc%F0%9F%98%80d%EF%BF%BD>; rel=\"successor-version\"", Link("/qian", query));
    }

    private static string Link(string pathBase, string query)
    {
        var http = new DefaultHttpContext();
        http.Request.PathBase = new PathString(pathBase);
        http.Request.QueryString = new QueryString(query);
        return CapabilityEndpoints.SuccessorLink(http.Request, "/api/capabilities");
    }
}
