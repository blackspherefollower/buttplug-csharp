using Buttplug.Core;

namespace Buttplug.Server.Bluetooth
{
    public class BluetoothResultWrapper
    {
        public ButtplugMessage Message;
        public byte[] Value;

        public BluetoothResultWrapper(ButtplugMessage aMessage, byte[] aValue = null)
        {
            Message = aMessage;
            Value = new byte[aValue?.Length ?? 0];
            aValue?.CopyTo(Value, 0);
        }
    }
}