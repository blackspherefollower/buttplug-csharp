using System;
using System.Text;
using System.Threading.Tasks;
using Buttplug.Core;
using Buttplug.Core.Messages;
using System.Collections.Generic;

namespace Buttplug.Server.Bluetooth.Devices
{
    internal class LetenBluetoothInfo : IBluetoothDeviceInfo
    {
        public enum Chrs : uint
        {
            Tx = 0,
            Rx,
        }

        /*
2017-08-31 14:17:53.1794|DEBUG|UWPBluetoothDeviceFactory|Found service UUID: 0000fff0-0000-1000-8000-00805f9b34fb (F520A-LT)
2017-08-31 14:17:53.1794|DEBUG|UWPBluetoothDeviceFactory|Found service UUID: f000ffc0-0451-4000-b000-000000000000 (F520A-LT)
2017-08-31 14:17:53.1794|DEBUG|UWPBluetoothDeviceFactory|Found service UUID: 00001800-0000-1000-8000-00805f9b34fb (F520A-LT)
2017-08-31 14:17:53.1794|DEBUG|UWPBluetoothDeviceFactory|Found service UUID: 00001801-0000-1000-8000-00805f9b34fb (F520A-LT)
2017-08-31 14:17:53.1794|DEBUG|UWPBluetoothDeviceFactory|Found service UUID: 0000180a-0000-1000-8000-00805f9b34fb (F520A-LT)
2017-08-31 14:17:53.1794|DEBUG|UWPBluetoothDeviceFactory|Found service UUID: 0000180f-0000-1000-8000-00805f9b34fb (F520A-LT)
2017-08-31 14:17:53.1794|DEBUG|UWPBluetoothDeviceFactory|Found service UUID: 0000ffe0-0000-1000-8000-00805f9b34fb (F520A-LT)
         */

        public Guid[] Services { get; } = { new Guid("78667579-7b48-43db-b8c5-7928a6b0a335") };

        public string[] Names { get; } =
        {
            "F520A-LT",
        };

        public Guid[] Characteristics { get; } =
        {
            // tx characteristic
            new Guid("78667579-a914-49a4-8333-aa3c0cd8fedc"),
        };

        public IButtplugDevice CreateDevice(IButtplugLogManager aLogManager,
            IBluetoothDeviceInterface aInterface)
        {
            return new LetenMotion(aLogManager, aInterface, this);
        }
    }

    internal class LetenMotion : ButtplugBluetoothDevice
    {
        private static Dictionary<string, string> FriendlyNames = new Dictionary<string, string>()
        {
            { "F520A-LT", "Nico" },
        };

        public LetenMotion(IButtplugLogManager aLogManager,
                           IBluetoothDeviceInterface aInterface,
                           IBluetoothDeviceInfo aInfo)
            : base(aLogManager,
                   $"Leten Device ({FriendlyNames[aInterface.Name]})",
                   aInterface,
                   aInfo)
        {
            MsgFuncs.Add(typeof(SingleMotorVibrateCmd), HandleSingleMotorVibrateCmd);
            MsgFuncs.Add(typeof(StopDeviceCmd), HandleStopDeviceCmd);
        }

        private async Task<ButtplugMessage> HandleStopDeviceCmd(ButtplugDeviceMessage aMsg)
        {
            return await HandleSingleMotorVibrateCmd(new SingleMotorVibrateCmd(aMsg.DeviceIndex, 0, aMsg.Id));
        }

        private async Task<ButtplugMessage> HandleSingleMotorVibrateCmd(ButtplugDeviceMessage aMsg)
        {
            var cmdMsg = aMsg as SingleMotorVibrateCmd;
            if (cmdMsg is null)
            {
                return BpLogger.LogErrorMsg(aMsg.Id, Error.ErrorClass.ERROR_DEVICE, "Wrong Handler");
            }

            var data = new byte[] { 0x0b, 0xff, 0x04, 0x0a, 0x32, 0x32, 0x00, 0x04, 0x08, 0x00, 0x64, 0x00 };
            data[9] = Convert.ToByte(cmdMsg.Speed * byte.MaxValue);

            // While there are 3 lovense revs right now, all of the characteristic arrays are the same.
            return await Interface.WriteValue(aMsg.Id,
                Info.Characteristics[(uint)MagicMotionBluetoothInfo.Chrs.Tx],
                data);
        }
    }
}
