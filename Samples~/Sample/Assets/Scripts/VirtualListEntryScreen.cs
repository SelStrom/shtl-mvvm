using UnityEngine;

namespace Shtl.Mvvm.Samples
{
    public class VirtualListEntryScreen : MonoBehaviour
    {
        [SerializeField] private ChatView _chatView;
        [SerializeField] private ChatMessagesView _verticalWidgetView;
        [SerializeField] private ChatMessagesView _horizontalWidgetView;

        private ChatModel _model;
        private ChatViewModel _chatViewModel;
        private ChatMessagesViewModel _verticalMessages;
        private ChatMessagesViewModel _horizontalMessages;

        private void Start()
        {
            _model = new ChatModel();
            _chatViewModel = new ChatViewModel();
            // W-05: pass the height through the ctor so we don't re-assign ReactiveVirtualList
            // after construction (an object initializer would break AbstractViewModel's reflection cache).
            _verticalMessages = new ChatMessagesViewModel(80f);
            _horizontalMessages = new ChatMessagesViewModel(208f);

            _chatView.Connect(_chatViewModel);
            _verticalWidgetView.Connect(_verticalMessages);
            _horizontalWidgetView.Connect(_horizontalMessages);
            new VirtualListSampleWidget().Connect(_model, _chatViewModel, _verticalMessages, _horizontalMessages);

            // Seed data.
            _model.AddBatch(3);
        }
    }
}
