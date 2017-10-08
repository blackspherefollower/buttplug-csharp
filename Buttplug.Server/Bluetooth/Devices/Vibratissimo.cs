using System;
using System.Threading.Tasks;
using Buttplug.Core;
using Buttplug.Core.Messages;

namespace Buttplug.Server.Bluetooth.Devices
{
    internal class VibratissimoBluetoothInfo : IBluetoothDeviceInfo
    {
        public enum Chrs : uint
        {
            TxMode = 0,
            TxSpeed,
            Rx,
        }

        public Guid[] Services { get; } = { new Guid("00001523-1212-efde-1523-785feabcd123") };

        // Device can be renamed, but wildcarding spams our logs and they
        // reuse a common Service UUID, so require it to be the default
        public string[] Names { get; } =
        {
            "Vibratissimo",
        };

        public Guid[] Characteristics { get; } =
        {
            // tx characteristic
            new Guid("00001524-1212-efde-1523-785feabcd123"),
            new Guid("00001526-1212-efde-1523-785feabcd123"),

            // rx characteristic
            new Guid("00001527-1212-efde-1523-785feabcd123"),
        };

        public IButtplugDevice CreateDevice(IButtplugLogManager aLogManager,
            IBluetoothDeviceInterface aInterface)
        {
            return new Vibratissimo(aLogManager, aInterface, this);
        }
    }

    internal class Vibratissimo : ButtplugBluetoothDevice
    {
        public Vibratissimo(IButtplugLogManager aLogManager,
                            IBluetoothDeviceInterface aInterface,
                            IBluetoothDeviceInfo aInfo)
            : base(aLogManager,
                   $"Vibratissimo Device ({aInterface.Name})",
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

            var data = new byte[2];
            data[0] = 0x03;
            data[1] = 0xFF;
            await Interface.WriteValue(aMsg.Id,
                Info.Characteristics[(uint)VibratissimoBluetoothInfo.Chrs.TxMode],
                data);

            data[0] = Convert.ToByte(_vibratorSpeeds[0] * byte.MaxValue);
            data[1] = 0x00;
            return await Interface.WriteValue(aMsg.Id,
                Info.Characteristics[(uint)VibratissimoBluetoothInfo.Chrs.TxSpeed],
                data);
        }
    }
}
