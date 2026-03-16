using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shtl.Mvvm.Samples
{
    public class AutoSliderView : AbstractWidgetView<SliderViewModel>
    {
        [SerializeField] private Slider _slider;
        [SerializeField] private TextMeshProUGUI _value;

        protected override void OnConnected()
        {
            ViewModel.Value.Connect(x =>
            {
                _value.text = x.ToString("0.00");
                _slider.value = x;
            });
        }
    }
}
