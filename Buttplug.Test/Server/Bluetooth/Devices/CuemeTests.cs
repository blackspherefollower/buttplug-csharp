﻿// <copyright file="MysteryVibeTests.cs" company="Nonpolynomial Labs LLC">
// Buttplug C# Source Code File - Visit https://buttplug.io for more info about the project.
// Copyright (c) Nonpolynomial Labs LLC. All rights reserved.
// Licensed under the BSD 3-Clause license. See LICENSE file in the project root for full license information.
// </copyright>

// Test file, disable ConfigureAwait checking.
// ReSharper disable ConsiderUsingConfigureAwait

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Buttplug.Core.Messages;
using Buttplug.Server.Bluetooth.Devices;
using Buttplug.Server.Test.Util;
using JetBrains.Annotations;
using NUnit.Framework;

namespace Buttplug.Test.Server.Bluetooth.Devices
{
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "Test classes can skip documentation requirements")]
    [TestFixture]
    public class CuemeTests
    {
        [NotNull]
        private readonly BluetoothDeviceTestUtils<CuemeBluetoothInfo> _testUtil = new BluetoothDeviceTestUtils<CuemeBluetoothInfo>();

        [SetUp]
        public async Task Init()
        {
            await _testUtil.SetupTest("FUNCODE_");
        }

        [Test]
        public void TestAllowedMessages()
        {
            _testUtil.TestDeviceAllowedMessages(new Dictionary<System.Type, uint>()
            {
                { typeof(StopDeviceCmd), 0 },
                { typeof(SingleMotorVibrateCmd), 0 },
                { typeof(VibrateCmd), 4 },
            });
        }

        // StopDeviceCmd noop test handled in GeneralDeviceTests

        [Test]
        public async Task TestStopDeviceCmd()
        {
            var expected =
                new List<(byte[], uint)>()
                {
                    (new byte[] { 0x17 }, _testUtil.NoCharacteristic),
                };

            await _testUtil.TestDeviceMessage(new SingleMotorVibrateCmd(4, 0.5), expected, false);

            expected =
                new List<(byte[], uint)>()
                {
                    (new byte[] { 0x00 }, _testUtil.NoCharacteristic),
                };

            await _testUtil.TestDeviceMessageOnWrite(new StopDeviceCmd(4), expected, false);
        }

        [Test]
        public async Task TestSingleMotorVibrateCmd()
        {
            var expected =
                new List<(byte[], uint)>()
                {
                    (new byte[] { 0x17 }, _testUtil.NoCharacteristic),
                };

            await _testUtil.TestDeviceMessage(new SingleMotorVibrateCmd(4, 0.5), expected, false);
        }

        [Test]
        public async Task TestVibrateCmd()
        {
            var expected =
                new List<(byte[], uint)>()
                {
                    (new byte[] { 0x17 }, _testUtil.NoCharacteristic),
                };

            await _testUtil.TestDeviceMessage(VibrateCmd.Create(4, 1, 0.5, 4), expected, false);
        }

        [Test]
        public void TestInvalidVibrateCmd()
        {
            _testUtil.TestInvalidVibrateCmd(6);
        }
    }
}