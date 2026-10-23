using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TP4
{
    /// <summary>
    /// Exercice 4 : logique du panneau UI (boutons, slider, InputField) utilisé au laser.
    /// </summary>
    public class UIDemoPanel : MonoBehaviour
    {
        [Header("Widgets")]
        public Button resetScoreButton;
        public Button toggleSpawnButton;
        public TMP_Text toggleSpawnLabel;
        public Slider forceSlider;
        public TMP_Text forceLabel;
        public TMP_InputField nameField;
        public TMP_Text greetingLabel;

        [Header("Objets pilotés")]
        public VRPistol pistol;
        public TargetSpawner spawner;

        void Start()
        {
            if (resetScoreButton != null)
                resetScoreButton.onClick.AddListener(ScoreBoard.ResetScore);

            if (toggleSpawnButton != null)
                toggleSpawnButton.onClick.AddListener(ToggleSpawn);
            RefreshSpawnLabel();

            if (forceSlider != null)
            {
                if (pistol != null) forceSlider.SetValueWithoutNotify(pistol.propulsionForce);
                forceSlider.onValueChanged.AddListener(OnForceChanged);
                OnForceChanged(forceSlider.value);
            }

            if (nameField != null)
            {
                nameField.onValueChanged.AddListener(OnNameChanged);
                OnNameChanged(nameField.text);
            }
        }

        void ToggleSpawn()
        {
            if (spawner == null) return;
            spawner.Paused = !spawner.Paused;
            RefreshSpawnLabel();
        }

        void RefreshSpawnLabel()
        {
            if (toggleSpawnLabel != null)
                toggleSpawnLabel.text = spawner != null && spawner.Paused ? "Reprendre les cibles" : "Pause des cibles";
        }

        void OnForceChanged(float value)
        {
            if (pistol != null) pistol.propulsionForce = value;
            if (forceLabel != null) forceLabel.text = $"Force de tir : {value:0.00}";
        }

        void OnNameChanged(string value)
        {
            if (greetingLabel == null) return;
            greetingLabel.text = string.IsNullOrWhiteSpace(value)
                ? "Tapez votre nom avec le clavier virtuel"
                : $"Bonjour, {value} !";
        }
    }
}
