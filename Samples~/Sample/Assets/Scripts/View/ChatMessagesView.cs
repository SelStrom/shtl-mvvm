using UnityEngine;

namespace Shtl.Mvvm.Samples
{
    public sealed class ChatMessagesViewModel : AbstractViewModel
    {
        // W-05: исключаем footgun "field initializer + объект-инициализатор" в коде потребителя.
        // Раньше Messages инициализировалось через field initializer (= new(80f)), а в EntryScreen
        // переприсваивалось object initializer'ом (Messages = new(208)). AbstractViewModel
        // в своём конструкторе рефлексией кэширует ссылку на 80f-инстанс ДО того, как object
        // initializer перезапишет поле -- кэш разъезжается с актуальной ссылкой.
        //
        // Теперь высота передаётся явно через конструктор, чтобы потребитель не переприсваивал
        // поле после конструирования. Парам-less ctor сохранён только для удовлетворения
        // generic-constraint `new()` в AbstractWidgetView<TViewModel>; фактически фреймворк
        // его не вызывает (Connect принимает уже сконструированный экземпляр).
        public ReactiveVirtualList<ChatMessageViewModel> Messages;

        public ChatMessagesViewModel() : this(80f) { }

        public ChatMessagesViewModel(float fixedHeight)
        {
            Messages = new ReactiveVirtualList<ChatMessageViewModel>(fixedHeight);
        }
    }

    public class ChatMessagesView : AbstractWidgetView<ChatMessagesViewModel>
    {
        [SerializeField] private VirtualScrollRect _scrollRect;
        [SerializeField] private ChatMessageView _messagePrefab;

        protected override void OnConnected()
        {
            Bind.From(ViewModel.Messages).To(_messagePrefab, _scrollRect);
        }
    }
}
