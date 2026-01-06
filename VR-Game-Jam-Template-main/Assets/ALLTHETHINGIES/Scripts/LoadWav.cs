using UnityEngine;
using System;
using System.IO;

/// <summary>
/// Utility class to load WAV files from bytes into Unity AudioClips
/// </summary>
public static class LoadWav
{
    public static AudioClip FromBytes(byte[] wavBytes, string name = "LoadedWav")
    {
        if (wavBytes == null || wavBytes.Length < 44)
        {
            Debug.LogError("LoadWav: Invalid WAV data (too short or null)");
            return null;
        }

        try
        {
            using (var stream = new MemoryStream(wavBytes))
            using (var reader = new BinaryReader(stream))
            {
                // Read RIFF header
                string riff = new string(reader.ReadChars(4));
                if (riff != "RIFF")
                {
                    Debug.LogError("LoadWav: Not a valid RIFF file");
                    return null;
                }

                int chunkSize = reader.ReadInt32();
                string format = new string(reader.ReadChars(4));
                if (format != "WAVE")
                {
                    Debug.LogError("LoadWav: Not a valid WAVE file");
                    return null;
                }

                // Read fmt chunk
                string fmtChunk = new string(reader.ReadChars(4));
                if (fmtChunk != "fmt ")
                {
                    Debug.LogError("LoadWav: fmt chunk not found");
                    return null;
                }

                int fmtChunkSize = reader.ReadInt32();
                ushort audioFormat = reader.ReadUInt16(); // Should be 1 for PCM
                ushort numChannels = reader.ReadUInt16();
                int sampleRate = reader.ReadInt32();
                int byteRate = reader.ReadInt32();
                ushort blockAlign = reader.ReadUInt16();
                ushort bitsPerSample = reader.ReadUInt16();

                // Skip any extra fmt data
                if (fmtChunkSize > 16)
                {
                    reader.ReadBytes(fmtChunkSize - 16);
                }

                // Find data chunk
                string dataChunk = "";
                int dataChunkSize = 0;
                while (stream.Position < stream.Length)
                {
                    dataChunk = new string(reader.ReadChars(4));
                    dataChunkSize = reader.ReadInt32();

                    if (dataChunk == "data")
                    {
                        break;
                    }
                    else
                    {
                        // Skip this chunk
                        reader.ReadBytes(dataChunkSize);
                    }
                }

                if (dataChunk != "data")
                {
                    Debug.LogError("LoadWav: data chunk not found");
                    return null;
                }

                // Read audio data
                byte[] audioData = reader.ReadBytes(dataChunkSize);
                
                // Convert bytes to float samples
                int samples = audioData.Length / (bitsPerSample / 8);
                float[] floatData = new float[samples];

                if (bitsPerSample == 16)
                {
                    for (int i = 0; i < samples; i++)
                    {
                        short sample = BitConverter.ToInt16(audioData, i * 2);
                        floatData[i] = sample / 32768f;
                    }
                }
                else if (bitsPerSample == 8)
                {
                    for (int i = 0; i < samples; i++)
                    {
                        floatData[i] = (audioData[i] - 128) / 128f;
                    }
                }
                else
                {
                    Debug.LogError($"LoadWav: Unsupported bits per sample: {bitsPerSample}");
                    return null;
                }

                // Create AudioClip
                AudioClip clip = AudioClip.Create(
                    name,
                    samples / numChannels,
                    numChannels,
                    sampleRate,
                    false
                );

                clip.SetData(floatData, 0);

                Debug.Log($"LoadWav: Loaded WAV - {samples / numChannels} samples, {numChannels} channels, {sampleRate}Hz, {bitsPerSample}bit");
                return clip;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"LoadWav: Error loading WAV - {e.Message}\n{e.StackTrace}");
            return null;
        }
    }
}

