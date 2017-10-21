using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Buttplug.Core
{
    public interface IButtplugDevice
    {
        [NotNull]
        string Name { get; }

        [NotNull]
        string Identifier { get; }

        [NotNull]
        uint Index { get; set; }

        [NotNull]
        bool IsConnected { get; }

        [CanBeNull]
        event EventHandler DeviceRemoved;

        [CanBeNull]
        event EventHandler<MessageReceivedEventArgs> MessageEmitted;

        [NotNull]
        IEnumerable<Type> GetAllowedMessageTypes();

        [NotNull]
        uint VibratorCount { get; }

        [NotNull]
        Task<ButtplugMessage> ParseMessage(ButtplugDeviceMessage aMsg);

        [NotNull]
        Task<ButtplugMessage> Initialize();

        void Disconnect();

        Dictionary<string, string> GetMessageAttrs(Type aMsg);
    }
}
