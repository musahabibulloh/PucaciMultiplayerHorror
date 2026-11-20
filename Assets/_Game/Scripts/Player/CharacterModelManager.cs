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

        [Header("First-Person Custom Meshes")]
        [Tooltip("Mesh khusus tangan saja untuk Pak Mamat di tampilan FPP")]
        [SerializeField] private Mesh pakMamatArmsMesh;

        [Header("Component References")]
        [SerializeField] private PlayerCharacterController characterController;
        [SerializeField] private FlashlightController flashlightController;
        [SerializeField] private PlayerFootstepAudio footstepAudio;
        [SerializeField] private PlayerNetworkSetup networkSetup;

        [SyncVar(hook = nameof(OnCharacterIndexChanged))]
        private int characterIndex = -1;

        public CharacterType ActiveCharacterType => (CharacterType)Mathf.Max(0, characterIndex);
        public GameObject ActiveModel => (characterIndex == 1) ? modelPakMamat : modelLaras;

        private void Awake()
        {
            if (characterController == null) characterController = GetComponent<PlayerCharacterController>();
            if (flashlightController == null) flashlightController = GetComponent<FlashlightController>();
            if (footstepAudio == null) footstepAudio = GetComponent<PlayerFootstepAudio>();
            if (networkSetup == null) networkSetup = GetComponent<PlayerNetworkSetup>();

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
            characterIndex = Mathf.Clamp(index, 0, 1);
            AutoResolveModels();

            bool isMamat = (characterIndex == 1);

            if (modelLaras != null) modelLaras.SetActive(!isMamat);
            if (modelPakMamat != null) modelPakMamat.SetActive(isMamat);

            GameObject targetModelObj = isMamat ? modelPakMamat : modelLaras;
            if (targetModelObj == null) return;

            Transform targetTransform = targetModelObj.transform;
            targetTransform.localPosition = Vector3.zero;

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
                characterController.SetupCharacterModel(targetTransform, targetAnim, targetRenderer, customFppMesh);
            }

            if (flashlightController != null)
            {
                flashlightController.SetupFlashlightModel(targetTransform);
            }

            if (footstepAudio != null)
            {
                footstepAudio.SetupFootstepBones(targetTransform);
            }

            if (networkSetup != null && targetRenderer != null)
            {
                networkSetup.SetupRenderer(targetRenderer);
            }
        }
    }
}
