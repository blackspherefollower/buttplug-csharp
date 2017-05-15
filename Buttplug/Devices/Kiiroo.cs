﻿using Buttplug.Core;
using Buttplug.Messages;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using LogLevel = NLog.LogLevel;

namespace Buttplug.Devices
{
    internal class KiirooBluetoothInfo : IBluetoothDeviceInfo
    {
        public string[] Names { get; } = { "ONYX", "PEARL" };
        public Guid[] Services { get; } = { new Guid("49535343-fe7d-4ae5-8fa9-9fafd205e455") };

        public Guid[] Characteristics { get; } =
        {
            // tx
            new Guid("49535343-8841-43f4-a8d4-ecbe34729bb3"),
            // rx
            new Guid("49535343-1e4d-4bd9-ba61-23c647249616")
        };

        public ButtplugBluetoothDevice CreateDevice(BluetoothLEDevice aDevice,
            Dictionary<Guid, GattCharacteristic> aCharacteristics)
        {
            return new Kiiroo(aDevice,
                aCharacteristics[Characteristics[0]],
                aCharacteristics[Characteristics[1]]);
        }
    }

    internal class Kiiroo : ButtplugBluetoothDevice
    {
        public Kiiroo(BluetoothLEDevice aDevice,
            GattCharacteristic aWriteChr,
            GattCharacteristic aReadChr) :
            base($"Kiiroo {aDevice.Name}",
                 aDevice,
                 aWriteChr,
                 aReadChr)
        {
            MsgFuncs.Add(typeof(KiirooRawCmd), HandleKiirooRawCmd);
        }

        public async Task<ButtplugMessage> HandleKiirooRawCmd(ButtplugDeviceMessage aMsg)
        {
            var cmdMsg = aMsg as KiirooRawCmd;
            if (cmdMsg is null)
            {
                return ButtplugUtils.LogAndError(aMsg.Id, BpLogger, LogLevel.Error, "Wrong Handler");
            }
            return await WriteToDevice(cmdMsg, ButtplugUtils.WriteString($"{cmdMsg.Position},\n"));
        }
    }
}