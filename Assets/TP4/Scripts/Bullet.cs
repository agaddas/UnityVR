using UnityEngine;

namespace TP4
{
    /// <summary>
    /// Balle tirée par une arme (exercice 5).
    /// Une balle rapide (30 m/s) avance de ~60 cm par pas de physique : elle peut traverser une cible
    /// sans que la collision soit détectée. On vérifie donc aussi le trajet parcouru à chaque pas
    /// avec un SphereCast, ce qui rend la détection des cibles fiable (exercice 6).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Bullet : MonoBehaviour
    {
        [Tooltip("Détruire la balle dès qu'elle touche quelque chose")]
        public bool destroyOnImpact = true;
        [Tooltip("Rayon utilisé pour la détection le long du trajet")]
        public float hitRadius = 0.02f;

        [Tooltip("Effet d'impact (optionnel), joué quand la balle touche autre chose qu'une cible")]
        public GameObject impactPrefab;
        public float impactScale = 0.3f;

        /// <summary>Arme qui a tiré : ses colliders sont ignorés.</summary>
        [HideInInspector] public Transform ignoreRoot;

        Rigidbody m_Body;
        Vector3 m_LastPosition;
        bool m_HasHit;

        void Awake()
        {
            m_Body = GetComponent<Rigidbody>();
            m_LastPosition = transform.position;
        }

        void FixedUpdate()
        {
            if (m_HasHit) return;
            Vector3 current = m_Body.position;
            Vector3 delta = current - m_LastPosition;
            float distance = delta.magnitude;
            if (distance > 0.0001f)
            {
                RaycastHit[] hits = Physics.SphereCastAll(m_LastPosition, hitRadius, delta / distance, distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue;
                int nearest = -1;
                for (int i = 0; i < hits.Length; i++)
                {
                    if (IsIgnored(hits[i].collider)) continue;
                    if (hits[i].distance < best) { best = hits[i].distance; nearest = i; }
                }
                if (nearest >= 0)
                {
                    RaycastHit h = hits[nearest];
                    // distance 0 = déjà en contact au départ : le point renvoyé n'est pas fiable.
                    Vector3 point = h.distance > 0f ? h.point : m_LastPosition;
                    Hit(h.collider, point, h.normal);
                    return;
                }
            }
            m_LastPosition = current;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (m_HasHit || IsIgnored(collision.collider)) return;
            bool hasContact = collision.contactCount > 0;
            ContactPoint c = hasContact ? collision.GetContact(0) : default;
            Hit(collision.collider, hasContact ? c.point : transform.position, hasContact ? c.normal : -transform.forward);
        }

        bool IsIgnored(Collider col)
        {
            if (col == null) return true;
            if (col.transform.IsChildOf(transform)) return true;
            if (ignoreRoot != null && col.transform.IsChildOf(ignoreRoot)) return true;
            return col.GetComponentInParent<Bullet>() != null;
        }

        void Hit(Collider col, Vector3 point, Vector3 normal)
        {
            m_HasHit = true;

            ShootingTarget target = col.GetComponentInParent<ShootingTarget>();
            if (target != null)
            {
                target.TakeHit(point);
            }
            else if (impactPrefab != null)
            {
                Quaternion rot = normal.sqrMagnitude > 0.001f ? Quaternion.LookRotation(normal) : Quaternion.identity;
                GameObject fx = Instantiate(impactPrefab, point, rot);
                fx.transform.localScale = impactPrefab.transform.localScale * impactScale;
                Destroy(fx, 2f);
            }

            if (destroyOnImpact)
                Destroy(gameObject);
        }
    }
}
