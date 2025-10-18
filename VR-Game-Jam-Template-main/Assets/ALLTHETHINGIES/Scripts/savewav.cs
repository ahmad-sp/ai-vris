using UnityEngine;
using System;
using System.IO;
using System.Text;

/// <summary>
/// Utility class to save Unity AudioClips as .wav files
/// </summary>
public static class SavWav
{
    const int HEADER_SIZE = 44;

    public static bool Save(string fullPath, AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogError("SavWav: No audio clip provided to save.");
            return false;
        }

        try
        {
            // ✅ Ensure directory exists
            string directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            Debug.Log($"💾 Saving WAV file to: {fullPath}");

            // Convert AudioClip data
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            using (var fileStream = CreateEmpty(fullPath))
            {
                ConvertAndWrite(fileStream, samples);
                WriteHeader(fileStream, clip);
            }

            Debug.Log($"✅ Audio saved successfully at: {fullPath}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Error saving WAV: {e.Message}");
            return false;
        }
    }

    private static FileStream CreateEmpty(string filePath)
    {
        var fileStream = new FileStream(filePath, FileMode.Create);
        byte emptyByte = new byte();
        for (int i = 0; i < HEADER_SIZE; i++)
        {
            fileStream.WriteByte(emptyByte);
        }
        return fileStream;
    }

    private static void ConvertAndWrite(FileStream fileStream, float[] samples)
    {
        Int16[] intData = new Int16[samples.Length];
        Byte[] bytesData = new Byte[samples.Length * 2];

        const float rescaleFactor = 32767; // to convert float to Int16

        for (int i = 0; i < samples.Length; i++)
        {
            intData[i] = (short)(samples[i] * rescaleFactor);
            Byte[] byteArr = BitConverter.GetBytes(intData[i]);
            byteArr.CopyTo(bytesData, i * 2);
        }

        fileStream.Write(bytesData, 0, bytesData.Length);
    }

    private static void WriteHeader(FileStream fileStream, AudioClip clip)
    {
        var hz = clip.frequency;
        var channels = clip.channels;
        var samples = clip.samples;

        fileStream.Seek(0, SeekOrigin.Begin);

        Byte[] riff = Encoding.UTF8.GetBytes("RIFF");
        fileStream.Write(riff, 0, 4);

        Byte[] chunkSize = BitConverter.GetBytes(fileStream.Length - 8);
        fileStream.Write(chunkSize, 0, 4);

        Byte[] wave = Encoding.UTF8.GetBytes("WAVE");
        fileStream.Write(wave, 0, 4);

        Byte[] fmt = Encoding.UTF8.GetBytes("fmt ");
        fileStream.Write(fmt, 0, 4);

        Byte[] subChunk1 = BitConverter.GetBytes(16);
        fileStream.Write(subChunk1, 0, 4);

        UInt16 audioFormat = 1;
        Byte[] audioFormatBytes = BitConverter.GetBytes(audioFormat);
        fileStream.Write(audioFormatBytes, 0, 2);

        Byte[] numChannels = BitConverter.GetBytes((ushort)channels);
        fileStream.Write(numChannels, 0, 2);

        Byte[] sampleRate = BitConverter.GetBytes(hz);
        fileStream.Write(sampleRate, 0, 4);

        Byte[] byteRate = BitConverter.GetBytes(hz * channels * 2);
        fileStream.Write(byteRate, 0, 4);

        UInt16 blockAlign = (ushort)(channels * 2);
        fileStream.Write(BitConverter.GetBytes(blockAlign), 0, 2);

        UInt16 bitsPerSample = 16;
        fileStream.Write(BitConverter.GetBytes(bitsPerSample), 0, 2);

        Byte[] dataString = Encoding.UTF8.GetBytes("data");
        fileStream.Write(dataString, 0, 4);

        Byte[] subChunk2 = BitConverter.GetBytes(samples * channels * 2);
        fileStream.Write(subChunk2, 0, 4);
    }
}
