using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Pucaci.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class MenuPanel : MonoBehaviour
    {
        [Header("Navigation")]
        [Tooltip("Elemen UI yang otomatis dipilih saat panel ini terbuka (untuk keyboard & gamepad)")]
        [SerializeField] private Selectable firstSelected;

        private CanvasGroup canvasGroup;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        public virtual void Show()
        {
            gameObject.SetActive(true);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            if (firstSelected != null)
            {
                EventSystem.current?.SetSelectedGameObject(firstSelected.gameObject);
            }
        }

        public virtual void Hide()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            gameObject.SetActive(false);
        }

        public Selectable FirstSelected => firstSelected;
    }
}
