using UnityEngine;

namespace Shtl.Mvvm.Samples
{
    public class VirtualListEntryScreen : MonoBehaviour
    {
        [SerializeField] private ChatWidgetView _chatView;
        [SerializeField] private ChatMessagesWidgetView _verticalWidgetView;
        [SerializeField] private ChatMessagesWidgetView _horizontalWidgetView;

        private ChatModel _model;
        private ChatViewModel _chatViewModel;
        private ChatMessagesViewModel _verticalMessages;
        private ChatMessagesViewModel _horizontalMessages;
        private VirtualListSampleWidget _widget;

        private void Start()
        {
            _model = new ChatModel();
            _chatViewModel = new ChatViewModel();
            // The widget owns the per-index height list, so it must exist before the vertical
            // ChatMessagesViewModel is built — we inject its GetVerticalHeight as the provider.
            _widget = new VirtualListSampleWidget();
            // W-05: pass the height contract through the ctor so we don't re-assign ReactiveVirtualList
            // after construction (an object initializer would break AbstractViewModel's reflection cache).
            _verticalMessages = new ChatMessagesViewModel(_widget.GetVerticalHeight);
            _horizontalMessages = new ChatMessagesViewModel(208f);

            _chatView.Connect(_chatViewModel);
            _verticalWidgetView.Connect(_verticalMessages);
            _horizontalWidgetView.Connect(_horizontalMessages);
            _widget.Connect(_model, _chatViewModel, _verticalMessages, _horizontalMessages);

            // Seed data.
            _model.AddBatch(3);
        }
    }
}
