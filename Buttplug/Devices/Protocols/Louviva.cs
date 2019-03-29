// <copyright file="Picobong.cs" company="Nonpolynomial Labs LLC">
// Buttplug C# Source Code File - Visit https://buttplug.io for more info about the project.
// Copyright (c) Nonpolynomial Labs LLC. All rights reserved.
// Licensed under the BSD 3-Clause license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using Buttplug.Core.Logging;
using Buttplug.Core.Messages;

namespace Buttplug.Devices.Protocols
{
 /*   internal class LouvivaBluetoothInfo : IBluetoothDeviceInfo
    {
        public enum Chrs : uint
        {
            Cmd = 0,
            Tx,
        }

        public Guid[] Services { get; } = { new Guid("0000aa70-0000-1000-8000-00805f9b34fb") };

        public string[] NamePrefixes { get; } = { };

        public string[] Names { get; } =
        {
            "Belle",
        };

        // WeVibe causes the characteristic detector to misidentify characteristics. Do not remove these.
        public Dictionary<uint, Guid> Characteristics { get; } = new Dictionary<uint, Guid>()
        {
            // cmd characteristic
            { (uint)Chrs.Cmd, new Guid("0000aa71-0000-1000-8000-00805f9b34fb") },

            // tx characteristic
            { (uint)Chrs.Tx, new Guid("0000aa72-0000-1000-8000-00805f9b34fb") },
        };

        public IButtplugDevice CreateDevice(IButtplugLogManager aLogManager,
            IBluetoothDeviceInterface aInterface)
        {
            return new Louviva(aLogManager, aInterface, this);
        }
    }*/

    internal class LouvivaProtocol : ButtplugDeviceProtocol
    {
        private double _vibratorSpeed = 0;

        public LouvivaProtocol(IButtplugLogManager aLogManager,
            IButtplugDeviceImpl aInterface)
            : base(aLogManager,
                $"Louviva {aInterface.Name}",
                aInterface)
        {
            AddMessageHandler<SingleMotorVibrateCmd>(HandleSingleMotorVibrateCmd);
            AddMessageHandler<VibrateCmd>(HandleVibrateCmd, new MessageAttributes() { FeatureCount = 1 });
            AddMessageHandler<StopDeviceCmd>(HandleStopDeviceCmd);
        }

        public override async Task InitializeAsync(CancellationToken aToken)
        {
            await Interface.WriteValueAsync(new byte[] { 0x09, 0xb7 }, new ButtplugDeviceWriteOptions { Endpoint = Endpoints.TxMode }, aToken).ConfigureAwait(false);
        }

        private async Task<ButtplugMessage> HandleStopDeviceCmd(ButtplugDeviceMessage aMsg, CancellationToken aToken)
        {
            return await HandleSingleMotorVibrateCmd(new SingleMotorVibrateCmd(aMsg.DeviceIndex, 0, aMsg.Id), aToken).ConfigureAwait(false);
        }

        private async Task<ButtplugMessage> HandleSingleMotorVibrateCmd(ButtplugDeviceMessage aMsg, CancellationToken aToken)
        {
            var cmdMsg = CheckMessageHandler<SingleMotorVibrateCmd>(aMsg);

            return await HandleVibrateCmd(VibrateCmd.Create(cmdMsg.DeviceIndex, cmdMsg.Id, cmdMsg.Speed, 1), aToken).ConfigureAwait(false);
        }

        private async Task<ButtplugMessage> HandleVibrateCmd(ButtplugDeviceMessage aMsg, CancellationToken aToken)
        {
            var cmdMsg = CheckGenericMessageHandler<VibrateCmd>(aMsg, 1);

            var changed = false;
            foreach (var v in cmdMsg.Speeds)
            {
                if (!(Math.Abs(v.Speed - _vibratorSpeed) > 0.001))
                {
                    continue;
                }

                changed = true;
                _vibratorSpeed = v.Speed;
            }

            if (!changed)
            {
                return new Ok(cmdMsg.Id);
            }

            var speedInt = Convert.ToUInt16(_vibratorSpeed * 256);

            await Interface.WriteValueAsync(new byte[] { Convert.ToByte(speedInt) }, new ButtplugDeviceWriteOptions { Endpoint = Endpoints.Tx }, aToken).ConfigureAwait(false);
            return new Ok(aMsg.Id);
        }
    }
}