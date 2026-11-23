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
        [SerializeField] private float hoverSlideX = 10f;
        [SerializeField] private RectTransform slideTarget;
        [SerializeField] private Image borderImage;
        [SerializeField] private Color normalBorderColor = new Color(0.2f, 0.25f, 0.35f, 0.6f);
        [SerializeField] private Color highlightBorderColor = new Color(1f, 0.75f, 0.3f, 1f);

        [Header("Horror Brush Tag (The Infected Soul Style)")]
        [SerializeField] private Image brushTagImage;
        [SerializeField] private Color brushTagNormalColor = new Color(1f, 1f, 1f, 0f);
        [SerializeField] private Color brushTagHighlightColor = new Color(0.96f, 0.96f, 0.98f, 1f);

        [Header("Colors")]
        [SerializeField] private bool changeTextColor = true;
        [SerializeField] private Color normalTextColor = new Color(0.72f, 0.76f, 0.82f, 0.82f);
        [SerializeField] private Color highlightTextColor = new Color(0.08f, 0.09f, 0.12f, 1f);
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
        [SerializeField] private bool isSelectedOrHovered;
        private bool canSlide = true;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            initialScale = transform.localScale;

            if (slideTarget == null && targetText != null)
            {
                slideTarget = targetText.rectTransform;
            }
            if (slideTarget == null)
            {
                slideTarget = rectTransform;
            }

            if (slideTarget != null)
            {
                initialAnchoredPos = slideTarget.anchoredPosition;
            }

            // Bisa slide jika slideTarget adalah child (bukan root yang dikontrol LayoutGroup)
            canSlide = (slideTarget != rectTransform) || (GetComponentInParent<LayoutGroup>() == null);

            if (targetText == null)
            {
                targetText = GetComponentInChildren<TMP_Text>();
            }

            if (audioSource == null)
            {
                audioSource = GetComponentInParent<AudioSource>();
            }

            if (borderImage != null)
            {
                borderImage.color = normalBorderColor;
            }

            // Set state awal sesuai teks
            if (targetText != null && changeTextColor)
            {
                targetText.color = isSelectedOrHovered ? highlightTextColor : normalTextColor;
            }
            if (brushTagImage != null)
            {
                brushTagImage.color = isSelectedOrHovered ? brushTagHighlightColor : brushTagNormalColor;
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
            if (brushTagImage != null)
            {
                brushTagImage.color = brushTagNormalColor;
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

            if (brushTagImage != null)
            {
                brushTagImage.color = brushTagHighlightColor;
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

            if (brushTagImage != null)
            {
                brushTagImage.color = brushTagNormalColor;
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
            if (slideTarget == null || !canSlide) return;
            if (gameObject.activeInHierarchy)
            {
                if (slideCoroutine != null) StopCoroutine(slideCoroutine);
                slideCoroutine = StartCoroutine(SlideRoutine(targetPos));
            }
            else
            {
                slideTarget.anchoredPosition = targetPos;
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
            Vector2 start = slideTarget != null ? slideTarget.anchoredPosition : Vector2.zero;
            float elapsed = 0f;
            while (elapsed < scaleDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (slideTarget != null) slideTarget.anchoredPosition = Vector2.Lerp(start, target, elapsed / scaleDuration);
                yield return null;
            }
            if (slideTarget != null) slideTarget.anchoredPosition = target;
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
