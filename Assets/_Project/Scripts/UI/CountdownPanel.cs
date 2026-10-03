using TMPro;
using UnityEngine;

namespace Ricochet.UI
{
    /// <summary>Big 3-2-1-GO! with a punch-scale animation on each number.</summary>
    public class CountdownPanel : UIPanel
    {
        [SerializeField] TMP_Text numberText;
        [SerializeField] Color numberColor = new Color(0.2f, 1f, 1f);
        [SerializeField] Color goColor = new Color(1f, 0.2f, 0.8f);

        float _punch;

        protected override void OnInitialize() => Game.CountdownTicked += OnTick;

        void OnTick(int number)
        {
            numberText.text = number > 0 ? number.ToString() : "GO!";
            numberText.color = number > 0 ? numberColor : goColor;
            _punch = 1f;
        }

        void Update()
        {
            _punch = Mathf.MoveTowards(_punch, 0f, Time.unscaledDeltaTime * 2.5f);
            numberText.rectTransform.localScale = Vector3.one * (1f + _punch * _punch * 0.6f);
        }
    }
}
