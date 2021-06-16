﻿using System;
using Newtonsoft.Json;

namespace Ayu.Discord.Voice.Models
{
    public sealed class VoiceSpeaking
    {
        [Flags]
        public enum State
        {
            None = 0,
            Microphone = 1 << 0,
            Soundshare = 1 << 1,
            Priority = 1 << 2
        }

        [JsonProperty("speaking")] public int Speaking { get; set; }

        [JsonProperty("delay")] public int Delay { get; set; }

        [JsonProperty("ssrc")] public uint Ssrc { get; set; }
    }
}