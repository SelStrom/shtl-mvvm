using System;
using System.Collections.Generic;

namespace Shtl.Mvvm.Samples
{
    public class SampleModel
    {
        public event Action<ElementModel> OnElementAdded;
        public event Action<int> OnElementRemoved;

        public ObservableValue<float> Score;
        public ObservableValue<int> IntScore;
        public List<ElementModel> Elements;

        public void AddNewElement()
        {
            var element = new ElementModel
            {
                Score = new ObservableValue<int>((int)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            };
            Elements.Add(element);
            OnElementAdded?.Invoke(element);
        }

        public void RemoveRandomElement()
        {
            if (Elements.Count == 0)
            {
                return;
            }
            var index = UnityEngine.Random.Range(0, Elements.Count);
            Elements.RemoveAt(index);
            OnElementRemoved?.Invoke(index);
        }

        public void ClearAll()
        {
            Elements.Clear();
        }
    }
}
