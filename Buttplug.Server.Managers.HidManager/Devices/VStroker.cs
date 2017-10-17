using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Buttplug.Core;
using HidLibrary;

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
        }

        protected override bool HandleData(byte[] data)
        {
            var xor_byte = data[0];
            var axis = new int[] { 0, 0, 0 };
            for( int i = 0; i < 3; i++ )
            {
                var a = (((data[(i * 2) + 1] & 0xf) << 4) | (data[(i * 2) + 1] >> 4)) ^ xor_byte;
                var b = (((data[(i * 2) + 2] & 0xf) << 4) | (data[(i * 2) + 2] >> 4)) ^ xor_byte;
                axis[i] = (a | b << 8);
                if (axis[i] > (2 ^ 15))
                    axis[i] = axis[i] - (2 ^ 15);
            }

            Console.Out.WriteLine($"x:{axis[0]} y:{axis[1]} z:{axis[2]}");
            return true;
        }
    }
}
