using System;
using System.Collections.Generic;

namespace Buttplug.Server.Bluetooth
{
    public class BluetoothGattHolder
    {
        public class GattUuid
        {
            public Guid Uuid;
            public ushort Handle;
        }

        public enum GattCharProps : uint
        {
            None = 0,
            Broadcast = 1,
            Read = 2,
            WriteWithoutResponse = 4,
            Write = 8,
            Notify = 16,
            Indicate = 32,
            AuthenticatedSignedWrites = 64,
            ExtendedProperties = 128,
            ReliableWrites = 256,
            WritableAuxiliaries = 512,
        }

        public string DeviceName;

        public Dictionary<GattUuid, Dictionary<GattUuid, uint>> Services = new Dictionary<GattUuid, Dictionary<GattUuid, uint>>();
    }
}
