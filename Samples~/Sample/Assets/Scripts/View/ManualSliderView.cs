using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shtl.Mvvm.Samples
{
    public class ManualSliderView : AbstractWidgetView<SliderViewModel>
    {
        [SerializeField] private Slider _slider;
        [SerializeField] private TextMeshProUGUI _value;

        protected override void OnConnected()
        {
            _slider.onValueChanged.AddListener(OnSliderValueChanged);
            
            ViewModel.Value.Connect(x =>
            {
                _value.text = x.ToString("0.00");
            });
        }

        private void OnSliderValueChanged(float value) => ViewModel.OnValueChanged.Value?.Invoke(value);

        protected override void OnDisposed()
        {
            _slider.onValueChanged.RemoveListener(OnSliderValueChanged);
        } 
    }
}
