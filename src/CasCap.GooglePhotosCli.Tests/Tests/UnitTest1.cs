using Xunit;
using Xunit.Abstractions;
namespace CasCap.GooglePhotosCli.Tests
{
    public class UnitTest1 : TestBase
    {
        public UnitTest1(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void Test1()
        {
            //todo: how best to test a command line application?
            Assert.True(true);
        }
    }
}