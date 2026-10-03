using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ricochet.UI
{
    /// <summary>Guides the player through scanning the floor and placing the beacon.</summary>
    public class ScanPanel : UIPanel
    {
        const string ScanText = "Move your phone slowly\nto scan the floor";
        const string PlaceText = "Aim at the floor and\ntap to place the beacon";

        [SerializeField] TMP_Text instruction;
        [SerializeField] RectTransform phoneIcon;
        [SerializeField] Button backButton;

        float _time;

        protected override void OnInitialize()
        {
            backButton.onClick.AddListener(Game.BackToMenu);
            Game.Placement.ReticleValidChanged += SetValid;
        }

        protected override void OnShow() => SetValid(Game.Placement.ReticleValid);

        void SetValid(bool valid)
        {
            instruction.text = valid ? PlaceText : ScanText;
            if (phoneIcon) phoneIcon.gameObject.SetActive(!valid);
        }

        void Update()
        {
            if (!phoneIcon) return;
            // Phone icon sways left/right to show "move your phone".
            _time += Time.unscaledDeltaTime;
            phoneIcon.anchoredPosition = new Vector2(Mathf.Sin(_time * 2f) * 90f, phoneIcon.anchoredPosition.y);
            phoneIcon.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_time * 2f) * -12f);
        }
    }
}
