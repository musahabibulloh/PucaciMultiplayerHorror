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

        [Header("Slide Animation")]
        [SerializeField] private float hoverSlideX = 14f;
        [SerializeField] private Image borderImage;
        [SerializeField] private Color normalBorderColor = new Color(0.2f, 0.25f, 0.35f, 0.6f);
        [SerializeField] private Color highlightBorderColor = new Color(1f, 0.75f, 0.3f, 1f);

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
        private Vector2 initialAnchoredPos;
        private RectTransform rectTransform;
        private Coroutine scaleCoroutine;
        private Coroutine slideCoroutine;
        private bool isSelectedOrHovered;
        private bool canSlide = true;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            initialScale = transform.localScale;
            if (rectTransform != null) initialAnchoredPos = rectTransform.anchoredPosition;

            // Jangan animasi slide jika posisi dikontrol LayoutGroup (bisa merusak layout)
            canSlide = GetComponentInParent<LayoutGroup>() == null;

            if (targetText == null)
            {
                targetText = GetComponentInChildren<TMP_Text>();
            }

            if (targetText != null && changeTextColor)
            {
                // Ambil warna awal teks yang telah diset di editor/builder
                normalTextColor = targetText.color;
            }

            if (audioSource == null)
            {
                audioSource = GetComponentInParent<AudioSource>();
            }

            if (borderImage != null)
            {
                borderImage.color = normalBorderColor;
            }
        }

        private void OnDisable()
        {
            transform.localScale = initialScale;
            if (rectTransform != null && canSlide) rectTransform.anchoredPosition = initialAnchoredPos;
            if (targetText != null && changeTextColor)
            {
                targetText.color = normalTextColor;
            }
            if (borderImage != null)
            {
                borderImage.color = normalBorderColor;
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
            PlaySound(clickSound, true);
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
            AnimateSlide(initialAnchoredPos + new Vector2(hoverSlideX, 0f));

            if (targetText != null && changeTextColor)
            {
                targetText.color = highlightTextColor;
            }

            if (borderImage != null)
            {
                borderImage.color = highlightBorderColor;
            }

            PlaySound(hoverSound, false);
        }

        private void TriggerExit()
        {
            isSelectedOrHovered = false;
            AnimateScale(initialScale);
            AnimateSlide(initialAnchoredPos);

            if (targetText != null && changeTextColor)
            {
                targetText.color = normalTextColor;
            }

            if (borderImage != null)
            {
                borderImage.color = normalBorderColor;
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

        private void AnimateSlide(Vector2 targetPos)
        {
            if (rectTransform == null || !canSlide) return;
            if (gameObject.activeInHierarchy)
            {
                if (slideCoroutine != null) StopCoroutine(slideCoroutine);
                slideCoroutine = StartCoroutine(SlideRoutine(targetPos));
            }
            else
            {
                rectTransform.anchoredPosition = targetPos;
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

        private IEnumerator SlideRoutine(Vector2 target)
        {
            Vector2 start = rectTransform.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < scaleDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                rectTransform.anchoredPosition = Vector2.Lerp(start, target, elapsed / scaleDuration);
                yield return null;
            }
            rectTransform.anchoredPosition = target;
        }

        private void PlaySound(AudioClip clip, bool isClick)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip);
            }
            else if (MenuAtmosphereAudio.Instance != null)
            {
                if (isClick) MenuAtmosphereAudio.Instance.PlayClickSound();
                else MenuAtmosphereAudio.Instance.PlayHoverSound();
            }
        }
    }
}
