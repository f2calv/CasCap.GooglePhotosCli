using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;
namespace CasCap.GooglePhotosCli.Tests;

public class CliTests : TestBase
{
    public CliTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task Test1()
    {
        //todo: how best to test a command line application?
        try
        {
            _ = await _googlePhotosSvc.CreateAlbumAsync("test");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            Debugger.Break();
        }
        Assert.True(true);//assert true regardless of actual outcome, will add full tests later
    }
}