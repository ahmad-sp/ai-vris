using System;
using UnityEngine;

public class InterviewerController : MonoBehaviour
{
    public PhenomesOutput inPhenomes;

    [Serializable]
    public struct animprop
    {
        public string name;
        public Vector2 pos;
    }
    public animprop[] AnimPositions;
    private Vector2 currentAnimPosition;

    public Animator InterviewerAnimator;
    public float LerpSpeed = 1f;

    [Range(-1, 1)]
    public float Expression;
    public bool Talking;
    public void Update()
    {
        int cMC = inPhenomes.currentMouthCode;
        if (cMC < 0 || cMC >= AnimPositions.Length) return;
        currentAnimPosition = Vector2.Lerp(currentAnimPosition, AnimPositions[inPhenomes.currentMouthCode].pos, Time.deltaTime * LerpSpeed);
        InterviewerAnimator.SetFloat("Y", currentAnimPosition.y);
        InterviewerAnimator.SetFloat("X", currentAnimPosition.x);
        InterviewerAnimator.SetFloat("Expression", Expression);
        InterviewerAnimator.SetBool("Hands", Talking );
    }
}
