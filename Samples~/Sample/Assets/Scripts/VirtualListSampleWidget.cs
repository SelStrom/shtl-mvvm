using System.Collections.Generic;
using UnityEngine;

namespace Shtl.Mvvm.Samples
{
    public class VirtualListSampleWidget
    {
        // Random per-message height range for the vertical list demo.
        private const float MinHeight = 50f;
        private const float MaxHeight = 150f;

        private ChatModel _model;
        private IEventBindingContext _bindingContext;
        private ChatViewModel _chatVm;
        private ChatMessagesViewModel _verticalMessagesVm;
        private ChatMessagesViewModel _horizontalMessagesVm;

        // Heights are cached per index so the height provider is stable across repeated calls
        // from the virtualization layer (PositionView queries it every frame for visible items).
        private readonly List<float> _verticalHeights = new();

        public float GetVerticalHeight(int index) => _verticalHeights[index];

        public void Connect(ChatModel model, ChatViewModel chatVm, ChatMessagesViewModel verticalMessagesVm, ChatMessagesViewModel horizontalMessagesVm)
        {
            _horizontalMessagesVm = horizontalMessagesVm;
            _verticalMessagesVm = verticalMessagesVm;
            _chatVm = chatVm;
            _model = model;

            _chatVm.OnAddMessageClicked.Value = OnAddMessage;
            _chatVm.OnAddBatchClicked.Value = OnAddBatch;

            _model.OnMessageAdded += OnMessageAdded;

            UpdateCount();
        }

        private void OnAddMessage()
        {
            _model.AddMessage();
            UpdateCount();
        }

        private void OnAddBatch()
        {
            _model.AddBatch(100);
            UpdateCount();
        }

        private void OnMessageAdded(ChatMessageModel message)
        {
            // Push the cached height first: the binding queries GetItemHeight(newIndex) as soon
            // as Messages.Add invokes its added-callback, so the entry must already be in place.
            _verticalHeights.Add(Random.Range(MinHeight, MaxHeight));
            _verticalMessagesVm.Messages.Add(new ChatMessageViewModel()
            {
                Author = { Value = message.Author },
                Text = { Value = message.Text }
            });
            _horizontalMessagesVm.Messages.Add(new ChatMessageViewModel()
            {
                Author = { Value = message.Author },
                Text = { Value = message.Text }
            });
        }

        private void UpdateCount()
        {
            _chatVm.MessageCount.Value = $"Messages: {_model.Messages.Count}";
        }
    }
}
