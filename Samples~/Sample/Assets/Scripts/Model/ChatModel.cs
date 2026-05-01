using System;
using System.Collections.Generic;

namespace Shtl.Mvvm.Samples
{
    public class ChatModel
    {
        public event Action<ChatMessageModel> OnMessageAdded;

        public readonly List<ChatMessageModel> Messages = new();

        private static readonly string[] Authors = { "Alice", "Bob", "Charlie", "Diana", "Eve" };

        private static readonly string[] Phrases =
        {
            "Hello!",
            "How are you?",
            "I'm fine, thanks!",
            "What are you working on?",
            "Just writing some code",
            "Sounds fun!",
            "Have you seen the new update?",
            "Not yet, is it good?",
            "Absolutely!",
            "Let me check it out",
        };

        public void AddMessage()
        {
            var message = new ChatMessageModel
            {
                Author = Authors[UnityEngine.Random.Range(0, Authors.Length)],
                Text = Phrases[UnityEngine.Random.Range(0, Phrases.Length)],
            };
            Messages.Add(message);
            OnMessageAdded?.Invoke(message);
        }

        public void AddBatch(int count)
        {
            for (var i = 0; i < count; i++)
            {
                AddMessage();
            }
        }
    }
}
