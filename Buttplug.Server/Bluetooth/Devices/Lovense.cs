using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Buttplug.Core;
using Buttplug.Core.Messages;

namespace Buttplug.Server.Bluetooth.Devices
{
    internal class LovenseRev1BluetoothInfo : IBluetoothDeviceInfo
    {
        public enum Chrs : uint
        {
            Tx = 0,
            Rx,
        }

        public Guid[] Services { get; } = { new Guid("0000fff0-0000-1000-8000-00805f9b34fb") };

        public string[] Names { get; } =
        {
            // Nora
            "LVS-A011", "LVS-C011",

            // Max
            "LVS-B011",

            // Ambi
            "LVS-L009",
        };

        public Guid[] Characteristics { get; } =
        {
            // tx characteristic
            new Guid("0000fff2-0000-1000-8000-00805f9b34fb"),

            // rx characteristic
            new Guid("0000fff1-0000-1000-8000-00805f9b34fb"),
        };

        public IButtplugDevice CreateDevice(IButtplugLogManager aLogManager,
            IBluetoothDeviceInterface aInterface)
        {
            return new Lovense(aLogManager, aInterface, this);
        }
    }

    internal class LovenseRev2BluetoothInfo : IBluetoothDeviceInfo
    {
        public enum Chrs : uint
        {
            Tx = 0,
            Rx,
        }

        public Guid[] Services { get; } = { new Guid("6e400001-b5a3-f393-e0a9-e50e24dcca9e") };

        public string[] Names { get; } =
        {
            // Lush
            "LVS-S001",

            // Hush
            "LVS-Z001",

            // Hush Prototype
            "LVS_Z001",
        };

        public Guid[] Characteristics { get; } =
        {
            // tx characteristic
            new Guid("6e400002-b5a3-f393-e0a9-e50e24dcca9e"),

            // rx characteristic
            new Guid("6e400003-b5a3-f393-e0a9-e50e24dcca9e"),
        };

        public IButtplugDevice CreateDevice(IButtplugLogManager aLogManager,
            IBluetoothDeviceInterface aInterface)
        {
            return new Lovense(aLogManager, aInterface, this);
        }
    }

    internal class LovenseRev3BluetoothInfo : IBluetoothDeviceInfo
    {
        public enum Chrs : uint
        {
            Tx = 0,
            Rx,
        }

        public Guid[] Services { get; } = { new Guid("50300001-0024-4bd4-bbd5-a6920e4c5653") };

        public string[] Names { get; } =
        {
            // Edge
            "LVS-P36",
        };

        public Guid[] Characteristics { get; } =
        {
            // tx characteristic
            new Guid("50300002-0024-4bd4-bbd5-a6920e4c5653"),

            // rx characteristic
            new Guid("50300003-0024-4bd4-bbd5-a6920e4c5653"),
        };

        public IButtplugDevice CreateDevice(IButtplugLogManager aLogManager,
            IBluetoothDeviceInterface aInterface)
        {
            return new Lovense(aLogManager, aInterface, this);
        }
    }

    internal class LovenseRev4BluetoothInfo : IBluetoothDeviceInfo
    {
        public enum Chrs : uint
        {
            Tx = 0,
            Rx,
        }

        public Guid[] Services { get; } = { new Guid("57300001-0023-4bd4-bbd5-a6920e4c5653") };

        public string[] Names { get; } =
        {
            // Edge
            "LVS-Domi37",
        };

        public Guid[] Characteristics { get; } =
        {
            // tx characteristic
            new Guid("57300002-0023-4bd4-bbd5-a6920e4c5653"),

            // rx characteristic
            new Guid("57300003-0023-4bd4-bbd5-a6920e4c5653"),
        };

        public IButtplugDevice CreateDevice(IButtplugLogManager aLogManager,
            IBluetoothDeviceInterface aInterface)
        {
            return new Lovense(aLogManager, aInterface, this);
        }
    }

    internal class LovenseRev5BluetoothInfo : IBluetoothDeviceInfo
    {
        public enum Chrs : uint
        {
            Tx = 0,
            Rx,
        }

        public Guid[] Services { get; } = { new Guid("5a300001-0024-4bd4-bbd5-a6920e4c5653") };

        public string[] Names { get; } =
        {
            // Hush. Again.
            "LVS-Z36",
        };

        public Guid[] Characteristics { get; } =
        {
            // tx characteristic
            new Guid("5a300002-0024-4bd4-bbd5-a6920e4c5653"),

            // rx characteristic
            new Guid("5a300003-0024-4bd4-bbd5-a6920e4c5653"),
        };

        public IButtplugDevice CreateDevice(IButtplugLogManager aLogManager,
            IBluetoothDeviceInterface aInterface)
        {
            return new Lovense(aLogManager, aInterface, this);
        }
    }

    internal class Lovense : ButtplugBluetoothDevice
    {
        private static Dictionary<string, string> friendlyNames = new Dictionary<string, string>()
        {
            { "LVS-A011", "Nora" },
            { "LVS-C011", "Nora" },
            { "LVS-B011", "Max" },
            { "LVS-L009", "Ambi" },
            { "LVS-S001", "Lush" },
            { "LVS-Z001", "Hush" },
            { "LVS_Z001", "Hush Prototype" },
            { "LVS-P36", "Edge" },
            { "LVS-Z36", "Hush" },
            { "LVS-Domi37", "Domi" },
        };

        private string _accelerometerData;

        public Lovense(IButtplugLogManager aLogManager,
                       IBluetoothDeviceInterface aInterface,
                       IBluetoothDeviceInfo aInfo)
            : base(aLogManager,
                   $"Lovense Device ({friendlyNames[aInterface.Name]})",
                   aInterface,
                   aInfo,
                   1)
        {
            MsgFuncs.Add(typeof(SingleMotorVibrateCmd), new ButtplugDeviceWrapper(HandleSingleMotorVibrateCmd));
            MsgFuncs.Add(typeof(VibrateCmd), new ButtplugDeviceWrapper(HandleSingleMotorVibrateCmd, new Dictionary<string, string>() { { "VibratorCount", "1" } }));
            MsgFuncs.Add(typeof(StopDeviceCmd), new ButtplugDeviceWrapper(HandleStopDeviceCmd));

            if (friendlyNames[aInterface.Name] == "Nora" || friendlyNames[aInterface.Name] == "Max")
            {
                MsgFuncs.Add(typeof(StartAccelerometerCmd), new ButtplugDeviceWrapper(HandleStartAccelerometerCmd));
                MsgFuncs.Add(typeof(StopAccelerometerCmd), new ButtplugDeviceWrapper(HandleStopAccelerometerCmd));
                aInterface.BluetoothMessageReceived += OnBluetoothMessageReceieved;
            }
        }

        private void OnBluetoothMessageReceieved(object sender, BluetoothMessageReceivedEventArgs e)
        {
            _accelerometerData += Encoding.ASCII.GetString(e.Data);
            int chunkIdx = -1;
            while ((chunkIdx = _accelerometerData.IndexOf(';')) >= 0)
            {
                var chunk = _accelerometerData.Substring(0, chunkIdx);
                _accelerometerData = _accelerometerData.Substring(chunkIdx + 1);

                if (chunk.Length == 13 && chunk[0] == 'G')
                {
                    // GEF008312ED00;
                    // [0x00EF, 0x1283, 0x00ED]
                    var axis = new int[] { 0, 0, 0 };
                    for (int i = 0; i < 3; i++)
                    {
                        var data1 = Convert.ToByte(chunk.Substring((i * 2) + 1, 2), 16);
                        var data2 = Convert.ToByte(chunk.Substring((i * 2) + 3, 2), 16);
                        axis[i] = (short)(data1 | data2 << 8);
                    }

                    Console.Out.WriteLine($"x:{axis[0]} y:{axis[1]} z:{axis[2]}");
                }
            }
        }

        private async Task<ButtplugMessage> HandleStopDeviceCmd(ButtplugDeviceMessage aMsg)
        {
            BpLogger.Debug("Stopping Device " + Name);

            if (friendlyNames[Interface.Name] == "Nora" || friendlyNames[Interface.Name] == "Max")
            {
                return await Interface.WriteValue(aMsg.Id,
                    Info.Characteristics[(uint)LovenseRev1BluetoothInfo.Chrs.Tx],
                    Encoding.ASCII.GetBytes($"StopMove:1;"));
            }

            return await HandleSingleMotorVibrateCmd(new SingleMotorVibrateCmd(aMsg.DeviceIndex, 0, aMsg.Id));
        }

        private async Task<ButtplugMessage> HandleSingleMotorVibrateCmd(ButtplugDeviceMessage aMsg)
        {
            var cmdMsg = aMsg as SingleMotorVibrateCmd;
            var cmdMsg2 = aMsg as VibrateCmd;
            if (cmdMsg is null && cmdMsg2 is null)
            {
                return BpLogger.LogErrorMsg(aMsg.Id, Error.ErrorClass.ERROR_DEVICE, "Wrong Handler");
            }

            if (cmdMsg != null)
            {
                _vibratorSpeeds[0] = cmdMsg.Speed;
            }
            else
            {
                foreach (var vi in cmdMsg2.Speeds)
                {
                    if (vi.Index == 0)
                    {
                        _vibratorSpeeds[0] = vi.Speed;
                    }
                }
            }

            // While there are 3 lovense revs right now, all of the characteristic arrays are the same.
            return await Interface.WriteValue(aMsg.Id,
                Info.Characteristics[(uint)LovenseRev1BluetoothInfo.Chrs.Tx],
                Encoding.ASCII.GetBytes($"Vibrate:{(int)(_vibratorSpeeds[0] * 20)};"));
        }

        private async Task<ButtplugMessage> HandleStartAccelerometerCmd(ButtplugDeviceMessage aMsg)
        {
            var cmdMsg = aMsg as StartAccelerometerCmd;
            if (cmdMsg is null)
            {
                return BpLogger.LogErrorMsg(aMsg.Id, Error.ErrorClass.ERROR_DEVICE, "Wrong Handler");
            }

            await Interface.WriteValue(aMsg.Id,
                Info.Characteristics[(uint)LovenseRev1BluetoothInfo.Chrs.Tx],
                Encoding.ASCII.GetBytes($"StartMove:1;"));
            return await Interface.SubscribeValue(aMsg.Id,
                Info.Characteristics[(uint)LovenseRev1BluetoothInfo.Chrs.Rx]);
        }

        private async Task<ButtplugMessage> HandleStopAccelerometerCmd(ButtplugDeviceMessage aMsg)
        {
            var cmdMsg = aMsg as StopAccelerometerCmd;
            if (cmdMsg is null)
            {
                return BpLogger.LogErrorMsg(aMsg.Id, Error.ErrorClass.ERROR_DEVICE, "Wrong Handler");
            }

            return await Interface.WriteValue(aMsg.Id,
                Info.Characteristics[(uint)LovenseRev1BluetoothInfo.Chrs.Tx],
                Encoding.ASCII.GetBytes($"StopMove:1;"));
        }
    }
}
