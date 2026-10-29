using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Pucaci.UI
{
    public class ButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        [Header("Scale Animation")]
        [SerializeField] private float hoverScale = 1.05f;
        [SerializeField] private float clickScale = 0.95f;
        [SerializeField] private float scaleDuration = 0.1f;

        [Header("Colors")]
        [SerializeField] private bool changeTextColor = true;
        [SerializeField] private Color normalTextColor = new Color(0.95f, 0.91f, 0.84f, 1f); // #F2E8D5
        [SerializeField] private Color highlightTextColor = new Color(1f, 0.70f, 0.28f, 1f); // #FFB347 (Aksen Hangat)
        [SerializeField] private TMP_Text targetText;

        [Header("Audio (Optional)")]
        [SerializeField] private AudioClip hoverSound;
        [SerializeField] private AudioClip clickSound;
        [SerializeField] private AudioSource audioSource;

        private Vector3 initialScale;
        private Coroutine scaleCoroutine;
        private bool isSelectedOrHovered;

        private void Awake()
        {
            initialScale = transform.localScale;
            if (targetText == null)
            {
                targetText = GetComponentInChildren<TMP_Text>();
            }

            if (audioSource == null)
            {
                audioSource = GetComponentInParent<AudioSource>();
            }

            if (targetText != null && changeTextColor)
            {
                targetText.color = normalTextColor;
            }
        }

        private void OnDisable()
        {
            transform.localScale = initialScale;
            if (targetText != null && changeTextColor)
            {
                targetText.color = normalTextColor;
            }
            isSelectedOrHovered = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            TriggerHover();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            TriggerExit();
        }

        public void OnSelect(BaseEventData eventData)
        {
            TriggerHover();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            TriggerExit();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            AnimateScale(initialScale * clickScale);
            PlaySound(clickSound);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (isSelectedOrHovered)
            {
                AnimateScale(initialScale * hoverScale);
            }
            else
            {
                AnimateScale(initialScale);
            }
        }

        private void TriggerHover()
        {
            isSelectedOrHovered = true;
            AnimateScale(initialScale * hoverScale);
            if (targetText != null && changeTextColor)
            {
                targetText.color = highlightTextColor;
            }
            PlaySound(hoverSound);
        }

        private void TriggerExit()
        {
            isSelectedOrHovered = false;
            AnimateScale(initialScale);
            if (targetText != null && changeTextColor)
            {
                targetText.color = normalTextColor;
            }
        }

        private void AnimateScale(Vector3 target)
        {
            if (gameObject.activeInHierarchy)
            {
                if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
                scaleCoroutine = StartCoroutine(ScaleRoutine(target));
            }
            else
            {
                transform.localScale = target;
            }
        }

        private IEnumerator ScaleRoutine(Vector3 target)
        {
            Vector3 start = transform.localScale;
            float elapsed = 0f;
            while (elapsed < scaleDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                transform.localScale = Vector3.Lerp(start, target, elapsed / scaleDuration);
                yield return null;
            }
            transform.localScale = target;
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
    }
}
