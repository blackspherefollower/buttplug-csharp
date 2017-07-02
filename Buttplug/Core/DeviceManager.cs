﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buttplug.Messages;
using static Buttplug.Messages.Error;

namespace Buttplug.Core
{
    internal class DeviceManager
    {
        private readonly List<IDeviceSubtypeManager> _managers;
        internal Dictionary<uint, IButtplugDevice> _devices { get; }
        public Error.ErrorClass ERROR_DEVICE { get; private set; }

        private long _deviceIndexCounter;
        private readonly IButtplugLog _bpLogger;
        private readonly IButtplugLogManager _bpLogManager;
        private bool _sentFinished;
        public event EventHandler<MessageReceivedEventArgs> DeviceMessageReceived;
        public event EventHandler<EventArgs> ScanningFinished;

        public DeviceManager(IButtplugLogManager aLogManager)
        {
            _bpLogManager = aLogManager;
            _bpLogger = _bpLogManager.GetLogger(GetType());
            _bpLogger.Trace("Setting up DeviceManager");
            _sentFinished = true;
            _devices = new Dictionary<uint, IButtplugDevice>();
            _deviceIndexCounter = 0;

            _managers = new List<IDeviceSubtypeManager>();
        }
        
        protected IEnumerable<string> GetAllowedMessageTypesAsStrings(IButtplugDevice aDevice)
        {
            return from x in aDevice.GetAllowedMessageTypes() select x.Name;
        }

        private void DeviceAddedHandler(object o, DeviceAddedEventArgs e)
        {
            // If we get to 4 billion devices connected, this may be a problem.
            var deviceIndex = (uint)Interlocked.Increment(ref _deviceIndexCounter);
            // Devices can be turned off by the time they get to this point, at which point they end up null. Make sure the device isn't null.
            if (e.Device == null)
            {
                return;
            }
            var duplicates = from x in _devices.Values
                where x.Identifier == e.Device.Identifier
                select x;
            if (duplicates.Any())
            {
                _bpLogger.Trace($"Already have device {e.Device.Name} in Devices list");
                return;
            }
            _bpLogger.Debug($"Adding Device {e.Device.Name} at index {deviceIndex}");
            _devices.Add(deviceIndex, e.Device);
            e.Device.DeviceRemoved += DeviceRemovedHandler;
            var msg = new DeviceAdded(deviceIndex, e.Device.Name, GetAllowedMessageTypesAsStrings(e.Device).ToArray());
            
            DeviceMessageReceived?.Invoke(this, new MessageReceivedEventArgs(msg));
        }

        private void DeviceRemovedHandler(object o, EventArgs e)
        {
            if ((o as ButtplugDevice) == null)
            {
                _bpLogger.Error("Got DeviceRemoved message from an object that is not a ButtplugDevice.");
                return;
            }
            var device = (ButtplugDevice) o;
            // The device itself will fire the remove event, so look it up in the dictionary and translate that for clients.
            var entry = (from x in _devices where x.Value.Identifier == device.Identifier select x).ToList();
            if (!entry.Any())
            {
                _bpLogger.Error("Got DeviceRemoved Event from object that is not in devices dictionary");
            }
            if (entry.Count() > 1)
            {
                _bpLogger.Error("Device being removed has multiple entries in device dictionary.");
            }
            foreach (var pair in entry.ToList())
            {
                pair.Value.DeviceRemoved -= DeviceRemovedHandler;
                _bpLogger.Info($"Device removed: {pair.Key} - {pair.Value.Name}");
                _devices.Remove(pair.Key);
                DeviceMessageReceived?.Invoke(this, new MessageReceivedEventArgs(new DeviceRemoved(pair.Key)));
            }
        }
        
        private void ScanningFinishedHandler(object o, EventArgs e)
        {
            if (_sentFinished)
            {
                return;
            }

            var done = true;
            _managers.ForEach(m => done &= !m.IsScanning());
            if(done)
            {
                _sentFinished = true;
                ScanningFinished?.Invoke(this, new EventArgs());
            }
        }

        public async Task<ButtplugMessage> SendMessage(ButtplugMessage aMsg)
        {
            var id = aMsg.Id;
            switch (aMsg)
            {
                case StartScanning _:
                    StartScanning();
                    return new Ok(id);

                case StopScanning _:
                    StopScanning();
                    return new Ok(id);

                case StopAllDevices _:
                    var isOk = true;
                    var errorMsg = "";
                    foreach (var d in _devices.ToList())
                    {
                        var r = await d.Value.ParseMessage(new StopDeviceCmd(d.Key, aMsg.Id));
                        if (r is Ok)
                        {
                            continue;
                        }
                        isOk = false;
                        errorMsg += $"{(r as Error).ErrorMessage}; ";
                    }
                    if (isOk)
                    {
                        return new Ok(aMsg.Id);
                    }
                    return new Error(errorMsg, ERROR_DEVICE, aMsg.Id);
                case RequestDeviceList _:
                    var msgDevices = _devices
                        .Select(d => new DeviceMessageInfo(d.Key, d.Value.Name,
                            GetAllowedMessageTypesAsStrings(d.Value).ToArray())).ToList();
                    return new DeviceList(msgDevices.ToArray(), id);

                // If it's a device message, it's most likely not ours.
                case ButtplugDeviceMessage m:
                    _bpLogger.Trace($"Sending {aMsg.GetType().Name} to device index {m.DeviceIndex}");
                    if (_devices.ContainsKey(m.DeviceIndex))
                    {
                        return await _devices[m.DeviceIndex].ParseMessage(m);
                    }
                    return _bpLogger.LogErrorMsg(id, ErrorClass.ERROR_DEVICE, $"Dropping message for unknown device index {m.DeviceIndex}");
            }
            return _bpLogger.LogErrorMsg(id, ErrorClass.ERROR_MSG, $"Message type {aMsg.GetType().Name} unhandled by this server.");
        }

        private void StartScanning()
        {
            _sentFinished = false;
            _managers.ForEach(m => m.StartScanning());
        }

        private void StopScanning()
        {
            _managers.ForEach(m => m.StopScanning());
        }

        public void AddDeviceSubtypeManager<T>(Func<IButtplugLogManager,T> aCreateMgrFunc) where T : IDeviceSubtypeManager
        {
            AddDeviceSubtypeManager(aCreateMgrFunc(_bpLogManager));
        }

        internal void AddDeviceSubtypeManager(IDeviceSubtypeManager mgr)
        {
            _managers.Add(mgr);
            mgr.DeviceAdded += DeviceAddedHandler;
            mgr.ScanningFinished += ScanningFinishedHandler;
        }
    }
}
