using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TP4
{
    /// <summary>
    /// Exercice 3 : déplacement au joystick avec le nouveau Input System.
    /// - Joystick gauche : avancer / reculer / pas de côté (relatif à la direction du regard).
    /// - Joystick droit  : rotation par cran (snap turn) de 30° ou 45°.
    /// Clavier (test sans casque) : ZQSD / WASD pour bouger, A/E pour tourner.
    /// </summary>
    [RequireComponent(typeof(XROrigin))]
    public class JoystickLocomotion : MonoBehaviour
    {
        [Header("Déplacement (joystick gauche)")]
        public InputActionProperty moveAction = new InputActionProperty(
            new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2"));
        [Tooltip("Vitesse en m/s")]
        public float moveSpeed = 2f;
        [Range(0f, 0.5f)] public float deadZone = 0.15f;
        [Tooltip("Gravité gérée par ce script (laisser désactivé si le Gravity Provider XRI est actif)")]
        public bool applyGravity = false;

        [Header("Rotation (joystick droit)")]
        public InputActionProperty turnAction = new InputActionProperty(
            new InputAction("Turn", InputActionType.Value, expectedControlType: "Vector2"));
        [Tooltip("Angle de chaque cran : 30 ou 45 degrés")]
        public float snapAngle = 45f;
        [Range(0.1f, 1f)] public float turnThreshold = 0.6f;
        [Tooltip("Délai minimum entre deux crans (s)")]
        public float turnCooldown = 0.25f;

        [Header("Compatibilité template XRI")]
        [Tooltip("Désactive les Move/Turn providers du XR Origin pour éviter un double déplacement")]
        public bool disableDefaultXRILocomotion = true;

        XROrigin m_Origin;
        CharacterController m_Controller;
        bool m_TurnArmed = true;
        float m_LastTurnTime = -10f;
        float m_VerticalSpeed;

        void Awake()
        {
            m_Origin = GetComponent<XROrigin>();
            m_Controller = GetComponent<CharacterController>();
            EnsureBindings();

            if (disableDefaultXRILocomotion)
                DisableDefaultProviders();
        }

        void OnEnable()
        {
            moveAction.action?.Enable();
            turnAction.action?.Enable();
        }

        void OnDisable()
        {
            moveAction.action?.Disable();
            turnAction.action?.Disable();
        }

        void EnsureBindings()
        {
            var move = moveAction.action;
            if (move != null && move.bindings.Count == 0)
            {
                move.AddBinding("<XRController>{LeftHand}/{Primary2DAxis}");
                move.AddBinding("<Gamepad>/leftStick");
                move.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                    .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            }

            var turn = turnAction.action;
            if (turn != null && turn.bindings.Count == 0)
            {
                turn.AddBinding("<XRController>{RightHand}/{Primary2DAxis}");
                turn.AddBinding("<Gamepad>/rightStick");
                turn.AddCompositeBinding("2DVector")
                    .With("Left", "<Keyboard>/q").With("Right", "<Keyboard>/e");
            }
        }

        void Update()
        {
            HandleMove();
            HandleSnapTurn();
        }

        void HandleMove()
        {
            var action = moveAction.action;
            Vector2 input = action != null ? action.ReadValue<Vector2>() : Vector2.zero;
            if (input.magnitude < deadZone) input = Vector2.zero;
            input = Vector2.ClampMagnitude(input, 1f);

            Transform head = m_Origin.Camera != null ? m_Origin.Camera.transform : transform;
            Vector3 up = m_Origin.transform.up;
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(head.right, up).normalized;

            Vector3 motion = (forward * input.y + right * input.x) * (moveSpeed * Time.deltaTime);

            if (m_Controller != null && m_Controller.enabled)
            {
                if (applyGravity)
                {
                    if (m_Controller.isGrounded && m_VerticalSpeed < 0f) m_VerticalSpeed = -1f;
                    else m_VerticalSpeed += Physics.gravity.y * Time.deltaTime;
                    motion += up * (m_VerticalSpeed * Time.deltaTime);
                }
                if (motion.sqrMagnitude > 0f) m_Controller.Move(motion);
            }
            else
            {
                m_Origin.transform.position += motion;
            }
        }

        void HandleSnapTurn()
        {
            var action = turnAction.action;
            float x = action != null ? action.ReadValue<Vector2>().x : 0f;

            if (Mathf.Abs(x) >= turnThreshold)
            {
                if (m_TurnArmed && Time.time - m_LastTurnTime >= turnCooldown)
                {
                    // Tourne autour de la tête du joueur et non autour du centre du rig.
                    m_Origin.RotateAroundCameraUsingOriginUp(Mathf.Sign(x) * snapAngle);
                    m_LastTurnTime = Time.time;
                    m_TurnArmed = false; // il faut relâcher le joystick pour tourner à nouveau
                }
            }
            else if (Mathf.Abs(x) < turnThreshold * 0.5f)
            {
                m_TurnArmed = true;
            }
        }

        void DisableDefaultProviders()
        {
            foreach (var behaviour in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour == this) continue;
                for (var t = behaviour.GetType(); t != null; t = t.BaseType)
                {
                    if (t.Name == "ContinuousMoveProvider" || t.Name == "SnapTurnProvider" ||
                        t.Name == "ContinuousTurnProvider")
                    {
                        behaviour.enabled = false;
                        break;
                    }
                }
            }
        }
    }
}
