using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Buttplug.Core;
using Buttplug.Core.Messages;

namespace Buttplug.Server.Bluetooth.Devices
{
    internal class YoucupsBluetoothInfo : IBluetoothDeviceInfo
    {
        public enum Chrs : uint
        {
            Tx = 0,
            Rx,
        }

        public Guid[] Services { get; } = { new Guid("0000fee9-0000-1000-8000-00805f9b34fb") };

        public string[] Names { get; } =
        {
            // Warrior II
            "Youcups",
        };

        public Guid[] Characteristics { get; } =
        {
            // tx characteristic
            new Guid("d44bc439-abfd-45a2-b575-925416129600"), // ,

            // rx characteristic
        };

        public IButtplugDevice CreateDevice(IButtplugLogManager aLogManager,
            IBluetoothDeviceInterface aInterface)
        {
            return new Youcups(aLogManager, aInterface, this);
        }
    }

    internal class Youcups : ButtplugBluetoothDevice
    {
        private static Dictionary<string, string> friendlyNames = new Dictionary<string, string>()
        {
            { "Youcups", "Warrior II" },
        };

        public Youcups(IButtplugLogManager aLogManager,
                       IBluetoothDeviceInterface aInterface,
                       IBluetoothDeviceInfo aInfo)
            : base(aLogManager,
                   $"Youcups Device ({friendlyNames[aInterface.Name]})",
                   aInterface,
                   aInfo,
                   1)
        {
            MsgFuncs.Add(typeof(SingleMotorVibrateCmd), HandleSingleMotorVibrateCmd);
            MsgFuncs.Add(typeof(VibrateCmd), HandleSingleMotorVibrateCmd);
            MsgFuncs.Add(typeof(StopDeviceCmd), HandleStopDeviceCmd);
        }

        private async Task<ButtplugMessage> HandleStopDeviceCmd(ButtplugDeviceMessage aMsg)
        {
            BpLogger.Debug("Stopping Device " + Name);
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

            return await Interface.WriteValue(aMsg.Id,
                Info.Characteristics[(uint)YoucupsBluetoothInfo.Chrs.Tx],
                Encoding.ASCII.GetBytes($"$SYS,{(int)(_vibratorSpeeds[0] * 8), 1}?"));
        }
    }
}
