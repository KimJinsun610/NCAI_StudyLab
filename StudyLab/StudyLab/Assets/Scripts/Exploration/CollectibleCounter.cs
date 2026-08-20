using System;
using UnityEngine;

namespace VARCO_Workshop
{
    public class CollectibleCounter : MonoBehaviour
    {
        int count;
        public int Count => count;
        public event Action<int> OnChanged;
        public void Add(int n = 1)
        {
            count += n;
            OnChanged?.Invoke(count);
            VARCOGameHUD.ShowNotice("아이템을 얻었습니다. 현재 수집: " + count);
        }
    }
}
