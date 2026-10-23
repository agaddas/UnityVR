using TMPro;
using UnityEngine;

namespace TP4
{
    /// <summary>Score affiché sur le panneau UI (bonus de l'exercice 6).</summary>
    public class ScoreBoard : MonoBehaviour
    {
        public TMP_Text label;

        public static int Score { get; private set; }
        public static int Hits { get; private set; }
        static ScoreBoard s_Instance;

        void Awake()
        {
            s_Instance = this;
            Score = 0;
            Hits = 0;
            Refresh();
        }

        public static void Add(int points)
        {
            Score += points;
            Hits++;
            if (s_Instance != null) s_Instance.Refresh();
        }

        public static void ResetScore()
        {
            Score = 0;
            Hits = 0;
            if (s_Instance != null) s_Instance.Refresh();
        }

        void Refresh()
        {
            if (label != null) label.text = $"Score : {Score}   (cibles : {Hits})";
        }
    }
}
