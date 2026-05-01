using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shtl.Mvvm.Samples
{
    public class ChatViewModel : AbstractViewModel
    {
        public readonly ReactiveValue<Action> OnAddMessageClicked = new();
        public readonly ReactiveValue<Action> OnAddBatchClicked = new();
        public readonly ReactiveValue<string> MessageCount = new();
    }

    public class ChatWidgetView : AbstractWidgetView<ChatViewModel>
    {
        [SerializeField] private Button _addMessageButton;
        [SerializeField] private Button _addBatchButton;
        [SerializeField] private TextMeshProUGUI _messageCountLabel;

        protected override void OnConnected()
        {
            Bind.From(_addMessageButton).To(ViewModel.OnAddMessageClicked);
            Bind.From(_addBatchButton).To(ViewModel.OnAddBatchClicked);
            ViewModel.MessageCount.Connect(value => _messageCountLabel.text = value);
        }
    }
}
