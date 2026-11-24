using System;
using UnityEngine;
using Mirror;

namespace Pucaci.Player
{
    /// <summary>
    /// Sistem nyawa (Health) untuk semua karakter, termasuk Kuntilanak dan manusia.
    /// Mengatur damage, penyembuhan, dan status kematian (TriggerDeath).
    /// Mendukung mode offline/lokal maupun multiplayer (Mirror).
    /// </summary>
    [RequireComponent(typeof(PlayerCharacterController))]
    public class PlayerHealth : NetworkBehaviour
    {
        [Header("Health Settings")]
        [Tooltip("Jumlah nyawa / darah maksimal")]
        [SerializeField] private float maxHealth = 100f;

        [SyncVar(hook = nameof(OnSyncHealthChanged))]
        [SerializeField] private float syncHealth = 100f;

        [SyncVar(hook = nameof(OnSyncDeadChanged))]
        [SerializeField] private bool syncDead = false;

        [SerializeField] private float currentHealth = 100f;
        [SerializeField] private bool isDead = false;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public float HealthNormalized => maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
        public bool IsDead => isDead;

        public event Action<float, float> OnHealthChangedEvent; // current, max
        public event Action OnDeathEvent;
        public event Action OnReviveEvent;

        private PlayerCharacterController pcc;
        private PlayerCharacterController PCC => pcc != null ? pcc : (pcc = GetComponent<PlayerCharacterController>());

        private void Awake()
        {
            pcc = GetComponent<PlayerCharacterController>();
            currentHealth = maxHealth;
            isDead = false;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            currentHealth = maxHealth;
            syncHealth = maxHealth;
            isDead = false;
            syncDead = false;
        }

        public void TakeDamage(float amount)
        {
            if (isDead || amount <= 0f) return;

            if (netIdentity != null && isClient && !isServer)
            {
                CmdTakeDamage(amount);
            }
            else
            {
                ApplyDamage(amount);
            }
        }

        [Command]
        private void CmdTakeDamage(float amount)
        {
            ApplyDamage(amount);
        }

        private void ApplyDamage(float amount)
        {
            if (isDead) return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            if (netIdentity != null && (isServer || NetworkServer.active))
            {
                syncHealth = currentHealth;
            }
            OnHealthChangedEvent?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        /// <summary>
        /// Langsung menghabisi karakter (contoh: hasil cekikan mematikan 5 detik Kuntilanak).
        /// </summary>
        public void Kill()
        {
            if (netIdentity != null && isClient && !isServer)
            {
                CmdTakeDamage(maxHealth * 2f);
            }
            Die();
        }

        public void Die()
        {
            if (isDead) return;

            isDead = true;
            currentHealth = 0f;
            if (netIdentity != null && (isServer || NetworkServer.active))
            {
                syncDead = true;
                syncHealth = 0f;
            }

            if (PCC != null)
            {
                PCC.TriggerDeath();
            }

            var choke = GetComponent<KuntilanakChokeMechanic>();
            if (choke != null)
            {
                choke.SetBeingChoked(false, null);
            }

            OnDeathEvent?.Invoke();

            if (Pucaci.Gameplay.SesajenManager.Instance != null)
            {
                Pucaci.Gameplay.SesajenManager.Instance.CheckSurvivorElimination();
            }
        }

        public void Heal(float amount)
        {
            if (isDead || amount <= 0f) return;

            if (netIdentity != null && isClient && !isServer)
            {
                CmdHeal(amount);
            }
            else
            {
                currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
                if (netIdentity != null && (isServer || NetworkServer.active))
                {
                    syncHealth = currentHealth;
                }
                OnHealthChangedEvent?.Invoke(currentHealth, maxHealth);
            }
        }

        [Command]
        private void CmdHeal(float amount)
        {
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            if (netIdentity != null && (isServer || NetworkServer.active))
            {
                syncHealth = currentHealth;
            }
            OnHealthChangedEvent?.Invoke(currentHealth, maxHealth);
        }

        public void Revive(float healthPercent = 1.0f)
        {
            if (!isDead) return;

            isDead = false;
            currentHealth = maxHealth * Mathf.Clamp01(healthPercent);
            if (netIdentity != null && (isServer || NetworkServer.active))
            {
                syncDead = false;
                syncHealth = currentHealth;
            }

            if (PCC != null)
            {
                PCC.TriggerGetUp();
            }

            OnReviveEvent?.Invoke();
        }

        private void OnSyncHealthChanged(float oldHealth, float newHealth)
        {
            currentHealth = newHealth;
            OnHealthChangedEvent?.Invoke(newHealth, maxHealth);
        }

        private void OnSyncDeadChanged(bool oldDead, bool newDead)
        {
            isDead = newDead;
            if (newDead && PCC != null)
            {
                PCC.TriggerDeath();
                var choke = GetComponent<KuntilanakChokeMechanic>();
                if (choke != null)
                {
                    choke.SetBeingChoked(false, null);
                }
                OnDeathEvent?.Invoke();
            }
            else if (!newDead && PCC != null)
            {
                PCC.TriggerGetUp();
                OnReviveEvent?.Invoke();
            }
        }
    }
}
