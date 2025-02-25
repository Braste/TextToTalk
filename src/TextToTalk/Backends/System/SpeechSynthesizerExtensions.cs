﻿using Dalamud.Logging;
using System;
using System.Speech.Synthesis;

namespace TextToTalk.Backends.System
{
    public static class SpeechSynthesizerExtensions
    {
        public static void UseVoicePreset(this SpeechSynthesizer synthesizer, VoicePreset preset)
        {
            synthesizer.Rate = preset.Rate;
            synthesizer.Volume = preset.Volume;
            try
            {
                if (synthesizer.Voice.Name != preset.VoiceName)
                {
                    synthesizer.SelectVoice(preset.VoiceName);
                }
            }
            catch (Exception e)
            {
                PluginLog.LogError(e, "Failed to select voice {0}", preset.VoiceName);
            }
        }
    }
}