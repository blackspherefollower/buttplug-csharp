using Buttplug.Core;
using Buttplug.Core.Messages;
using Xunit;

namespace Buttplug.Server.Test
{
    public class ButtplugJsonMessageParserTests
    {
        private readonly TestServer _server = new TestServer();

        [Fact]
        public void JsonConversionTest()
        {
            var m1 = new Core.Messages.Test("ThisIsATest", ButtplugConsts.SystemMsgId);
            var m2 = new Core.Messages.Test("ThisIsAnotherTest", ButtplugConsts.SystemMsgId);
            var msg = _server.Serialize(m1);
            Assert.True(msg.Length > 0);
            Assert.Equal("[{\"Test\":{\"TestString\":\"ThisIsATest\",\"Id\":0}}]", msg);
            ButtplugMessage[] msgs = { m1, m2 };
            msg = _server.Serialize(msgs);
            Assert.True(msg.Length > 0);
            Assert.Equal("[{\"Test\":{\"TestString\":\"ThisIsATest\",\"Id\":0}},{\"Test\":{\"TestString\":\"ThisIsAnotherTest\",\"Id\":0}}]", msg);
        }

        // Not valid JSON
        [InlineData("not a json message")]

        // Valid json object but no contents
        [InlineData("{}")]

        // Valid json but not an object
        [InlineData("[]")]

        // Not a message type
        [InlineData("[{\"NotAMessage\":{}}]")]

        // Valid json and message type but not in correct format
        [InlineData("[{\"Test\":[]}]")]

        // Valid json and message type but not in correct format
        [InlineData("[{\"Test\":{}}]")]

        // Valid json and message type but with erroneous content
        [InlineData("[{\"Test\":{\"TestString\":\"Error\",\"Id\":0}}]")]

        // Valid json and message type but with extra content
        [InlineData("[{\"Test\":{\"TestString\":\"Yup\",\"NotAField\":\"NotAValue\",\"Id\":0}}]")]
        [Theory]
        public void DeserializeIncorrectMessages(string aMsgStr)
        {
            var res = _server.Deserialize(aMsgStr);
            Assert.True(res.Length == 1);
            Assert.True(res[0] is Error);
        }

        [Fact]
        public void DeserializeCorrectMessage()
        {
            var m = _server.Deserialize("[{\"Test\":{\"TestString\":\"Test\",\"Id\":0}}]");
            Assert.True(m.Length == 1);
            switch (m[0])
            {
                case Error e:
                    Assert.True(false, $"Got Error: {e.ErrorMessage}");
                    break;
                case Core.Messages.Test tm:
                    Assert.True(tm.TestString == "Test");
                    break;
                default:
                    Assert.True(false, $"Got wrong message type {m.GetType().Name}");
                    break;
            }
        }

        [Fact]
        public void SerializeDowngrade()
        {
            var msg = new DeviceList(new DeviceMessageInfo[]
            {
                new DeviceMessageInfo(0, "testDev0", new System.Collections.Generic.Dictionary<string, MessageAttributes>()
                {
                    { "VibrateCmd", new MessageAttributes() { FeatureCount = 2 } },
                }),
                new DeviceMessageInfo(5, "testDev5", new System.Collections.Generic.Dictionary<string, MessageAttributes>()
                {
                    { "LinearCmd", new MessageAttributes() { FeatureCount = 1 } },
                }),
            }, 3);

            var parser = new ButtplugJsonMessageParser(new ButtplugLogManager());
            Assert.Equal("[{\"DeviceList\":{\"Devices\":[{\"DeviceName\":\"testDev0\",\"DeviceIndex\":0,\"DeviceMessages\":{\"VibrateCmd\":{\"FeatureCount\":2}}},{\"DeviceName\":\"testDev5\",\"DeviceIndex\":5,\"DeviceMessages\":{\"LinearCmd\":{\"FeatureCount\":1}}}],\"Id\":3}}]", parser.Serialize(msg, 1));
            Assert.Equal("[{\"DeviceList\":{\"Devices\":[{\"DeviceName\":\"testDev0\",\"DeviceIndex\":0,\"DeviceMessages\":[\"VibrateCmd\"]},{\"DeviceName\":\"testDev5\",\"DeviceIndex\":5,\"DeviceMessages\":[\"LinearCmd\"]}],\"Id\":3}}]", parser.Serialize(msg, 0));
        }
    }
}
