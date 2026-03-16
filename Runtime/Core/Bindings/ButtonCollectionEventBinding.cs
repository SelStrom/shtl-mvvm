using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.UI;

namespace Shtl.Mvvm
{
    public class ButtonCollectionEventBinding<TContext> : AbstractEventBinding<ButtonCollectionEventBinding<TContext>>
    {
        private Action<TContext> _buttonClickHandler;
        private TContext _context;
        private IReadOnlyCollection<Button> _buttons;

        private string _key;
        public string Key => _key ??= GetKey();

        public ButtonCollectionEventBinding<TContext> Connect(
            IReadOnlyCollection<Button> buttons,
            Action<TContext> buttonClickHandler
        )
        {
            _buttons = buttons;
            _buttonClickHandler = buttonClickHandler;

            return this;
        }

        public ButtonCollectionEventBinding<TContext> SetContext(TContext context)
        {
            _context = context;
            return this;
        }

        internal void OnActionValueChanged(TContext context)
        {
            var _ = SetContext(context);
        }

        public override void Activate()
        {
            foreach (var button in _buttons)
            {
                button.onClick.AddListener(ClickHandler);
            }
        }

        public override void Invoke()
        {
        }

        private void ClickHandler() => _buttonClickHandler?.Invoke(_context);

        public override void Dispose()
        {
            foreach (var button in _buttons)
            {
                button.onClick.RemoveListener(ClickHandler);
            }

            _context = default(TContext);
            _buttons = null;
            _buttonClickHandler = null;
        }

        private string GetKey()
        {
            var sb = new StringBuilder();
            foreach (var button in _buttons)
            {
                sb.Append(button.name);
                sb.Append(';');
            }
            return sb.ToString();
        }
    }
}
