using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pucaci.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class SceneFader : MonoBehaviour
    {
        [SerializeField] private float defaultFadeDuration = 0.6f;
        [SerializeField] private bool fadeInOnStart = true;

        private CanvasGroup canvasGroup;
        private static SceneFader instance;

        public static SceneFader Instance => instance;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }

            canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Start()
        {
            if (fadeInOnStart)
            {
                canvasGroup.alpha = 1f;
                StartCoroutine(FadeRoutine(1f, 0f, defaultFadeDuration, null));
            }
            else
            {
                canvasGroup.alpha = 0f;
            }
        }

        public void FadeIn(float duration = -1f, Action onComplete = null)
        {
            float d = duration > 0 ? duration : defaultFadeDuration;
            StartCoroutine(FadeRoutine(canvasGroup.alpha, 0f, d, onComplete));
        }

        public void FadeOut(float duration = -1f, Action onComplete = null)
        {
            float d = duration > 0 ? duration : defaultFadeDuration;
            StartCoroutine(FadeRoutine(canvasGroup.alpha, 1f, d, onComplete));
        }

        public void FadeAndLoadScene(string sceneName, float duration = -1f)
        {
            float d = duration > 0 ? duration : defaultFadeDuration;
            StartCoroutine(FadeAndLoadRoutine(sceneName, d));
        }

        private IEnumerator FadeRoutine(float startAlpha, float endAlpha, float duration, Action onComplete)
        {
            canvasGroup.blocksRaycasts = endAlpha > 0.01f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
                yield return null;
            }

            canvasGroup.alpha = endAlpha;
            canvasGroup.blocksRaycasts = endAlpha > 0.01f;
            onComplete?.Invoke();
        }

        private IEnumerator FadeAndLoadRoutine(string sceneName, float duration)
        {
            yield return StartCoroutine(FadeRoutine(canvasGroup.alpha, 1f, duration, null));

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning($"[Pucaci.UI] Scene \"{sceneName}\" tidak ditemukan di Build Settings atau belum dibuat! Mohon tambahkan scene ke Build Settings.");
                yield return StartCoroutine(FadeRoutine(1f, 0f, duration, null));
                yield break;
            }

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            while (!asyncLoad.isDone)
            {
                yield return null;
            }
        }
    }
}
