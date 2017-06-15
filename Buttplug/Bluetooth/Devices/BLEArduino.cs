using System;
using System.Text;
using System.Threading.Tasks;
using Buttplug.Core;
using Buttplug.Messages;
using JetBrains.Annotations;

namespace Buttplug.Bluetooth.Devices
{
    internal class BLEArduinoBluetoothInfo : IBluetoothDeviceInfo
    {
        public enum Chrs : uint
        {
            Rx = 0,
            Tx = 0
        }
        public string[] Names { get; } = { "" };
        public Guid[] Services { get; } = { new Guid("0000dfb0-0000-1000-8000-00805f9b34fb") };

        public Guid[] Characteristics { get; } =
        {
            // rx+tx
            new Guid("0000dfb1-0000-1000-8000-00805f9b34fb")
        };

        public IButtplugDevice CreateDevice(IButtplugLogManager aLogManager,
            IBluetoothDeviceInterface aInterface)
        {
            return new BLEArduino(aLogManager, aInterface);
        }
    }

    internal class BLEArduino : ButtplugBluetoothDevice
    {
        public BLEArduino(IButtplugLogManager aLogManager,
            IBluetoothDeviceInterface aInterface) :
            base(aLogManager,
                $"BLEArduino {aInterface.Name}",
                aInterface)
        {
            MsgFuncs.Add(typeof(SingleMotorVibrateCmd), HandleBluePlugRawCmd);
            MsgFuncs.Add(typeof(StopDeviceCmd), HandleStopDeviceCmd);
        }

        private Task<ButtplugMessage> HandleStopDeviceCmd([NotNull] ButtplugDeviceMessage aMsg)
        {
            // Right now, this is a nop. The Onyx doesn't have any sort of permanent movement state, 
            // and its longest movement is like 150ms or so. The Pearl is supposed to vibrate but I've 
            // never gotten that to work. So for now, we just return ok.
            return Task.FromResult<ButtplugMessage>(new Ok(aMsg.Id));
        }

        private async Task<ButtplugMessage> HandleBluePlugRawCmd([NotNull] ButtplugDeviceMessage aMsg)
        {
            var cmdMsg = aMsg as SingleMotorVibrateCmd;
            if (cmdMsg is null)
            {
                return BpLogger.LogErrorMsg(aMsg.Id, Error.ErrorClass.ERROR_DEVICE, "Wrong Handler");
            }

            int speed = (int)(cmdMsg.Speed * 256);

            return await Interface.WriteValue(cmdMsg.Id,
                (uint)BLEArduinoBluetoothInfo.Chrs.Tx,
                Encoding.ASCII.GetBytes($"{speed}\n"));
        }
    }
}