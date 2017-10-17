using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Buttplug.Core;
using HidLibrary;

namespace Buttplug.Server.Managers.HidManager.Devices
{
    internal abstract class HidButtplugDevice : ButtplugDevice, IDisposable
    {
        private IButtplugLogManager _logManager;
        private VstrokerHidDeviceInfo _deviceInfo;
        private IHidDevice _hid;
        private bool _disposed = false;

        public HidButtplugDevice(IButtplugLogManager aLogManager, IHidDevice aHid, VstrokerHidDeviceInfo aDeviceInfo)
            : base(aLogManager, aDeviceInfo.Name, aHid.DevicePath)
        {
            _logManager = aLogManager;
            _hid = aHid;
            _deviceInfo = aDeviceInfo;

            _hid.OpenDevice();
            _hid.Inserted += DeviceAttachedHandler;
            _hid.Removed += DeviceRemovedHandler;
            BeginRead();
        }

        public override void Disconnect()
        {
            _hid.CloseDevice();
            InvokeDeviceRemoved();
        }

        public void BeginRead()
        {
            _hid.MonitorDeviceEvents = true;
            _hid.ReadReport(OnReport);
        }

        private void DeviceAttachedHandler()
        {
        }

        private void DeviceRemovedHandler()
        {
            InvokeDeviceRemoved();
        }

        private void OnReport(HidReport report)
        {
            if (!report.Exists || report.ReadStatus != HidDeviceData.ReadStatus.Success)
            {
                return;
            }

            if (HandleData(report.Data))
            {
                _hid.ReadReport(OnReport);
            }
        }

        protected abstract bool HandleData(byte[] data);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool aDisposing)
        {
            if (!_disposed)
            {
                if (aDisposing)
                {
                    _hid.CloseDevice();
                    InvokeDeviceRemoved();
                }

                _disposed = true;
            }
        }
    }
}