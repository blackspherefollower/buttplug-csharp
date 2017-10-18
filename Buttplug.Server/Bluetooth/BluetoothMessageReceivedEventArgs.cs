using System;

namespace Buttplug.Server.Bluetooth
{
    public class BluetoothMessageReceivedEventArgs : EventArgs
    {
        public byte[] Data;

        public DateTime Timestamp;

        public BluetoothMessageReceivedEventArgs(byte[] aData, DateTime aTimestamp)
        {
            Data = aData;
            Timestamp = aTimestamp;
        }
    }
}