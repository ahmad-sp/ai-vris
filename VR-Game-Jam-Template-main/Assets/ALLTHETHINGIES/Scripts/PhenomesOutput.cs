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
    public float speedMultiplier = 1f;

    public void StartTest()
    {
        StartCoroutine(Playback());
    }

    public IEnumerator Playback()
    {
        framesGen = GetPhenomeFrames();

        foreach (var f in framesGen)
        {
            if(f.pauseAfter > 300) yield return new WaitForSeconds(0.1f);
            currentMouthCode = f.mouthCode;
            yield return new WaitForSeconds(f.pauseAfter * speedMultiplier / 1000f);
        }

        currentMouthCode = 0;
    }

}
