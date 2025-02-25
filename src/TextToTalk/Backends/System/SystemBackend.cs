﻿using Dalamud.Interface;
using Dalamud.Logging;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;
using TextToTalk.GameEnums;
using TextToTalk.Lexicons;

namespace TextToTalk.Backends.System
{
    public class SystemBackend : VoiceBackend
    {
        private static readonly Vector4 Red = new(1, 0, 0, 1);

        private static readonly SpeechSynthesizer DummySynthesizer = new();

        private readonly PluginConfiguration config;
        private readonly SystemSoundQueue soundQueue;
        private readonly LexiconManager lexiconManager;

        public SystemBackend(PluginConfiguration config)
        {
            this.config = config;
            this.lexiconManager = new LexiconManager();
            this.soundQueue = new SystemSoundQueue(this.lexiconManager);

            for (var i = 0; i < this.config.Lexicons.Count; i++)
            {
                var lexicon = this.config.Lexicons[i];

                try
                {
                    this.lexiconManager.AddLexicon(lexicon);
                }
                catch (Exception e)
                {
                    PluginLog.LogError(e, "Failed to add lexicon.");
                    this.config.Lexicons.RemoveAt(i);
                    this.config.Save();
                    i--;
                }
            }
        }

        public override void Say(TextSource source, Gender gender, string text)
        {
            var voicePreset = gender switch
            {
                Gender.Male => this.config.GetCurrentMaleVoicePreset(),
                Gender.Female => this.config.GetCurrentFemaleVoicePreset(),
                _ => this.config.GetCurrentUngenderedVoicePreset(),
            };

            this.soundQueue.EnqueueSound(voicePreset, source, text);
        }

        public override void CancelAllSpeech()
        {
            this.soundQueue.CancelAllSounds();
        }

        public override void CancelSay(TextSource source)
        {
            this.soundQueue.CancelFromSource(source);
        }

        public override void DrawSettings(ImExposedFunctions helpers)
        {
            var currentVoicePreset = this.config.GetCurrentVoicePreset();

            var presets = this.config.VoicePresets.ToList();
            presets.Sort((a, b) => a.Id - b.Id);

            if (presets.Any())
            {
                var presetIndex = presets.IndexOf(currentVoicePreset);
                if (ImGui.Combo("Preset##TTTVoice3", ref presetIndex, presets.Select(p => p.Name).ToArray(),
                        presets.Count))
                {
                    this.config.CurrentVoicePresetId = presets[presetIndex].Id;
                    this.config.Save();
                }
            }
            else
            {
                ImGui.TextColored(Red, "You have no voices installed. Please install more voices or use a different backend.");
            }

            if (ImGui.Button("New preset##TTTVoice4") && this.config.TryCreateVoicePreset(out var newPreset))
            {
                this.config.SetCurrentVoicePreset(newPreset.Id);
            }

            if (this.config.VoicePresets.Count > 1)
            {
                ImGui.SameLine();
                if (ImGui.Button("Delete preset##TTTVoice5"))
                {
                    var otherPreset = this.config.VoicePresets.First(p => p.Id != currentVoicePreset.Id);
                    this.config.SetCurrentVoicePreset(otherPreset.Id);

                    if (this.config.UngenderedVoicePresetId == currentVoicePreset.Id)
                    {
                        this.config.UngenderedVoicePresetId = 0;
                    }
                    else if (this.config.MaleVoicePresetId == currentVoicePreset.Id)
                    {
                        this.config.MaleVoicePresetId = 0;
                    }
                    else if (this.config.FemaleVoicePresetId == currentVoicePreset.Id)
                    {
                        this.config.FemaleVoicePresetId = 0;
                    }

                    this.config.VoicePresets.Remove(currentVoicePreset);
                }
            }

            var presetName = currentVoicePreset.Name;
            if (ImGui.InputText("Preset name", ref presetName, 64))
            {
                currentVoicePreset.Name = presetName;
                this.config.Save();
            }

            var rate = currentVoicePreset.Rate;
            if (ImGui.SliderInt("Rate##TTTVoice6", ref rate, -10, 10))
            {
                currentVoicePreset.Rate = rate;
                this.config.Save();
            }

            var volume = currentVoicePreset.Volume;
            if (ImGui.SliderInt("Volume##TTTVoice7", ref volume, 0, 100))
            {
                currentVoicePreset.Volume = volume;
                this.config.Save();
            }

            var voiceName = currentVoicePreset.VoiceName;
            var voices = DummySynthesizer.GetInstalledVoices().Where(iv => iv?.Enabled ?? false).ToList();
            var voiceIndex = voices.FindIndex(iv => iv?.VoiceInfo?.Name == voiceName);
            if (ImGui.Combo("Voice##TTTVoice8",
                ref voiceIndex,
                voices
                    .Where(iv => iv?.Enabled ?? false)
                    .Select(FormatVoiceInfo)
                    .ToArray(),
                voices.Count))
            {
                currentVoicePreset.VoiceName = voices[voiceIndex].VoiceInfo.Name;
                this.config.Save();
            }

            if (ImGui.Button("Don't see all of your voices?##VoiceUnlockerSuggestion"))
            {
                helpers.OpenVoiceUnlockerWindow?.Invoke();
            }

            ImGui.Text("Lexicons");
            for (var i = 0; i < this.config.Lexicons.Count; i++)
            {
                // Remove if no longer existent
                if (!File.Exists(this.config.Lexicons[i]))
                {
                    this.config.Lexicons[i] = "";
                }

                // Editing options
                var lexiconPath = this.config.Lexicons[i];
                var lexiconPathBuf = Encoding.UTF8.GetBytes(lexiconPath);
                ImGui.InputText($"##TTTSystemLexiconText{i}", lexiconPathBuf, (uint)lexiconPathBuf.Length, ImGuiInputTextFlags.ReadOnly);

                if (!string.IsNullOrEmpty(this.config.Lexicons[i]))
                {
                    ImGui.SameLine();
                    var deferred = LexiconRemoveButton(i, lexiconPath);

                    if (this.lexiconRemoveExceptions[i] != null)
                    {
                        ImGui.TextColored(Red, this.lexiconRemoveExceptions[i].Message);
                    }

                    deferred?.Invoke();
                }
            }

            LexiconAddButton();

            if (this.lexiconAddSucceeded)
            {
                ImGui.SameLine();

                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.Text(FontAwesomeIcon.CheckCircle.ToIconString());
                ImGui.PopFont();
            }

            if (this.lexiconAddException != null)
            {
                ImGui.TextColored(Red, this.lexiconAddException.Message);
            }

            ImGui.Spacing();

            if (presets.Any())
            {
                var useGenderedVoicePresets = this.config.UseGenderedVoicePresets;
                if (ImGui.Checkbox("Use gendered voice presets##TTTVoice9", ref useGenderedVoicePresets))
                {
                    this.config.UseGenderedVoicePresets = useGenderedVoicePresets;
                    this.config.Save();
                }

                if (useGenderedVoicePresets)
                {
                    var currentUngenderedVoicePreset = this.config.GetCurrentUngenderedVoicePreset();
                    var currentMaleVoicePreset = this.config.GetCurrentMaleVoicePreset();
                    var currentFemaleVoicePreset = this.config.GetCurrentFemaleVoicePreset();

                    var presetArray = presets.Select(p => p.Name).ToArray();

                    var ungenderedPresetIndex = presets.IndexOf(currentUngenderedVoicePreset);
                    if (ImGui.Combo("Ungendered preset##TTTVoice12", ref ungenderedPresetIndex, presetArray, presets.Count))
                    {
                        this.config.UngenderedVoicePresetId = presets[ungenderedPresetIndex].Id;
                        this.config.Save();
                    }

                    var malePresetIndex = presets.IndexOf(currentMaleVoicePreset);
                    if (ImGui.Combo("Male preset##TTTVoice10", ref malePresetIndex, presetArray, presets.Count))
                    {
                        this.config.MaleVoicePresetId = presets[malePresetIndex].Id;
                        this.config.Save();
                    }

                    var femalePresetIndex = presets.IndexOf(currentFemaleVoicePreset);
                    if (ImGui.Combo("Female preset##TTTVoice11", ref femalePresetIndex, presetArray, presets.Count))
                    {
                        this.config.FemaleVoicePresetId = presets[femalePresetIndex].Id;
                        this.config.Save();
                    }
                }
            }
            else
            {
                ImGui.TextColored(Red, "You have no voices installed. Please install more voices or use a different backend.");
            }
        }

        private static string FormatVoiceInfo(InstalledVoice iv)
        {
            var line = new StringBuilder(iv.VoiceInfo?.Name ?? "");
            line.Append(" (")
                .Append(iv.VoiceInfo?.Culture?.TwoLetterISOLanguageName.ToUpperInvariant() ?? "Unknown Language")
                .Append(")");

            if (iv.VoiceInfo?.Name.Contains("Zira") ?? false)
            {
                line.Append(" [UNSTABLE]");
            }

            return line.ToString();
        }

        public override TextSource GetCurrentlySpokenTextSource()
        {
            return this.soundQueue.GetCurrentlySpokenTextSource();
        }

        private readonly IList<Exception> lexiconRemoveExceptions = new List<Exception>();

        /// <summary>
        /// Draws the remove lexicon button. Returns an action that should be called after all other operations
        /// on the provided index are done.
        /// </summary>
        private Action LexiconRemoveButton(int i, string lexiconPath)
        {
            if (this.lexiconRemoveExceptions.Count < this.config.Lexicons.Count)
            {
                this.lexiconRemoveExceptions.Add(null);
            }

            Action deferred = null;

            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button($"{FontAwesomeIcon.TimesCircle.ToIconString()}##TTTSystemLexiconRemove{i}"))
            {
                try
                {
                    this.lexiconRemoveExceptions[i] = null;
                    this.lexiconManager.RemoveLexicon(lexiconPath);

                    // This is ugly but it works
                    deferred = () =>
                    {
                        this.config.Lexicons.RemoveAt(i);
                        this.lexiconRemoveExceptions.RemoveAt(i);

                        this.config.Save();
                    };
                }
                catch (Exception e)
                {
                    PluginLog.LogError(e, "Failed to remove lexicon.");
                    this.lexiconRemoveExceptions[i] = e;
                }

                this.config.Lexicons[i] = "";
            }
            ImGui.PopFont();

            return deferred;
        }

        private Exception lexiconAddException;
        private bool lexiconAddSucceeded;
        private void LexiconAddButton()
        {
            if (ImGui.Button("Open Lexicon##TTTSystemLexiconAdd"))
            {
                this.lexiconAddException = null;
                this.lexiconAddSucceeded = false;

                _ = Task.Run(() =>
                {
                    var filePath = OpenFile.FileSelect();
                    if (string.IsNullOrEmpty(filePath)) return;

                    try
                    {
                        this.lexiconManager.AddLexicon(filePath);
                        this.config.Lexicons.Add(filePath);
                        this.config.Save();
                        this.lexiconAddSucceeded = true;
                    }
                    catch (Exception e)
                    {
                        PluginLog.LogError(e, "Failed to load lexicon.");
                        this.lexiconAddException = e;
                    }
                });
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.soundQueue.Dispose();
            }
        }
    }
}