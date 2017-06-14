﻿using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Foundation;
using Buttplug.Messages;
using Buttplug.Core;
using Buttplug.Bluetooth;
using JetBrains.Annotations;
using static Buttplug.Messages.Error;

namespace ButtplugUWPBluetoothManager.Core
{
    internal class UWPBluetoothDeviceInterface : IBluetoothDeviceInterface
    {
        public string Name => _bleDevice.Name;
        [NotNull]
        private readonly BluetoothLEDevice _bleDevice;
        [NotNull]
        private readonly GattCharacteristic[] _gattCharacteristics;
        [CanBeNull]
        private IAsyncOperation<GattCommunicationStatus> _currentTask;
        [NotNull]
        private readonly IButtplugLog _bpLogger;
        [CanBeNull]
        public event EventHandler DeviceRemoved;

        public UWPBluetoothDeviceInterface(
            [NotNull] IButtplugLogManager aLogManager,
            [NotNull] BluetoothLEDevice aDevice,
            [NotNull] GattCharacteristic[] aCharacteristics)
        {
            _bpLogger = aLogManager.GetLogger(GetType());
            _bleDevice = aDevice;
            _gattCharacteristics = aCharacteristics;
            _bleDevice.ConnectionStatusChanged += ConnectionStatusChangedHandler;
        }

        private void ConnectionStatusChangedHandler([NotNull] BluetoothLEDevice aDevice, [NotNull] object aObj)
        {
            if (_bleDevice.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                DeviceRemoved?.Invoke(this, new EventArgs());
            }
        }

        public ulong GetAddress()
        {
            return _bleDevice.BluetoothAddress;
        }

        [ItemNotNull]
        public async Task<ButtplugMessage> WriteValue(uint aMsgId, 
            uint aCharacteristicIndex,
            byte[] aValue,
            bool aWriteOption = false)
        {
            if (!(_currentTask is null))
            {
                _currentTask.Cancel();
                _bpLogger.Error("Cancelling device transfer in progress for new transfer.");
            }
            var gattCharacteristic = aCharacteristicIndex < 0 || aCharacteristicIndex >= _gattCharacteristics.Length ? null : _gattCharacteristics[aCharacteristicIndex];
            if (gattCharacteristic == null)
            {
                return _bpLogger.LogErrorMsg(aMsgId, ErrorClass.ERROR_DEVICE, $"Requested character {aCharacteristicIndex} out of range");
            }
            _currentTask = gattCharacteristic.WriteValueAsync(aValue.AsBuffer(), aWriteOption ? GattWriteOption.WriteWithResponse : GattWriteOption.WriteWithoutResponse);
            try
            {
                var status = await _currentTask;
                _currentTask = null;
                if (status != GattCommunicationStatus.Success)
                {
                    return _bpLogger.LogErrorMsg(aMsgId, ErrorClass.ERROR_DEVICE, $"GattCommunication Error: {status}");
                }
            }
            catch (InvalidOperationException e)
            {
                // This exception will be thrown if the bluetooth device disconnects in the middle of a transfer.
                return _bpLogger.LogErrorMsg(aMsgId, ErrorClass.ERROR_DEVICE, $"GattCommunication Error: {e.Message}");
            }
            return new Ok(aMsgId);
        }
    }
}
