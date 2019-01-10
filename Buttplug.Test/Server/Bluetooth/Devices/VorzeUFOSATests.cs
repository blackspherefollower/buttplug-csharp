﻿// <copyright file="VorzeCycloneSATests.cs" company="Nonpolynomial Labs LLC">
//     Buttplug C# Source Code File - Visit https://buttplug.io for more info about the project.
//     Copyright (c) Nonpolynomial Labs LLC. All rights reserved. Licensed under the BSD 3-Clause
//     license. See LICENSE file in the project root for full license information.
// </copyright>

// Test file, disable ConfigureAwait checking.
// ReSharper disable ConsiderUsingConfigureAwait

using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Buttplug.Server.Bluetooth.Devices;
using NUnit.Framework;

namespace Buttplug.Server.Test.Bluetooth.Devices
{
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "Test classes can skip documentation requirements")]
    [TestFixture]
    public class VorzeUFOSATests : VorzeSATests
    {
        private string _deviceName = "UFOSA";
        private byte _commandPrefix = 0x02;
        private VorzeSA.CommandType _commandType = VorzeSA.CommandType.Rotate;

        [Test]
        public async Task TestAllowedMessages()
        {
            await TestAllowedMessages(_deviceName, _commandType);
        }

        // StopDeviceCmd noop test handled in GeneralDeviceTests

        [Test]
        public async Task TestStopDeviceCmd()
        {
            await TestStopDeviceCmd(_deviceName, _commandPrefix, _commandType);
        }

        [Test]
        public async Task TestVorzeA10CycloneCmd()
        {
            await TestVorzeA10CycloneCmd(_deviceName, _commandPrefix);
        }

        [Test]
        public async Task TestRotateCmd()
        {
            await TestRotateCmd(_deviceName, _commandPrefix);
        }

        [Test]
        public async Task TestInvalidCmds()
        {
            await TestInvalidCmds(_deviceName, _commandType);
        }
    }
}