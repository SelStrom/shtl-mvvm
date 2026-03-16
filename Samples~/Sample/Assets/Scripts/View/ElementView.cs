using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shtl.Mvvm.Samples
{
    public sealed class ElementViewModel : AbstractViewModel
    {
        public readonly ReactiveValue<string> Title = new();
        public readonly ReactiveValue<int> Score = new();
        public readonly ReactiveAwaitable WaitForAnimation = new();

        public readonly ReactiveValue<bool> LockForAnimation = new();
        public readonly ReactiveValue<string> ButtonLabel = new();

        public readonly ReactiveValue<Action> OnButtonClicked = new();
    }

    public class ElementView : AbstractWidgetView<ElementViewModel>
    {
        [SerializeField] private Button _button;
        [SerializeField] private TextMeshProUGUI _score;

        protected override void OnConnected()
        {
            ViewModel.Score.Connect(value => _score.text = value.ToString());
            ViewModel.WaitForAnimation.Connect(StartAnimation);
            ViewModel.ButtonLabel.Connect(value => _button.GetComponentInChildren<TextMeshProUGUI>().text = value);
            ViewModel.LockForAnimation.Connect(value =>
            {
                _button.interactable = !value;
                _button.gameObject.GetComponent<Image>().color = value ? Color.red : Color.blue;
                _button.GetComponentInChildren<TextMeshProUGUI>().color = value ? Color.black : Color.white;
            });

            Bind.From(_button).To(ViewModel.OnButtonClicked);
        }

        private async void StartAnimation(TaskCompletionSource<bool> promise)
        {
            Debug.Log($"StartAnimation in '{name}'");
            // e.g. _animator.SetState("start");
            await Task.Delay(2000);
            promise.TrySetResult(true);
        }
    }
}
