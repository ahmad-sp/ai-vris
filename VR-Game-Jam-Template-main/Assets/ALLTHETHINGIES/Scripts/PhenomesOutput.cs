using UnityEngine;
using System.Collections;
using System.Collections.Generic;
public class PhenomesOutput : MonoBehaviour
{
    public int currentMouthCode;
    public string sentence;
    public List<Phenominizor.MouthFrame> framesGen;

    public List<Phenominizor.MouthFrame> GetPhenomeFrames()
    {
        List<Phenominizor.MouthFrame> frames = Phenominizor.ToMouthFrames(sentence);
        return frames;
    }
    public float speedMultiplier = 2.5f;

    public void StartTest()
    {
        StartCoroutine(Playback());
    }

    private Coroutine playbackCoroutine;

    public void PlaySentence(string text)
    {
        sentence = text;
        if (playbackCoroutine != null) StopCoroutine(playbackCoroutine);
        playbackCoroutine = StartCoroutine(Playback());
    }

    public void StopLegacy()
    {
         if (playbackCoroutine != null) StopCoroutine(playbackCoroutine);
         currentMouthCode = 0;
    }

    public IEnumerator Playback()
    {
        framesGen = GetPhenomeFrames();

        foreach (var f in framesGen)
        {
            if(f.pauseAfter > 300) yield return new WaitForSeconds(0.1f);
            currentMouthCode = f.mouthCode;
            
            // Speed = Distance / Time -> Time = Distance / Speed
            // We want Higher Speed -> Lower Wait Time
            // formula: (duration / speed)
            yield return new WaitForSeconds((f.pauseAfter / 1000f) / speedMultiplier);
        }

        currentMouthCode = 0;
        playbackCoroutine = null;
    }

}
