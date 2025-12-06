using UnityEngine;
using UnityEngine.UI;

namespace Viking.UI
{
    /// <summary>
    /// Adds a subtle pulsing animation to connection elements for a "living" feel.
    /// Viking-themed - like runes glowing with ancient power.
    /// </summary>
    public class ConnectionPulse : MonoBehaviour
    {
        private Image _image;
        private Color _baseColor;
        private float _pulseOffset;
        private float _pulseSpeed = 1.5f;
        private float _pulseIntensity = 0.3f;

        private void Awake()
        {
            _image = GetComponent<Image>();
            if (_image != null)
            {
                _baseColor = _image.color;
            }
            // Random offset so not all dots pulse in sync
            _pulseOffset = Random.value * Mathf.PI * 2f;
        }

        private void Update()
        {
            if (_image == null) return;

            // Subtle sine wave pulse
            float pulse = Mathf.Sin(Time.time * _pulseSpeed + _pulseOffset);
            float alpha = _baseColor.a + pulse * _pulseIntensity * _baseColor.a;
            float brightness = 1f + pulse * 0.15f;

            _image.color = new Color(
                _baseColor.r * brightness,
                _baseColor.g * brightness,
                _baseColor.b * brightness,
                Mathf.Clamp01(alpha)
            );
        }

        /// <summary>
        /// Configure the pulse parameters.
        /// </summary>
        public void Configure(float speed, float intensity)
        {
            _pulseSpeed = speed;
            _pulseIntensity = intensity;
        }
    }
}
