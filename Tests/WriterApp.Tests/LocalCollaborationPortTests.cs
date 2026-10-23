using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using WriterApp.Application.Collaboration;
using Xunit;

namespace WriterApp.Tests;

public sealed class LocalCollaborationPortTests
{
    [Theory]
    [InlineData("localhost:5407","http","127.0.0.1","http://localhost:5407",true)]
    [InlineData("localhost:5407","http","::1",null,true)]
    [InlineData("localhost:5398","http","127.0.0.1",null,false)]
    [InlineData("localhost:5407","http","127.0.0.1","http://localhost:5398",false)]
    [InlineData("localhost:5407","http","192.0.2.10",null,false)]
    [InlineData("localhost:5407","https","127.0.0.1",null,false)]
    [InlineData("127.0.0.1:5407","http","127.0.0.1",null,false)]
    public void AlternatePortKeepsExactLoopbackBoundary(string host,string scheme,string address,string? origin,bool expected)
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
            ["Collaboration:LocalTest:Port"]="5407",["Collaboration:LocalTest:CollaborationPort"]="1247"}).Build();
        var context=new DefaultHttpContext();context.Connection.RemoteIpAddress=IPAddress.Parse(address);
        context.Request.Host=new(host);context.Request.Scheme=scheme;
        if(origin is not null)context.Request.Headers.Origin=origin;
        Assert.Equal(expected,LocalCollaborationTest.AcceptsRequest(context,config));
        Assert.Equal("http://localhost:5407",LocalCollaborationTest.LocalOrigin(config));
        Assert.Equal(1247,LocalCollaborationTest.WorkerPort(config));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1023")]
    [InlineData("65536")]
    public void InvalidConfiguredPortsFailClosed(string port)
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
            ["Collaboration:LocalTest:Port"]=port,["Collaboration:LocalTest:CollaborationPort"]=port}).Build();
        Assert.Throws<InvalidOperationException>(()=>LocalCollaborationTest.AppPort(config));
        Assert.Throws<InvalidOperationException>(()=>LocalCollaborationTest.WorkerPort(config));
    }
}
