using System;
using UnityEngine;

namespace Shtl.Mvvm.Samples
{
    public sealed class ChatMessagesViewModel : AbstractViewModel
    {
        // W-05: avoid the "field initializer + object initializer" footgun in consumer code.
        // Previously Messages was initialised via a field initializer (= new(80f)) and then
        // re-assigned in EntryScreen with an object initializer (Messages = new(208)).
        // AbstractViewModel caches the reference to the 80f-instance via reflection in its
        // constructor BEFORE the object initializer overwrites the field — the cache then
        // diverges from the actual reference.
        //
        // Now the height is passed explicitly through the constructor so the consumer does not
        // re-assign the field after construction. The parameterless ctor is kept only to satisfy
        // the `new()` generic constraint on AbstractWidgetView<TViewModel>; the framework does
        // not actually invoke it (Connect receives an already-constructed instance).
        public ReactiveVirtualList<ChatMessageViewModel> Messages;

        public ChatMessagesViewModel() : this(80f) { }

        public ChatMessagesViewModel(float fixedHeight)
        {
            Messages = new ReactiveVirtualList<ChatMessageViewModel>(fixedHeight);
        }

        // Variable-height mode: each item's height is resolved per-index via the provider.
        // Provider must be stable (same index -> same height across calls) — see ReactiveVirtualList.
        public ChatMessagesViewModel(Func<int, float> heightProvider)
        {
            Messages = new ReactiveVirtualList<ChatMessageViewModel>(heightProvider);
        }
    }

    public class ChatMessagesWidgetView : AbstractWidgetView<ChatMessagesViewModel>
    {
        [SerializeField] private VirtualScrollRect _scrollRect;
        [SerializeField] private ChatMessageView _messagePrefab;

        protected override void OnConnected()
        {
            Bind.From(ViewModel.Messages).To(_messagePrefab, _scrollRect);
        }
    }
}
