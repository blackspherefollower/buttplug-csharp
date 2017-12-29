using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Buttplug.Client.Test
{
    public class ArgsTests
    {
        [Fact]
        public void ErrorEventArgsTest()
        {
            var arg = new ErrorEventArgs(new Core.Messages.Error("foo", Core.Messages.Error.ErrorClass.ERROR_UNKNOWN, 0));
            Assert.Equal("foo", arg.Message.ErrorMessage);
            Assert.Null(arg.Exception);

            arg = new ErrorEventArgs(new Exception("bar"));
            Assert.Equal("bar", arg.Message.ErrorMessage);
            Assert.Equal("bar", arg.Exception.Message);
        }

        [Fact]
        public void LogEventArgsTest()
        {
            var arg = new LogEventArgs(new Core.Messages.Log(Core.ButtplugLogLevel.Debug, "test"));
            Assert.Equal("test", arg.Message.LogMessage);
            Assert.Equal(Core.ButtplugLogLevel.Debug, arg.Message.LogLevel);
        }

        [Fact]
        public void ScanningFinishedEventArgsTest()
        {
            var arg = new ScanningFinishedEventArgs(new Core.Messages.ScanningFinished());
            Assert.Equal(0U, arg.Message.Id);
        }
    }
}
