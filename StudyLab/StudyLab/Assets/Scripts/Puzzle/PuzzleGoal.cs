using UnityEngine;

namespace VARCO_Workshop
{
    public class PuzzleGoal : MonoBehaviour
    {
        public void TriggerPuzzleSolved() { if (GameManager.Instance) GameManager.Instance.TriggerClear(); }
    }
}
