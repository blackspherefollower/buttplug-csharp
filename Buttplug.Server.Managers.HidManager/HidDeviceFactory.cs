using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Buttplug.Core;
using Buttplug.Core.Messages;
using HidLibrary;

namespace Buttplug.Server.Managers.HidManager
{
    internal class HidDeviceFactory
    {
        private readonly IButtplugLog _bpLogger;
        
        private readonly IHidDeviceInfo _deviceInfo;
        
        private readonly IButtplugLogManager _buttplugLogManager;

        public HidDeviceFactory(IButtplugLogManager aLogManager, IHidDeviceInfo aInfo)
        {
            _buttplugLogManager = aLogManager;
            _bpLogger = _buttplugLogManager.GetLogger(GetType());
            _bpLogger.Trace($"Creating {GetType().Name}");
            _deviceInfo = aInfo;
        }

        public bool MayBeDevice(int aVendorId, int aProductId)
        {
            if(_deviceInfo.VendorId == aVendorId && _deviceInfo.ProductId == aProductId)
            {
                _bpLogger.Debug("Matched " + _deviceInfo.Name);
                return true;
            }

            return false;
        }

        internal IButtplugDevice CreateDevice(IHidDevice aHid)
        {
            return _deviceInfo.CreateDevice(_buttplugLogManager, aHid);
        }
    }
}
