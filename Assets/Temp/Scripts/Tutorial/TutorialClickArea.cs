using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>チュートリアルの暗幕が押されたことを知らせる（暗幕は透明でも入力を受けるため、画面のどこを押しても届く）</summary>
public class TutorialClickArea : MonoBehaviour, IPointerClickHandler
{
    public event Action Clicked;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            Clicked?.Invoke();
    }
}
