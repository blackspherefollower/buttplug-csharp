using System;
using System.Threading.Tasks;
using Buttplug.Core;

namespace Buttplug.Server.Bluetooth
{
    public interface IBluetoothDeviceInterface
    {
        string Name { get; }

        Task<ButtplugMessage> WriteValue(uint aMsgId, Guid aCharacteristicIndex, byte[] aValue, bool aWriteWithResponse = false);

        Task<BluetoothResultWrapper> ReadValue(uint aMsgId, Guid aCharacteristic);

        Task<ButtplugMessage> SubscribeValue(uint aMsgId, Guid aCharacteristic);

        ulong GetAddress();

        event EventHandler DeviceRemoved;

        event EventHandler<BluetoothMessageReceivedEventArgs> BluetoothMessageReceived;

        void Disconnect();
    }
}
