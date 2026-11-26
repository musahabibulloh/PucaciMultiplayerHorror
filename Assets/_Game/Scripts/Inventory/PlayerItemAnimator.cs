using System.Collections;
using UnityEngine;

namespace Pucaci.Inventory
{
    /// <summary>
    /// Mengelola animasi prosedural karakter saat menggunakan item yang sedang dipegang (Keris, Kentongan, Tulup, Totem).
    /// Bekerja mulus baik pada sudut pandang First-Person maupun Third-Person,
    /// serta menggerakkan transform item dan tangan kanan karakter secara dinamis.
    /// </summary>
    public class PlayerItemAnimator : MonoBehaviour
    {
        private Coroutine activeAnimationCoroutine;

        public bool IsPlayingAnimation => activeAnimationCoroutine != null;

        /// <summary>
        /// Memainkan animasi penggunaan item sesuai tipe item yang dipegang.
        /// </summary>
        public void PlayUseAnimation(ItemType type, GameObject handItemObj, Transform rightHandBone)
        {
            if (activeAnimationCoroutine != null)
            {
                StopCoroutine(activeAnimationCoroutine);
                activeAnimationCoroutine = null;
            }

            if (handItemObj == null) return;

            switch (type)
            {
                case ItemType.Keris:
                    activeAnimationCoroutine = StartCoroutine(CoAnimateKerisSlash(handItemObj.transform, rightHandBone));
                    break;
                case ItemType.Kentongan:
                    activeAnimationCoroutine = StartCoroutine(CoAnimateKentonganStrike(handItemObj.transform, rightHandBone));
                    break;
                case ItemType.Tulup:
                    activeAnimationCoroutine = StartCoroutine(CoAnimateTulupShoot(handItemObj.transform, rightHandBone));
                    break;
                case ItemType.Totem:
                    activeAnimationCoroutine = StartCoroutine(CoAnimateTotemRaise(handItemObj.transform, rightHandBone));
                    break;
            }
        }

        /// <summary>
        /// Animasi Serangan Keris: Menarik ke belakang sejenak lalu menebas tajam ke depan dengan ayunan diagonal.
        /// </summary>
        private IEnumerator CoAnimateKerisSlash(Transform itemTransform, Transform rightHandBone)
        {
            Vector3 origPos = itemTransform.localPosition;
            Quaternion origRot = itemTransform.localRotation;

            // 1. Fase Windup (menarik keris ke samping/belakang) ~ 0.08s
            float windupDuration = 0.08f;
            float elapsed = 0f;
            Vector3 windupPos = origPos + new Vector3(-0.06f, 0.08f, -0.10f);
            Quaternion windupRot = origRot * Quaternion.Euler(-35f, 40f, -20f);

            while (elapsed < windupDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / windupDuration;
                itemTransform.localPosition = Vector3.Lerp(origPos, windupPos, t);
                itemTransform.localRotation = Quaternion.Slerp(origRot, windupRot, t);
                yield return null;
            }

            // 2. Fase Slash Cepat Menusuk/Menebas Maju ke Depan ~ 0.12s
            float slashDuration = 0.12f;
            elapsed = 0f;
            Vector3 strikePos = origPos + new Vector3(0.12f, -0.05f, 0.28f);
            Quaternion strikeRot = origRot * Quaternion.Euler(45f, -60f, 35f);

            while (elapsed < slashDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Sin((elapsed / slashDuration) * (Mathf.PI * 0.5f));
                itemTransform.localPosition = Vector3.Lerp(windupPos, strikePos, t);
                itemTransform.localRotation = Quaternion.Slerp(windupRot, strikeRot, t);
                yield return null;
            }

            // 3. Fase Recovery (Kembali ke posisi siap genggam) ~ 0.18s
            float recoverDuration = 0.18f;
            elapsed = 0f;
            while (elapsed < recoverDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / recoverDuration;
                float smoothT = t * t * (3f - 2f * t); // Smoothstep
                itemTransform.localPosition = Vector3.Lerp(strikePos, origPos, smoothT);
                itemTransform.localRotation = Quaternion.Slerp(strikeRot, origRot, smoothT);
                yield return null;
            }

            itemTransform.localPosition = origPos;
            itemTransform.localRotation = origRot;
            activeAnimationCoroutine = null;
        }

        /// <summary>
        /// Animasi Memukul Kentongan: Mengayunkan kentongan ke atas lalu menghentakkannya ke bawah berulang 2x ketukan ritmis.
        /// </summary>
        private IEnumerator CoAnimateKentonganStrike(Transform itemTransform, Transform rightHandBone)
        {
            Vector3 origPos = itemTransform.localPosition;
            Quaternion origRot = itemTransform.localRotation;

            // Lakukan 2x ketukan pukulan ritmis (TUK-TUK!)
            for (int strike = 0; strike < 2; strike++)
            {
                // Angkat sedikit ~0.07s
                float upDuration = 0.07f;
                float elapsed = 0f;
                Vector3 upPos = origPos + new Vector3(0f, 0.08f, -0.04f);
                Quaternion upRot = origRot * Quaternion.Euler(-25f, 10f, 0f);

                while (elapsed < upDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / upDuration;
                    itemTransform.localPosition = Vector3.Lerp(origPos, upPos, t);
                    itemTransform.localRotation = Quaternion.Slerp(origRot, upRot, t);
                    yield return null;
                }

                // Hentak ke bawah memukul kentongan ~0.08s
                float downDuration = 0.08f;
                elapsed = 0f;
                Vector3 downPos = origPos + new Vector3(0.03f, -0.06f, 0.08f);
                Quaternion downRot = origRot * Quaternion.Euler(30f, -15f, 0f);

                while (elapsed < downDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / downDuration;
                    itemTransform.localPosition = Vector3.Lerp(upPos, downPos, t);
                    itemTransform.localRotation = Quaternion.Slerp(upRot, downRot, t);
                    yield return null;
                }

                // Pantulan kembali ~0.08s
                float bounceDuration = 0.08f;
                elapsed = 0f;
                while (elapsed < bounceDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / bounceDuration;
                    itemTransform.localPosition = Vector3.Lerp(downPos, origPos, t);
                    itemTransform.localRotation = Quaternion.Slerp(downRot, origRot, t);
                    yield return null;
                }

                if (strike == 0) yield return new WaitForSeconds(0.04f);
            }

            itemTransform.localPosition = origPos;
            itemTransform.localRotation = origRot;
            activeAnimationCoroutine = null;
        }

        /// <summary>
        /// Animasi Menembak Tulup: Mengangkat pipa tulup ke depan mulut/kamera, meniup, lalu mengalami sentakan recoil mundur.
        /// </summary>
        private IEnumerator CoAnimateTulupShoot(Transform itemTransform, Transform rightHandBone)
        {
            Vector3 origPos = itemTransform.localPosition;
            Quaternion origRot = itemTransform.localRotation;

            // 1. Angkat ke posisi mulut / membidik (Raise to Mouth) ~0.10s
            float raiseDuration = 0.10f;
            float elapsed = 0f;
            Vector3 aimPos = origPos + new Vector3(-0.04f, 0.10f, 0.08f);
            Quaternion aimRot = origRot * Quaternion.Euler(-18f, 12f, 5f);

            while (elapsed < raiseDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / raiseDuration;
                itemTransform.localPosition = Vector3.Lerp(origPos, aimPos, t);
                itemTransform.localRotation = Quaternion.Slerp(origRot, aimRot, t);
                yield return null;
            }

            // 2. Sentakan Recoil Meniup (Blow Recoil mundur sedikit) ~0.09s
            float recoilDuration = 0.09f;
            elapsed = 0f;
            Vector3 recoilPos = aimPos + new Vector3(0.01f, 0.02f, -0.07f);
            Quaternion recoilRot = aimRot * Quaternion.Euler(12f, 0f, 0f);

            while (elapsed < recoilDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / recoilDuration;
                itemTransform.localPosition = Vector3.Lerp(aimPos, recoilPos, t);
                itemTransform.localRotation = Quaternion.Slerp(aimRot, recoilRot, t);
                yield return null;
            }

            // 3. Kembali ke posisi awal ~0.20s
            float returnDuration = 0.20f;
            elapsed = 0f;
            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / returnDuration;
                float smoothT = t * t * (3f - 2f * t);
                itemTransform.localPosition = Vector3.Lerp(recoilPos, origPos, smoothT);
                itemTransform.localRotation = Quaternion.Slerp(recoilRot, origRot, smoothT);
                yield return null;
            }

            itemTransform.localPosition = origPos;
            itemTransform.localRotation = origRot;
            activeAnimationCoroutine = null;
        }

        /// <summary>
        /// Animasi Mengangkat Totem / Jimat Perlindungan: Mengangkat jimat ke depan dada/ke atas dengan getaran mistis.
        /// </summary>
        private IEnumerator CoAnimateTotemRaise(Transform itemTransform, Transform rightHandBone)
        {
            Vector3 origPos = itemTransform.localPosition;
            Quaternion origRot = itemTransform.localRotation;

            float raiseDuration = 0.15f;
            float elapsed = 0f;
            Vector3 upPos = origPos + new Vector3(0f, 0.14f, 0.10f);
            Quaternion upRot = origRot * Quaternion.Euler(-20f, 0f, 0f);

            while (elapsed < raiseDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / raiseDuration;
                itemTransform.localPosition = Vector3.Lerp(origPos, upPos, t);
                itemTransform.localRotation = Quaternion.Slerp(origRot, upRot, t);
                yield return null;
            }

            // Getaran berkah doa selama 0.4 detik
            float pulseTime = 0.4f;
            float pulseElapsed = 0f;
            while (pulseElapsed < pulseTime)
            {
                pulseElapsed += Time.deltaTime;
                float shake = Mathf.Sin(pulseElapsed * 35f) * 0.015f;
                itemTransform.localPosition = upPos + new Vector3(shake, shake * 0.5f, 0f);
                yield return null;
            }

            // Kembali ke posisi normal
            float returnDuration = 0.20f;
            elapsed = 0f;
            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / returnDuration;
                itemTransform.localPosition = Vector3.Lerp(upPos, origPos, t);
                itemTransform.localRotation = Quaternion.Slerp(upRot, origRot, t);
                yield return null;
            }

            itemTransform.localPosition = origPos;
            itemTransform.localRotation = origRot;
            activeAnimationCoroutine = null;
        }
    }
}
