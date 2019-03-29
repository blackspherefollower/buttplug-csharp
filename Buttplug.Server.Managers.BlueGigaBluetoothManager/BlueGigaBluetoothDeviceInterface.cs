// <copyright file="UWPBluetoothDeviceInterface.cs" company="Nonpolynomial Labs LLC">
//     Buttplug C# Source Code File - Visit https://buttplug.io for more info about the project.
//     Copyright (c) Nonpolynomial Labs LLC. All rights reserved. Licensed under the BSD 3-Clause
//     license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Buttplug.Core;
using Buttplug.Core.Logging;
using Buttplug.Core.Messages;
using Buttplug.Devices;
using Buttplug.Devices.Configuration;
using JetBrains.Annotations;

namespace Buttplug.Server.Managers.BlueGigaBluetoothManager
{
    internal class BlueGigaBluetoothDeviceInterface : ButtplugDeviceImpl
    {
        [NotNull]
        private readonly Dictionary<string, Guid> _indexedChars = new Dictionary<string, Guid>();

        [NotNull]
        private CancellationTokenSource _internalTokenSource = new CancellationTokenSource();

        [CanBeNull]
        private CancellationTokenSource _currentWriteTokenSource;
        
        [CanBeNull]
        private BlueGigaBluetoothManager.BlueGigaDevice _bleDevice;

        public override bool Connected => _bleDevice != null && _bleDevice.Advertisment.ConnectionHandle != 0;

        public override Task WriteValueAsyncInternal(byte[] aValue, ButtplugDeviceWriteOptions aOptions,
            CancellationToken aToken = default(CancellationToken))
        {
           // _bleDevice.Container.Send(aValue, _bleDevice.Advertisment.ConnectionHandle, aOptions.Endpoint);
           return Task.FromResult<object>(null);
        }

        public override Task<byte[]> ReadValueAsyncInternal(ButtplugDeviceReadOptions aOptions, CancellationToken aToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        protected BlueGigaBluetoothDeviceInterface(
            [NotNull] IButtplugLogManager aLogManager,
            [NotNull] string aName,
            [NotNull] BlueGigaBluetoothManager.BlueGigaDevice aDevice)
        : base(aLogManager)
        {
            _bleDevice = aDevice;
            Name = aName;
            Address = _bleDevice.Advertisment.Address;
            //_bleDevice.ConnectionStatusChanged += ConnectionStatusChangedHandler;
        }
        
        public static async Task<IButtplugDeviceImpl> Create(IButtplugLogManager aLogManager,
            BluetoothLEProtocolConfiguration aConfig,
            [NotNull] string aName,
            BlueGigaBluetoothManager.BlueGigaDevice aDevice)
        {
            var device = new BlueGigaBluetoothDeviceInterface(aLogManager, aName, aDevice);
            //await device.InitializeDevice(aConfig).ConfigureAwait(false);
            return device;
        }
/*
        protected async Task<Guid> GetService(Guid aServiceGuid)
        {
            // GetGattServicesForUuidAsync is 15063+ only?
            var serviceResult = await _bleDevice.GetGattServicesForUuidAsync(aServiceGuid, BluetoothCacheMode.Cached);

            // Don't log exceptions here, as we may not want to report them at some points.
            if (serviceResult.Status != GattCommunicationStatus.Success)
            {
                throw new ButtplugDeviceException($"Cannot check for service {aServiceGuid} of {_bleDevice.Name}.");
            }

            if (serviceResult.Services.Count == 0)
            {
                throw new ButtplugDeviceException($"Cannot find service {aServiceGuid} of {_bleDevice.Name}.");
            }

            // TODO is there EVER a way we'd get more than one service back?
            return serviceResult.Services[0];
        }*/

        protected async Task InitializeDevice(BluetoothLEProtocolConfiguration aConfig)
        {
            foreach (var serviceInfo in aConfig.Services)
            {
                // If we don't have any characteristic configuration, assume we're using
                // characteristic detection.
                if (serviceInfo.Value == null || serviceInfo.Value.Count == 0)
                {
                    await AddDefaultCharacteristics(serviceInfo.Key).ConfigureAwait(false);
                }
                else
                {
                    var serviceGuid = serviceInfo.Key;

                   /* GattDeviceService service;

                    try
                    {
                        service = await GetService(serviceGuid).ConfigureAwait(false);
                    }
                    catch (ButtplugDeviceException)
                    {
                        // In this case, we may have a whole bunch of services that aren't valid for
                        // a device and only one that is. We can ignore the exception here, and throw
                        // later if we don't get anything from any service in the list.
                        continue;
                    }

                    var chrResult = await service.GetCharacteristicsAsync();
                    if (chrResult.Status != GattCommunicationStatus.Success)
                    {
                        throw new ButtplugDeviceException(BpLogger,
                            $"Cannot connect to characteristics for {serviceGuid} of {_bleDevice.Name}.");
                    }

                    foreach (var chr in chrResult.Characteristics)
                    {
                        foreach (var indexChr in serviceInfo.Value)
                        {
                            if (chr.Uuid != indexChr.Value)
                            {
                                continue;
                            }

                            if (_indexedChars.ContainsKey(indexChr.Key))
                            {
                                // We've somehow doubled up endpoint names. Freak out.
                                throw new ButtplugDeviceException(BpLogger, $"Found repeated endpoint name {indexChr.Key} on {Name}");
                            }

                            BpLogger.Debug($"Found characteristic {indexChr.Key} {chr.Uuid} ({_bleDevice.Name})");
                            _indexedChars.Add(indexChr.Key, chr);
                        }
                    }*/
                }
            }

            // If we've exited characteristic finding without any characteristics to use, something
            // is wrong with our configuration and we won't be able to talk to the device. Don't
            // continue connection.
            if (!_indexedChars.Any())
            {
                throw new ButtplugDeviceException(BpLogger, $"No characteristics to connect to for device {Name}");
            }
        }

        private async Task AddDefaultCharacteristics(Guid aServiceGuid)
        {
            /*GattDeviceService service;
            try
            {
                service = await GetService(aServiceGuid).ConfigureAwait(false);
            }
            catch (ButtplugDeviceException)
            {
                // In this case, we may have a whole bunch of services that aren't valid for a device
                // and only one that is. We can ignore the exception here, and throw later if we
                // don't get anything from any service in the list.
                return;
            }

            // In the case we have multiple services that exist on a device, and no characteristics
            // defined for them, throw, because otherwise we'll end up assigning colliding endpoints.
            if (_indexedChars.ContainsKey(Endpoints.Rx) || _indexedChars.ContainsKey(Endpoints.Tx))
            {
                throw new ButtplugDeviceException(BpLogger,
                    $"Default characteristics already assigned for {_bleDevice.Name}.");
            }

            var chrResult = await service.GetCharacteristicsAsync();
            if (chrResult.Status != GattCommunicationStatus.Success)
            {
                throw new ButtplugDeviceException(BpLogger,
                    $"Cannot connect to characteristics for {service.Uuid} of {_bleDevice.Name}.");
            }

            var chrs = chrResult.Characteristics;

            foreach (var c in chrs)
            {
                if (c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Read) ||
                    c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify) ||
                    c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate))
                {
                    if (_indexedChars.ContainsKey(Endpoints.Rx))
                    {
                        throw new ButtplugDeviceException(BpLogger,
                            $"Too many possible rx characteristics on service {service.Uuid} of {_bleDevice.Name}.");
                    }

                    _indexedChars[Endpoints.Rx] = c;
                }
                else if (c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse) ||
                         c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Write))
                {
                    if (_indexedChars.ContainsKey(Endpoints.Tx))
                    {
                        throw new ButtplugDeviceException(BpLogger,
                            $"Too many possible tx characteristics on service {service.Uuid} of {_bleDevice.Name}.");
                    }

                    _indexedChars[Endpoints.Tx] = c;
                }
            }*/
        }

        /*
        public override async Task SubscribeToUpdatesAsyncInternal(ButtplugDeviceReadOptions aOptions)
        {
            if (!_indexedChars.ContainsKey(aOptions.Endpoint))
            {
                throw new ButtplugDeviceException(BpLogger, $"Endpoint {aOptions.Endpoint} does not exist on device {Name}.");
            }
            await SubscribeToUpdatesAsync(_indexedChars[aOptions.Endpoint]).ConfigureAwait(false);
        }

        private async Task SubscribeToUpdatesAsync(GattCharacteristic aCharacteristic)
        {
            ButtplugUtils.ArgumentNotNull(aCharacteristic, nameof(aCharacteristic));

            if (!aCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify) &&
                !aCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate))
            {
                throw new ButtplugDeviceException(BpLogger, $"Cannot subscribe to BLE updates from {Name}: No Notify characteristic found.");
            }

            if (aCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))
            {
                var status = await aCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.Notify);
                if (status != GattCommunicationStatus.Success)
                {
                    throw new ButtplugDeviceException(BpLogger, $"Cannot subscribe to BLE notify updates from {Name}: Failed Request {status}");
                }
            }

            if (aCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate))
            {
                var status = await aCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.Indicate);
                if (status != GattCommunicationStatus.Success)
                {
                    throw new ButtplugDeviceException(BpLogger, $"Cannot subscribe to BLE indicate updates from {Name}: Failed Request {status}");
                }
            }

            // Server has been informed of clients interest.
            aCharacteristic.ValueChanged += BluetoothNotifyReceivedHandler;
        }

        private void ConnectionStatusChangedHandler([NotNull] BluetoothLEDevice aDevice, [NotNull] object aObj)
        {
            if (_bleDevice?.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                InvokeDeviceRemoved();
            }
        }

        private void BluetoothNotifyReceivedHandler(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            CryptographicBuffer.CopyToByteArray(args.CharacteristicValue, out var bytes);
            InvokeDataReceived(new ButtplugDeviceDataEventArgs("rx", bytes));
        }

        [ItemNotNull]
        public override async Task WriteValueAsyncInternal(byte[] aValue,
            ButtplugDeviceWriteOptions aOptions,
            CancellationToken aToken = default(CancellationToken))
        {
            if (!_indexedChars.ContainsKey(aOptions.Endpoint))
            {
                throw new ButtplugDeviceException(BpLogger,
                    $"WriteValue using indexed characteristics called with invalid index {aOptions.Endpoint} on {Name}");
            }

            await WriteValueAsync(_indexedChars[aOptions.Endpoint], aValue, aOptions.WriteWithResponse, aToken).ConfigureAwait(false);
        }

        private async Task WriteValueAsync(GattCharacteristic aChar,
            byte[] aValue,
            bool aWriteWithResponse,
            CancellationToken aToken)
        {
            if (!(_currentWriteTokenSource is null))
            {
                _internalTokenSource.Cancel();
                BpLogger.Error("Cancelling device transfer in progress for new transfer.");
            }

            try
            {
                _internalTokenSource = new CancellationTokenSource();
                _currentWriteTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_internalTokenSource.Token, aToken);
                var writeTask = aChar.WriteValueAsync(aValue.AsBuffer(),
                    aWriteWithResponse ? GattWriteOption.WriteWithResponse : GattWriteOption.WriteWithoutResponse).AsTask(_currentWriteTokenSource.Token);
                var status = await writeTask.ConfigureAwait(false);
                _currentWriteTokenSource = null;
                if (status != GattCommunicationStatus.Success)
                {
                    throw new ButtplugDeviceException(BpLogger,
                        $"GattCommunication Error: {status}");
                }
            }
            catch (InvalidOperationException e)
            {
                // This exception will be thrown if the bluetooth device disconnects in the middle of
                // a transfer.
                throw new ButtplugDeviceException(BpLogger,
                    $"GattCommunication Error: {e.Message}");
            }
            catch (TaskCanceledException e)
            {
                // This exception will be thrown if the bluetooth device disconnects in the middle of
                // a transfer (happened when MysteryVibe lost power).
                throw new ButtplugDeviceException(BpLogger,
                    $"Device disconnected: {e.Message}");
            }
        }

        public override async Task<byte[]> ReadValueAsyncInternal(ButtplugDeviceReadOptions aOptions,
            CancellationToken aToken = default(CancellationToken))
        {
            if (!_indexedChars.ContainsKey(aOptions.Endpoint))
            {
                throw new ButtplugDeviceException(BpLogger,
                    "ReadValue using indexed characteristics called with invalid index");
            }

            return await ReadValueAsync(_indexedChars[aOptions.Endpoint], aToken).ConfigureAwait(false);
        }

        private async Task<byte[]> ReadValueAsync(GattCharacteristic aChar, CancellationToken aToken)
        {
            var result = await aChar.ReadValueAsync().AsTask(aToken).ConfigureAwait(false);
            if (result.Status != GattCommunicationStatus.Success)
            {
                throw new ButtplugDeviceException(BpLogger, $"Error while reading from {Name}");
            }

            return result.Value.ToArray();
        }
        */

        public override Task SubscribeToUpdatesAsyncInternal(ButtplugDeviceReadOptions aOptions)
        {
            throw new NotImplementedException();
        }

        public override void Disconnect()
        {
            InvokeDeviceRemoved();
            _indexedChars.Clear();

            //_bleDevice.Dispose();
            _bleDevice = null;
        }
    }
}
