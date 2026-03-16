using System;
using UnityEngine.UI;

namespace Shtl.Mvvm
{
    internal class ButtonEventSimpleBinding : AbstractEventBinding<ButtonEventSimpleBinding>
    {
        private Action _buttonClickHandler;
        private Button _button;

        public ButtonEventSimpleBinding Connect(Button button, Action buttonClickHandler)
        {
            _button = button;
            _buttonClickHandler = buttonClickHandler;

            return this;
        }

        public override void Activate()
        {
            _button.onClick.AddListener(ClickHandler);
        }

        public override void Invoke()
        {
        }

        private void ClickHandler() => _buttonClickHandler?.Invoke();

        public override void Dispose()
        {
            _button.onClick.RemoveListener(ClickHandler);

            _button = null;
            _buttonClickHandler = null;
        }
    }
}
