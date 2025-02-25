﻿using System;
using System.Speech.Synthesis;
using System.Threading;
using Dalamud.Logging;
using TextToTalk.Lexicons;

namespace TextToTalk.Backends.System
{
    public class SystemSoundQueue : SoundQueue<SystemSoundQueueItem>
    {
        private readonly SpeechSynthesizer speechSynthesizer;
        private readonly LexiconManager lexiconManager;
        private readonly AutoResetEvent speechCompleted;

        public SystemSoundQueue(LexiconManager lexiconManager)
        {
            this.speechCompleted = new AutoResetEvent(false);
            this.lexiconManager = lexiconManager;
            this.speechSynthesizer = new SpeechSynthesizer();

            this.speechSynthesizer.SpeakCompleted += (_, _) =>
            {
                // Allows PlaySoundLoop to continue.
                this.speechCompleted.Set();
            };
        }

        public void EnqueueSound(VoicePreset preset, TextSource source, string text)
        {
            AddQueueItem(new SystemSoundQueueItem
            {
                Preset = preset,
                Text = text,
                Source = source,
            });
        }

        protected override void OnSoundLoop(SystemSoundQueueItem nextItem)
        {
            this.speechSynthesizer.UseVoicePreset(nextItem.Preset);

            var ssml = this.lexiconManager.MakeSsml(nextItem.Text, this.speechSynthesizer.Voice.Culture.IetfLanguageTag);
            PluginLog.Log(ssml);

            this.speechSynthesizer.SpeakSsmlAsync(ssml);

            // Waits for the AutoResetEvent lock in the callback to fire.
            this.speechCompleted.WaitOne();
        }

        protected override void OnSoundCancelled()
        {
            try
            {
                this.speechSynthesizer.SpeakAsyncCancelAll();
            }
            catch (ObjectDisposedException) { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.speechSynthesizer.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}