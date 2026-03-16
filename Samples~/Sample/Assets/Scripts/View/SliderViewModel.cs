using System;

namespace Shtl.Mvvm.Samples
{
    public class SliderViewModel : AbstractViewModel
    {
        public readonly ReactiveValue<float> Value = new();
        public readonly ReactiveValue<Action<float>> OnValueChanged = new();
    }
}
