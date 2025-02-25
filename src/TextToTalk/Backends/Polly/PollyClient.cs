﻿using Amazon;
using Amazon.Polly;
using Amazon.Polly.Model;
using Amazon.Runtime;
using Dalamud.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TextToTalk.Backends.Polly
{
    public class PollyClient : IDisposable
    {
        private readonly AmazonPollyClient client;
        private readonly PollySoundQueue soundQueue;

        public PollyClient(string accessKey, string secretKey, RegionEndpoint region)
        {
            var credentials = new BasicAWSCredentials(accessKey, secretKey);
            this.client = new AmazonPollyClient(credentials, region);
            this.soundQueue = new PollySoundQueue();
        }

        public IList<LexiconDescription> GetLexicons()
        {
            var lexiconsReq = new ListLexiconsRequest();

            var lexicons = new List<LexiconDescription>();
            do
            {
                var lexiconsRes = this.client.ListLexiconsAsync(lexiconsReq).GetAwaiter().GetResult();
                lexicons.AddRange(lexiconsRes.Lexicons);
                lexiconsReq.NextToken = lexiconsRes.NextToken;
            } while (!string.IsNullOrEmpty(lexiconsReq.NextToken));

            return lexicons;
        }

        public Lexicon GetLexicon(string lexiconName)
        {
            var lexicon = this.client.GetLexiconAsync(new GetLexiconRequest
            {
                Name = lexiconName,
            }).GetAwaiter().GetResult();

            return lexicon.Lexicon;
        }

        public void UploadLexicon(string lexiconFilePath)
        {
            var content = File.ReadAllText(lexiconFilePath);
            var name = Path.GetFileNameWithoutExtension(lexiconFilePath);

            this.client.PutLexiconAsync(new PutLexiconRequest
            {
                Content = content,
                Name = name,
            }).GetAwaiter().GetResult();
        }

        public void DeleteLexicon(string lexiconName)
        {
            this.client.DeleteLexiconAsync(new DeleteLexiconRequest
            {
                Name = lexiconName,
            });
        }

        public IList<Voice> GetVoicesForEngine(Engine engine)
        {
            var voicesReq = new DescribeVoicesRequest
            {
                Engine = engine,
            };

            var voices = new List<Voice>();
            string nextToken;
            do
            {
                var voicesRes = this.client.DescribeVoicesAsync(voicesReq).GetAwaiter().GetResult();
                voices.AddRange(voicesRes.Voices);
                nextToken = voicesRes.NextToken;
            } while (!string.IsNullOrEmpty(nextToken));

            return voices;
        }

        public TextSource GetCurrentlySpokenTextSource()
        {
            // This should probably be designed differently.
            return this.soundQueue.GetCurrentlySpokenTextSource();
        }

        public async Task Say(Engine engine, VoiceId voice, int sampleRate, float volume, IList<string> lexicons, TextSource source, string text)
        {
            var req = new SynthesizeSpeechRequest
            {
                Text = text,
                VoiceId = voice,
                Engine = engine,
                OutputFormat = OutputFormat.Mp3,
                SampleRate = sampleRate.ToString(),
                LexiconNames = lexicons.Where(l => !string.IsNullOrEmpty(l)).ToList(),
                TextType = TextType.Ssml,
            };

            SynthesizeSpeechResponse res;
            try
            {
                res = await this.client.SynthesizeSpeechAsync(req);
            }
            catch (LexiconNotFoundException e)
            {
                if (lexicons.Any())
                {
                    PluginLog.LogError(e, "A lexicon could not be found, retrying without any lexicons... Additional data: {@Data}", e.Data);
                    await Say(engine, voice, sampleRate, volume, Array.Empty<string>(), source, text);
                }
                else
                {
                    PluginLog.LogError(e, "A lexicon could not be found... but we aren't using any lexicons? Additional data: {@Data}", e.Data);
                }

                return;
            }
            catch (Exception e)
            {
                PluginLog.LogError(e, "Synthesis request failed in {0}.", nameof(PollyClient));
                return;
            }

            var responseStream = new MemoryStream();
            await res.AudioStream.CopyToAsync(responseStream);
            responseStream.Seek(0, SeekOrigin.Begin);

            this.soundQueue.EnqueueSound(responseStream, source, volume);
        }

        public Task CancelAllSounds()
        {
            this.soundQueue.CancelAllSounds();
            return Task.CompletedTask;
        }

        public Task CancelFromSource(TextSource source)
        {
            this.soundQueue.CancelFromSource(source);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            this.client.Dispose();
            this.soundQueue.Dispose();
        }
    }
}