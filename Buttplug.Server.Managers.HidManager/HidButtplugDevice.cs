using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Buttplug.Core;
using HidLibrary;
using System.Threading;

namespace Buttplug.Server.Managers.HidManager.Devices
{
    internal abstract class HidButtplugDevice : ButtplugDevice, IDisposable
    {
        private IButtplugLogManager _logManager;
        private VstrokerHidDeviceInfo _deviceInfo;
        private IHidDevice _hid;
        private bool _disposed = false;
        private bool _reading = false;
        private Task _readerThread;
        private CancellationTokenSource _tokenSource;

        public HidButtplugDevice(IButtplugLogManager aLogManager, IHidDevice aHid, VstrokerHidDeviceInfo aDeviceInfo)
            : base(aLogManager, aDeviceInfo.Name, aHid.DevicePath)
        {
            _logManager = aLogManager;
            _hid = aHid;
            _deviceInfo = aDeviceInfo;

            _tokenSource = new CancellationTokenSource();
            _hid.Inserted += DeviceAttachedHandler;
            _hid.Removed += DeviceRemovedHandler;
        }

        public override void Disconnect()
        {
            _hid.CloseDevice();
            InvokeDeviceRemoved();
        }

        public void BeginRead()
        {
            if(_readerThread != null && _readerThread.Status == TaskStatus.Running)
            {
                return;
            }

            _readerThread = new Task(() => { ReportReader(_tokenSource.Token); }, _tokenSource.Token, TaskCreationOptions.LongRunning);
            _readerThread.Start();
        }

        public void EndRead()
        {
            _reading = false;
            _readerThread?.Wait();
            _readerThread = null;
            _hid.MonitorDeviceEvents = false;
            _hid.CloseDevice();
        }

        private void DeviceAttachedHandler()
        {
        }

        private void DeviceRemovedHandler()
        {
            _reading = false;
            InvokeDeviceRemoved();
        }

        private void ReportReader(CancellationToken aToken)
        {
            _hid.OpenDevice();
            _reading = true;
            _hid.MonitorDeviceEvents = true;
            _hid.ReadReport(OnReport);
        }

        private void OnReport(HidReport report)
        {
            if (!report.Exists || report.ReadStatus != HidDeviceData.ReadStatus.Success)
            {
                return;
            }

            if (HandleData(report.Data) && _reading)
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