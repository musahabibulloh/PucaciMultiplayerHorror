using Mirror;
using UnityEngine;
using Pucaci.Network;

namespace Pucaci.Player
{
    [DisallowMultipleComponent]
    public class CharacterModelManager : NetworkBehaviour
    {
        [Header("Character Models")]
        [Tooltip("Root GameObject model Laras")]
        [SerializeField] private GameObject modelLaras;

        [Tooltip("Root GameObject model Pak Mamat")]
        [SerializeField] private GameObject modelPakMamat;

        [Tooltip("Root GameObject model Kuntilanak")]
        [SerializeField] private GameObject modelKuntilanak;

        [Tooltip("Root GameObject model Pak Vincent")]
        [SerializeField] private GameObject modelPakVincent;

        [Header("First-Person Custom Meshes")]
        [Tooltip("Mesh khusus tangan saja untuk Pak Mamat di tampilan FPP")]
        [SerializeField] private Mesh pakMamatArmsMesh;

        [Header("Component References")]
        [SerializeField] private PlayerCharacterController characterController;
        [SerializeField] private FlashlightController flashlightController;
        [SerializeField] private PlayerFootstepAudio footstepAudio;
        [SerializeField] private PlayerNetworkSetup networkSetup;
        [SerializeField] private KuntilanakGhostVanish ghostVanish;

        [SyncVar(hook = nameof(OnCharacterIndexChanged))]
        private int characterIndex = -1;

        public CharacterType ActiveCharacterType => (CharacterType)Mathf.Max(0, characterIndex);
        public GameObject ActiveModel => (characterIndex == 3) ? modelPakVincent : ((characterIndex == 2) ? modelKuntilanak : ((characterIndex == 1) ? modelPakMamat : modelLaras));

        private void Awake()
        {
            if (characterController == null) characterController = GetComponent<PlayerCharacterController>();
            if (flashlightController == null) flashlightController = GetComponent<FlashlightController>();
            if (footstepAudio == null) footstepAudio = GetComponent<PlayerFootstepAudio>();
            if (networkSetup == null) networkSetup = GetComponent<PlayerNetworkSetup>();
            if (ghostVanish == null) ghostVanish = GetComponent<KuntilanakGhostVanish>();

            AutoResolveModels();
        }

        private void Start()
        {
            // Jika mode offline / singleplayer / belum ada koneksi Mirror yang aktif
            if (!NetworkClient.active && !NetworkServer.active)
            {
                int defaultChoice = (int)CharacterSelectionData.CurrentCharacter;
                ApplyCharacter(defaultChoice);
            }
            else if (characterIndex >= 0)
            {
                ApplyCharacter(characterIndex);
            }
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            int myChoice = (int)CharacterSelectionData.CurrentCharacter;
            ApplyCharacter(myChoice);
            CmdSetCharacter(myChoice);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (characterIndex >= 0)
            {
                ApplyCharacter(characterIndex);
            }
        }

        [Command]
        private void CmdSetCharacter(int index)
        {
            characterIndex = index;
        }

        private void OnCharacterIndexChanged(int oldIndex, int newIndex)
        {
            ApplyCharacter(newIndex);
        }

        public void AutoResolveModels()
        {
            if (modelLaras == null)
            {
                var t = transform.Find("Model_Laras") ?? transform.Find("Model");
                if (t != null) modelLaras = t.gameObject;
            }

            if (modelPakMamat == null)
            {
                var t = transform.Find("Model_PakMamat") ?? transform.Find("Model_Mamat");
                if (t != null) modelPakMamat = t.gameObject;
            }

            if (modelKuntilanak == null)
            {
                var t = transform.Find("Model_Kuntilanak") ?? transform.Find("Model_Kunti");
                if (t != null) modelKuntilanak = t.gameObject;
            }

            if (modelPakVincent == null)
            {
                var t = transform.Find("Model_PakVincent") ?? transform.Find("Model_Vincent");
                if (t != null) modelPakVincent = t.gameObject;
            }

            if (pakMamatArmsMesh == null)
            {
#if UNITY_EDITOR
                pakMamatArmsMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Game/Models/Character/Pak Mamat/PakMamat_ArmsOnly.asset");
#endif
            }
        }

        public void SelectCharacter(CharacterType type)
        {
            CharacterSelectionData.CurrentCharacter = type;
            int idx = (int)type;
            ApplyCharacter(idx);

            if (isLocalPlayer && NetworkClient.isConnected)
            {
                CmdSetCharacter(idx);
            }
        }

        public void ApplyCharacter(int index)
        {
            characterIndex = Mathf.Clamp(index, 0, 3);
            AutoResolveModels();

            bool isLaras = (characterIndex == 0);
            bool isMamat = (characterIndex == 1);
            bool isKunti = (characterIndex == 2);
            bool isVincent = (characterIndex == 3);

            if (modelLaras != null) modelLaras.SetActive(isLaras);
            if (modelPakMamat != null) modelPakMamat.SetActive(isMamat);
            if (modelKuntilanak != null) modelKuntilanak.SetActive(isKunti);
            if (modelPakVincent != null) modelPakVincent.SetActive(isVincent);

            GameObject targetModelObj = isVincent ? modelPakVincent : (isKunti ? modelKuntilanak : (isMamat ? modelPakMamat : modelLaras));
            if (targetModelObj == null) return;

            // Armature & Mesh FBX Pak Vincent memiliki offset X = +0.716134f bawaan export Blender.
            // Berikan kompensasi offset X negatif agar seluruh tubuh dan tulangnya tepat di tengah (center)
            Vector3 modelOffset = isVincent ? new Vector3(-0.716134f, 0f, 0f) : Vector3.zero;

            Transform targetTransform = targetModelObj.transform;
            targetTransform.localPosition = modelOffset;

            Animator targetAnim = targetModelObj.GetComponent<Animator>() ?? targetModelObj.GetComponentInChildren<Animator>();
            if (targetAnim != null)
            {
                targetAnim.applyRootMotion = false;
            }

            SkinnedMeshRenderer targetRenderer = targetModelObj.GetComponentInChildren<SkinnedMeshRenderer>();

            bool isLocal = true;
            try { isLocal = isLocalPlayer; } catch {}

            if (characterController != null)
            {
                Mesh customFppMesh = (isMamat && isLocal) ? pakMamatArmsMesh : null;
                characterController.SetupCharacterModel(targetTransform, targetAnim, targetRenderer, customFppMesh, isKunti, modelOffset);
            }

            if (flashlightController != null)
            {
                flashlightController.SetupFlashlightModel(targetTransform);
            }

            if (footstepAudio != null)
            {
                footstepAudio.SetupFootstepBones(targetTransform, isKunti);
            }

            if (ghostVanish != null)
            {
                ghostVanish.SetKuntilanakActive(isKunti, targetTransform);
            }

            if (networkSetup != null && targetRenderer != null)
            {
                networkSetup.SetupRenderer(targetRenderer);
            }
        }
    }
}
