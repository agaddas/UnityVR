using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TP4
{
    /// <summary>
    /// Exercice 6 : fait apparaître aléatoirement des cibles (cubes, cônes, sphères...)
    /// dans une zone, après un délai aléatoire, avec un nombre maximum de cibles.
    /// </summary>
    public class TargetSpawner : MonoBehaviour
    {
        public GameObject[] targetPrefabs;

        [Header("Zone (locale au spawner)")]
        public Vector3 areaSize = new Vector3(6f, 2f, 4f);

        [Header("Rythme")]
        public float minDelay = 1.5f;
        public float maxDelay = 3.5f;
        public int maxTargets = 8;
        public Vector2 scaleRange = new Vector2(0.3f, 0.6f);
        public bool randomColor = true;

        public bool Paused { get; set; }

        readonly List<GameObject> m_Alive = new List<GameObject>();
        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int k_Color = Shader.PropertyToID("_Color");

        void OnEnable() => StartCoroutine(SpawnLoop());

        IEnumerator SpawnLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(minDelay, maxDelay));
                m_Alive.RemoveAll(t => t == null);
                if (!Paused && m_Alive.Count < maxTargets)
                    SpawnOne();
            }
        }

        public GameObject SpawnOne()
        {
            if (targetPrefabs == null || targetPrefabs.Length == 0) return null;
            GameObject prefab = targetPrefabs[Random.Range(0, targetPrefabs.Length)];
            if (prefab == null) return null;

            Vector3 local = new Vector3(
                Random.Range(-0.5f, 0.5f) * areaSize.x,
                Random.Range(-0.5f, 0.5f) * areaSize.y,
                Random.Range(-0.5f, 0.5f) * areaSize.z);
            Vector3 pos = transform.TransformPoint(local);
            Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            GameObject target = Instantiate(prefab, pos, rot);
            target.transform.localScale = prefab.transform.localScale *
                                          (Random.Range(scaleRange.x, scaleRange.y) / 0.45f);

            if (randomColor)
            {
                var block = new MaterialPropertyBlock();
                Color c = Color.HSVToRGB(Random.value, 0.75f, 1f);
                block.SetColor(k_BaseColor, c);
                block.SetColor(k_Color, c);
                foreach (var r in target.GetComponentsInChildren<Renderer>())
                    r.SetPropertyBlock(block);
            }

            StartCoroutine(PopIn(target.transform));
            m_Alive.Add(target);
            return target;
        }

        static IEnumerator PopIn(Transform t)
        {
            Vector3 finalScale = t.localScale;
            float time = 0f;
            const float duration = 0.25f;
            while (t != null && time < duration)
            {
                time += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, time / duration);
                t.localScale = finalScale * k;
                yield return null;
            }
            if (t != null) t.localScale = finalScale;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, areaSize);
        }
    }
}
