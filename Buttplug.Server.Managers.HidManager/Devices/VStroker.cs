using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Buttplug.Core;
using HidLibrary;
using Buttplug.Core.Messages;

namespace Buttplug.Server.Managers.HidManager.Devices
{
    class VstrokerHidDeviceInfo : IHidDeviceInfo
    {
        public string Name { get; } = "Vstroker";

        public int VendorId { get; } = 0x0451;

        public int ProductId { get; } = 0x55A5;

        public IButtplugDevice CreateDevice(IButtplugLogManager aLogManager, IHidDevice aHid)
        {
            return new Vstroker(aLogManager, aHid, this);
        }
    }

    class Vstroker : HidButtplugDevice
    {
        public Vstroker(IButtplugLogManager aLogManager, IHidDevice aHid, VstrokerHidDeviceInfo aDeviceInfo) : base(aLogManager, aHid, aDeviceInfo)
        {
            MsgFuncs.Add(typeof(StartAccelerometerCmd), new ButtplugDeviceWrapper(HandleStartAccelerometerCmd));
            MsgFuncs.Add(typeof(StopAccelerometerCmd), new ButtplugDeviceWrapper(HandleStopAccelerometerCmd));
            MsgFuncs.Add(typeof(StopDeviceCmd), new ButtplugDeviceWrapper(HandleStopAccelerometerCmd));
        }

        protected override bool HandleData(byte[] data)
        {
            var xor_byte = data[0];
            var axis = new int[] { 0, 0, 0 };
            for( int i = 0; i < 3; i++ )
            {
                var a = (((data[(i * 2) + 1] & 0xf) << 4) | (data[(i * 2) + 1] >> 4)) ^ xor_byte;
                var b = (((data[(i * 2) + 2] & 0xf) << 4) | (data[(i * 2) + 2] >> 4)) ^ xor_byte;
                axis[i] = (short)(a | b << 8);
            }

            Console.Out.WriteLine($"x:{axis[0]} y:{axis[1]} z:{axis[2]}");
            return true;
        }

        private async Task<ButtplugMessage> HandleStartAccelerometerCmd(ButtplugDeviceMessage aMsg)
        {
            var cmdMsg = aMsg as StartAccelerometerCmd;
            if (cmdMsg is null)
            {
                return BpLogger.LogErrorMsg(aMsg.Id, Error.ErrorClass.ERROR_DEVICE, "Wrong Handler");
            }
            
            BeginRead();
            return new Ok(cmdMsg.Id);
        }

        private async Task<ButtplugMessage> HandleStopAccelerometerCmd(ButtplugDeviceMessage aMsg)
        {
            var cmdMsg1 = aMsg as StopAccelerometerCmd;
            var cmdMsg2 = aMsg as StopDeviceCmd;
            if (cmdMsg1 is null && cmdMsg2 is null)
            {
                return BpLogger.LogErrorMsg(aMsg.Id, Error.ErrorClass.ERROR_DEVICE, "Wrong Handler");
            }

            EndRead();
            return new Ok(aMsg.Id);
        }
    }
}
