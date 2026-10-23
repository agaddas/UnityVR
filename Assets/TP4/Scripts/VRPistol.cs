using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TP4
{
    /// <summary>
    /// Exercice 5 : arme VR (pistolet, SMG, fusil...). Saisie avec le bouton grip ; la gâchette (trigger)
    /// déclenche l'événement "Activate" de l'XRGrabInteractable, qui appelle Fire().
    /// Fire() fait apparaître une balle au bout du canon et la propulse avec AddForce.
    /// En mode automatique, l'arme tire tant que la gâchette est enfoncée.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class VRPistol : MonoBehaviour
    {
        [Header("Balle")]
        public GameObject bulletPrefab;
        [Tooltip("Point de sortie : son axe Z (bleu) indique la direction du tir")]
        public Transform muzzle;
        [Tooltip("Force de propulsion (impulsion, en N·s). Vitesse = force / masse de la balle")]
        public float propulsionForce = 1.5f;
        public bool bulletUseGravity = true;
        [Tooltip("Durée de vie de la balle (s)")]
        public float bulletLifetime = 5f;

        [Header("Cadence")]
        [Tooltip("Délai minimum entre deux tirs en coup par coup (s)")]
        public float fireCooldown = 0.15f;
        [Tooltip("Tir automatique tant que la gâchette est enfoncée")]
        public bool automatic = false;
        [Tooltip("Cadence en mode automatique (balles par seconde)")]
        public float roundsPerSecond = 10f;

        [Header("Retours")]
        public ParticleSystem muzzleFlash;
        public AudioClip shotSound;
        [Range(0f, 1f)] public float hapticAmplitude = 0.6f;
        public float hapticDuration = 0.08f;

        XRGrabInteractable m_Grab;
        Collider[] m_GunColliders;
        AudioSource m_Audio;
        XRBaseInputInteractor m_Holder;
        bool m_TriggerHeld;
        float m_LastShot = -10f;

        float Cooldown => automatic ? 1f / Mathf.Max(0.1f, roundsPerSecond) : fireCooldown;

        void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
            m_GunColliders = GetComponentsInChildren<Collider>();
            if (shotSound != null)
            {
                m_Audio = gameObject.AddComponent<AudioSource>();
                m_Audio.playOnAwake = false;
                m_Audio.spatialBlend = 1f;
            }
        }

        void OnEnable()
        {
            m_Grab.activated.AddListener(OnActivated);
            m_Grab.deactivated.AddListener(OnDeactivated);
            m_Grab.selectExited.AddListener(OnReleased);
        }

        void OnDisable()
        {
            m_Grab.activated.RemoveListener(OnActivated);
            m_Grab.deactivated.RemoveListener(OnDeactivated);
            m_Grab.selectExited.RemoveListener(OnReleased);
            m_TriggerHeld = false;
        }

        void OnActivated(ActivateEventArgs args)
        {
            m_Holder = args.interactorObject as XRBaseInputInteractor;
            m_TriggerHeld = true;
            TryFireWithHaptics();
        }

        void OnDeactivated(DeactivateEventArgs args) => m_TriggerHeld = false;
        void OnReleased(SelectExitEventArgs args) => m_TriggerHeld = false;

        void Update()
        {
            if (automatic && m_TriggerHeld) TryFireWithHaptics();
        }

        void TryFireWithHaptics()
        {
            if (Fire() && m_Holder != null)
                m_Holder.SendHapticImpulse(hapticAmplitude, hapticDuration);
        }

        /// <summary>Tire une balle. Peut aussi être appelé depuis un UnityEvent.</summary>
        public bool Fire()
        {
            if (bulletPrefab == null || muzzle == null) return false;
            if (Time.time - m_LastShot < Cooldown) return false;
            m_LastShot = Time.time;

            GameObject bullet = Instantiate(bulletPrefab, muzzle.position, muzzle.rotation);

            // La balle ne doit pas entrer en collision avec l'arme elle-même.
            foreach (var bulletCol in bullet.GetComponentsInChildren<Collider>())
                foreach (var gunCol in m_GunColliders)
                    if (gunCol != null) Physics.IgnoreCollision(bulletCol, gunCol);
            if (bullet.TryGetComponent(out Bullet b)) b.ignoreRoot = transform;

            if (bullet.TryGetComponent(out Rigidbody rb))
            {
                rb.useGravity = bulletUseGravity;
                rb.AddForce(muzzle.forward * propulsionForce, ForceMode.Impulse);
            }

            Destroy(bullet, bulletLifetime);

            if (muzzleFlash != null) muzzleFlash.Play(true);
            if (m_Audio != null) m_Audio.PlayOneShot(shotSound);
            return true;
        }
    }
}
