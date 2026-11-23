using Mirror;
using UnityEngine;
using Pucaci.Network;
using Pucaci.Rendering;

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
        [Tooltip("Mesh tubuh utuh asli Pak Mamat (Cube) untuk tampilan TPP")]
        [SerializeField] private Mesh pakMamatFullBodyMesh;

        [Header("Component References")]
        [SerializeField] private PlayerCharacterController characterController;
        [SerializeField] private FlashlightController flashlightController;
        [SerializeField] private PlayerFootstepAudio footstepAudio;
        [SerializeField] private PlayerNetworkSetup networkSetup;
        [SerializeField] private KuntilanakGhostVanish ghostVanish;

        [SyncVar(hook = nameof(OnCharacterIndexChanged))]
        private int characterIndex = -1;
        private int localCharacterIndex = -1;

        public CharacterType ActiveCharacterType => (CharacterType)Mathf.Max(0, (characterIndex >= 0 ? characterIndex : localCharacterIndex));
        public GameObject ActiveModel
        {
            get
            {
                int idx = characterIndex >= 0 ? characterIndex : localCharacterIndex;
                return (idx == 3) ? modelPakVincent : ((idx == 2) ? modelKuntilanak : ((idx == 1) ? modelPakMamat : modelLaras));
            }
        }

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
            // Jika mode offline / singleplayer: acak 1 warga (0 = Laras, 1 = Pak Mamat, 3 = Pak Vincent)
            if (!NetworkClient.active && !NetworkServer.active)
            {
                int[] villagers = { 0, 1, 3 };
                int randomVillager = villagers[UnityEngine.Random.Range(0, villagers.Length)];
                ApplyCharacter(randomVillager);
            }
            else if (characterIndex >= 0)
            {
                ApplyCharacter(characterIndex);
            }
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            // Karakter ditentukan secara server-authoritative (acak oleh server, tidak bisa dipilih manual)
            if (characterIndex >= 0)
            {
                ApplyCharacter(characterIndex);
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (characterIndex >= 0)
            {
                ApplyCharacter(characterIndex);
            }
        }

        [Server]
        public void SetServerCharacter(int index)
        {
            characterIndex = Mathf.Clamp(index, 0, 3);
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

            if (pakMamatFullBodyMesh == null && modelPakMamat != null)
            {
                var smr = modelPakMamat.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null && smr.sharedMesh != null && smr.sharedMesh != pakMamatArmsMesh)
                {
                    pakMamatFullBodyMesh = smr.sharedMesh;
                }
            }

            if (pakMamatFullBodyMesh == null)
            {
#if UNITY_EDITOR
                var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Game/Models/Character/Pak Mamat/Breathing Idle.fbx");
                foreach (var a in assets)
                {
                    if (a is Mesh m && m.name == "Cube")
                    {
                        pakMamatFullBodyMesh = m;
                        break;
                    }
                }
#endif
            }

            // Pasang Toon Outline tipis (Inverted Hull) ke seluruh model karakter dan senter
            if (modelLaras != null) ToonOutlineApplier.ApplyTo(modelLaras, 1.2f);
            if (modelPakMamat != null) ToonOutlineApplier.ApplyTo(modelPakMamat, 1.2f);
            if (modelKuntilanak != null) ToonOutlineApplier.ApplyTo(modelKuntilanak, 1.2f);
            if (modelPakVincent != null) ToonOutlineApplier.ApplyTo(modelPakVincent, 1.2f);
            if (flashlightController != null) ToonOutlineApplier.ApplyTo(flashlightController.gameObject, 1.0f);
        }

        public Mesh GetOriginalFullBodyMesh(CharacterType type)
        {
            if (type == CharacterType.PakMamat) return pakMamatFullBodyMesh;
            if (type == CharacterType.Laras && modelLaras != null) return modelLaras.GetComponentInChildren<SkinnedMeshRenderer>()?.sharedMesh;
            if (type == CharacterType.Kuntilanak && modelKuntilanak != null) return modelKuntilanak.GetComponentInChildren<SkinnedMeshRenderer>()?.sharedMesh;
            if (type == CharacterType.PakVincent && modelPakVincent != null) return modelPakVincent.GetComponentInChildren<SkinnedMeshRenderer>()?.sharedMesh;
            return null;
        }

        public void SelectCharacter(CharacterType type)
        {
            CharacterSelectionData.CurrentCharacter = type;
            int idx = (int)type;
            ApplyCharacter(idx);

            if (netIdentity != null && isLocalPlayer && NetworkClient.isConnected)
            {
                CmdSetCharacter(idx);
            }
        }

        public void ApplyCharacter(int index)
        {
            int clamped = Mathf.Clamp(index, 0, 3);
            localCharacterIndex = clamped;
            if (netIdentity != null && (isServer || NetworkServer.active))
            {
                characterIndex = clamped;
            }

            AutoResolveModels();

            int activeIdx = characterIndex >= 0 ? characterIndex : localCharacterIndex;
            bool isLaras = (activeIdx == 0);
            bool isMamat = (activeIdx == 1);
            bool isKunti = (activeIdx == 2);
            bool isVincent = (activeIdx == 3);

            if (modelLaras != null) modelLaras.SetActive(isLaras);
            if (modelPakMamat != null)
            {
                // Jika Pak Mamat sedang dinonaktifkan, pastikan mesh-nya dikembalikan ke full-body
                // agar tidak tertinggal dalam keadaan terpotong (tangan saja)
                if (!isMamat && pakMamatFullBodyMesh != null)
                {
                    var mamatSmr = modelPakMamat.GetComponentInChildren<SkinnedMeshRenderer>();
                    if (mamatSmr != null)
                    {
                        mamatSmr.sharedMesh = pakMamatFullBodyMesh;
                    }
                }
                modelPakMamat.SetActive(isMamat);
            }
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
            ToonOutlineApplier.ApplyTo(targetModelObj, 1.2f);

            bool isLocal = true;
            try { isLocal = isLocalPlayer; } catch {}

            if (characterController != null)
            {
                Mesh customFppMesh = (isMamat && isLocal) ? pakMamatArmsMesh : null;
                Mesh targetFullBodyMesh = isMamat ? pakMamatFullBodyMesh : (targetRenderer != null ? targetRenderer.sharedMesh : null);
                characterController.SetupCharacterModel(targetTransform, targetAnim, targetRenderer, customFppMesh, isKunti, modelOffset, targetFullBodyMesh);
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

            if (networkSetup != null)
            {
                if (targetRenderer != null) networkSetup.SetupRenderer(targetRenderer);
                string charName = CharacterSelectionData.GetCharacterName((CharacterType)clamped);
                networkSetup.UpdateNameTagText(charName);
            }
        }
    }
}
