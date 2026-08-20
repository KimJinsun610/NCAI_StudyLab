using UnityEngine;

namespace VARCO_Workshop
{
    /// <summary>다른 시스템(예: 자석 염동력)이 캐릭터 컨트롤러의 이동 입력을 일시적으로
    /// 대신 넣어줄 수 있게 하는 최소 인터페이스. PlayerController_ThirdPerson/_Platform이 구현합니다.</summary>
    public interface IExternalMoveOverride
    {
        void SetQaMoveInput(Vector2 input, bool run);
        void ClearQaMoveInput();
    }
}
