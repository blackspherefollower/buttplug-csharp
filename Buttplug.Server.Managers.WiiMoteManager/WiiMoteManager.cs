using Buttplug.Core;
using WiimoteLib;

namespace Buttplug.Server.Managers.XInputGamepadManager
{
    public class WiiMoteManager : DeviceSubtypeManager
    {
        private WiimoteCollection _collection;

        public WiiMoteManager(IButtplugLogManager aLogManager)
            : base(aLogManager)
        {
            BpLogger.Info("Loading WiiMote Manager");
            _collection = new WiimoteCollection();
        }

        public override void StartScanning()
        {
            BpLogger.Info("WiiMoteManager start scanning");
            _collection.FindAllWiimotes();

            foreach (var c in _collection)
            {
                BpLogger.Debug($"Found connected WiiMote for Index {c.ID}");
                c.Connect();
                var device = new WiiMoteDevice(LogManager, c);
                InvokeDeviceAdded(new DeviceAddedEventArgs(device));
                InvokeScanningFinished();
            }
        }

        public override void StopScanning()
        {
            // noop
            BpLogger.Info("WiiMoteManager stop scanning");
        }

        public override bool IsScanning()
        {
            // noop
            return false;
        }
    }
}