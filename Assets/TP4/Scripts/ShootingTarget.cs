using System;
using UnityEngine;

namespace TP4
{
    /// <summary>
    /// Exercice 6 : cible. Détecte l'impact d'une balle (OnCollisionEnter),
    /// déclenche un Particle System d'explosion/fumée, ajoute des points et se détruit.
    /// </summary>
    public class ShootingTarget : MonoBehaviour
    {
        public static event Action<ShootingTarget> Destroyed;

        [Tooltip("Prefab contenant le(s) Particle System d'explosion (utilisé si la liste ci-dessous est vide)")]
        public GameObject explosionPrefab;
        [Tooltip("Explosions possibles (ex. Cartoon FX) : une est tirée au hasard")]
        public GameObject[] explosionPrefabs;
        public float explosionScale = 1f;
        public float explosionLifetime = 3f;

        [Header("Texte BOOM / POW (optionnel)")]
        public GameObject[] popupPrefabs;
        [Range(0f, 1f)] public float popupChance = 0.5f;
        public float popupScale = 0.6f;
        public int points = 10;
        [Tooltip("Nombre de balles nécessaires")]
        public int hitPoints = 1;

        [Header("Animation (optionnelle)")]
        public float spinSpeed = 30f;
        public float bobAmplitude = 0.1f;
        public float bobFrequency = 0.6f;

        Vector3 m_StartPos;
        float m_Phase;
        bool m_Dead;

        void Start()
        {
            m_StartPos = transform.position;
            m_Phase = UnityEngine.Random.value * 10f;
        }

        void Update()
        {
            if (spinSpeed != 0f) transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
            if (bobAmplitude > 0f)
                transform.position = m_StartPos +
                    Vector3.up * (Mathf.Sin((Time.time + m_Phase) * bobFrequency * Mathf.PI * 2f) * bobAmplitude);
        }

        // L'impact des balles est détecté par Bullet (trajet vérifié à chaque pas de physique),
        // qui appelle TakeHit(). Au cas où la cible serait configurée en trigger.
        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<Bullet>() == null) return;
            TakeHit(other.transform.position);
            Destroy(other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject);
        }

        public void TakeHit(Vector3 point)
        {
            if (m_Dead) return;
            hitPoints--;
            if (hitPoints > 0) return;

            m_Dead = true;
            GameObject fxPrefab = Pick(explosionPrefabs) ?? explosionPrefab;
            Spawn(fxPrefab, transform.position, explosionScale);
            if (UnityEngine.Random.value < popupChance)
                Spawn(Pick(popupPrefabs), transform.position + Vector3.up * 0.4f, popupScale);
            ScoreBoard.Add(points);
            Destroyed?.Invoke(this);
            Destroy(gameObject);
        }

        static GameObject Pick(GameObject[] list)
        {
            if (list == null || list.Length == 0) return null;
            GameObject g = list[UnityEngine.Random.Range(0, list.Length)];
            return g != null ? g : null;
        }

        void Spawn(GameObject prefab, Vector3 position, float scale)
        {
            if (prefab == null) return;
            GameObject fx = Instantiate(prefab, position, Quaternion.identity);
            fx.transform.localScale = prefab.transform.localScale * scale;
            Destroy(fx, explosionLifetime);
        }
    }
}
