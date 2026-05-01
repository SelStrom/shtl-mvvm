using TMPro;
using UnityEngine;

namespace Shtl.Mvvm.Samples
{
    public sealed class ChatMessageViewModel : AbstractViewModel
    {
        public ReactiveValue<string> Author = new();
        public ReactiveValue<string> Text = new();
    }

    public class ChatMessageView : AbstractWidgetView<ChatMessageViewModel>
    {
        [SerializeField] private TextMeshProUGUI _author;
        [SerializeField] private TextMeshProUGUI _text;

        protected override void OnConnected()
        {
            ViewModel.Author.Connect(value => _author.text = value);
            ViewModel.Text.Connect(value => _text.text = value);
        }
    }
}
