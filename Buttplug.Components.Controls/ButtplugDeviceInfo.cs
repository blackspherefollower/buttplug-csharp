﻿using System.Collections.Generic;

namespace Buttplug.Components.Controls
{
    public class ButtplugDeviceInfo
    {
        public string Name { get; }

        public uint Index { get; }

        public Dictionary<string, Dictionary<string, string>> Messages { get; }

        public ButtplugDeviceInfo(uint aIndex, string aName,
            Dictionary<string, Dictionary<string, string>> aMessages)
        {
            Index = aIndex;
            Name = aName;
            Messages = aMessages;
        }

        public override string ToString()
        {
            return $"{Index}: {Name}";
        }
    }
}