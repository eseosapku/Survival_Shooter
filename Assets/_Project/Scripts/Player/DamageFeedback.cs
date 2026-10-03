using Ricochet.Audio;
using Ricochet.Core;
using UnityEngine;

namespace Ricochet.Player
{
    /// <summary>
    /// Non-UI feedback when the player is hurt: shakes the gun viewmodel (never the AR camera, which must
    /// follow the real phone), vibrates the phone and plays the hurt/death sounds.
    /// The red screen vignette is done by the HUD, which listens to the same PlayerHealth events.
    /// </summary>
    public class DamageFeedback : MonoBehaviour
    {
        [SerializeField] PlayerHealth health;
        [SerializeField] Transform viewmodel;
        [SerializeField, Min(0f)] float shakeStrength = 0.02f;
        [SerializeField, Min(0.01f)] float shakeDuration = 0.25f;

        Vector3 _viewmodelRest;
        float _shakeTimer;

        void Awake()
        {
            if (viewmodel) _viewmodelRest = viewmodel.localPosition;
        }

        void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        void OnDamaged(DamageInfo info)
        {
            _shakeTimer = shakeDuration;
            AudioManager.Instance?.Play(SoundId.PlayerHurt);
#if UNITY_ANDROID || UNITY_IOS
            if (GameSettings.Vibration) Handheld.Vibrate();
#endif
        }

        void OnDied() => AudioManager.Instance?.Play(SoundId.PlayerDeath);

        void LateUpdate()
        {
            if (!viewmodel) return;
            if (_shakeTimer > 0f)
            {
                _shakeTimer -= Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(_shakeTimer / shakeDuration) * shakeStrength;
                viewmodel.localPosition = _viewmodelRest + (Vector3)(Random.insideUnitCircle * k);
            }
            else
            {
                viewmodel.localPosition = Vector3.Lerp(viewmodel.localPosition, _viewmodelRest, 20f * Time.unscaledDeltaTime);
            }
        }
    }
}
