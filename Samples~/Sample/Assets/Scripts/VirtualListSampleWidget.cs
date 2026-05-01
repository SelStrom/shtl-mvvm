namespace Shtl.Mvvm.Samples
{
    public class VirtualListSampleWidget
    {
        private ChatModel _model;
        private IEventBindingContext _bindingContext;
        private ChatViewModel _chatVm;
        private ChatMessagesViewModel _verticalMessagesVm;
        private ChatMessagesViewModel _horizontalMessagesVm;

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
