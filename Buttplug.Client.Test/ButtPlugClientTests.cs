using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Buttplug.Components.WebsocketServer;
using Buttplug.Core;
using Buttplug.Server;
using Xunit;
using Buttplug.Core.Messages;

namespace Buttplug.Client.Test
{
    public class ButtplugClientTests
    {
        public class TestDevice : ButtplugDevice
        {
            public double V1 = 0;
            public double V2 = 0;

            public TestDevice(IButtplugLogManager aLogManager, string aName, string aIdentifier)
                : base(aLogManager, aName, aIdentifier)
            {
                MsgFuncs.Add(typeof(SingleMotorVibrateCmd), new ButtplugDeviceWrapper(HandleSingleMotorVibrateCmd));
                MsgFuncs.Add(typeof(VibrateCmd), new ButtplugDeviceWrapper(HandleVibrateCmd, new MessageAttributes() { FeatureCount = 2 }));
                MsgFuncs.Add(typeof(StopDeviceCmd), new ButtplugDeviceWrapper(HandleStopDeviceCmd));
            }

            private Task<ButtplugMessage> HandleStopDeviceCmd(ButtplugDeviceMessage aMsg)
            {
                V1 = V2 = 0;
                return Task.FromResult<ButtplugMessage>(new Ok(aMsg.Id));
            }

            private Task<ButtplugMessage> HandleVibrateCmd(ButtplugDeviceMessage aMsg)
            {
                var cmdMsg = aMsg as VibrateCmd;
                if (cmdMsg is null)
                {
                    return Task.FromResult<ButtplugMessage>(BpLogger.LogErrorMsg(aMsg.Id, Error.ErrorClass.ERROR_DEVICE, "Wrong Handler"));
                }

                if (cmdMsg.Speeds.Count < 1 || cmdMsg.Speeds.Count > 2)
                {
                    Task.FromResult<ButtplugMessage>(new Error(
                        "VibrateCmd requires between 1 and 2 vectors for this device.",
                        Error.ErrorClass.ERROR_DEVICE,
                        cmdMsg.Id));
                }

                foreach (var vi in cmdMsg.Speeds)
                {
                    if (vi.Index == 0)
                    {
                        V1 = vi.Speed;
                    }
                    else if (vi.Index == 1)
                    {
                        V2 = vi.Speed;
                    }
                    else
                    {
                        Task.FromResult<ButtplugMessage>(new Error(
                            $"Index {vi.Index} is out of bounds for VibrateCmd for this device.",
                            Error.ErrorClass.ERROR_DEVICE,
                            cmdMsg.Id));
                    }
                }

                return Task.FromResult<ButtplugMessage>(new Ok(aMsg.Id));
            }

            private Task<ButtplugMessage> HandleSingleMotorVibrateCmd(ButtplugDeviceMessage aMsg)
            {
                var cmdMsg = aMsg as SingleMotorVibrateCmd;

                if (cmdMsg is null)
                {
                    return Task.FromResult<ButtplugMessage>(BpLogger.LogErrorMsg(aMsg.Id, Error.ErrorClass.ERROR_DEVICE, "Wrong Handler"));
                }

                var speeds = new List<VibrateCmd.VibrateSubcommand>();
                for (uint i = 0; i < 2; i++)
                {
                    speeds.Add(new VibrateCmd.VibrateSubcommand(i, cmdMsg.Speed));
                }

                return HandleVibrateCmd(new VibrateCmd(cmdMsg.DeviceIndex, speeds, cmdMsg.Id));
            }

            public override void Disconnect()
            {
                InvokeDeviceRemoved();
            }
        }

        public class TestDeviceManager : IDeviceSubtypeManager
        {
            private List<TestDevice> _devices = new List<TestDevice>();

            public event EventHandler<DeviceAddedEventArgs> DeviceAdded;

            public event EventHandler<EventArgs> ScanningFinished;

            private bool _scanning = false;

            public bool IsScanning()
            {
                return _scanning;
            }

            public void StartScanning()
            {
                _scanning = true;
            }

            public void StopScanning()
            {
                _scanning = false;
                ScanningFinished?.Invoke(this, new EventArgs());
            }

            public void AddDevice(TestDevice dev, bool raise)
            {
                _devices.Add(dev);
                if (raise)
                {
                    DeviceAdded?.Invoke(this, new DeviceAddedEventArgs(dev));
                }
            }

            public void RemoveDevice(TestDevice dev)
            {
                var d2 = _devices.Find(i => { return i.Identifier == dev.Identifier; });
                if (d2 == null)
                {
                    return;
                }

                d2.Disconnect();
                _devices.Remove(d2);
            }
        }

        public class ButtplugTestServer : ButtplugServer, IButtplugServerFactory
        {
            private TestDeviceManager _manager;

            public ButtplugTestServer(DeviceManager aDevManager, out TestDeviceManager aTestDevManager)
                : base("Test server", 200, aDevManager)
            {
                aTestDevManager = new TestDeviceManager();
                AddDeviceSubtypeManager(aTestDevManager);
            }

            public ButtplugServer GetServer()
            {
                return this;
            }

            public void AddDevice(TestDevice dev)
            {
                _manager.AddDevice(dev, true);
            }

            public void RemoveDevice(TestDevice dev)
            {
                _manager.RemoveDevice(dev);
            }

            public IButtplugLogManager GetLogManager()
            {
                return _bpLogManager;
            }
        }

        private class ButtplugTestClient : ButtplugWSClient
        {
            public ButtplugTestClient(string aClientName)
                : base(aClientName)
            {
            }

            public async Task<ButtplugMessage> SendMsg(ButtplugMessage aMsg)
            {
                return await SendMessage(aMsg);
            }
        }

        [Fact]
        public async void TestConnection()
        {
            var bpDevMgr = new DeviceManager(new ButtplugLogManager());
            var bpSvr = new ButtplugTestServer(bpDevMgr, out var bpTestDevMgr);
            var server = new ButtplugWebsocketServer();
            server.StartServer(bpSvr);

            var client = new ButtplugTestClient("Test client");
            await client.Connect(new Uri("ws://localhost:12345/buttplug"));

            var msgId = client.nextMsgId;
            var res = await client.SendMsg(new Core.Messages.Test("Test string", msgId));
            Assert.True(res != null);
            Assert.True(res is Core.Messages.Test);
            Assert.True(((Core.Messages.Test)res).TestString == "Test string");
            Assert.True(((Core.Messages.Test)res).Id > msgId);

            // Check ping is working
            Thread.Sleep(400);

            msgId = client.nextMsgId;
            res = await client.SendMsg(new Core.Messages.Test("Test string", msgId));
            Assert.True(res != null);
            Assert.True(res is Core.Messages.Test);
            Assert.True(((Core.Messages.Test)res).TestString == "Test string");
            Assert.True(((Core.Messages.Test)res).Id > msgId);

            res = await client.SendMsg(new Core.Messages.Test("Test string"));
            Assert.True(res != null);
            Assert.True(res is Core.Messages.Test);
            Assert.True(((Core.Messages.Test)res).TestString == "Test string");
            Assert.True(((Core.Messages.Test)res).Id > msgId);

            Assert.True(client.nextMsgId > 5);

            bool scanningFinished = false;
            ButtplugClientDevice lastAdded = null;
            ButtplugClientDevice lastRemoved = null;
            client.ScanningFinished += (aSender, aArg) => { scanningFinished = true; };
            client.DeviceAdded += (aSender, aArg) => { lastAdded = aArg.Device; };
            client.DeviceRemoved += (aSender, aArg) => { lastRemoved = aArg.Device; };
            await client.StartScanning();
            bpTestDevMgr.AddDevice(new TestDevice(bpSvr.GetLogManager(), "A", "1"), false);
            Assert.Null(lastAdded);
            bpTestDevMgr.AddDevice(new TestDevice(bpSvr.GetLogManager(), "B", "2"), true);
            Thread.Sleep(100);
            Assert.NotNull(lastAdded);
            Assert.Equal("B", lastAdded.Name);

            Assert.True(!scanningFinished);
            await client.StopScanning();
            Assert.True(scanningFinished);

            Assert.Equal(1, client.getDevices().Length);
            Assert.Equal("B", client.getDevices()[0].Name);
            await client.RequestDeviceList();
            Assert.Equal(2, client.getDevices().Length);
            Assert.Equal("B", client.getDevices()[0].Name);
            Assert.Equal("A", client.getDevices()[1].Name);

            Assert.Null(lastRemoved);
            bpTestDevMgr.RemoveDevice(new TestDevice(bpSvr.GetLogManager(), "B", "2"));
            Thread.Sleep(100);
            Assert.NotNull(lastRemoved);
            Assert.Equal("B", lastRemoved.Name);
            Assert.Equal(1, client.getDevices().Length);
            Assert.Equal("A", client.getDevices()[0].Name);

            // Shut it down
            await client.Disconnect();
            server.StopServer();
        }

        [Fact]
        public async void TestSSLConnection()
        {
            var server = new ButtplugWebsocketServer();
            var bpDevMgr = new DeviceManager(new ButtplugLogManager());
            var bpSvr = new ButtplugTestServer(bpDevMgr, out var bpTestDevMgr);
            server.StartServer(bpSvr, 12346, true, true);

            var client = new ButtplugTestClient("Test client");
            await client.Connect(new Uri("wss://localhost:12346/buttplug"), true);

            var msgId = client.nextMsgId;
            var res = await client.SendMsg(new Core.Messages.Test("Test string", msgId));
            Assert.True(res != null);
            Assert.True(res is Core.Messages.Test);
            Assert.True(((Core.Messages.Test)res).TestString == "Test string");
            Assert.True(((Core.Messages.Test)res).Id > msgId);

            // Check ping is working
            Thread.Sleep(400);

            msgId = client.nextMsgId;
            res = await client.SendMsg(new Core.Messages.Test("Test string", msgId));
            Assert.True(res != null);
            Assert.True(res is Core.Messages.Test);
            Assert.True(((Core.Messages.Test)res).TestString == "Test string");
            Assert.True(((Core.Messages.Test)res).Id > msgId);

            Assert.True(client.nextMsgId > 4);

            await client.RequestDeviceList();

            // Shut it down
            await client.Disconnect();
            server.StopServer();
        }
    }
}
